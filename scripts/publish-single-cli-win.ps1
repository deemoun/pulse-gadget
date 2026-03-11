param(
    [string]$Rid = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoSelfContained
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir "..")
$ProjectPath = Join-Path $RepoRoot "src/PulseGadget.Cli/PulseGadget.Cli.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI is not installed or not in PATH."
}

$SelfContained = if ($NoSelfContained) { "false" } else { "true" }
$OutDir = Join-Path $RepoRoot "artifacts/cli/$Rid"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

Write-Host "Publishing single-file Windows CLI artifact..."
Write-Host "  Project: $ProjectPath"
Write-Host "  RID: $Rid"
Write-Host "  Configuration: $Configuration"
Write-Host "  Self-contained: $SelfContained"
Write-Host "  Output: $OutDir"

dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Rid `
    --self-contained $SelfContained `
    -o $OutDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugSymbols=false `
    /p:DebugType=None

$artifactSource = Join-Path $OutDir "PulseGadget.Cli.exe"
$artifactTarget = Join-Path $OutDir "pulse-gadget.exe"

if (Test-Path $artifactSource) {
    Copy-Item $artifactSource $artifactTarget -Force
    Write-Host "Created artifact: $artifactTarget"
} else {
    Write-Warning "Expected published binary not found at $artifactSource"
}
