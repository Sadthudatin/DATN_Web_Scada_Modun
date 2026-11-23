# BOM — Gateway ESP32-S3

| # | Linh kiện | SL | Ghi chú |
|---|---|---|---|
| 1 | ESP32-S3-WROOM-1 N8R2 | 1 | 8 MB flash, 2 MB PSRAM |
| 2 | ADM2587E | 1 | RS-485 cách ly 2.5 kVrms, tích hợp DC-DC |
| 3 | ADM3251E | 1 | RS-232 cách ly 2.5 kVrms, tích hợp DC-DC |
| 4 | SM712 | 1 | TVS bus RS-485 (bất đối xứng −7/+12 V) |
| 5 | SMBJ15CA | 2 | TVS RS-232 (±12 V) |
| 6 | SMBJ33A | 1 | TVS nguồn 24 V |
| 7 | TPS54360 | 1 | Buck 60 V, 3.5 A (không dùng LM2596) |
| 8 | AMS1117-3.3 | 1 | LDO 3.3 V |
| 9 | PPTC 100 mA | 2 | Hạn dòng trên A/B |
| 10 | 680 Ω 1% | 2 | Phân cực RS-485 (5 V → A/B → GND_ISO) |
| 11 | 120 Ω 1% | 1 | Kết cuối + jumper |
| 12 | USB-C + ESD array | 1 | USB native GPIO19/20 |
| 13 | Terminal 3.5 mm | 5–8 | Có nhãn |

## Ghi chú bố trí

- 4 lớp: Signal / GND / PWR / Signal.
- Rãnh cách ly ≥ 3 mm dưới hai transceiver; không chạy trace tín hiệu qua rãnh.
- TVS đặt sát connector, trước PPTC.
- GND_ISO và GND hệ thống chỉ gặp nhau bên trong transceiver.
- Antenna keep-out theo datasheet WROOM-1.

## Biến thể mở rộng (không hàn mặc định)

| Footprint | Linh kiện | Dùng khi |
|---|---|---|
| U4 | ADM2582E (RS-422 full-duplex) | Chuyển sang Mitsubishi MD8 |
| — | W5500 (SPI) | Cần Ethernet có dây trong tủ nhiễu |
