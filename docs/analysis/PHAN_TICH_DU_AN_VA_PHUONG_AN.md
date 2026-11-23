# KẾ HOẠCH TRIỂN KHAI ĐỒ ÁN TỐT NGHIỆP (BẢN CHỐT)

**Đề tài:** Thiết kế IoT Gateway biên cho PLC Delta DVP: giám sát, xử lý dữ liệu tại biên và nạp chương trình từ xa
**Chuyên ngành:** Kỹ thuật Cơ điện tử / Tự động hóa
**Phiên bản:** v2 — chốt phương án
**Ngày:** 28/10/2026

> Bản v1 (phân tích hồ sơ gốc: FX3U + one-bus + nạp qua RS-485) đã được thay thế toàn bộ bởi tài liệu này.

---

## 0. Quyết định chốt — đọc mục này là đủ

| Hạng mục | Chốt |
|---|---|
| PLC demo | **Delta DVP-24ES2R** (relay) hoặc **24ES2T / 32ES2T** nếu cần động cơ |
| Kiến trúc bus | **2 kênh vật lý độc lập**: COM1 RS-232 (nạp) + COM2 RS-485 (Modbus) |
| Nạp từ xa | ISPSoft → COM ảo → TCP :8899 → gateway → COM1 (phương án **B**) |
| Modbus | Gateway làm **master**, PLC làm **slave**, chu kỳ 300 ms @38400 |
| Transceiver | **ADM3251E** (cách ly RS-232) + **ADM2587E** (cách ly RS-485) |
| Nguồn | 24 VDC → buck 60 V → 3.3 V; cách ly tích hợp trong transceiver |
| Mạng | Wi-Fi mặc định, **W5500 (SPI)** tuỳ chọn cho môi trường tủ điện |
| Hạ tầng | Mosquitto + Node-RED + InfluxDB + Grafana + Cloudflare Tunnel |
| Đường nạp | Cloudflare Access TCP **hoặc** Tailscale (RTT thấp hơn) |
| Phạm vi | Delta DVP **duy nhất**; Mitsubishi = hướng phát triển |
| Thời lượng | **14–15 tuần** (3,5 tháng) |

### Đã loại bỏ (và lý do)

| Loại bỏ | Lý do |
|---|---|
| Mitsubishi FX3U (gốc và clone) | MD8 là RS-422, FX3U gốc không có Modbus RTU, clone không có bảng địa chỉ thống nhất |
| Nạp qua RS-485 mặc định | COM1 mới là cổng lập trình chính thức; RS-485 chỉ dùng khi test xác nhận (phương án A) |
| ISO3082 + B0505S + rãnh 3 mm | ADM3251E/ADM2587E tích hợp DC-DC cách ly, giảm 4 điểm hỏng |
| SMBJ6.0CA | Standoff 6 V < common-mode ±7 V của RS-485 |
| Trở phân cực 4.7 kΩ | Không đủ tạo 200 mV với 60 Ω tải |
| LM2596 | Max 40 V, không chịu xung 24 V công nghiệp (IEC 61131-2) |
| One-bus dùng chung | Không giám sát được trong khi nạp; firmware phức tạp |

---

## 1. Kiến trúc hệ thống

