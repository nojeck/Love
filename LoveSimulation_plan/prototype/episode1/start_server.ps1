param(
    [string]$Host = "127.0.0.1",
    [int]$Port = 5000,
    [string]$Python = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
$VenvDir = Join-Path $ProjectRoot ".venv"
$VenvPython = Join-Path $VenvDir "Scripts\python.exe"
$PyVenvCfg = Join-Path $VenvDir "pyvenv.cfg"
$Requirements = Join-Path $ScriptDir "requirements.txt"
$ServerFile = Join-Path $ScriptDir "server.py"

function Resolve-Python {
    param([string]$Override)

    if ($Override -and (Test-Path $Override)) {
        return $Override
    }

    if ((Test-Path $VenvPython) -and (Test-Path $PyVenvCfg)) {
        return $VenvPython
    }

    return "python"
}

function Ensure-Venv {
    if ((Test-Path $VenvPython) -and (Test-Path $PyVenvCfg)) {
        return
    }

    Write-Host "[setup] creating virtual environment at $VenvDir"
    python -m venv $VenvDir

    if (-not (Test-Path $VenvPython)) {
        throw "Failed to create venv python at $VenvPython"
    }

    Write-Host "[setup] installing dependencies"
    & $VenvPython -m pip install --upgrade pip
    & $VenvPython -m pip install -r $Requirements
}

Ensure-Venv
$PyExe = Resolve-Python -Override $Python

if (-not (Test-Path $ServerFile)) {
    throw "server.py not found: $ServerFile"
}

if (-not $env:DEEPGRAM_API_KEY) {
    Write-Warning "DEEPGRAM_API_KEY is not set. STT will be skipped."
}

$env:FLASK_RUN_HOST = $Host
$env:FLASK_RUN_PORT = "$Port"
Write-Host "[run] python: $PyExe"
Write-Host "[run] server: $ServerFile"
Write-Host "[run] host=$Host port=$Port"

& $PyExe $ServerFile
