/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#include "everdrive.h"

void main_display_edid();
void main_display_error(u8 err);
u8 main_display_menu();

int main(void) {

    u8 resp;
    FATFS fs;

    sysInit();
    bi_init();

    screen_clear();
    screen_print("FATFS initilizing...");
    screen_repaint();

    /* mount disk */
    memset(&fs, 0, sizeof (fs));
    resp = f_mount(&fs, "", 1);
    if (resp)main_display_error(resp);


    while (1) {
        resp = main_display_menu();
        if (resp)main_display_error(resp);
    }

}

u8 main_display_menu() {

    enum {
        MENU_FILE_MANAGER,
        MENU_FILE_READ,
        MENU_FILE_WRITE,
        MENU_USB_TERMINAL,
        MENU_USB_LOADER,
        MENU_EDID,
        MENU_SIZE
    };

    struct controller_data cd;
    u8 * menu[MENU_SIZE];
    u32 selector = 0;
    u8 resp;

    menu[MENU_FILE_MANAGER] = "File Explorer";
    menu[MENU_FILE_READ] = "File Read (\"SD:\\ED64\\OS64.v64\")";
    menu[MENU_FILE_WRITE] = "File Write (\"SD:\\test.txt\")";
    menu[MENU_USB_TERMINAL] = "USB Terminal";
    menu[MENU_USB_LOADER] = "USB ROM Loader";
    menu[MENU_EDID] = "ED64 Hardware Rev ID";

    while (1) {

        screen_clear();

        for (int i = 0; i < MENU_SIZE; i++) {
            screen_print("  ");
            if (i == selector) {
                screen_append_str_print(">");
            } else {
                screen_append_str_print(" ");
            }
            screen_append_str_print(menu[i]);
        }

        screen_repaint();
        controller_scan();
        cd = get_keys_down();

        if (cd.c[0].up) {
            if (selector != 0)selector--;
        }

        if (cd.c[0].down) {
            if (selector < MENU_SIZE - 1)selector++;
        }

        if (!cd.c[0].A)continue;

        /* browse files in root dir and launch the ROM */
        if (selector == MENU_FILE_MANAGER) {
            resp = fmanager();
            if (resp)return resp;
        }


        /* read data from file */
        if (selector == MENU_FILE_READ) {
            resp = fileRead();
            if (resp)return resp;
        }

        /* write string to the test.txt file */
        if (selector == MENU_FILE_WRITE) {
            resp = fileWrite();
            if (resp)return resp;
        }

        /* Simple communication via USB. receive and transmit strings. 
        Send some strings via virtual COM port and they will be printed on screen.
        String length should be multiple of 4 */
        if (selector == MENU_USB_TERMINAL) {
            usb_terminal();
        }

        /* usb client demo compatible with usb64.exe */
        if (selector == MENU_USB_LOADER) {
            usb_load_rom();
        }

        /* everdrive hardware identification */
        if (selector == MENU_EDID) {
            main_display_edid();
        }

    }
}

void main_display_edid() {

    struct controller_data cd;
    u32 id = bi_get_cart_id();

    screen_clear();
    screen_print("ED64 H/W Rev ID:   ");
    screen_append_hex32_print(id);
    screen_print("ED64 H/W Rev Name: ");

    switch (id) {
        case CART_ID_V2:
            screen_append_str_print("EverDrive 64 V2.5");
            break;
        case CART_ID_V3:
            screen_append_str_print("EverDrive 64 V3");
            break;
        case CART_ID_X7:
            screen_append_str_print("EverDrive 64 X7");
            break;
        case CART_ID_X5:
            screen_append_str_print("EverDrive 64 X5");
            break;
        default:
            screen_append_str_print("Unknown");
            break;
    }


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

}

void main_display_error(u8 err) {

    screen_clear();
    screen_print("error: ");
    screen_append_hex8_print(err);
    screen_repaint();

    while (1);
}

