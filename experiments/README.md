# experiments/ — KPI và ma trận thực nghiệm

## Chỉ tiêu

| Chỉ tiêu | Ngưỡng | Cách đo |
|---|---|---|
| Chu kỳ polling | ≤ 300 ms @ 38400 | Timestamp ESP32, log CSV |
| MQTT end-to-end | p95 < 500 ms (LAN) | Timestamp host vs ESP32 |
| Tỉ lệ rớt gói | < 0.1% / 24 h | Counter firmware |
| Khôi phục sau mất mạng | ≥ 99% (5/15/30 phút) | Đếm sequence |
| Giảm băng thông nhờ COV | ≥ 60% | So sánh raw vs COV |
| Nạp từ xa | 10/10 @ 9600, cả LAN và WAN | Đếm phiên |
| Cách ly | 2.5 kVrms / 1 phút | Hipot |

## Ma trận thực nghiệm

| Mã | Kịch bản | Biến số | Số liệu thu |
|---|---|---|---|
| E1 | Polling liên tục 24 h | 9600 / 19200 / 38400 | Chu kỳ, lỗi, rớt gói |
| E2 | Mất mạng 5/15/30 phút | Thời gian ngắt | % khôi phục, số bản ghi đệm |
| E3 | COV bật/tắt | Ngưỡng deadband | Băng thông, số message |
| E4 | Nạp LAN | 9600 / 19200 / 38400 | Thời gian, 10/10? |
| E5 | Nạp WAN | 2 vùng tunnel | RTT vs thời gian |
| E6 | Giám sát song song khi nạp | — | Polling vẫn cập nhật? |
| E7 | Nhiễu đóng cắt tải cảm | Khoảng cách, có/không shield | Số lỗi CRC |
| E8 | Cách ly | Hipot 2.5 kVrms | Đạt/không |
| E9 | So sánh gateway thương mại | USR-DR302 / HF2211 | Cùng bộ chỉ tiêu |
| E10 | So sánh PLC có Ethernet | DVP-SE / AS228T | Thời gian nạp, chi phí |

## Dữ liệu đo

- Telemetry log theo giờ: `scada/logs/telemetry-YYYY-MM-DD-HH.csv` (~20 dòng/giây, ~7 MB/giờ).
- Dashboard Grafana: `scada/grafana-data/`.
