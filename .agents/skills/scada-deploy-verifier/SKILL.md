---
name: scada-deploy-verifier
description: Kiểm chứng stack deploy của đồ án (Docker Compose, Mosquitto, Postgres, InfluxDB, API, Web SCADA) — port, env, healthcheck, smoke test end-to-end simulator → ingest → realtime. Không sửa code nghiệp vụ.
---

# SCADA Deploy Verifier

## Khi nào kích hoạt
- "docker không lên", "API không kết nối DB/MQTT", "web không thấy dữ liệu", "chuẩn bị demo/bảo vệ", "kiểm tra stack".
- Đọc `README.md` + `docs/specs/README_WEBSCADA.md` trước.

## Phạm vi
- `deploy/docker-compose.yml`, `deploy/**`, `tools/dev-broker/**`.
- `scada/` là cụm legacy (Node-RED/Grafana) — chỉ kiểm khi task nêu.
- Đọc `control-csharp/src/Control.Api/appsettings*.json`; env `MQTT__*`, `Influx__*`, `ConnectionStrings__Default`, `Jwt__Key`.

## Checklist
- [ ] Port khớp README: Web **5080**, API **5081**, MQTT **1884**, Influx **8087**, Postgres **5433**.
- [ ] `depends_on` + healthcheck; API chờ Postgres/MQTT sẵn sàng mới start.
- [ ] Env: `Influx__Enabled` đúng ý (dev=false → in-memory, mất khi restart); token/org/bucket khớp.
- [ ] JWT key ≥32 ký tự; admin seed `admin/Admin123!` chỉ dùng dev.
- [ ] Mosquitto anonymous chỉ cho dev; cảnh báo nếu expose public.
- [ ] Smoke: `docker compose up -d --build` → login → chạy simulator → thấy telemetry + alarm + command ack.
- [ ] Log API: không exception lặp; worker timeout/offline không spam.

## Lệnh
```bash
cd deploy && docker compose up -d --build
docker compose ps
docker compose logs --tail=100 api
```

## Output
Bảng: `Dịch vụ | Trạng thái | Bằng chứng (log/curl) | Rủi ro`. Không sửa code; nêu lệnh sửa cụ thể.

## Circuit breaker
Sau 3 lần stack vẫn fail → dừng, báo service + log cuối, không đốt thêm vòng.
