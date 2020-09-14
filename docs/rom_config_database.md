#ROM configuration database:
An ID reserved for developers 'ED' With this ID cart will load config from ROM header (one byte at offset 0x3F)


The line is depicted in this order
* First number for save type
* Second number for game config.
Note: The config options can be mixed using addition (1+2=3 for rtc+region).

## Usage
ROM ID or CRC HI can be used for game detection. (check "ROM Info" menu for details). Identifiers should be entered without spaces.

Upper records have priority over records below. 

| ID | save type |
|:-|---|
| 0 | OFF |
| 1 | EEPROM 4k |
| 2 | EEPROM 16K |
| 3 | SRAM |
| 4 | SRAM 768K |
| 5 | FLASHRAM |
| 6 | SRAM 128K |


| ID | config |
|:-|---|
| 0 | OFF |
| 1 | Force RTC |
| 2 | Region free ROM. Use native system region for game launch. For applications without region lock |


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
