using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// The Error object — a stopgap until family 9 gives C# its error handler.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Harbour Error object (stopgap) ----
    //
    // This class exists purely to let transpiled code that builds and
    // inspects Harbour Error objects compile. It mimics the shape of the
    // canonical Error class (the fields that createerror.prg and callers
    // poke at) but has no real semantics — no error propagation, no RTE
    // integration, no Try/SEQUENCE wiring. A real C# port would throw
    // structured exceptions or use Result<T,E> instead; hanging properties
    // off a POCO is just the path of least resistance to get past 1061s
    // in the classlib build. Expect this whole class to be deleted once
    // the idiomatic story is figured out.

    // Derives from HbDynamicObject so a message in any casing reaches its
    // member, as Harbour's messages are case-insensitive: EasiPOS writes
    // oError:description, hbtest o:oscode and oError:Description, and a
    // plain class threw RuntimeBinderException for all but the exact
    // spelling.
    public class HbError : HbDynamicObject
    {
        public decimal severity    { get; set; }
        public decimal genCode     { get; set; }
        public string  subSystem   { get; set; } = "";
        public decimal subCode     { get; set; }
        public string  description { get; set; } = "";
        public bool    canRetry    { get; set; }
        public bool    canDefault  { get; set; }
        public string  fileName    { get; set; } = "";
        public decimal osCode      { get; set; }
        public dynamic[] args      { get; set; } = System.Array.Empty<dynamic>();
        public string  operation   { get; set; } = "";
        public dynamic tries       { get; set; }
        public string classname() => "ERROR";
    }

    public static dynamic ErrorNew() => new HbError();
    /* Object `.classname()` fallback for arbitrary objects — Harbour allows
       it on anything. Real port would use `GetType().Name`. */
    public static string CLASSNAME(dynamic o) =>
        o is HbError he ? he.classname() : (o?.GetType().Name ?? "NIL");
}
