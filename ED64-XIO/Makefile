PROG_NAME =	ED64-XIO-SAMPLE
ROMSIZE = 256K


ifdef SystemRoot
FIXPATH = $(subst /,\,$1)
RM = DEL /Q
else
FIXPATH = $1
RM = rm -f
endif

ROOTDIR = $(N64_INST)
GCCN64PREFIX = $(ROOTDIR)/bin/mips64-elf-
CHKSUM64PATH = $(ROOTDIR)/bin/chksum64
MKDFSPATH = $(ROOTDIR)/bin/mkdfs
N64TOOL = $(ROOTDIR)/bin/n64tool

CC = $(GCCN64PREFIX)gcc
AS = $(GCCN64PREFIX)as
LD = $(GCCN64PREFIX)ld
OBJCOPY = $(GCCN64PREFIX)objcopy

SRCDIR = src
SRCDIR_EDLIB = $(SRCDIR)/edlib
SRCDIR_FFLIB = $(SRCDIR)/ff
LIBDIR = lib
INCDIR = inc
INCDIR_EDLIB = inc/edlib
RESDIR = res
OBJDIR = obj
BINDIR = bin

HEADERDIR :=$(ROOTDIR)/mips64-elf/lib
HEADERNAME = header
HEADERTITLE = $(PROG_NAME)

LINK_FLAGS = -L$(ROOTDIR)/lib -L$(ROOTDIR)/mips64-elf/lib -ldragon -lc -lm -ldragonsys -Tn64.ld

ASFLAGS := -mtune=vr4300 -march=vr4300
CCFLAGS := -std=gnu99 $(ASFLAGS) $(OPTIMIZE_FLAG) -Wall -Wno-pointer-sign -I$(ROOTDIR)/mips64-elf/include -I$(SRCDIR_FFLIB) -I$(INCDIR) -I$(INCDIR_EDLIB)

SOURCES_ASM := $(wildcard $(SRCDIR)/*.s)
SOURCES := $(wildcard $(SRCDIR)/*.c)
SOURCES_EDLIB := $(wildcard $(SRCDIR_EDLIB)/*.c)
SOURCES_FFLIB := $(wildcard $(SRCDIR_FFLIB)/*.c)

OBJECTS_ASM := $(SOURCES_ASM:$(SRCDIR)/%.s=$(OBJDIR)/%.o)
OBJECTS := $(SOURCES:$(SRCDIR)/%.c=$(OBJDIR)/%.o)
OBJECTS_EDLIB := $(SOURCES_EDLIB:$(SRCDIR_EDLIB)/%.c=$(OBJDIR)/%.o)
OBJECTS_FFLIB := $(SOURCES_FFLIB:$(SRCDIR_FFLIB)/%.c=$(OBJDIR)/%.o)


$(PROG_NAME).v64: $ $(PROG_NAME).elf
	$(OBJCOPY) $(BINDIR)/$(PROG_NAME).elf $(BINDIR)/$(PROG_NAME).bin -O binary
	$(N64TOOL) -l $(ROMSIZE) -t $(HEADERTITLE) -h $(HEADERDIR)/$(HEADERNAME) -o $(BINDIR)/$(PROG_NAME).v64 $(BINDIR)/$(PROG_NAME).bin
	$(CHKSUM64PATH) $(BINDIR)/$(PROG_NAME).v64

$(PROG_NAME).elf : $(OBJECTS_ASM) $(OBJECTS) $(OBJECTS_EDLIB) $(OBJECTS_FFLIB)
	$(LD) -o $(BINDIR)/$(PROG_NAME).elf $(OBJECTS_ASM) $(OBJECTS) $(OBJECTS_EDLIB) $(OBJECTS_FFLIB) $(LINK_FLAGS)

$(OBJECTS_ASM): $(OBJDIR)/%.o : $(SRCDIR)/%.s
	$(CC) $(ASFLAGS) -c $< -o $@
	
$(OBJECTS): $(OBJDIR)/%.o : $(SRCDIR)/%.c
	$(CC) $(CCFLAGS) -c $< -o $@

$(OBJECTS_EDLIB): $(OBJDIR)/%.o : $(SRCDIR_EDLIB)/%.c
	$(CC) $(CCFLAGS) -c $< -o $@

$(OBJECTS_FFLIB): $(OBJDIR)/%.o : $(SRCDIR_FFLIB)/%.c
	$(CC) $(CCFLAGS) -c $< -o $@

release: BINDIR := bin/release
release: OPTIMIZE_FLAG = -O2 -g0
release: $(PROG_NAME).v64

debug: BINDIR := bin/debug
debug: OPTIMIZE_FLAG = -O0 -DDEBUG -g
debug: $(PROG_NAME).v64

all: $(release)

clean:
	$(info "Cleaning $(PROG_NAME) build folders...")
	@$(RM) $(call FIXPATH,$(BINDIR)/**/*)
	@$(RM) $(call FIXPATH,$(OBJDIR)/*.o)