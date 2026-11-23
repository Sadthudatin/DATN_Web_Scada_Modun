# HANDOFF BRIEF --- Thiết kế PowerPoint đồ án tốt nghiệp

> **Mục đích tài liệu:** Đây là tài liệu bàn giao ngữ cảnh cho một AI
> Agent khác để thiết kế bộ PowerPoint thuyết trình đồ án. Agent cần đọc
> toàn bộ tài liệu trước khi tạo slide. Hãy dùng nội dung này làm nguồn
> sự thật chính; không tự ý thêm tính năng, thông số hoặc kết quả thử
> nghiệm chưa được xác nhận.

------------------------------------------------------------------------

## 1. Vai trò của Agent nhận bàn giao

Bạn là trợ lý thiết kế slide kỹ thuật cho đồ án tốt nghiệp ngành Kỹ
thuật Cơ điện tử/Tự động hóa.

Nhiệm vụ: - Thiết kế bộ PowerPoint tiếng Việt, phong cách kỹ thuật công
nghiệp hiện đại. - Chuyển nội dung thành slide ngắn gọn, trực quan, dễ
thuyết trình trước giảng viên. - Dùng sơ đồ kiến trúc, luồng dữ liệu, sơ
đồ khối và timeline thay cho các đoạn văn dài. - Giữ nguyên phạm vi và
trạng thái thực tế của đề tài. - Không tự tạo số liệu, kết quả đo, ảnh
phần cứng đã chế tạo, hoặc khẳng định tính năng đã hoàn thành nếu tài
liệu không nói rõ.

**Đầu ra mong muốn:** file PowerPoint `.pptx` có thể chỉnh sửa, tỷ lệ
16:9, khoảng 16 slide. Nếu có thể, cung cấp thêm PDF để xem nhanh.

## 2. Thông tin đề tài

**Tên đề tài chính xác:**

"Nghiên cứu, thiết kế thiết bị IoT Gateway phục vụ giám sát, điều khiển
và nạp chương trình từ xa cho PLC công nghiệp."

**Bản chất đề tài:**

Nghiên cứu và thiết kế một thiết bị IoT Gateway kết nối với PLC công
nghiệp, thu thập dữ liệu vận hành, truyền dữ liệu lên máy chủ, cho phép
giám sát và điều khiển thông qua Web SCADA. Hệ thống có cơ chế lưu dữ
liệu cục bộ khi mất mạng và đồng bộ lại khi kết nối được khôi phục. Đề
tài cũng nghiên cứu phương án hỗ trợ truy cập/nạp chương trình PLC từ xa
thông qua kênh kết nối riêng.

**Thông điệp xuyên suốt cần thể hiện:**

PLC → IoT Gateway → MQTT/Host Server → Web SCADA

Chiều điều khiển:

Web SCADA → Host → MQTT → Gateway → PLC → phản hồi/đọc xác nhận → Web
SCADA

Gateway là cầu nối giữa thiết bị tự động hóa công nghiệp và nền tảng
giám sát qua mạng.

## 3. Mục tiêu và phạm vi

### Mục tiêu chính

1.  Thiết kế phần cứng Gateway sử dụng ESP32-S3.
2.  Kết nối PLC qua giao diện truyền thông phù hợp.
3.  Thu thập dữ liệu và truyền lên máy chủ qua MQTT.
4.  Xây dựng Host Server và Web SCADA.
5.  Hỗ trợ giám sát, điều khiển, lịch sử dữ liệu và cảnh báo.
6.  Lưu dữ liệu vào microSD khi mất mạng, đồng bộ lại khi có mạng.
7.  Nghiên cứu hỗ trợ nạp chương trình PLC từ xa cho một PLC đại diện.

### PLC dự kiến thử nghiệm

-   **Delta DVP-ES:** phương án dự kiến RS485, Modbus RTU.
-   **Mitsubishi FX3U:** khảo sát đúng model, cổng và board thực tế; có
    thể dùng RS422 hoặc RS485 và driver tương ứng.
-   **Siemens S7-1200:** ưu tiên kết nối Ethernet.

Không được nói rằng tất cả PLC đã kết nối thành công. Đây là các đối
tượng dự kiến khảo sát/thử nghiệm, trừ khi người làm đồ án cung cấp bằng
chứng triển khai sau này.

### Phạm vi thực nghiệm

-   Mục tiêu mô hình: tối thiểu 3 Gateway kết nối về một Host.
-   Nhiều PLC trong phạm vi các dòng đã chọn.
-   Chức năng nạp chương trình từ xa có thể triển khai thực tế với một
    dòng PLC đại diện.
-   Không đặt mục tiêu tương thích với mọi PLC trên thị trường.

### Phân biệt bắt buộc và mở rộng

**Phần cốt lõi:** Gateway ESP32-S3, Ethernet/W5500, giao tiếp PLC,
firmware FreeRTOS, MQTT, Host Server, Web SCADA, giám sát, điều khiển,
lịch sử, lưu trữ offline, thử nghiệm nhiều Gateway.

**Phần mở rộng/định hướng:** Communication Module dạng module hóa; các
cổng RS232/RS422/RS485; tự nhận dạng module; mở rộng nhiều driver PLC và
nhiều giao thức.

**Không mở rộng thành:** nền tảng enterprise IoT, AI predictive
maintenance, MQTT cluster, Kubernetes hoặc cloud quy mô lớn.

## 4. Kiến trúc kỹ thuật dự kiến

### 4.1. Gateway

