using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

/// <summary>
/// LLM Configuration Editor Window
/// 
/// Allows users to configure LLM settings directly in the Unity Editor
/// without creating a separate scene.
/// </summary>
public class LLMConfigWindow : EditorWindow
{
    private string serverUrl = "http://127.0.0.1:5000";
    private string currentProvider = "claude";
    private string currentApiKey = "";
    private string currentModel = "claude-3-5-sonnet-20241022";
    private string statusMessage = "준비 완료";
    private Color statusColor = Color.white;
    
    private Vector2 scrollPosition = Vector2.zero;
    private bool isLoading = false;
    
    private Dictionary<string, List<string>> providerModels = new Dictionary<string, List<string>>
    {
        { "claude", new List<string> { "claude-3-5-sonnet-20241022", "claude-3-opus-20250219" } },
        { "openai", new List<string> { "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo" } },
        { "ollama", new List<string> { "mistral", "llama2", "neural-chat" } },
        { "gemini", new List<string> { "gemini-pro", "gemini-1.5-pro" } }
    };
    
    [MenuItem("Tools/LLM Configuration")]
    public static void ShowWindow()
    {
        GetWindow<LLMConfigWindow>("LLM Config");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("⚙️ LLM CONFIGURATION", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        
        // Server URL
        GUILayout.Label("Server Configuration", EditorStyles.boldLabel);
        serverUrl = EditorGUILayout.TextField("Server URL:", serverUrl);
        GUILayout.Space(10);
        
        // Provider Selection
        GUILayout.Label("LLM Provider Settings", EditorStyles.boldLabel);
        
        string[] providers = { "claude", "openai", "ollama", "gemini" };
        string[] providerLabels = { "Claude (Anthropic)", "OpenAI (GPT-4)", "Ollama (Local)", "Google Gemini" };
        
        int currentIndex = System.Array.IndexOf(providers, currentProvider);
        int newIndex = EditorGUILayout.Popup("LLM Provider:", currentIndex, providerLabels);
        
        if (newIndex != currentIndex)
        {
            currentProvider = providers[newIndex];
            currentApiKey = "";
            
            // Update model list
            if (providerModels.ContainsKey(currentProvider))
            {
                currentModel = providerModels[currentProvider][0];
            }
        }
        
        // API Key Input
        EditorGUILayout.LabelField("API Key:", EditorStyles.label);
        currentApiKey = EditorGUILayout.PasswordField(currentApiKey);
        
        // Model Selection
        if (providerModels.ContainsKey(currentProvider))
        {
            List<string> models = providerModels[currentProvider];
            int modelIndex = models.IndexOf(currentModel);
            if (modelIndex < 0) modelIndex = 0;
            
            modelIndex = EditorGUILayout.Popup("Model:", modelIndex, models.ToArray());
            currentModel = models[modelIndex];
        }
        
        GUILayout.Space(15);
        
        // Status Display
        GUILayout.Label("Status", EditorStyles.boldLabel);
        
        GUI.color = statusColor;
        EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        GUI.color = Color.white;
        
        GUILayout.Space(15);
        
        // Buttons
        GUILayout.BeginHorizontal();
        
        GUI.enabled = !isLoading;
        
        if (GUILayout.Button("🧪 TEST", GUILayout.Height(40)))
        {
            TestConfiguration();
        }
        
        if (GUILayout.Button("💾 SAVE", GUILayout.Height(40)))
        {
            SaveConfiguration();
        }
        
        if (GUILayout.Button("↩️ LOAD", GUILayout.Height(40)))
        {
            LoadConfiguration();
        }
        
        GUI.enabled = true;
        
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // Info
        EditorGUILayout.HelpBox(
            "Configure your LLM provider settings here.\n\n" +
            "• Claude: Requires ANTHROPIC_API_KEY\n" +
            "• OpenAI: Requires OPENAI_API_KEY\n" +
            "• Ollama: Requires local Ollama server\n" +
            "• Gemini: Requires GOOGLE_API_KEY",
            MessageType.Info
        );
        
        GUILayout.EndScrollView();
    }
    
    private void TestConfiguration()
    {
        isLoading = true;
        statusMessage = "테스트 중...";
        statusColor = Color.yellow;
        
        // Create test request with selected provider
        var testData = new { provider = currentProvider };
        string jsonData = JsonUtility.ToJson(testData);
        
        Debug.Log($"[LLMConfigWindow] Testing provider: {currentProvider}");
        Debug.Log($"[LLMConfigWindow] Request JSON: {jsonData}");
        
        // Send test request
        var request = new UnityWebRequest($"{serverUrl}/config/llm/test", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        var operation = request.SendWebRequest();
        
        // Timeout handling
        float timeout = 5f;
        float elapsed = 0f;
        EditorApplication.CallbackFunction updateHandler = null;
        
        updateHandler = () =>
        {
            elapsed += Time.deltaTime;
            
            if (operation.isDone || elapsed > timeout)
            {
                EditorApplication.update -= updateHandler;
                
                if (elapsed > timeout)
                {
                    statusMessage = "연결 시간 초과 (5초)";
                    statusColor = Color.red;
                    Debug.LogError("[LLMConfigWindow] Test timeout after 5 seconds");
                }
                else if (request.result == UnityWebRequest.Result.Success)
                {
                    statusMessage = "[OK] 연결 성공!";
                    statusColor = Color.green;
                    Debug.Log($"[LLMConfigWindow] Test successful for {currentProvider}");
                }
                else
                {
                    statusMessage = $"[ERROR] 연결 실패: {request.error}";
                    statusColor = Color.red;
                    Debug.LogError($"[LLMConfigWindow] Test failed: {request.error}");
                    
                    // Try to parse error response
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.LogError($"[LLMConfigWindow] Response: {responseText}");
                    }
                    catch { }
                }
                
                isLoading = false;
                request.Dispose();
                Repaint();
            }
        };
        
        EditorApplication.update += updateHandler;
    }
    
    private void SaveConfiguration()
    {
        isLoading = true;
        statusMessage = "저장 중...";
        statusColor = Color.yellow;
        
        // Create config data using serializable class
        var configData = new LLMConfigData
        {
            llm_provider = currentProvider,
            anthropic_api_key = currentProvider == "claude" ? currentApiKey : "",
            openai_api_key = currentProvider == "openai" ? currentApiKey : "",
            google_api_key = currentProvider == "gemini" ? currentApiKey : "",
            ollama_base_url = "http://localhost:11434",
            ollama_model = "mistral"
        };
        
        string jsonData = JsonUtility.ToJson(configData);
        
        Debug.Log($"[LLMConfigWindow] Saving configuration:");
        Debug.Log($"[LLMConfigWindow] Provider: {currentProvider}");
        Debug.Log($"[LLMConfigWindow] API Key: {(string.IsNullOrEmpty(currentApiKey) ? "[EMPTY]" : "[SET]")}");
        Debug.Log($"[LLMConfigWindow] JSON Data: {jsonData}");
        
        // Send save request
        var request = new UnityWebRequest($"{serverUrl}/config/llm", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        var operation = request.SendWebRequest();
        
        EditorApplication.CallbackFunction updateHandler = null;
        updateHandler = () =>
        {
            if (operation.isDone)
            {
                EditorApplication.update -= updateHandler;
                
                if (request != null && request.result == UnityWebRequest.Result.Success)
                {
                    statusMessage = "[OK] 저장 완료!";
                    statusColor = Color.green;
                    Debug.Log("[LLMConfigWindow] Configuration saved successfully");
                    
                    // Parse response to show saved path
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.Log($"[LLMConfigWindow] Server response: {responseText}");
                    }
                    catch { }
                }
                else if (request != null)
                {
                    statusMessage = $"[ERROR] 저장 실패: {request.error}";
                    statusColor = Color.red;
                    Debug.LogError($"[LLMConfigWindow] Save failed: {request.error}");
                    
                    // Try to parse error response
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.LogError($"[LLMConfigWindow] Error response: {responseText}");
                    }
                    catch { }
                }
                
                isLoading = false;
                if (request != null)
                {
                    request.Dispose();
                }
                Repaint();
            }
        };
        
        EditorApplication.update += updateHandler;
    }
    