void boot_simulator(u8 cic) {


    static u16 cheats_on; /* 0 = off, 1 = select, 2 = all */
    static u8 game_cic;

    game_cic = cic;
    cheats_on = 0;


    /*  Start ROM via CIC boot code */
    asm __volatile__(
            ".set noreorder;"

            "lui    $t0, 0x8000;"

            // State required by all CICs
            "move   $s3, $zero;" // osRomType (0: N64, 1: 64DD)
            "lw     $s4, 0x0300($t0);" // osTvType (0: PAL, 1: NTSC, 2: MPAL)
            "move   $s5, $zero;" // osResetType (0: Cold, 1: NMI)
            "lui    $s6, %%hi(cic_ids);" // osCicId (See cic_ids LUT)
            "addu   $s6, $s6, %0;"
            "lbu    $s6, %%lo(cic_ids)($s6);"
            "lw     $s7, 0x0314($t0);" // osVersion

            // Copy PIF code to RSP IMEM (State required by CIC-NUS-6105)
            "lui    $a0, 0xA400;"
            "lui    $a1, %%hi(imem_start);"
            "ori    $a2, $zero, 0x0008;"
            "1:"
            "lw     $t0, %%lo(imem_start)($a1);"
            "addiu  $a1, $a1, 4;"
            "sw     $t0, 0x1000($a0);"
            "addiu  $a2, $a2, -1;"
            "bnez   $a2, 1b;"
            "addiu  $a0, $a0, 4;"

            // Copy CIC boot code to RSP DMEM
            "lui    $t3, 0xA400;"
            "ori    $t3, $t3, 0x0040;" // State required by CIC-NUS-6105
            "move   $a0, $t3;"
            "lui    $a1, 0xB000;"
            "ori    $a2, 0x0FC0;"
            "1:"
            "lw     $t0, 0x0040($a1);"
            "addiu  $a1, $a1, 4;"
            "sw     $t0, 0x0000($a0);"
            "addiu  $a2, $a2, -4;"
            "bnez   $a2, 1b;"
            "addiu  $a0, $a0, 4;"

            // Boot with or without cheats enabled?
            "beqz   %1, 2f;"

            // Patch CIC boot code
            "lui    $a1, %%hi(cic_patch_offsets);"
            "addu   $a1, $a1, %0;"
            "lbu    $a1, %%lo(cic_patch_offsets)($a1);"
            "addu   $a0, $t3, $a1;"
            "lui    $a1, 0x081C;" // "j 0x80700000"
            "ori    $a2, $zero, 0x06;"
            "bne    %0, $a2, 1f;"
            "lui    $a2, 0x8188;"
            "ori    $a2, $a2, 0x764A;"
            "xor    $a1, $a1, $a2;" // CIC-NUS-6106 encryption
            "1:"
            "sw     $a1, 0x0700($a0);" // Patch CIC boot code with jump

            // Patch CIC boot code to disable checksum failure halt
            // Required for CIC-NUS-6105
            "ori    $a2, $zero, 0x05;"
            "beql   %0, $a2, 2f;"
            "sw     $zero, 0x06CC($a0);"

            // Go!
            "2:"
            "lui    $sp, 0xA400;"
            "ori    $ra, $sp, 0x1550;" // State required by CIC-NUS-6105
            "jr     $t3;"
            "ori    $sp, $sp, 0x1FF0;" // State required by CIC-NUS-6105


            // Table of all CIC IDs
            "cic_ids:"
            ".byte  0x00;" // Unused
            ".byte  0x3F;" // NUS-CIC-6101
            ".byte  0x3F;" // NUS-CIC-6102
            ".byte  0x78;" // NUS-CIC-6103
            ".byte  0xAC;" // NUS-CIC-5101
            ".byte  0x91;" // NUS-CIC-6105
            ".byte  0x85;" // NUS-CIC-6106
            ".byte  0xDD;" // NUS-CIC-5167

            "cic_patch_offsets:"
            ".byte  0x00;" // Unused
            ".byte  0x30;" // CIC-NUS-6101
            ".byte  0x2C;" // CIC-NUS-6102
            ".byte  0x20;" // CIC-NUS-6103
            ".byte  0x30;" // NUS-CIC-5101
            ".byte  0x8C;" // CIC-NUS-6105
            ".byte  0x60;" // CIC-NUS-6106
            ".byte  0x30;" // NUS-CIC-5167

            // These instructions are copied to RSP IMEM; we don't execute them.
            "imem_start:"
            "lui    $t5, 0xBFC0;"
            "1:"
            "lw     $t0, 0x07FC($t5);"
            "addiu  $t5, $t5, 0x07C0;"
            "andi   $t0, $t0, 0x0080;"
            "bnezl  $t0, 1b;"
            "lui    $t5, 0xBFC0;"
            "lw     $t0, 0x0024($t5);"
            "lui    $t3, 0xB000;"

            : // outputs
            : "r" (game_cic), // inputs
            "r" (cheats_on)
            : "$4", "$5", "$6", "$8", // clobber
            "$11", "$19", "$20", "$21",
            "$22", "$23", "memory"
            );


    return;
}
