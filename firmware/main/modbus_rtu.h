#pragma once

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#define MB_MAX_REGS_PER_BLOCK 125
#define MB_MAX_BITS_PER_BLOCK 2000

typedef struct {
    bool ok;
    uint16_t regs[MB_MAX_REGS_PER_BLOCK];
    uint8_t bits[MB_MAX_BITS_PER_BLOCK / 8 + 1]; /* packed, LSB first */
} modbus_read_result_t;

static inline bool modbus_result_bit(const modbus_read_result_t *r, uint16_t i)
{
    return ((r->bits[i / 8] >> (i % 8)) & 0x01u) != 0;
}

bool modbus_rtu_init(void);
bool modbus_read_block(uint8_t fc, uint16_t start, uint16_t count, modbus_read_result_t *out);
bool modbus_write_coil(uint16_t addr, bool value);
bool modbus_write_reg(uint16_t addr, uint16_t value);
bool modbus_rtu_healthy(void);
uint32_t modbus_fail_count(void);
