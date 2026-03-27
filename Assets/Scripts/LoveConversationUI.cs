using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UnityMicRecorder))]
public class LoveConversationUI : MonoBehaviour
{
    [Header("Recording UI")]
    public Button startButton;
    public Button stopButton;
    public TMP_Text statusText;
    public TMP_Text conversationDisplayText;
    
    [Header("Response Display")]
    public TMP_Text playerTranscriptText;
    public TMP_Text npcResponseText;
    public TMP_Text emotionText;
    public TMP_Text contextText;
    
    [Header("Affection UI (Phase 7.2)")]
    public AffectionUIController affectionUI;
    
    [Header("Calibration UI")]
    public Button calibrateButton;
    public TMP_Text calibrationStatusText;
    public GameObject calibrationPanel;
    
    [Header("Settings")]
    public TMP_InputField sessionIdInput;
    public TMP_Dropdown npcPersonalityDropdown;
    
    [Header("Server Config")]
    public string serverUrl = "http://127.0.0.1:5000";
    
    private UnityMicRecorder recorder;
    private string currentSessionId;
    private string currentTranscript;
    private float currentScore;
    private string currentEmotion;
    private string deviceId;
    private bool isCalibrated = false;
    private bool isCalibrationMode = false;
    private int calibrationSamplesReceived = 0;
    private string calibrationSessionId;
    private int currentCalibrationPromptIndex = 0;
    private List<ConversationTurn> conversationHistory = new List<ConversationTurn>();
    
    // 캘리브레이션용 표준 문장들 (다양한 발음 패턴 포함)
    private readonly string[] CALIBRATION_PROMPTS = {
        "안녕하세요, 오늘 기분이 정말 좋아요.",  // 기본 인사, 긍정 감정
        "오늘 날씨가 참 맑고 화창하네요.",        // 평범한 관찰
        "당신을 만나서 정말 기뻐요."             // 감정 표현
    };
    
    private struct ConversationTurn
    {
        public string playerText;
        public string emotion;
        public float score;
        public string npcResponse;
        public string timestamp;
    }
    
    [System.Serializable]
    private class AnalyzeResponse
    {
        public string transcript;
        public float text_score;
        public Emotion emotion;
        public ConversationContext conversation_context;
        public Inputs inputs;
    }
    
    [System.Serializable]
    private class Inputs
    {
        public string transcript;
    }
    
    [System.Serializable]
    private class Emotion
    {
        public string emotion;
        public float valence;
        public float arousal;
    }
    
    [System.Serializable]
    private class ConversationContext
    {
        public int total_turns;
        public float avg_score;
        public string[] repeated_emotions;
        public bool should_vary_response;
        public string personality;
    }
    
    [System.Serializable]
    private class FeedbackResponse
    {
        public string feedback;
        public string npc_emotion;
        public float mood_change;
        public string score_level;
        public string hint;
        public string special_action;
        public string generator_version;
        public string session_id;
        public float score;
        
        // Legacy compatibility
        public string variation_type;
        public bool was_force_llm;
    }
    
    [System.Serializable]
    private class ConversationStatusResponse
    {
        public int total_turns;
        public float average_score;
        public string[] repeated_emotions;
    }
    
    [System.Serializable]
    private class CalibrationStartResponse
    {
        public string status;
        public string session_id;
        public string device_id;
        public int samples_needed;
        public int samples_received;
        public string message;
    }
    
    [System.Serializable]
    private class CalibrationSampleResponse
    {
        public string status;
        public string session_id;
        public string sample_id;
        public int samples_received;
        public bool is_complete;
        public string message;
    }
    
    [System.Serializable]
    private class CalibrationFinishResponse
    {
        public string status;
        public string device_id;
        public bool calibration_applied;
        public string message;
    }

    void Awake()
    {
        recorder = GetComponent<UnityMicRecorder>();
        recorder.OnServerResponse = OnAnalyzeResponse;
        recorder.OnStatus = (s) => UpdateStatus(s);
        
        // Generate device ID
        deviceId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(deviceId) || deviceId == "unknown")
        {
            deviceId = "device_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }

    void Start()
    {
        // Wire up buttons
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopClicked);
        if (calibrateButton != null) calibrateButton.onClick.AddListener(OnCalibrateClicked);
        
        // Setup dropdowns
        if (npcPersonalityDropdown != null)
        {
            npcPersonalityDropdown.options = new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData("romantic"),
                new TMP_Dropdown.OptionData("mysterious"),
                new TMP_Dropdown.OptionData("playful"),
                new TMP_Dropdown.OptionData("serious")
            };
            npcPersonalityDropdown.value = 0;
        }
        
