#include "modbus_rtu.h"

#include "app_config.h"
#include "driver/uart.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "esp_log.h"

#include <string.h>

static const char *TAG = "modbus";

static SemaphoreHandle_t s_mutex;
static volatile bool s_healthy;
static volatile uint32_t s_fail_count;

static uint16_t crc16(const uint8_t *buf, size_t len)
{
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < len; i++) {
        crc ^= buf[i];
        for (int b = 0; b < 8; b++) {
            if (crc & 1) {
                crc = (uint16_t)((crc >> 1) ^ 0xA001);
            } else {
                crc >>= 1;
            }
        }
    }
    return crc;
}

static bool transact(const uint8_t *req, size_t req_len, size_t expect_len,
                     uint8_t *resp, size_t resp_cap, size_t *resp_len)
{
    if (xSemaphoreTake(s_mutex, pdMS_TO_TICKS(MB_TIMEOUT_MS)) != pdTRUE) {
        return false;
    }

    uart_flush_input(MB_UART_NUM);
    uart_write_bytes(MB_UART_NUM, (const char *)req, req_len);
    uart_wait_tx_done(MB_UART_NUM, pdMS_TO_TICKS(MB_TIMEOUT_MS));

    if (expect_len > resp_cap) {
        expect_len = resp_cap;
    }
    size_t got = 0;
    while (got < expect_len) {
        int n = uart_read_bytes(MB_UART_NUM, resp + got, (uint32_t)(expect_len - got),
                                pdMS_TO_TICKS(MB_INTERBYTE_TIMEOUT_MS));
        if (n <= 0) {
            break;
        }
        got += (size_t)n;
        if (got >= 5 && (resp[1] & 0x80)) {
            break;
        }
    }
    xSemaphoreGive(s_mutex);

    if (got < 5) {
        return false;
    }

    uint16_t rx_crc = (uint16_t)resp[got - 2] | ((uint16_t)resp[got - 1] << 8);
    if (crc16(resp, got - 2) != rx_crc) {
        ESP_LOGW(TAG, "CRC mismatch (got 0x%04X, %u bytes)", rx_crc, (unsigned)got);
        return false;
    }
    if (resp[1] & 0x80) {
        ESP_LOGW(TAG, "Modbus exception code %d", resp[2]);
        return false;
    }

    *resp_len = got;
    return true;
}

static void req_read(uint8_t *req, uint8_t fc, uint16_t start, uint16_t count)
{
    req[0] = MB_SLAVE_ADDR;
    req[1] = fc;
    req[2] = (uint8_t)(start >> 8);
    req[3] = (uint8_t)(start & 0xFF);
    req[4] = (uint8_t)(count >> 8);
    req[5] = (uint8_t)(count & 0xFF);
    uint16_t crc = crc16(req, 6);
    req[6] = (uint8_t)(crc & 0xFF);
    req[7] = (uint8_t)(crc >> 8);
}

static bool read_with_retry(const uint8_t *req, size_t req_len, size_t expect_len,
                            uint8_t *resp, size_t resp_cap, size_t *resp_len)
{
    for (int attempt = 0; attempt <= MB_RETRIES; attempt++) {
        if (transact(req, req_len, expect_len, resp, resp_cap, resp_len)) {
            return true;
        }
    }
    s_fail_count++;
    s_healthy = false;
    ESP_LOGE(TAG, "read failed after %d retries (fc=0x%02X)", MB_RETRIES + 1, req[1]);
    return false;
}