```
                 ┌───────────────────── DVP-24ES2 ─────────────────────┐
                 │  COM1 RS-232 (Mini-DIN 8)      COM2 RS-485 (2 dây)   │
                 └───────┬──────────────────────────────────┬───────────┘
                         │ TXD/RXD/GND (≤3 m)               │ A/B/SG
                         ▼                                  ▼
                 ┌───────────────┐                  ┌───────────────┐
                 │   ADM3251E    │                  │   ADM2587E    │
                 │ cách ly 2.5kV │                  │ cách ly 2.5kV │
                 └───────┬───────┘                  └───────┬───────┘
                         │ UART1                            │ UART2
                 ┌───────┴──────────────────────────────────┴───────┐
                 │              ESP32-S3-WROOM-1 (N8R2)             │
                 │                                                  │
                 │  Core 1: TaskUart1 (nạp)  │  TaskModbus (300 ms)  │
                 │           │ mutex riêng  │  mutex riêng          │
                 │           ▼              ▼                        │
                 │    TCP Server :8899   FreeRTOS Queue → MQTT       │
                 │           │              │                        │
                 │  Core 0:  USB CDC (COM ảo) │ Wi-Fi / W5500        │
                 └───────────┼──────────────┼────────────────────────┘
                             │              │
                             │              ▼
                             │     Mosquitto :1883
                             │       ├── Node-RED :1880 (HMI, alarm, cấu hình)
                             │       ├── Telegraf → InfluxDB → Grafana
                             │       └── cloudflared (outbound, không mở port)
                             │              │
                             │              ▼
                             │     Cloudflare Access (OTP) ──► HTTPS
                             │
                             ▼
                  VCOM trên PC kỹ sư ──► ISPSoft
```

### Nguyên tắc cốt lõi

Hai kênh **vật lý tách rời** ⇒ giám sát vẫn chạy trong khi kỹ sư nạp chương trình. Đây là điểm khác biệt so với thiết kế one-bus và là nội dung bảo vệ mạnh nhất.

---

## 2. Phạm vi

### Trong phạm vi

1. Gateway cách ly 24 V, RS-232 + RS-485, ESP32-S3.
2. Modbus RTU master → MQTT telemetry.
3. Nạp chương trình từ xa qua COM ảo + tunnel.
4. Pipeline xử lý dữ liệu tại biên (mục 6).
5. Web SCADA + lưu trữ chuỗi thời gian.
6. Thực nghiệm đo đạc (mục 8).

### Ngoài phạm vi (ghi vào "hướng phát triển")

- Hỗ trợ Mitsubishi FX3U.
- Nạp qua RS-485 (chỉ làm nếu test mục 5.3 đạt).
- OTA firmware cho chính gateway (làm nếu còn ≥3 tuần).
- Chẩn đoán sớm / bảo trì dự đoán (làm nếu còn ≥2 tuần).
- Điều khiển đa PLC, phân quyền nhiều người dùng.

---

## 3. Phần cứng

### 3.1 Khối nguồn

| Khối | Linh kiện | Ghi chú |
|---|---|---|
| Bảo vệ đầu vào | Cầu chì 0.5 A + diode chống ngược + **TVS SMBJ33A** | 24 V công nghiệp có xung tới 60 V |
| Hạ áp | **TPS54360** (60 V, 3.5 A) hoặc LM5164 | **Không dùng LM2596** |
| 3.3 V | AMS1117-3.3 hoặc MP2315 | Cấp ESP32-S3 |
| Cách ly | Không cần module rời | ADM3251E/ADM2587E tự tạo nguồn cách ly |
| USB | Diode OR-ing / power-mux | Chống backfeed 5 V vào laptop khi cắm 24 V |

### 3.2 Kênh RS-485 (Modbus, COM2)

| Hạng mục | Linh kiện | Ghi chú |
|---|---|---|
| Transceiver | **ADM2587E** | Cách ly 2.5 kVrms, 500 kbps, tích hợp DC-DC |
| Bảo vệ | **SM712** (không dùng SMBJ6.0CA) | Bất đối xứng −7/+12 V, đúng chuẩn RS-485 |
| Hạn dòng | PPTC 100 mA trên A/B | Chống đấu nhầm 24 V |
| Phân cực | **680 Ω** lên 5 V và xuống GND_ISO | 4.7 kΩ không đủ 200 mV |
| Kết cuối | 120 Ω + jumper | Chỉ bật ở hai đầu bus |
| Điều khiển chiều | RTS → DE và /RE | `UART_MODE_RS485_HALF_DUPLEX` |

Tính toán phân cực (đưa vào thuyết minh):

