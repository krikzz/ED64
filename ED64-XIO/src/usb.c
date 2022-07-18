/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "everdrive.h"

u8 usb_cmd_resp(u8 resp);
void usb_cmd_cmem_fill(u8 *cmd);
u8 usb_cmd_rom_wr(u8 *cmd);

void usb_terminal() {

    u8 data[4 + 1];
    u8 tout;
    struct controller_data cd;

    screen_clear();
    screen_print("USB CDC terminal demo");
    screen_print("");
    screen_print("Waiting to receive data...");
    screen_print("");
    screen_print("");
    screen_print("Press (B) to exit");
    screen_repaint();

    data[4] = 1;

    for ( ;; ) { /* forever [equivalent to: "while (1)"] */

        screen_vsync();
        controller_scan();
        cd = get_keys_down();
        if (cd.c[0].B)return;

        if (!ed64_bios_usb_can_read())continue;

        /* read from virtual serial port. 
        Size must be a multiple of 4. 
        Use 512B blocks for best performance */
        tout = ed64_bios_usb_read(data, 4);
        if (tout)continue;

        /* Send echo string back to the serial port */
        ed64_bios_usb_write(data, 4);

        screen_print(data);
        screen_repaint();
    }
}

void usb_load_rom() {

    u8 resp, usb_cmd;
    u8 cmd[16];
    struct controller_data cd;

    screen_clear();
    screen_print("Waiting for ROM data...");
    screen_print("Press (B) to exit");
    screen_repaint();

    for ( ;; ) { /* forever [equivalent to: "while (1)"] */

        screen_vsync();
        controller_scan();
        cd = get_keys_down();
        if (cd.c[0].B)return;

        if (!ed64_bios_usb_can_read())continue;

        resp = ed64_bios_usb_read(cmd, 16);
        if (resp)continue;
        //resp = ed64_bios_usb_read(cmd + 16, 512 - 16);
        //if (resp)return resp;

        if (cmd[0] != 'c')continue;
        if (cmd[1] != 'm')continue;
        if (cmd[2] != 'd')continue;
        usb_cmd = cmd[3];

        /* Host send this command during the everdrive seek */
        if (usb_cmd == 't') {
            usb_cmd_resp(0);
        }

        /* start the rom */
        if (usb_cmd == 's') {
            ed64_bios_rom_savetype_set(ED64_SAVE_EEP16K); /* set save type */
            rom_boot_simulator(CIC_6102); /* run the ROM */
        }

        /* Fill ro memory. Used if ROM size less than 2MB (required for correct crc values) */
        if (usb_cmd == 'c') {
            usb_cmd_cmem_fill(cmd);
        }

        /* write to ROM memory */
        if (usb_cmd == 'W') {
            usb_cmd_rom_wr(cmd);
        }

    }

}

u8 usb_cmd_resp(u8 resp) {

    u8 buff[16];
    buff[0] = 'c';
    buff[1] = 'm';
    buff[2] = 'd';
    buff[3] = 'r';
    buff[4] = resp;
    return ed64_bios_usb_write(buff, sizeof (buff));
}

void usb_cmd_cmem_fill(u8 *cmd) {

    u16 i;
    u32 addr = *(u32 *) & cmd[4];
    u32 slen = *(u32 *) & cmd[8];
    u32 val = *(u32 *) & cmd[12];
    u32 buff[512 / 4];

    for (i = 0; i < 512 / 4; i++) {
        buff[i] = val;
    }

    while (slen--) {
        sys_n64_pi_write(buff, addr, 512);
        addr += 512;
    }
}

u8 usb_cmd_rom_wr(u8 *cmd) {

    u8 resp;
    u8 buff[512];
    u32 addr = *(u32 *) & cmd[4]; /* destination address */
    u32 slen = *(u32 *) & cmd[8]; /* size in sectors (512B) */

    if (slen == 0)return 0;

    ed64_bios_usb_read_start(); /* begin first block receiving (512B) */

    while (slen--) {

        resp = ed64_bios_usb_read_end(buff); /* wait for block receiving completion and read it to the buffer */
        if (slen != 0)ed64_bios_usb_read_start(); /* begin next block receiving while previous block transfers to the ROM */
        if (resp)return resp;
        sys_n64_pi_write(buff, addr, 512); /* copy received block to the rom memory */
        addr += 512;
    }

    return 0;
}