#pragma once

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#define SF_MAX_PAYLOAD 480

typedef struct {
    uint32_t seq;
    uint16_t len;
    uint8_t payload[SF_MAX_PAYLOAD];
} sf_record_t;

bool sf_init(void);
uint32_t sf_next_seq(void);
bool sf_append(uint32_t seq, const void *payload, size_t len);
bool sf_peek_oldest(sf_record_t *out);
bool sf_ack(uint32_t seq);
size_t sf_pending_count(void);
uint32_t sf_recovered_percent(void);
