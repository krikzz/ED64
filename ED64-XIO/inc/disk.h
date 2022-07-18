/*
* Copyright (c) Krikzz and Contributors.
* See LICENSE file in the project root for full license information.
*/

#ifndef ED64_SD_DISKIO_H
#define ED64_SD_DISKIO_H

#ifdef __cplusplus
extern "C" {
#endif

#define DISK_ERR_INIT 0xD0 /* command initialization error */
#define DISK_ERR_CTO 0xD1 /* command CTO error */
#define DISK_ERR_RD1 0xD2 /* command timeout */
#define DISK_ERR_RD2 0xD2 /* IO error */
#define DISK_ERR_WR1 0xD3 /* command timeout */
#define DISK_ERR_WR2 0xD3 /* IO error */

u8 sd_disk_init();
u8 sd_disk_read_to_ram(u32 sd_addr, void *dst, u16 slen);
u8 sd_disk_read_to_rom(u32 sd_addr, u32 dst, u16 slen);
u8 sd_disk_read(void *dst, u32 saddr, u32 slen);
u8 sd_disk_write(void *src, u32 saddr, u32 slen);
u8 sd_disk_close_rw();
// u8 sd_disk_stop();

#ifdef __cplusplus
}
#endif

#endif /* ED64_SD_DISKIO_H */
