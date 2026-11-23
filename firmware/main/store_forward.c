#include "store_forward.h"

#include "app_config.h"
#include "esp_log.h"
#include "esp_rom_crc.h"
#include "esp_vfs_fat.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static const char *TAG = "sf";

#define SF_MAGIC 0x314C4653u
#define SF_PAYLOAD_SIZE (SF_SLOT_SIZE - 16)

typedef struct __attribute__((packed)) {
    uint32_t magic;
    uint32_t seq;
    uint16_t len;
    uint16_t flags;
    uint32_t crc;
    uint8_t payload[SF_PAYLOAD_SIZE];
} sf_slot_t;

_Static_assert(sizeof(sf_slot_t) == SF_SLOT_SIZE, "sf_slot_t must fill one slot");

static SemaphoreHandle_t s_mutex;
static wl_handle_t s_wl = WL_INVALID_HANDLE;
static FILE *s_file;
static uint32_t s_next_seq = 1;
static uint32_t s_acked;
static uint32_t s_pending_seq[SF_SLOT_COUNT];
static size_t s_pending_n;

static bool slot_valid(const sf_slot_t *slot, uint32_t *seq_out, uint16_t *len_out)
{
    if (slot->magic != SF_MAGIC || slot->len > SF_PAYLOAD_SIZE) {
        return false;
    }
    uint32_t crc = esp_crc32_le(0, slot->payload, slot->len);
    if (crc != slot->crc) {
        return false;
    }
    if (seq_out) *seq_out = slot->seq;
    if (len_out) *len_out = slot->len;
    return true;
}

static bool read_slot(uint32_t seq, sf_slot_t *slot)
{
    off_t off = (off_t)((seq - 1) % SF_SLOT_COUNT) * SF_SLOT_SIZE;
    if (fseek(s_file, off, SEEK_SET) != 0) {
        return false;
    }
    return fread(slot, 1, SF_SLOT_SIZE, s_file) == SF_SLOT_SIZE;
}

static bool write_slot(uint32_t seq, const sf_slot_t *slot)
{
    off_t off = (off_t)((seq - 1) % SF_SLOT_COUNT) * SF_SLOT_SIZE;
    if (fseek(s_file, off, SEEK_SET) != 0) {
        return false;
    }
    if (fwrite(slot, 1, SF_SLOT_SIZE, s_file) != SF_SLOT_SIZE) {
        return false;
    }
    return fflush(s_file) == 0;
}

static int cmp_u32(const void *a, const void *b)
{
    uint32_t va = *(const uint32_t *)a;
    uint32_t vb = *(const uint32_t *)b;
    if (va < vb) return -1;
    if (va > vb) return 1;
    return 0;
}

bool sf_init(void)
{
    s_mutex = xSemaphoreCreateMutex();
    if (!s_mutex) {
        return false;
    }

    esp_vfs_fat_mount_config_t mount = {
        .format_if_mount_failed = true,
        .max_files = 4,
        .allocation_unit_size = 4096,
    };
    esp_err_t err = esp_vfs_fat_spiflash_mount_rw_wl(SF_MOUNT_POINT, "storage", &mount, &s_wl);
    if (err != ESP_OK) {
        ESP_LOGE(TAG, "FAT mount failed: %s", esp_err_to_name(err));
        return false;
    }

    s_file = fopen(SF_FILE_NAME, "r+b");
    if (!s_file) {
        s_file = fopen(SF_FILE_NAME, "w+b");
        if (!s_file) {
            ESP_LOGE(TAG, "cannot create journal");
            return false;
        }
        sf_slot_t empty = {0};
        for (uint32_t i = 0; i < SF_SLOT_COUNT; i++) {
            fwrite(&empty, 1, SF_SLOT_SIZE, s_file);
        }
        fflush(s_file);
    }

    fseek(s_file, 0, SEEK_END);
    long size = ftell(s_file);
    if (size != (long)SF_SLOT_COUNT * SF_SLOT_SIZE) {
        ESP_LOGW(TAG, "journal size %ld != expected, reinitialising", size);
        fclose(s_file);
        s_file = fopen(SF_FILE_NAME, "w+b");
        if (!s_file) {
            return false;
        }
        sf_slot_t empty = {0};
        for (uint32_t i = 0; i < SF_SLOT_COUNT; i++) {
            fwrite(&empty, 1, SF_SLOT_SIZE, s_file);
        }
        fflush(s_file);
    }

    uint32_t max_seq = 0;
    s_pending_n = 0;
    for (uint32_t seq = 1; seq <= SF_SLOT_COUNT; seq++) {
        sf_slot_t slot;
        if (!read_slot(seq, &slot)) {
            continue;
        }
        uint32_t s;
        uint16_t l;
        if (slot_valid(&slot, &s, &l)) {
            s_pending_seq[s_pending_n++] = s;
            if (s > max_seq) {
                max_seq = s;
            }
        }
    }
    s_next_seq = max_seq + 1;
    if (s_pending_n > 1) {
        qsort(s_pending_seq, s_pending_n, sizeof(uint32_t), cmp_u32);
    }
    s_acked = 0;

    ESP_LOGI(TAG, "journal ready, %u pending record(s), next seq %u", (unsigned)s_pending_n, (unsigned)s_next_seq);
    return true;
}

