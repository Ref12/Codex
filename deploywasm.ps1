[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$CodexExeDir = "$PSScriptRoot/bin/exe",
    [string]$WasmPublishDir = "$PSScriptRoot/bin/wasm",
    [string]$OutputDir = "$PSScriptRoot/bin/web",
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = "Stop"

& "$CodexExeDir/Codex.exe" deployweb -s "$WasmPublishDir/wwwroot" -t "$OutputDir" -h pages -m wasm -i "index?_ts{timestamp}=1" @RemainingArgs