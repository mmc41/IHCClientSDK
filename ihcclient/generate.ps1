#!/usr/bin/env pwsh
# PowerShell port of generate.sh
# Regenerates the SOAP layer in generatedsrc/ from the fixed WSDL files using dotnet-svcutil.
# Requires the dotnet-svcutil global tool: dotnet tool install --global dotnet-svcutil

$ErrorActionPreference = 'Stop'

# Run relative to this script's location (the ihcclient/ directory).
Push-Location $PSScriptRoot
try {
    Remove-Item generatedsrc\*.cs -ErrorAction SilentlyContinue

    foreach ($filepath in (Get-ChildItem wsdl\fixed\*.wsdl | Sort-Object Name)) {
        $filebase = $filepath.BaseName
        $fileNS = $filebase.Substring(0, 1).ToUpper() + $filebase.Substring(1)

        dotnet-svcutil --serializer XmlSerializer --noStdLib --outputDir generatedsrc --outputFile $filebase --namespace "*,Ihc.Soap.$fileNS" $filepath.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet-svcutil failed for $($filepath.Name) (exit code $LASTEXITCODE)"
        }
    }

    Remove-Item generatedsrc\*.json -ErrorAction SilentlyContinue
}
finally {
    Pop-Location
}
