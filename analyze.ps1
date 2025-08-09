[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$CodexExeDir = "$PSScriptRoot\bin\exe",
    [string]$AnalysisDir = "$PSScriptRoot\bin\cdx\analyze",
    [string]$RepoRoot = "$PSScriptRoot",
    [string]$BinLogPath = "$PSScriptRoot\msbuild.binlog",
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

& "$CodexExeDir\Codex.exe" analyze --binLogSearchDirectory "$BinLogPath" --clean --noMsBuild --out "$AnalysisDir" --path "$RepoRoot" @RemainingArgs