-   MCU: **ESP32-S3-WROOM-1**
-   RTOS: FreeRTOS
-   Ethernet: W5500 (kết nối có dây là phương án chính)
-   Wi-Fi: kết nối phụ/cấu hình khi phù hợp
-   microSD: lưu dữ liệu cục bộ, store-and-forward
-   MQTT Client: truyền telemetry, trạng thái, cảnh báo, command và ACK
-   Kênh TCP/VPN riêng: phục vụ truy cập engineering/programming nếu khả
    thi

Các nhóm firmware: - Network Manager - PLC Driver Manager - Data
Acquisition - MQTT Client - Command Handler - SD Logger /
Store-and-Forward - Health Monitor

### 4.2. Communication Module

Định hướng chia phần cứng thành: - **Gateway Base:** ESP32-S3, nguồn,
W5500, microSD và xử lý trung tâm. - **Communication Module:** giao tiếp
vật lý với PLC.

Phương án đang nghiên cứu là 3 đầu nối DB8: - DB8-1: RS422 - DB8-2:
RS232 - DB8-3: RS485

**Lưu ý kỹ thuật quan trọng:** - DB8 chỉ là đầu nối vật lý do người
thiết kế quy định; không phải chuẩn truyền thông. - Pinout DB8 chưa được
chốt; không tự bịa pinout. - Không nối trực tiếp chân UART ESP32 vào đầu
nối PLC; phải có transceiver phù hợp. - Cần cân nhắc bảo vệ ESD/TVS,
cách ly, nguồn, nối đất và layout. - Không khẳng định mức cách ly 3 kV
hoặc thông số điện nếu chưa có linh kiện và thiết kế PCB xác minh. -
Module hóa là điểm nhấn thiết kế, nhưng nếu chưa chế tạo thì phải thể
hiện là phương án/định hướng.

### 4.3. Host Server

Định hướng nền tảng: - Ubuntu Server - Docker - Mosquitto MQTT Broker -
Node-RED cho xử lý logic và giao diện Web SCADA - InfluxDB cho dữ liệu
chuỗi thời gian - PostgreSQL có thể dùng nếu cần lưu cấu hình, người
dùng, sự kiện có cấu trúc

Không cần trình bày quá sâu về hạ tầng enterprise. Truy cập từ xa cần có
VPN hoặc giải pháp truy cập an toàn; không public trực tiếp cổng MQTT,
Node-RED hoặc cổng lập trình PLC ra Internet.

## 5. Các chức năng hệ thống cần thể hiện

### Giám sát

-   Danh sách Gateway và PLC.
-   Trạng thái online/offline.
-   Các tag/giá trị PLC đã cấu hình.
-   Trạng thái kết nối và sức khỏe Gateway.
-   Lịch sử dữ liệu vận hành.
-   Cảnh báo/sự kiện.

### Điều khiển từ xa

Luồng điều khiển cần thể hiện rõ:

1.  Người dùng gửi command từ Web.
2.  Host kiểm tra quyền/cấu hình và chuyển lệnh qua MQTT.
3.  Gateway nhận lệnh và kiểm tra tính hợp lệ.
4.  Gateway giao tiếp với PLC.
5.  Gateway gửi ACK và kết quả thực hiện.
6.  Gateway có thể đọc lại giá trị để xác nhận trạng thái thực tế.
7.  Web cập nhật kết quả cho người dùng.

**Không nhầm MQTT PUBACK với xác nhận PLC đã thực hiện lệnh.** ACK tầng
MQTT chỉ xác nhận thông điệp được nhận ở một chặng; cần ACK ứng dụng
và/hoặc read-back để xác nhận kết quả PLC.

Các chức năng điều khiển phải giới hạn theo quyền và cấu hình. Không mô
tả việc bỏ qua E-stop, interlock hoặc cơ chế bảo vệ máy.

### Store-and-Forward khi mất mạng

-   Bình thường: Gateway đọc PLC và gửi MQTT lên Host.
-   Mất mạng: Gateway vẫn thu thập dữ liệu, ghi vào microSD.
-   Mạng phục hồi: Gateway gửi lại các bản ghi chưa đồng bộ.
-   Host lưu dữ liệu và trả Application ACK.
-   Gateway đánh dấu đồng bộ sau ACK.
-   Dùng `gateway_id`, `boot_id`, `sequence` hoặc `event_id` để phát
    hiện bản ghi trùng.

Không tuyên bố "không mất dữ liệu tuyệt đối". Dung lượng lưu phụ thuộc
tốc độ lấy mẫu, kích thước bản ghi và dung lượng thẻ. Có thể minh họa cơ
chế bằng sơ đồ luồng 4--5 bước.

### Hỗ trợ nạp chương trình PLC từ xa

-   MQTT không phải kênh truyền chương trình PLC.
-   Hướng ưu tiên: VPN → máy tính Engineering/Programming → PLC.
-   Chỉ cần triển khai thực nghiệm cho một PLC đại diện.
-   Quy trình cần có backup, xác minh đúng PLC, thực hiện nạp theo hướng
    dẫn của nhà sản xuất, verify và khôi phục vận hành.
-   Không tuyên bố TCP-to-Serial transparent bridge tương thích GX
    Works, ISPSoft hoặc TIA Portal nếu chưa kiểm chứng.

## 6. Mô hình dữ liệu tham khảo

Có thể đưa một ví dụ ngắn trong slide kiến trúc phần mềm hoặc slide dữ
liệu:

``` json
{
  "gateway_id": "GW_001",
  "plc_id": "PLC_001",
  "boot_id": "BOOT_0001",
  "sequence": 1025,
  "timestamp": "2026-11-03T03:00:00Z",
  "tag": "Motor_Run",
  "value": true,
  "data_type": "BOOL",
  "quality": "GOOD"
}
```

