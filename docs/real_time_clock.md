# Real Time Clock

## Setting the RTC

```
#define REG_RTC_SET     0x8010
```

WIP! (Krikzz input required)

## Getting the RTC
The ED64 uses the same calls as used by other ROMs that need the RTC on the N64.
When emulating a ROM that uses the RTC, an entry needs to be present in the default database, or ED64 folder called `save_db.txt` (there by default, although user configurable).

An example for the 64DD would be a line entry:
`DD=31		All 64DD ROMs SRAM+RTC` which ensures that the RTC is enabled and that the save type is SRAM.

Please refer to the [Config Database](rom_config_database.md) for more information.
