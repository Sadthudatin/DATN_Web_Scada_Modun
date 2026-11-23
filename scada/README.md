# Cụm SCADA Gateway cho PLC Delta DVP-24ES2

Cụm host SCADA chạy bằng Docker Compose, gồm 6 service, phục vụ đồ án tốt nghiệp Cơ điện tử:
thu thập dữ liệu từ các gateway IoT (PLC Delta DVP-24ES2) qua MQTT, lưu trữ và hiển thị.

Mỗi mạch có một nhánh topic riêng `plc/<mã-mạch>/...`, nên thêm/bớt mạch không cần sửa cấu hình
của Telegraf hay Web UI.

Luồng dữ liệu chính:

```
Gateway PLC --MQTT (plc/<id>/telemetry)--> Mosquitto --> Telegraf --> InfluxDB --> Grafana
                                               |
                                               +--> Node-RED (simulator / xử lý)
                                               |
                                               +--> Web UI (MQTT over WebSocket, plc/<id>/cmd -> plc/<id>/ack)
```

## 1. Thành phần và cổng

| Service | Image | Vai trò |
|---|---|---|
| `mosquitto` | `eclipse-mosquitto:2` | MQTT broker trung tâm, có cả TCP và WebSocket |
| `web` | `nginx:1.27-alpine` | Web UI SCADA (HTML/CSS/JS thuần) và proxy `/mqtt` |
| `nodered` | `nodered/node-red:4.0` | Node-RED, chứa flow simulator và xử lý lệnh |
| `influxdb` | `influxdb:2.7` | Cơ sở dữ liệu chuỗi thời gian, bucket `plc` |
| `telegraf` | `telegraf:1.31` | Cầu nối MQTT `plc/+/telemetry` → InfluxDB |
| `grafana` | `grafana/grafana:11.2.0` | Dashboard giám sát |

| Cổng host | Giao thức | Dịch vụ | Mục đích |
|---|---|---|---|
| 1883 | MQTT TCP | `mosquitto` | Kênh dữ liệu chính cho gateway PLC, Telegraf, `mosquitto_sub`/`mosquitto_pub` |
| 9001 | MQTT over WebSocket | `mosquitto` | Cho trình duyệt; Web UI kết nối qua nginx tại `/mqtt` |
| 8088 | HTTP | `web` (nginx) | Web UI SCADA, đồng thời proxy `/mqtt` sang `mosquitto:9001` |
| 1880 | HTTP | `nodered` | Trình soạn thảo Node-RED |
| 3000 | HTTP | `grafana` | Dashboard Grafana |
| 8086 | HTTP | `influxdb` | API và giao diện InfluxDB (`/health` để kiểm tra) |

Tất cả service nằm trong Docker network `datn-scada` (do Compose tạo). Container dùng tên cố định:
`mosquitto`, `web`, `nodered`, `influxdb`, `telegraf`, `grafana`.

> Trên máy này cổng `8080` đã bị một ứng dụng khác chiếm, nên Web UI dùng `8088`.
> Nếu muốn đổi, sửa đồng thời `docker-compose.yml` (ports), `scada.bat` (URL) và
> `scripts/healthcheck.ps1` (dòng `Web UI`).

## 2. Tài khoản mặc định

Giá trị dưới đây lấy từ `.env`. Đây là **mật khẩu demo cho mạng LAN nội bộ**, phải đổi hết
trước khi công khai cụm ra ngoài (Internet hoặc mạng không tin cậy).

| Thành phần | Tài khoản | Mật khẩu | Dùng cho |
|---|---|---|---|
| Mosquitto | `gateway` | `gw-pass-2026` | Gateway PLC thật: publish telemetry, subscribe `plc/<id>/cmd` |
| Mosquitto | `web` | `web-pass-2026` | Web UI kết nối MQTT over WebSocket |
| Mosquitto | `nodered` | `nr-pass-2026` | Node-RED |
| Mosquitto | `telegraf` | `tg-pass-2026` | Telegraf đọc `plc/+/telemetry` |
| InfluxDB | `admin` | `datn-influx-2026` | Giao diện/API InfluxDB |
| Grafana | `admin` | `datn-grafana-2026` | Đăng nhập dashboard |

Ngoài ra `.env` còn có `INFLUX_TOKEN` (token API InfluxDB) dùng cho Telegraf và datasource Grafana.
Không đưa `.env` lên kho công khai. Khi đổi mật khẩu/token phải cập nhật đồng bộ `.env`,
`mosquitto/config/passwd`, cấu hình Telegraf và datasource Grafana.

## 3. Chuẩn bị lần đầu: tạo password file cho Mosquitto