Đây là ví dụ minh họa cấu trúc dữ liệu, không phải dữ liệu đo thực tế.

Command nên có các trường định danh và kiểm soát như `command_id`,
`gateway_id`, `plc_id`, `tag`, `value`, `issued_by`, `issued_at`,
`expires_at`.

## 7. Cấu trúc PowerPoint --- 16 slide

Thiết kế đúng thứ tự sau. Có thể rút gọn câu chữ trên slide, nhưng không
bỏ các ý chính.

### Slide 1 --- Tên đề tài

Tên đề tài đầy đủ. Thông tin sinh viên, giảng viên hướng dẫn,
khoa/trường có thể để placeholder nếu chưa được cung cấp. Không tự tạo
tên hoặc logo.

### Slide 2 --- Nội dung trình bày

Sáu phần: 1. Giới thiệu đề tài. 2. Đối tượng và phạm vi nghiên cứu. 3.
Thiết kế kiến trúc hệ thống. 4. Xây dựng hệ thống phần mềm. 5. Chức năng
và giải pháp kỹ thuật. 6. Kế hoạch thực hiện.

### Slide 3 --- Lý do chọn đề tài

-   PLC phổ biến trong tự động hóa công nghiệp.
-   Khó giám sát/quản lý nhiều thiết bị từ xa nếu chỉ dựa vào giao diện
    tại chỗ.
-   Dữ liệu có thể gián đoạn khi mạng mất.
-   Cần một Gateway làm cầu nối PLC với máy chủ/Web SCADA.

### Slide 4 --- Mục tiêu đề tài

-   Thiết kế Gateway ESP32-S3.
-   Giao tiếp PLC và thu thập dữ liệu.
-   MQTT lên máy chủ.
-   Web SCADA giám sát/điều khiển/lịch sử.
-   Store-and-forward.
-   Nghiên cứu remote programming cho PLC đại diện.

### Slide 5 --- Đối tượng PLC

Trình bày bảng 3 dòng: - Delta DVP-ES --- RS485 / Modbus RTU (dự
kiến). - Mitsubishi FX3U --- RS422 hoặc RS485 tùy cấu hình thực tế. -
Siemens S7-1200 --- Ethernet (dự kiến).

Ghi chú: giao thức/cổng phải xác minh theo model, phiên bản và phần cứng
thực tế.

### Slide 6 --- Kiến trúc tổng thể

Sơ đồ trực quan: PLC(s) → Gateway(s) → MQTT Broker/Host → Database + Web
SCADA → Người dùng. Thể hiện chiều command ngược lại từ Web đến PLC.
Nhấn mạnh nhiều Gateway kết nối một Host tập trung.

### Slide 7 --- Kiến trúc phần cứng Gateway

Sơ đồ khối: ESP32-S3-WROOM-1 ở trung tâm; W5500/Ethernet; Wi-Fi;
microSD; Communication Module/transceiver; PLC. Ghi chú FreeRTOS chạy
trên ESP32-S3. Không cần đưa thông số linh kiện chưa chốt.

### Slide 8 --- Điểm nhấn: Communication Module

Thể hiện Gateway Base tách khỏi Communication Module. Minh họa ba giao
diện DB8 được quy ước: RS422, RS232, RS485. Nhấn mạnh tính module hóa và
khả năng mở rộng. Ghi chú DB8 là connector riêng của thiết kế, pinout
đang được xác định.

### Slide 9 --- Kiến trúc phần mềm

Hai khối: - Gateway Firmware: FreeRTOS, PLC Driver Manager, Data
Acquisition, MQTT, SD Logger, Health Monitor. - Host: Mosquitto,
Node-RED, InfluxDB, Ubuntu/Docker. Có thể thêm một luồng dữ liệu mẫu PLC
→ driver → data model → MQTT → database/dashboard.

### Slide 10 --- Chức năng Web SCADA

Các khu vực giao diện minh họa: - Tổng quan Gateway/PLC. - Trạng thái
online/offline. - Giá trị tag. - Điều khiển có kiểm soát. - Lịch sử dữ
liệu. - Alarm/event. Nếu tạo mockup, phải ghi là giao diện minh họa/dự
kiến, không phải ảnh chụp sản phẩm đã hoàn thiện.

### Slide 11 --- Xử lý mất mạng

Sơ đồ: Normal → MQTT → Host. Network loss → local microSD buffer.
Reconnect → replay/sync → Host ACK → mark synced. Thể hiện
sequence/event ID để chống trùng. Nêu mục đích duy trì thu thập và đồng
bộ sau outage.

### Slide 12 --- Kết quả đã có

Chỉ nêu kết quả khảo sát/định hướng thiết kế: - Kiến trúc
PLC--Gateway--Host--Web. - Định hướng ESP32-S3, W5500, microSD,
FreeRTOS. - Lựa chọn MQTT, Ubuntu/Docker, Node-RED, InfluxDB. - Định
hướng PLC Driver Manager. - Ý tưởng Communication Module với
RS232/RS422/RS485. Không trình bày như phần cứng/firmware đã hoàn thiện
nếu chưa có minh chứng.

### Slide 13 --- Dự kiến kết quả đạt được

-   Thiết bị Gateway có Ethernet và kết nối PLC.
-   Firmware đọc PLC, MQTT và lưu offline.
-   Host + Web SCADA.
-   Thực nghiệm tối thiểu 3 Gateway, nhiều PLC trong phạm vi.
-   Remote programming thử nghiệm trên một PLC đại diện. Có thể trình
    bày dạng checklist sản phẩm bàn giao.

