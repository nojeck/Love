using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UnityMicRecorder))]
public class LoveConversationUI : MonoBehaviour
{
    private enum FlowState
    {
        Initializing,
        StartingEpisode,
        ShowingSituation,
        WaitingPlayerResponse,
        ProcessingAudio,
        ShowingFeedback,
        TransitioningSituation,
        Error
    }

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
    
    [Header("Auto Recording (Phase 7.3)")]
    public AutoRecordingController autoRecording;
    public bool useAutoRecording = true;
    
    [Header("Result Popup (Phase 7.4)")]
    public ResultPopupController resultPopup;
    
    [Header("Situation Panel (Phase 7.5)")]
    public SituationPanelController situationPanel;
    public bool useSituationPanel = false;
    
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
    
    // 에피소드/상황 관리 (Phase 7.5)
    private int currentEpisodeId = 1;
    private string currentSituationId = "";
    private string currentNpcId = "suji";
    private bool episodeStarted = false;
    [SerializeField] private float feedbackDisplayDuration = 2.5f;
    [SerializeField] private bool useClickOrSpaceAdvance = true;
    private Coroutine pendingSituationTransition;
    private GameObject responseDisplayRoot;
    private GameObject conversationDisplayRoot;
    private FlowState currentFlowState = FlowState.Initializing;
    private bool isAdvancePending = false;
    private string pendingTransitionResult;
    [SerializeField] private int episodeStartMaxRetries = 3;
    [SerializeField] private float episodeStartRetryDelay = 3f;
    private bool isEpisodeStartInProgress = false;
    private readonly List<string> scoreLogEntries = new List<string>();
    private const int ScoreLogLimit = 5;
    private string latestStatusMessage = "Ready";
    
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
        public float text_score;
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
        recorder.OnStatus = OnRecorderStatus;
        useSituationPanel = false;
        
