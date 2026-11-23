---
name: mqtt-contract-guardian
description: Gác contract MQTT giữa gateway ESP32, simulator và C# Control — topic, payload schema v1, QoS/retain, boot_id/seq, cmd→ack, telemetry_ack. Read-only mặc định; chỉ sửa khi task chỉ định.
---

# MQTT Contract Guardian

## Khi nào kích hoạt
- "gateway không lên dữ liệu", "ack không về", "command kẹt pending", "lệch topic/payload", "thêm tag mới", "sửa simulator".
- Trước khi kết luận: đối chiếu 4 nguồn, không đoán.

## Nguồn chân lý
| Vai | File |
|---|---|
| C# topics + record | `control-csharp/src/Control.Domain/MqttContracts.cs` |
| C# subscribe/publish | `src/Control.Api/BackgroundServices/MqttIngestionService.cs`, `Services/CommandService.cs`, `Controllers/TagsController.cs` |
| Simulator | `control-csharp/src/Control.Simulator/Program.cs` |
| Firmware (legacy) | `firmware/main/app_config.h`, `mqtt_client.c` |
| Spec | `docs/specs/CLAUDE_CODE_WEB_SCADA_SPEC.md` |

## Checklist
- [ ] Topic đúng tập: `plc/<gw>/{telemetry,status,ack,cmd,config,telemetry_ack}`; không thừa/thiếu dấu `/`.
- [ ] Server chỉ subscribe `plc/+/telemetry`, `plc/+/status`, `plc/+/ack`; không subscribe `telemetry_ack` (tránh tự ack chính mình).
- [ ] Payload `schema: "v1"`, field snake_case, `timestamp` UTC ISO-8601, `gateway_id`/`plc_id` khớp topic.
- [ ] `boot_id` + `seq` tăng đơn điệu; dedup theo cặp này; seq reset khi boot mới.
- [ ] QoS 1. Retain chỉ cho `status` và `config`; **không** retain telemetry/cmd/ack.
- [ ] `cmd` có `cmd_id`; `ack` phải khớp `cmd_id` **và** đúng gateway; mismatch → bỏ + log.
- [ ] Server publish `telemetry_ack {status:"stored"}` sau khi lưu (kể cả duplicate) — gateway subscribe topic đó.
- [ ] `ParseValue` xử lý bool/number/string/null an toàn; quality (`good/bad/stale`) có mặt.
- [ ] Simulator phản hồi đúng `cmd` → `ack` để test timeout worker.

## Output
Bảng: `Topic | Producer | Consumer | Payload | QoS/Retain | Lệch? | Sửa ở đâu`.
Read-only: **không sửa code**, chỉ đề xuất diff/đường dẫn dòng.

## Circuit breaker
Sau 3 vòng đối chiếu vẫn lệch → dừng, báo rõ nguồn nào mâu thuẫn và đề xuất chốt một nguồn.
