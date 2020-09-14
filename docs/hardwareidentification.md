#Identifying an ED64 hardware revision

Note: Cartridge menu OS versions prior to V3.04 will not work with this method.
Note: There is no current way to know the cartridge revision over USB within the OS menu.
Note: There is no current way to know the cartridge OS version within ROM or over USB within the OS menu.

## Pseudo code

The following registers are required:
```
#define REGISTER_BASE                       0x1F800000
#define REGISTER_ED64_HARDWARE_REVISION     0x0014
```

The ED64 Hardware revision can be identified using the following values
```
#define ED64_HARDWARE_REVISION_V2      0xED640007
#define ED64_HARDWARE_REVISION_V3      0xED640008
#define ED64_HARDWARE_REVISION_X7      0xED640013
#define ED64_HARDWARE_REVISION_X5      0xED640014

```

This pseudo code shows you how it can be read by getting them from the Peripheral Interface:
```
    unsigned long register_read(unsigned short register)
    {       
        return systemPI_read(&val, REGISTER_ADDRESS(register), 4);
    }

    unsigned long get_ed64_hardware_revision()
    {
    return register_read(REGISTER_ED64_HARDWARE_REVISION);
    }

```

This pseudo code shows how you can handle the hardware revision:
```
    unsigned long type = get_ed64_hardware_revision();
    switch (type) {
        case ED64_HARDWARE_REVISION_V2: //wont work with current hardware due to OS not on V3x
            printf("EverDrive 64 V2.5 detected.");
            break;
        case ED64_HARDWARE_REVISION_V3:
            printf("EverDrive 64 V3 detected.");
            break;
        case ED64_HARDWARE_REVISION_X7:
            printf("EverDrive 64 X7 detected.");
            break;
        case ED64_HARDWARE_REVISION_X5: // for future hardware revision (not yet released)
            printf("EverDrive 64 X5 detected.");
            break;
        default:
            printf("Unknown hardware revision detected.");
            break;
    }
```
