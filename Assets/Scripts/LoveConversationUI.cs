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
    private List<ConversationTurn> conversationHistory = new List<ConversationTurn>();
    
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
        public string variation_type;
        public bool was_force_llm;
        public string session_id;
    }
    
    [System.Serializable]
    private class ConversationStatusResponse
    {
        public int total_turns;
        public float average_score;
        public string[] repeated_emotions;
    }

    void Awake()
    {
        recorder = GetComponent<UnityMicRecorder>();
        recorder.OnServerResponse = OnAnalyzeResponse;
        recorder.OnStatus = (s) => UpdateStatus(s);
    }

    void Start()
    {
        // Wire up buttons
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.AddListener(OnStopClicked);
        
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
        
        UpdateStatus("Ready");
        UpdateConversationDisplay();
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopClicked);
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
        UpdateStatus("Processing...");
        recorder.StopAndSend();
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
        Debug.Log($"NPC Feedback: {feedback.feedback} (LLM: {feedback.was_force_llm})");
        
        // Display NPC response
        if (npcResponseText != null)
        {
            npcResponseText.text = feedback.feedback;
        }
        
        // Add to conversation history
        var turn = new ConversationTurn
        {
            playerText = currentTranscript,
            emotion = currentEmotion,
            score = currentScore,
            npcResponse = feedback.feedback,
            timestamp = System.DateTime.Now.ToString("HH:mm:ss")
        };
        conversationHistory.Add(turn);
        
        UpdateConversationDisplay();
        UpdateStatus($"NPC: {feedback.variation_type}" + (feedback.was_force_llm ? " (LLM)" : " (Rules)"));
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

    [System.Serializable]
    private class FeedbackRequest
    {
        public string session_id;
        public string transcript;
        public string emotion;
        public float score;
        public float audio_score;
    }
}