Mosquitto cấu hình `allow_anonymous false` và đọc `mosquitto/config/passwd`. Nếu file này chưa có,
container sẽ thoát ngay. Tạo file **trước** lần `up` đầu tiên, tại thư mục `scada`:

```powershell
# Tạo file passwd và user gateway
docker run --rm -it -v "$PWD/mosquitto/config:/m" eclipse-mosquitto:2 mosquitto_passwd -c /m/passwd gateway

# Thêm các user còn lại (nhập mật khẩu tương ứng trong bảng trên)
docker run --rm -it -v "$PWD/mosquitto/config:/m" eclipse-mosquitto:2 mosquitto_passwd /m/passwd web
docker run --rm -it -v "$PWD/mosquitto/config:/m" eclipse-mosquitto:2 mosquitto_passwd /m/passwd nodered
docker run --rm -it -v "$PWD/mosquitto/config:/m" eclipse-mosquitto:2 mosquitto_passwd /m/passwd telegraf
```

Có thể thêm user không tương tác bằng `-b`:

```powershell
docker run --rm -v "$PWD/mosquitto/config:/m" eclipse-mosquitto:2 mosquitto_passwd -b /m/passwd web web-pass-2026
```

Sau khi sửa `passwd`, khởi động lại broker để nạp lại file:

```powershell
docker compose restart mosquitto
```

Kiểm tra nhanh nội dung file (mỗi dòng một user, dạng `user:hash`):

```powershell
Get-Content .\mosquitto\config\passwd
```

## 4. Vận hành

Chạy các lệnh tại thư mục `scada`:

```powershell
docker compose up -d                  # khởi động toàn bộ cụm
docker compose ps                     # xem trạng thái container
docker compose logs -f mosquitto      # theo dõi log broker (Ctrl+C để thoát)
docker compose logs -f telegraf       # log Telegraf khi nghi thiếu dữ liệu
docker compose logs -f grafana        # log Grafana khi datasource lỗi
docker compose down                   # dừng và xóa container, giữ dữ liệu trên đĩa
```

Dữ liệu được giữ trong các thư mục bind mount (`mosquitto/data`, `influxdb`, `grafana-data`...),
nên `docker compose down` không làm mất dữ liệu. Chỉ xóa thủ công khi thật sự muốn làm lại từ đầu.

### 4.1. Bật/tắt bằng `scada.bat`

Nháy đúp `scada.bat` để mở menu, hoặc dùng tham số:

| Lệnh | Tác dụng |
|---|---|
| `scada.bat start` | Bật Docker Desktop nếu chưa chạy, chờ engine sẵn sàng, `docker compose up -d`, mở Web UI |
| `scada.bat stop` | `docker compose down` — tắt container và network, giữ nguyên dữ liệu |
| `scada.bat restart` | Tắt rồi bật lại |
| `scada.bat status` | In trạng thái container và chạy `scripts\healthcheck.ps1` |
| `scada.bat stopall` | `docker compose down` + thoát Docker Desktop |

`start` tự mở trình duyệt tới Web UI. Docker Desktop **không** tự khởi động sau reboot —
sau khi bật máy phải chạy `scada.bat start` (hoặc mở Docker Desktop thủ công).

### 4.2. Truy cập qua Tailscale

Máy host đã cài Tailscale, IP hiện tại **`100.68.150.98`**. Các cổng publish qua `0.0.0.0`
nên truy cập được từ mọi thiết bị trong tailnet:

| Dịch vụ | URL |
|---|---|
| Web UI | <http://100.68.150.98:8088> |
| Node-RED | <http://100.68.150.98:1880> |
| Grafana | <http://100.68.150.98:3000> |
| InfluxDB | <http://100.68.150.98:8086> |
| MQTT | `100.68.150.98:1883` |

`scada.bat` tự đọc IP bằng `tailscale ip -4` và in ra các URL trên; nếu không tìm thấy
`tailscale.exe` thì dùng mặc định `100.68.150.98`.

Lưu ý:

- Web UI dùng MQTT-over-WebSocket **cùng origin** (`/mqtt`) nên vẫn chạy khi mở bằng IP Tailscale.
- Thiết bị truy cập phải đăng nhập cùng tailnet.
- **Gateway ESP32 không chạy được Tailscale.** Khi nối PLC thật, firmware vẫn trỏ broker về
  **IP LAN** (`192.168.10.198:1883`), không dùng IP Tailscale.
- Nếu IP Tailscale thay đổi, chạy `tailscale ip -4` rồi cập nhật lại `TS_IP` mặc định trong `scada.bat`.

### 4.3. Quản lý danh sách mạch

