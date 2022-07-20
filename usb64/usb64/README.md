# PC Program for communicating with the ED64 via USB

## supported commands:
### FPGA
Loads an RBF file into the ED64's fpga
Arg: `-fpga=<filename>`


### Transfer ROM
Writes a (ROM) file to ED64's volatile memory
Arg: `-rom=<filename>`


### Start ROM
Starts the ROM written to the ED64's volatile memory
Arg: `-start`
Arg (Advanced): `-start[=<filename>]` (Only required when different from `-rom=<filename>` or using the command after the initial console session).	 


### Perform diagnostics
Compares that a write and read from the ED64's volatile memory matches.
Arg: `-diag`


### Dump ROM
Dumps the loaded ROM from the ED64's volatile memory
Arg: `-drom=<filename>`


### Take screenshot
Generates an image from the frame buffer (in bitmap format)
Arg `-screen=<filename>`

### Transfer File
Transfers a file between devices
Arg `-cp <sourceFilePath> <destinationFilePath>


### Developer options:

#### Force ROM load
Loads specified ROM, even though it is not of a known type (e.g. 64dd).
Arg: `-forcerom=<filename>` ();

#### Save Type
Runs the ROM with a save type when not matched in the internal database
 Arg: `-save=<savetype>`
 Options: `[None,Eeprom4k,Eeprom16k,Sram,Sram768k,FlashRam,Sram128k]`

 #### Extra Info
 Runs the ROM with RTC or forced region
Arg: `-extra=<RTC-RegionType>`
Options: `[Off,Rtc,NoRegion,All]`

#### Debug
Runs the unf Debugger Console session
Arg: `-unfdebug`