---
name: scada-realtime-ui
description: Web SCADA React/TS + WPF Operator — SignalR realtime, cập nhật state theo payload thay vì reload toàn bộ, throttle telemetry, reconnect/resubscribe, HttpClient dùng lại. Không lo thẩm mỹ (dùng ui-ux).
---

# SCADA Realtime UI

## Khi nào kích hoạt
- "web chậm/giật", "realtime không cập nhật", "mất kết nối hub", "WPF treo", "sửa dashboard SCADA".
- Đọc `docs/specs/README_WEBSCADA.md` trước.

## Phạm vi
| Vùng | File |
|---|---|
| Web | `web-scada/src/{App.tsx,useScadaHub.ts,api.ts,types.ts,auth.tsx}` |
| WPF | `control-csharp/src/Control.Desktop/{MainWindow.xaml.cs,ApiClient.cs}` |
| Hub server | `control-csharp/src/Control.Api/Hubs/ScadaHub.cs` |

## Checklist
- [ ] Mỗi event SignalR cập nhật đúng slice state; **không** gọi `LoadAllAsync`/refetch toàn bộ mỗi telemetry.
- [ ] `ApiClient` dùng **một** `HttpClient` (field/DI), không `new HttpClient` mỗi request.
- [ ] `WithAutomaticReconnect` + handler `Reconnected` → resubscribe/fetch snapshot một lần.
- [ ] Telemetry tần suất cao → throttle/batch render (4–10 Hz) hoặc dùng payload để update row.
- [ ] Auth: token cho hub + REST; 401 → logout sạch, dừng timer.
- [ ] Không leak subscription/timer khi logout/đóng cửa sổ (`StopHub`, `_timer.Stop`).
- [ ] Full reload chỉ khi: reconnect, user bấm Refresh, hoặc đổi bộ lọc.
- [ ] Hiển thị trạng thái LIVE/POLLING/ERR rõ ràng; không nuốt exception.

## Acceptance
- Web: `cd web-scada && npm run build`; mở app, xem Network/SignalR, console 0 error.
- WPF: `dotnet build control-csharp/Control.slnx` (nếu có SDK); quan sát UI cập nhật không reload.
- Không có toolchain: ghi rõ "chưa verify runtime".

## Output
`Files modified` + `Luồng dữ liệu trước/sau` + `Bằng chứng` + `Rủi ro`.
