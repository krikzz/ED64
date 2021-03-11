/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "bios.h"


#define REG_BASE        0x1F800000
#define REG_FPG_CFG     0x0000
#define REG_USB_CFG     0x0004
#define REG_TIMER       0x000C
#define REG_BOOT_CFG    0x0010
#define REG_EDID        0x0014
#define REG_I2C_CMD     0x0018
#define REG_I2C_DAT     0x001C

#define REG_FPG_DAT     0x0200
#define REG_USB_DAT     0x0400

#define REG_SYS_CFG     0x8000
#define REG_KEY         0x8004
#define REG_DMA_STA     0x8008
#define REG_DMA_ADDR    0x8008
#define REG_DMA_LEN     0x800C
#define REG_RTC_SET     0x8010
#define REG_GAM_CFG     0x8018
#define REG_IOM_CFG     0x801C
#define REG_SDIO        0x8020
#define REG_SDIO_ARD    0x8200
#define REG_IOM_DAT     0x8400
#define REG_DD_TBL      0x8800
#define REG_SD_CMD_RD   (REG_SDIO + 0x00*4)
#define REG_SD_CMD_WR   (REG_SDIO + 0x01*4)
#define REG_SD_DAT_RD   (REG_SDIO + 0x02*4)
#define REG_SD_DAT_WR   (REG_SDIO + 0x03*4)
#define REG_SD_STATUS   (REG_SDIO + 0x04*4)

#define DMA_STA_BUSY    0x0001
#define DMA_STA_ERROR   0x0002
#define DMA_STA_LOCK    0x0080

#define SD_CFG_BITLEN   0x000F
#define SD_CFG_SPD      0x0010
#define SD_STA_BUSY     0x0080

#define CFG_BROM_ON     0x0001
#define CFG_REGS_OFF    0x0002
#define CFG_SWAP_ON     0x0004

#define FPG_CFG_NCFG    0x0001
#define FPG_STA_CDON    0x0001
#define FPG_STA_NSTAT   0x0002

#define I2C_CMD_DAT     0x10
#define I2C_CMD_STA     0x20
#define I2C_CMD_END     0x30

#define IOM_CFG_SS      0x0001
#define IOM_CFG_RST     0x0002
#define IOM_CFG_ACT     0x0080
#define IOM_STA_CDN     0x0001

#define USB_LE_CFG      0x8000
#define USB_LE_CTR      0x4000

#define USB_CFG_ACT     0x0200
#define USB_CFG_RD      0x0400
#define USB_CFG_WR      0x0000

#define USB_STA_ACT     0x0200
#define USB_STA_RXF     0x0400
#define USB_STA_TXE     0x0800
#define USB_STA_PWR     0x1000
#define USB_STA_BSY     0x2000

#define USB_CMD_RD_NOP  (USB_LE_CFG | USB_LE_CTR | USB_CFG_RD)
#define USB_CMD_RD      (USB_LE_CFG | USB_LE_CTR | USB_CFG_RD | USB_CFG_ACT)
#define USB_CMD_WR_NOP  (USB_LE_CFG | USB_LE_CTR | USB_CFG_WR)
#define USB_CMD_WR      (USB_LE_CFG | USB_LE_CTR | USB_CFG_WR | USB_CFG_ACT)

#define REG_LAT 0x04
#define REG_PWD 0x04

#define ROM_LAT 0x40
#define ROM_PWD 0x12

#define REG_ADDR(reg)   (KSEG1 | REG_BASE | (reg))

u32 bi_reg_rd(u16 reg);
void bi_reg_wr(u16 reg, u32 val);
void bi_usb_init();
u8 bi_usb_busy();

u16 bi_sd_cfg;

void bi_init() {

    /* setup n64 bus timings for better performance */
    IO_WRITE(PI_BSD_DOM1_LAT_REG, 0x04);
    IO_WRITE(PI_BSD_DOM1_PWD_REG, 0x0C);

    /* unlock regs */
    bi_reg_wr(REG_KEY, 0xAA55);

    bi_reg_wr(REG_SYS_CFG, 0);

    /* flush usb */
    bi_usb_init();

    bi_sd_cfg = 0;
    bi_reg_wr(REG_SD_STATUS, bi_sd_cfg);

    /* turn off backup ram */
    bi_game_cfg_set(SAVE_OFF);
}

