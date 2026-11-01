# Static analysis

What checks this repository's source without running it, where each check is declared, and what its verdict
means. What the test suites cover is [TESTSTRATEGY.md](../TESTSTRATEGY.md); this page is everything that judges
the code without executing it.

## The layers

| Layer | When it runs | Declared in | What a finding does |
| --- | --- | --- | --- |
| **Compiler analyzers**: nullable reference types, `AnalysisMode=Recommended`, the security category at `All`, and individually chosen correctness rules | Every build | `Directory.Build.props`, `Directory.Build.targets`, `.globalconfig`, `ihcclient/analyser.config` and per-folder `.editorconfig` files; [ARCHITECTURE.md invariant 8](../ARCHITECTURE.md#architectural-invariants) says which file decides what | Fails the build: warnings are errors in every project |
| **Copy/paste detection** (jscpd) | Once per build, whatever mix of projects is built | `.jscpd.json`, run by `scripts/static_check.sh` and `scripts/static_check.ps1` | Reported in `artifacts/jscpd/jscpd-ai.txt`; never fails the build |
| **Pattern-based security scan** (opengrep) | In CI on every push and pull request, and by hand | `.github/workflows/opengrep.yml`, with the ruleset pinned in `scripts/opengrep-scan.sh` | Reported; a hand run gates only with `--error` |
| **Data-flow analysis** (CodeQL) | In CI only | `.github/workflows/codeql.yml` | Listed in the repository's Security tab |

How to run the last two by hand is in the [README](../README.md#running-the-tests). The compiler layer and
CodeQL between them are why no test suite carries a pass of its own over variable lifecycle or data flow.

## Build-time checks

Every build runs the repository's static checks once, whatever mix of projects is built.
`scripts/static_check.sh` and `scripts/static_check.ps1` ARE the list: the build files name no check, so a
check is extended by editing both peers and nothing else. A check whose tool is missing, or that fails to run,
warns and leaves the verdict to the compiler, so a green build is never evidence that the checks ran: the
warning is, or the report's timestamp. A peer exits non-zero only when a check could not RUN, never because a
check found something. `-p:RunStaticChecks=false` opts a build out, and `bash scripts/static_check.sh` -- or
`pwsh -NoProfile -File scripts/static_check.ps1` on Windows -- runs the checks without building.

## Copy/paste detection

Today the build-time checks are jscpd copy/paste detection, writing `artifacts/jscpd/jscpd-ai.txt` -- one line
per clone pair, then a summary. It is `.txt` because the `ai` reporter emits plain text, not markdown, whatever
its name suggests. The file is written only when the scan succeeds, so a report on disk is always a report some
run produced.

### What the scan covers

The whole scan is declared in `.jscpd.json`: authored C# only, with the generated SOAP layer,
`*.g.cs`/`*.Designer.cs` and the `tests/testdata/` oracle corpus excluded as unauthored or byte-pinned, the
standalone `examples/` left out as well, and the reporter and output directory alongside them. jscpd's parser
is strict JSON, so a comment in that file is a parse error -- which is why its reasoning lives here.

`ProblemCatalogEntries.*.cs` is excluded on a third ground: those files are pure declaration tables, where a
"clone" is only ever two catalogue entries passing the same constructor arguments positionally, which is the
shape the data has and not something to refactor. What keeps that exclusion honest is that the files carry no
control flow at all -- no `if`, `for`, `switch`, `return` or local; the day logic lands in one, it stops being a
declaration table and the exclusion stops being safe.

`ihcclient/src/vis/catalog/definitions/**` is excluded on a near-identical ground and needs its own entry
because those files carry no `.g.cs` suffix any more: a clone there is two products or two grammar declarations
passing the same builder-call chain, which is the transcribed catalogue data, and what each file evaluates to is
pinned by `BuiltInCatalogDigestTests` rather than kept honest by refactoring. Naming the folder also removes a
half-truth the file-size limit would otherwise leave in the report: jscpd skips a file over 1 MB silently, and
one of the three is well past that, so a scanned folder would report on two of them while reading as all three.

Do not add `"gitignore": true`: jscpd already respects `.gitignore` by default, so it reads as scope the config
does not actually control. The file is jscpd's own config format, so `jscpd .` by hand from the repository root
is the same scan; its `output` key is why a hand-run with a file reporter lands under `artifacts/jscpd/` instead
of creating `report/` in the repository root.

### What is not evidence of copy/paste

`ignorePattern` says which text is not evidence of copy/paste -- `using` directives, and the fixture plumbing
every headless UI test opens with. jscpd blanks each match before matching but keeps the block's line numbers,
and the surviving tokens still have to reach the threshold alone, so the effect is "fifty tokens of something
other than boilerplate", not a lower bar.

The test a pattern has to pass: the text it matches must be the SAME text everywhere it matches, so blanking it
erases no distinction between two blocks. That is why each entry spells out a whole statement and pins it
between `^[ \t]*` and `\r?$` rather than wrapping a method name in `^.*` and `.*$` -- the wrapping form also
swallows whatever else shares the line, and a line is shared by the receiver and the variable the call is
assigned to, by a comment that merely mentions the method, and by an `Assert.That(...)` whose subject the call
happens to be. Blanked that way, `CurrentTestWindow = null;` and `CurrentTestWindow = window;` become the same
line, which is the invented match.

Anchor `using` at column zero, because the indented `using var x = ...;` statement is real code; use `[ \t]*`
and not `\s*` for indentation, because under `(?m)` a `\s*` also eats newlines and runs the match back through
the preceding blank lines; and write a line end `\r?$`, because the working tree is CRLF on Windows and LF
elsewhere. Naming the variable in the pattern (`harness`, `vm`, `window`) is deliberate: it is what makes the
match a fixed string instead of a hole a real name could fall into.

### Levers that do not work

Raising `minTokens`/`minLines` instead does not work here and has been measured -- the boilerplate and the
smallest real findings share one size band, so a threshold that removes much of the first removes most of the
second; `mode` is no lever either, the default `mild` already reports the fewest. `baseline` and
`failOnNewClones` are valid keys, but the `ai` reporter ignores a baseline and prints every clone regardless,
and `failOnNewClones` must never be set: exiting non-zero on a finding inverts the exit-code contract above, and
the report gets deleted rather than written.
