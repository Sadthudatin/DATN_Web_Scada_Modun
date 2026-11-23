# hardware/ — Thiết kế phần cứng Gateway

Gateway dùng **ESP32-S3-WROOM-1 (N8R2)** với hai kênh truyền thông vật lý tách rời:

- **COM1 RS-232** (ADM3251E cách ly) — nạp chương trình từ xa qua TCP :8899.
- **COM2 RS-485** (ADM2587E cách ly) — Modbus RTU master, poll PLC 300 ms @ 38400.

Nguồn 24 VDC → buck (TPS54360, 60 V) → 3.3 V. Cách ly tích hợp trong transceiver.

Chi tiết linh kiện và bố trí: [`BOM.md`](BOM.md). Toàn bộ phân tích kỹ thuật: mục 3 trong [`../docs/analysis/PHAN_TICH_DU_AN_VA_PHUONG_AN.md`](../docs/analysis/PHAN_TICH_DU_AN_VA_PHUONG_AN.md).

> Schematic/PCB chưa có trong repo. BOM là cơ sở để đặt linh kiện và vẽ sơ đồ nguyên lý.
