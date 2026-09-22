// Regression: a THREAD STATIC initialised to anything but NIL, 0 or .F. -
// C#'s [ThreadStatic] runs the initializer on the first thread only, so
// another thread would start with the C# default (W0034).

THREAD STATIC scName := "main"

PROCEDURE Main()

   ? scName

   RETURN