Mỗi mạch là một cấu hình **retained** trên broker:

| Topic | Payload | Ý nghĩa |
|---|---|---|
| `plc/<id>/config` | `{"id","name","slave","cycle"}` | Khai báo mạch; retained nên client mới kết nối vẫn thấy đủ danh sách |
| `plc/<id>/telemetry` | JSON telemetry | Dữ liệu quét từ PLC |
| `plc/<id>/cmd` | JSON lệnh ghi | Điều khiển `Y0`–`Y7` |
| `plc/<id>/ack` | JSON phản hồi | Xác nhận lệnh |
| `plc/<id>/status` | `online` / `offline` | Presence, retained |

Thêm mạch ngay trên Web UI: bấm **+ Thêm mạch**, nhập mã (ví dụ `dvp24-07`), tên trạm,
địa chỉ Modbus 1–247 và chu kỳ quét, rồi **Thêm mạch**. Cấu hình được publish retained vào
`plc/<id>/config`; Node-RED simulator và mọi trình duyệt đang mở sẽ nhận mạch mới trong ~1 giây.

Thêm bằng dòng lệnh (tương đương):

```powershell
docker exec -it mosquitto mosquitto_pub -h 127.0.0.1 -u web -P web-pass-2026 -t plc/dvp24-07/config -r -m '{"id":"dvp24-07","name":"Trạm thử nghiệm","slave":7,"cycle":300}'
```

Gỡ mạch: xóa retained config (payload rỗng), simulator sẽ ngừng publish và Web UI tự bỏ mạch khỏi danh sách:

```powershell
docker exec -it mosquitto mosquitto_pub -h 127.0.0.1 -u web -P web-pass-2026 -t plc/dvp24-07/config -r -n
```

Danh sách mặc định (`dvp24-01`…`dvp24-06`) được simulator seed lại sau mỗi lần deploy Node-RED.
Chu kỳ quét cho phép 100, 200, 300, 500, 1000, 2000 ms.

### 4.4. Ghi log CSV theo giờ

Node-RED (node `csv hourly file`) ghi mọi bản tin telemetry thành CSV, **mỗi giờ một file**:

| Mục | Giá trị |
|---|---|
| Thư mục trên host | `scada/logs/` (mount vào `/logs` của Node-RED, `/srv/logs` của nginx) |
| Tên file | `telemetry-YYYY-MM-DD-HH.csv` — theo giờ máy chủ (`TZ` trong `.env`) |
| Cột | `time, device, seq, quality, X0…X7, Y0…Y7, M0…M3, D100, D101, D102` |
| Dòng tiêu đề | chỉ ghi khi file được tạo; nếu file bị xóa giữa giờ, header được ghi lại ở bản tin kế tiếp |
| Tốc độ | ~20 dòng/giây (6 mạch × 300 ms) ≈ 72.000 dòng/giờ ≈ 7 MB |

Khi đồng hồ sang giờ mới, Node-RED tự tạo file mới; file của giờ trước giữ nguyên và không bị sửa.
Tên file hiện tại được publish retained tại `plc/log/file` nên Web UI biết ngay khi chuyển giờ.

Tải file:

- Web UI: nút **Tải CSV** cạnh **Làm mới** — tải file của giờ hiện tại.
- Trực tiếp: `http://<host>:8088/logs/telemetry-2026-10-31-14.csv`;
  nginx phục vụ thư mục `/logs/` kèm `Content-Disposition: attachment`.

Nút **Xuất CSV** trong panel Nhật ký sẽ tải 20 dòng sự kiện gần nhất (lệnh, ACK, lỗi) ngay trên
trình duyệt — file này không lưu trên máy chủ.

Lưu ý: file đang mở vẫn được tải bình thường (dữ liệu tới thời điểm tải). Dọn log cũ bằng cách
xóa file trong `scada/logs/`; Node-RED chỉ tạo lại file của giờ hiện tại.

## 5. Kiểm tra nhanh

Xem telemetry đang chảy (mặc định 6 mạch × 300 ms ≈ 20 message/giây):

```powershell
docker exec -it mosquitto mosquitto_sub -h 127.0.0.1 -u web -P web-pass-2026 -t "plc/+/telemetry" -v
```

Xem toàn bộ topic của hệ thống:

```powershell
docker exec -it mosquitto mosquitto_sub -h 127.0.0.1 -u web -P web-pass-2026 -t "plc/#" -v
```

Gửi lệnh bật `Y3` cho mạch `dvp24-01` (mở một cửa sổ khác để xem ACK):

