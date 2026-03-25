# TestRecordScene

Usage:

- In Unity Editor, open the menu `Tools -> Create -> Test Record Scene` to generate the scene at `Assets/Scenes/TestRecordScene.unity`.
- Open the created scene and press Play. Use the `Start Recording` button to begin a short recording (up to 10s), then `Stop & Send` to upload to the local analysis server (`http://127.0.0.1:5000/analyze`).
- The status text shows server responses or errors.

Notes:

- Ensure the analysis server is running and accessible from the editor machine.
- The recorder script is at `Assets/Scripts/UnityMicRecorder.cs` and the UI glue logic is `Assets/Scripts/TestRecorderUI.cs`.
- For calibration flow, extend the server endpoints `/calibrate` and call from UI or additional editor tools.

Saving recordings for calibration:

- Use the `Save WAV` button in the test scene to save the last recorded WAV to `LoveSimulation_plan/prototype/episode1/sample_wavs/`.
- After saving several recordings, run the calibration client:

```powershell
python .\LoveSimulation_plan\prototype\episode1\calibrate_client.py --device editor_pc LoveSimulation_plan\prototype\episode1\sample_wavs\recording_20260314_120000.wav ...
```

