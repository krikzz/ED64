ROOTDIR = E:/devtools/mips64
GCCN64PREFIX =	$(ROOTDIR)/bin/mips64-elf-
CHKSUM64PATH =	$(ROOTDIR)/bin/chksum64
MKDFSPATH =	$(ROOTDIR)/bin/mkdfs
N64TOOL =	$(ROOTDIR)/bin/n64tool
FFDIR		:=ff
SRCDIR		:=src
INCDIR		:=inc
RESDIR		:=res
ROMFIX64 =	./tools/romfix64
LINK_FLAGS =	-L$(ROOTDIR)/lib -L$(ROOTDIR)/mips64-elf/lib -ldragon -lc -lm -lnosys -T $(RESDIR)/ed64.ld
PROG_NAME =	ED64-XIO-SAMPLE
FLAGS =		-std=gnu99 -march=vr4300 -mtune=vr4300 -O2 -Wall -Wno-pointer-sign -I$(INCDIR) -I$(ROOTDIR)/include -I$(ROOTDIR)/mips64-elf/include -I$(ROOTDIR)/libdragon/include -Iedlib -I. -I$(FFDIR) -G0 
FLAGS_VT =	-std=gnu99 -march=vr4300 -mtune=vr4300 -O0 -Wall -Wno-pointer-sign -I$(INCDIR) -I$(ROOTDIR)/include -I$(ROOTDIR)/mips64-elf/include -I$(ROOTDIR)/libdragon/include -Iedlib -I. -I$(FFDIR)
ASFLAGS =	-mtune=vr4300 -march=vr4300
CC = $(GCCN64PREFIX)gcc
AS = $(GCCN64PREFIX)as
LD = $(GCCN64PREFIX)ld
OBJCOPY = $(GCCN64PREFIX)objcopy


BDIR		:=build
VPATH		:= $(BDIR)

MAKEFLAGS= -j8


SRC = $(wildcard $(SRCDIR)/*.c) $(wildcard $(FFDIR)/*.c)
SRCS = $(wildcard $(SRCDIR)/*.s) 
OBJ = $(SRC:.c=.o)
OBJ += $(SRCS:.s=.o)
OFILES = $(OBJ:%.o=build/%.o)


all: $(PROG_NAME).v64


$(PROG_NAME).v64: $(PROG_NAME).elf
	$(OBJCOPY) $(BDIR)/$(PROG_NAME).elf $(BDIR)/$(PROG_NAME).bin -O binary
	$(RM) -f $(PROG_NAME).v64
	$(N64TOOL) -l 256K -t "ED64-XIO-SAMPLE" -h $(RESDIR)/header -o $(PROG_NAME).v64 $(BDIR)/$(PROG_NAME).bin -s
	$(ROMFIX64) $(PROG_NAME).v64

$(PROG_NAME).elf : $(OBJ)
	$(LD) -o $(BDIR)/$(PROG_NAME).elf $(OFILES) $(LINK_FLAGS)

%.o: %.c
	$(CC) $(FLAGS) -c $< -o $(BDIR)/$@

%.o: %.s
	$(AS) $(ASFLAGS) $< -o $(BDIR)/$@

clean:
	$(RM) -f *.v64 *.elf *.o
	$(RM) $(BDIR)/*.o
	$(RM) $(BDIR)/*.elf
	$(RM) $(BDIR)/*.bin
	$(RM) $(BDIR)/$(FFDIR)/*.o