### Slide 14 --- Dự kiến các công việc triển khai

1.  Khảo sát PLC và giao thức.
2.  Thiết kế Gateway Base, Communication Module, schematic/PCB.
3.  Phát triển firmware FreeRTOS và PLC drivers.
4.  Xây dựng Host và Web SCADA.
5.  Tích hợp, kiểm thử nhiều Gateway và PLC.
6.  Thử nghiệm remote programming trên PLC đại diện.
7.  Đánh giá, hoàn thiện và viết báo cáo.

Đây là công việc một người thực hiện, không chia nhóm/phân công nhân sự.

### Slide 15 --- Tiến độ dự kiến

Timeline 12 giai đoạn: 1. Khảo sát PLC/giao thức. 2. Thiết kế kiến trúc.
3. Schematic và PCB. 4. Firmware cơ bản. 5. PLC drivers. 6. MQTT và Host
Server. 7. Web SCADA. 8. Store-and-Forward. 9. Tích hợp 3 Gateway. 10.
Remote programming. 11. Kiểm thử/đánh giá. 12. Báo cáo và demo.

Không tự gán ngày/tháng nếu chưa có lịch chính thức. Có thể dùng
timeline GĐ1--GĐ12.

### Slide 16 --- Kết luận / Sản phẩm cuối

Tóm tắt giải pháp PLC--Gateway--Host--Web SCADA. Danh sách sản phẩm dự
kiến: - Gateway phần cứng. - Communication Module. - Firmware. - Host
Server. - Web SCADA. - Mô hình thực nghiệm nhiều Gateway/PLC. - Kết quả
thử nghiệm remote programming cho PLC đại diện. Kết thúc bằng "Em xin
cảm ơn quý thầy cô".

------------------------------------------------------------------------

## 8. Nội dung lời thuyết trình / speaker notes

Dưới đây là lời trình bày tham khảo. Có thể đưa vào Speaker Notes của
từng slide hoặc tách thành file script. Không nên nhồi toàn bộ lời nói
lên mặt slide.

### Slide 1 --- Tên đề tài

"Kính thưa quý thầy cô! Em xin trình bày đề tài đồ án tốt nghiệp với tên
gọi: 'Nghiên cứu, thiết kế thiết bị IoT Gateway phục vụ giám sát, điều
khiển và nạp chương trình từ xa cho PLC công nghiệp.'

Đề tài tập trung nghiên cứu và thiết kế một thiết bị IoT Gateway có khả
năng kết nối với các bộ điều khiển PLC công nghiệp, thu thập dữ liệu vận
hành, truyền dữ liệu lên máy chủ và hỗ trợ người dùng giám sát, điều
khiển hệ thống từ xa thông qua giao diện Web.

Bên cạnh đó, đề tài cũng nghiên cứu giải pháp hỗ trợ kết nối và nạp
chương trình PLC từ xa thông qua một kênh kết nối riêng.

Sau đây, em xin trình bày các nội dung chính của đề tài."

### Slide 2 --- Nội dung trình bày

"Bài thuyết trình của em gồm sáu nội dung chính.

Thứ nhất là giới thiệu đề tài, bao gồm lý do lựa chọn và mục tiêu nghiên
cứu.

Thứ hai là trình bày các đối tượng PLC và phạm vi nghiên cứu.

Thứ ba là kiến trúc tổng thể của hệ thống, trong đó có thiết kế phần
cứng Gateway và module giao tiếp PLC.

Thứ tư là phương án xây dựng phần mềm, bao gồm firmware trên Gateway,
máy chủ và giao diện Web SCADA.

Thứ năm là các chức năng chính của hệ thống như giám sát, điều khiển từ
xa, lưu trữ dữ liệu khi mất kết nối và hỗ trợ nạp chương trình PLC từ
xa.

Cuối cùng là các công việc triển khai, tiến độ và kết quả dự kiến của đề
tài."

### Slide 3 --- Lý do chọn đề tài

"Hiện nay, PLC được sử dụng rộng rãi trong các hệ thống tự động hóa công
nghiệp như dây chuyền sản xuất, hệ thống điều khiển máy móc và các hệ
thống giám sát thiết bị.

Tuy nhiên, trong nhiều hệ thống thực tế, việc giám sát và điều khiển PLC
vẫn chủ yếu được thực hiện trực tiếp tại máy hoặc thông qua các hệ thống
SCADA có sẵn.

Điều này gây ra một số hạn chế khi người vận hành cần theo dõi thiết bị
ở vị trí khác, quản lý nhiều máy móc hoặc truy cập dữ liệu vận hành từ
xa.

Ngoài ra, khi xảy ra sự cố kết nối mạng, dữ liệu thu thập từ PLC có thể
bị gián đoạn nếu thiết bị không có cơ chế lưu trữ tạm thời.

Từ những vấn đề đó, em lựa chọn nghiên cứu thiết kế một thiết bị IoT
Gateway nhằm kết nối PLC với hệ thống máy chủ, tạo nền tảng cho việc
giám sát, điều khiển và quản lý thiết bị từ xa."

### Slide 4 --- Mục tiêu đề tài

"Mục tiêu tổng quát của đề tài là nghiên cứu và thiết kế một thiết bị
IoT Gateway có khả năng kết nối với PLC công nghiệp và truyền dữ liệu
lên hệ thống máy chủ.

Cụ thể, đề tài hướng đến các mục tiêu sau:

