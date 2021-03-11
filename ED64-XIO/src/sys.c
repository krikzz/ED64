/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "sys.h"

typedef struct PI_regs_s {
    /** @brief Uncached address in RAM where data should be found */
    void * ram_address;
    /** @brief Address of data on peripheral */
    unsigned long pi_address;
    /** @brief How much data to read from RAM into the peripheral */
    unsigned long read_length;
    /** @brief How much data to write to RAM from the peripheral */
    unsigned long write_length;
    /** @brief Status of the PI, including DMA busy */
    unsigned long status;
} PI_regs_s;

typedef struct SI_regs_s {
    /** @brief Uncached address in RAM where data should be found */
    volatile void * DRAM_addr;
    /** @brief Address to read when copying from PIF RAM */
    volatile void * PIF_addr_read;
    /** @brief Reserved word */
    uint32_t reserved1;
    /** @brief Reserved word */
    uint32_t reserved2;
    /** @brief Address to write when copying to PIF RAM */
    volatile void * PIF_addr_write;
    /** @brief Reserved word */
    uint32_t reserved3;
    /** @brief SI status, including DMA busy and IO busy */
    uint32_t status;
} SI_regs_t;


const u32 ntsc_320[] = {
    0x00013002, 0x00000000, 0x00000140, 0x00000200,
    0x00000000, 0x03e52239, 0x0000020d, 0x00000c15,
    0x0c150c15, 0x006c02ec, 0x002501ff, 0x000e0204,
    0x00000200, 0x00000400
};


const u32 mpal_320[] = {
    0x00013002, 0x00000000, 0x00000140, 0x00000200,
    0x00000000, 0x04651e39, 0x0000020d, 0x00040c11,
    0x0c190c1a, 0x006c02ec, 0x002501ff, 0x000e0204,
    0x00000200, 0x00000400
};

const u32 pal_320[] = {
    0x00013002, 0x00000000, 0x00000140, 0x00000200,
    0x00000000, 0x0404233a, 0x00000271, 0x00150c69,
    0x0c6f0c6e, 0x00800300, 0x005f0239, 0x0009026b,
    0x00000200, 0x00000400
};

#define SI_STATUS_DMA_BUSY  ( 1 << 0 )
#define SI_STATUS_IO_BUSY   ( 1 << 1 )
#define SYS_MAX_PIXEL_W   320
#define SYS_MAX_PIXEL_H   240

void sysDisplayInit();
void sysPI_rd_safe(void *ram, unsigned long pi_address, unsigned long len);
void sysPI_wr_safe(void *ram, unsigned long pi_address, unsigned long len);


static volatile struct PI_regs_s * const PI_regs = (struct PI_regs_s *) 0xa4600000;
//static volatile struct SI_regs_s * const SI_regs = (struct SI_regs_s *) 0xa4800000; //Un-used in sample
//static void * const PIF_RAM = (void *) 0x1fc007c0; //Un-used in sample
static vu32 *vregs = (vu32 *) 0xa4400000;
BootStrap *sys_boot_strap = (BootStrap *) 0x80000300;

extern u8 font[];
Screen screen;

u16 pal[16] = {
    RGB(0, 0, 0), RGB(31, 31, 31), RGB(16, 16, 16), RGB(28, 28, 2),
    RGB(8, 8, 8), RGB(31, 0, 0), RGB(0, 31, 0), RGB(12, 12, 12),
    0x0000, 0x0000, 0x0000, 0x0000,
    0x0000, 0x0000, 0x0000, 0x0aa0
};

