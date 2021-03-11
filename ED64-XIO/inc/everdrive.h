/* 
 * File:   everdrive.h
 * Author: igor
 *
 * Created on September 11, 2020, 6:59 PM
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

void boot_simulator(u8 cic);
u8 fmanager();
void usbTerminal();
void usbLoadGame();
u8 fileRead();
u8 fileWrite();

#ifdef __cplusplus
}
#endif

#endif	/* EVERDRIVE_H */
