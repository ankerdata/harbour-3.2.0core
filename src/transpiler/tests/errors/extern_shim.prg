// --extern= (plan B12): extern_shim.tab describes NetShim47, a .NET class
// the program reaches (in EasiPOS, a COM shim's .NET build). The static
// names the class, so it is declared as its C# type, Test47.NetShim47,
// and its calls are typed: `@` into an out parameter is `out`, through
// the reference shim's temporary when the local's type is another; an
// omitted out is `out _`; a method's return type types the result. The
// table's rows are never saved into the reftab.

static soNetShim47

procedure ExternShim47()

   local cName
   local nSize
   local lReady

   soNetShim47:Fill( @cName, @nSize )
   soNetShim47:Fill( @cName )
   lReady := soNetShim47:Ready( "probe" )
   ? cName, nSize, lReady

return
