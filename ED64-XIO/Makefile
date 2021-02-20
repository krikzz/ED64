RM = rm -rf

ROOTDIR = $(N64_INST)
GCCN64PREFIX = $(ROOTDIR)/bin/mips64-elf-
CHKSUM64PATH = $(ROOTDIR)/bin/chksum64
MKDFSPATH = $(ROOTDIR)/bin/mkdfs
N64TOOL = $(ROOTDIR)/bin/n64tool

FFDIR := ff
SRCDIR := src
INCDIR := inc
RESDIR := res
OBJDIR = obj
BINDIR = bin

PROG_NAME =	ED64-XIO-SAMPLE

HDDIR :=$(ROOTDIR)/mips64-elf/lib
HEADERNAME = header
HEADERTITLE = $(PROG_NAME)

CC = $(GCCN64PREFIX)gcc
AS = $(GCCN64PREFIX)as
LD = $(GCCN64PREFIX)ld
OBJCOPY = $(GCCN64PREFIX)objcopy

LINK_FLAGS = -L$(ROOTDIR)/lib -L$(ROOTDIR)/mips64-elf/lib -ldragon -lc -lm -ldragonsys -Tn64.ld

FLAGS = -std=gnu99 -march=vr4300 -mtune=vr4300 -O2 -Wall -Wno-pointer-sign -I$(ROOTDIR)/mips64-elf/include -Iedlib -I$(FFDIR) -I$(INCDIR) -G0 
ASFLAGS = -mtune=vr4300 -march=vr4300

SOURCES_FF := $(wildcard $(FFDIR)/*.c)
SOURCES_S := $(wildcard $(SRCDIR)/*.s)
SOURCES := $(wildcard $(SRCDIR)/*.c)

OBJECTS_FF := $(SOURCES_FF:$(FFDIR)/%.c=$(OBJDIR)/%.o)
OBJECTS_S := $(SOURCES_S:$(SRCDIR)/%.s=$(OBJDIR)/%.o)
OBJECTS := $(SOURCES:$(SRCDIR)/%.c=$(OBJDIR)/%.o)


$(PROG_NAME).v64: $ $(PROG_NAME).elf
	$(OBJCOPY) $(BINDIR)/$(PROG_NAME).elf $(BINDIR)/$(PROG_NAME).bin -O binary
	$(N64TOOL) -l 256K -t $(HEADERTITLE) -h $(HDDIR)/$(HEADERNAME) -o $(BINDIR)/$(PROG_NAME).v64 $(BINDIR)/$(PROG_NAME).bin
	$(CHKSUM64PATH) $(BINDIR)/$(PROG_NAME).v64

$(PROG_NAME).elf : $(OBJECTS_FF) $(OBJECTS_S) $(OBJECTS)
	$(LD) -o $(BINDIR)/$(PROG_NAME).elf $(OBJECTS_FF) $(OBJECTS_S) $(OBJECTS) $(LINK_FLAGS)

$(OBJECTS_FF): $(OBJDIR)/%.o : $(FFDIR)/%.c
	$(CC) $(FLAGS) -c $< -o $@

$(OBJECTS_S): $(OBJDIR)/%.o : $(SRCDIR)/%.s
	$(CC) $(ASFLAGS) -c $< -o $@
	
$(OBJECTS): $(OBJDIR)/%.o : $(SRCDIR)/%.c
	$(CC) $(FLAGS) -c $< -o $@

all: $(PROG_NAME).v64

clean:
	$(RM) $(BINDIR)/*.v64 
	$(RM) $(BINDIR)/*.elf 
	$(RM) $(OBJDIR)/*.o 
	$(RM) $(BINDIR)/*.bin 
	$(RM) $(BINDIR)/*.dfs