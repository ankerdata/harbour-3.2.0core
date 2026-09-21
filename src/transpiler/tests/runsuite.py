#!/usr/bin/env python3
"""Windows runner for the transpiler test suite.

The .sh scripts are macOS-first and fail on Windows two ways:

  * they pass $SCRIPTDIR as an absolute POSIX path, so hbmk2.exe and
    dotnet receive -o/c/Users/... and every build fails;
  * driving them through Git Bash puts /usr/bin ahead of MSVC on PATH,
    so GNU link shadows link.exe and hbmk2 dies with
    "link: unknown option -- n".

This does the same work in Python with relative paths, and runtests.bat
calls it directly rather than through a shell, which is what keeps the
linker straight. Run runtests.bat; it sets up vcvarsall and the Harbour
bin directory first.

Stages (default "all"):
  gen   regenerate hbout/ and csout/     (runtests.sh + buildcs.sh)
  prg   build the Harbour reference exes (buildprg.sh)
  cs    build the emitted C#             (buildcs.sh)
  run   run both and diff the output     (runprg.sh/runcs.sh/comparecs.sh)

Builds are incremental: a Harbour exe is rebuilt only when it is older
than its .prg sources or a local .ch they include, and a C# test only
when the .cs it would be built from differ from what it was last built
from, or when the runtime assembly (the HbRuntime/ sources plus the libraries)
beside it is not the current one. gen always transpiles every test — the suite shares one
reftab, so one test's change can change another's output — and every
test always runs. --full rebuilds everything: the occasional sweep, and
the answer to a changed Harbour or .NET toolchain, which the
incremental checks cannot see.

Usage: runsuite.py [all|gen|prg|cs|run] [--full]

Environment:
  HBTRANSPILER  transpiler binary (default <root>/bin/hbtranspiler.exe)
"""
import os
import re
import shutil
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

TESTS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(TESTS, "..", "..", ".."))
HBOUT = os.path.join(TESTS, "hbout")
CSOUT = os.path.join(TESTS, "csout")
CSEXE = os.path.join(TESTS, "csexe")
PRGEXE = os.path.join(TESTS, "prgexe")
DEFINES = os.path.join(TESTS, "defines")
ORM = os.path.join(TESTS, "orm")
PRELOAD = os.path.join(TESTS, "preload.txt")
REFTAB = os.path.join(ROOT, "src", "transpiler", "hbreftab.tab")
GENDEFINES = os.path.join(ROOT, "src", "transpiler", "tools", "gendefines.py")

TRANSPILER = os.environ.get("HBTRANSPILER") or \
    os.path.join(ROOT, "bin", "hbtranspiler.exe")

# Multi-file tests: testNNa + testNNb build as one project named testNN.
PAIRS = ["19", "20", "22", "41", "45", "46"]
PAIRMEMBERS = set()
for _p in PAIRS:
    PAIRMEMBERS |= {"test" + _p + "a", "test" + _p + "b"}

SRC_RE = re.compile(r"^(test\w+)\.prg$")
INCLUDE_RE = re.compile(r'^[ \t]*#[ \t]*include[ \t]+"([^"]+)"', re.I | re.M)

