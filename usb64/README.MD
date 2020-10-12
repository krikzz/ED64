# PC Program for communicating with the ED64 via USB

## supported commands:
### FPGA
Loads an RBF file into the ED64's fpga
Arg: `-fpga <filename>`


### Transfer ROM
Writes a (ROM) file to ED64's volatile memory
Arg: `-rom=<filename>`


### Start ROM
Starts the ROM written to the ED64's volatile memory
Arg: `-start`		 


### Perform diagnostics
Compares that a write and read from the ED64's volatile memory matches.
Arg: `-diag`


### Dump ROM
Dumps the loaded ROM from the ED64's volatile memory
Arg: `-drom=<filename>`


### Take screenshot
Generates an image from the frame buffer (in bitmap format)
Arg `-screen=<filename>`