$$I_{bias}=\frac{5-0.2}{680+680}=3.5\text{ mA},\quad V_{AB}=3.5\text{ mA}\times 60\ \Omega=211\text{ mV}>200\text{ mV}$$

### 3.3 Kênh RS-232 (nạp, COM1)

| Hạng mục | Linh kiện | Ghi chú |
|---|---|---|
| Transceiver | **ADM3251E** | Cách ly 2.5 kVrms, tích hợp DC-DC |
| Dây | TXD, RXD, GND | **Cáp ≤ 3 m** trong tủ |
| Bảo vệ | TVS 15 V (SMBJ15CA) | RS-232 ±12 V |
| Connector | Terminal 3 chân có nhãn | Cáp chuyển ra Mini-DIN 8 làm rời |
| Handshake | Không dùng | Delta không cần RTS/CTS |

**Cảnh báo:** chân 1 của Mini-DIN 8 Delta thường là **+5 V** — không nối vào PC.

### 3.4 Biến thể mở rộng (không hàn mặc định)

| Footprint | Linh kiện | Dùng khi |
|---|---|---|
| U4 | ADM2582E (RS-422 full-duplex) | Chuyển sang Mitsubishi MD8 sau này |
| — | W5500 (SPI) | Cần Ethernet dây trong tủ nhiễu |

### 3.5 Quy tắc layout

- 4 lớp: Signal / GND / PWR / Signal.
- Rãnh cách ly ≥ 3 mm dưới hai transceiver.
- Không chạy trace tín hiệu qua rãnh cách ly.
- Antenna keep-out theo datasheet WROOM-1.
- TVS đặt sát connector, trước PPTC.
- GND_ISO và GND hệ thống **chỉ** gặp nhau bên trong transceiver.

### 3.6 BOM rút gọn

| # | Linh kiện | SL | Ghi chú |
|---|---|---|---|
| 1 | ESP32-S3-WROOM-1 N8R2 | 1 | 8 MB flash, 2 MB PSRAM |
| 2 | ADM2587E | 1 | RS-485 cách ly |
| 3 | ADM3251E | 1 | RS-232 cách ly |
| 4 | SM712 | 1 | TVS bus 485 |
| 5 | SMBJ15CA | 2 | TVS RS-232 |
| 6 | SMBJ33A | 1 | TVS nguồn 24 V |
| 7 | TPS54360 | 1 | Buck 60 V |
| 8 | AMS1117-3.3 | 1 | LDO |
| 9 | PPTC 100 mA | 2 | A/B |
| 10 | 680 Ω 1% | 2 | Phân cực |
| 11 | 120 Ω 1% | 1 | Kết cuối + jumper |
| 12 | USB-C + ESD array | 1 | USB native GPIO19/20 |
| 13 | Terminal 3.5 mm | 5–8 | Có nhãn |

---

## 4. Firmware

### 4.1 Phân task

| Core | Task | Priority | Chức năng |
|---|---|---|---|
| 1 | `TaskModbus` | 3 | Poll PLC 300 ms, đẩy dữ liệu vào Queue |
| 1 | `TaskFlashBridge` | 4 | Byte pipe UART1 ⇄ TCP :8899, bật/tắt theo lệnh |
| 0 | `TaskUsbCdc` | 2 | COM ảo trên mạch, phục vụ nạp tại chỗ |
| 0 | `TaskNetwork` | 2 | Wi-Fi/W5500, MQTT publish/subscribe |
| 0 | `TaskPipeline` | 1 | COV, quality, buffer, alarm |

Hai kênh UART dùng **hai mutex riêng** — không tranh bus, không cần state machine chuyển mode.

### 4.2 Cấu hình UART

| | UART1 (nạp) | UART2 (Modbus) |
|---|---|---|
| Baud | 9600 (thử 19200/38400 sau) | 38400 |
| Data bits | **7** | 8 |
| Parity | **Even** | Even (hoặc None — khớp PLC) |
| Stop | 1 | 1 |
| Mode | Normal | `UART_MODE_RS485_HALF_DUPLEX` |
| Buffer | 4 KB mỗi chiều | 512 B |

