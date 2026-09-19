/*
 * Our tests for the bit functions, which hbtest does not cover. EasiPOS
 * calls hb_bitAnd 624 times, hb_bitOr 272, hb_bitNot 220 and hb_bitShift
 * 5, mostly to test and set flag masks. Written as hbtest writes its
 * assertions; Harbour runs them first, so every expected value here is
 * Harbour's own.
 */

#include "rt_main.ch"

PROCEDURE Main_BITS()

   /* hb_bitAnd: variadic, each operand an integer */
   HBTEST hb_bitAnd( 12, 10 )                    IS 8
   HBTEST hb_bitAnd( 255, 15, 6 )                IS 6
   HBTEST hb_bitAnd( -1, 1234 )                  IS 1234
   HBTEST hb_bitAnd( 0x0F0F, 0x00FF )            IS 15
   HBTEST hb_bitAnd( 5, 0 )                      IS 0
   HBTEST hb_bitAnd( 0x80000000, 0xFFFFFFFF )    IS 2147483648
   HBTEST hb_bitAnd( 6, 4 ) == 4                 IS .T.
   HBTEST hb_bitAnd( 7.9, 3 )                    IS 3

   /* hb_bitOr */
   HBTEST hb_bitOr( 12, 10 )                     IS 14
   HBTEST hb_bitOr( 1, 2, 4 )                    IS 7
   HBTEST hb_bitOr( 0, 0 )                       IS 0
   HBTEST hb_bitOr( -8, 1 )                      IS -7
   HBTEST hb_bitOr( 0x100, 0x001 )               IS 257

   /* hb_bitNot */
   HBTEST hb_bitNot( 0 )                         IS -1
   HBTEST hb_bitNot( 5 )                         IS -6
   HBTEST hb_bitNot( -1 )                        IS 0
   HBTEST hb_bitAnd( 0xFF, hb_bitNot( 0x0F ) )   IS 240

   /* hb_bitShift: left for a positive count, right for a negative one */
   HBTEST hb_bitShift( 1, 4 )                    IS 16
   HBTEST hb_bitShift( 256, -4 )                 IS 16
   HBTEST hb_bitShift( 5, 0 )                    IS 5
   HBTEST hb_bitShift( -16, -2 )                 IS -4
   HBTEST hb_bitShift( 1, 31 )                   IS 2147483648

   RETURN
