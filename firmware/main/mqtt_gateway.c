#include "mqtt_gateway.h"

#include "app_config.h"
#include "cJSON.h"
#include "esp_log.h"
#include "esp_mqtt_client.h"
#include "esp_random.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

static const char *TAG = "mqtt_gw";

static esp_mqtt_client_handle_t s_client;
static char s_gateway_id[32];
static char s_plc_id[32];
static char s_boot_id[32];
static char s_topic_telemetry[64];
static char s_topic_status[64];
static char s_topic_cmd[64];
static char s_topic_ack[64];
static char s_topic_config[64];
static char s_topic_tack[64];
static char s_lwt_payload[192];
static volatile bool s_online;

static mqtt_cmd_handler_t s_cmd_cb;
static mqtt_online_handler_t s_online_cb;
static mqtt_published_handler_t s_pub_cb;
static mqtt_tack_handler_t s_tack_cb;

static void iso8601_now(char *buf, size_t n)
{
    time_t now;
    struct tm tmv;
    time(&now);
    gmtime_r(&now, &tmv);
    strftime(buf, n, "%Y-%m-%dT%H:%M:%SZ", &tmv);
}

static bool topic_equals(const esp_mqtt_event_handle_t event, const char *topic)
{
    size_t len = strlen(topic);
    return event->topic_len == (int)len && memcmp(event->topic, topic, len) == 0;
}

static void handle_cmd(const esp_mqtt_event_handle_t event)
{
    cJSON *root = cJSON_ParseWithLength(event->data, event->data_len);
    if (!root) {
        ESP_LOGW(TAG, "cmd JSON parse failed");
        return;
    }

    const cJSON *cmd_id = cJSON_GetObjectItem(root, "cmd_id");
    const cJSON *target = cJSON_GetObjectItem(root, "target");
    const cJSON *value = cJSON_GetObjectItem(root, "value");
    const cJSON *ttl = cJSON_GetObjectItem(root, "ttl_ms");

    if (!cJSON_IsString(cmd_id) || !cJSON_IsString(target) || !value) {
        cJSON_Delete(root);
        return;
    }

    char *value_json = cJSON_PrintUnformatted(value);
    int ttl_ms = cJSON_IsNumber(ttl) ? ttl->valueint : 0;
    if (s_cmd_cb) {
        s_cmd_cb(cmd_id->valuestring, target->valuestring, value_json ? value_json : "null", ttl_ms);
    }
    if (value_json) {
        cJSON_free(value_json);
    }
    cJSON_Delete(root);
}

static void handle_tack(const esp_mqtt_event_handle_t event)
{
    cJSON *root = cJSON_ParseWithLength(event->data, event->data_len);
    if (!root) {
        return;
    }
    const cJSON *seq = cJSON_GetObjectItem(root, "seq");
    const cJSON *status = cJSON_GetObjectItem(root, "status");
    if (cJSON_IsNumber(seq) && cJSON_IsString(status) && s_tack_cb) {
        s_tack_cb((uint32_t)seq->valuedouble, status->valuestring);
    }
    cJSON_Delete(root);
}

static void mqtt_event_handler(void *arg, esp_event_base_t base, int32_t event_id, void *event_data)
{
    esp_mqtt_event_handle_t event = event_data;
    switch ((esp_mqtt_event_id_t)event_id) {
    case MQTT_EVENT_CONNECTED:
        s_online = true;
        mqtt_gw_publish_status(true);
        esp_mqtt_client_subscribe(s_client, s_topic_cmd, MQTT_QOS);
        esp_mqtt_client_subscribe(s_client, s_topic_tack, MQTT_QOS);
        ESP_LOGI(TAG, "connected, subscribed to %s and %s", s_topic_cmd, s_topic_tack);
        if (s_online_cb) {
            s_online_cb();
        }
        break;
    case MQTT_EVENT_DISCONNECTED:
        s_online = false;
        ESP_LOGW(TAG, "disconnected");
        break;
    case MQTT_EVENT_DATA:
        if (topic_equals(event, s_topic_cmd)) {
            handle_cmd(event);
        } else if (topic_equals(event, s_topic_tack)) {
            handle_tack(event);
        }
        break;
    case MQTT_EVENT_PUBLISHED:
        if (s_pub_cb) {
            s_pub_cb(event->msg_id);
        }
        break;
    case MQTT_EVENT_ERROR:
        if (event->error_handle->error_type == MQTT_ERROR_TYPE_TCP_TRANSPORT) {
            ESP_LOGE(TAG, "TCP transport error: %d", event->error_handle->esp_transport_sock_errno);
        }
        break;
    default:
        break;
    }
}

