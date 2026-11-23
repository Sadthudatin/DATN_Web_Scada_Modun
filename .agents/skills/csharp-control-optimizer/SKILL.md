---
name: csharp-control-optimizer
description: Tối ưu C# Control Service của đồ án (ASP.NET Core, EF Core, MQTTnet, SignalR) — săn N+1, batch DB/Influx, gỡ block MQTT callback bằng Channel, reconnect + resubscribe, AsNoTracking, worker lọc ở SQL. Chỉ sửa control-csharp/src, không đụng firmware/web.
---

# C# Control Optimizer

## Khi nào kích hoạt
- "tối ưu app điều khiển", "app chậm", "lag realtime", "N+1", "MQTT drop/ack mất", "review C# control".
- Trước khi sửa: đọc `docs/specs/CLAUDE_CODE_WEB_SCADA_SPEC.md` (contract) và `control-csharp/src/Control.Domain/MqttContracts.cs`.

## Bản đồ entrypoint
| Vùng | File |
|---|---|
| DI + hosted services | `control-csharp/src/Control.Api/Program.cs` |
| Ingest telemetry/status/ack | `src/Control.Api/BackgroundServices/MqttIngestionService.cs` |
| Worker timeout/offline | `src/Control.Api/BackgroundServices/CommandTimeoutService.cs` (2 class) |
| MQTT client | `src/Control.Infrastructure/Mqtt/MqttService.cs` |
| Dedup | `src/Control.Infrastructure/Telemetry/TelemetryDeduplicator.cs` |
| Store | `src/Control.Infrastructure/Telemetry/{TelemetryStore,InfluxTelemetryStore}.cs` |
| Alarm/Command | `src/Control.Api/Services/{AlarmService,CommandService}.cs` |
| Read API | `src/Control.Api/Controllers/*.cs` |

## Checklist bắt buộc (evidence-first)
1. **N+1**: không để `await db...` trong `foreach`/`for` trên tag/command. Gom `Where(x => ids.Contains(x.Id))` một lần, evaluate in-memory.
2. **Ghi DB**: một `SaveChangesAsync` cho mỗi telemetry message, không per-tag.
3. **Influx**: dùng batch write (nhiều point/lần), không 1 HTTP/point.
4. **MQTT callback**: `OnMessageAsync` chỉ parse + `Channel.Writer.TryWrite`; consumer nền xử lý DB. Channel bounded, `FullMode` tường minh.
5. **Dedup**: `TelemetryDeduplicator` phải bounded/purge theo `(gateway,boot)`; boot cũ bị loại.
6. **Read API**: `AsNoTracking()` + `OrderByDescending(...).Take(n)` ở SQL; không `Take(500)` rồi sort in-memory.
7. **Worker**: lọc ở SQL (`LastSeen < cutoff`, `Status == "pending"`); không load toàn bảng rồi `Where` trong RAM.
8. **Reconnect**: `MqttService` phải connect lại và subscribe lại sau `DisconnectedAsync` (broker restart / mất session). Subscribe idempotent.
9. **CancellationToken**: truyền xuyên suốt query/`SaveChanges`/`SendAsync`; không `async void` trong service.
10. **SignalR**: gộp batch thay vì gửi từng tag; cân nhắc group theo gateway/plc. Không `Clients.All` cho dữ liệu nếu cần phân quyền.

## Allowlist
- Được sửa: `control-csharp/src/Control.Api/**`, `control-csharp/src/Control.Infrastructure/**`, `control-csharp/tests/**`.
- Không sửa: `*.csproj`, `appsettings*.json`, `firmware/**`, `web-scada/**`, `scada/**` — trừ khi task nêu rõ.

## Acceptance
- Có SDK: `dotnet build control-csharp/Control.slnx` + `dotnet test control-csharp/tests`.
- Không có SDK: ghi rõ **"chưa build verify"**, kèm diff + lý do; cấm tuyên bố "pass".
- Bằng chứng hiệu năng: số query/telemetry trước–sau (log EF/`ToQueryString`), hoặc log ingest; không nhận cảm tính.

## Circuit breaker
Tối đa 3 vòng sửa. Quá 3 → dừng, xuất traceback + giả thuyết + đề xuất.

## Output
`Files modified` + `Diff summary` + `Acceptance log` + `Rủi ro còn lại`.
