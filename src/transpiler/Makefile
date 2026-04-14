ROOT := ../../

#YACC_FLAGS := -p hb_comp
YACC_SOURCE := harbour.y
YACC_DEPEND := complex.c
YACC_HEADERS := \
   hbcomp.h \
   hbcompdf.h \
   hbsetup.h \
   hbpcode.h \
   hbdefs.h \
   hberrors.h \
   hbpp.h \

# Transpiler-specific source files (new or modified)
C_SOURCES := \
   cmdcheck.c \
   complex.c \
   expropta.c \
   genhb.c \
   genstubs.c \
   hbast.c \
   hbclsparse.c \
   hbcomp.c \
   hbmain.c \
   hbtypes.c \
   ppcomp.c \

# Identical files from ../compiler/ (not modified for transpiler)
C_SOURCES += \
   ../compiler/compi18n.c \
   ../compiler/exproptb.c \
   ../compiler/hbdbginf.c \
   ../compiler/hbdead.c \
   ../compiler/hbfix.c \
   ../compiler/hbfunchk.c \
   ../compiler/hbgenerr.c \
   ../compiler/hbident.c \
   ../compiler/hblbl.c \
   ../compiler/hbopt.c \
   ../compiler/hbpcode.c \
   ../compiler/hbstripl.c \
   ../compiler/hbusage.c \

LIBNAME := hbcplr

include $(TOP)$(ROOT)config/lib.mk
