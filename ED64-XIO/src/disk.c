/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "everdrive.h"


#define CMD0  0x40    /* software reset */
#define CMD1  0x41    /* brings card out of idle state */
#define CMD8  0x48    /* Reserved */
#define CMD12 0x4C    /* stop transmission on multiple block read */
#define CMD17 0x51    /* read single block */
#define CMD18 0x52    /* read multiple block */
#define CMD58 0x7A    /* reads the OCR register */
#define CMD55 0x77
#define CMD41 0x69
#define CMD24 0x58    /* writes a single block */
#define CMD25 0x59    /* writes a multi block */
#define	ACMD41 0x69
#define	ACMD6 0x46
#define SD_V2 2
#define SD_HC 1

#define CMD2 0x42 /*read cid */
#define CMD3 0x43 /*read rca */
#define CMD7 0x47
#define CMD9 0x49
#define CMD6 0x46 /*set hi speed */

#define R1 1
#define R2 2
#define R3 3
#define R6 6
#define R7 7

#define DISK_CMD_TOUT 2048
#define DISK_MODE_NOP   0
#define DISK_MODE_RD    1
#define DISK_MODE_WR    2



u32 crc7(u8 *buff, u32 len);

u8 sd_disk_cmd(u8 cmd, u32 arg);
u8 sd_disk_read_resp(u8 cmd);
u8 sd_disk_open_read(u32 saddr);
u8 sd_disk_close_rw();

u8 sd_resp_buff[18];
u32 disk_cur_addr;
u8 disk_card_type;
u8 disk_mode;

/******************************************************************************
* sdcard disk base functions
*******************************************************************************/

/**
 * @brief Initializes the sdcard interface
 *
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_init() {

    u16 i;
    volatile u8 resp = 0;
    u32 rca;
    u32 wait_max = 1024;

    disk_card_type = 0;
    disk_mode = DISK_MODE_NOP;

    ed64_bios_sdio_speed(ED64_SDIO_SPEED_LOW);

    ed64_bios_sdio_bitlength(8);
    for (i = 0; i < 40; i++)ed64_bios_sdio_cmd_write(0xff);
    sd_disk_cmd(CMD0, 0x1aa);


    for (i = 0; i < 40; i++)ed64_bios_sdio_cmd_write(0xff);

    resp = sd_disk_cmd(CMD8, 0x1aa);


    if (resp != 0 && resp != DISK_ERR_CTO) {
        return DISK_ERR_INIT;
    }


    if (resp == 0)disk_card_type |= SD_V2;


    if (disk_card_type == SD_V2) {

        for (i = 0; i < wait_max; i++) {

            resp = sd_disk_cmd(CMD55, 0);
            if (resp)return DISK_ERR_INIT;
            if ((sd_resp_buff[3] & 1) != 1)continue;
            resp = sd_disk_cmd(CMD41, 0x40300000);
            if ((sd_resp_buff[1] & 128) == 0)continue;

            break;
        }
    } else {

        i = 0;
        do {
            resp = sd_disk_cmd(CMD55, 0);
            if (resp)return DISK_ERR_INIT;
            resp = sd_disk_cmd(CMD41, 0x40300000);
            if (resp)return DISK_ERR_INIT;

        } while (sd_resp_buff[1] < 1 && i++ < wait_max);

    }

    if (i == wait_max)return DISK_ERR_INIT;

    if ((sd_resp_buff[1] & 64) && disk_card_type != 0)disk_card_type |= SD_HC;



    resp = sd_disk_cmd(CMD2, 0);
    if (resp)return DISK_ERR_INIT;

    resp = sd_disk_cmd(CMD3, 0);
    if (resp)return DISK_ERR_INIT;

    resp = sd_disk_cmd(CMD7, 0);


    rca = (sd_resp_buff[1] << 24) | (sd_resp_buff[2] << 16) | (sd_resp_buff[3] << 8) | (sd_resp_buff[4] << 0);


    resp = sd_disk_cmd(CMD9, rca); /* get csd */
    if (resp)return DISK_ERR_INIT;


    resp = sd_disk_cmd(CMD7, rca);
    if (resp)return DISK_ERR_INIT;


    resp = sd_disk_cmd(CMD55, rca);
    if (resp)return DISK_ERR_INIT;


    resp = sd_disk_cmd(CMD6, 0x02);
    if (resp)return DISK_ERR_INIT;


    ed64_bios_sdio_speed(ED64_SDIO_SPEED_HIGH);

    return 0;
}

/**
 * @brief Sends an SDIO command
 *
 * @param[in]  cmd
 *             The command
 * @param[in]  arg
 *             The value to write
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_cmd(u8 cmd, u32 arg) {


    u8 p = 0;
    u8 buff[6];

    u8 crc;
    buff[p++] = cmd;
    buff[p++] = (arg >> 24);
    buff[p++] = (arg >> 16);
    buff[p++] = (arg >> 8);
    buff[p++] = (arg >> 0);
    crc = crc7(buff, 5) | 1;

    ed64_bios_sdio_bitlength(8);

    ed64_bios_sdio_cmd_write(0xff);
    ed64_bios_sdio_cmd_write(cmd);
    ed64_bios_sdio_cmd_write(arg >> 24);
    ed64_bios_sdio_cmd_write(arg >> 16);
    ed64_bios_sdio_cmd_write(arg >> 8);
    ed64_bios_sdio_cmd_write(arg);
    ed64_bios_sdio_cmd_write(crc);


    if (cmd == CMD18)return 0;

    return sd_disk_read_resp(cmd);
}


/**
 * @brief Reads the SD card response
 *
 * @param[in]  cmd
 *             The command to read
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_read_resp(u8 cmd) {

    u16 i;

    u8 resp_len = cmd == CMD2 || cmd == CMD9 ? 17 : 6;

    i = 0;
    sd_resp_buff[0] = ed64_bios_sdio_cmd_read();
    ed64_bios_sdio_bitlength(1);


    while ((sd_resp_buff[0] & 0xC0) != 0) { /* wait for resp begin. first two bits should be zeros */
        sd_resp_buff[0] = ed64_bios_sdio_cmd_read();

        if (i++ == DISK_CMD_TOUT)return DISK_ERR_CTO;
    }

    ed64_bios_sdio_bitlength(8);

    for (i = 1; i < resp_len; i++) {

        sd_resp_buff[i] = ed64_bios_sdio_cmd_read(); //8
    }

    return 0;
}


