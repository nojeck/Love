using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class CreateLoveConversationScene
{
    private static readonly Color ButtonGreenColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    private static readonly Color ButtonRedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    private static readonly Color PanelDarkColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color PanelColor = new Color(0.15f, 0.15f, 0.25f, 1f);
    private static readonly Color HeaderColor = new Color(1f, 0.8f, 0.8f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.9f, 0.7f, 1f);
    
    private static TMP_FontAsset _pairingFont;

    [MenuItem("Tools/Create/Love Conversation Scene")]
    public static void CreateScene()
    {
        // Try to load Korean font, but don't fail if not found
        string fontPath = "Assets/TextMesh Pro/Fonts/YPairingFont-Regular SDF.asset";
        _pairingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        
        if (_pairingFont == null)
        {
            // Try loading by GUID (from .meta file)
            string fontGUID = "0d08fa8f981739742b922e0612ebc35c";
            string guidPath = AssetDatabase.GUIDToAssetPath(fontGUID);
            if (!string.IsNullOrEmpty(guidPath))
            {
                _pairingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(guidPath);
                if (_pairingFont != null)
                {
                    Debug.Log($"✓ Loaded font from GUID: {guidPath}");
                }
            }
        }
        
        if (_pairingFont == null)
        {
            // Try alternative search
            string[] fontGuids = AssetDatabase.FindAssets("YPairingFont", new[] { "Assets/TextMesh Pro/Fonts" });
            if (fontGuids.Length > 0)
            {
                foreach (var guid in fontGuids)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (foundPath.Contains("Regular") && foundPath.Contains("SDF"))
                    {
                        _pairingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(foundPath);
                        if (_pairingFont != null)
                        {
                            Debug.Log($"✓ Loaded font from search: {foundPath}");
                            break;
                        }
                    }
                }
            }
        }
        
        if (_pairingFont == null)
        {
            Debug.LogWarning("⚠ YPairingFont-Regular SDF not found. Using default TMP font.");
            Debug.LogWarning($"Expected path: {fontPath}");
            
            // Show available fonts in console
            string[] allFonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/TextMesh Pro" });
            if (allFonts.Length > 0)
            {
                Debug.LogWarning("Available TMP fonts:");
                foreach (var guid in allFonts)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    Debug.LogWarning($"  - {assetPath}");
                }
            }
        }
        else
        {
            Debug.Log($"✓ Font loaded successfully: {_pairingFont.name}");
        }
        
        Debug.Log("Starting scene creation...");
        
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // Create Canvas - Full Screen (1920x1080)
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        var canvasScaler = canvasGO.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        
        // Background Panel
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.GetComponent<Image>();
        bgImage.color = PanelDarkColor;
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Main Layout - Full Size with padding
        GameObject layoutGO = new GameObject("MainLayout", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        layoutGO.transform.SetParent(canvasGO.transform, false);
        
        var layoutGroup = layoutGO.GetComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(40, 40, 40, 40);
        layoutGroup.spacing = 20;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        
        var layoutRect = layoutGO.GetComponent<RectTransform>();
        layoutRect.anchorMin = Vector2.zero;
        layoutRect.anchorMax = Vector2.one;
        layoutRect.offsetMin = Vector2.zero;
        layoutRect.offsetMax = Vector2.zero;
        
        // Header (80px height)
        CreateHeaderText("❤️ LOVE CONVERSATION TEST", layoutGO.transform, 50, HeaderColor, 80);
        
        // Control Section (180px height)
        CreateControlSection(layoutGO, 180);
        
        // Conversation Section (500px height)
        CreateConversationSection(layoutGO, 500);
        
        // Info Section (150px height)
        CreateInfoSection(layoutGO, 150);
        
        // Create Recorder object and attach scripts
        GameObject managerGO = new GameObject("LoveConversationManager");
        managerGO.transform.SetParent(null);
        
        // Add UnityMicRecorder
        var recorder = managerGO.AddComponent<UnityMicRecorder>();
        
        // Attach LoveConversationUI and wire references
        var ui = managerGO.AddComponent<LoveConversationUI>();
        
        // Find and assign all UI elements
        AssignUIElements(ui, canvasGO);
        
        // Ensure an EventSystem exists
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
        
        // Save scene
        string path = "Assets/Scenes/LoveConversationScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        
        string fontMsg = _pairingFont != null ? $"Font: ✓ {_pairingFont.name}" : "Font: ⚠ Using default TMP font";
        
        EditorUtility.DisplayDialog("Love Conversation Scene Created ✓", 
            $"Saved LoveConversationScene to Assets/Scenes/LoveConversationScene.unity\n\n" +
            $"Resolution: 1920x1080\n" +
            $"{fontMsg}\n\n" +
            $"Ready for testing! Start Server and click Play.", "OK");
        
        Debug.Log("✓ Scene creation completed successfully!");
    }
    
    private static void CreateHeaderText(string text, Transform parent, int fontSize, Color color, float height)
    {
        GameObject headerGO = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        headerGO.transform.SetParent(parent, false);
        
        var headerText = headerGO.GetComponent<TextMeshProUGUI>();
        headerText.text = text;
        headerText.fontSize = fontSize;
        headerText.color = color;
        headerText.alignment = TextAlignmentOptions.Center;
        
        if (_pairingFont != null)
            headerText.font = _pairingFont;
        
        var layoutElement = headerGO.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1;
    }
    
    private static void CreateSectionTitle(string text, GameObject parent, int fontSize)
    {
        GameObject titleGO = new GameObject("SectionTitle", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleGO.transform.SetParent(parent.transform, false);
        var titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = text;
        titleText.fontSize = fontSize;
        titleText.color = GoldColor;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        
        if (_pairingFont != null)
            titleText.font = _pairingFont;
        
        var layoutElement = titleGO.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 35;
        layoutElement.flexibleWidth = 1;
    }
    
    private static void CreateControlSection(GameObject parent, float height)
    {
        GameObject sectionGO = new GameObject("ControlSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(Image));
        sectionGO.transform.SetParent(parent.transform, false);
        
        var sectionImage = sectionGO.GetComponent<Image>();
        sectionImage.color = new Color(0, 0, 0, 0.3f);
        
        var sectionLayout = sectionGO.GetComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 15;
        sectionLayout.padding = new RectOffset(20, 20, 15, 15);
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.childForceExpandWidth = true;
        
        var layoutElement = sectionGO.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1;
        
        // Title
        CreateSectionTitle("🎤 RECORDING CONTROL", sectionGO, 28);
        
        // Status Text (30px)
        GameObject statusGO = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        statusGO.transform.SetParent(sectionGO.transform, false);
        var statusText = statusGO.GetComponent<TextMeshProUGUI>();
        statusText.text = "Status: Ready";
        statusText.fontSize = 24;
        statusText.color = TextColor;
        AssignFont(statusText);
        var statusLayout = statusGO.GetComponent<LayoutElement>();
        statusLayout.preferredHeight = 30;
        
        // Progress Bar (20px)
        GameObject progressBarGO = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        progressBarGO.transform.SetParent(sectionGO.transform, false);
        var progressBarImage = progressBarGO.GetComponent<Image>();
        progressBarImage.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        var progressBarLayout = progressBarGO.GetComponent<LayoutElement>();
        progressBarLayout.preferredHeight = 20;
        
        // Progress Fill
        GameObject progressFillGO = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        progressFillGO.transform.SetParent(progressBarGO.transform, false);
        var progressFillImage = progressFillGO.GetComponent<Image>();
        progressFillImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        var progressFillRect = progressFillGO.GetComponent<RectTransform>();
        progressFillRect.anchorMin = new Vector2(0, 0);
        progressFillRect.anchorMax = new Vector2(0, 1);
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = new Vector2(0, 0);
        
        // Button Container (70px)
        GameObject buttonContainerGO = new GameObject("ButtonContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonContainerGO.transform.SetParent(sectionGO.transform, false);
        var buttonLayout = buttonContainerGO.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 15;
        buttonLayout.padding = new RectOffset(10, 10, 5, 5);
        buttonLayout.childForceExpandHeight = true;
        buttonLayout.childForceExpandWidth = true;
        
        var buttonLayoutElement = buttonContainerGO.GetComponent<LayoutElement>();
        buttonLayoutElement.preferredHeight = 70;
        
        CreateButton("▶ START", "StartButton", buttonContainerGO.transform, ButtonGreenColor, 28);
        CreateButton("⏹ STOP", "StopButton", buttonContainerGO.transform, ButtonRedColor, 28);
        
        // Session ID Input (40px)
        CreateLabeledInput("Session ID:", "SessionIdInput", sectionGO.transform, 40);
        
        // NPC Personality Dropdown (40px)
        CreateLabeledDropdown("NPC Personality:", "NpcPersonalityDropdown", sectionGO.transform, 40);
    }
    
    private static void CreateConversationSection(GameObject parent, float height)
    {
        GameObject sectionGO = new GameObject("ConversationSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(Image));
        sectionGO.transform.SetParent(parent.transform, false);
        
        var sectionImage = sectionGO.GetComponent<Image>();
        sectionImage.color = new Color(0, 0, 0, 0.3f);
        
        var sectionLayout = sectionGO.GetComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 15;
        sectionLayout.padding = new RectOffset(20, 20, 15, 15);
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.childForceExpandWidth = true;
        
        var layoutElement = sectionGO.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1;
        
        // Title (35px)
        CreateSectionTitle("💬 CONVERSATION HISTORY", sectionGO, 28);
        
        // Scroll View for conversation display (370px)
        GameObject scrollViewGO = new GameObject("ConversationDisplay", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(LayoutElement));
        scrollViewGO.transform.SetParent(sectionGO.transform, false);
        
        var scrollImage = scrollViewGO.GetComponent<Image>();
        scrollImage.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        
        var scrollRect = scrollViewGO.GetComponent<ScrollRect>();
        var scrollViewLayout = scrollViewGO.GetComponent<LayoutElement>();
        scrollViewLayout.preferredHeight = 370;
        scrollViewLayout.flexibleWidth = 1;
        
        // Viewport
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        var viewportImage = viewportGO.GetComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0);
        var viewportMask = viewportGO.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        var viewportRect = viewportGO.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        // Content
        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentLayout = contentGO.GetComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.spacing = 5;
        
        var contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        // Text
        GameObject displayTextGO = new GameObject("ConversationDisplayText", typeof(RectTransform), typeof(TextMeshProUGUI));
        displayTextGO.transform.SetParent(contentGO.transform, false);
        var displayText = displayTextGO.GetComponent<TextMeshProUGUI>();
        displayText.text = "=== Conversation History ===\n(대화가 여기에 표시됩니다)";
        displayText.fontSize = 20;
        displayText.color = TextColor;
        AssignFont(displayText);
        displayText.alignment = TextAlignmentOptions.TopLeft;
        
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        
        // Player Transcript Display (60px)
        GameObject playerTranscriptGO = new GameObject("PlayerTranscriptText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        playerTranscriptGO.transform.SetParent(sectionGO.transform, false);
        var playerTranscriptText = playerTranscriptGO.GetComponent<TextMeshProUGUI>();
        playerTranscriptText.text = "You: (listening...)";
        playerTranscriptText.fontSize = 20;
        playerTranscriptText.color = new Color(0.8f, 0.8f, 1f, 1f);
        AssignFont(playerTranscriptText);
        var playerTranscriptLayout = playerTranscriptGO.GetComponent<LayoutElement>();
        playerTranscriptLayout.preferredHeight = 50;
        
        // NPC Response Display (60px)
        GameObject npcGO = new GameObject("NpcResponseText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        npcGO.transform.SetParent(sectionGO.transform, false);
        var npcText = npcGO.GetComponent<TextMeshProUGUI>();
        npcText.text = "NPC Response: (waiting...)";
        npcText.fontSize = 22;
        npcText.color = new Color(1, 0.8f, 0.8f, 1f);
        AssignFont(npcText);
        var npcLayout = npcGO.GetComponent<LayoutElement>();
        npcLayout.preferredHeight = 60;
    }
    
    private static void CreateInfoSection(GameObject parent, float height)
    {
        GameObject sectionGO = new GameObject("InfoSection", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        sectionGO.transform.SetParent(parent.transform, false);
        
        var sectionLayout = sectionGO.GetComponent<HorizontalLayoutGroup>();
        sectionLayout.spacing = 20;
        sectionLayout.padding = new RectOffset(10, 10, 10, 10);
        sectionLayout.childForceExpandHeight = true;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childControlHeight = true;
        sectionLayout.childControlWidth = true;
        
        var layoutElement = sectionGO.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1;
        
        // Emotion Panel
        CreateInfoPanel("EmotionPanel", "😊 EMOTION", "EmotionText", "(analyzing...)", sectionGO.transform);
        
        // Context Panel
        CreateInfoPanel("ContextPanel", "📊 CONTEXT", "ContextText", "(waiting...)", sectionGO.transform);
    }
    
    private static void CreateInfoPanel(string panelName, string title, string textName, string textContent, Transform parent)
    {
        GameObject panelGO = new GameObject(panelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGO.transform.SetParent(parent, false);
        
        var panelImage = panelGO.GetComponent<Image>();
        panelImage.color = PanelColor;
        
        var panelLayout = panelGO.GetComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 8;
        panelLayout.padding = new RectOffset(15, 15, 12, 12);
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;
        
        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 22;
        titleText.color = GoldColor;
        AssignFont(titleText);
        var titleLayout = titleGO.GetComponent<LayoutElement>();
        titleLayout.preferredHeight = 30;
        
        // Text
        GameObject textGO = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panelGO.transform, false);
        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.text = textContent;
        text.fontSize = 18;
        text.color = TextColor;
        AssignFont(text);
        text.alignment = TextAlignmentOptions.TopLeft;
    }
    
    private static void CreateButton(string label, string name, Transform parent, Color color, int fontSize)
    {
        GameObject buttonGO = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
        buttonGO.transform.SetParent(parent, false);
        
        var buttonImage = buttonGO.GetComponent<Image>();
        buttonImage.color = color;
        
        var buttonComponent = buttonGO.GetComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;
        
        var colors = buttonComponent.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        buttonComponent.colors = colors;
        
        // Text Child
        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(buttonGO.transform, false);
        var tmpText = textGO.GetComponent<TextMeshProUGUI>();
        tmpText.text = label;
        tmpText.fontSize = fontSize;
        tmpText.color = Color.white;
        AssignFont(tmpText);
        tmpText.alignment = TextAlignmentOptions.Center;
        
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
    
    private static void CreateLabeledInput(string label, string name, Transform parent, float height)
    {
        GameObject containerGO = new GameObject("LabeledInput", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        containerGO.transform.SetParent(parent, false);
        
        var containerLayout = containerGO.GetComponent<HorizontalLayoutGroup>();
        containerLayout.spacing = 12;
        containerLayout.childForceExpandHeight = true;
        containerLayout.childForceExpandWidth = false;
        
        var containerLayoutElement = containerGO.GetComponent<LayoutElement>();
        containerLayoutElement.preferredHeight = height;
        
        // Label (160px)
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGO.transform.SetParent(containerGO.transform, false);
        var labelText = labelGO.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 20;
        labelText.color = TextColor;
        AssignFont(labelText);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLayout = labelGO.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 160;
        
        // Input Field (flexible width)
        GameObject inputGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        inputGO.transform.SetParent(containerGO.transform, false);
        
        var inputImage = inputGO.GetComponent<Image>();
        inputImage.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        
        var inputLayout = inputGO.GetComponent<LayoutElement>();
        inputLayout.flexibleWidth = 1;
        
        // Text Area
        GameObject textAreaGO = new GameObject("Text Area", typeof(RectTransform));
        textAreaGO.transform.SetParent(inputGO.transform, false);
        var textAreaRect = textAreaGO.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(5, 0);
        textAreaRect.offsetMax = new Vector2(-5, 0);
        
        // Text
        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(textAreaGO.transform, false);
        var textComponent = textGO.GetComponent<TextMeshProUGUI>();
        textComponent.text = "";
        textComponent.fontSize = 20;
        textComponent.color = TextColor;
        AssignFont(textComponent);
        textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Configure InputField
        var inputField = inputGO.GetComponent<TMP_InputField>();
        inputField.textComponent = textComponent;
        inputField.textViewport = textAreaRect;
        inputField.text = "";
    }
    
    private static void CreateLabeledDropdown(string label, string name, Transform parent, float height)
    {
        GameObject containerGO = new GameObject("LabeledDropdown", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        containerGO.transform.SetParent(parent, false);
        
        var containerLayout = containerGO.GetComponent<HorizontalLayoutGroup>();
        containerLayout.spacing = 12;
        containerLayout.childForceExpandHeight = true;
        containerLayout.childForceExpandWidth = false;
        
        var containerLayoutElement = containerGO.GetComponent<LayoutElement>();
        containerLayoutElement.preferredHeight = height;
        
        // Label (160px)
        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGO.transform.SetParent(containerGO.transform, false);
        var labelText = labelGO.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 20;
        labelText.color = TextColor;
        AssignFont(labelText);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLayout = labelGO.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 160;
        
        // Dropdown (flexible width)
        GameObject dropdownGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown), typeof(LayoutElement));
        dropdownGO.transform.SetParent(containerGO.transform, false);
        
        var dropdownImage = dropdownGO.GetComponent<Image>();
        dropdownImage.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        
        var dropdownLayout = dropdownGO.GetComponent<LayoutElement>();
        dropdownLayout.flexibleWidth = 1;
        
        // Dropdown Template
        GameObject templateGO = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        templateGO.transform.SetParent(dropdownGO.transform, false);
        var templateImage = templateGO.GetComponent<Image>();
        templateImage.color = new Color(0.15f, 0.15f, 0.25f, 1f);
        var templateLayout = templateGO.GetComponent<LayoutElement>();
        templateLayout.preferredHeight = 150;
        
        // Viewport
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGO.transform.SetParent(templateGO.transform, false);
        var viewportImage = viewportGO.GetComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0);
        var viewportMask = viewportGO.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        var viewportRect = viewportGO.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        // Content
        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentLayout = contentGO.GetComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.spacing = 2;
        var contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        
        // Item Template
        GameObject itemGO = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        itemGO.transform.SetParent(contentGO.transform, false);
        var itemImage = itemGO.GetComponent<Image>();
        itemImage.color = new Color(0.2f, 0.2f, 0.3f, 1f);
        var itemLayout = itemGO.GetComponent<LayoutElement>();
        itemLayout.preferredHeight = 30;
        
        // Item Text
        GameObject itemTextGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        itemTextGO.transform.SetParent(itemGO.transform, false);
        var itemText = itemTextGO.GetComponent<TextMeshProUGUI>();
        itemText.text = "Option";
        itemText.fontSize = 18;
        itemText.color = TextColor;
        AssignFont(itemText);
        itemText.alignment = TextAlignmentOptions.MidlineLeft;
        var itemTextRect = itemTextGO.GetComponent<RectTransform>();
        itemTextRect.anchorMin = Vector2.zero;
        itemTextRect.anchorMax = Vector2.one;
        itemTextRect.offsetMin = new Vector2(10, 0);
        itemTextRect.offsetMax = new Vector2(-10, 0);
        
        // Label
        GameObject labelTextGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelTextGO.transform.SetParent(dropdownGO.transform, false);
        var labelTextComponent = labelTextGO.GetComponent<TextMeshProUGUI>();
        labelTextComponent.text = "romantic";
        labelTextComponent.fontSize = 20;
        labelTextComponent.color = TextColor;
        AssignFont(labelTextComponent);
        labelTextComponent.alignment = TextAlignmentOptions.MidlineLeft;
        var labelTextRect = labelTextGO.GetComponent<RectTransform>();
        labelTextRect.anchorMin = Vector2.zero;
        labelTextRect.anchorMax = Vector2.one;
        labelTextRect.offsetMin = new Vector2(10, 0);
        labelTextRect.offsetMax = new Vector2(-10, 0);
        
        // Configure Dropdown
        var dropdown = dropdownGO.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = dropdownImage;
        dropdown.template = templateGO.GetComponent<RectTransform>();
        dropdown.captionText = labelTextComponent;
        dropdown.itemText = itemText;
        dropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("romantic"),
            new TMP_Dropdown.OptionData("mysterious"),
            new TMP_Dropdown.OptionData("playful"),
            new TMP_Dropdown.OptionData("serious")
        };
        dropdown.value = 0;
    }
    
    private static void AssignFont(TextMeshProUGUI tmpText)
    {
        if (tmpText != null && _pairingFont != null)
            tmpText.font = _pairingFont;
    }
    
    private static void AssignUIElements(LoveConversationUI ui, GameObject canvas)
    {
        // Find and assign all buttons
        foreach (var button in canvas.GetComponentsInChildren<Button>())
        {
            if (button.gameObject.name == "StartButton")
                ui.startButton = button;
            else if (button.gameObject.name == "StopButton")
                ui.stopButton = button;
        }
        
        // Find and assign all texts
        foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (text.gameObject.name == "StatusText")
                ui.statusText = text;
            else if (text.gameObject.name == "ConversationDisplayText")
                ui.conversationDisplayText = text;
            else if (text.gameObject.name == "PlayerTranscriptText")
                ui.playerTranscriptText = text;
            else if (text.gameObject.name == "NpcResponseText")
                ui.npcResponseText = text;
            else if (text.gameObject.name == "EmotionText")
                ui.emotionText = text;
            else if (text.gameObject.name == "ContextText")
                ui.contextText = text;
        }
        
        // Find and assign input fields
        foreach (var inputField in canvas.GetComponentsInChildren<TMP_InputField>())
        {
            if (inputField.gameObject.name == "SessionIdInput")
                ui.sessionIdInput = inputField;
        }
        
        // Find and assign dropdowns
        foreach (var dropdown in canvas.GetComponentsInChildren<TMP_Dropdown>())
        {
            if (dropdown.gameObject.name == "NpcPersonalityDropdown")
                ui.npcPersonalityDropdown = dropdown;
        }
        
        Debug.Log("✓ All UI elements assigned to LoveConversationUI");
    }
}
