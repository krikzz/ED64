# ROM configuration database:

## Developer override
Within the ROM header:
A ROM ID (alphanumeric characters at bytes `0x3C` and `0x3D`) `ED` along with specific config loaded the byte at offset `0x3F` (see below) will override the save database built into the ED64 menu.

On Windows, the byte `0x3F` uses the save type at high nibble, and the extra config option at low nibble.

| ROM ID | = | SAVE TYPE | CONFIG | DEVELOPER OVERRIDE | DESCRIPTION           |
|:-      |---|---        |---     |---                 |---                    |
|ED      | = | 3         | 1      | `0x31`             | 64DD game SRAM+RTC    |
|ED      | = | 1         | 2      | `0x12`             | EEP4K+region-free     |
|ED      | = | 1         | 2      | `0x13`             | EEP4K+region-free+RTC |


## Usage
The ROM ID or CRC HI can be used for game detection (check "ROM Info" from the Everdrive OS menu for the value needed).

The line is depicted in this order:
| ROM ID or CRC-HIGH | = | SAVE TYPE | CONFIG | DESCRIPTION                |
|:-                  |---|---        |---     |---                         |
|DD                  | = | 3         | 1      | All 64DD games SRAM+RTC    |
|0xABA51D09          | = | 1         | 2      | 40 Winks EEP4K+region-free |


Upper records have priority over records below. 

| ID | save type  |
|:-  |---         |
| 0  | OFF        |
| 1  | EEPROM 4k  |
| 2  | EEPROM 16K |
| 3  | SRAM       |
| 4  | SRAM 768K  |
| 5  | FLASHRAM   |
| 6  | SRAM 128K  |


Note: The CONFIG options can be mixed using addition (1+2=3 for rtc+region).
| ID | config          | Region? |
|:-  |---              | ---     |
| 0  | Off             |         |
| 1  | Rtc             |         |
| 2  | Region free ROM | Use native system region for game launch. For applications without region lock. |
| 3  | All             | Rtc and native system region are enabled. |


### Sample
An example `save_db.txt` file would look like:
```
------------------------ CRC detection ------------------------------ 
0xCE84793D=30 	Donkey Kong [f2]. CRC detection. SRAM
0x4CBC3B56=31	64DD DMTJ SRAM+RTC
0xABA51D09=12	40 Winks EEP4K+region-free
0xFA5A3DFF=02	Clay Fighter
0xbcb1f89f=10	kirby-1.3
0x46039FB4=20	kirby-U
0x0D93BA11=20	kirby-U
------------------------ ID detection ------------------------------ 
N6=10		Dr. Mario. ROM ID detection EEP4K
AF=51		Doubutsu no Mori FLASHRAM+RTC
DD=31		All 64DD games SRAM+RTC
```
