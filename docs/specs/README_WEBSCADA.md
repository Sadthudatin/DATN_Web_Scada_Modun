# Web SCADA + C# Control Service

Hệ thống Web SCADA cho PLC Delta DVP qua IoT Gateway MQTT.

- **Phần mềm điều khiển:** C# / ASP.NET Core / WPF.
- **Web SCADA:** React + TypeScript (có thể thay bằng ngôn ngữ khác, miễn gọi API C#).
- **Gateway:** ESP32-S3 bên ngoài; simulator dùng để phát triển và kiểm thử.
- **Phạm vi MVP:** Delta DVP-24ES2R, giám sát, điều khiển, lịch sử, alarm, Store-and-Forward.
- **Không có:** nạp PLC từ xa, COM ảo, TCP 8899, ISPSoft bridge.

## Kiến trúc

```text
PLC Delta DVP --RS-485/Modbus RTU--> Gateway ESP32-S3
      Gateway --MQTT--> Mosquitto --> C# Control Service
                                     |-- PostgreSQL (cấu hình, alarm, command, audit)
                                     |-- InfluxDB (telemetry)
                                     |-- API + SignalR --> Web SCADA (React/TS)
                                     |-- API + SignalR --> WPF Operator (C#)
```

Chỉ **C# Control Service** publish `plc/<id>/cmd`. Web/WPF gửi command request qua API C#.

## Cấu trúc

```text
control-csharp/       # C# control service + WPF + simulator + tests
web-scada/            # Web SCADA React/TypeScript
deploy/               # Docker Compose, Mosquitto config
tools/dev-broker/     # MQTT broker dev bằng Aedes (không dùng production)
```

## Chạy bằng Docker

Yêu cầu: Docker Desktop đang chạy.

```bash
cd deploy
docker compose up -d --build
```

| Dịch vụ | URL |
|---|---|
| Web SCADA | http://localhost:5080 |
| C# API | http://localhost:5081 |
| MQTT | `localhost:1884` |
| InfluxDB | http://localhost:8087 |

Tài khoản dev:

```text
admin / Admin123!
```

Đổi mật khẩu, JWT key, token InfluxDB và cấu hình MQTT trước khi dùng thật. Mosquitto trong `deploy/` đang để anonymous cho môi trường dev.

## Chạy local không cần Docker

Cần .NET SDK 10, Node.js và npm.

### 1. MQTT broker dev

```bash
cd tools/dev-broker
npm install
npm start
```

Broker nghe cổng `1884`.

### 2. C# Control API

```bash
cd control-csharp
dotnet build
dotnet test

# Windows PowerShell
$env:Database__Provider="Sqlite"
$env:ConnectionStrings__Default="Data Source=control-dev.db"
$env:Mqtt__Host="localhost"
$env:Mqtt__Port="1884"
$env:Influx__Enabled="false"
dotnet run --project src/Control.Api --urls http://localhost:5081
```

`Influx__Enabled=false` dùng bộ nhớ tạm cho telemetry; khi Docker chạy thì dùng InfluxDB.

### 3. Simulator

```bash
cd control-csharp
$env:MQTT_HOST="localhost"
$env:MQTT_PORT="1884"
dotnet run --project src/Control.Simulator
```

Simulator phát telemetry 500 ms, status retained và trả ACK cho command.

### 4. Web SCADA

```bash
cd web-scada
npm install
npm run dev
```

Mở http://localhost:5173. Vite proxy `/api` và `/hubs` sang API `5081`.

### 5. WPF Operator

```bash
cd control-csharp
dotnet run --project src/Control.Desktop
```

Nhập API URL `http://localhost:5081`, đăng nhập bằng tài khoản dev. App dùng API + SignalR, không nối trực tiếp MQTT/DB.

## Build và kiểm thử

```bash
# C#
dotnet build control-csharp/Control.slnx -c Release
dotnet test control-csharp/Control.slnx -c Release

# Web
cd web-scada
npm ci
npm run build
```

## API chính

| Method | Endpoint | Mục đích |
|---|---|---|
| POST | `/api/auth/login` | Đăng nhập, nhận JWT |
| GET | `/api/gateways` | Danh sách Gateway |
| GET | `/api/tags` | Tag + giá trị mới nhất |
| PUT | `/api/tags/{id}` | Sửa cấu hình tag |
| GET | `/api/telemetry?tagId=&from=&to=` | Lịch sử |
| GET | `/api/alarms` | Alarm |
| POST | `/api/alarms/{id}/ack` | Xác nhận alarm |
| GET/POST | `/api/commands` | Danh sách / gửi lệnh |
| GET | `/api/audit` | Audit |

SignalR hub: `/hubs/scada`.

## MQTT topic

| Topic | Chiều |
|---|---|
| `plc/<gateway_id>/telemetry` | Gateway → Control |
| `plc/<gateway_id>/status` | Gateway → Control |
| `plc/<gateway_id>/config` | Control → Gateway |
| `plc/<gateway_id>/cmd` | Control → Gateway |
| `plc/<gateway_id>/ack` | Gateway → Control |
| `plc/<gateway_id>/telemetry_ack` | Control → Gateway |

Payload dùng schema v1; xem `control-csharp/src/Control.Domain/MqttContracts.cs`.

## Giới hạn hiện tại

- MVP dùng PLC Delta DVP; chưa triển khai driver Mitsubishi/Siemens.
- Gateway firmware không nằm trong repo này; simulator thay thế khi phát triển.
- Chưa có TLS/HTTPS production; cần bổ sung reverse proxy, chứng chỉ và MQTT credential.
- Mosquitto dev để anonymous; phải cấu hình tài khoản trước khi công khai.
- `Influx__Enabled=false` chỉ dùng cho dev; dữ liệu lịch sử sẽ nằm trong bộ nhớ và mất khi restart.
- Web/WPF chưa có đầy đủ tính năng quản trị người dùng; admin được seed từ cấu hình.
- Không có nạp chương trình PLC từ xa hoặc COM ảo.