### 4.3 Bẫy 7-bit

TCP truyền byte 8-bit; UART 7-bit sẽ cắt MSB. Protocol Delta là ASCII 7-bit nên vô hại, nhưng phải:

- Đặt đúng `UART_DATA_7_BITS`, không để 8N1 "cho chắc".
- Log cảnh báo nếu có byte > 0x7F đi qua.
- Đọc/ghi byte thô (`uart_read_bytes` / `uart_write_bytes`), không parse frame.

### 4.4 TCP :8899

| Thuộc tính | Giá trị |
|---|---|
| Số client | 1 (mutex) |
| `TCP_NODELAY` | Bật cả hai đầu |
| TLS | Không (tunnel đã mã hóa) |
| Mở cổng | Theo lệnh "Programming mode" từ web, yêu cầu quyền |
| Tự đóng | Sau 10 phút không hoạt động |
| Log | Mọi phiên ghi vào file + MQTT |

### 4.5 Modbus

| Hạng mục | Giá trị |
|---|---|
| Vai trò | Master |
| Function code | 01/02 (X, Y, M), 03 (D), 05/06 (ghi) |
| Gộp lệnh | Đọc tối đa 125 register / 2000 bit mỗi transaction |
| Timeout | 300 ms, retry 1 lần |
| Lỗi liên tiếp | 3 lần → đánh dấu BAD quality, phát alarm |

Bảng địa chỉ chuẩn Delta (đối chiếu manual ES2 trước khi hard-code):

| Thiết bị | Địa chỉ | FC |
|---|---|---|
| S | `0x0000 + n` | 01/05 |
| X | `0x0400 + n` | 02/01 |
| Y | `0x0500 + n` | 01/05 |
| T | `0x0600 + n` | 01/03 |
| M | `0x0800 + n` | 01/05 |
| C | `0x0E00 + n` | 01/03 |
| D | `0x1000 + n` | 03/06/16 |

---

## 5. Nạp chương trình từ xa

### 5.1 Chuỗi truyền

```
ISPSoft ── COM10 ảo ── hub4com / HW VSP ── TCP ── tunnel ── gateway:8899 ── UART1 7E1 ── COM1 PLC
```

### 5.2 Phía PC

| Cách | Công cụ | Ghi chú |
|---|---|---|
| A | HW VSP3 | Dễ nhất, tạo VCOM trực tiếp tới host:port |
| B | `com0com` + `hub4com` | Miễn phí, RFC 2217 |
| C | Tailscale | RTT thấp nhất, kỹ sư nối thẳng `100.x.x.x:8899` |

Cấu hình ISPSoft: **RS-232 → COM10 → 9600, 7, Even, 1, station 1**. Tăng timeout nếu có.

### 5.3 Bố trí cổng — chọn sau khi test

| | Nạp | Modbus | Giám sát khi nạp | Khi nào chọn |
|---|---|---|---|---|
| **B (mặc định)** | COM1 RS-232 | COM2 RS-485 | Có | Luôn dùng được |
| **A (dự phòng)** | COM2 **RS-485** | COM1 RS-232 | Có | Chỉ khi test 2 dưới đây đạt |

Test quyết định A (30 phút):

| # | Test | Đạt khi |
|---|---|---|
| 1 | ISPSoft → COM1 9600 7E1 → Read PLC | Đọc được ⇒ B khả thi |
| 2 | ISPSoft → COM2 RS-485 → Read PLC | Đọc được ⇒ A khả thi |
| 3 | Modbus Poll → cổng còn lại, FC03 đọc D0 | Trả lời ⇒ 2 kênh độc lập |

Nếu test 2 fail: chốt B, ghi trong thuyết minh "nạp qua cổng lập trình RS-232, đường xa là Ethernet".

