# Firmware — IoT Gateway ESP32-S3

ESP-IDF v5.x project for the industrial IoT Gateway bridging Delta DVP PLC (Modbus RTU) to MQTT.

## Build & Flash

```bash
# From this directory
idf.py set-target esp32s3
idf.py menuconfig       # set Wi-Fi SSID, MQTT broker, IDs
idf.py build
idf.py -p /dev/ttyUSB0 flash monitor
```

Default partition table (`partitions.csv`) uses 8 MB flash: OTA_0/OTA_1 apps + FAT journal for store-and-forward.

## Key Files

| File | Purpose |
|---|---|
| `main/app_main.c` | Entry point; creates all FreeRTOS tasks |
| `main/modbus_rtu.c` | Modbus RTU master on UART2 (RS-485 38400 8E1) |
| `main/mqtt_gateway.c` | ESP-MQTT client matching the `MqttContracts.cs` schema v1 |
| `main/store_forward.c` | PSRAM/FAT ring buffer with sequence + dedup |
| `main/quality_cov.c` | COV/deadband engine + quality codes |
| `main/tag_map.c` | Delta DVP address map (S/X/Y/M/T/C/D) |
| `main/prog_bridge.c` | TCP :8899 programming bridge (UART1 raw pipe) |
| `main/app_config.h` | Pin, baud, timing, and task placement constants |
| `main/Kconfig.projbuild` | Wi-Fi/ MQTT/ ID configuration via `menuconfig` |

## MQTT Topics (schema v1)

- `plc/<gateway_id>/telemetry` — QoS1 publish
- `plc/<gateway_id>/status` — QoS1 retain, LWT offline
- `plc/<gateway_id>/cmd` — QoS1 subscribe
- `plc/<gateway_id>/ack` — QoS1 publish on command ack
- `plc/<gateway_id>/telemetry_ack` — QoS1 publish on receipt ack
- `plc/<gateway_id>/config` — reserved for future use

Payloads match `control-csharp/src/Control.Domain/MqttContracts.cs` exactly.

## FreeRTOS Task Placement

| Task | Core | Priority | Role |
|---|---|---|---|
| TaskModbus | 1 | 3 | Modbus RTU poll every 300 ms |
| TaskFlashBridge | 1 | 4 | Programming bridge TCP server |
| TaskUsbCdc | 0 | 2 | USB CDC stub (ESP32-S3 USB OTG) |
| TaskNetwork | 0 | 2 | Wi-Fi + MQTT + SNTP |
| TaskPipeline | 0 | 1 | COV + telemetry publish |

## Hardware Requirements

See `hardware/BOM.md` — ESP32-S3-WROOM-1 N8R2, ADM2587E (RS-485), ADM3251E (RS-232), TPS54360 (24 V→3.3 V), SM712 TVS.