bool modbus_rtu_init(void)
{
    s_mutex = xSemaphoreCreateMutex();
    if (!s_mutex) {
        return false;
    }

    uart_config_t cfg = {
        .baud_rate = MB_BAUD,
        .data_bits = MB_DATA_BITS,
        .parity = MB_PARITY,
        .stop_bits = MB_STOP_BITS,
        .flow_ctrl = UART_HW_FLOWCTRL_DISABLE,
        .rx_flow_ctrl_thresh = 122,
        .source_clk = UART_SCLK_DEFAULT,
    };

    if (uart_driver_install(MB_UART_NUM, 2048, 2048, 0, NULL, 0) != ESP_OK) {
        return false;
    }
    uart_param_config(MB_UART_NUM, &cfg);
    uart_set_pin(MB_UART_NUM, MB_TX_GPIO, MB_RX_GPIO, MB_RTS_GPIO, UART_PIN_NO_CHANGE);
    uart_set_mode(MB_UART_NUM, UART_MODE_RS485_HALF_DUPLEX);

    s_healthy = false;
    s_fail_count = 0;
    ESP_LOGI(TAG, "UART%d RS-485 ready @ %d 8E1 (TX=%d RX=%d RTS=%d)",
             MB_UART_NUM, MB_BAUD, MB_TX_GPIO, MB_RX_GPIO, MB_RTS_GPIO);
    return true;
}

bool modbus_read_block(uint8_t fc, uint16_t start, uint16_t count, modbus_read_result_t *out)
{
    if (!out || count == 0) {
        return false;
    }

    size_t expect_len;
    if (fc == 0x03) {
        if (count > MB_MAX_REGS_PER_BLOCK) {
            return false;
        }
        expect_len = 5u + 2u * count;
    } else if (fc == 0x01 || fc == 0x02) {
        if (count > MB_MAX_BITS_PER_BLOCK) {
            return false;
        }
        expect_len = 5u + (count + 7u) / 8u;
    } else {
        return false;
    }

    uint8_t req[8];
    req_read(req, fc, start, count);

    uint8_t resp[512];
    size_t rlen = 0;
    if (!read_with_retry(req, 8, expect_len, resp, sizeof(resp), &rlen)) {
        return false;
    }

    uint8_t byte_count = resp[2];
    if (fc == 0x03) {
        if (byte_count != count * 2 || (size_t)byte_count + 5 > rlen) {
            return false;
        }
        for (uint16_t i = 0; i < count; i++) {
            out->regs[i] = (uint16_t)((resp[3 + 2 * i] << 8) | resp[4 + 2 * i]);
        }
    } else {
        uint8_t expect = (uint8_t)((count + 7) / 8);
        if (byte_count != expect || (size_t)byte_count + 5 > rlen) {
            return false;
        }
        memset(out->bits, 0, sizeof(out->bits));
        for (uint16_t b = 0; b < count; b++) {
            if ((resp[3 + b / 8] >> (b % 8)) & 0x01) {
                out->bits[b / 8] |= (uint8_t)(1u << (b % 8));
            }
        }
    }

    s_healthy = true;
    out->ok = true;
    return true;
}

static bool write_simple(uint8_t fc, uint16_t addr, uint16_t value)
{
    uint8_t req[8];
    req[0] = MB_SLAVE_ADDR;
    req[1] = fc;
    req[2] = (uint8_t)(addr >> 8);
    req[3] = (uint8_t)(addr & 0xFF);
    req[4] = (uint8_t)(value >> 8);
    req[5] = (uint8_t)(value & 0xFF);
    uint16_t crc = crc16(req, 6);
    req[6] = (uint8_t)(crc & 0xFF);
    req[7] = (uint8_t)(crc >> 8);

    uint8_t resp[16];
    size_t rlen = 0;
    if (!read_with_retry(req, 8, 8, resp, sizeof(resp), &rlen)) {
        return false;
    }
    return (rlen >= 8 && memcmp(resp, req, 6) == 0);
}

bool modbus_write_coil(uint16_t addr, bool value)
{
    return write_simple(0x05, addr, value ? 0xFF00 : 0x0000);
}

bool modbus_write_reg(uint16_t addr, uint16_t value)
{
    return write_simple(0x06, addr, value);
}

bool modbus_rtu_healthy(void)
{
    return s_healthy;
}

uint32_t modbus_fail_count(void)
{
    return s_fail_count;
}
