/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#ifndef ED64_BIOS_H
#define ED64_BIOS_H

#ifdef __cplusplus
extern "C" {
#endif

#include "sys.h"

#define ED64_SIZE_ROM 0x4000000 /*rom size */
#define ED64_SIZE_BRM 0x20000   // backup ram size */

#define ED64_ADDR_ROM (KSEG1 | 0x10000000)
#define ED64_ADDR_BRM (KSEG1 | 0x08000000)

#define ED64_ERR_I2C_CMD 0xB0
#define ED64_ERR_I2C_TOUT 0xB1
#define ED64_ERR_USB_TOUT 0xB2
#define ED64_ERR_FPG_CFG 0xB3

/* 
SD card controller speed select.
LOW speed is only for initilization procedure.
HIGH speed should be used for all other functions.
*/
#define ED64_SDIO_SPEED_LOW  0x00
#define ED64_SDIO_SPEED_HIGH 0x01

/* bootloader flags */
#define ED64_BCFG_BOOTMOD 0x01
#define ED64_BCFG_SD_INIT 0x02
#define ED64_BCFG_SD_TYPE 0x04
#define ED64_BCFG_GAMEMOD 0x08
#define ED64_BCFG_CICLOCK 0x8000

/* 64dd ROM table to know which data areas should be saved */
#define ED64_64DD_TBL_SIZE 2048
#define ED64_64DD_PGE_SIZE 0x8000

#define ED64_CART_ID_V2 0xED640007
#define ED64_CART_ID_V3 0xED640008
#define ED64_CART_ID_X7 0xED640013
#define ED64_CART_ID_X5 0xED640014

/* rom cfg register flags */
#define ED64_SAVE_OFF 0x0000
#define ED64_SAVE_EEP4K 0x0001
#define ED64_SAVE_EEP16K 0x0002
#define ED64_SAVE_SRM32K 0x0003
#define ED64_SAVE_SRM96K 0x0004
#define ED64_SAVE_FLASH 0x0005
#define ED64_SAVE_SRM128K 0x0006
#define ED64_SAVE_DD64 0x0010

void ed64_bios_init();
u8 ed64_bios_usb_can_read();
u8 ed64_bios_usb_can_write();
u8 ed64_bios_usb_read(void *dst, u32 len);
u8 ed64_bios_usb_write(void *src, u32 len);
void ed64_bios_usb_read_start();
u8 ed64_bios_usb_read_end(void *dst);

void ed64_bios_sdio_speed(u8 speed);
void ed64_bios_sdio_bitlength(u8 val);
u8 ed64_bios_sdio_cmd_read();
void ed64_bios_sdio_cmd_write(u8 val);
u8 ed64_bios_sd_data_read();
void ed64_bios_sdio_data_write(u8 val);
u8 ed64_bios_sdio_to_ram(void *dst, u16 slen);
u8 ed64_bios_sdio_to_rom(u32 dst, u16 slen);
u8 ed64_bios_ram_to_sdio(void *src, u16 slen);

void ed64_bios_rom_savetype_set(u8 type); /* set save type */
void ed64_bios_write_endian_swap(u8 swap_on);
u32 ed64_bios_get_cart_id();

#ifdef __cplusplus
}
#endif

#endif /* ED64_BIOS_H */