        // Initialize session
        if (sessionIdInput != null && string.IsNullOrEmpty(sessionIdInput.text))
        {
            currentSessionId = "player_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sessionIdInput.text = currentSessionId;
        }
        else if (sessionIdInput != null)
        {
            currentSessionId = sessionIdInput.text;
        }
        
        // Check calibration status
        StartCoroutine(CheckCalibrationStatus());
        
        // Initialize affection UI
        if (affectionUI != null)
        {
            affectionUI.SetPlayerId(currentSessionId);
            affectionUI.OnAffectionMax += OnAffectionMax;
            affectionUI.OnAffectionMin += OnAffectionMin;
        }
        
        UpdateStatus("Ready");
        UpdateConversationDisplay();
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopClicked);
        if (calibrateButton != null) calibrateButton.onClick.RemoveListener(OnCalibrateClicked);
        
        // Unregister affection events
        if (affectionUI != null)
        {
            affectionUI.OnAffectionMax -= OnAffectionMax;
            affectionUI.OnAffectionMin -= OnAffectionMin;
        }
    }

    private void OnStartClicked()
    {
        UpdateStatus("Recording...");
        recorder.StartRecording();
        if (startButton != null) startButton.interactable = false;
        if (stopButton != null) stopButton.interactable = true;
    }

    private void OnStopClicked()
    {
        if (isCalibrationMode)
        {
            // 캘리브레이션 모드: 샘플 제출
            UpdateStatus("Submitting calibration sample...");
            AudioClip clip = recorder.StopAndGetClip();
            if (clip != null)
            {
                StartCoroutine(SubmitCalibrationSampleCoroutine(clip));
            }
            else
            {
                UpdateStatus("No audio captured");
                isCalibrationMode = false;
            }
        }
        else
        {
            // 일반 모드: 서버로 분석 요청
            UpdateStatus("Processing...");
            recorder.StopAndSend();
        }
        if (startButton != null) startButton.interactable = true;
        if (stopButton != null) stopButton.interactable = false;
    }

    private void OnAnalyzeResponse(string response)
    {
        Debug.Log("LoveConversationUI: Analyze response: " + response);
        
        try
        {
            var analyzeRes = JsonUtility.FromJson<AnalyzeResponse>(response);
            if (analyzeRes != null)
            {
                // Try to get transcript from inputs first (where Deepgram result is stored)
                if (analyzeRes.inputs != null && !string.IsNullOrEmpty(analyzeRes.inputs.transcript))
                {
                    currentTranscript = analyzeRes.inputs.transcript;
                    Debug.Log($"Using transcript from inputs: {currentTranscript}");
                }
                else
                {
                    currentTranscript = analyzeRes.transcript ?? "(no transcript)";
                    Debug.Log($"Using transcript from top-level: {currentTranscript}");
                }
                
                currentScore = analyzeRes.text_score;
                if (analyzeRes.emotion != null)
                {
                    currentEmotion = analyzeRes.emotion.emotion;
                }
                
                // Display player transcript
                if (playerTranscriptText != null)
                {
                    string displayTranscript = string.IsNullOrEmpty(currentTranscript) || currentTranscript == "(no transcript)"
                        ? "You: (silence)"
                        : $"You: {currentTranscript}";
                    playerTranscriptText.text = displayTranscript;
                    Debug.Log($"PlayerTranscriptText updated: {displayTranscript}");
                }
                
                // Display emotion and metrics
                if (emotionText != null && analyzeRes.emotion != null)
                {
                    emotionText.text = $"Emotion: {analyzeRes.emotion.emotion}\nValence: {analyzeRes.emotion.valence:F2}\nArousal: {analyzeRes.emotion.arousal:F2}";
                }
                
                // Display conversation context
                if (contextText != null && analyzeRes.conversation_context != null)
                {
                    var ctx = analyzeRes.conversation_context;
                    string repeatedStr = ctx.repeated_emotions != null && ctx.repeated_emotions.Length > 0 
                        ? string.Join(", ", ctx.repeated_emotions) 
                        : "None";
                    contextText.text = $"Turns: {ctx.total_turns}\nAvg Score: {ctx.avg_score:F2}\nRepeated: {repeatedStr}\nVary Response: {ctx.should_vary_response}";
                }
                
                UpdateStatus($"Analyzed: {currentEmotion} ({currentScore:F2})");
                
                // Automatically generate NPC feedback
                StartCoroutine(RequestNPCFeedback());
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to parse analyze response: " + ex.Message);
        }
        
        UpdateStatus("Server error: " + response);
    }

    private IEnumerator RequestNPCFeedback()
    {
        string feedbackUrl = $"{serverUrl}/feedback";
        
        // 실제 transcript가 비어있으면 "(no transcript)" 대신 빈 문자열 사용
        string transcriptForFeedback = string.IsNullOrEmpty(currentTranscript) || currentTranscript == "(no transcript)" 
            ? "" 
            : currentTranscript;
        
        // currentEmotion이 null이거나 비어있으면 기본값 설정
        string emotionForFeedback = string.IsNullOrEmpty(currentEmotion) ? "neutral" : currentEmotion;
        
        var feedbackRequest = new FeedbackRequest
        {
            session_id = currentSessionId,
            transcript = transcriptForFeedback,
            emotion = emotionForFeedback,
            score = currentScore,
            audio_score = currentScore
        };
        
        Debug.Log($"RequestNPCFeedback: transcript='{transcriptForFeedback}', emotion={emotionForFeedback}, score={currentScore}");
        
        string jsonData = JsonUtility.ToJson(feedbackRequest);
        Debug.Log("Requesting feedback: " + jsonData);
        
        using (UnityWebRequest www = new UnityWebRequest(feedbackUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Feedback request failed: " + www.error);
                UpdateStatus("Feedback error: " + www.error);
            }
            else
            {
                try
                {
                    var feedbackRes = JsonUtility.FromJson<FeedbackResponse>(www.downloadHandler.text);
                    OnFeedbackReceived(feedbackRes);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Failed to parse feedback: " + ex.Message);
                    UpdateStatus("Feedback parse error: " + ex.Message);
                }
            }
        }
    }

    private void OnFeedbackReceived(FeedbackResponse feedback)
    {
        Debug.Log($"NPC Feedback: {feedback.feedback} (Emotion: {feedback.npc_emotion}, Score Level: {feedback.score_level})");
        
        // Display NPC response
        if (npcResponseText != null)
        {
            npcResponseText.text = feedback.feedback;
        }
        
        // Update affection UI (Phase 7.2)
        if (affectionUI != null)
        {
            StartCoroutine(affectionUI.SendAffectionUpdate(feedback.mood_change));
        }
        
        // Add to conversation history
        var turn = new ConversationTurn
        {
            playerText = currentTranscript,
            emotion = feedback.npc_emotion ?? currentEmotion,
            score = feedback.score,
            npcResponse = feedback.feedback,
            timestamp = System.DateTime.Now.ToString("HH:mm:ss")
        };
        conversationHistory.Add(turn);
        
        UpdateConversationDisplay();
        
        // Show score level and mood change
        string statusText = $"NPC: {feedback.score_level} ({feedback.mood_change:+0.00;-0.00;0.00})";
        if (!string.IsNullOrEmpty(feedback.hint))
        {
            statusText += $" [Hint: {feedback.hint}]";
        }
        UpdateStatus(statusText);
    }

    private void UpdateConversationDisplay()
    {
        if (conversationDisplayText == null) return;
        
        string display = "=== Conversation ===\n";
        foreach (var turn in conversationHistory)
        {
            display += $"[{turn.timestamp}] {turn.emotion} ({turn.score:F2})\n";
            display += $"Player: {turn.playerText}\n";
            display += $"NPC: {turn.npcResponse}\n\n";
        }
        
        conversationDisplayText.text = display;
    }
    
    #region Affection Events (Phase 7.2)
    
    private void OnAffectionMax()
    {
        // Clear! 호감도 100 달성
        Debug.Log("[LoveConversationUI] Episode Clear! Affection reached 100!");
        UpdateStatus("★ CLEAR! 호감도 100 달성! ★");
        // TODO: Clear 팝업 표시
    }
    
    private void OnAffectionMin()
    {
        // Fail... 호감도 0
        Debug.Log("[LoveConversationUI] Episode Fail... Affection dropped to 0");
        UpdateStatus("✗ FAIL... 호감도가 바닥났다...");
        // TODO: Fail 팝업 표시 + 회귀 버튼
    }
    
    #endregion

    private void UpdateStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
        else
            Debug.Log(text);
    }

    public void OnQueryConversationStatus()
    {
        StartCoroutine(QueryConversationStatusCoroutine());
    }

    private IEnumerator QueryConversationStatusCoroutine()
    {
        string statusUrl = $"{serverUrl}/conversation-status?session_id={currentSessionId}";
        
        using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Status query failed: " + www.error);
                UpdateStatus("Status error: " + www.error);
            }
            else
            {
                try
                {
                    var statusRes = JsonUtility.FromJson<ConversationStatusResponse>(www.downloadHandler.text);
                    Debug.Log($"Conversation Status: turns={statusRes.total_turns}, avg={statusRes.average_score:F2}");
                    UpdateStatus($"Status: {statusRes.total_turns} turns, avg score {statusRes.average_score:F2}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Failed to parse status: " + ex.Message);
                }
            }
        }
    }

    #region Calibration Functions
    
    private void OnCalibrateClicked()
    {
        StartCoroutine(StartCalibration());
    }
    
    private IEnumerator CheckCalibrationStatus()
    {
        string statusUrl = $"{serverUrl}/calibrate/status?device_id={deviceId}";
        
        using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<CalibrationStatusCheckResponse>(www.downloadHandler.text);
                    isCalibrated = response.calibrated;
                    
                    if (calibrationStatusText != null)
                    {
                        calibrationStatusText.text = isCalibrated 
                            ? $"✓ Calibrated ({response.num_samples} samples)" 
                            : "○ Not calibrated";
                    }
                    
                    Debug.Log($"Calibration status: {(isCalibrated ? "Calibrated" : "Not calibrated")}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Calibration status parse error: {ex.Message}");
                    if (calibrationStatusText != null)
                        calibrationStatusText.text = "? Unknown";
                }
            }
            else
            {
                Debug.LogWarning($"Calibration status check failed: {www.error}");
                if (calibrationStatusText != null)
                    calibrationStatusText.text = "✗ Server error";
            }
        }
    }
    
    private IEnumerator StartCalibration()
    {
        UpdateStatus("Starting calibration...");
        
        // Start calibration session
        var startData = new CalibrationStartRequest
        {
            device_id = deviceId
        };
        string json = JsonUtility.ToJson(startData);
        
        using (UnityWebRequest www = new UnityWebRequest($"{serverUrl}/calibrate/start", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Calibration start failed: {www.error}");
                UpdateStatus("Calibration failed");
                yield break;
            }
            
            var response = JsonUtility.FromJson<CalibrationStartResponse>(www.downloadHandler.text);
            calibrationSessionId = response.session_id;
            calibrationSamplesReceived = 0;
            currentCalibrationPromptIndex = 0;
            isCalibrationMode = true;  // 캘리브레이션 모드 활성화
            
            Debug.Log($"Calibration started: {response.message}");
            
            // 첫 번째 문장 표시
            string firstPrompt = CALIBRATION_PROMPTS[0];
            UpdateStatus($"Read this sentence:\n\"{firstPrompt}\"");
            
            // Show calibration panel
            if (calibrationPanel != null)
                calibrationPanel.SetActive(true);
            
            if (calibrationStatusText != null)
                calibrationStatusText.text = $"○ Sample 1/{response.samples_needed}";
            
            // 플레이어 텍스트 영역에도 문장 표시
            if (playerTranscriptText != null)
                playerTranscriptText.text = $"📖 Read:\n{firstPrompt}";
        }
    }
    
    public void SubmitCalibrationSample(AudioClip clip)
    {
        StartCoroutine(SubmitCalibrationSampleCoroutine(clip));
    }
    
    private IEnumerator SubmitCalibrationSampleCoroutine(AudioClip clip)
    {
        UpdateStatus($"Submitting sample {calibrationSamplesReceived + 1}/3...");
        
        // Convert clip to WAV
        byte[] wavData = ConvertClipToWav(clip);
        
        // Create multipart form manually
        string boundary = "----CalibrationBoundary" + System.DateTime.Now.Ticks;
        byte[] formData = BuildMultipartFormData(boundary, calibrationSessionId, wavData);
        
        using (UnityWebRequest www = new UnityWebRequest($"{serverUrl}/calibrate/sample", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(formData);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Sample submission failed: {www.error}");
                UpdateStatus("Sample failed - try again");
                yield break;
            }
            
            var response = JsonUtility.FromJson<CalibrationSampleResponse>(www.downloadHandler.text);
            calibrationSamplesReceived = response.samples_received;
            currentCalibrationPromptIndex++;
            
            Debug.Log($"Sample received: {response.message}");
            
            // Update calibration status text
            if (calibrationStatusText != null)
                calibrationStatusText.text = $"○ Sample {calibrationSamplesReceived}/3 ✓";
            
            if (response.is_complete)
            {
                // Auto-finish calibration
                yield return StartCoroutine(FinishCalibration());
            }
            else
            {
                // 다음 문장 표시
                string nextPrompt = CALIBRATION_PROMPTS[currentCalibrationPromptIndex % CALIBRATION_PROMPTS.Length];
                UpdateStatus($"Read this sentence:\n\"{nextPrompt}\"");
                
                if (playerTranscriptText != null)
                    playerTranscriptText.text = $"📖 Read:\n{nextPrompt}";
            }
        }
    }
    
    private byte[] BuildMultipartFormData(string boundary, string sessionId, byte[] wavData)
    {
        var sb = new System.Text.StringBuilder();
        
        // Session ID field
        sb.AppendLine($"--{boundary}");
        sb.AppendLine("Content-Disposition: form-data; name=\"session_id\"");
        sb.AppendLine();
        sb.AppendLine(sessionId);
        
        // File field
        sb.AppendLine($"--{boundary}");
        sb.AppendLine("Content-Disposition: form-data; name=\"file\"; filename=\"sample.wav\"");
        sb.AppendLine("Content-Type: audio/wav");
        sb.AppendLine();
        
        byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        byte[] footerBytes = System.Text.Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
        
        byte[] result = new byte[headerBytes.Length + wavData.Length + footerBytes.Length];
        System.Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        System.Buffer.BlockCopy(wavData, 0, result, headerBytes.Length, wavData.Length);
        System.Buffer.BlockCopy(footerBytes, 0, result, headerBytes.Length + wavData.Length, footerBytes.Length);
        
        return result;
    }
    
    private IEnumerator FinishCalibration()
    {
        UpdateStatus("Finalizing calibration...");
        
        var finishData = new CalibrationFinishRequest
        {
            session_id = calibrationSessionId
        };
        string json = JsonUtility.ToJson(finishData);
        
        using (UnityWebRequest www = new UnityWebRequest($"{serverUrl}/calibrate/finish", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Calibration finish failed: {www.error}");
                UpdateStatus("Calibration failed");
                isCalibrationMode = false;
                yield break;
            }
            
            var response = JsonUtility.FromJson<CalibrationFinishResponse>(www.downloadHandler.text);
            isCalibrated = response.calibration_applied;
            isCalibrationMode = false;  // 캘리브레이션 모드 종료
            currentCalibrationPromptIndex = 0;
            
            Debug.Log($"Calibration complete: {response.message}");
            UpdateStatus("Calibration complete! Ready to play.");
            
            if (calibrationStatusText != null)
                calibrationStatusText.text = "✓ Calibrated";
            
            if (calibrationPanel != null)
                calibrationPanel.SetActive(false);
            
            // 플레이어 텍스트 영역 초기화
            if (playerTranscriptText != null)
                playerTranscriptText.text = "";
        }
    }
    
    private byte[] ConvertClipToWav(AudioClip clip)
    {
        // Simple WAV conversion
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        
        int sampleCount = samples.Length;
        int frequency = clip.frequency;
        int channels = clip.channels;
        
        // WAV header (44 bytes) + data
        byte[] wav = new byte[44 + sampleCount * 2];
        
        // RIFF header
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        System.BitConverter.GetBytes(36 + sampleCount * 2).CopyTo(wav, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        
        // fmt chunk
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        System.BitConverter.GetBytes(16).CopyTo(wav, 16); // chunk size
        System.BitConverter.GetBytes((short)1).CopyTo(wav, 20); // PCM
        System.BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        System.BitConverter.GetBytes(frequency).CopyTo(wav, 24);
        System.BitConverter.GetBytes(frequency * channels * 2).CopyTo(wav, 28);
        System.BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
        System.BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        
        // data chunk
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        System.BitConverter.GetBytes(sampleCount * 2).CopyTo(wav, 40);
        
        // Convert float samples to 16-bit PCM
        for (int i = 0; i < sampleCount; i++)
        {
            short value = (short)(samples[i] * 32767f);
            System.BitConverter.GetBytes(value).CopyTo(wav, 44 + i * 2);
        }
        
        return wav;
    }
    
    #endregion

    [System.Serializable]
    private class FeedbackRequest
    {
        public string session_id;
        public string transcript;
        public string emotion;
        public float score;
        public float audio_score;
    }
    
    [System.Serializable]
    private class CalibrationStartRequest
    {
        public string device_id;
    }
    
    [System.Serializable]
    private class CalibrationFinishRequest
    {
        public string session_id;
    }
    
    [System.Serializable]
    private class CalibrationStatusCheckResponse
    {
        public string status;
        public bool calibrated;
        public string device_id;
        public int num_samples;
    }
}