void bi_reg_wr(u16 reg, u32 val) {

    sysPI_wr(&val, REG_ADDR(reg), 4);
}

u32 bi_reg_rd(u16 reg) {

    u32 val;
    sysPI_rd(&val, REG_ADDR(reg), 4);
    return val;
}

void bi_usb_init() {

    u8 buff[512];
    u8 resp;
    bi_reg_wr(REG_USB_CFG, USB_CMD_RD_NOP); /* turn off usb r/w activity */

    /* flush fifo buffer */
    while (bi_usb_can_rd()) {
        resp = bi_usb_rd(buff, 512);
        if (resp)break;
    }
}

u8 bi_usb_can_rd() {

    u32 status = bi_reg_rd(REG_USB_CFG) & (USB_STA_PWR | USB_STA_RXF);
    if (status == USB_STA_PWR)return 1;
    return 0;
}

u8 bi_usb_can_wr() {

    u32 status = bi_reg_rd(REG_USB_CFG) & (USB_STA_PWR | USB_STA_TXE);
    if (status == USB_STA_PWR)return 1;
    return 0;
}

u8 bi_usb_busy() {

    u32 tout = 0;

    while ((bi_reg_rd(REG_USB_CFG) & USB_STA_ACT) != 0) {

        if (tout++ != 8192)continue;
        bi_reg_wr(REG_USB_CFG, USB_CMD_RD_NOP);
        return BI_ERR_USB_TOUT;
    }

    return 0;
}

u8 bi_usb_rd(void *dst, u32 len) {

    u8 resp = 0;
    u16 blen, baddr;

    while (len) {

        blen = 512; /* rx block len */
        if (blen > len)blen = len;
        baddr = 512 - blen; /* address in fpga internal buffer. requested data length equal to 512-int buffer addr */


        bi_reg_wr(REG_USB_CFG, USB_CMD_RD | baddr); /* usb read request. fpga will receive usb bytes until the buffer address reaches 512 */

        resp = bi_usb_busy(); /* wait until requested data amount will be transferred to the internal buffer */
        if (resp)break; /* timeout */

        sysPI_rd(dst, REG_ADDR(REG_USB_DAT + baddr), blen); /* get data from internal buffer */

        dst += blen;
        len -= blen;
    }

    return resp;
}

u8 bi_usb_wr(void *src, u32 len) {

    u8 resp = 0;
    u16 blen, baddr;

    bi_reg_wr(REG_USB_CFG, USB_CMD_WR_NOP);

    while (len) {

        blen = 512; /* tx block len */
        if (blen > len)blen = len;
        baddr = 512 - blen; /* address in fpga internal buffer. data length equal to 512-int buffer addr */

        sysPI_wr(src, REG_ADDR(REG_USB_DAT + baddr), blen); /* copy data to the internal buffer */
        src += 512;

        bi_reg_wr(REG_USB_CFG, USB_CMD_WR | baddr); /* usb write request */

        resp = bi_usb_busy(); /* wait until the requested data amount is transferred */
        if (resp)break; /* timeout */

        len -= blen;
    }

    return resp;
}

void bi_usb_rd_start() {

    bi_reg_wr(REG_USB_CFG, USB_CMD_RD | 512);
}

u8 bi_usb_rd_end(void *dst) {

    u8 resp = bi_usb_busy();
    if (resp)return resp;

    sysPI_rd(dst, REG_ADDR(REG_USB_DAT), 512);

    return 0;
}
//******************************************************************************
// sdio
//******************************************************************************/
void sdCrc16(void *src, u16 *crc_out);

void bi_sd_speed(u8 speed) {

    if (speed == BI_DISK_SPD_LO) {
        bi_sd_cfg &= ~SD_CFG_SPD;
    } else {
        bi_sd_cfg |= SD_CFG_SPD;
    }

    bi_reg_wr(REG_SD_STATUS, bi_sd_cfg);
}
u16 bi_old_sd_mode;
/* this function gives time for setting stable values on open bus */

