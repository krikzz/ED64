/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "everdrive.h"

u8 fileRead() {

    u8 *path = "ED64/OS64.v64"; /* this file is garanteed to exist! */
    struct controller_data cd;
    u8 buff[256];
    FIL f;
    UINT br;
    u8 resp;

    screen_clear();
    screen_repaint();


    resp = f_open(&f, path, FA_READ);
    if (resp)return resp;

    resp = f_read(&f, buff, sizeof (buff), &br);
    if (resp)return resp;

    resp = f_close(&f);
    if (resp)return resp;


    screen_clear();
    screen_print("Read 256 bytes from file: ");
    screen_print("\"SD:/");
    screen_append_str_print(path);
    screen_append_str_print("\"");
    screen_print("");
    screen_print("");
    for (int i = 0; i < sizeof (buff); i++) {
        if (i % 16 == 0)screen_print("");
        screen_append_hex8_print(buff[i]);
    }
    screen_print("");
    screen_print("");
    screen_print("Press (B) to exit");


    screen_repaint();
    while (1) {
        gVsync();
        controller_scan();
        cd = get_keys_down();

        if (cd.c[0].B) {
            break;
        }
    }

    return 0;
}

u8 fileWrite() {

    u8 *path = "test.txt";
    u8 *msg = "This is an example to show text can be written to a file!";
    struct controller_data cd;
    FIL f;
    UINT bw;
    u8 resp;
    u32 str_len;

    screen_clear();
    screen_repaint();

    for (str_len = 0; msg[str_len] != 0; str_len++);

    resp = f_open(&f, path, FA_WRITE | FA_CREATE_ALWAYS);
    if (resp)return resp;

    resp = f_write(&f, msg, str_len, &bw);
    if (resp)return resp;

    resp = f_close(&f);
    if (resp)return resp;


    screen_clear();
    screen_print("Sucessfully written the text: ");
    screen_print("");
    screen_print("\"");
    screen_append_str_print(msg);
    screen_append_str_print("\"");
    screen_print("");
    screen_print("");
    screen_print("To the file: ");
    screen_print("\"SD:/");
    screen_append_str_print(path);
    screen_append_str_print("\"");
    screen_print("");
    screen_print("");
    screen_print("");
    screen_print("Press (B) to exit");

    screen_repaint();
    while (1) {
        gVsync();
        controller_scan();
        cd = get_keys_down();

        if (cd.c[0].B) {
            break;
        }
    }

    return 0;
}