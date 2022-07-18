/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#ifndef ED_ROM_SAMPLE_H
#define ED_ROM_SAMPLE_H

#ifdef __cplusplus
extern "C" {
#endif

#include "bios.h"
#include "disk.h"
#include "ff.h"
#include "sys.h"


void rom_boot_simulator(u8 cic);
u8 fmanager_display();
void usb_terminal();
void usb_load_rom();
u8 fm_file_read();
u8 fm_file_write();

#ifdef __cplusplus
}
#endif

#endif /* ED_ROM_SAMPLE_H */