void bi_sd_switch_mode(u16 mode) {

    if (bi_old_sd_mode == mode)return;
    bi_old_sd_mode = mode;

    u16 old_sd_cfg = bi_sd_cfg;
    bi_sd_bitlen(0);
    bi_reg_wr(mode, 0xffff);
    bi_sd_cfg = old_sd_cfg;
    bi_reg_wr(REG_SD_STATUS, old_sd_cfg);
}

void bi_sd_bitlen(u8 val) {

    bi_sd_cfg &= ~SD_CFG_BITLEN;
    bi_sd_cfg |= (val & SD_CFG_BITLEN);
    bi_reg_wr(REG_SD_STATUS, bi_sd_cfg);
}

void bi_sd_busy() {
    while ((bi_reg_rd(REG_SD_STATUS) & SD_STA_BUSY) != 0);
}

void bi_sd_cmd_wr(u8 val) {
    bi_sd_switch_mode(REG_SD_CMD_WR);
    bi_reg_wr(REG_SD_CMD_WR, val);
    bi_sd_busy();
}

u8 bi_sd_cmd_rd() {

    bi_sd_switch_mode(REG_SD_CMD_RD);
    bi_reg_wr(REG_SD_CMD_RD, 0xffff);
    bi_sd_busy();
    return bi_reg_rd(REG_SD_CMD_RD);
}

void bi_sd_dat_wr(u8 val) {
    bi_sd_switch_mode(REG_SD_DAT_WR);
    bi_reg_wr(REG_SD_DAT_WR, 0x00ff | (val << 8));
    //bi_sd_busy();
}

u8 bi_sd_dat_rd() {

    bi_sd_switch_mode(REG_SD_DAT_RD);
    bi_reg_wr(REG_SD_DAT_RD, 0xffff);
    //bi_sd_busy();
    return bi_reg_rd(REG_SD_DAT_RD);
}

u8 bi_sd_to_ram(void *dst, u16 slen) {

    u16 i;
    u8 crc[8];
    u32 old_pwd = IO_READ(PI_BSD_DOM1_PWD_REG);
    IO_WRITE(PI_BSD_DOM1_PWD_REG, 0x09);


    while (slen--) {

        bi_sd_bitlen(1);
        i = 1;
        while (bi_sd_dat_rd() != 0xf0) {
            i++;
            if (i == 0) {
                IO_WRITE(PI_BSD_DOM1_PWD_REG, old_pwd);
                return 1;
            }
        }

        bi_sd_bitlen(4);

        bi_sd_switch_mode(REG_SD_DAT_RD);
        sysPI_rd(dst, REG_ADDR(REG_SDIO_ARD), 512);
        sysPI_rd(crc, REG_ADDR(REG_SDIO_ARD), 8);
        dst += 512;

    }

    IO_WRITE(PI_BSD_DOM1_PWD_REG, old_pwd);

    return 0;
}

u8 bi_sd_to_rom(u32 dst, u16 slen) {

    u16 resp = DMA_STA_BUSY;

    bi_reg_wr(REG_DMA_ADDR, dst);
    bi_reg_wr(REG_DMA_LEN, slen);

    bi_sd_switch_mode(REG_SD_DAT_RD);
    while ((resp & DMA_STA_BUSY)) {
        resp = bi_reg_rd(REG_DMA_STA);
    }

    if ((resp & DMA_STA_ERROR))return 1;

    return 0;
}

