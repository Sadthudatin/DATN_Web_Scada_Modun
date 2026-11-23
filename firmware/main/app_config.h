#pragma once

#include "driver/uart.h"
#include "driver/gpio.h"
#include "sdkconfig.h"

#define GW_FW_VERSION "1.0.0"

/* Modbus RTU master on UART2 (RS-485 half-duplex) */
#define MB_UART_NUM          UART_NUM_2
#define MB_TX_GPIO           17
#define MB_RX_GPIO           18
#define MB_RTS_GPIO          16
#define MB_BAUD              38400
#define MB_DATA_BITS         UART_DATA_8_BITS
#define MB_PARITY            UART_PARITY_EVEN
#define MB_STOP_BITS         UART_STOP_BITS_1
#define MB_SLAVE_ADDR        1
#define MB_POLL_PERIOD_MS    300
#define MB_TIMEOUT_MS        300
#define MB_INTERBYTE_TIMEOUT_MS 20
#define MB_RETRIES           1
#define MB_MAX_FAILS         3
#define MB_MAX_REGS          125
#define MB_MAX_BITS          2000

/* Programming channel on UART1 (RS-232, raw byte pipe, no framing) */
#define PROG_UART_NUM        UART_NUM_1
#define PROG_TX_GPIO         4
#define PROG_RX_GPIO         5
#define PROG_BAUD            9600
#define PROG_DATA_BITS       UART_DATA_7_BITS
#define PROG_PARITY          UART_PARITY_EVEN
#define PROG_STOP_BITS       UART_STOP_BITS_1
#define PROG_TCP_PORT        8899
#define PROG_IDLE_TIMEOUT_MS (10 * 60 * 1000)

/* Store-and-forward journal */
#define SF_SLOT_SIZE         512
#define SF_SLOT_COUNT        4096
#define SF_MOUNT_POINT       "/sffs"
#define SF_FILE_NAME         "/sffs/journal.bin"

/* MQTT */
#define MQTT_QOS             1
#define MQTT_KEEPALIVE_S     30
#define MQTT_RECONNECT_S     5

/* COV */
#define COV_FULL_SNAPSHOT_MS 60000
#define COV_REPLAY_BUDGET    32

/* Task placement (core, priority) */
#define TASK_MODBUS_CORE     1
#define TASK_MODBUS_PRIO     3
#define TASK_FLASH_CORE      1
#define TASK_FLASH_PRIO      4
#define TASK_USB_CORE        0
#define TASK_USB_PRIO        2
#define TASK_NET_CORE        0
#define TASK_NET_PRIO        2
#define TASK_PIPE_CORE       0
#define TASK_PIPE_PRIO       1
