// W0035: a hash used with keys its name does not declare (Alex,
// 2026-09-27). h<...> is keyed by strings, hn<...> by numbers, and C#
// commits the dictionary to the name's key type. Each kind of
// contradiction has its own wording, so run.py checks each; the lines
// marked "quiet" must not warn.
// W0048: hc<...> says strings as hn<...> says numbers (Alex,
// 2026-11-18); a bare h<...> does not say, and is taken as strings.

PROCEDURE Main()

   LOCAL hByName := { => }
   LOCAL hnById := { => }
   LOCAL hWrong := { 1 => "one" }       // fires: given a hash keyed by numbers
   LOCAL hcByCode := { => }             // quiet: the name says strings
   LOCAL nId := 3

   hByName[ nId ] := "three"            // fires: a number as an h name's key
   hnById[ "three" ] := 3               // fires: a string as an hn name's key
   hByName[ "three" ] := 3              // quiet
   hnById[ nId ] := "three"             // quiet
   hcByCode[ "three" ] := 3             // quiet
   hcByCode[ nId ] := "three"           // fires: a number as an hc name's key
   ? hByName, hnById, hWrong, hcByCode

   RETURN