### 5.4 Trình tự vận hành

| Bước | Việc | Kiểm tra |
|---|---|---|
| 1 | Web SCADA → bật *Programming mode* (xác nhận 2 bước) | Log "port 8899 open" |
| 2 | Đưa PLC về **STOP** | Đèn RUN tắt |
| 3 | PC: tunnel → VCOM | COM10 xuất hiện |
| 4 | ISPSoft → **Read PLC** | Đọc được |
| 5 | **Write PLC** → verify | Không lỗi LRC |
| 6 | PLC về RUN, tắt phiên | Cổng tự đóng |

**An toàn:** nạp bắt buộc PLC về STOP. Trên máy đang chạy đây là hành động nguy hiểm — phải có interlock và xác nhận 2 bước.

### 5.5 Bảng lỗi thường gặp

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| Timeout ngay lệnh đầu | Sai parity / data bits | Kiểm tra `UART_DATA_7_BITS` |
| Chậm bất thường | Nagle bật | `TCP_NODELAY` hai đầu |
| Rác ký tự | Nhầm TXD/RXD | Đảo 2 dây |
| "Port busy" | VCOM bị giữ | Đóng Modbus Poll / terminal |
| Verify fail | Nhiễu cáp RS-232 | Rút ngắn cáp, dùng shield |
| Chết giữa phiên | RTT > timeout ISPSoft | Giảm baud / chuyển Tailscale |

---

## 6. Xử lý dữ liệu tại biên

Đây là phần thay thế độ khó của "nạp PLC" trong bản one-bus. Mỗi cơ chế phải có số liệu.

| # | Cơ chế | Nội dung | Số liệu chứng minh |
|---|---|---|---|
| 1 | **COV / deadband** | Chỉ gửi khi giá trị đổi > ngưỡng, cấu hình theo tag | % giảm băng thông MQTT |
| 2 | **Quality code** | GOOD / BAD / STALE / UNRELIABLE theo kiểu OPC UA | Số điểm BAD khi mất bus |
| 3 | **Store-and-forward** | Ring buffer LittleFS/PSRAM, gửi bù, sequence + dedup | % dữ liệu khôi phục sau mất mạng 5/15/30 phút |
| 4 | **Alarm + hysteresis** | Ngưỡng cao/thấp, trễ chống rung, timestamp biên | Số báo động giả giảm |
| 5 | **Engineering unit** | Scale thô → đơn vị kỹ thuật, bit-unpack X/Y/M | Đối chiếu 100% với màn hình PLC |

Tuỳ chọn nếu còn thời gian:

| # | Cơ chế | Công | Giá trị |
|---|---|---|---|
| 6 | Sparkplug B (birth/death, sequence) | 4 ngày | Điểm cộng học thuật |
| 7 | Chẩn đoán sớm: CUSUM cycle-time, sensor-stuck | 2 tuần | Mũi nhọn, cần bơm lỗi chủ động |
| 8 | OTA firmware gateway (A/B partition, ký số) | 3 ngày | Giữ câu chuyện "cập nhật từ xa" |

---

## 7. Web SCADA

```
Mosquitto :1883
  ├── Node-RED :1880      HMI, điều khiển, alarm, form cấu hình tag
  ├── Telegraf → InfluxDB → Grafana    trend, thống kê, số liệu Ch.4
  └── cloudflared         Cloudflare Access (OTP) → HTTPS
```

| Thành phần | Vai trò | Ghi chú |
|---|---|---|
| Mosquitto | Broker | TLS + user/password, LWT |
| Node-RED | HMI | Dashboard + node điều khiển, ghi có xác nhận |
| InfluxDB | Chuỗi thời gian | Retention 30 ngày |
| Grafana | Phân tích | Biểu đồ latency, packet loss, availability |
| Cloudflare Access | Zero Trust | OTP email + service token cho đường nạp |

Bảo mật tối thiểu:

