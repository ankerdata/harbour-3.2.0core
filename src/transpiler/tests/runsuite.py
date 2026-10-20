#!/usr/bin/env python3
"""The transpiler test suite: every tests/test*.prg transpiled, built by
Harbour and as C#, run both ways, the outputs compared.

Run runtests.bat, which sets up vcvarsall and the Harbour bin directory
and calls this directly, never through a shell: Git Bash puts /usr/bin
ahead of MSVC on PATH, so GNU link shadows link.exe and hbmk2 dies with
"link: unknown option -- n".

Stages (default "all", which runs them in this order):
  gen   scan (-GF) and regenerate csout/ (-GS); the reftab must come out
        of two scans of the same sources byte for byte
  prg   build the Harbour reference exes into prgexe/
  cs    build HbRuntime and the emitted C# into csexe/
  run   run both, compare the C# output with Harbour's

Builds are incremental: a Harbour exe is rebuilt only when it is older
than its .prg sources or a local .ch they include. The C# tests build as
ONE assembly (csexe/suite): each test's emitted files wrapped in a
namespace named for it, so every test keeps its own Program, Main,
statics and classes, and a generated Suite.cs runs the test named on the
command line. It is rebuilt when any staged file changed or when the
public surface of the runtime assembly (the HbRuntime/ sources plus the
libraries) differs from the one it was built against; a runtime change
that keeps the surface (its reference assembly is byte for byte the same)
copies the new HbRuntime.dll beside it instead. gen always transpiles every test — the suite shares one
reftab, so one test's change can change another's output — in batches of
BATCH files per transpiler process, as the pipeline does; and every
test always runs. --full rebuilds everything: the occasional sweep, and
the answer to a changed Harbour or .NET toolchain, which the
incremental checks cannot see.

--test testNNN[,testNNN...] runs the stages for those tests alone (a
pair member, test45a, names its pair): gen keeps the shared reftab and
scans and emits only their files, so a change to the named
test is seen in seconds. Another test's output can change with it, since
the reftab is shared, so the whole suite still runs once at the end of a
change. Without a reftab yet, gen does the whole suite first.

Usage: runsuite.py [all|gen|prg|cs|run] [--full] [--test testNNN[,...]]

Environment:
  HBTRANSPILER  transpiler binary (default <root>/bin/hbtranspiler.exe)
"""
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor

TESTS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(TESTS, "..", "..", ".."))
CSOUT = os.path.join(TESTS, "csout")
CSEXE = os.path.join(TESTS, "csexe")
PRGEXE = os.path.join(TESTS, "prgexe")
DEFINES = os.path.join(TESTS, "defines")
ORM = os.path.join(TESTS, "orm")
PRELOAD = os.path.join(TESTS, "preload.txt")
REFTAB = os.path.join(ROOT, "src", "transpiler", "hbreftab.tab")
GENDEFINES = os.path.join(ROOT, "src", "transpiler", "tools", "gendefines.py")
TEST_GENDEFINES = os.path.join(ROOT, "src", "transpiler", "tools", "test_gendefines.py")

TRANSPILER = os.environ.get("HBTRANSPILER") or \
    os.path.join(ROOT, "bin", "hbtranspiler.exe")

# Multi-file tests: testNNa + testNNb build as one project named testNN.
PAIRS = ["19", "20", "22", "41", "45", "46", "131", "138"]
PAIRMEMBERS = set()
for _p in PAIRS:
    PAIRMEMBERS |= {"test" + _p + "a", "test" + _p + "b"}

SRC_RE = re.compile(r"^(test\w+)\.prg$")
INCLUDE_RE = re.compile(r'^[ \t]*#[ \t]*include[ \t]+"([^"]+)"', re.I | re.M)

FULL = False                     # --full: rebuild every test
BATCH = 150                      # files per transpiler process in gen (the
                                 # pipeline's bound; bare names keep the
                                 # command line far below Windows' limit)

SUITE = os.path.join(CSEXE, "suite")   # the one project every test's C# builds into

SUITEPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>suite</RootNamespace>
    <AssemblyName>suite</AssemblyName>
    <StartupObject>Suite</StartupObject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../HbRuntime/HbRuntime.csproj" />
  </ItemGroup>
</Project>
"""

LIBPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
"""


def test_names():
    """Every buildable case: singles plus one entry per multi-file pair."""
    names = []
    for fn in sorted(os.listdir(TESTS)):
        m = SRC_RE.match(fn)
        if m and m.group(1) not in PAIRMEMBERS:
            names.append(m.group(1))
    return names + ["test" + p for p in PAIRS]