Thứ nhất, thiết kế phần cứng Gateway sử dụng vi điều khiển ESP32-S3, kết
hợp Ethernet, Wi-Fi và thẻ nhớ microSD.

Thứ hai, xây dựng firmware có khả năng giao tiếp với PLC, đọc dữ liệu và
truyền dữ liệu lên máy chủ thông qua giao thức MQTT.

Thứ ba, xây dựng hệ thống máy chủ và giao diện Web SCADA để người dùng
có thể theo dõi trạng thái, xem dữ liệu lịch sử và thực hiện một số thao
tác điều khiển từ xa.

Thứ tư, nghiên cứu cơ chế lưu trữ dữ liệu khi mất kết nối mạng và tự
động đồng bộ lại khi kết nối được khôi phục.

Cuối cùng, nghiên cứu phương án hỗ trợ nạp chương trình PLC từ xa thông
qua kết nối mạng riêng."

### Slide 5 --- Đối tượng PLC

"Trong phạm vi nghiên cứu, em dự kiến lựa chọn một số dòng PLC đại diện
để thử nghiệm khả năng kết nối của Gateway.

Các dòng PLC dự kiến bao gồm Delta DVP-ES, Mitsubishi FX3U và Siemens
S7-1200.

Đối với Delta DVP-ES, phương án giao tiếp dự kiến là RS485 sử dụng giao
thức Modbus RTU.

Đối với Mitsubishi FX3U, đề tài sẽ khảo sát cổng truyền thông và phần
cứng thực tế để lựa chọn phương án RS422 hoặc RS485 phù hợp.

Đối với Siemens S7-1200, phương án kết nối dự kiến là Ethernet.

Việc lựa chọn các dòng PLC này nhằm nghiên cứu khả năng kết nối với
những giao diện và giao thức truyền thông khác nhau.

Em không đặt mục tiêu hỗ trợ tất cả các dòng PLC trên thị trường, mà tập
trung xây dựng kiến trúc có khả năng mở rộng driver để bổ sung các thiết
bị khác trong tương lai."

### Slide 6 --- Kiến trúc tổng thể hệ thống

"Đây là kiến trúc tổng thể của hệ thống mà em dự kiến xây dựng.

Hệ thống được chia thành ba thành phần chính: PLC công nghiệp, IoT
Gateway và Host Server.

Ở tầng thiết bị, các PLC thực hiện nhiệm vụ điều khiển máy móc và cung
cấp dữ liệu vận hành.

Ở tầng Gateway, thiết bị IoT Gateway kết nối với PLC thông qua các giao
diện truyền thông tương ứng. Gateway thực hiện đọc dữ liệu, xử lý dữ
liệu và gửi thông tin lên máy chủ.

Ở tầng máy chủ, Host tiếp nhận dữ liệu từ các Gateway thông qua MQTT
Broker, sau đó xử lý và lưu trữ dữ liệu.

Cuối cùng, người dùng truy cập vào giao diện Web SCADA để theo dõi trạng
thái các PLC, xem lịch sử hoạt động và gửi lệnh điều khiển.

Một điểm quan trọng trong kiến trúc này là hệ thống được thiết kế theo
mô hình nhiều Gateway kết nối về một máy chủ tập trung. Nhờ đó, có thể
mở rộng hệ thống để giám sát nhiều PLC hoặc nhiều khu vực khác nhau."

### Slide 7 --- Kiến trúc phần cứng Gateway

"Về phần cứng, thành phần điều khiển trung tâm của Gateway là
ESP32-S3-WROOM-1.

ESP32-S3 đảm nhiệm việc xử lý dữ liệu, điều khiển các tác vụ giao tiếp
và quản lý hoạt động của thiết bị.

Hệ thống sử dụng W5500 để cung cấp kết nối Ethernet có dây. Đây là
phương án kết nối chính nhằm đảm bảo đường truyền ổn định trong môi
trường công nghiệp.

Ngoài ra, ESP32-S3 còn hỗ trợ Wi-Fi để phục vụ cấu hình hoặc kết nối
trong những trường hợp phù hợp.

Thẻ nhớ microSD được sử dụng để lưu trữ dữ liệu tạm thời khi mất kết nối
với máy chủ.

Về phần mềm nhúng, em dự kiến sử dụng FreeRTOS để tổ chức các tác vụ
riêng biệt, chẳng hạn như đọc dữ liệu PLC, truyền MQTT, ghi dữ liệu vào
thẻ nhớ và giám sát trạng thái thiết bị.

Việc phân chia thành các tác vụ giúp hệ thống dễ quản lý và thuận tiện
hơn khi phát triển thêm các chức năng."

### Slide 8 --- Communication Module

"Một điểm em muốn tập trung nghiên cứu trong thiết kế phần cứng là
Communication Module, tức module giao tiếp với PLC.

Thay vì tích hợp cố định tất cả các mạch giao tiếp vào một bo mạch duy
nhất, em định hướng thiết kế Gateway theo hai phần: Gateway Base và
Communication Module.

Gateway Base đảm nhiệm xử lý trung tâm, kết nối Ethernet, Wi-Fi và lưu
trữ dữ liệu.

Communication Module đảm nhiệm việc chuyển đổi và bảo vệ các giao diện
truyền thông kết nối với PLC.

Trong phương án thiết kế ban đầu, module dự kiến có ba cổng DB8, lần
lượt phục vụ giao tiếp RS422, RS232 và RS485.

Tại mỗi cổng sẽ có mạch transceiver phù hợp, cùng với các giải pháp bảo
vệ tín hiệu và cách ly nếu cần thiết.

