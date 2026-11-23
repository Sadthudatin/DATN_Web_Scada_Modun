#include "tag_map.h"

#include <string.h>

/* Delta DVP protocol (0-based) addresses, verified against the DVP-ES2 manual. */
#define BASE_S 0x0000u
#define BASE_X 0x0400u
#define BASE_Y 0x0500u
#define BASE_T 0x0600u
#define BASE_M 0x0800u
#define BASE_C 0x0E00u
#define BASE_D 0x1000u

#define B(name_, area_, idx_, wr_) \
    { name_, area_, idx_, TAG_KIND_BIT, 1.0f, 0.0f, wr_ }
#define R(name_, area_, idx_, sc_, db_, wr_) \
    { name_, area_, idx_, TAG_KIND_REG, sc_, db_, wr_ }

static const tag_def_t g_tags[] = {
    B("X0", TAG_AREA_X, 0, false), B("X1", TAG_AREA_X, 1, false),
    B("X2", TAG_AREA_X, 2, false), B("X3", TAG_AREA_X, 3, false),
    B("X4", TAG_AREA_X, 4, false), B("X5", TAG_AREA_X, 5, false),
    B("X6", TAG_AREA_X, 6, false), B("X7", TAG_AREA_X, 7, false),

    B("Y0", TAG_AREA_Y, 0, true), B("Y1", TAG_AREA_Y, 1, true),
    B("Y2", TAG_AREA_Y, 2, true), B("Y3", TAG_AREA_Y, 3, true),
    B("Y4", TAG_AREA_Y, 4, true), B("Y5", TAG_AREA_Y, 5, true),
    B("Y6", TAG_AREA_Y, 6, true), B("Y7", TAG_AREA_Y, 7, true),

    B("M0", TAG_AREA_M, 0, true), B("M1", TAG_AREA_M, 1, true),
    B("M2", TAG_AREA_M, 2, true), B("M3", TAG_AREA_M, 3, true),
    B("M4", TAG_AREA_M, 4, true), B("M5", TAG_AREA_M, 5, true),
    B("M6", TAG_AREA_M, 6, true), B("M7", TAG_AREA_M, 7, true),

    B("S0", TAG_AREA_S, 0, true), B("S1", TAG_AREA_S, 1, true),
    B("S2", TAG_AREA_S, 2, true), B("S3", TAG_AREA_S, 3, true),
    B("S4", TAG_AREA_S, 4, true), B("S5", TAG_AREA_S, 5, true),
    B("S6", TAG_AREA_S, 6, true), B("S7", TAG_AREA_S, 7, true),

    R("T0", TAG_AREA_T, 0, 1.0f, 0.5f, false),
    R("T1", TAG_AREA_T, 1, 1.0f, 0.5f, false),
    R("T2", TAG_AREA_T, 2, 1.0f, 0.5f, false),
    R("T3", TAG_AREA_T, 3, 1.0f, 0.5f, false),

    R("C0", TAG_AREA_C, 0, 1.0f, 0.5f, false),
    R("C1", TAG_AREA_C, 1, 1.0f, 0.5f, false),
    R("C2", TAG_AREA_C, 2, 1.0f, 0.5f, false),
    R("C3", TAG_AREA_C, 3, 1.0f, 0.5f, false),

    R("D0", TAG_AREA_D, 0, 0.1f, 0.05f, true),
    R("D1", TAG_AREA_D, 1, 0.1f, 0.05f, true),
    R("D2", TAG_AREA_D, 2, 0.1f, 0.05f, true),
    R("D3", TAG_AREA_D, 3, 0.1f, 0.05f, true),
    R("D4", TAG_AREA_D, 4, 0.1f, 0.05f, true),
    R("D5", TAG_AREA_D, 5, 0.1f, 0.05f, true),
    R("D6", TAG_AREA_D, 6, 0.1f, 0.05f, true),
    R("D7", TAG_AREA_D, 7, 0.1f, 0.05f, true),
    R("D8", TAG_AREA_D, 8, 0.1f, 0.05f, true),
    R("D9", TAG_AREA_D, 9, 0.1f, 0.05f, true),
};

static const poll_block_t g_blocks[] = {
    {0x02, TAG_AREA_X, 0, 8},
    {0x01, TAG_AREA_Y, 0, 8},
    {0x01, TAG_AREA_M, 0, 8},
    {0x01, TAG_AREA_S, 0, 8},
    {0x03, TAG_AREA_T, 0, 4},
    {0x03, TAG_AREA_C, 0, 4},
    {0x03, TAG_AREA_D, 0, 10},
};

const tag_def_t *tag_map_tags(size_t *count)
{
    if (count) {
        *count = sizeof(g_tags) / sizeof(g_tags[0]);
    }
    return g_tags;
}

const poll_block_t *tag_map_blocks(size_t *count)
{
    if (count) {
        *count = sizeof(g_blocks) / sizeof(g_blocks[0]);
    }
    return g_blocks;
}

const tag_def_t *tag_map_find(const char *name)
{
    if (!name) {
        return NULL;
    }
    size_t n = sizeof(g_tags) / sizeof(g_tags[0]);
    for (size_t i = 0; i < n; i++) {
        if (strcmp(g_tags[i].name, name) == 0) {
            return &g_tags[i];
        }
    }
    return NULL;
}

int tag_map_index(const char *name)
{
    if (!name) {
        return -1;
    }
    size_t n = sizeof(g_tags) / sizeof(g_tags[0]);
    for (size_t i = 0; i < n; i++) {
        if (strcmp(g_tags[i].name, name) == 0) {
            return (int)i;
        }
    }
    return -1;
}

uint16_t tag_map_modbus_addr(tag_area_t area, uint16_t index)
{
    switch (area) {
    case TAG_AREA_S: return (uint16_t)(BASE_S + index);
    case TAG_AREA_X: return (uint16_t)(BASE_X + index);
    case TAG_AREA_Y: return (uint16_t)(BASE_Y + index);
    case TAG_AREA_T: return (uint16_t)(BASE_T + index);
    case TAG_AREA_M: return (uint16_t)(BASE_M + index);
    case TAG_AREA_C: return (uint16_t)(BASE_C + index);
    case TAG_AREA_D: return (uint16_t)(BASE_D + index);
    }
    return 0;
}

uint8_t tag_map_read_fc(tag_area_t area)
{
    switch (area) {
    case TAG_AREA_X: return 0x02;
    case TAG_AREA_T:
    case TAG_AREA_C:
    case TAG_AREA_D: return 0x03;
    case TAG_AREA_S:
    case TAG_AREA_Y:
    case TAG_AREA_M:
    default: return 0x01;
    }
}

uint8_t tag_map_write_fc(tag_area_t area)
{
    switch (area) {
    case TAG_AREA_S:
    case TAG_AREA_Y:
    case TAG_AREA_M: return 0x05;
    case TAG_AREA_D: return 0x06;
    default: return 0x00;
    }
}

bool tag_map_writable(tag_area_t area)
{
    return tag_map_write_fc(area) != 0x00;
}
