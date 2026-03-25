# Build And Package Guide (Windows)

## 1) Build Unity client

### Option A: From Unity Editor
- Open `File > Build Settings`
- Target: `Windows, x86_64`
- Build to: `Builds/Windows/`

### Option B: From command line (CI-friendly)
```powershell
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "C:\Users\555a\LoveSimulation_sample" `
  -executeMethod BuildCli.BuildWindows64 `
  -logFile "Builds\unity_build.log"
```

This uses: `Assets/Editor/BuildCli.cs`

## 2) Package release bundle

Run from workspace root:
```powershell
powershell -ExecutionPolicy Bypass -File "LoveSimulation_plan/prototype/episode1/package_release.ps1"
```

Output example:
- `Releases/LoveSim_release_YYYYMMDD_HHMMSS/Client`
- `Releases/LoveSim_release_YYYYMMDD_HHMMSS/Server`
- `Releases/LoveSim_release_YYYYMMDD_HHMMSS/run_all.ps1`

## 3) Run on another PC

1. Install Python 3.11+ (or 3.14 as used here)
2. Open PowerShell in release folder
3. Set key:
```powershell
$env:DEEPGRAM_API_KEY = "<your_key>"
```
4. Start server:
```powershell
powershell -ExecutionPolicy Bypass -File .\Server\start_server.ps1
```
5. Run client `.exe` from `Client` folder

## Notes
- Current Unity upload URL is local: `http://127.0.0.1:5000/analyze`
- If server is remote, change `uploadUrl` in Unity and rebuild.
- Keep `Server` and `Client` folders together for easiest ops.
