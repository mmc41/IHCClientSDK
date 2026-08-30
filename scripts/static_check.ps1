<#
    The repository's static checks (PowerShell peer of static_check.sh; same checks, same contract).

    Directory.Build.targets calls this after compiling ihcclient on Windows; so can you, from any
    directory:

        pwsh -NoProfile -File scripts/static_check.ps1

    Any check added here must be added to static_check.sh too — the build picks one by platform, so
    a check that lives in only one of them is a check half the contributors never run.

    Exit code contract, which the build depends on: non-zero means a check could not RUN (its tool
    is missing, or it failed), never that a check found something. These checks advise; they do not
    gate. The build turns a non-zero exit into a warning and leaves the verdict to the compiler.
#>

# PowerShell 7.4 and later raise a terminating error when a native command exits non-zero and
# ErrorActionPreference is Stop. This script reads $LASTEXITCODE itself and reports through its own
# exit code, so it opts out rather than letting jscpd's exit status abort the run before the
# remaining checks have had their turn.
$PSNativeCommandUseErrorActionPreference = $false

# PowerShell decodes a native command's output with [Console]::OutputEncoding, which on Windows is
# the OEM codepage rather than UTF-8. This script carries a tool's stdout into a file, so without
# this the report arrives mojibake — measured: the `·` in jscpd's summary line became `┬À`.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Set-Location -LiteralPath (Join-Path $PSScriptRoot '..')

# .NET's own working directory does not follow Set-Location, so every path handed to a framework API
# below is absolute. Paths printed for a human stay repository-relative, as the peer prints them.
$repoRoot = (Get-Location).Path

$status = 0

# Copy/paste detection. Everything about the scan — which files, which excludes, which reporter, and
# the output directory below — is declared in .jscpd.json rather than on the command line, so that
# `jscpd .` by hand from the repository root is the same scan this runs.
#
# The ai reporter writes to stdout rather than to the output directory the config names, so capturing
# it is what puts the report on disk — and keeps 700-odd clone lines out of every build log.
#
# .txt, not .md: despite what the reporter is called, it emits plain text (a header, one line per
# clone pair, a rule, a summary) with no markdown in it, and read as markdown those lines would
# collapse into a single paragraph. jscpd's `markdown` reporter is the one that writes real markdown.
$jscpdReport = 'artifacts/jscpd/jscpd-ai.txt'
$jscpdReportPath = Join-Path $repoRoot $jscpdReport
if (Get-Command jscpd -CommandType Application -ErrorAction SilentlyContinue) {
    $jscpdOutput = & jscpd --config .jscpd.json --no-colors --no-tips .
    if ($LASTEXITCODE -eq 0) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jscpdReportPath) | Out-Null
        # Written through the framework rather than with `>`, which would end every line with the
        # platform separator and leave this report differing from the peer script's by line ending
        # alone. UTF8Encoding($false) is the no-BOM constructor. Written only once jscpd has
        # succeeded, so a report on disk is always a report some run produced.
        [System.IO.File]::WriteAllText(
            $jscpdReportPath,
            (($jscpdOutput -join "`n") + "`n"),
            [System.Text.UTF8Encoding]::new($false))
        Write-Output "jscpd copy/paste report: $jscpdReport"
    }
    else {
        Remove-Item -LiteralPath $jscpdReportPath -ErrorAction SilentlyContinue
        [Console]::Error.WriteLine("jscpd failed, so $jscpdReport was not written.")
        $status = 1
    }
}
else {
    [Console]::Error.WriteLine('jscpd not found on PATH, so copy/paste detection was skipped. Install it with: npm install -g jscpd')
    $status = 1
}

exit $status