Lưu ý rằng DB8 ở đây là loại đầu nối do đề tài quy định về mặt cơ khí và
pinout, không phải một chuẩn truyền thông công nghiệp.

Việc tách module giao tiếp giúp thiết kế có tính linh hoạt hơn. Trong
tương lai, có thể nghiên cứu thay đổi hoặc bổ sung module phù hợp với
từng loại PLC mà không cần thay đổi toàn bộ Gateway Base."

### Slide 9 --- Kiến trúc phần mềm

"Về phần mềm, hệ thống được chia thành hai phần: firmware trên Gateway
và phần mềm trên Host Server.

Đối với Gateway, firmware được xây dựng trên nền FreeRTOS.

Các nhóm chức năng chính gồm quản lý kết nối mạng, quản lý driver PLC,
thu thập dữ liệu, xử lý lệnh điều khiển, truyền thông MQTT và lưu trữ dữ
liệu cục bộ.

Trong đó, PLC Driver Manager có nhiệm vụ lựa chọn driver tương ứng với
loại PLC được cấu hình.

Đối với Host Server, em dự kiến sử dụng Ubuntu Server kết hợp Docker để
triển khai các dịch vụ.

Mosquitto được sử dụng làm MQTT Broker để tiếp nhận và phân phối dữ
liệu.

Node-RED đảm nhiệm một phần xử lý logic và xây dựng giao diện giám sát.

InfluxDB được sử dụng để lưu trữ dữ liệu vận hành theo thời gian, phục
vụ việc truy xuất và hiển thị lịch sử.

Các thành phần này được lựa chọn nhằm giảm thời gian phát triển phần mềm
nền tảng và tập trung vào chức năng chính của đề tài."

### Slide 10 --- Chức năng Web SCADA

"Giao diện Web SCADA là nơi người dùng tương tác với toàn bộ hệ thống.

Trên giao diện này, người dùng có thể xem danh sách các Gateway và PLC
đang được kết nối.

Hệ thống hiển thị trạng thái kết nối, trạng thái hoạt động và các giá
trị dữ liệu được thu thập từ PLC.

Ngoài chức năng giám sát, người dùng có thể gửi lệnh điều khiển đến các
PLC thông qua Gateway, ví dụ như điều khiển một số bit hoặc thanh ghi đã
được cấu hình cho phép.

Khi gửi lệnh, hệ thống cần theo dõi quá trình thực hiện và nhận phản hồi
từ Gateway.

Sau khi PLC thực hiện lệnh, Gateway có thể đọc lại trạng thái để xác
nhận kết quả. Như vậy, hệ thống không chỉ kiểm tra việc gửi lệnh thành
công mà còn quan tâm đến kết quả thực tế ở phía PLC.

Ngoài ra, giao diện còn có chức năng xem dữ liệu lịch sử, theo dõi cảnh
báo và trạng thái hoạt động của từng Gateway.

Các chức năng điều khiển sẽ được giới hạn theo quyền và cấu hình, không
thay thế các mạch bảo vệ hoặc liên động an toàn của hệ thống máy móc."

### Slide 11 --- Xử lý mất mạng

"Một chức năng quan trọng khác của đề tài là xử lý tình huống mất kết
nối giữa Gateway và máy chủ.

Trong điều kiện mạng bình thường, Gateway sẽ đọc dữ liệu PLC và gửi dữ
liệu lên Host thông qua MQTT.

Khi mất kết nối, Gateway vẫn tiếp tục thực hiện việc thu thập dữ liệu từ
PLC. Thay vì gửi lên máy chủ, dữ liệu sẽ được lưu tạm vào thẻ nhớ
microSD.

Khi kết nối mạng được khôi phục, Gateway sẽ lần lượt gửi lại những dữ
liệu chưa được đồng bộ.

Máy chủ tiếp nhận dữ liệu, lưu vào cơ sở dữ liệu và phản hồi xác nhận
cho Gateway.

Gateway chỉ đánh dấu dữ liệu đã đồng bộ sau khi nhận được xác nhận ở
tầng ứng dụng.

Để hạn chế dữ liệu trùng lặp khi gửi lại, mỗi bản ghi sẽ có thông tin
định danh như Gateway ID, Boot ID và Sequence Number.

Giải pháp này được gọi là Store-and-Forward. Nó giúp hệ thống duy trì
khả năng thu thập dữ liệu khi mạng không ổn định, đồng thời hỗ trợ đồng
bộ dữ liệu sau khi kết nối được phục hồi."

### Slide 12 --- Kết quả đã có

"Ở giai đoạn hiện tại, em đã xác định được định hướng kiến trúc và các
thành phần chính của hệ thống.

Cụ thể, kiến trúc được chia thành ba phần: PLC, IoT Gateway và Host
Server.

Về phần cứng, em đã định hướng sử dụng ESP32-S3, W5500 và microSD làm
các thành phần chính của Gateway.

Về phần mềm, các công nghệ dự kiến sử dụng đã được xác định, bao gồm
FreeRTOS, MQTT, Ubuntu Server, Docker, Node-RED và InfluxDB.

Ngoài ra, em cũng đã xây dựng phương án phân chia chức năng giữa Gateway
Base và Communication Module, trong đó có định hướng sử dụng các giao
diện RS232, RS422 và RS485.

Đây là những kết quả ở mức khảo sát, lựa chọn giải pháp và thiết kế định
hướng. Các phần cứng, firmware và chức năng thực tế sẽ tiếp tục được
triển khai, tích hợp và kiểm thử trong các giai đoạn tiếp theo."

