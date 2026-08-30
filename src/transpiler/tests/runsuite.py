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


# --------------------------------------------------------------- prg ----
def build_prg(name):
    srcs = sources(name, ".prg", ".")
    if not srcs:
        return name, "no source"
    exe = os.path.join(PRGEXE, name + ".exe")
    if os.path.isfile(exe):
        os.remove(exe)
    r = subprocess.run(["hbmk2"] + srcs +
                       [os.path.join("-oprgexe", name),
                        "-w", "-es2", "-gtcgi", "-q"],
                       cwd=TESTS, capture_output=True, text=True)
    if r.returncode == 0 and os.path.isfile(exe):
        return name, None
    tail = (r.stdout + r.stderr).strip().splitlines()
    return name, (tail[-1][:120] if tail else "rc=%d" % r.returncode)


def stage_prg(names):
    os.makedirs(PRGEXE, exist_ok=True)
    bad = [(n, e) for n, e in (build_prg(n) for n in names) if e]
    print("hbmk2:  %d built, %d failed" % (len(names) - len(bad), len(bad)))
    for n, e in bad:
        print("   FAIL %-12s %s" % (n, e))
    return not bad


# ---------------------------------------------------------------- cs ----
def build_cs(name):
    srcs = sources(name, ".cs", "csout")
    if not srcs:
        return name, "no source"
    d = os.path.join(CSEXE, name)
    os.makedirs(d, exist_ok=True)
    for old in os.listdir(d):              # stale .cs from an earlier run
        if old.endswith(".cs"):
            os.remove(os.path.join(d, old))
    with open(os.path.join(d, name + ".csproj"), "w") as fh:
        fh.write(CSPROJ % (name, name))
    for s in srcs:
        shutil.copy(os.path.join(TESTS, s), d)
    for sub in (DEFINES, ORM):             # Const classes + ORM fixtures
        if os.path.isdir(sub):
            for f in os.listdir(sub):
                if f.endswith(".cs"):
                    shutil.copy(os.path.join(sub, f), d)
    r = subprocess.run(["dotnet", "build", "--no-dependencies", "-v", "q",
                        "--nologo"], cwd=d, capture_output=True, text=True)
    if r.returncode == 0 and "error CS" not in r.stdout:
        return name, None
    errs = [l.strip() for l in r.stdout.splitlines() if "error CS" in l]
    return name, (errs[0][:120] if errs else "rc=%d" % r.returncode)


def stage_cs(names):
    """HbRuntime is built once up front: the per-test builds run in
    parallel and would otherwise race to write HbRuntime.dll, surfacing
    as random CS2012 file-in-use failures scattered across tests."""
    lib = os.path.join(CSEXE, "HbRuntime")
    os.makedirs(lib, exist_ok=True)
    shutil.copy(os.path.join(ROOT, "src", "transpiler", "HbRuntime.cs"),
                os.path.join(lib, "HbRuntime.cs"))
    with open(os.path.join(lib, "HbRuntime.csproj"), "w") as fh:
        fh.write(LIBPROJ)
    subprocess.run(["dotnet", "build", "-v", "q", "--nologo"],
                   cwd=lib, capture_output=True)
    with ThreadPoolExecutor(max_workers=4) as ex:
        res = list(ex.map(build_cs, names))
    bad = [(n, e) for n, e in res if e]
    print("dotnet: %d built, %d failed" % (len(names) - len(bad), len(bad)))
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
    stage = sys.argv[1] if len(sys.argv) > 1 else "all"
    if stage not in ("all", "gen", "prg", "cs", "run"):
        sys.exit("usage: runsuite.py [all|gen|prg|cs|run]")
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
