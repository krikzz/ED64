# Real Time Clock

WIP!

## Setting the RTC

```
#define REG_RTC_SET     0x8010
```

## Getting the RTC
The ED64 uses the same calls as used by other games that need the RTC on the N64.
When emulating a game that uses the RTC, an entry needs to be present in the ED64 folder called `save_db.txt` (there by default, although user configurable).

An example for the 64DD would be a line entry:
`DD=31		All 64DD games SRAM+RTC` which ensures that the RTC is enabled and that the save type is SRAM.


