# AGENTS.md — Web SCADA / C# Control Service (Đồ án tốt nghiệp)

Kế thừa luật chung ở `C:\Users\Admin\.agents\AGENTS.md`. File này chỉ bổ sung mặc định riêng của repo này.

## 1. Trạng thái dự án
- Trọng tâm hiện tại: **Web SCADA** (`web-scada/`) + backend **C# Control Service** (`control-csharp/`) + **deploy** (`deploy/`).
- Gateway ESP32, PLC thật, firmware/hardware: **loại bỏ khỏi phạm vi**; simulator là nguồn telemetry. WPF Operator ngoài phạm vi.
- Target `net10.0`. Máy dev có thể **không có .NET SDK**: không được tuyên bố "build/test pass" nếu chưa chạy lệnh.

## 2. Mặc định điều phối sub-agent
Với việc không tầm thường (≥2 file hoặc ≥2 module), mặc định:
1. Lead chốt DAG + **Allowlist file** cho từng nhánh trước khi spawn.
2. Chạy song song các sub-agent theo roster; mỗi agent một vùng file, **không trùng file**.
3. Supervisor (`test-qa-engineer`) nghiệm thu bằng log; thiếu bằng chứng = REJECTED.
4. Circuit breaker **3 vòng/agent**; quá 3 → dừng, xuất traceback + giả thuyết.
5. Merge tuần tự; không để 2 agent sửa cùng file.

## 3. Roster sub-agent
| Sub-agent | Dùng khi |
|---|---|
| `doc-researcher` | Đọc spec/docs/API trước khi sửa; chống ảo giác |
| `clean-coder` | Viết/refactor code trong Allowlist |
| `smart-debugger` | Traceback, lỗi khó tái hiện |
| `test-qa-engineer` | Viết test + nghiệm thu bằng chứng |
| `task-automation` | Docker/CI/script an toàn |
| `csharp-control-optimizer` | N+1, batch DB/Influx, MQTT ingestion, SignalR, worker |
| `mqtt-contract-guardian` | Lệch topic/payload/ack simulator ↔ C# |
| `scada-realtime-ui` | Web realtime, reconnect, state, HttpClient |
| `scada-deploy-verifier` | Compose/port/env/smoke end-to-end |

## 4. Lệnh chuẩn
```bash
dotnet build control-csharp/Control.slnx
dotnet test control-csharp/tests
cd web-scada && npm run build
cd deploy && docker compose up -d --build
```

## 5. Vùng cấm mặc định
- `firmware/`, `hardware/`, `scada/` (legacy), `docs/thesis/` — chỉ khi task chỉ định.
- Không commit trừ khi được yêu cầu. Không sửa `.csproj`/lockfile nếu không cần.
