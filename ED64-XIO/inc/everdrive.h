/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#ifndef EVERDRIVE_H
#define	EVERDRIVE_H

#ifdef __cplusplus
extern "C" {
#endif


#include "sys.h"
#include "bios.h"
#include "disk.h"
#include "ff.h"

void rom_boot_simulator(u8 cic);
u8 fmanager_display();
void usb_terminal();
void usb_load_rom();
u8 fileRead();
u8 fileWrite();

#ifdef __cplusplus
}
#endif

#endif	/* EVERDRIVE_H */
