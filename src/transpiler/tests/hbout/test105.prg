#include "astype.ch"
// Test 105: C# written in the .prg — `#pragma BEGINCSHARP` /
// `#pragma ENDCSHARP`.
//
// Some Harbour code can never become C# by translation (the
// `__objGetValueList` / HB_OO_DATA_* reflection in posclass.prg), and
// 9.0 must keep compiling from the same source. So a file can carry
// the C# itself: the preprocessor captures a BEGINCSHARP block as a
// raw stream — nothing inside is lexed as Harbour — and the emitter
// writes it out verbatim at namespace level, wherever in the file it
// stood. A class the block names in `partial class X` is emitted
// `partial` — only that one — so a block can add members to a class
// the file declares (`partial class Torch { … }`), a free
// function to `Program` (`FilamentRate`, called from Harbour code as
// any function is), or declare a whole class of its own. Under Harbour the block sits in
// `#ifdef __HB_TRANSPILER__` and is skipped — the stock preprocessor
// does not open a dump inside a false conditional — while the `#else`
// branch carries the Harbour version. Both programs print the same.
// A block is file-scope by design: it is appended beside the CLASS
// nodes, never inside a function body, so placement in the .prg is
// free and the order of statements around it cannot be disturbed.
// The round-trip emitter writes it back inside the same guard.
// Members added this way are invisible to the reftab: a send to one
// emits as a plain member call (`oTorch.Glow()`), which compiles
// because the partial class supplies it, and a free function call
// (`FilamentRate`) emits bare, resolved by C# against Program.
#include "hbclass.ch"

CLASS Torch

   DATA nWatts AS NUMERIC INIT 60

ENDCLASS

#ifdef __HB_TRANSPILER__
#pragma BEGINCSHARP
public partial class Torch
{
    public string Glow() => "torch:" + nWatts;
}

public static partial class Program
{
    public static string FilamentRate(decimal n) => n > 50 ? "bright" : "dim";
}
#pragma ENDCSHARP
#endif
#ifdef __HB_TRANSPILER__
#pragma BEGINCSHARP
// a second block, placed inside a function on purpose: it is still
// hoisted to namespace level, so this comment lands beside the classes
#pragma ENDCSHARP
#endif
PROCEDURE Main()
   LOCAL oTorch := Torch():New() AS OBJECT
   QOut(oTorch:Glow())

   QOut(FilamentRate(oTorch:nWatts))
RETURN
