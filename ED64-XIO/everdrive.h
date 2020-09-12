/* 
 * File:   everdrive.h
 * Author: igor
 *
 * Created on September 11, 2020, 6:59 PM
 */

#ifndef EVERDRIVE_H
#define	EVERDRIVE_H

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

#endif	/* EVERDRIVE_H */

