#include "quality_cov.h"

#include <math.h>
#include <stdlib.h>

static cov_state_t *s_states;
static const tag_def_t *s_tags;
static size_t s_count;

const char *quality_str(quality_t q)
{
    switch (q) {
    case QUALITY_GOOD: return "good";
    case QUALITY_BAD: return "bad";
    case QUALITY_STALE: return "stale";
    case QUALITY_UNRELIABLE: return "unreliable";
    }
    return "bad";
}

void cov_engine_init(const tag_def_t *tags, size_t count)
{
    free(s_states);
    s_tags = tags;
    s_count = count;
    s_states = calloc(count, sizeof(cov_state_t));
}

void cov_engine_force_full(void)
{
    for (size_t i = 0; i < s_count; i++) {
        s_states[i].has_last = false;
    }
}

bool cov_engine_update(size_t tag_index, double value, quality_t quality)
{
    if (!s_states || tag_index >= s_count) {
        return false;
    }

    cov_state_t *state = &s_states[tag_index];
    const tag_def_t *tag = &s_tags[tag_index];
    bool changed = false;

    if (!state->has_last) {
        changed = true;
    } else if (state->last_quality != quality) {
        changed = true;
    } else if (tag->kind == TAG_KIND_BIT) {
        changed = ((value != 0.0) != (state->last_value != 0.0));
    } else {
        double deadband = (double)tag->deadband;
        if (deadband > 0.0) {
            changed = fabs(value - state->last_value) >= deadband;
        } else {
            changed = (value != state->last_value);
        }
    }

    state->last_value = value;
    state->last_quality = quality;
    state->has_last = true;
    return changed;
}
