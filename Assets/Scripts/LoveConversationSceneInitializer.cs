using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// LoveConversationScene 자동 초기화 스크립트
/// 
/// 씬 로드 시 자동으로 모든 UI 요소를 생성합니다.
/// 이 스크립트를 빈 GameObject에 추가하고 Play하면 됩니다.
/// </summary>
public class LoveConversationSceneInitializer : MonoBehaviour
{
    private GameObject canvasGO;
    private LoveConversationUI conversationUI;

    void Start()
    {
        Debug.Log("=== LoveConversationScene Initializer ===");
        Debug.Log("Creating Canvas and UI elements...");

        // 기존 Canvas 확인
        var existingCanvas = FindObjectOfType<Canvas>();
        if (existingCanvas != null)
        {
            Debug.Log("Canvas already exists, skipping creation");
            canvasGO = existingCanvas.gameObject;
        }
        else
        {
            canvasGO = CreateCanvas();
        }

        SetupManager();
        Debug.Log("=== Scene Setup Complete ===");
    }

    private GameObject CreateCanvas()
    {
        var canvasGO = new GameObject("LoveConversationCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);

        var graphicRaycaster = canvasGO.AddComponent<GraphicRaycaster>();

        var rectTransform = canvasGO.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        // Background Panel
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Main Layout
        var layoutGO = new GameObject("MainLayout");
        layoutGO.transform.SetParent(canvasGO.transform, false);
        var layoutGroup = layoutGO.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);
        layoutGroup.spacing = 15;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;

        var layoutRect = layoutGO.GetComponent<RectTransform>();
        layoutRect.anchorMin = Vector2.zero;
        layoutRect.anchorMax = Vector2.one;
        layoutRect.offsetMin = Vector2.zero;
        layoutRect.offsetMax = Vector2.zero;

        // Header
        CreateHeader(layoutGO);

        // Control Section
        CreateControlSection(layoutGO);

        // Conversation Display Section
        CreateConversationSection(layoutGO);

        // Info Section (Emotion + Context)
        CreateInfoSection(layoutGO);

        return canvasGO;
    }

    private void CreateHeader(GameObject parent)
    {
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(parent.transform, false);

        var headerText = headerGO.AddComponent<TextMeshProUGUI>();
        headerText.text = "❤️ LOVE CONVERSATION TEST";
        headerText.fontSize = 40;
        headerText.color = new Color(1, 0.8f, 0.8f, 1);
        headerText.alignment = TextAlignmentOptions.Center;

        var headerRect = headerGO.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(0, 60);
    }

    private void CreateControlSection(GameObject parent)
    {
        var sectionGO = new GameObject("ControlSection");
        sectionGO.transform.SetParent(parent.transform, false);

        var sectionLayout = sectionGO.AddComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 10;
        sectionLayout.childForceExpandHeight = false;

        var sectionRect = sectionGO.GetComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(0, 150);

        // Title
        CreateSectionTitle("🎤 RECORDING CONTROL", sectionGO);

        // Status Text
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(sectionGO.transform, false);
        var statusText = statusGO.AddComponent<TextMeshProUGUI>();
        statusText.text = "Status: Ready";
        statusText.fontSize = 20;
        statusText.color = Color.white;
        var statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(0, 30);

        // Button Container
        var buttonContainerGO = new GameObject("ButtonContainer");
        buttonContainerGO.transform.SetParent(sectionGO.transform, false);
        var buttonLayout = buttonContainerGO.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10;
        buttonLayout.childForceExpandHeight = true;
        buttonLayout.childForceExpandWidth = true;

        var buttonContainerRect = buttonContainerGO.GetComponent<RectTransform>();
        buttonContainerRect.sizeDelta = new Vector2(0, 60);

        CreateButton("▶ START", "StartButton", buttonContainerGO, new Color(0.2f, 0.8f, 0.2f, 1));
        CreateButton("⏹ STOP", "StopButton", buttonContainerGO, new Color(0.8f, 0.2f, 0.2f, 1));

        // Session ID Input
        CreateLabeledInput("Session ID:", "SessionIdInput", sectionGO);
    }

