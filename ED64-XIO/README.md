# EverDrive-64 programming samples
FPGA firmware included in ED64 Menu OS version 3.04 or newer required to use this sample ROM.

## Developer Documentation
 [Table of Contents](/../../docs/table_of_contents.md)

## Features:
* Simple file manager and game loading from disk
* Use files with FatFs lib
* USB communications
* ED64 hardware version identification

## Building:
The sample currently relies on LibDragon, however its dependency is minimal and the source can be modified to use libUltra instead.

### With Docker
An example of how to build the sample using Docker can be found in the [build-ed64-example-rom workflow] (../../.github/workflows/build-ed64-example-rom.yml) file.

### With Makefile
A Makefile is included, but might need to be modified for your environment.
