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

    gCleanScreen();
    gRepaint();


    resp = f_open(&f, path, FA_READ);
    if (resp)return resp;

    resp = f_read(&f, buff, sizeof (buff), &br);
    if (resp)return resp;

    resp = f_close(&f);
    if (resp)return resp;


    gCleanScreen();
    gConsPrint("Content read from: ");
    gAppendString("\"SD:\\");
    gAppendString(path);
    gAppendString("\"");
    gConsPrint("Press (B) to exit");

    gConsPrint("");
    for (int i = 0; i < sizeof (buff); i++) {
        if (i % 16 == 0)gConsPrint("");
        gAppendHex8(buff[i]);
    }


    gRepaint();
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

    gCleanScreen();
    gRepaint();

    for (str_len = 0; msg[str_len] != 0; str_len++);

    resp = f_open(&f, path, FA_WRITE | FA_CREATE_ALWAYS);
    if (resp)return resp;

    resp = f_write(&f, msg, str_len, &bw);
    if (resp)return resp;

    resp = f_close(&f);
    if (resp)return resp;


    gCleanScreen();
    gConsPrint("Sucessfully written the text: ");
    gConsPrint("");
    gConsPrint("\"");
    gAppendString(msg);
    gAppendString("\"");
    gConsPrint("");
    gConsPrint("");
    gConsPrint("To the file: ");
    gConsPrint("\"SD:\\");
    gAppendString(path);
    gAppendString("\"");
    gConsPrint("");
    gConsPrint("");
    gConsPrint("");
    gConsPrint("Press (B) to exit");

    gRepaint();
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