        // Generate device ID
        deviceId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(deviceId) || deviceId == "unknown")
        {
            deviceId = "device_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }

    private void PresentSituation(SituationData situation)
    {
        if (situation == null)
        {
            SetFlowError("Situation data is null", "상황 데이터가 없습니다.");
            return;
        }

        currentSituationId = situation.situation_id;

        if (!TryExtractNpcPrompt(situation, out var dialogue, out var emotion))
        {
            SetFlowError("Situation npc dialogue missing", "NPC 대사 데이터가 없습니다.");
            return;
        }

        StartNpcDialogue(dialogue, emotion);
    }

    private void Update()
    {
        if (!useClickOrSpaceAdvance)
            return;

        if (!isAdvancePending)
            return;

        if (IsAdvanceTriggeredThisFrame())
        {
            TryAdvanceConversationStep();
        }
    }

    void Start()
    {
        Transform responseDisplay = transform.Find("ResponseDisplay");
        if (responseDisplay != null)
            responseDisplayRoot = responseDisplay.gameObject;

        Transform conversationDisplay = transform.Find("ConversationDisplay");
        if (conversationDisplay != null)
            conversationDisplayRoot = conversationDisplay.gameObject;

        SetFlowState(FlowState.Initializing, "Startup");

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
            affectionUI.OnAffectionMax.AddListener(OnAffectionMax);
            affectionUI.OnAffectionMin.AddListener(OnAffectionMin);
        }
        
        // Initialize auto recording (Phase 7.3)
        if (autoRecording != null && useAutoRecording)
        {
            autoRecording.OnRecordingComplete.AddListener(OnAutoRecordingComplete);
            autoRecording.OnSilenceTimeout.AddListener(OnAutoRecordingSilence);
            autoRecording.OnMaxTimeExceeded.AddListener(OnAutoRecordingTooLong);
        }
        
        UpdateStatus("Ready");
        UpdateConversationDisplay();
        
        // 에피소드 시작 (Phase 7.5)
        StartCoroutine(StartEpisode());
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopClicked);
        if (calibrateButton != null) calibrateButton.onClick.RemoveListener(OnCalibrateClicked);
        
        // Unregister affection events
        if (affectionUI != null)
        {
            affectionUI.OnAffectionMax.RemoveListener(OnAffectionMax);
            affectionUI.OnAffectionMin.RemoveListener(OnAffectionMin);
        }
        
        // Unregister auto recording events (Phase 7.3)
        if (autoRecording != null && useAutoRecording)
        {
            autoRecording.OnRecordingComplete.RemoveListener(OnAutoRecordingComplete);
            autoRecording.OnSilenceTimeout.RemoveListener(OnAutoRecordingSilence);
            autoRecording.OnMaxTimeExceeded.RemoveListener(OnAutoRecordingTooLong);
        }
        
        if (pendingSituationTransition != null)
        {
            StopCoroutine(pendingSituationTransition);
            pendingSituationTransition = null;
        }
    }

    private void OnStartClicked()
    {
        SetTransientStatus("Recording...");
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
            SetTransientStatus("Processing...");
            recorder.StopAndSend();
        }
        if (startButton != null) startButton.interactable = true;
        if (stopButton != null) stopButton.interactable = false;
    }

    private void OnAnalyzeResponse(string response)
    {
        Debug.Log("LoveConversationUI: Analyze response: " + response);
        SetFlowState(FlowState.ProcessingAudio, "AnalyzeResponse");

        if (!TryParseAnalyzeResponse(response, out var analyzeRes))
        {
            SetFlowError("Analyze response parse failed", "Server error: " + response);
            return;
        }

        ApplyAnalyzeResponse(analyzeRes);
        StartCoroutine(RequestNPCFeedback());
    }

    private bool TryParseAnalyzeResponse(string response, out AnalyzeResponse analyzeRes)
    {
        analyzeRes = null;

        try
        {
            analyzeRes = JsonUtility.FromJson<AnalyzeResponse>(response);
            return analyzeRes != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to parse analyze response: " + ex.Message);
            return false;
        }
    }

    private void ApplyAnalyzeResponse(AnalyzeResponse analyzeRes)
    {
        string transcriptFromInputs = analyzeRes.inputs != null ? analyzeRes.inputs.transcript : null;
        string transcriptFromTopLevel = analyzeRes.transcript;
        string normalizedTranscript = NormalizeTranscript(
            !string.IsNullOrWhiteSpace(transcriptFromInputs) ? transcriptFromInputs : transcriptFromTopLevel
        );

        if (!string.IsNullOrEmpty(normalizedTranscript))
        {
            currentTranscript = normalizedTranscript;
            Debug.Log($"Using normalized transcript: {currentTranscript}");
        }
        else
        {
            currentTranscript = "(no transcript)";
            Debug.Log("No valid transcript found after normalization.");
        }

        float parsedScore = analyzeRes.text_score;
        if (parsedScore <= 0f && analyzeRes.inputs != null && analyzeRes.inputs.text_score > 0f)
        {
            parsedScore = analyzeRes.inputs.text_score;
            Debug.Log($"Using text_score from inputs: {parsedScore:F3}");
        }
        currentScore = Mathf.Clamp01(parsedScore);

        if (analyzeRes.emotion != null)
        {
            currentEmotion = analyzeRes.emotion.emotion;
        }

        if (playerTranscriptText != null)
        {
            string displayTranscript = string.IsNullOrEmpty(currentTranscript) || currentTranscript == "(no transcript)"
                ? "You: (silence)"
                : $"You: {currentTranscript}";
            playerTranscriptText.text = displayTranscript;
            Debug.Log($"PlayerTranscriptText updated: {displayTranscript}");
        }

        if (emotionText != null && analyzeRes.emotion != null)
        {
            emotionText.text = $"Emotion: {analyzeRes.emotion.emotion}\nValence: {analyzeRes.emotion.valence:F2}\nArousal: {analyzeRes.emotion.arousal:F2}";
        }

        if (contextText != null && analyzeRes.conversation_context != null)
        {
            var ctx = analyzeRes.conversation_context;
            string repeatedStr = ctx.repeated_emotions != null && ctx.repeated_emotions.Length > 0
                ? string.Join(", ", ctx.repeated_emotions)
                : "None";
            contextText.text = $"Turns: {ctx.total_turns}\nAvg Score: {ctx.avg_score:F2}\nRepeated: {repeatedStr}\nVary Response: {ctx.should_vary_response}";
        }

        SetTransientStatus($"Analyzed: {currentEmotion} ({currentScore:F2})");
    }

    private IEnumerator RequestNPCFeedback()
    {
        string feedbackUrl = $"{serverUrl}/feedback";
        
        // 실제 transcript가 비어있으면 "(no transcript)" 대신 빈 문자열 사용
        string transcriptForFeedback = NormalizeTranscript(
            currentTranscript == "(no transcript)" ? "" : currentTranscript
        );
        
        // currentEmotion이 null이거나 비어있으면 기본값 설정
        string emotionForFeedback = string.IsNullOrEmpty(currentEmotion) ? "neutral" : currentEmotion;
        
        var feedbackRequest = new FeedbackRequest
        {
            session_id = currentSessionId,
            transcript = transcriptForFeedback,
            emotion = emotionForFeedback,
            score = currentScore,
            audio_score = currentScore,
            game_context = new GameContext
            {
                episode = currentEpisodeId,
                situation = currentSituationId ?? "",
                personality = "romantic"
            }
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
                SetFlowError("Feedback request failed", "Feedback error: " + www.error);
            }
            else
            {
                if (TryParseFeedbackResponse(www.downloadHandler.text, out var feedbackRes))
                {
                    OnFeedbackReceived(feedbackRes);
                }
                else
                {
                    SetFlowError("Feedback parse failed", "Feedback parse error");
                }
            }
        }
    }

    private bool TryParseFeedbackResponse(string response, out FeedbackResponse feedbackRes)
    {
        feedbackRes = null;

        try
        {
            feedbackRes = JsonUtility.FromJson<FeedbackResponse>(response);
            return feedbackRes != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to parse feedback: " + ex.Message);
            return false;
        }
    }

    private void OnFeedbackReceived(FeedbackResponse feedback)
    {
        Debug.Log($"NPC Feedback: {feedback.feedback} (Emotion: {feedback.npc_emotion}, Score Level: {feedback.score_level})");
        SetFlowState(FlowState.ShowingFeedback, "Feedback received");
        
        ShowNpcFeedbackText(feedback.feedback, feedback.npc_emotion ?? currentEmotion);
        
        // Update affection UI (Phase 7.2)
        if (affectionUI != null)
        {
            StartCoroutine(affectionUI.SendAffectionUpdate(feedback.mood_change));
        }
        
        AppendConversationTurn(
            currentTranscript,
            feedback.npc_emotion ?? currentEmotion,
            feedback.score,
            feedback.feedback
        );

        UpdateConversationDisplay();
        
        // Show score level and mood change
        string statusText = $"NPC: {feedback.score_level} ({feedback.mood_change:+0.00;-0.00;0.00})";
        if (!string.IsNullOrEmpty(feedback.hint))
        {
            statusText += $" [Hint: {feedback.hint}]";
        }
        SetTransientStatus(statusText);
        
        // Phase 7.5: 상황 전환 체크
        if (episodeStarted)
        {
            string result = GetResultFromScore(feedback.score);
            MarkAdvancePending(result);
        }

        AddScoreLogEntry(feedback.score, feedback.mood_change);
    }

    private void MarkAdvancePending(string result)
    {
        pendingTransitionResult = result;
        isAdvancePending = true;
        SetTransientStatus("클릭 또는 Space 키로 다음 단계로 진행하세요.");
    }

    private void TryAdvanceConversationStep()
    {
        if (!isAdvancePending)
            return;

        if (currentFlowState != FlowState.ShowingFeedback)
            return;

        if (string.IsNullOrWhiteSpace(pendingTransitionResult))
            return;

        isAdvancePending = false;
        BeginSituationTransition(pendingTransitionResult);
        pendingTransitionResult = null;
    }

    private bool IsAdvanceTriggeredThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return keyboardPressed || mousePressed;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
    }

    private void BeginSituationTransition(string result)
    {
        if (pendingSituationTransition != null)
        {
            StopCoroutine(pendingSituationTransition);
        }

        SetFlowState(FlowState.TransitioningSituation, $"result={result}");

        pendingSituationTransition = StartCoroutine(TransitionToNextSituation(result));
    }

    private IEnumerator TransitionToNextSituation(string result)
    {
        if (feedbackDisplayDuration > 0f)
        {
            yield return new WaitForSeconds(feedbackDisplayDuration);
        }

        yield return StartCoroutine(CheckNextSituation(result));
        pendingSituationTransition = null;
    }
    
    /// <summary>
    /// 점수에 따른 결과 분류
    /// </summary>
    private string GetResultFromScore(float score)
    {
        if (score >= 0.7f) return "success";
        if (score >= 0.4f) return "neutral";
        return "fail";
    }
    
    /// <summary>
    /// 다음 상황 확인 및 전환
    /// </summary>
    private IEnumerator CheckNextSituation(string result)
    {
        string url = $"{serverUrl}/episode/situation/next";
        
        var requestData = new NextSituationRequest
        {
            player_id = currentSessionId,
            result = result
        };
        
        string jsonData = JsonUtility.ToJson(requestData);
        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 5;
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                if (!TryParseNextSituationResponse(www.downloadHandler.text, out var response))
                {
                    SetFlowState(FlowState.WaitingPlayerResponse, "Next situation parse failed");
                    yield break;
                }

                if (response.status == "ok" && response.situation != null)
                {
                    Debug.Log($"[LoveConversationUI] Situation changed to: {response.situation.situation_id}");
                    PresentSituation(response.situation);
                }
                else
                {
                    SetFlowState(FlowState.WaitingPlayerResponse, "Next situation returned non-ok");
                }
            }
            else
            {
                SetFlowState(FlowState.WaitingPlayerResponse, "Next situation request failed");
            }
        }
    }

    private bool TryParseNextSituationResponse(string responseJson, out NextSituationResponse response)
    {
        response = null;

        try
        {
            response = JsonUtility.FromJson<NextSituationResponse>(responseJson);
            return response != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LoveConversationUI] Next situation parse error: {ex.Message}");
            return false;
        }
    }

    private bool TryParseEpisodeStartResponse(string responseJson, out EpisodeStartResponse response)
    {
        response = null;

        try
        {
            response = JsonUtility.FromJson<EpisodeStartResponse>(responseJson);
            return response != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoveConversationUI] Episode start parse error: {ex.Message}");
            return false;
        }
    }

    private void HandleEpisodeStartSuccess(EpisodeStartResponse response)
    {
        if (response.status != "ok" || response.situation == null)
        {
            SetFlowError("Episode start returned non-ok", "에피소드 시작 실패");
            return;
        }

        episodeStarted = true;

        string title = response.episode != null ? response.episode.episode_title : "";
        Debug.Log($"[LoveConversationUI] Episode started: {title}");
        Debug.Log($"[LoveConversationUI] Situation: {response.situation.situation_id} - {response.situation.phase}");

        PresentSituation(response.situation);
        UpdateStatus("NPC의 말을 듣고 자동 녹음 응답 후, 클릭/Space로 다음 단계로 진행하세요.");
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
        
        // Clear 팝업 표시
        if (resultPopup != null)
        {
            int turnCount = conversationHistory.Count;
            float avgScore = CalculateAverageScore();
            resultPopup.ShowClearPopup(1, turnCount, avgScore, currentSessionId);
        }
    }
    
    private void OnAffectionMin()
    {
        // Fail... 호감도 0
        Debug.Log("[LoveConversationUI] Episode Fail... Affection dropped to 0");
        UpdateStatus("<sprite name=\"cross_mark\"> FAIL... 호감도가 바닥났다...");
        
        // Fail 팝업 표시
        if (resultPopup != null)
        {
            int turnCount = conversationHistory.Count;
            float avgScore = CalculateAverageScore();
            resultPopup.ShowFailPopupWithAnalysis(1, turnCount, avgScore, currentSessionId);
        }
    }
    
    private float CalculateAverageScore()
    {
        if (conversationHistory.Count == 0)
            return 0f;
        
        float total = 0f;
        foreach (var turn in conversationHistory)
        {
            total += turn.score;
        }
        return total / conversationHistory.Count;
    }
    
    #endregion
    
    #region Auto Recording Events (Phase 7.3)
    
    private void OnAutoRecordingComplete(float duration, AudioClip clip)
    {
        if (currentFlowState != FlowState.WaitingPlayerResponse)
        {
            Debug.LogWarning($"[LoveConversationUI] Ignoring recording complete in state: {currentFlowState}");
            return;
        }

        Debug.Log($"[LoveConversationUI] Auto recording complete: {duration:F1}s");
        SetFlowState(FlowState.ProcessingAudio, "Recording complete");
        
        // 분석 요청
        if (clip != null)
        {
            StartCoroutine(SendAudioForAnalysis(clip));
        }
    }
    
    private void OnAutoRecordingSilence()
    {
        if (currentFlowState != FlowState.WaitingPlayerResponse)
        {
            Debug.LogWarning($"[LoveConversationUI] Ignoring silence timeout in state: {currentFlowState}");
            return;
        }

        Debug.Log("[LoveConversationUI] Auto recording silence timeout (10s)");
        SetFlowState(FlowState.ProcessingAudio, "Silence timeout");
        
        // 침묵 처리 - 빈 피드백 요청
        StartCoroutine(RequestSilenceFeedback());
    }
    
    private void OnAutoRecordingTooLong(float duration)
    {
        Debug.Log($"[LoveConversationUI] Auto recording too long: {duration:F1}s");
        
        // 과다 발화 처리
        UpdateStatus("말이 너무 길어요...");
        
        // TODO: 특수 피드백 요청
    }
    
    private IEnumerator SendAudioForAnalysis(AudioClip clip)
    {
        // WAV 변환
        byte[] wavData = ConvertClipToWav(clip);
        
        // 서버로 전송
        string url = $"{serverUrl}/analyze";
        string boundary = "----Boundary" + System.DateTime.Now.Ticks;
        
        // Multipart form data 생성
        var form = new WWWForm();
        form.AddBinaryData("file", wavData, "recording.wav", "audio/wav");
        form.AddField("session_id", currentSessionId);
        form.AddField("device_id", deviceId);
        
        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                OnAnalyzeResponse(www.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[LoveConversationUI] Analysis failed: {www.error}");
                SetTransientStatus("분석 실패: " + www.error);
                SetFlowState(FlowState.WaitingPlayerResponse, "Analysis failed, retry allowed");
            }
        }
        
        // AutoRecording을 Idle로 복귀
        if (autoRecording != null)
        {
            autoRecording.ReturnToIdle();
        }
    }
    
    private IEnumerator RequestSilenceFeedback()
    {
        // 침묵에 대한 피드백 요청
        string url = $"{serverUrl}/feedback";
        
        var requestData = new FeedbackRequest
        {
            session_id = currentSessionId,
            transcript = "",
            emotion = "silence",
            score = 0.0f,
            audio_score = 0.0f
        };
        
        string jsonData = JsonUtility.ToJson(requestData);
        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var feedback = JsonUtility.FromJson<FeedbackResponse>(www.downloadHandler.text);
                    OnFeedbackReceived(feedback);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LoveConversationUI] Failed to parse silence feedback: {ex.Message}");
                    SetFlowState(FlowState.WaitingPlayerResponse, "Silence feedback parse failed");
                }
            }
            else
            {
                SetFlowState(FlowState.WaitingPlayerResponse, "Silence feedback request failed");
            }
        }
        
        // AutoRecording을 Idle로 복귀
        if (autoRecording != null)
        {
            autoRecording.ReturnToIdle();
        }
    }
    
    #endregion

    private void UpdateStatus(string text)
    {
        latestStatusMessage = text;
        RenderStatusPanel();
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
                    calibrationStatusText.text = "<sprite name=\"cross_mark\"> Server error";
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
        public GameContext game_context;
    }
    
    [System.Serializable]
    private class GameContext
    {
        public int episode;
        public string situation;
        public string personality;
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
    
    #region Episode & Situation (Phase 7.5)
    
    [System.Serializable]
    private class EpisodeStartRequest
    {
        public int episode_id;
        public string player_id;
        public string npc_id;
    }
    
    [System.Serializable]
    private class EpisodeStartResponse
    {
        public string status;
        public EpisodeData episode;
        public SituationData situation;
        public NpcData npc;
        public string next_action;
    }
    
    [System.Serializable]
    private class EpisodeData
    {
        public int episode_id;
        public string episode_title;
        public string description;
        public int target_affection;
        public int initial_affection;
        public int max_turns;
    }
    
    [System.Serializable]
    private class SituationData
    {
        public string situation_id;
        public string phase;
        public string situation_text;
        public string intro_dialogue_text;
        public NpcDialogueData npc_dialogue;
        public ContextData context;
    }
    
    [System.Serializable]
    private class NpcDialogueData
    {
        public string text;
        public string emotion;
        public string tone;
        public string gesture;
        public string eye_contact;
    }
    
    [System.Serializable]
    private class ContextData
    {
        public string location;
        public string location_detail;
        public string time;
        public string day;
        public string weather;
        public string npc_state;
        public string npc_activity;
    }
    
    [System.Serializable]
    private class NpcData
    {
        public string npc_id;
        public string name;
        public string personality_type;
    }
    
    [System.Serializable]
    private class NextSituationRequest
    {
        public string player_id;
        public string result;
    }
    
    [System.Serializable]
    private class NextSituationResponse
    {
        public string status;
        public SituationData situation;
        public string game_status;
    }
    
    /// <summary>
    /// 에피소드 시작
    /// </summary>
    private IEnumerator StartEpisode()
    {
        if (episodeStarted || isEpisodeStartInProgress)
            yield break;

        isEpisodeStartInProgress = true;
        SetFlowState(FlowState.StartingEpisode, "StartEpisode");
        
        // 세션 ID가 없으면 생성
        if (string.IsNullOrEmpty(currentSessionId))
        {
            currentSessionId = "episode_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Debug.Log($"[LoveConversationUI] Generated session ID: {currentSessionId}");
        }
            
        Debug.Log($"[LoveConversationUI] Starting episode {currentEpisodeId}...");
        
        string url = $"{serverUrl}/episode/start";
        
        var requestData = new EpisodeStartRequest
        {
            episode_id = currentEpisodeId,
            player_id = currentSessionId,
            npc_id = currentNpcId
        };
        
        string jsonData = JsonUtility.ToJson(requestData);

        int attempt = 0;
        bool success = false;
        string lastError = null;

        while (attempt < Mathf.Max(1, episodeStartMaxRetries) && !success)
        {
            attempt++;
            SetTransientStatus($"에피소드 시작 시도 {attempt}/{episodeStartMaxRetries}...");

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 10;
                
                yield return www.SendWebRequest();
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    if (!TryParseEpisodeStartResponse(www.downloadHandler.text, out var response))
                    {
                        lastError = "응답 파싱 실패";
                        Debug.LogError("[LoveConversationUI] Episode start parse failed");
                    }
                    else
                    {
                        HandleEpisodeStartSuccess(response);
                        success = true;
                        break;
                    }
                }
                else
                {
                    lastError = www.error;
                    Debug.LogError($"[LoveConversationUI] Episode start request failed (try {attempt}): {www.error}");
                }
            }

            if (!success && attempt < episodeStartMaxRetries)
            {
                SetTransientStatus($"서버 연결 재시도 대기 중... ({episodeStartRetryDelay:F1}s)");
                yield return new WaitForSeconds(episodeStartRetryDelay);
            }
        }

        isEpisodeStartInProgress = false;

        if (!success)
        {
            string status = string.IsNullOrEmpty(lastError) ? "서버 연결 실패" : $"서버 연결 실패: {lastError}";
            SetFlowError("Episode start request failed", status);
        }
    }
    
    /// <summary>
    /// 상황 패널 표시
    /// </summary>
    private void ShowSituationPanel(SituationData situation)
    {
        if (situationPanel == null)
            return;

        SetFlowState(FlowState.ShowingSituation, situation?.situation_id ?? "unknown");

        if (npcResponseText != null)
        {
            npcResponseText.text = "";
        }
        
        var panelData = ToSituationPanelData(situation);
        situationPanel.ShowSituation(panelData);
    }
    
    /// <summary>
    /// 상황 패널 확인 버튼 클릭 시
    /// </summary>
    private void OnSituationConfirmed()
    {
        HandleSituationDecision("confirmed");
    }
    
    /// <summary>
    /// 상황 패널 건너뛰기 버튼 클릭 시
    /// </summary>
    private void OnSituationSkipped()
    {
        HandleSituationDecision("skipped");
    }

    private void HandleSituationDecision(string action)
    {
        if (currentFlowState != FlowState.ShowingSituation)
        {
            Debug.LogWarning($"[LoveConversationUI] Ignore situation {action} in state: {currentFlowState}");
            return;
        }

        Debug.Log($"[LoveConversationUI] Situation {action}");

        if (situationPanel == null)
        {
            SetFlowError("Situation panel reference missing", "상황 패널 참조가 없습니다.");
            return;
        }

        var situation = situationPanel.GetCurrentSituation();
        if (!TryExtractNpcPrompt(situation, out var dialogue, out var emotion))
        {
            SetFlowError("Current situation data missing", "상황 데이터가 비어 있습니다.");
            return;
        }

        StartNpcDialogue(dialogue, emotion);
    }

    private SituationPanelController.SituationData ToSituationPanelData(SituationData situation)
    {
        return new SituationPanelController.SituationData
        {
            situation_id = situation.situation_id,
            phase = situation.phase,
            situation_text = situation.situation_text,
            intro_dialogue_text = GetSituationIntroDialogueText(situation),
            npc_dialogue = new SituationPanelController.NpcDialogue
            {
                text = situation.npc_dialogue?.text ?? "",
                emotion = situation.npc_dialogue?.emotion ?? "neutral",
                tone = situation.npc_dialogue?.tone ?? "",
                gesture = situation.npc_dialogue?.gesture ?? "",
                eye_contact = situation.npc_dialogue?.eye_contact ?? ""
            },
            context = new SituationPanelController.ContextData
            {
                location = situation.context?.location ?? "",
                location_detail = situation.context?.location_detail ?? "",
                time = situation.context?.time ?? "",
                day = situation.context?.day ?? "",
                weather = situation.context?.weather ?? "",
                npc_state = situation.context?.npc_state ?? "",
                npc_activity = situation.context?.npc_activity ?? ""
            }
        };
    }

    private string GetSituationIntroDialogueText(SituationData situation)
    {
        if (situation == null || string.IsNullOrWhiteSpace(situation.intro_dialogue_text))
            return "";

        return situation.intro_dialogue_text.Trim();
    }

    private bool TryExtractNpcPrompt(SituationData situation, out string dialogue, out string emotion)
    {
        dialogue = null;
        emotion = null;

        if (situation == null || situation.npc_dialogue == null || string.IsNullOrWhiteSpace(situation.npc_dialogue.text))
            return false;

        dialogue = situation.npc_dialogue.text;
        emotion = string.IsNullOrWhiteSpace(situation.npc_dialogue.emotion) ? "neutral" : situation.npc_dialogue.emotion;
        return true;
    }

    private bool TryExtractNpcPrompt(SituationPanelController.SituationData situation, out string dialogue, out string emotion)
    {
        dialogue = null;
        emotion = null;

        if (situation == null || situation.npc_dialogue == null || string.IsNullOrWhiteSpace(situation.npc_dialogue.text))
            return false;

        dialogue = situation.npc_dialogue.text;
        emotion = string.IsNullOrWhiteSpace(situation.npc_dialogue.emotion) ? "neutral" : situation.npc_dialogue.emotion;
        return true;
    }
    
    /// <summary>
    /// NPC 대화 시작 (NPC가 먼저 말을 걸음)
    /// </summary>
    private void StartNpcDialogue(string dialogue, string emotion)
    {
        Debug.Log($"[LoveConversationUI] NPC starts dialogue: {dialogue}");
        SetFlowState(FlowState.WaitingPlayerResponse, "NPC prompt shown");

        ShowNpcFeedbackText(dialogue, emotion);
        AppendConversationTurn("", emotion, 0f, dialogue);

        UpdateConversationDisplay();
        SetTransientStatus("NPC가 말을 걸었습니다. 응답해주세요.");

        BeginAutoRecordingForPlayerResponse();
    }

    private void ShowNpcFeedbackText(string dialogue, string emotion)
    {
        SetNpcOutputText(dialogue);

        if (emotionText != null)
        {
            emotionText.text = emotion;
        }
    }

    private void AppendConversationTurn(string playerText, string emotion, float score, string npcResponse)
    {
        var turn = new ConversationTurn
        {
            playerText = playerText,
            emotion = emotion,
            score = score,
            npcResponse = npcResponse,
            timestamp = System.DateTime.Now.ToString("HH:mm:ss")
        };
        conversationHistory.Add(turn);
    }

    private void BeginAutoRecordingForPlayerResponse()
    {
        isAdvancePending = false;
        pendingTransitionResult = null;

        if (autoRecording != null && useAutoRecording)
        {
            autoRecording.StartRecordingMode();
        }
    }

    private string NormalizeTranscript(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string trimmed = raw.Trim();
        return trimmed == "(no transcript)" ? "" : trimmed;
    }

    private void SetNpcOutputText(string text)
    {
        if (npcResponseText != null)
        {
            npcResponseText.text = text;
        }
    }

    private void SetSituationOverlayMode(bool showSituationPanel)
    {
        if (responseDisplayRoot != null)
            responseDisplayRoot.SetActive(!showSituationPanel);

        if (conversationDisplayRoot != null)
            conversationDisplayRoot.SetActive(!showSituationPanel);
    }

    private void SetFlowState(FlowState nextState, string reason = null)
    {
        if (currentFlowState == nextState)
            return;

        currentFlowState = nextState;
        ApplyStatusForFlowState(nextState);

        if (string.IsNullOrEmpty(reason))
            Debug.Log($"[LoveConversationUI] Flow state -> {nextState}");
        else
            Debug.Log($"[LoveConversationUI] Flow state -> {nextState} ({reason})");
    }

    private void SetFlowError(string reason, string statusMessage)
    {
        SetTransientStatus(statusMessage);
        SetFlowState(FlowState.Error, reason);
    }

    private void SetTransientStatus(string message)
    {
        UpdateStatus(message);
    }

    private void OnRecorderStatus(string message)
    {
        if (currentFlowState == FlowState.WaitingPlayerResponse || currentFlowState == FlowState.ProcessingAudio)
        {
            SetTransientStatus(message);
        }
    }

    private void ApplyStatusForFlowState(FlowState state)
    {
        string mappedStatus = GetFlowStateStatusText(state);
        if (!string.IsNullOrEmpty(mappedStatus))
        {
            UpdateStatus(mappedStatus);
        }
    }

    private string GetFlowStateStatusText(FlowState state)
    {
        switch (state)
        {
            case FlowState.ShowingSituation:
                return "상황 준비 중...";
            case FlowState.WaitingPlayerResponse:
                return "자동 녹음 중입니다. 응답을 말해주세요.";
            case FlowState.ProcessingAudio:
                return "응답 분석 중...";
            case FlowState.ShowingFeedback:
                return isAdvancePending
                    ? "클릭 또는 Space 키로 다음 단계로 진행하세요."
                    : "NPC 응답 표시 중...";
            case FlowState.TransitioningSituation:
                return "다음 상황 준비 중...";
            default:
                return null;
        }
    }

    private void RenderStatusPanel()
    {
        if (statusText == null)
        {
            Debug.Log(latestStatusMessage);
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine(latestStatusMessage);
        builder.AppendLine($"현재 점수: {currentScore:F2}");

        if (scoreLogEntries.Count > 0)
        {
            builder.AppendLine("최근 변화:");
            for (int i = 0; i < scoreLogEntries.Count; i++)
            {
                builder.AppendLine(scoreLogEntries[i]);
            }
        }

        statusText.text = builder.ToString().TrimEnd();
    }

    private void AddScoreLogEntry(float score, float moodChange)
    {
        string entry = $" - {System.DateTime.Now:HH:mm:ss} 점수 {score:F2} (Δ{moodChange:+0.00;-0.00;0.00})";
        scoreLogEntries.Insert(0, entry);

        if (scoreLogEntries.Count > ScoreLogLimit)
        {
            scoreLogEntries.RemoveAt(scoreLogEntries.Count - 1);
        }

        RenderStatusPanel();
    }
    
    #endregion
}
