using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(UnityMicRecorder))]
public class TestRecorderUI : MonoBehaviour
{
    public Button startButton;
    public Button stopButton;
    public Button saveButton;
    public Button autoCalButton;
    public TMP_InputField deviceIdInput;
    public TMP_InputField countInput;
    public TMP_InputField secondsInput;
    public TMP_Text statusText;
    public TMP_Text transcriptText;
    public TMP_Text metricsText;
    public TMP_Text emotionText;
    public TMP_Text requestIdText;
    public TMP_Dropdown deviceDropdown;

    private UnityMicRecorder recorder;

    void Awake()
    {
        recorder = GetComponent<UnityMicRecorder>();
        recorder.OnServerResponse = OnServerResponse;
        recorder.OnStatus = (s) => { UpdateStatus(s); UpdateUIState(s); };
        Debug.Log("TestRecorderUI Awake: recorder attached=" + (recorder != null));
    }

    void Start()
    {
        Debug.Log("TestRecorderUI Start: wiring buttons");
        // attempt to auto-wire UI fields in case they weren't assigned in the scene
        AutoWireUI();
        if (deviceDropdown != null && !IsDropdownTemplateValid(deviceDropdown, out var dropdownIssue))
        {
            Debug.LogWarning("Device dropdown is misconfigured: " + dropdownIssue + ". Falling back to default microphone selection.");
            deviceDropdown.interactable = false;
            UpdateStatus("Device dropdown template missing. Using default microphone.");
            deviceDropdown = null;
        }
        // populate microphone list if dropdown provided
        try
        {
            if (deviceDropdown != null)
            {
                deviceDropdown.ClearOptions();
                var devices = Microphone.devices;
                if (devices != null && devices.Length > 0)
                {
                    var opts = new System.Collections.Generic.List<string>(devices);
                    deviceDropdown.AddOptions(opts);
                    deviceDropdown.onValueChanged.AddListener(OnDeviceDropdownChanged);
                    // set recorder device to first by default
                    recorder.SetDevice(devices[0]);
                }
                else
                {
                    deviceDropdown.AddOptions(new System.Collections.Generic.List<string> { "(no devices)" });
                    if (startButton != null) startButton.interactable = false;
                    UpdateStatus("No microphone devices found");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to populate device list: " + ex.Message);
        }
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopClicked);
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
        if (autoCalButton != null) autoCalButton.onClick.AddListener(OnAutoCalClicked);
        // default values
        if (deviceIdInput != null && string.IsNullOrEmpty(deviceIdInput.text)) deviceIdInput.text = "editor_pc";
        if (countInput != null && string.IsNullOrEmpty(countInput.text)) countInput.text = "3";
        if (secondsInput != null && string.IsNullOrEmpty(secondsInput.text)) secondsInput.text = "3";
        UpdateStatus("Ready");
    }

    private bool IsDropdownTemplateValid(TMP_Dropdown dd, out string reason)
    {
        reason = null;
        if (dd == null)
        {
            reason = "Dropdown reference is null";
            return false;
        }

        if (dd.template == null)
        {
            reason = "Template is not assigned";
            return false;
        }

        var itemToggle = dd.template.GetComponentInChildren<Toggle>(true);
        if (itemToggle == null)
        {
            reason = "Template has no child Toggle item";
            return false;
        }

        return true;
    }

    // Try to find UI components by common names or types when public fields are not set in the inspector
    private void AutoWireUI()
    {
        try
        {
            if (startButton == null) startButton = FindUI<Button>(new[] { "StartButton", "BtnStart", "startButton" });
            if (stopButton == null) stopButton = FindUI<Button>(new[] { "StopButton", "BtnStop", "stopButton" });
            if (saveButton == null) saveButton = FindUI<Button>(new[] { "SaveButton", "BtnSave", "saveButton" });
            if (autoCalButton == null) autoCalButton = FindUI<Button>(new[] { "AutoCalButton", "BtnAutoCal", "autoCalButton" });

            if (deviceIdInput == null) deviceIdInput = FindUI<TMP_InputField>(new[] { "DeviceIdInput", "deviceIdInput", "DeviceId" });
            if (countInput == null) countInput = FindUI<TMP_InputField>(new[] { "CountInput", "countInput" });
            if (secondsInput == null) secondsInput = FindUI<TMP_InputField>(new[] { "SecondsInput", "secondsInput" });

            if (statusText == null) statusText = FindUI<TMP_Text>(new[] { "StatusText", "statusText" });
            if (transcriptText == null) transcriptText = FindUI<TMP_Text>(new[] { "TranscriptText", "transcriptText" });
            if (metricsText == null) metricsText = FindUI<TMP_Text>(new[] { "MetricsText", "metricsText" });
            if (emotionText == null) emotionText = FindUI<TMP_Text>(new[] { "EmotionText", "emotionText" });
            if (requestIdText == null) requestIdText = FindUI<TMP_Text>(new[] { "RequestIdText", "requestIdText" });

            if (deviceDropdown == null) deviceDropdown = FindUI<TMP_Dropdown>(new[] { "DeviceDropdown", "deviceDropdown" });

            Debug.LogFormat("AutoWireUI: start={0} stop={1} save={2} autoCal={3} transcript={4} requestId={5}",
                startButton != null, stopButton != null, saveButton != null, autoCalButton != null,
                transcriptText != null, requestIdText != null);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("AutoWireUI failed: " + ex.Message);
        }
    }

    private T FindUI<T>(string[] names) where T : Component
    {
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            var go = GameObject.Find(n);
            if (go != null)
            {
                var comp = go.GetComponent<T>();
                if (comp != null) return comp;
            }
        }
        // fallback: first available in scene
        var all = GameObject.FindObjectsByType<T>(FindObjectsSortMode.None);
        if (all != null && all.Length > 0) return all[0];
        return null;
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopClicked);
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        if (autoCalButton != null) autoCalButton.onClick.RemoveListener(OnAutoCalClicked);
    }

    public void OnStartClicked()
    {
        // if dropdown exists, set selected device
        if (deviceDropdown != null && deviceDropdown.options != null && deviceDropdown.options.Count > 0)
        {
            var name = deviceDropdown.options[deviceDropdown.value].text;
            if (name != "(no devices)") recorder.SetDevice(name);
        }
        UpdateStatus("Recording...");
        recorder.StartRecording();
        UpdateUIState("Recording");
    }

    public void OnStopClicked()
    {
        UpdateStatus("Uploading...");
        recorder.StopAndSend();
        UpdateUIState("Uploading");
    }

    public void OnSaveClicked()
    {
        UpdateStatus("Saving...");
        string path = recorder.SaveLastRecording();
        if (!string.IsNullOrEmpty(path)) UpdateStatus("Saved: " + path);
        else UpdateStatus("Save failed or no recording available.");
    }

    public void OnAutoCalClicked()
    {
        int count = 3;
        float seconds = 3.0f;
        string device = "editor_pc";
        if (countInput != null) int.TryParse(countInput.text, out count);
        if (secondsInput != null) float.TryParse(secondsInput.text, out seconds);
        if (deviceIdInput != null && !string.IsNullOrEmpty(deviceIdInput.text)) device = deviceIdInput.text;
        StartCoroutine(AutoCalibrateCoroutine(count, seconds, device));
    }

    private System.Collections.IEnumerator AutoCalibrateCoroutine(int count, float secondsPerRecord, string deviceId)
    {
        UpdateStatus($"AutoCal: will record {count}x{secondsPerRecord}s for {deviceId}");
        var savedFiles = new System.Collections.Generic.List<string>();
        for (int i = 0; i < count; i++)
        {
            UpdateStatus($"Recording {i+1}/{count}...");
            recorder.StartRecording();
            yield return new WaitForSeconds(secondsPerRecord);
            recorder.StopAndSend();
            // give a small delay to ensure lastWav is written
            yield return new WaitForSeconds(0.2f);
            string fileName = $"auto_rec_{System.DateTime.Now.ToString("yyyyMMdd_HHmmss")}.wav";
            string path = recorder.SaveLastRecording(fileName);
            if (!string.IsNullOrEmpty(path)) savedFiles.Add(path);
            yield return new WaitForSeconds(0.2f);
        }

        if (savedFiles.Count == 0)
        {
            UpdateStatus("AutoCal: no recordings saved");
            yield break;
        }

        UpdateStatus("Uploading calibration files...");
        string url = $"http://127.0.0.1:5000/calibrate?device_id={UnityEngine.Networking.UnityWebRequest.EscapeURL(deviceId)}";
        WWWForm form = new WWWForm();
        foreach (var p in savedFiles)
        {
            byte[] data = System.IO.File.ReadAllBytes(p);
            form.AddBinaryData("files", data, System.IO.Path.GetFileName(p), "audio/wav");
        }

        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.ConnectionError || www.result == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError)
            {
                UpdateStatus("Calibrate upload failed: " + www.error);
            }
            else
            {
                UpdateStatus("Calibrate response: " + www.downloadHandler.text);
            }
        }
    }

    private void OnServerResponse(string response)
    {
        // quick handling for error sentinel strings
        if (!string.IsNullOrEmpty(response) && (response.StartsWith("ERROR") || response == "ERROR_NO_AUDIO"))
        {
            UpdateStatus("Server error: " + response);
            return;
        }
        Debug.Log("OnServerResponse raw: " + response);
        // try parse JSON and display structured info
        try
        {
            var res = JsonUtility.FromJson<AnalysisResult>(response);
            if (res != null && res.inputs != null)
            {
                Debug.LogFormat("Parsed AnalysisResult: req_uuid={0} deepgram_status={1} deepgram_request_id={2} transcript_len={3}",
                    res.req_uuid,
                    res.deepgram_status,
                    res.deepgram_request_id,
                    res.inputs.transcript == null ? "null" : res.inputs.transcript.Length.ToString());

                UpdateStatus($"Server: audio_score={res.audio_score} authenticity={res.authenticity}");

                string transcript = res.inputs.transcript;
                if (string.IsNullOrEmpty(transcript)) transcript = "(no transcript)";
                if (transcriptText != null) transcriptText.text = transcript;

                if (metricsText != null)
                {
                    metricsText.text =
                        $"f0: {res.inputs.f0_mean} Hz\n" +
                        $"hnr: {res.inputs.hnr_db} dB\n" +
                        $"jitter: {res.inputs.jitter}%\n" +
                        $"shimmer: {res.inputs.shimmer}%\n" +
                        $"text_score: {res.inputs.text_score}\n" +
                        $"dg_status: {res.deepgram_status}\n" +
                        $"dg_attempts: {res.deepgram_attempt_count}\n" +
                        $"transcript_source: {res.transcript_source}\n" +
                        $"transcript_empty: {res.transcript_is_empty}";
                }

                try
                {
                    var wrapper = JsonUtility.FromJson<EmotionWrapper>(response);
                    if (wrapper != null && wrapper.emotion != null && emotionText != null)
                    {
                        emotionText.text = $"{wrapper.emotion.emotion} (valence:{wrapper.emotion.valence}, arousal:{wrapper.emotion.arousal})";
                    }
                }
                catch { }

                try
                {
                    if (requestIdText != null)
                    {
                        string id = !string.IsNullOrEmpty(res.deepgram_request_id) ? res.deepgram_request_id : "(no id)";
                        string flowId = !string.IsNullOrEmpty(res.req_uuid) ? res.req_uuid : "(no req_uuid)";
                        string clientReqId = !string.IsNullOrEmpty(res.client_req_id) ? res.client_req_id : "(no client_req_id)";
                        string logName = !string.IsNullOrEmpty(res.deepgram_log_file) ? res.deepgram_log_file : "(no log)";
                        string dgError = !string.IsNullOrEmpty(res.deepgram_error) ? res.deepgram_error : "-";

                        requestIdText.text =
                            $"req:{flowId}\n" +
                            $"client_req:{clientReqId}\n" +
                            $"dg_called:{res.deepgram_called} status:{res.deepgram_status}\n" +
                            $"dg_id:{id}\n" +
                            $"dg_log:{logName}\n" +
                            $"dg_error:{dgError}";
                    }
                }
                catch { }

                UpdateUIState("Idle");
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to parse server JSON: " + ex.Message);
        }

        // Fallback: try to extract simple transcript field via string search (robust when JsonUtility fails)
        try
        {
            var key = "\"transcript\":";
            int idx = response.IndexOf(key, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int start = response.IndexOf('"', idx + key.Length);
                if (start >= 0)
                {
                    int end = response.IndexOf('"', start + 1);
                    if (end > start)
                    {
                        string simpleTranscript = response.Substring(start + 1, end - start - 1);
                        if (transcriptText != null) transcriptText.text = string.IsNullOrEmpty(simpleTranscript) ? "(no transcript)" : simpleTranscript;
                        UpdateStatus("Server: transcript updated");
                        UpdateUIState("Idle");
                        return;
                    }
                }
            }
        }
        catch { }

        UpdateStatus("Server: " + response);
        UpdateUIState("Idle");
    }

    private void OnDeviceDropdownChanged(int idx)
    {
        if (deviceDropdown == null) return;
        var name = deviceDropdown.options[idx].text;
        if (name != "(no devices)") recorder.SetDevice(name);
    }

    private void UpdateUIState(string status)
    {
        bool recording = status != null && status.ToLower().Contains("record");
        bool uploading = status != null && status.ToLower().Contains("upload");

        if (startButton != null) startButton.interactable = !recording && !uploading;
        if (stopButton != null) stopButton.interactable = recording;
        if (saveButton != null) saveButton.interactable = !recording && !uploading && recorder != null && recorder.HasRecording;
    }

    [System.Serializable]
    private class AnalysisResult
    {
        public string req_uuid;
        public string client_req_id;
        public float audio_score;
        public float authenticity;
        public bool calibration_used;
        public float memory_penalty;
        public string deepgram_request_id;
        public string deepgram_log_file;
        public bool deepgram_called;
        public string deepgram_status;
        public string deepgram_error;
        public int deepgram_attempt_count;
        public float deepgram_confidence;
        public int deepgram_http_status;
        public string transcript_source;
        public bool transcript_is_empty;
        public Inputs inputs;
    }

    [System.Serializable]
    private class Inputs
    {
        public float f0_mean;
        public float hnr_db;
        public float jitter;
        public float pitch_dev_percent;
        public int repeat_count;
        public float shimmer;
        public float text_score;
        public string transcript;
    }

    [System.Serializable]
    private class Emotion
    {
        public string emotion;
        public float valence;
        public float arousal;
        public float confidence;
    }

    [System.Serializable]
    private class EmotionWrapper
    {
        public Emotion emotion;
    }

    private void UpdateStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
        else
            Debug.Log(text);
    }
}
