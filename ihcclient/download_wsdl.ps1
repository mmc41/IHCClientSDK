#!/usr/bin/env pwsh
# PowerShell port of download_wsdl.sh
# Downloads the raw WSDL files from an IHC controller into the wsdl/ directory.
# Usage: ./download_wsdl.ps1 <ip-address-of-ihc-controller>

param(
    [Parameter(Mandatory = $true, HelpMessage = 'IP address of IHC controller')]
    [string]$Ip
)

$ErrorActionPreference = 'Stop'

$wsdls = @(
    'authentication'
    'configuration'
    'controller'
    'messagecontrollog'
    'module'
    'notificationmanager'
    'resourceinteraction'
    'timemanager'
    'usermanager'
    'openapi'
    'airlinkmanagement'
    'smsmodem'
    'testihc'
    'leddimmermanagement'
    'productiontest'
)

# Run relative to this script's location (the ihcclient/ directory).
Push-Location (Join-Path $PSScriptRoot 'wsdl')
try {
    Write-Host "Downloading WSDL from IHC controller at $Ip"

    foreach ($name in $wsdls) {
        $url = "http://$Ip/wsdl/$name.wsdl"
        Write-Host $url
        Invoke-WebRequest -Uri $url -OutFile "$name.wsdl"
    }
}
finally {
    Pop-Location
}