def sources(name, ext, subdir):
    """Source files for a case, relative to TESTS."""
    if name[4:] in PAIRS:
        cand = [os.path.join(subdir, name + s + ext) for s in ("a", "b")]
    else:
        cand = [os.path.join(subdir, name + ext)]
    return [c for c in cand if os.path.isfile(os.path.join(TESTS, c))]


# --------------------------------------------------------------- gen ----
def stage_gen(only=None):
    """Clear hbreftab.tab, scan every source with -GF, scan AGAIN without
    clearing, and emit -GS into csout/. The second scan sees what the
    first recorded; csout/ was made so, and one scan alone gives false
    diffs (test79 emits Animal vs dynamic).

    The whole suite's first scan is repeated into a throwaway reftab and
    the two must be byte for byte the same: the pipeline's scan loop
    stops when the reftab's md5 stops changing, so a reftab that
    differs between identical scans (an unsorted table) would run every
    scan to its pass limit.

    With `only` (test names, --test) the reftab is kept and the passes
    run over those tests' files alone: their rows replace what the last
    whole-suite scan recorded for them, and the emit reads everything
    else from that scan.
    """
    if only and not os.path.isfile(REFTAB):
        print("gen: no hbreftab.tab yet, the whole suite is scanned first")
        only = None
    if not os.path.isfile(TRANSPILER):
        sys.exit("transpiler not found: %s (set HBTRANSPILER)" % TRANSPILER)
    # gendefines.py's own tests first: gen's defines map rests on its
    # reading of #define values, so a failure there stops the suite before
    # any test's output could mislead.
    r = subprocess.run([sys.executable, TEST_GENDEFINES],
                       capture_output=True, text=True, errors="replace")
    if r.returncode != 0:
        print((r.stdout + r.stderr).strip())
        sys.exit("gen: tools/test_gendefines.py failed")
    os.makedirs(CSOUT, exist_ok=True)

    if only:
        srcs = [os.path.basename(s) for n in only for s in sources(n, ".prg", ".")]
    else:
        srcs = sorted(f for f in os.listdir(TESTS) if SRC_RE.match(f))
    base = [TRANSPILER]
    if os.path.isfile(PRELOAD):
        base.append("--preload-list=" + PRELOAD)
    base += ["-I" + os.path.join(ROOT, "include"), "-I" + TESTS]

    # Every pass's exit code is read: a transpiler failure used to pass
    # unnoticed here and surface, if at all, as a baffling build error or
    # diff later. The negative tests (tests/errors/) are not in srcs, so
    # every file gen touches must transpile cleanly.
    failed = []

    def run(args):
        r = subprocess.run(args, cwd=TESTS, capture_output=True, text=True, errors="replace")
        if r.returncode != 0:
            mode = next((a for a in args if a in ("-GF", "-GS")), "?")
            src = next((a for a in args if SRC_RE.match(os.path.basename(a))), "?")
            text = (r.stdout + r.stderr).strip().splitlines()
            failed.append("   FAIL %-12s %s exit %d: %s"
                          % (src, mode, r.returncode, text[-1] if text else "(no output)"))

    def run_batched(before, files, after):
        """One transpiler process per BATCH files, the file list between
        `before` and `after`, as the pipeline runs its passes (scan.py,
        tc.make_batches): the process's fixed cost (the headers, the
        reftab, the canon set) outweighs a test's own work, and one
        process per file per pass was 483 spawns, about 45 s of the
        suite's 80. A batch that fails is re-run one file at a time, so
        the failure lands on its file with its own message."""
        for i in range(0, len(files), BATCH):
            batch = files[i:i + BATCH]
            if len(batch) == 1:
                run(before + batch + after)
                continue
            r = subprocess.run(before + batch + after, cwd=TESTS,
                               capture_output=True, text=True, errors="replace")
            if r.returncode != 0:
                for f in batch:
                    run(before + [f] + after)

    if not only and os.path.isfile(REFTAB):
        os.remove(REFTAB)
    run_batched(base, srcs, ["-GF", "-q"])
    if not only and not failed:
        fd, again = tempfile.mkstemp(suffix=".tab")
        os.close(fd)
        try:
            run_batched(base + ["--reftab=" + again], srcs, ["-GF", "-q"])
            if not failed and not same_bytes(REFTAB, again):
                failed.append("   FAIL reftab: two scans of the same sources wrote different tables")
        finally:
            os.remove(again)
    run_batched(base, srcs, ["-GF", "-q"])   # deliberately NOT clearing the reftab

    if os.path.isdir(DEFINES):
        shutil.rmtree(DEFINES)
    rc = subprocess.run([sys.executable, GENDEFINES,
                         "--include-dir", TESTS, "--src-dir", TESTS,
                         "--output-dir", DEFINES],
                        capture_output=True).returncode
    if rc != 0:
        sys.exit("gendefines.py failed")

    opts = []
    dmap = os.path.join(DEFINES, "defines_map.txt")
    if os.path.isfile(dmap) and os.path.getsize(dmap):
        opts.append("--defines-map=" + dmap)
    ftypes = os.path.join(ORM, "fieldtypes.tsv")
    if os.path.isfile(ftypes) and os.path.getsize(ftypes):
        opts.append("--fieldtypes=" + ftypes)
    run_batched(base + opts + ["-o" + CSOUT + os.sep], srcs, ["-GS", "-q"])
    print("csout/  regenerated (%s)" %
          (", ".join(srcs) if only else "%d sources" % len(srcs)))
    if failed:
        print("gen: %d transpiler run(s) failed" % len(failed))
        for line in failed:
            print(line)
        sys.exit(1)