void sysInit() {

    disable_interrupts();
    set_AI_interrupt(0);
    set_VI_interrupt(0, 0);
    set_PI_interrupt(0);
    set_DP_interrupt(0);


    IO_WRITE(PI_STATUS_REG, 3);
    IO_WRITE(PI_BSD_DOM1_LAT_REG, 0x40);
    IO_WRITE(PI_BSD_DOM1_PWD_REG, 0x12);
    IO_WRITE(PI_BSD_DOM1_PGS_REG, 0x07);
    IO_WRITE(PI_BSD_DOM1_RLS_REG, 0x03);



    IO_WRITE(PI_BSD_DOM2_LAT_REG, 0x05);
    IO_WRITE(PI_BSD_DOM2_PWD_REG, 0x0C);
    IO_WRITE(PI_BSD_DOM2_PGS_REG, 0x0D);
    IO_WRITE(PI_BSD_DOM2_RLS_REG, 0x02);
    /*
    IO_WRITE(PI_BSD_DOM2_LAT_REG, 0x40);
    IO_WRITE(PI_BSD_DOM2_PWD_REG, 0x12);
    IO_WRITE(PI_BSD_DOM2_PGS_REG, 0x07);
    IO_WRITE(PI_BSD_DOM2_RLS_REG, 0x03);*/

    rdp_init();

    screen.buff_sw = 0;
    screen.buff[0] = (u16 *) malloc(SYS_MAX_PIXEL_W * SYS_MAX_PIXEL_H * 2);
    screen.buff[1] = (u16 *) malloc(SYS_MAX_PIXEL_W * SYS_MAX_PIXEL_H * 2);
    screen.current = screen.buff[screen.buff_sw];
    screen.bgr_ptr = 0;

    sysDisplayInit();

}

void sysDisplayInit() {

    u32 i;
    u32 *v_setup;
    u32 region = *(u32 *) 0x80000300;

    disable_interrupts();


    screen.w = 40;
    screen.h = 30;
    screen.pixel_w = 320;
    screen.pixel_h = 240;
    screen.buff_len = screen.pixel_w * screen.pixel_h;
    screen.char_h = 8;
    v_setup = region == REGION_NTSC ? (u32*) ntsc_320 : region == REGION_PAL ? (u32*) pal_320 : (u32*) mpal_320;

    while (IO_READ(VI_CURRENT_REG) > 10);
    IO_WRITE(VI_CONTROL_REG, 0);

    vregs[1] = (vu32) screen.current;
    vregs[2] = v_setup[2];
    for (i = 5; i < 9; i++)vregs[i] = v_setup[i];
    vregs[9] = v_setup[9]; //bnack
    vregs[10] = v_setup[10];
    vregs[11] = v_setup[11];
    vregs[3] = v_setup[3];
    vregs[12] = v_setup[12];
    vregs[13] = v_setup[13];
    vregs[0] = v_setup[0];


    enable_interrupts();
}

void sysPI_rd(void *ram, unsigned long pi_address, unsigned long len) {

    pi_address &= 0x1FFFFFFF;

    data_cache_hit_writeback_invalidate(ram, len);
    disable_interrupts();

    while (dma_busy());
    IO_WRITE(PI_STATUS_REG, 3);
    PI_regs->ram_address = ram;
    PI_regs->pi_address = pi_address; //(pi_address | 0x10000000) & 0x1FFFFFFF;
    PI_regs->write_length = len - 1;
    while (dma_busy());

    enable_interrupts();
}

void sysPI_wr(void *ram, unsigned long pi_address, unsigned long len) {

    pi_address &= 0x1FFFFFFF;

    data_cache_hit_writeback(ram, len);
    disable_interrupts();

    while (dma_busy());
    IO_WRITE(PI_STATUS_REG, 3);
    PI_regs->ram_address = ram;
    PI_regs->pi_address = pi_address; //(pi_address | 0x10000000) & 0x1FFFFFFF;
    PI_regs->read_length = len - 1;
    while (dma_busy());

    enable_interrupts();

}

//****************************************************************************** gfx
u16 *g_disp_ptr;
u16 g_cur_pal;
u16 g_cons_ptr;
u8 g_last_x;
u8 g_last_y;
u16 gfx_buff[G_SCREEN_W * G_SCREEN_H];

