#pragma once

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>
#include "tag_map.h"

typedef enum {
    QUALITY_GOOD = 0,
    QUALITY_BAD,
    QUALITY_STALE,
    QUALITY_UNRELIABLE,
} quality_t;

const char *quality_str(quality_t q);

typedef struct {
    double last_value;
    quality_t last_quality;
    bool has_last;
} cov_state_t;

void cov_engine_init(const tag_def_t *tags, size_t count);
void cov_engine_force_full(void);
bool cov_engine_update(size_t tag_index, double value, quality_t quality);
