#pragma once

#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

typedef void (*mqtt_cmd_handler_t)(const char *cmd_id, const char *target, const char *value_json, int ttl_ms);
typedef void (*mqtt_online_handler_t)(void);
typedef void (*mqtt_published_handler_t)(int msg_id);
typedef void (*mqtt_tack_handler_t)(uint32_t seq, const char *status);

bool mqtt_gw_init(const char *uri, const char *username, const char *password);
void mqtt_gw_start(void);
bool mqtt_gw_is_online(void);

const char *mqtt_gw_gateway_id(void);
const char *mqtt_gw_plc_id(void);
const char *mqtt_gw_boot_id(void);

int mqtt_gw_publish_telemetry(const char *json, size_t len);
void mqtt_gw_publish_status(bool online);
void mqtt_gw_publish_ack(const char *cmd_id, bool ok, const char *error);
void mqtt_gw_publish_telemetry_ack(uint32_t seq, const char *status);

void mqtt_gw_set_cmd_handler(mqtt_cmd_handler_t handler);
void mqtt_gw_set_online_handler(mqtt_online_handler_t handler);
void mqtt_gw_set_published_handler(mqtt_published_handler_t handler);
void mqtt_gw_set_tack_handler(mqtt_tack_handler_t handler);
