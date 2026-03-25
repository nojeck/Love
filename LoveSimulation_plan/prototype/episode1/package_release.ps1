param(
    [string]$UnityBuildDir = "Builds/Windows",
    [string]$OutputRoot = "Releases"
)

$ErrorActionPreference = "Stop"

$WorkspaceRoot = (Get-Location).Path
$UnityBuildPath = Join-Path $WorkspaceRoot $UnityBuildDir
if (-not (Test-Path $UnityBuildPath)) {
    throw "Unity build directory not found: $UnityBuildPath"
}

$EpisodeDir = Join-Path $WorkspaceRoot "LoveSimulation_plan/prototype/episode1"
if (-not (Test-Path $EpisodeDir)) {
    throw "Episode1 directory not found: $EpisodeDir"
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$ReleaseDir = Join-Path $WorkspaceRoot (Join-Path $OutputRoot ("LoveSim_release_" + $stamp))
$ClientDir = Join-Path $ReleaseDir "Client"
$ServerDir = Join-Path $ReleaseDir "Server"

New-Item -ItemType Directory -Path $ClientDir -Force | Out-Null
New-Item -ItemType Directory -Path $ServerDir -Force | Out-Null

Write-Host "[package] Copying Unity build..."
Copy-Item -Path (Join-Path $UnityBuildPath "*") -Destination $ClientDir -Recurse -Force

$serverFiles = @(
    "server.py",
    "scorer.py",
    "emotion_lexicon.json",
    "requirements.txt",
    "start_server.ps1",
    "README.md"
)

Write-Host "[package] Copying server runtime files..."
foreach ($f in $serverFiles) {
    $src = Join-Path $EpisodeDir $f
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $ServerDir -Force
    }
}

# Optional helper and docs
$optionalFiles = @(
    "DEEPGRAM_README.md",
    "calibrate_client.py",
    "test_post.py"
)
foreach ($f in $optionalFiles) {
    $src = Join-Path $EpisodeDir $f
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $ServerDir -Force
    }
}

$runAll = @"
`$ErrorActionPreference = "Stop"
Write-Host "Set your Deepgram key first:"
Write-Host "  `$env:DEEPGRAM_API_KEY = '<your_key>'"
Write-Host ""
Write-Host "Starting server..."
Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', '.\\Server\\start_server.ps1'
Write-Host "Starting client..."
`$exe = Get-ChildItem '.\\Client' -Filter '*.exe' | Select-Object -First 1
if (`$null -eq `$exe) { throw 'No .exe found in Client folder.' }
Start-Process `$exe.FullName
"@
Set-Content -Path (Join-Path $ReleaseDir "run_all.ps1") -Value $runAll -Encoding UTF8

$readme = @"
LoveSim Release Package
======================

1) Set Deepgram API key in PowerShell:
   `$env:DEEPGRAM_API_KEY = "<your_key>"

2) Start server:
   powershell -ExecutionPolicy Bypass -File .\\Server\\start_server.ps1

3) Run client:
   .\\Client\\LoveSimulation_sample.exe (or first .exe in Client)

Notes:
- Client currently posts to http://127.0.0.1:5000/analyze
- For remote server, change uploadUrl in Unity and rebuild.
"@
Set-Content -Path (Join-Path $ReleaseDir "README_DEPLOY.txt") -Value $readme -Encoding UTF8

Write-Host "[done] Release created: $ReleaseDir"