    private void LoadConfiguration()
    {
        isLoading = true;
        statusMessage = "로드 중...";
        statusColor = Color.yellow;
        
        // Send load request
        var request = UnityWebRequest.Get($"{serverUrl}/config/llm");
        var operation = request.SendWebRequest();
        
        EditorApplication.CallbackFunction updateHandler = null;
        updateHandler = () =>
        {
            if (operation.isDone)
            {
                EditorApplication.update -= updateHandler;
                
                if (request != null && request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        LLMConfig config = JsonUtility.FromJson<LLMConfig>(json);
                        
                        currentProvider = config.llm_provider;
                        
                        // Load API key
                        if (!string.IsNullOrEmpty(config.anthropic_api_key))
                            currentApiKey = config.anthropic_api_key;
                        else if (!string.IsNullOrEmpty(config.openai_api_key))
                            currentApiKey = config.openai_api_key;
                        else if (!string.IsNullOrEmpty(config.google_api_key))
                            currentApiKey = config.google_api_key;
                        
                        statusMessage = "✓ 로드 완료!";
                        statusColor = Color.green;
                        Debug.Log("[LLMConfigWindow] Configuration loaded successfully");
                    }
                    catch (System.Exception e)
                    {
                        statusMessage = $"✗ 파싱 실패: {e.Message}";
                        statusColor = Color.red;
                        Debug.LogError($"[LLMConfigWindow] Parse failed: {e.Message}");
                    }
                }
                else if (request != null)
                {
                    statusMessage = $"✗ 로드 실패: {request.error}";
                    statusColor = Color.red;
                    Debug.LogError($"[LLMConfigWindow] Load failed: {request.error}");
                }
                
                isLoading = false;
                if (request != null)
                {
                    request.Dispose();
                }
                Repaint();
            }
        };
        
        EditorApplication.update += updateHandler;
    }
    
    [System.Serializable]
    public class LLMConfigData
    {
        public string llm_provider;
        public string anthropic_api_key;
        public string openai_api_key;
        public string google_api_key;
        public string ollama_base_url;
        public string ollama_model;
    }
    
    [System.Serializable]
    public class LLMConfig
    {
        public string llm_provider;
        public string anthropic_api_key;
        public string openai_api_key;
        public string google_api_key;
        public string ollama_base_url;
        public string ollama_model;
    }
}