void gDrawChar8X8(u32 val, u32 x, u32 y) {

    u64 tmp;
    u32 font_val;
    u8 *font_ptr = &font[(val & 0xff) * 8];
    u64 *ptr = (u64 *) & screen.current[ (x * 8 + y * 8 * screen.pixel_w)];
    u64 pal_bgr = pal[(val >> 8) & 0x0f];
    u64 pal_txt = pal[val >> 12];

    for (int i = 0; i < 4; i++) {
        pal_bgr = (pal_bgr << 16) | pal_bgr;
    }

    for (u32 i = 0; i < 8; i++) {

        font_val = *font_ptr++;

        tmp = pal_bgr;
        if ((font_val & 0x80) != 0)tmp = (tmp & 0x0000FFFFFFFFFFFF) | (pal_txt << 48);
        if ((font_val & 0x40) != 0)tmp = (tmp & 0xFFFF0000FFFFFFFF) | (pal_txt << 32);
        if ((font_val & 0x20) != 0)tmp = (tmp & 0xFFFFFFFF0000FFFF) | (pal_txt << 16);
        if ((font_val & 0x10) != 0)tmp = (tmp & 0xFFFFFFFFFFFF0000) | (pal_txt);
        *ptr++ = tmp;

        tmp = pal_bgr;
        if ((font_val & 0x08) != 0)tmp = (tmp & 0x0000FFFFFFFFFFFF) | (pal_txt << 48);
        if ((font_val & 0x04) != 0)tmp = (tmp & 0xFFFF0000FFFFFFFF) | (pal_txt << 32);
        if ((font_val & 0x02) != 0)tmp = (tmp & 0xFFFFFFFF0000FFFF) | (pal_txt << 16);
        if ((font_val & 0x01) != 0)tmp = (tmp & 0xFFFFFFFFFFFF0000) | (pal_txt);
        *ptr++ = tmp;

        ptr += (screen.pixel_w - 8) / 4;
    }
}

void screen_repaint() {

    u16 *chr_ptr = gfx_buff;

    screen.buff_sw = (screen.buff_sw ^ 1) & 1;
    screen.current = screen.buff[screen.buff_sw];


    for (u32 y = 0; y < screen.h; y++) {
        for (u32 x = 0; x < screen.w; x++) {

            gDrawChar8X8(*chr_ptr++, x, y);
        }
    }

    data_cache_hit_writeback(screen.current, screen.buff_len * 2);
    screen_vsync();
    vregs[1] = (vu32) screen.current;

}

void screen_vsync() {

    while (vregs[4] == 0x200);
    while (vregs[4] != 0x200);
}


void screen_append_hex4_print(u8 val);

void screen_clear() {

    g_cur_pal = 0;
    screen_set_xy_pos(G_BORDER_X, G_BORDER_Y);
    for (int i = 0; i < G_SCREEN_W * G_SCREEN_H; i++)gfx_buff[i] = PAL_B3;
    screen_set_pal(PAL_B1);
}

void screen_set_pal(u16 pal) {
    g_cur_pal = pal;
}

void screen_append_str_print(u8 *str) {
    while (*str != 0)*g_disp_ptr++ = *str++ + g_cur_pal;
}

void screen_append_char_print(u8 chr) {

    *g_disp_ptr++ = chr + g_cur_pal;
}

void screen_append_hex4_print(u8 val) {

    val += (val < 10 ? '0' : '7');
    *g_disp_ptr++ = val + g_cur_pal;
}

void screen_append_hex8_print(u8 val) {

    screen_append_hex4_print(val >> 4);
    screen_append_hex4_print(val & 15);
}

void screen_append_hex16_print(u16 val) {

    screen_append_hex8_print(val >> 8);
    screen_append_hex8_print(val);
}

void screen_append_hex32_print(u32 val) {

    screen_append_hex16_print(val >> 16);
    screen_append_hex16_print(val);

}

void screen_set_xy_pos(u8 x, u8 y) {

    g_cons_ptr = x + y * G_SCREEN_W;
    g_disp_ptr = &gfx_buff[g_cons_ptr];
    g_last_x = x;
    g_last_y = y;
}

void screen_print(u8 *str) {

    g_disp_ptr = &gfx_buff[g_cons_ptr];
    g_cons_ptr += G_SCREEN_W;
    g_last_y++;
    screen_append_str_print(str);
}