- MQTT có user riêng cho từng thiết bị, không dùng anonymous.
- Lệnh điều khiển dùng QoS 1 + ACK + idempotency key.
- Node-RED không lộ trực tiếp; chỉ đi qua Cloudflare Access.
- Đường nạp dùng service token riêng, không dùng chung với web.

---

## 8. KPI và ma trận thực nghiệm

### 8.1 Chỉ tiêu

| Chỉ tiêu | Ngưỡng | Cách đo |
|---|---|---|
| Chu kỳ polling | ≤ 300 ms @38400 | Timestamp ESP32, log CSV |
| MQTT end-to-end | p95 < 500 ms (LAN) | Timestamp Node-RED vs ESP32 |
| Tỉ lệ rớt gói | < 0.1% / 24 h | Counter firmware |
| Dữ liệu khôi phục sau mất mạng | ≥ 99% (5/15/30 phút) | Đếm sequence |
| Giảm băng thông nhờ COV | ≥ 60% | So sánh raw vs COV |
| Nạp từ xa | 10/10 @9600, cả LAN và WAN | Đếm phiên |
| Thời gian Read/Write 2 KB | Ghi nhận, không đặt ngưỡng | Bấm giờ |
| Cách ly | 2.5 kVrms / 1 phút | Hipot |
| Nhiễu | Ổn định khi đóng cắt tải cảm 24 V gần bus | Test thực tế |

### 8.2 Ma trận thực nghiệm

| Mã | Kịch bản | Biến số | Số liệu thu |
|---|---|---|---|
| E1 | Polling liên tục 24 h | 9600 / 19200 / 38400 | Chu kỳ, lỗi, rớt gói |
| E2 | Mất mạng 5/15/30 phút | Thời gian ngắt | % khôi phục, số bản ghi đệm |
| E3 | COV bật/tắt | Ngưỡng deadband | Băng thông, số message |
| E4 | Nạp LAN | 9600 / 19200 / 38400 | Thời gian, 10/10? |
| E5 | Nạp WAN | 2 vùng tunnel | RTT vs thời gian, 10/10? |
| E6 | Giám sát song song khi nạp | — | Polling vẫn cập nhật? |
| E7 | Nhiễu đóng cắt tải cảm | Khoảng cách, có/không shield | Số lỗi CRC |
| E8 | Cách ly | Hipot 2.5 kVrms | Đạt/không |
| E9 | So sánh gateway thương mại | USR-DR302 / HF2211 | Cùng bộ chỉ tiêu |
| E10 | So sánh PLC có Ethernet | DVP-SE / AS228T | Thời gian nạp, chi phí |

E9 và E10 gần như miễn phí nhưng làm Ch.4 nặng hơn hẳn.

---

## 9. Timeline 14–15 tuần

| Tuần | Việc | Cổng kiểm tra |
|---|---|---|
| 1 | Chốt phạm vi, **viết Ch.1–Ch.2 ngay**, đặt PLC + devkit + module | Có PLC trong tay |
| 2 | Test PLC: Modbus Poll + ISPSoft COM1 | Đọc được cả 2 |
| 3 | Firmware Modbus trên devkit + module mua sẵn | Đọc X/Y/M/D thật |
| 4 | **Chốt schematic, order PCB v1** | DRC sạch + GV duyệt |
| 5 | MQTT + Node-RED + tunnel | HMI chạy từ xa |
| 6 | Review GV lần 1 + test nạp LAN | Demo sống |
| 7–8 | Bridge 7E1 + VCOM + nạp LAN 10/10 | E4 đạt |
| 9 | Pipeline biên: COV, quality, buffer | E2, E3 đạt |
| 10 | PCB về → hàn, test nguồn, thay module | Board chạy = module |
| 11 | Nạp qua WAN, đo RTT | E5, E6 |
| 12 | Nhiễu, hipot, 24 h | E7, E8, E1 |
| 13 | Ch.3–Ch.4 + review lần 2 | Bản nháp 80% |
| 14 | Ch.5, slide, poster, review lần 3 | Bản nộp |
| 15 | Buffer | — |