# ------------------------------------------------------- incremental ----
def prg_deps(srcs):
    """A case's .prg files and the local .ch files they include, nested
    includes followed; Harbour's own headers are not tracked (--full)."""
    todo = [os.path.join(TESTS, s) for s in srcs]
    seen = []
    while todo:
        p = todo.pop()
        if p in seen or not os.path.isfile(p):
            continue
        seen.append(p)
        with open(p, encoding="latin-1") as fh:
            for inc in INCLUDE_RE.findall(fh.read()):
                todo.append(os.path.join(TESTS, inc))
    return seen


def newest_file(d, fname):
    """Newest copy of fname under d (dotnet's bin/ layout varies with the
    platform vcvarsall sets), or None."""
    best = None
    for dp, _, fns in os.walk(d):
        if fname in fns:
            p = os.path.join(dp, fname)
            if best is None or os.path.getmtime(p) > os.path.getmtime(best):
                best = p
    return best


def same_bytes(a, b):
    if not os.path.isfile(b) or os.path.getsize(a) != os.path.getsize(b):
        return False
    with open(a, "rb") as fa, open(b, "rb") as fb:
        return fa.read() == fb.read()


def stage_files(files, d, ext):
    """Make d's `ext` files exactly `files` ({basename: source path}),
    touching nothing that is already identical. True if anything
    changed."""
    changed = False
    for f in os.listdir(d):
        if f.endswith(ext) and f not in files:
            os.remove(os.path.join(d, f))
            changed = True
    for f, src in files.items():
        if not same_bytes(src, os.path.join(d, f)):
            shutil.copy(src, os.path.join(d, f))
            changed = True
    return changed


def write_if_changed(path, text):
    if os.path.isfile(path):
        with open(path) as fh:
            if fh.read() == text:
                return False
    with open(path, "w") as fh:
        fh.write(text)
    return True


def write_text_if_changed(path, text):
    """UTF-8, the line endings as given: a staged csout file keeps its own,
    and a comment's `→` must not meet the console code page."""
    if os.path.isfile(path):
        with open(path, encoding="utf-8", newline="") as fh:
            if fh.read() == text:
                return False
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)
    return True


# --------------------------------------------------------------- prg ----
def build_prg(name):
    """(name, error or None, built?)"""
    srcs = sources(name, ".prg", ".")
    if not srcs:
        return name, "no source", False
    exe = os.path.join(PRGEXE, name + ".exe")
    if not FULL and os.path.isfile(exe):
        t = os.path.getmtime(exe)
        if all(os.path.getmtime(p) <= t for p in prg_deps(srcs)):
            return name, None, False
    if os.path.isfile(exe):
        os.remove(exe)
    # a test that starts threads needs Harbour's MT VM (in the ST one
    # hb_threadStart() returns NIL), as the RTL harness's _mt tests do
    mt = ["-mt"] if any("hb_threadstart" in open(os.path.join(TESTS, p), encoding="latin-1").read().lower()
                         for p in srcs) else []
    r = subprocess.run(["hbmk2"] + srcs +
                       [os.path.join("-oprgexe", name),
                        "-w", "-es2", "-gtcgi", "-q"] + mt,
                       cwd=TESTS, capture_output=True, text=True)
    if r.returncode == 0 and os.path.isfile(exe):
        return name, None, True
    tail = (r.stdout + r.stderr).strip().splitlines()
    return name, (tail[-1][:120] if tail else "rc=%d" % r.returncode), True