### Slide 13 --- Dự kiến kết quả đạt được

"Sau khi hoàn thành đề tài, em dự kiến đạt được các kết quả chính sau:

Thứ nhất, thiết kế và chế tạo được thiết bị IoT Gateway sử dụng
ESP32-S3, có kết nối Ethernet và khả năng giao tiếp với PLC.

Thứ hai, xây dựng được firmware có khả năng thu thập dữ liệu PLC, truyền
dữ liệu qua MQTT và lưu trữ dữ liệu khi mất kết nối.

Thứ ba, xây dựng được Host Server và giao diện Web SCADA phục vụ giám
sát, điều khiển và xem lịch sử dữ liệu.

Thứ tư, triển khai thử nghiệm hệ thống với tối thiểu ba Gateway kết nối
về một máy chủ và nhiều PLC thuộc các dòng khác nhau trong phạm vi đề
tài.

Cuối cùng, thực hiện thử nghiệm phương án kết nối và nạp chương trình
PLC từ xa đối với một dòng PLC đại diện.

Các kết quả sẽ được đánh giá thông qua khả năng hoạt động thực tế, độ ổn
định kết nối, tính chính xác của dữ liệu và khả năng phục hồi sau khi
mất mạng."

### Slide 14 --- Dự kiến các công việc triển khai

"Để thực hiện đề tài, em dự kiến chia công việc thành bảy nhóm chính.

Đầu tiên là khảo sát PLC, giao thức truyền thông và xác định các yêu cầu
chức năng của hệ thống.

Tiếp theo là thiết kế phần cứng, bao gồm Gateway Base, Communication
Module, sơ đồ nguyên lý và PCB.

Nhóm công việc thứ ba là phát triển firmware trên ESP32-S3, bao gồm quản
lý tác vụ FreeRTOS, giao tiếp PLC, MQTT và lưu trữ dữ liệu.

Thứ tư là xây dựng Host Server và giao diện Web SCADA, triển khai các
dịch vụ tiếp nhận dữ liệu, lưu trữ, giám sát và điều khiển.

Thứ năm là tích hợp hệ thống và kiểm thử với PLC thực tế, đồng thời đánh
giá khả năng hoạt động của nhiều Gateway.

Thứ sáu là nghiên cứu và thử nghiệm chức năng nạp chương trình PLC từ xa
đối với một PLC đại diện.

Cuối cùng là đánh giá kết quả, hoàn thiện phần cứng, phần mềm, tài liệu
kỹ thuật và báo cáo đồ án."

### Slide 15 --- Tiến độ dự kiến

"Về tiến độ, em dự kiến triển khai đề tài theo từng giai đoạn.

Giai đoạn đầu tiên là khảo sát PLC, giao thức truyền thông và xác định
yêu cầu hệ thống.

Sau đó là thiết kế kiến trúc, sơ đồ nguyên lý và PCB cho thiết bị
Gateway.

Khi phần cứng đã có phương án cụ thể, em sẽ tiến hành phát triển
firmware cơ bản, sau đó bổ sung các driver giao tiếp PLC.

Tiếp theo là triển khai MQTT, Host Server và xây dựng giao diện Web
SCADA.

Sau khi các thành phần hoạt động riêng lẻ, em sẽ tích hợp toàn bộ hệ
thống và triển khai cơ chế Store-and-Forward.

Giai đoạn tiếp theo là thử nghiệm với tối thiểu ba Gateway, đánh giá khả
năng kết nối đồng thời và độ ổn định của hệ thống.

Cuối cùng là thử nghiệm chức năng nạp chương trình từ xa, kiểm thử tổng
thể, đánh giá kết quả và hoàn thiện báo cáo.

Các giai đoạn có thể được điều chỉnh tùy theo tiến độ chế tạo phần cứng
và khả năng tiếp cận các dòng PLC thực tế."

### Slide 16 --- Kết luận và sản phẩm cuối

"Tóm lại, đề tài hướng đến việc nghiên cứu và thiết kế một thiết bị IoT
Gateway có khả năng kết nối PLC công nghiệp với hệ thống máy chủ, phục
vụ giám sát, điều khiển và quản lý dữ liệu từ xa.

Điểm chính của đề tài là kết hợp phần cứng Gateway, firmware nhúng, giao
thức MQTT và Web SCADA trong một hệ thống thống nhất.

Bên cạnh đó, đề tài cũng tập trung giải quyết hai vấn đề thực tế là lưu
trữ dữ liệu khi mất kết nối mạng và hỗ trợ truy cập phục vụ nạp chương
trình PLC từ xa.

Sản phẩm cuối dự kiến bao gồm thiết bị Gateway, Communication Module,
firmware, Host Server, giao diện Web SCADA và mô hình thực nghiệm với
nhiều Gateway kết nối đến các PLC.

Thông qua đề tài, em mong muốn vận dụng kiến thức về vi điều khiển,
truyền thông công nghiệp, thiết kế mạch điện tử và hệ thống IoT để xây
dựng một giải pháp có khả năng ứng dụng thực tế trong lĩnh vực tự động
hóa.

Phần trình bày của em đến đây là kết thúc.

Em xin cảm ơn quý thầy cô đã lắng nghe và mong nhận được những ý kiến
đóng góp để em có thể hoàn thiện đề tài tốt hơn."

------------------------------------------------------------------------

## 9. Định hướng thiết kế hình ảnh và bố cục

### Phong cách

