Deepgram STT Integration
------------------------

Usage:

1. Set environment variable `DEEPGRAM_API_KEY` with your Deepgram API key in the shell where the server runs.

PowerShell example:

```powershell
$env:DEEPGRAM_API_KEY="YOUR_KEY"
```

2. Start the prototype server using the project venv:

```powershell
.\LoveSimulation_plan\.venv\Scripts\python.exe .\LoveSimulation_plan\prototype\episode1\server.py
```

3. Test transcription with the included client:

```powershell
python .\LoveSimulation_plan\prototype\episode1\deepgram_test_client.py .\LoveSimulation_plan\prototype\episode1\sample_wavs\auto_rec_20260314_231110.wav
```

Notes:
- The server will attempt Deepgram transcription only if `DEEPGRAM_API_KEY` is set.
- For real-time low-latency transcription, consider implementing WebSocket streaming (future work).