    private void CreateConversationSection(GameObject parent)
    {
        var sectionGO = new GameObject("ConversationSection");
        sectionGO.transform.SetParent(parent.transform, false);

        var sectionLayout = sectionGO.AddComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 10;
        sectionLayout.childForceExpandHeight = false;

        var sectionRect = sectionGO.GetComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(0, 400);

        // Title
        CreateSectionTitle("💬 CONVERSATION HISTORY", sectionGO);

        // Display Area
        var displayGO = new GameObject("ConversationDisplayText");
        displayGO.transform.SetParent(sectionGO.transform, false);

        // Scrollable text
        var scrollViewGO = new GameObject("ScrollView");
        scrollViewGO.transform.SetParent(displayGO.transform, false);
        var scrollRect = scrollViewGO.AddComponent<ScrollRect>();

        var scrollViewRect = scrollViewGO.GetComponent<RectTransform>();
        scrollViewRect.sizeDelta = new Vector2(0, 300);

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollViewGO.transform, false);
        var viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0);

        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);

        var displayText = content.AddComponent<TextMeshProUGUI>();
        displayText.text = "=== Conversation History ===\n(대화가 여기에 표시됩니다)";
        displayText.fontSize = 18;
        displayText.color = Color.white;

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;

        // NPC Response Display
        var npcGO = new GameObject("NpcResponseText");
        npcGO.transform.SetParent(sectionGO.transform, false);
        var npcText = npcGO.AddComponent<TextMeshProUGUI>();
        npcText.text = "NPC Response: (waiting...)";
        npcText.fontSize = 20;
        npcText.color = new Color(1, 0.8f, 0.8f, 1);
        var npcRect = npcGO.GetComponent<RectTransform>();
        npcRect.sizeDelta = new Vector2(0, 50);
    }

    private void CreateInfoSection(GameObject parent)
    {
        var sectionGO = new GameObject("InfoSection");
        sectionGO.transform.SetParent(parent.transform, false);

        var sectionLayout = sectionGO.AddComponent<HorizontalLayoutGroup>();
        sectionLayout.spacing = 20;
        sectionLayout.childForceExpandHeight = true;
        sectionLayout.childForceExpandWidth = true;

        var sectionRect = sectionGO.GetComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(0, 120);

        // Emotion Panel
        var emotionPanelGO = new GameObject("EmotionPanel");
        emotionPanelGO.transform.SetParent(sectionGO.transform, false);
        var emotionPanelImage = emotionPanelGO.AddComponent<Image>();
        emotionPanelImage.color = new Color(0.15f, 0.15f, 0.25f, 1);

        var emotionPanelLayout = emotionPanelGO.AddComponent<VerticalLayoutGroup>();
        emotionPanelLayout.spacing = 5;
        emotionPanelLayout.padding = new RectOffset(10, 10, 10, 10);
        emotionPanelLayout.childForceExpandHeight = false;

        CreateSectionTitle("😊 EMOTION", emotionPanelGO);
        var emotionTextGO = new GameObject("EmotionText");
        emotionTextGO.transform.SetParent(emotionPanelGO.transform, false);
        var emotionText = emotionTextGO.AddComponent<TextMeshProUGUI>();
        emotionText.text = "(analyzing...)";
        emotionText.fontSize = 16;
        emotionText.color = Color.white;

        // Context Panel
        var contextPanelGO = new GameObject("ContextPanel");
        contextPanelGO.transform.SetParent(sectionGO.transform, false);
        var contextPanelImage = contextPanelGO.AddComponent<Image>();
        contextPanelImage.color = new Color(0.15f, 0.15f, 0.25f, 1);

        var contextPanelLayout = contextPanelGO.AddComponent<VerticalLayoutGroup>();
        contextPanelLayout.spacing = 5;
        contextPanelLayout.padding = new RectOffset(10, 10, 10, 10);
        contextPanelLayout.childForceExpandHeight = false;

        CreateSectionTitle("📊 CONTEXT", contextPanelGO);
        var contextTextGO = new GameObject("ContextText");
        contextTextGO.transform.SetParent(contextPanelGO.transform, false);
        var contextText = contextTextGO.AddComponent<TextMeshProUGUI>();
        contextText.text = "(waiting...)";
        contextText.fontSize = 16;
        contextText.color = Color.white;
    }

    private void CreateSectionTitle(string title, GameObject parent)
    {
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(parent.transform, false);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 24;
        titleText.color = new Color(1, 0.9f, 0.7f, 1);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 30);
    }

    private void CreateButton(string label, string name, GameObject parent, Color color)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent.transform, false);

        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = color;

        var buttonComponent = buttonGO.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;

        var buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(200, 60);

        // Add Colors to Button
        var colors = buttonComponent.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        buttonComponent.colors = colors;

        // Text Child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        var tmpText = textGO.AddComponent<TextMeshProUGUI>();
        tmpText.text = label;
        tmpText.fontSize = 22;
        tmpText.color = Color.white;
        tmpText.alignment = TextAlignmentOptions.Center;

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void CreateLabeledInput(string label, string name, GameObject parent)
    {
        var containerGO = new GameObject("LabeledInput");
        containerGO.transform.SetParent(parent.transform, false);

        var containerLayout = containerGO.AddComponent<HorizontalLayoutGroup>();
        containerLayout.spacing = 10;
        containerLayout.childForceExpandHeight = true;

        var containerRect = containerGO.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(0, 40);

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(containerGO.transform, false);
        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 18;
        labelText.color = Color.white;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(150, 40);

        // Input Field
        var inputGO = new GameObject(name);
        inputGO.transform.SetParent(containerGO.transform, false);

        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = inputGO.GetComponent<RectTransform>();

        var inputImage = inputGO.AddComponent<Image>();
        inputImage.color = new Color(0.1f, 0.1f, 0.15f, 1);

        // Input Text
        var textGO = new GameObject("Text Area");
        textGO.transform.SetParent(inputGO.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var textComponent = textGO.AddComponent<TextMeshProUGUI>();
        textComponent.text = "";
        textComponent.fontSize = 18;
        textComponent.color = Color.white;

        inputField.textComponent = textComponent;
        inputField.text = "";
    }

    private void SetupManager()
    {
        var managerGO = new GameObject("LoveConversationManager");
        managerGO.transform.SetParent(canvasGO.transform, false);

        var loveUI = managerGO.AddComponent<LoveConversationUI>();
        var recorder = managerGO.AddComponent<UnityMicRecorder>();

        // Find and assign all UI elements
        AssignUIElements(loveUI, canvasGO);

        Debug.Log("Manager setup complete. UI elements assigned.");
        Debug.Log($"  startButton: {(loveUI.startButton != null ? "✓" : "✗")}");
        Debug.Log($"  stopButton: {(loveUI.stopButton != null ? "✓" : "✗")}");
        Debug.Log($"  statusText: {(loveUI.statusText != null ? "✓" : "✗")}");
        Debug.Log($"  conversationDisplayText: {(loveUI.conversationDisplayText != null ? "✓" : "✗")}");
        Debug.Log($"  npcResponseText: {(loveUI.npcResponseText != null ? "✓" : "✗")}");
        Debug.Log($"  emotionText: {(loveUI.emotionText != null ? "✓" : "✗")}");
        Debug.Log($"  contextText: {(loveUI.contextText != null ? "✓" : "✗")}");
    }

    private void AssignUIElements(LoveConversationUI loveUI, GameObject canvas)
    {
        // Find all required components
        foreach (var button in canvas.GetComponentsInChildren<Button>())
        {
            if (button.gameObject.name == "StartButton")
                loveUI.startButton = button;
            else if (button.gameObject.name == "StopButton")
                loveUI.stopButton = button;
        }

        foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (text.gameObject.name == "StatusText")
                loveUI.statusText = text;
            else if (text.gameObject.name == "ConversationDisplayText")
                loveUI.conversationDisplayText = text;
            else if (text.gameObject.name == "NpcResponseText")
                loveUI.npcResponseText = text;
            else if (text.gameObject.name == "EmotionText")
                loveUI.emotionText = text;
            else if (text.gameObject.name == "ContextText")
                loveUI.contextText = text;
        }

        foreach (var inputField in canvas.GetComponentsInChildren<TMP_InputField>())
        {
            if (inputField.gameObject.name == "SessionIdInput")
                loveUI.sessionIdInput = inputField;
        }

        foreach (var dropdown in canvas.GetComponentsInChildren<TMP_Dropdown>())
        {
            if (dropdown.gameObject.name == "NpcPersonalityDropdown")
                loveUI.npcPersonalityDropdown = dropdown;
        }
    }
}