bool mqtt_gw_init(const char *uri, const char *username, const char *password)
{
    strlcpy(s_gateway_id, CONFIG_GW_GATEWAY_ID, sizeof(s_gateway_id));
    strlcpy(s_plc_id, CONFIG_GW_PLC_ID, sizeof(s_plc_id));
    snprintf(s_boot_id, sizeof(s_boot_id), "BOOT_%08X", esp_random());

    snprintf(s_topic_telemetry, sizeof(s_topic_telemetry), "plc/%s/telemetry", s_gateway_id);
    snprintf(s_topic_status, sizeof(s_topic_status), "plc/%s/status", s_gateway_id);
    snprintf(s_topic_cmd, sizeof(s_topic_cmd), "plc/%s/cmd", s_gateway_id);
    snprintf(s_topic_ack, sizeof(s_topic_ack), "plc/%s/ack", s_gateway_id);
    snprintf(s_topic_config, sizeof(s_topic_config), "plc/%s/config", s_gateway_id);
    snprintf(s_topic_tack, sizeof(s_topic_tack), "plc/%s/telemetry_ack", s_gateway_id);

    char ts[24];
    iso8601_now(ts, sizeof(ts));
    snprintf(s_lwt_payload, sizeof(s_lwt_payload),
             "{\"schema\":\"v1\",\"gateway_id\":\"%s\",\"status\":\"offline\",\"ts\":\"%s\"}",
             s_gateway_id, ts);

    esp_mqtt_client_config_t cfg = {
        .broker.address.uri = uri,
        .credentials.username = (username && username[0]) ? username : NULL,
        .credentials.authentication.password = (password && password[0]) ? password : NULL,
        .session.keepalive = MQTT_KEEPALIVE_S,
        .session.last_will.topic = s_topic_status,
        .session.last_will.msg = s_lwt_payload,
        .session.last_will.qos = MQTT_QOS,
        .session.last_will.retain = 1,
        .network.reconnect_timeout_ms = MQTT_RECONNECT_S * 1000,
        .task.stack_size = 6144,
        .task.priority = 5,
    };

    s_client = esp_mqtt_client_init(&cfg);
    if (!s_client) {
        ESP_LOGE(TAG, "esp_mqtt_client_init failed");
        return false;
    }
    esp_mqtt_client_register_event(s_client, ESP_EVENT_ANY_ID, mqtt_event_handler, NULL);
    ESP_LOGI(TAG, "client prepared (uri=%s, gateway=%s, plc=%s, boot=%s)", uri, s_gateway_id, s_plc_id, s_boot_id);
    return true;
}

void mqtt_gw_start(void)
{
    if (s_client) {
        esp_mqtt_client_start(s_client);
    }
}

bool mqtt_gw_is_online(void)
{
    return s_online;
}

int mqtt_gw_publish_telemetry(const char *json, size_t len)
{
    if (!s_client || !s_online) {
        return -1;
    }
    return esp_mqtt_client_publish(s_client, s_topic_telemetry, json, (int)len, MQTT_QOS, 0);
}

void mqtt_gw_publish_status(bool online)
{
    if (!s_client) {
        return;
    }
    char ts[24];
    iso8601_now(ts, sizeof(ts));
    char buf[192];
    int n = snprintf(buf, sizeof(buf),
                     "{\"schema\":\"v1\",\"gateway_id\":\"%s\",\"status\":\"%s\",\"ts\":\"%s\"}",
                     s_gateway_id, online ? "online" : "offline", ts);
    esp_mqtt_client_publish(s_client, s_topic_status, buf, n, MQTT_QOS, 1);
}

void mqtt_gw_publish_ack(const char *cmd_id, bool ok, const char *error)
{
    if (!s_client) {
        return;
    }
    char ts[24];
    iso8601_now(ts, sizeof(ts));
    char buf[256];
    int n;
    if (error) {
        n = snprintf(buf, sizeof(buf),
                     "{\"schema\":\"v1\",\"cmd_id\":\"%s\",\"ok\":%s,\"error\":\"%s\",\"ts\":\"%s\"}",
                     cmd_id, ok ? "true" : "false", error, ts);
    } else {
        n = snprintf(buf, sizeof(buf),
                     "{\"schema\":\"v1\",\"cmd_id\":\"%s\",\"ok\":%s,\"error\":null,\"ts\":\"%s\"}",
                     cmd_id, ok ? "true" : "false", ts);
    }
    esp_mqtt_client_publish(s_client, s_topic_ack, buf, n, MQTT_QOS, 1);
}

void mqtt_gw_publish_telemetry_ack(uint32_t seq, const char *status)
{
    if (!s_client) {
        return;
    }
    char buf[192];
    int n = snprintf(buf, sizeof(buf),
                     "{\"schema\":\"v1\",\"gateway_id\":\"%s\",\"boot_id\":\"%s\",\"seq\":%u,\"status\":\"%s\"}",
                     s_gateway_id, s_boot_id, (unsigned)seq, status);
    esp_mqtt_client_publish(s_client, s_topic_tack, buf, n, MQTT_QOS, 0);
}

void mqtt_gw_set_cmd_handler(mqtt_cmd_handler_t handler)
{
    s_cmd_cb = handler;
}

void mqtt_gw_set_online_handler(mqtt_online_handler_t handler)
{
    s_online_cb = handler;
}

void mqtt_gw_set_published_handler(mqtt_published_handler_t handler)
{
    s_pub_cb = handler;
}

void mqtt_gw_set_tack_handler(mqtt_tack_handler_t handler)
{
    s_tack_cb = handler;
}
const char *mqtt_gw_gateway_id(void) { return s_gateway_id; }
const char *mqtt_gw_plc_id(void) { return s_plc_id; }
const char *mqtt_gw_boot_id(void) { return s_boot_id; }