```powershell
docker exec -it mosquitto mosquitto_sub -h 127.0.0.1 -u web -P web-pass-2026 -t "plc/+/ack" -v
docker exec -it mosquitto mosquitto_pub -h 127.0.0.1 -u web -P web-pass-2026 -t plc/dvp24-01/cmd -m '{"device":"dvp24-01","id":"cmd-1730000000000-3","action":"write","target":"Y3","value":1}'
```

Mở giao diện:

- Web UI SCADA: <http://localhost:8088>
- Grafana: <http://localhost:3000>
- Node-RED: <http://localhost:1880>

Có thể chạy `scripts/healthcheck.ps1` để kiểm tra tổng hợp (Docker, 6 container, HTTP, MQTT):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\healthcheck.ps1
```

Script in PASS/FAIL từng mục, tổng kết và trả exit code 1 nếu có mục FAIL.

## 6. Troubleshooting

### 6.1. Mosquitto không lên do thiếu `passwd`

Triệu chứng: `docker compose ps` thấy `mosquitto` ở trạng thái `Restarting`/`Exited`,
log có `Unable to open pwfile /mosquitto/config/passwd` hoặc `Error: Unable to open password file`.

Xử lý: tạo `mosquitto/config/passwd` theo mục 3, kiểm tra file tồn tại và có ít nhất một dòng `user:hash`,
sau đó `docker compose restart mosquitto` và xem lại `docker compose logs mosquitto`.
Lưu ý trên Windows, file phải nằm đúng thư mục `mosquitto\config` mà Compose mount.

Nếu file đã tồn tại mà vẫn báo `Unable to open pwfile`, đó là lỗi quyền trên bind mount Windows.
`mosquitto_passwd` tạo file với quyền `600` thuộc `root`, trong khi tiến trình broker chạy bằng user
`mosquitto` nên không đọc được. Sửa:

```powershell
docker run --rm -v "${PWD}/mosquitto/config:/m" --entrypoint chmod eclipse-mosquitto:2 644 /m/passwd
docker restart mosquitto
```

### 6.2. Web UI hiển thị OFFLINE

Triệu chứng: giao diện báo OFFLINE dù cổng 8088 mở.

Kiểm tra lần lượt:

- `docker compose ps` — `mosquitto` và `web` phải `running`.
- Mở DevTools → Console/Network: nếu báo `Not authorized` thì sai user/mật khẩu MQTT (`web` / `web-pass-2026`);
  nếu lỗi kết nối WebSocket thì kiểm tra proxy `/mqtt` trong `nginx/default.conf` trỏ tới `mosquitto:9001`.
- Xác nhận mạch đã có retained config và publish `plc/<id>/status` = `online` (xem bằng `mosquitto_sub -t "plc/+/status" -v`).
- Tải lại trang sau khi broker được restart (client sẽ tự reconnect sau 2 giây, nhưng nên F5).

### 6.3. InfluxDB không có dữ liệu

Triệu chứng: Grafana trống, bucket `plc` không có measurement.

- Xem `docker compose logs telegraf`: nếu có `unauthorized`/`401` thì token sai.
  Token trong `.env` (`INFLUX_TOKEN`) phải khớp token admin tạo lúc khởi tạo InfluxDB.
- Nếu đã đổi token sau khi InfluxDB đã khởi tạo lần đầu, giá trị mới không tự áp dụng:
  tạo token mới trong UI InfluxDB rồi cập nhật `.env`, hoặc làm lại thư mục dữ liệu InfluxDB (mất dữ liệu cũ).
- Kiểm tra Telegraf đăng nhập MQTT bằng user `telegraf`/`tg-pass-2026` và topic đúng là `plc/+/telemetry`.
- Trong Grafana đặt time range `Last 15 minutes`; kiểm tra bucket `plc`, org `datn`.

### 6.4. Grafana báo lỗi datasource

Triệu chứng: panel hiện `Datasource error`, `401 Unauthorized`, `connection refused`.

- Datasource được provisioning từ `grafana/provisioning`; URL phải là `http://influxdb:8086` (tên service trong mạng Docker, không phải `localhost`).
- Token, org `datn`, bucket `plc` phải khớp `.env` và InfluxDB.
- Sau khi sửa cấu hình provisioning, chạy `docker compose restart grafana` và xem `docker compose logs grafana`.
- Nếu InfluxDB chưa lên xong, Grafana có thể lỗi tạm thời; chờ vài chục giây rồi thử lại.

### 6.5. Cổng bị chiếm

Triệu chứng: `docker compose up` báo `bind: address already in use` / `port is already allocated`.

Xác định tiến trình đang giữ cổng (ví dụ 8088):

```powershell
netstat -ano | findstr :8088
Get-Process -Id <PID>
```

