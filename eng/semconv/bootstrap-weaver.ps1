[CmdletBinding()]
Param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "scripts\bootstrap-weaver.ps1") @args