**Mốc không được trượt:** order PCB tuần 4. Nếu trượt, mất 2–3 tuần và không kịp Tết (xưởng Trung Quốc nghỉ ~28/1 – 20/2/2027).

---

## 10. Rủi ro và đối sách

| # | Rủi ro | Xác suất | Đối sách |
|---|---|---|---|
| 1 | ISPSoft không nạp được qua bridge | Trung bình | Giảm baud; tăng timeout; dùng Tailscale; dự phòng RDP vào PC xưởng |
| 2 | PCB trễ / sai mạch | Trung bình | Vertical slice: sản phẩm chạy trên module trước khi PCB về |
| 3 | COM2 không nạp được (phương án A) | Cao | Dùng B — không ảnh hưởng KPI |
| 4 | Wi-Fi nhiễu trong tủ | Trung bình | Hàn W5500 (footprint đã có) |
| 5 | Sai bảng địa chỉ Modbus | Thấp | Đối chiếu manual bằng Modbus Poll trước |
| 6 | Chưa quen ESP-IDF | Trung bình | +2 tuần buffer; ưu tiên ví dụ có sẵn của ESP-IDF |

---

## 11. Cấu trúc thuyết minh 5 chương

| Chương | Nội dung chính | Số liệu/đóng góp |
|---|---|---|
| 1 | Tổng quan IoT công nghiệp; bài toán PLC legacy; so sánh FX3U vs Delta và lý do chọn | Bảng so sánh, phân tích yêu cầu |
| 2 | RS-232/RS-485/RS-422; Modbus RTU; FreeRTOS; kiến trúc tunnel; lý thuyết cách ly | Công thức phân cực, tính toán công suất, sơ đồ nguyên lý |
| 3 | Thiết kế phần cứng, firmware 2 core, pipeline biên, hạ tầng SCADA | Schematic, BOM, sơ đồ task, thiết kế topic MQTT |
| 4 | Thực nghiệm E1–E10 và phân tích | 10 bảng số liệu, đồ thị latency/loss/COV |
| 5 | Kết luận, hạn chế, hướng phát triển (FX3U, nạp 485, OTA, chẩn đoán sớm) | Đánh giá mục tiêu |

---

## 12. Checklist mua hàng

| # | Vật tư | SL | Ghi chú |
|---|---|---|---|
| 1 | Delta DVP-24ES2R (hoặc 24ES2T) | 1 | Kiểm tra hàng thật, đủ 2 cổng COM |
| 2 | ESP32-S3-DevKitC-1 N8R2 | 2 | 1 dự phòng |
| 3 | Module RS-485 cách ly (mua sẵn) | 1 | Cho vertical slice tuần 2–3 |
| 4 | Module RS-232 cách ly hoặc USB-RS232 | 1 | Test COM1 |
| 5 | USB-RS485 | 1 | Test Modbus |
| 6 | Nguồn 24 V / 1 A | 1 | |
| 7 | Gateway thương mại (USR-DR302/HF2211) | 1 | Baseline E9 — mua muộn cũng được |
| 8 | Cáp Mini-DIN 8 | 2 | Một cho Delta, một để dự phòng |

---

## 13. Việc cần làm ngay

- [ ] Gửi GV duyệt tên đề tài + phạm vi Delta-only + bỏ nạp qua RS-485 mặc định
- [ ] Đặt PLC Delta và devkit
- [ ] Test 30 phút: ISPSoft qua COM1, Modbus Poll qua COM2
- [ ] Viết Ch.1–Ch.2 (không phụ thuộc phần cứng)
- [ ] Chốt schematic + order PCB trước tuần 4
- [ ] Dựng repo: `firmware/`, `hardware/`, `scada/`, `docs/`, `experiments/`