u32 crc7(u8 *buff, u32 len) {

    u32 a, crc = 0;

    while (len--) {
        crc ^= *buff++;
        a = 8;
        do {
            crc <<= 1;
            if (crc & (1 << 8)) crc ^= 0x12;
        } while (--a);
    }
    return (crc & 0xfe);
}

/******************************************************************************
* sdcard read functions
*******************************************************************************/

/**
 * @brief Reads the SD card
 *
 * @param[in]  saddr
 *             The start memory address
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_open_read(u32 saddr) {

    u8 resp;
    if (disk_mode == DISK_MODE_RD && saddr == disk_cur_addr)return 0;

    sd_disk_close_rw();
    disk_cur_addr = saddr;
    if ((disk_card_type & SD_HC) == 0)saddr *= 512;
    resp = sd_disk_cmd(CMD18, saddr);
    if (resp)return resp;

    disk_mode = DISK_MODE_RD;

    return 0;
}

/**
 * @brief Reads the SD card to RAM
 *
 * @param[in]  dst
 *             destination pointer to copy to
 * 
 * @param[in]  saddr
 *             The start memory address
 * 
 * @param[in]  slen
 *             Length in bytes to copy
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_read_to_ram(u32 sd_addr, void *dst, u16 slen) {

    u8 resp = 0;

    resp = sd_disk_open_read(sd_addr);
    if (resp)return DISK_ERR_RD1;
    disk_cur_addr += slen;

    resp = ed64_bios_sdio_to_ram(dst, slen);
    if (resp)return DISK_ERR_RD2;

    return 0;
}

/**
 * @brief Reads the SD card to ROM
 *
 * @param[in]  dst
 *             destination pointer to copy to
 * 
 * @param[in]  saddr
 *             The start memory address
 * 
 * @param[in]  slen
 *             Length in bytes to copy
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_read_to_rom(u32 sd_addr, u32 dst, u16 slen) {

    u8 resp = 0;

    resp = sd_disk_open_read(sd_addr);
    if (resp)return DISK_ERR_RD1;
    disk_cur_addr += slen;

    resp = ed64_bios_sdio_to_rom(dst, slen);
    if (resp)return DISK_ERR_RD2;

    return 0;
}

/**
 * @brief Reads the SD card
 *
 * @param[in]  dst
 *             destination pointer to copy to
 * 
 * @param[in]  saddr
 *             The start memory address
 * 
 * @param[in]  slen
 *             Length in bytes to copy
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_read(void *dst, u32 saddr, u32 slen) {

    if (((u32) dst & 0x1FFFFFFF) < 0x800000) {
        return sd_disk_read_to_ram(saddr, dst, slen);
    } else {
        return sd_disk_read_to_rom(saddr, ((u32) dst) & 0x3FFFFFF, slen);
    }
}

/******************************************************************************
* sdcard var functions
*******************************************************************************/

/**
 * @brief Closes the SD card
 *
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_close_rw() {

    u8 resp;
    u16 i;

    if (disk_mode == DISK_MODE_NOP)return 0;

    resp = sd_disk_cmd(CMD12, 0);
    disk_mode = DISK_MODE_NOP;
    if (resp)return resp;

    ed64_bios_sdio_bitlength(1);
    ed64_bios_sd_data_read();
    ed64_bios_sd_data_read();
    ed64_bios_sd_data_read();
    ed64_bios_sdio_bitlength(2);

    i = 65535;
    while (--i) {

        if (ed64_bios_sd_data_read() == 0xff)break;
    }

    return 0;
}

/******************************************************************************
* sdcard write functions
*******************************************************************************/

/**
 * @brief Writes to the SD card
 *
 * @param[in]  saddr
 *             The start memory address
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_open_write(u32 saddr) {

    u8 resp;
    if (disk_mode == DISK_MODE_WR && saddr == disk_cur_addr)return 0;

    sd_disk_close_rw();
    disk_cur_addr = saddr;
    if ((disk_card_type & SD_HC) == 0)saddr *= 512;
    resp = sd_disk_cmd(CMD25, saddr);
    if (resp)return resp;

    disk_mode = DISK_MODE_WR;

    return 0;
}

/**
 * @brief Writes to the SD card
 *
 * @param[in]  src
 *             Source pointer to copy from
 * 
 * @param[in]  saddr
 *             The start memory address
 * 
 * @param[in]  slen
 *             Length in bytes to copy
 * 
 * @return 0 on successful or a other value on failure.
 */
u8 sd_disk_write(void *src, u32 saddr, u32 slen) {

    u8 resp;

    resp = sd_disk_open_write(saddr);
    if (resp)return DISK_ERR_WR1;
    disk_cur_addr += slen;

    resp = ed64_bios_ram_to_sdio(src, slen);
    if (resp)return DISK_ERR_WR2;

    return 0;
}
