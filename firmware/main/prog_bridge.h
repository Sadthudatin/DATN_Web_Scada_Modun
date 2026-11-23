#pragma once

#include <stdbool.h>

bool prog_bridge_init(void);
void prog_bridge_set_enabled(bool enabled);
bool prog_bridge_is_enabled(void);
void prog_bridge_task(void *arg);