Đóng ứng dụng đang chiếm cổng, hoặc dừng container cũ (`docker ps`, `docker stop <tên>`).
Nếu buộc phải đổi cổng, sửa phần `ports` của service tương ứng trong `docker-compose.yml`
và cập nhật URL truy cập cho phù hợp.

### 6.6. Docker Desktop chưa chạy

Triệu chứng: `docker info` báo `cannot connect ... dockerDesktopLinuxEngine` hoặc
`error during connect: open //./pipe/dockerDesktopLinuxEngine`.

Xử lý: mở Docker Desktop, chờ engine chuyển sang trạng thái running (biểu tượng cá voi hết nhấp nháy),
rồi chạy lại `docker info` và `docker compose up -d`. Có thể kiểm tra service nền:

```powershell
Get-Service com.docker.service
```

### 6.7. Không thấy telemetry dù broker vẫn chạy

- Node-RED simulator chưa deploy hoặc đang stopped: mở <http://localhost:1880> kiểm tra flow và bấm Deploy.
- Gateway thật chưa kết nối: xem log Mosquitto có dòng `New client connected ... as gateway` hay không.
- Sai topic: phải đúng `plc/<mã-mạch>/telemetry` (phân biệt hoa/thường); kiểm tra mạch đã có
  retained config `plc/<mã-mạch>/config` hay chưa.

## 7. Ghi chú vận hành

- **Docker Desktop không tự khởi động sau khi reboot** (theo yêu cầu của đồ án). Sau mỗi lần khởi động lại máy,
  phải mở Docker Desktop thủ công rồi chạy `docker compose up -d`. Thuộc tính `restart: unless-stopped`
  chỉ tự khởi động lại container khi Docker engine đang chạy.
- Máy chủ là VM nên phải **tắt sleep/hibernate** để cụm không "đứng" khi không thao tác:

  ```powershell
  powercfg /change standby-timeout-ac 0
  powercfg /change hibernate-timeout-ac 0
  powercfg /change monitor-timeout-ac 0
  ```

  Trong giao diện Windows: Settings → System → Power → Screen and sleep → đặt `Never` cho cả AC và pin.
- **Không tắt Windows Update.** Cập nhật bảo mật vẫn phải chạy; nếu máy tự reboot thì mở lại Docker Desktop
  và `docker compose up -d`. Nên đặt Active hours để bản cập nhật không reboot giữa buổi demo.
- Giữ đồng bộ thời gian (NTP) và đúng múi giờ `Asia/Ho_Chi_Minh` (`TZ` trong `.env`) để timestamp hiển thị chính xác.

## 8. Nối gateway thật

1. Xác định IP LAN của máy host:

   ```powershell
   ipconfig
   ```

   Lấy địa chỉ IPv4 của card mạng đang dùng. Trên máy này là **`192.168.10.198`**
   (không dùng IP Tailscale `100.68.150.98` cho gateway — ESP32 không chạy Tailscale).
2. Cấu hình gateway (firmware) trỏ broker về `192.168.10.198`, cổng `1883`,
   user `gateway`, mật khẩu `gw-pass-2026`. Với mạch `dvp24-01`, gateway publish
   `plc/dvp24-01/telemetry` (1 msg/chu kỳ, QoS 1), subscribe `plc/dvp24-01/cmd`,
   publish `plc/dvp24-01/ack` và `plc/dvp24-01/status` (`online`/`offline` qua LWT).
   Mạch mới khai báo trên Web UI cần một gateway (hoặc kênh Modbus) tương ứng publish cùng quy ước topic.
3. **Tắt node simulator trong Node-RED** để tránh hai nguồn cùng publish `plc/<id>/telemetry`:
   mở <http://localhost:1880>, tìm flow simulator, disable (hoặc xóa) node inject `100 ms` và
   node `seed fleet`, bấm **Deploy**. Có thể để flow xử lý lệnh tiếp tục hoạt động.
4. Nếu gateway nằm ở máy khác, mở firewall cho cổng 1883 (cần quyền admin, chỉ làm một lần):

   ```powershell
   New-NetFirewallRule -DisplayName "MQTT SCADA 1883" -Direction Inbound -Protocol TCP -LocalPort 1883 -Action Allow
   ```

5. Kiểm tra lại bằng `docker exec -it mosquitto mosquitto_sub -h 127.0.0.1 -u web -P web-pass-2026 -t "plc/+/telemetry" -v`
   và xác nhận `device` là `dvp24-01`, `seq` tăng đều. Thử ghi `Y3` từ Web UI và xem `plc/+/ack` trả `ok: true`.
