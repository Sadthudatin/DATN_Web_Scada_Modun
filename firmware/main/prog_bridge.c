#include "prog_bridge.h"

#include "app_config.h"
#include "driver/uart.h"
#include "esp_log.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include <lwip/sockets.h>
#include <netinet/tcp.h>
#include <string.h>

static const char *TAG = "prog_bridge";

static volatile bool s_enabled;
static int s_listen_sock = -1;

static bool uart_init_prog(void)
{
    uart_config_t cfg = {
        .baud_rate = PROG_BAUD,
        .data_bits = PROG_DATA_BITS,
        .parity = PROG_PARITY,
        .stop_bits = PROG_STOP_BITS,
        .flow_ctrl = UART_HW_FLOWCTRL_DISABLE,
        .rx_flow_ctrl_thresh = 122,
        .source_clk = UART_SCLK_DEFAULT,
    };
    if (uart_driver_install(PROG_UART_NUM, 2048, 2048, 0, NULL, 0) != ESP_OK) {
        return false;
    }
    uart_param_config(PROG_UART_NUM, &cfg);
    uart_set_pin(PROG_UART_NUM, PROG_TX_GPIO, PROG_RX_GPIO, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE);
    ESP_LOGI(TAG, "UART%d programming channel ready @ %d 7E1 (TX=%d RX=%d)",
             PROG_UART_NUM, PROG_BAUD, PROG_TX_GPIO, PROG_RX_GPIO);
    return true;
}

static void serve_client(int sock)
{
    ESP_LOGI(TAG, "programming session opened (TCP_NODELAY, idle timeout %d min)",
             PROG_IDLE_TIMEOUT_MS / 60000);
    uint8_t buf[512];
    TickType_t last_rx = xTaskGetTickCount();

    while (s_enabled) {
        fd_set rfds;
        FD_ZERO(&rfds);
        FD_SET(sock, &rfds);
        struct timeval tv = {0, 100000};
        int sel = select(sock + 1, &rfds, NULL, NULL, &tv);
        if (sel > 0 && FD_ISSET(sock, &rfds)) {
            int n = recv(sock, buf, sizeof(buf), 0);
            if (n <= 0) {
                break;
            }
            uart_write_bytes(PROG_UART_NUM, (const char *)buf, n);
            uart_wait_tx_done(PROG_UART_NUM, pdMS_TO_TICKS(1000));
            last_rx = xTaskGetTickCount();
        }

        int n = uart_read_bytes(PROG_UART_NUM, buf, sizeof(buf), pdMS_TO_TICKS(20));
        if (n > 0) {
            send(sock, buf, n, 0);
            last_rx = xTaskGetTickCount();
            for (int i = 0; i < n; i++) {
                if (buf[i] > 0x7F) {
                    ESP_LOGW(TAG, "byte 0x%02X > 0x7F on programming channel", buf[i]);
                }
            }
        }

        if ((xTaskGetTickCount() - last_rx) > pdMS_TO_TICKS(PROG_IDLE_TIMEOUT_MS)) {
            ESP_LOGW(TAG, "idle timeout, closing session");
            break;
        }
    }

    close(sock);
    ESP_LOGI(TAG, "programming session closed");
}

void prog_bridge_set_enabled(bool enabled)
{
    s_enabled = enabled;
}

bool prog_bridge_is_enabled(void)
{
    return s_enabled;
}

bool prog_bridge_init(void)
{
    return uart_init_prog();
}

void prog_bridge_task(void *arg)
{
    (void)arg;

    for (;;) {
        if (!s_enabled) {
            if (s_listen_sock >= 0) {
                close(s_listen_sock);
                s_listen_sock = -1;
                ESP_LOGI(TAG, "bridge disabled, listener closed");
            }
            vTaskDelay(pdMS_TO_TICKS(500));
            continue;
        }

        if (s_listen_sock < 0) {
            s_listen_sock = socket(AF_INET, SOCK_STREAM, 0);
            if (s_listen_sock < 0) {
                vTaskDelay(pdMS_TO_TICKS(1000));
                continue;
            }
            int opt = 1;
            setsockopt(s_listen_sock, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

            struct sockaddr_in addr = {0};
            addr.sin_family = AF_INET;
            addr.sin_addr.s_addr = htonl(INADDR_ANY);
            addr.sin_port = htons(PROG_TCP_PORT);

            if (bind(s_listen_sock, (struct sockaddr *)&addr, sizeof(addr)) < 0) {
                ESP_LOGE(TAG, "bind :%d failed", PROG_TCP_PORT);
                close(s_listen_sock);
                s_listen_sock = -1;
                vTaskDelay(pdMS_TO_TICKS(2000));
                continue;
            }
            listen(s_listen_sock, 1);
            ESP_LOGI(TAG, "listening on :%d", PROG_TCP_PORT);
        }

        fd_set ls;
        FD_ZERO(&ls);
        FD_SET(s_listen_sock, &ls);
        struct timeval tv = {0, 500000};
        int sel = select(s_listen_sock + 1, &ls, NULL, NULL, &tv);
        if (sel <= 0) {
            continue;
        }

        struct sockaddr_in caddr;
        socklen_t clen = sizeof(caddr);
        int csock = accept(s_listen_sock, (struct sockaddr *)&caddr, &clen);
        if (csock < 0) {
            continue;
        }
        if (!s_enabled) {
            close(csock);
            continue;
        }

        int nodelay = 1;
        setsockopt(csock, IPPROTO_TCP, TCP_NODELAY, &nodelay, sizeof(nodelay));
        ESP_LOGI(TAG, "client %s:%d connected", inet_ntoa(caddr.sin_addr), ntohs(caddr.sin_port));
        serve_client(csock);
    }
}
