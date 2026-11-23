#pragma once

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

typedef enum {
    TAG_KIND_BIT = 0,
    TAG_KIND_REG = 1,
} tag_kind_t;

typedef enum {
    TAG_AREA_S = 0,
    TAG_AREA_X,
    TAG_AREA_Y,
    TAG_AREA_M,
    TAG_AREA_T,
    TAG_AREA_C,
    TAG_AREA_D,
} tag_area_t;

typedef struct {
    const char *name;
    tag_area_t area;
    uint16_t index;
    tag_kind_t kind;
    float scale;
    float deadband;
    bool writable;
} tag_def_t;

typedef struct {
    uint8_t fc;
    tag_area_t area;
    uint16_t start_index;
    uint16_t count;
} poll_block_t;

const tag_def_t *tag_map_tags(size_t *count);
const poll_block_t *tag_map_blocks(size_t *count);
const tag_def_t *tag_map_find(const char *name);
int tag_map_index(const char *name);
uint16_t tag_map_modbus_addr(tag_area_t area, uint16_t index);
uint8_t tag_map_read_fc(tag_area_t area);
uint8_t tag_map_write_fc(tag_area_t area);
bool tag_map_writable(tag_area_t area);
