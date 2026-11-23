#pragma once

#include <stdint.h>
#include <stdbool.h>
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/event_groups.h"
#include "quality_cov.h"

#define APP_MAX_TAGS 64

typedef struct {
    double value;
    quality_t quality;
} app_tag_value_t;

typedef struct {
    app_tag_value_t tags[APP_MAX_TAGS];
    uint32_t sample_id;
    bool modbus_healthy;
} app_snapshot_t;

extern app_snapshot_t g_snapshot;
extern SemaphoreHandle_t g_snapshot_mutex;
extern EventGroupHandle_t g_app_events;

#define APP_EVT_NEW_SAMPLE  BIT0
#define APP_EVT_MQTT_ONLINE BIT1
#define APP_EVT_IP_GOT      BIT2