FULL = False                     # --full: rebuild every test

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>%s</RootNamespace>
    <AssemblyName>%s</AssemblyName>
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
def stage_gen():
    """Reproduce runtests.sh, then buildcs.sh.

    runtests.sh clears hbreftab.tab, scans every source with -GF, then
    emits -GT into hbout/. buildcs.sh then scans AGAIN without clearing
    and emits -GS into csout/. Clearing before the -GS pass produces
    false diffs (test79 emits Animal vs dynamic), so both the order and
    the not-clearing matter.
    """
    if not os.path.isfile(TRANSPILER):
        sys.exit("transpiler not found: %s (set HBTRANSPILER)" % TRANSPILER)
    for d in (HBOUT, CSOUT):
        os.makedirs(d, exist_ok=True)

    srcs = sorted(f for f in os.listdir(TESTS) if SRC_RE.match(f))
    base = [TRANSPILER]
    if os.path.isfile(PRELOAD):
        base.append("--preload-list=" + PRELOAD)
    base += ["-I" + os.path.join(ROOT, "include"), "-I" + TESTS]

    def run(args):
        subprocess.run(args, cwd=TESTS, capture_output=True)

    if os.path.isfile(REFTAB):
        os.remove(REFTAB)
    for f in srcs:
        run(base + [f, "-GF", "-q"])
    for f in srcs:
        run(base + ["-o" + HBOUT + os.sep, f, "-GT"])
    print("hbout/  regenerated (%d sources)" % len(srcs))

    for f in srcs:                    # deliberately NOT clearing the reftab
        run(base + [f, "-GF", "-q"])

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
    for f in srcs:
        run(base + opts + ["-o" + CSOUT + os.sep, f, "-GS", "-q"])
    print("csout/  regenerated")


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
    r = subprocess.run(["hbmk2"] + srcs +
                       [os.path.join("-oprgexe", name),
                        "-w", "-es2", "-gtcgi", "-q"],
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
def build_cs(name, lib_dll):
    """(name, error or None, built?). Skipped when the staged .cs and
    .csproj are byte-identical to the last build's and the runtime
    assembly that build copied beside it is the current one. (Not "newer
    than the runtime": MSBuild copies a new HbRuntime.dll but leaves the
    test's own dll alone when the runtime's public API did not change.)"""
    srcs = sources(name, ".cs", "csout")
    if not srcs:
        return name, "no source", False
    d = os.path.join(CSEXE, name)
    os.makedirs(d, exist_ok=True)
    files = {os.path.basename(s): os.path.join(TESTS, s) for s in srcs}
    for sub in (DEFINES, ORM):             # Const classes + ORM fixtures
        if os.path.isdir(sub):
            for f in os.listdir(sub):
                if f.endswith(".cs"):
                    files[f] = os.path.join(sub, f)
    changed = stage_files(files, d, ".cs")
    changed |= write_if_changed(os.path.join(d, name + ".csproj"),
                                CSPROJ % (name, name))
    dll = newest_file(os.path.join(d, "bin"), name + ".dll")
    rt = newest_file(os.path.join(d, "bin"), "HbRuntime.dll")
    if not FULL and not changed and dll and rt and same_bytes(lib_dll, rt):
        return name, None, False
    r = subprocess.run(["dotnet", "build", "--no-dependencies", "-v", "q",
                        "--nologo"], cwd=d, capture_output=True, text=True)
    if r.returncode == 0 and "error CS" not in r.stdout:
        return name, None, True
    if dll and os.path.isfile(dll):        # never skip a failed build later
        os.remove(dll)
    errs = [l.strip() for l in r.stdout.splitlines() if "error CS" in l]
    return name, (errs[0][:120] if errs else "rc=%d" % r.returncode), True


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


def stage_cs(names):
    """HbRuntime is built once up front: the per-test builds run in
    parallel and would otherwise race to write HbRuntime.dll, surfacing
    as random CS2012 file-in-use failures scattered across tests. It is
    rebuilt only when its sources changed, and a test whose output holds
    another HbRuntime.dll than the one built here is rebuilt against it."""
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
    with ThreadPoolExecutor(max_workers=4) as ex:
        res = list(ex.map(lambda n: build_cs(n, lib_dll), names))
    bad = [(n, e) for n, e, _ in res if e]
    built = sum(1 for _, e, b in res if b and not e)
    print("dotnet: %d built, %d up to date, %d failed" %
          (built, len(names) - built - len(bad), len(bad)))
    for n, e in bad:
        print("   FAIL %-12s %s" % (n, e))
    return not bad


# --------------------------------------------------------------- run ----
def run_one(name):
    """Each executable runs in its own output directory: some tests touch
    files relative to cwd, and the tests/ tree is not a scratch area."""
    out_p = out_c = None
    exe = os.path.join(PRGEXE, name + ".exe")
    if os.path.isfile(exe):
        r = subprocess.run([exe], cwd=PRGEXE, capture_output=True, text=True)
        out_p = r.stdout + r.stderr
        with open(os.path.join(PRGEXE, name + ".txt"), "w") as fh:
            fh.write(out_p)
    d = os.path.join(CSEXE, name)
    if os.path.isdir(d):
        r = subprocess.run(["dotnet", "run", "--no-build"], cwd=d,
                           capture_output=True, text=True)
        out_c = r.stdout + r.stderr
        with open(os.path.join(CSEXE, name + ".txt"), "w") as fh:
            fh.write(out_c)
    return name, out_p, out_c


def stage_run(names):
    with ThreadPoolExecutor(max_workers=4) as ex:
        res = list(ex.map(run_one, names))
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
    FULL = "--full" in args
    args = [a for a in args if a != "--full"]
    stage = args[0] if args else "all"
    if stage not in ("all", "gen", "prg", "cs", "run") or len(args) > 1:
        sys.exit("usage: runsuite.py [all|gen|prg|cs|run] [--full]")
    ok = True
    if stage in ("all", "gen"):
        stage_gen()
    names = test_names()
    if stage in ("all", "prg"):
        ok &= stage_prg(names)
    if stage in ("all", "cs"):
        ok &= stage_cs(names)
    if stage in ("all", "run"):
        ok &= stage_run(names)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
