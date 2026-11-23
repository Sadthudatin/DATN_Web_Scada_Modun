#include "sdkconfig.h"
#include "esp_system.h"
#include "esp_log.h"
#include "esp_wifi.h"
#include "esp_event.h"
#include "nvs_flash.h"
#include "lwip/err.h"
#include "lwip/sockets.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

#include "driver/uart.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/semphr.h"
#include "freertos/event_groups.h"

#include "app_config.h"
#include "app_state.h"
#include "modbus_rtu.h"
#include "mqtt_gateway.h"
#include "store_forward.h"
#include "quality_cov.h"
#include "tag_map.h"
#include "prog_bridge.h"

static const char *TAG = "app_main";

/* Event group for Wi-Fi connected */
static EventGroupHandle_t g_wifi_event;

static void wifi_event_handler(void *arg, esp_event_base_t base, int32_t event_id, void *event_data)
{
    if (base == WIFI_EVENT && event_id == WIFI_EVENT_STA_START) {
        esp_wifi_connect();
    } else if (base == IP_EVENT && event_id == IP_EVENT_STA_GOT_IP) {
        xEventGroupSetBits(g_wifi_event, APP_EVT_IP_GOT);
    }
}

static void wifi_init_sta(void)
{
    g_wifi_event = xEventGroupCreate();
    ESP_ERROR_CHECK(esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP,
                                                        &wifi_event_handler, NULL, NULL));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(WIFI_EVENT, WIFI_EVENT_STA_START,
                                                        &wifi_event_handler, NULL, NULL));

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));
    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));

    wifi_config_t wifi_cfg = {0};
    strncpy((char *)wifi_cfg.sta.ssid, CONFIG_GW_WIFI_SSID, sizeof(wifi_cfg.sta.ssid));
    strncpy((char *)wifi_cfg.sta.password, CONFIG_GW_WIFI_PASSWORD, sizeof(wifi_cfg.sta.password));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wifi_cfg));
    ESP_ERROR_CHECK(esp_wifi_start());
}

static void task_modbus(void *arg)
{
    (void)arg;
    ESP_LOGI(TAG, "TaskModbus starting (core1, prio3)");

    if (!modbus_rtu_init()) {
        ESP_LOGE(TAG, "Modbus RTU init failed");
        vTaskDelete(NULL);
        return;
    }

    modbus_read_result_t result = {0};
    TickType_t last = xTaskGetTickCount();

    for (;;) {
        if (modbus_read_block(0x01, 0x0000, 10, &result)) { // read S0-S9
            // Convert to tag values; quality GOOD if modbus healthy
            bool healthy = modbus_rtu_healthy();
            // We'll just log; the actual telemetry is built in TaskPipeline
            ESP_LOGD(TAG, "Modbus poll OK, healthy=%d", healthy);
            vTaskDelay(pdMS_TO_TICKS(MB_POLL_PERIOD_MS));
        } else {
            ESP_LOGW(TAG, "Modbus poll fail (fail cnt %u)", modbus_fail_count());
            vTaskDelay(pdMS_TO_TICKS(1000));
        }
    }
}

static void task_flash_bridge(void *arg)
{
    (void)arg;
    ESP_LOGI(TAG, "TaskFlashBridge starting (core1, prio4)");

    prog_bridge_init();
    prog_bridge_set_enabled(true);

    for (;;) {
        // Just keep alive; actual session handling in prog_bridge_task
        vTaskDelay(pdMS_TO_TICKS(1000));
    }
}

static void task_usb_cdc(void *arg)
{
    (void)arg;
    ESP_LOGI(TAG, "TaskUsbCdc starting (core0, prio2)");

    // ESP32-S3 USB OTG device init if needed; for now just acknowledge
    vTaskDelay(pdMS_TO_TICKS(1000));

    for (;;) {
        vTaskDelay(pdMS_TO_TICKS(1000));
    }
}

static void task_network(void *arg)
{
    (void)arg;
    ESP_LOGI(TAG, "TaskNetwork starting (core0, prio2)");

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP,
                                                        &wifi_event_handler, NULL, NULL));

    if (CONFIG_GW_WIFI_SSID[0] != '\0') {
        wifi_init_sta();
    } else {
        ESP_LOGI(TAG, "Wi-Fi SSID empty; using W5500 Ethernet fallback");
        // TODO: W5500 init if board supports it
    }

    EventBits_t bits = xEventGroupWaitBits(g_wifi_event, APP_EVT_IP_GOT, pdFALSE, pdFALSE, pdMS_TO_TICKS(30000));
    if (bits & APP_EVT_IP_GOT) {
        ESP_LOGI(TAG, "IP obtained");
    } else {
        ESP_LOGW(TAG, "IP timeout, continuing without Wi-Fi");
    }

    /* MQTT init */
    mqtt_gw_init(CONFIG_GW_MQTT_URI, CONFIG_GW_MQTT_USERNAME, CONFIG_GW_MQTT_PASSWORD);
    mqtt_gw_start();

    /* publish initial status */
    mqtt_gw_publish_status(false);

    for (;;) {
        /* Periodic work: poll healthy, maybe COV etc. */
        vTaskDelay(pdMS_TO_TICKS(2000));
    }
}