def stage_prg(names):
    os.makedirs(PRGEXE, exist_ok=True)
    res = [build_prg(n) for n in names]
    bad = [(n, e) for n, e, _ in res if e]
    built = sum(1 for _, e, b in res if b and not e)
    print("hbmk2:  %d built, %d up to date, %d failed" %
          (built, len(names) - built - len(bad), len(bad)))
    for n, e in bad:
        print("   FAIL %-12s %s" % (n, e))
    return not bad


# ---------------------------------------------------------------- cs ----
REF_STAMP = ".hbruntime-ref"   # the runtime surface a test was built against


def runtime_surface(lib):
    """A fingerprint of the runtime's public surface: its reference
    assembly, which the compiler writes byte for byte the same while the
    public API is unchanged. None when there is none. The newest one under
    obj/, wherever the platform put it, as newest_file() finds the dll: it
    read obj/Debug/net10.0/ref/ only, which runtests.bat's x86 vcvarsall
    never writes (obj/x86/Debug/...), so it hashed a stale copy, the surface
    never moved, and a runtime whose API changed was copied beside tests
    compiled against the old one (MissingFieldException at run time)."""
    refs = [os.path.join(dp, "HbRuntime.dll")
            for dp, _, fns in os.walk(os.path.join(lib, "obj"))
            if os.path.basename(dp) == "ref" and "HbRuntime.dll" in fns]
    if not refs:
        return None
    with open(max(refs, key=os.path.getmtime), "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def wrapped_source(name, path):
    """A test's emitted C# inside a namespace named for the test, so the
    suite's one assembly holds every test's `Program`, `Main`, statics and
    classes apart (eight class names, Animal, Dog, Person and Widget among
    them, are declared by more than one test), and a test's file statics
    initialise only when its Program is first touched, as in its own
    executable. The emitted C# has no using lines, the program's project
    supplies them, so the namespace opens with HbRuntime's and Program's
    static imports, where `Program` names the test's own (System's
    namespaces are the suite project's implicit usings); a Const class or
    an ORM fixture stays global and is found from inside."""
    with open(path, encoding="utf-8", newline="") as fh:
        text = fh.read()
    return "namespace %s\n{\nusing static HbRuntime;\nusing static Program;\n\n%s%s}\n" % (
        name, text, "" if text.endswith("\n") else "\n")


def dispatcher(names):
    """Suite.cs, the assembly's entry point: it runs the test named on the
    command line, the remaining arguments its Main's, after telling
    HbRuntime which Program is the program's (its own lookup finds a
    global `Program`, and there is none here)."""
    lines = ["// Generated by runsuite.py: one entry point for every test.",
             "public static class Suite",
             "{",
             "    public static int Main(string[] args)",
             "    {",
             "        if (args.Length == 0)",
             "        {",
             "            System.Console.Error.WriteLine(\"usage: suite <testNNN> [args]\");",
             "            return 2;",
             "        }",
             "        string[] rest = args[1..];",
             "        switch (args[0])",
             "        {"]
    for n in names:
        lines += ["            case \"%s\":" % n,
                  "                HbRuntime.SetProgramType(typeof(%s.Program));" % n,
                  "                %s.Program.Main(rest);" % n,
                  "                return 0;"]
    lines += ["            default:",
              "                System.Console.Error.WriteLine(\"suite: no test named \" + args[0]);",
              "                return 2;",
              "        }",
              "    }",
              "}",
              ""]
    return "\n".join(lines)


def stage_suite(lib_dll, surface):
    """(errors or None, built?). Every test's C# is staged, wrapped, into
    the one project, with the Const classes, the ORM fixtures and the
    dispatcher. It is built when anything staged changed, or when the
    runtime's public surface moved since the last build (REF_STAMP, the
    reference assembly's hash). When only the runtime changed and its
    surface did not, the new HbRuntime.dll is copied beside the assembly
    instead: nothing it compiled against moved, so a build would only
    have copied it too. (Not "newer than the runtime": MSBuild copies a
    new HbRuntime.dll but leaves the assembly alone when the runtime's
    public API did not change.) Until 2026-10-15 each test was a project
    of its own: 153 builds after a change to the runtime's surface (228 s),
    a dotnet host per test to run, 228 MB of bin and obj."""
    os.makedirs(SUITE, exist_ok=True)
    names = test_names()
    changed = False
    keep = {"Suite.cs"}
    for n in names:
        for s in sources(n, ".cs", "csout"):
            base = os.path.basename(s)
            keep.add(base)
            changed |= write_text_if_changed(os.path.join(SUITE, base),
                                             wrapped_source(n, os.path.join(TESTS, s)))
    for sub in (DEFINES, ORM):             # Const classes + ORM fixtures, global
        if os.path.isdir(sub):
            for f in os.listdir(sub):
                if f.endswith(".cs"):
                    keep.add(f)
                    if not same_bytes(os.path.join(sub, f), os.path.join(SUITE, f)):
                        shutil.copy(os.path.join(sub, f), os.path.join(SUITE, f))
                        changed = True
    changed |= write_text_if_changed(os.path.join(SUITE, "Suite.cs"), dispatcher(names))
    for f in os.listdir(SUITE):
        if f.endswith(".cs") and f not in keep:
            os.remove(os.path.join(SUITE, f))
            changed = True
    changed |= write_if_changed(os.path.join(SUITE, "suite.csproj"), SUITEPROJ)
    dll = newest_file(os.path.join(SUITE, "bin"), "suite.dll")
    rt = newest_file(os.path.join(SUITE, "bin"), "HbRuntime.dll")
    stamp = os.path.join(SUITE, REF_STAMP)
    stamped = bool(surface) and os.path.isfile(stamp) and \
        open(stamp, encoding="ascii").read().strip() == surface
    if not FULL and not changed and dll and rt:
        # the current runtime beside the assembly is not enough: it may have
        # been copied there by a run that misread the surface, beside an
        # assembly compiled against the old one, so the stamp must agree too
        if same_bytes(lib_dll, rt) and (stamped or not surface):
            return None, False
        if stamped:
            shutil.copy2(lib_dll, rt)
            lib_pdb, rt_pdb = lib_dll[:-4] + ".pdb", rt[:-4] + ".pdb"
            if os.path.isfile(lib_pdb):
                shutil.copy2(lib_pdb, rt_pdb)
            return None, False
    r = subprocess.run(["dotnet", "build", "--no-dependencies", "-v", "q",
                        "--nologo"], cwd=SUITE, capture_output=True, text=True)
    if r.returncode == 0 and "error CS" not in r.stdout:
        if surface:
            with open(stamp, "w", encoding="ascii") as fh:
                fh.write(surface + "\n")
        return None, True
    if dll and os.path.isfile(dll):        # never skip a failed build later
        os.remove(dll)
    errs = [l.strip() for l in r.stdout.splitlines() if "error CS" in l]
    return (errs[:8] if errs else ["rc=%d" % r.returncode]), True


def hbruntime_sources():
    """{basename: path} of the HbRuntime sources — one partial class,
    split by category across src/transpiler/HbRuntime/. Empty is fatal: a
    runtime built from none of them would fail every test for the wrong
    reason."""
    d = os.path.join(ROOT, "src", "transpiler", "HbRuntime")
    files = {f: os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith(".cs")} \
        if os.path.isdir(d) else {}
    if not files:
        sys.exit("no HbRuntime sources in %s" % d)
    return files


def stage_cs():
    """HbRuntime is built first, on its own, when its sources changed, so a
    runtime that fails to build fails the stage with its own error; then
    the suite's one assembly (stage_suite), which references it."""
    lib = os.path.join(CSEXE, "HbRuntime")
    os.makedirs(lib, exist_ok=True)
    files = hbruntime_sources()
    # The contrib libraries (src/transpiler/libraries/<lib>/*.cs) compile
    # into the same test runtime assembly: a test calling HbWin.wapi_Sleep
    # or Xhb.TOleAuto finds them without a reference per library.
    libs = os.path.join(ROOT, "src", "transpiler", "libraries")
    if os.path.isdir(libs):
        for sub in sorted(os.listdir(libs)):
            d = os.path.join(libs, sub)
            if os.path.isdir(d):
                for f in os.listdir(d):
                    if f.endswith(".cs"):
                        files[f] = os.path.join(d, f)
    changed = stage_files(files, lib, ".cs")
    changed |= write_if_changed(os.path.join(lib, "HbRuntime.csproj"), LIBPROJ)
    lib_dll = newest_file(os.path.join(lib, "bin"), "HbRuntime.dll")
    if FULL or changed or not lib_dll:
        r = subprocess.run(["dotnet", "build", "-v", "q", "--nologo"],
                           cwd=lib, capture_output=True, text=True)
        lib_dll = newest_file(os.path.join(lib, "bin"), "HbRuntime.dll")
        if r.returncode != 0 or not lib_dll:
            errs = [l.strip() for l in r.stdout.splitlines() if "error CS" in l]
            print("dotnet: HbRuntime failed: %s" %
                  (errs[0][:120] if errs else "rc=%d" % r.returncode))
            if lib_dll:
                os.remove(lib_dll)
            return False
        print("dotnet: HbRuntime rebuilt")
    surface = runtime_surface(lib)
    errs, built = stage_suite(lib_dll, surface)
    removed = 0                            # the old layout's per-test project trees
    for d in os.listdir(CSEXE):
        p = os.path.join(CSEXE, d)
        if os.path.isdir(p) and SRC_RE.match(d + ".prg"):
            shutil.rmtree(p)
            removed += 1
    if removed:
        print("dotnet: %d per-test project trees removed (one assembly now)" % removed)
    if errs:
        print("dotnet: the suite's assembly failed to build")
        for line in errs:
            print("   " + line[:160])
        return False
    print("dotnet: suite %s" % ("built" if built else "up to date"))
    return True


# --------------------------------------------------------------- run ----
def suite_command():
    """How to start the suite's assembly: its apphost beside the dll, else
    the dll through dotnet. None when it was not built."""
    exe = newest_file(os.path.join(SUITE, "bin"), "suite.exe")
    if exe:
        return [exe]
    dll = newest_file(os.path.join(SUITE, "bin"), "suite.dll")
    return ["dotnet", dll] if dll else None


def run_one(name, cmd):
    """Each test runs in a directory of its own: some tests touch files
    relative to cwd, and the tests/ tree is not a scratch area. The C# side
    is the suite's assembly, told which test to run."""
    out_p = out_c = None
    exe = os.path.join(PRGEXE, name + ".exe")
    if os.path.isfile(exe):
        r = subprocess.run([exe], cwd=PRGEXE, capture_output=True, text=True)
        out_p = r.stdout + r.stderr
        with open(os.path.join(PRGEXE, name + ".txt"), "w") as fh:
            fh.write(out_p)
    if cmd:
        d = os.path.join(CSEXE, "run", name)
        os.makedirs(d, exist_ok=True)
        r = subprocess.run(cmd + [name], cwd=d, capture_output=True, text=True)
        out_c = r.stdout + r.stderr
        with open(os.path.join(CSEXE, name + ".txt"), "w") as fh:
            fh.write(out_c)
    return name, out_p, out_c


def stage_run(names):
    cmd = suite_command()
    # 306 process starts, nothing shared between them: one per core
    with ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as ex:
        res = list(ex.map(lambda n: run_one(n, cmd), names))
    match, differ, skip = 0, 0, 0
    for name, p, c in res:
        if p is None or c is None:
            skip += 1
            print("   SKIP %-12s %s" %
                  (name, "no prg build" if p is None else "no cs build"))
        elif p == c:
            match += 1
        else:
            differ += 1
            print("   DIFFER %s" % name)
            print("      prg: %r" % p[:200])
            print("      cs : %r" % c[:200])
    print("compare: %d match, %d differ, %d skipped" % (match, differ, skip))
    return differ == 0 and skip == 0


def main():
    global FULL
    args = sys.argv[1:]
    usage = "usage: runsuite.py [all|gen|prg|cs|run] [--full] [--test testNNN[,...]]"
    FULL = "--full" in args
    args = [a for a in args if a != "--full"]
    only = None
    if "--test" in args:
        i = args.index("--test")
        if i + 1 >= len(args):
            sys.exit(usage)
        every = test_names()
        only = []
        for w in args[i + 1].split(","):
            w = w.strip()
            n = w[:-1] if w in PAIRMEMBERS else w     # test45a names test45
            if n not in every:
                sys.exit("runsuite.py: no test named %s" % w)
            if n not in only:
                only.append(n)
        del args[i:i + 2]
    stage = args[0] if args else "all"
    if stage not in ("all", "gen", "prg", "cs", "run") or len(args) > 1:
        sys.exit(usage)
    ok = True
    if stage in ("all", "gen"):
        stage_gen(only)
    names = only or test_names()
    if stage in ("all", "prg"):
        ok &= stage_prg(names)
    if stage in ("all", "cs"):
        ok &= stage_cs()
    if stage in ("all", "run"):
        ok &= stage_run(names)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