-   Tỷ lệ 16:9.
-   Phong cách industrial / engineering / modern technical.
-   Nền sáng trắng hoặc xanh navy đậm; chọn một hệ màu nhất quán.
-   Màu nhấn gợi ý: xanh cyan hoặc xanh dương kỹ thuật; màu cam chỉ dùng
    cho cảnh báo/điểm nhấn.
-   Font sans-serif dễ đọc, hỗ trợ tiếng Việt tốt.
-   Tiêu đề rõ, nội dung ngắn, tương phản cao.
-   Không dùng hiệu ứng chuyển cảnh rườm rà.

### Quy tắc nội dung

-   Một slide chỉ có một thông điệp chính.
-   Hạn chế đoạn văn; ưu tiên 3--6 ý ngắn/slide.
-   Không chép toàn bộ speaker notes lên slide.
-   Sơ đồ phải có mũi tên và nhãn luồng dữ liệu/điều khiển rõ ràng.
-   Giữ cách gọi thống nhất: Gateway, Host Server, PLC, Web SCADA,
    Communication Module, Store-and-Forward.
-   Dùng tiếng Việt cho diễn giải, giữ tên giao thức/công nghệ bằng
    tiếng Anh.
-   Nếu chưa có ảnh sản phẩm thực, dùng sơ đồ khối, icon hoặc hình minh
    họa có nhãn "minh họa".
-   Không tạo ảnh PCB/thiết bị giả rồi trình bày như ảnh chụp prototype
    thật.

### Các slide nên có sơ đồ

-   Slide 6: sơ đồ kiến trúc tổng thể.
-   Slide 7: sơ đồ khối phần cứng Gateway.
-   Slide 8: sơ đồ Gateway Base và Communication Module.
-   Slide 9: kiến trúc firmware/Host.
-   Slide 10: mockup giao diện Web SCADA.
-   Slide 11: flowchart Store-and-Forward.
-   Slide 14--15: checklist công việc và timeline.

### Tính nhất quán

Có thể dùng một sơ đồ kiến trúc tổng thể làm hình nền tham chiếu, sau đó
phóng to từng khu vực ở các slide phần cứng/phần mềm. Dùng cùng biểu
tượng cho PLC, Gateway, Server, Database và người vận hành.

## 10. Những điểm kỹ thuật cần tránh nói sai

1.  Không nói DB8 là chuẩn công nghiệp; đó là connector được quy ước
    trong thiết kế.
2.  Không khẳng định pinout DB8 khi chưa chốt.
3.  Không nối trực tiếp UART ESP32 với đường RS232/RS422/RS485; cần
    transceiver.
4.  Không khẳng định tất cả PLC đều dùng cùng giao thức hoặc cùng cổng.
5.  Không nói MQTT PUBACK chứng minh PLC đã thực hiện lệnh.
6.  Không nói MQTT là kênh nạp chương trình PLC.
7.  Không khẳng định TCP-to-Serial bridge tương thích phần mềm lập trình
    PLC khi chưa thử nghiệm.
8.  Không tuyên bố dữ liệu không thể mất; trình bày cơ chế lưu tạm, đồng
    bộ và deduplication.
9.  Không khẳng định cách ly 3 kV, độ chính xác timestamp hoặc thông số
    điện nếu chưa có thiết kế/đo kiểm xác nhận.
10. Không trình bày thiết kế dự kiến như phần cứng hoặc phần mềm đã hoàn
    thành.
11. Không làm hệ thống phình thành nền tảng cloud/enterprise vượt phạm
    vi đồ án.
12. Không mô tả việc vô hiệu hóa interlock, E-stop hoặc cơ chế bảo vệ
    máy.

## 11. Câu hỏi giảng viên có thể đặt ra

Agent có thể thêm một slide phụ lục Q&A nếu người dùng yêu cầu, nhưng
không nhất thiết đưa vào 16 slide chính.

-   Vì sao chọn ESP32-S3 thay vì PLC hoặc máy tính công nghiệp?
-   Vai trò của W5500 là gì? Vì sao chọn Ethernet làm kết nối chính?
-   Tại sao dùng MQTT? MQTT khác Modbus như thế nào?
-   Gateway hỗ trợ nhiều loại PLC bằng cách nào?
-   RS232, RS422 và RS485 khác nhau ở đâu?
-   Vì sao cần Communication Module và DB8?
-   Khi mất mạng, dữ liệu được lưu và đồng bộ như thế nào?
-   Làm sao tránh dữ liệu bị ghi trùng khi gửi lại?
-   Làm sao xác nhận lệnh điều khiển đã được PLC thực hiện?
-   Vì sao không dùng MQTT để nạp chương trình PLC?
-   Nạp chương trình từ xa có rủi ro gì và giới hạn phạm vi ra sao?
-   Làm thế nào chứng minh hệ thống hoạt động với ít nhất ba Gateway?
-   Những chức năng nào là bắt buộc, chức năng nào là mở rộng?

## 12. Chỉ dẫn cuối cho Agent

Hãy tạo PowerPoint theo đúng 16 slide đã nêu. Mỗi slide nên có: - Tiêu
đề ngắn. - Nội dung hiển thị cô đọng. - Hình/sơ đồ phù hợp khi cần. -
Speaker Notes dựa trên phần lời thuyết trình tương ứng.

Ưu tiên tính chính xác kỹ thuật, mạch kể chuyện rõ ràng và khả năng
thuyết trình trước giảng viên. Không tự thêm kết quả thực nghiệm, số
liệu hiệu năng, thông số điện, ảnh prototype hoặc tính năng chưa được
xác nhận. Nếu cần placeholder, ghi rõ `[BỔ SUNG THÔNG TIN]` thay vì tự
suy đoán.
