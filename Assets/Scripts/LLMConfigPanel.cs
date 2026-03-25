using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// LLM Configuration Panel
/// 
/// Allows users to:
/// - Select LLM provider (Claude, OpenAI, Ollama, Gemini)
/// - Input API keys
/// - Select models
/// - Test configuration
/// - Save settings
/// </summary>
public class LLMConfigPanel : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Dropdown providerDropdown;
    public TMP_InputField apiKeyInput;
    public TMP_Dropdown modelDropdown;
    public Button testButton;
    public Button saveButton;
    public Button cancelButton;
    public TMP_Text statusText;
    public Image statusIcon;
    
    [Header("Server Configuration")]
    public string serverUrl = "http://127.0.0.1:5000";
    
    [Header("Colors")]
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    public Color neutralColor = Color.white;
    
    private string currentProvider = "claude";
    private string currentApiKey = "";
    private Dictionary<string, List<string>> providerModels;
    
    void Start()
    {
        InitializeUI();
        LoadConfig();
        RegisterButtonListeners();
    }
    
    void InitializeUI()
    {
        // Initialize provider dropdown
        providerDropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Claude (Anthropic)"),
            new TMP_Dropdown.OptionData("OpenAI (GPT-4)"),
            new TMP_Dropdown.OptionData("Ollama (Local)"),
            new TMP_Dropdown.OptionData("Google Gemini")
        };
        providerDropdown.onValueChanged.AddListener(OnProviderChanged);
        
        // Initialize model dropdown
        InitializeModelDropdown();
        
        // Initialize status text
        if (statusText != null)
        {
            statusText.text = "준비 완료";
            statusText.color = neutralColor;
        }
    }
    
    void InitializeModelDropdown()
    {
        providerModels = new Dictionary<string, List<string>>
        {
            { "claude", new List<string> { "claude-3-5-sonnet-20241022", "claude-3-opus-20250219" } },
            { "openai", new List<string> { "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo" } },
            { "ollama", new List<string> { "mistral", "llama2", "neural-chat" } },
            { "gemini", new List<string> { "gemini-pro", "gemini-1.5-pro" } }
        };
        
        UpdateModelDropdown("claude");
    }
    
    void OnProviderChanged(int index)
    {
        string[] providers = { "claude", "openai", "ollama", "gemini" };
        currentProvider = providers[index];
        UpdateModelDropdown(currentProvider);
        
        // Clear API key input for new provider
        apiKeyInput.text = "";
        
        UpdateStatus($"{currentProvider} 선택됨", neutralColor);
    }
    
    void UpdateModelDropdown(string provider)
    {
        modelDropdown.options.Clear();
        
        if (providerModels.ContainsKey(provider))
        {
            foreach (var model in providerModels[provider])
            {
                modelDropdown.options.Add(new TMP_Dropdown.OptionData(model));
            }
        }
        
        modelDropdown.value = 0;
    }
    
    void RegisterButtonListeners()
    {
        if (testButton != null)
            testButton.onClick.AddListener(OnTestClicked);
        
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }
    
    void LoadConfig()
    {
        StartCoroutine(LoadConfigCoroutine());
    }
    
    IEnumerator LoadConfigCoroutine()
    {
        UpdateStatus("설정 로드 중...", neutralColor);
        
        using (UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/config/llm"))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = www.downloadHandler.text;
                    LLMConfig config = JsonUtility.FromJson<LLMConfig>(json);
                    
                    // Update UI with loaded config
                    int providerIndex = GetProviderIndex(config.llm_provider);
                    providerDropdown.value = providerIndex;
                    currentProvider = config.llm_provider;
                    
                    // Mask API key for display
                    if (!string.IsNullOrEmpty(config.anthropic_api_key))
                    {
                        apiKeyInput.text = MaskApiKey(config.anthropic_api_key);
                        currentApiKey = config.anthropic_api_key;
                    }
                    else if (!string.IsNullOrEmpty(config.openai_api_key))
                    {
                        apiKeyInput.text = MaskApiKey(config.openai_api_key);
                        currentApiKey = config.openai_api_key;
                    }
                    else if (!string.IsNullOrEmpty(config.google_api_key))
                    {
                        apiKeyInput.text = MaskApiKey(config.google_api_key);
                        currentApiKey = config.google_api_key;
                    }
                    
                    UpdateStatus("✓ 설정 로드 완료", successColor);
                }
                catch (System.Exception e)
                {
                    UpdateStatus($"✗ 설정 파싱 실패: {e.Message}", errorColor);
                }
            }
            else
            {
                UpdateStatus($"✗ 설정 로드 실패: {www.error}", errorColor);
            }
        }
    }
    
    void OnTestClicked()
    {
        StartCoroutine(TestConfigCoroutine());
    }
    
    IEnumerator TestConfigCoroutine()
    {
        UpdateStatus("테스트 중...", neutralColor);
        
        var testData = new { provider = currentProvider };
        string jsonData = JsonUtility.ToJson(testData);
        
        using (UnityWebRequest www = new UnityWebRequest($"{serverUrl}/config/llm/test", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                UpdateStatus("✓ 연결 성공!", successColor);
            }
            else
            {
                UpdateStatus($"✗ 연결 실패: {www.error}", errorColor);
            }
        }
    }
    
    void OnSaveClicked()
    {
        StartCoroutine(SaveConfigCoroutine());
    }
    
    IEnumerator SaveConfigCoroutine()
    {
        UpdateStatus("저장 중...", neutralColor);
        
        // Get API key from input
        string apiKey = apiKeyInput.text;
        
        // If it's masked, use the stored key
        if (apiKey.Contains("*"))
        {
            apiKey = currentApiKey;
        }
        else
        {
            currentApiKey = apiKey;
        }
        
        // Create config data based on provider
        var configData = new { llm_provider = currentProvider };
        
        // Add provider-specific API key
        string jsonData = "";
        if (currentProvider == "claude")
        {
            jsonData = JsonUtility.ToJson(new { llm_provider = currentProvider, anthropic_api_key = apiKey });
        }
        else if (currentProvider == "openai")
        {
            jsonData = JsonUtility.ToJson(new { llm_provider = currentProvider, openai_api_key = apiKey });
        }
        else if (currentProvider == "gemini")
        {
            jsonData = JsonUtility.ToJson(new { llm_provider = currentProvider, google_api_key = apiKey });
        }
        else if (currentProvider == "ollama")
        {
            jsonData = JsonUtility.ToJson(new { llm_provider = currentProvider });
        }
        
        using (UnityWebRequest www = new UnityWebRequest($"{serverUrl}/config/llm", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                UpdateStatus("✓ 저장 완료!", successColor);
                
                // Mask the API key after saving
                if (!string.IsNullOrEmpty(apiKey))
                {
                    apiKeyInput.text = MaskApiKey(apiKey);
                }
            }
            else
            {
                UpdateStatus($"✗ 저장 실패: {www.error}", errorColor);
            }
        }
    }
    
    void OnCancelClicked()
    {
        LoadConfig();
        UpdateStatus("취소됨", neutralColor);
    }
    
    void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        if (statusIcon != null)
        {
            statusIcon.color = color;
        }
    }
    
    int GetProviderIndex(string provider)
    {
        return provider switch
        {
            "claude" => 0,
            "openai" => 1,
            "ollama" => 2,
            "gemini" => 3,
            _ => 0
        };
    }
    
    string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            return "***";
        
        return apiKey.Substring(0, 4) + new string('*', apiKey.Length - 8) + apiKey.Substring(apiKey.Length - 4);
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
