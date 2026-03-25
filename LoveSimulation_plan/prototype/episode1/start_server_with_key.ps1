Param(
    [string]$Key = "15277f43302b64b0253d41a3ff178e8c72de4013"
)

$env:DEEPGRAM_API_KEY = $Key
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$python = Join-Path $scriptRoot '..\..\.venv\Scripts\python.exe'
$server = Join-Path $scriptRoot 'server.py'

Write-Host "Using DEEPGRAM_API_KEY (length)=" ($env:DEEPGRAM_API_KEY).Length
Write-Host "Starting server via: $python $server"

& $python $server
