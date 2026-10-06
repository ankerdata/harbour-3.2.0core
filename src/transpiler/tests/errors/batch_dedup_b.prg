// A warning at the same line of two files of one batch (one process,
// several files, as the pipeline runs them) is reported for each. The
// table that prints a warning once per line and name lived as long as the
// process, so the second file's went unsaid. batch_dedup_b.prg is this
// file under another name.

PROCEDURE Main()

   LOCAL hByName := { => }
   LOCAL nId := 3

   hByName[ nId ] := "three"            // W0035: a number as an h name's key
   ? hByName

   RETURN
