/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "everdrive.h"

u8 fm_load_dir(u8 *path, FILINFO *inf, u32 max_items);
u8 fm_load_rom(u8 *path);

#define MAX_DIR_SIZE    20
#define MAX_STR_LEN     36

u8 fmanager_display() {

    FILINFO inf[MAX_DIR_SIZE];
    u32 selector = 0;
    struct controller_data cd;
    u8 resp;

    /* open root dir */
    resp = fm_load_dir("", inf, MAX_DIR_SIZE);
    if (resp)return resp;


    for ( ;; ) { /* forever [equivalent to: "while (1)"] */

        /* print items */
        screen_clear();
        for (int i = 0; i < MAX_DIR_SIZE && inf[i].fname[0]; i++) {
            screen_print(selector == i ? ">" : " ");
            u8 tmp = inf[i].fname[MAX_STR_LEN];
            inf[i].fname[MAX_STR_LEN] = 0; /* make sure that the printed string doesn't exceed max len */
            screen_append_str_print(inf[i].fname);
            inf[i].fname[MAX_STR_LEN] = tmp;
        }

        screen_repaint();

        /* controls */
        for ( ;; ) { /* forever [equivalent to: "while (1)"] */

            screen_vsync();
            controller_scan();
            cd = get_keys_down();

            if (cd.c[0].B)return 0;

            if (cd.c[0].up) {
                if (selector != 0)selector--;
                break;
            }

            if (cd.c[0].down) {
                if ((selector + 1) < MAX_DIR_SIZE && inf[selector + 1].fname[0])selector++;
                break;
            }

            if (cd.c[0].A && !(inf[selector].fattrib & AM_DIR)) {

                screen_clear();
                screen_print("loading...");
                screen_repaint();

                resp = fm_load_rom(inf[selector].fname);
                if (resp)return resp;

                ed64_bios_rom_savetype_set(ED64_SAVE_EEP16K); /* set save type */
                rom_boot_simulator(CIC_6102); /* run the ROM */
            }
        }
    }

    return 0;
}

u8 fm_load_dir(u8 *path, FILINFO *inf, u32 max_items) {

    u8 resp;
    DIR dir;

    resp = f_opendir(&dir, path);
    if (resp)return resp;

    //load directory items
    for (int i = 0; i < max_items; i++) {

        resp = f_readdir(&dir, &inf[i]);
        if (resp)return resp;
        if (inf[i].fname[0] == 0)break; /* no directory items anymore */
    }

    resp = f_closedir(&dir);
    if (resp)return resp;

    return 0;
}

u8 fm_load_rom(u8 *path) {

    FIL f;
    u8 resp;
    u8 header[8];
    UINT br;
    u32 fsize;

    resp = f_open(&f, path, FA_READ);
    if (resp)return resp;

    fsize = f.obj.objsize - f.fptr;

    /* read rom header */
    resp = f_read(&f, header, sizeof (header), &br);
    if (resp)return resp;

    //set read position
    resp = f_lseek(&f, 0);
    if (resp)return resp;

    if (header[1] == 0x80) {
        /* enable byte swapping for disk operations if rom image has swapped byte order
        affects only reading to ROM address space */
        ed64_bios_write_endian_swap(1);
    }

    /* warning! file can be read directly to rom but not to bram */
    resp = f_read(&f, (void *) ED64_ADDR_ROM, fsize, &br);
    if (resp)return resp;

    ed64_bios_write_endian_swap(0);
    if (resp)return resp;

    resp = f_close(&f);
    if (resp)return resp;

    return 0;
}