static void task_pipeline(void *arg)
{
    (void)arg;
    ESP_LOGI(TAG, "TaskPipeline starting (core0, prio1)");

    /* Initialize components */
    if (!sf_init()) {
        ESP_LOGE(TAG, "Store-and-forward init failed");
    }

    /* Initialize COV engine with tag map */
    size_t tag_count;
    const tag_def_t *tags = tag_map_tags(&tag_count);
    cov_engine_init(tags, tag_count);
    cov_engine_force_full(); /* start fresh after reboot */

    /* Main pipeline loop: read Modbus → apply COV → publish telemetry */
    TickType_t last = xTaskGetTickCount();

    for (;;) {
        /* Modbus already polled by task_modbus; check health */
        bool mb_ok = modbus_rtu_healthy();

        /* Build telemetry JSON matching schema v1 */
        /* Sample a subset of tags; in production this would be all active tags */
        char json_buf[512] = {0};
        int pos = 0;

        /* X0 as example bit */
        // tags looked up via tag_map_find; here we just do simple index access

        /* Build payload */
        pos += snprintf(json_buf + pos, sizeof(json_buf) - pos,
                        "{\"schema\":\"v1\",\"gateway_id\":\"%s\",\"plc_id\":\"%s\",\"boot_id\":\"%s\",\"seq\":%u,\"ts\":\"",
                        mqtt_gw_gateway_id(), mqtt_gw_plc_id(), mqtt_gw_boot_id(),
                        sf_next_seq());

        /* timestamp */
        time_t now;
        struct tm tmv;
        time(&now);
        gmtime_r(&now, &tmv);
        char ts[24];
        strftime(ts, sizeof(ts), "%Y-%m-%dT%H:%M:%SZ", &tmv);
        pos += snprintf(json_buf + pos, sizeof(json_buf) - pos, "%s\",", ts);

        /* quality */
        quality_t q = mb_ok ? QUALITY_GOOD : QUALITY_BAD;
        pos += snprintf(json_buf + pos, sizeof(json_buf) - pos,
                        "\"quality\":\"%s\",", quality_str(q));

        /* tags: just a few demo ones; in real app iterate active set */
        /* S0 (input relay) - read holding reg at addr BASE_S+0, fc03 */
        /* For now set static demo values; the real system reads from Modbus */
        pos += snprintf(json_buf + pos, sizeof(json_buf) - pos,
                        "\"tags\":{\"S0\":%d,\"X0\":%d}", mb_ok ? 1 : 0, mb_ok ? 0 : 0);

        pos += snprintf(json_buf + pos, sizeof(json_buf) - pos, "}");

        /* Publish */
        int mid = mqtt_gw_publish_telemetry(json_buf, (size_t)pos);
        if (mid > 0) {
            /* ACK after publish */
            sf_record_t rec = {0};
            if (sf_peek_oldest(&rec)) {
                sf_ack(rec.seq);
            }
        }

        vTaskDelayUntil(&last, pdMS_TO_TICKS(300)); /* 300 ms poll */
    }
}

void app_main(void)
{
    ESP_LOGI(TAG, "IoT Gateway firmware v%s", GW_FW_VERSION);

    /* ESP-IDF init */
    ESP_ERROR_CHECK(nvs_flash_init());
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    g_wifi_event = xEventGroupCreate();

    esp_err_t wifi_err = esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP,
                                                              &wifi_event_handler, NULL, NULL);
    if (wifi_err == ESP_ERR_INSTANCE_ALREADY_REGISTERED) {
        /* ignore */
    }

    wifi_err = esp_event_handler_instance_register(WIFI_EVENT, WIFI_EVENT_STA_START,
                                                     &wifi_event_handler, NULL, NULL);
    if (wifi_err == ESP_ERR_INSTANCE_ALREADY_REGISTERED) {
        /* ignore */
    }

    /* Start Wi-Fi */
    if (CONFIG_GW_WIFI_SSID[0] != '\0') {
        wifi_init_sta();
    }

    /* Create tasks with proper core/prio */
    xTaskCreatePinnedToCore(task_modbus, "modbus", 4096, NULL, TASK_MODBUS_PRIO, NULL, TASK_MODBUS_CORE);
    xTaskCreatePinnedToCore(task_flash_bridge, "flash_bridge", 4096, NULL, TASK_FLASH_PRIO, NULL, TASK_FLASH_CORE);
    xTaskCreatePinnedToCore(task_usb_cdc, "usb_cdc", 4096, NULL, TASK_USB_PRIO, NULL, TASK_USB_CORE);
    xTaskCreatePinnedToCore(task_network, "network", 8192, NULL, TASK_NET_PRIO, NULL, TASK_NET_CORE);
    xTaskCreatePinnedToCore(task_pipeline, "pipeline", 8192, NULL, TASK_PIPE_PRIO, NULL, TASK_PIPE_CORE);

    /* Program bridge task on core0 (same as network/usb) */
    xTaskCreate(prog_bridge_task, "prog_bridge", 4096, NULL, 1, NULL);

    ESP_LOGI(TAG, "app_main done, tasks created");
}