u8 bi_ram_to_sd(void *src, u16 slen) {

    u8 resp;
    u16 crc[4];

    while (slen--) {

        sdCrc16(src, crc);

        bi_sd_bitlen(2);
        bi_sd_dat_wr(0xff);
        bi_sd_dat_wr(0xf0);

        bi_sd_bitlen(4);
        sysPI_wr(src, REG_ADDR(REG_SDIO_ARD), 512);
        sysPI_wr(crc, REG_ADDR(REG_SDIO_ARD), 8);
        src += 512;

        bi_sd_bitlen(1);
        bi_sd_dat_wr(0xff);

        for (int i = 0;; i++) {
            resp = bi_sd_dat_rd();
            if ((resp & 1) == 0)break;
            if (i == 1024)return 1;
        }

        resp = 0;
        for (int i = 0; i < 3; i++) {
            resp <<= 1;
            resp |= bi_sd_dat_rd() & 1;
        }

        resp &= 7;
        if (resp != 0x02) {
            if (resp == 5)return 2; /* crc error */
            return 3;
        }

        for (int i = 0;; i++) {

            if (bi_sd_dat_rd() == 0xff)break;
            if (i == 65535)return 4;
        }
    }


    return 0;
}

void sdCrc16(void *src, u16 *crc_out) {

    u16 i;
    u16 u;
    u8 *src8;
    u8 val[4];
    u16 crc_table[4];
    u16 tmp1;
    u8 dat;


    static const u8 crc_bit_table[256] = {
        0x00, 0x01, 0x04, 0x05, 0x10, 0x11, 0x14, 0x15, 0x40, 0x41, 0x44, 0x45, 0x50, 0x51, 0x54, 0x55,
        0x02, 0x03, 0x06, 0x07, 0x12, 0x13, 0x16, 0x17, 0x42, 0x43, 0x46, 0x47, 0x52, 0x53, 0x56, 0x57,
        0x08, 0x09, 0x0C, 0x0D, 0x18, 0x19, 0x1C, 0x1D, 0x48, 0x49, 0x4C, 0x4D, 0x58, 0x59, 0x5C, 0x5D,
        0x0A, 0x0B, 0x0E, 0x0F, 0x1A, 0x1B, 0x1E, 0x1F, 0x4A, 0x4B, 0x4E, 0x4F, 0x5A, 0x5B, 0x5E, 0x5F,
        0x20, 0x21, 0x24, 0x25, 0x30, 0x31, 0x34, 0x35, 0x60, 0x61, 0x64, 0x65, 0x70, 0x71, 0x74, 0x75,
        0x22, 0x23, 0x26, 0x27, 0x32, 0x33, 0x36, 0x37, 0x62, 0x63, 0x66, 0x67, 0x72, 0x73, 0x76, 0x77,
        0x28, 0x29, 0x2C, 0x2D, 0x38, 0x39, 0x3C, 0x3D, 0x68, 0x69, 0x6C, 0x6D, 0x78, 0x79, 0x7C, 0x7D,
        0x2A, 0x2B, 0x2E, 0x2F, 0x3A, 0x3B, 0x3E, 0x3F, 0x6A, 0x6B, 0x6E, 0x6F, 0x7A, 0x7B, 0x7E, 0x7F,
        0x80, 0x81, 0x84, 0x85, 0x90, 0x91, 0x94, 0x95, 0xC0, 0xC1, 0xC4, 0xC5, 0xD0, 0xD1, 0xD4, 0xD5,
        0x82, 0x83, 0x86, 0x87, 0x92, 0x93, 0x96, 0x97, 0xC2, 0xC3, 0xC6, 0xC7, 0xD2, 0xD3, 0xD6, 0xD7,
        0x88, 0x89, 0x8C, 0x8D, 0x98, 0x99, 0x9C, 0x9D, 0xC8, 0xC9, 0xCC, 0xCD, 0xD8, 0xD9, 0xDC, 0xDD,
        0x8A, 0x8B, 0x8E, 0x8F, 0x9A, 0x9B, 0x9E, 0x9F, 0xCA, 0xCB, 0xCE, 0xCF, 0xDA, 0xDB, 0xDE, 0xDF,
        0xA0, 0xA1, 0xA4, 0xA5, 0xB0, 0xB1, 0xB4, 0xB5, 0xE0, 0xE1, 0xE4, 0xE5, 0xF0, 0xF1, 0xF4, 0xF5,
        0xA2, 0xA3, 0xA6, 0xA7, 0xB2, 0xB3, 0xB6, 0xB7, 0xE2, 0xE3, 0xE6, 0xE7, 0xF2, 0xF3, 0xF6, 0xF7,
        0xA8, 0xA9, 0xAC, 0xAD, 0xB8, 0xB9, 0xBC, 0xBD, 0xE8, 0xE9, 0xEC, 0xED, 0xF8, 0xF9, 0xFC, 0xFD,
        0xAA, 0xAB, 0xAE, 0xAF, 0xBA, 0xBB, 0xBE, 0xBF, 0xEA, 0xEB, 0xEE, 0xEF, 0xFA, 0xFB, 0xFE, 0xFF,
    };

    static const u16 crc_16_table[] = {
        0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50A5, 0x60C6, 0x70E7,
        0x8108, 0x9129, 0xA14A, 0xB16B, 0xC18C, 0xD1AD, 0xE1CE, 0xF1EF,
        0x1231, 0x0210, 0x3273, 0x2252, 0x52B5, 0x4294, 0x72F7, 0x62D6,
        0x9339, 0x8318, 0xB37B, 0xA35A, 0xD3BD, 0xC39C, 0xF3FF, 0xE3DE,
        0x2462, 0x3443, 0x0420, 0x1401, 0x64E6, 0x74C7, 0x44A4, 0x5485,
        0xA56A, 0xB54B, 0x8528, 0x9509, 0xE5EE, 0xF5CF, 0xC5AC, 0xD58D,
        0x3653, 0x2672, 0x1611, 0x0630, 0x76D7, 0x66F6, 0x5695, 0x46B4,
        0xB75B, 0xA77A, 0x9719, 0x8738, 0xF7DF, 0xE7FE, 0xD79D, 0xC7BC,
        0x48C4, 0x58E5, 0x6886, 0x78A7, 0x0840, 0x1861, 0x2802, 0x3823,
        0xC9CC, 0xD9ED, 0xE98E, 0xF9AF, 0x8948, 0x9969, 0xA90A, 0xB92B,
        0x5AF5, 0x4AD4, 0x7AB7, 0x6A96, 0x1A71, 0x0A50, 0x3A33, 0x2A12,
        0xDBFD, 0xCBDC, 0xFBBF, 0xEB9E, 0x9B79, 0x8B58, 0xBB3B, 0xAB1A,
        0x6CA6, 0x7C87, 0x4CE4, 0x5CC5, 0x2C22, 0x3C03, 0x0C60, 0x1C41,
        0xEDAE, 0xFD8F, 0xCDEC, 0xDDCD, 0xAD2A, 0xBD0B, 0x8D68, 0x9D49,
        0x7E97, 0x6EB6, 0x5ED5, 0x4EF4, 0x3E13, 0x2E32, 0x1E51, 0x0E70,
        0xFF9F, 0xEFBE, 0xDFDD, 0xCFFC, 0xBF1B, 0xAF3A, 0x9F59, 0x8F78,
        0x9188, 0x81A9, 0xB1CA, 0xA1EB, 0xD10C, 0xC12D, 0xF14E, 0xE16F,
        0x1080, 0x00A1, 0x30C2, 0x20E3, 0x5004, 0x4025, 0x7046, 0x6067,
        0x83B9, 0x9398, 0xA3FB, 0xB3DA, 0xC33D, 0xD31C, 0xE37F, 0xF35E,
        0x02B1, 0x1290, 0x22F3, 0x32D2, 0x4235, 0x5214, 0x6277, 0x7256,
        0xB5EA, 0xA5CB, 0x95A8, 0x8589, 0xF56E, 0xE54F, 0xD52C, 0xC50D,
        0x34E2, 0x24C3, 0x14A0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
        0xA7DB, 0xB7FA, 0x8799, 0x97B8, 0xE75F, 0xF77E, 0xC71D, 0xD73C,
        0x26D3, 0x36F2, 0x0691, 0x16B0, 0x6657, 0x7676, 0x4615, 0x5634,
        0xD94C, 0xC96D, 0xF90E, 0xE92F, 0x99C8, 0x89E9, 0xB98A, 0xA9AB,
        0x5844, 0x4865, 0x7806, 0x6827, 0x18C0, 0x08E1, 0x3882, 0x28A3,
        0xCB7D, 0xDB5C, 0xEB3F, 0xFB1E, 0x8BF9, 0x9BD8, 0xABBB, 0xBB9A,
        0x4A75, 0x5A54, 0x6A37, 0x7A16, 0x0AF1, 0x1AD0, 0x2AB3, 0x3A92,
        0xFD2E, 0xED0F, 0xDD6C, 0xCD4D, 0xBDAA, 0xAD8B, 0x9DE8, 0x8DC9,
        0x7C26, 0x6C07, 0x5C64, 0x4C45, 0x3CA2, 0x2C83, 0x1CE0, 0x0CC1,
        0xEF1F, 0xFF3E, 0xCF5D, 0xDF7C, 0xAF9B, 0xBFBA, 0x8FD9, 0x9FF8,
        0x6E17, 0x7E36, 0x4E55, 0x5E74, 0x2E93, 0x3EB2, 0x0ED1, 0x1EF0
    };


    for (i = 0; i < 4; i++)crc_table[i] = 0;
    src8 = (u8 *) src;

    for (i = 0; i < 128; i++) {


        dat = *src8++;
        val[3] = (dat & 0x88);
        val[2] = (dat & 0x44) << 1;
        val[1] = (dat & 0x22) << 2;
        val[0] = (dat & 0x11) << 3;

        dat = *src8++;
        val[3] |= (dat & 0x88) >> 1;
        val[2] |= (dat & 0x44);
        val[1] |= (dat & 0x22) << 1;
        val[0] |= (dat & 0x11) << 2;

        dat = *src8++;
        val[3] |= (dat & 0x88) >> 2;
        val[2] |= (dat & 0x44) >> 1;
        val[1] |= (dat & 0x22);
        val[0] |= (dat & 0x11) << 1;

        dat = *src8++;
        val[3] |= (dat & 0x88) >> 3;
        val[2] |= (dat & 0x44) >> 2;
        val[1] |= (dat & 0x22) >> 1;
        val[0] |= (dat & 0x11);

        val[0] = crc_bit_table[val[0]];
        val[1] = crc_bit_table[val[1]];
        val[2] = crc_bit_table[val[2]];
        val[3] = crc_bit_table[val[3]];

        tmp1 = crc_table[0];
        crc_table[0] = crc_16_table[(tmp1 >> 8) ^ val[0]];
        crc_table[0] = crc_table[0] ^ (tmp1 << 8);

        tmp1 = crc_table[1];
        crc_table[1] = crc_16_table[(tmp1 >> 8) ^ val[1]];
        crc_table[1] = crc_table[1] ^ (tmp1 << 8);

        tmp1 = crc_table[2];
        crc_table[2] = crc_16_table[(tmp1 >> 8) ^ val[2]];
        crc_table[2] = crc_table[2] ^ (tmp1 << 8);

        tmp1 = crc_table[3];
        crc_table[3] = crc_16_table[(tmp1 >> 8) ^ val[3]];
        crc_table[3] = crc_table[3] ^ (tmp1 << 8);

    }

    for (i = 0; i < 4; i++) {
        for (u = 0; u < 16; u++) {
            crc_out[3 - i] >>= 1;
            crc_out[3 - i] |= (crc_table[u % 4] & 1) << 15;
            crc_table[u % 4] >>= 1;
        }
    }

}

//******************************************************************************/

void bi_game_cfg_set(u8 type) {

    bi_reg_wr(REG_GAM_CFG, type);
}

/* swaps bytes copied from SD card. only affects reads to ROM area */
void bi_wr_swap(u8 swap_on) {

    if (swap_on) {
        bi_reg_wr(REG_SYS_CFG, CFG_SWAP_ON);
    } else {
        bi_reg_wr(REG_SYS_CFG, 0);
    }
}

u32 bi_get_cart_id() {

    return bi_reg_rd(REG_EDID);
}