bool sf_append(uint32_t seq, const void *payload, size_t len)
{
    if (!s_file || !payload || seq == 0 || len == 0 || len > SF_PAYLOAD_SIZE) {
        return false;
    }

    xSemaphoreTake(s_mutex, portMAX_DELAY);
    if (s_pending_n >= SF_SLOT_COUNT) {
        xSemaphoreGive(s_mutex);
        ESP_LOGE(TAG, "journal full (%u), drop seq %u", (unsigned)s_pending_n, (unsigned)seq);
        return false;
    }
    if (seq >= s_next_seq) {
        s_next_seq = seq + 1;
    }

    sf_slot_t slot = {0};
    slot.magic = SF_MAGIC;
    slot.seq = seq;
    slot.len = (uint16_t)len;
    slot.flags = 0;
    memcpy(slot.payload, payload, len);
    slot.crc = esp_crc32_le(0, slot.payload, len);

    bool ok = write_slot(seq, &slot);
    if (ok) {
        s_pending_seq[s_pending_n++] = seq;
    }
    xSemaphoreGive(s_mutex);

    if (!ok) {
        ESP_LOGE(TAG, "journal write failed");
    }
    return ok;
}

bool sf_peek_oldest(sf_record_t *out)
{
    if (!out) {
        return false;
    }

    xSemaphoreTake(s_mutex, portMAX_DELAY);
    bool found = false;
    while (!found && s_pending_n > 0) {
        sf_slot_t slot;
        if (read_slot(s_pending_seq[0], &slot)) {
            uint32_t s;
            uint16_t l;
            if (slot_valid(&slot, &s, &l)) {
                out->seq = s;
                out->len = l;
                memcpy(out->payload, slot.payload, l);
                found = true;
                break;
            }
        }
        memmove(&s_pending_seq[0], &s_pending_seq[1], (s_pending_n - 1) * sizeof(uint32_t));
        s_pending_n--;
    }
    xSemaphoreGive(s_mutex);
    return found;
}

bool sf_ack(uint32_t seq)
{
    if (seq == 0) {
        return false;
    }

    xSemaphoreTake(s_mutex, portMAX_DELAY);
    bool removed = false;
    for (size_t i = 0; i < s_pending_n; i++) {
        if (s_pending_seq[i] == seq) {
            memmove(&s_pending_seq[i], &s_pending_seq[i + 1], (s_pending_n - i - 1) * sizeof(uint32_t));
            s_pending_n--;
            removed = true;
            break;
        }
    }

    if (removed) {
        sf_slot_t slot = {0};
        write_slot(seq, &slot);
        s_acked++;
    }
    xSemaphoreGive(s_mutex);
    return removed;
}

size_t sf_pending_count(void)
{
    xSemaphoreTake(s_mutex, portMAX_DELAY);
    size_t n = s_pending_n;
    xSemaphoreGive(s_mutex);
    return n;
}

uint32_t sf_recovered_percent(void)
{
    xSemaphoreTake(s_mutex, portMAX_DELAY);
    uint32_t total = s_acked + (uint32_t)s_pending_n;
    uint32_t pct = (total == 0) ? 100u : (uint32_t)((100ull * s_acked) / total);
    xSemaphoreGive(s_mutex);
    return pct;
}
uint32_t sf_next_seq(void)
{
    xSemaphoreTake(s_mutex, portMAX_DELAY);
    uint32_t seq = s_next_seq++;
    xSemaphoreGive(s_mutex);
    return seq;
}
