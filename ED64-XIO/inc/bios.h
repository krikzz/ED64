/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#ifndef BIOS_H
#define BIOS_H

#ifdef __cplusplus
extern "C" {
#endif

#include "sys.h"

#define BI_SIZE_ROM 0x4000000 /*rom size */
#define BI_SIZE_BRM 0x20000   // backup ram size */

#define BI_ADDR_ROM (KSEG1 | 0x10000000)
#define BI_ADDR_BRM (KSEG1 | 0x08000000)

#define BI_ERR_I2C_CMD 0xB0
#define BI_ERR_I2C_TOUT 0xB1
#define BI_ERR_USB_TOUT 0xB2
#define BI_ERR_FPG_CFG 0xB3

/* sd controller speed select. LO speed only for init procedure */
#define BI_DISK_SPD_LO 0x00
#define BI_DISK_SPD_HI 0x01

/* bootloader flags */
#define BI_BCFG_BOOTMOD 0x01
#define BI_BCFG_SD_INIT 0x02
#define BI_BCFG_SD_TYPE 0x04
#define BI_BCFG_GAMEMOD 0x08
#define BI_BCFG_CICLOCK 0x8000

/* 64dd ROM table to know which data areas should be saved */
#define BI_DD_TBL_SIZE 2048
#define BI_DD_PGE_SIZE 0x8000

#define CART_ID_V2 0xED640007
#define CART_ID_V3 0xED640008
#define CART_ID_X7 0xED640013
#define CART_ID_X5 0xED640014

/* game cfg register flags */
#define SAVE_OFF 0x0000
#define SAVE_EEP4K 0x0001
#define SAVE_EEP16K 0x0002
#define SAVE_SRM32K 0x0003
#define SAVE_SRM96K 0x0004
#define SAVE_FLASH 0x0005
#define SAVE_SRM128K 0x0006
#define SAVE_DD64 0x0010

void bi_init();
u8 bi_usb_can_rd();
u8 bi_usb_can_wr();
u8 bi_usb_rd(void *dst, u32 len);
u8 bi_usb_wr(void *src, u32 len);
void bi_usb_rd_start();
u8 bi_usb_rd_end(void *dst);

void bi_sd_speed(u8 speed);
void bi_sd_bitlen(u8 val);
u8 bi_sd_cmd_rd();
void bi_sd_cmd_wr(u8 val);
u8 bi_sd_dat_rd();
void bi_sd_dat_wr(u8 val);
u8 bi_sd_to_ram(void *dst, u16 slen);
u8 bi_sd_to_rom(u32 dst, u16 slen);
u8 bi_ram_to_sd(void *src, u16 slen);

void bi_game_cfg_set(u8 type); /* set save type */
void bi_wr_swap(u8 swap_on);
u32 bi_get_cart_id();

#ifdef __cplusplus
}
#endif

#endif /* BIOS_H */
