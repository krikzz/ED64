/* 
 * File:   disk.h
 * Author: igor
 *
 * Created on September 11, 2020, 7:00 PM
 */

#ifndef DISK_H
#define	DISK_H


#define DISK_ERR_INIT   0xD0
#define DISK_ERR_CTO    0xD1
#define DISK_ERR_RD1    0xD2//cmd tout
#define DISK_ERR_RD2    0xD2//io error
#define DISK_ERR_WR1    0xD3//cmd tout
#define DISK_ERR_WR2    0xD3//io error

u8 sd_disk_init();
u8 sd_disk_read_to_ram(u32 sd_addr, void *dst, u16 slen);
u8 sd_disk_read_to_rom(u32 sd_addr, u32 dst, u16 slen);
u8 sd_disk_read(void *dst, u32 saddr, u32 slen);
u8 sd_disk_write(void *src, u32 saddr, u32 slen);
u8 sd_disk_close_rw();
u8 diskStop();


#endif	/* DISK_H */
