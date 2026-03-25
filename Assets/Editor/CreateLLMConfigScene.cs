using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class CreateLLMConfigScene
{
    private static readonly Color ButtonGreenColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    private static readonly Color ButtonBlueColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    private static readonly Color ButtonRedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    private static readonly Color PanelDarkColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color PanelColor = new Color(0.15f, 0.15f, 0.25f, 1f);
    private static readonly Color HeaderColor = new Color(1f, 0.8f, 0.8f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.9f, 0.7f, 1f);
    
    private static TMP_FontAsset _pairingFont;

    [MenuItem("Tools/Create/LLM Config Scene")]
    public static void CreateScene()
    {
        // Try to load Korean font
        string fontPath = "Assets/TextMesh Pro/Fonts/YPairingFont-Regular SDF.asset";
        _pairingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        
        if (_pairingFont == null)
        {
            string fontGUID = "0d08fa8f981739742b922e0612ebc35c";
            string guidPath = AssetDatabase.GUIDToAssetPath(fontGUID);
            if (!string.IsNullOrEmpty(guidPath))
            {
                _pairingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(guidPath);
            }
        }
        
        if (_pairingFont == null)
        {
            string[] fontGuids = AssetDatabase.FindAssets("YPairingFont", new[] { "Assets/TextMesh Pro/Fonts" });
            if (fontGuids.Length > 0)
            {
                foreach (var guid in fontGuids)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (foundPath.Contains("Regular") && foundPath.Contains("SDF"))
                    {
                        _pairingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(foundPath);
                        if (_pairingFont != null) break;
                    }
                }
            }
        }
        
        Debug.Log("Starting LLM Config Scene creation...");
        
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // Create Canvas
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
        
        // Main Layout
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
        
        // Header
        CreateHeaderText("⚙️ LLM CONFIGURATION", layoutGO.transform, 50, HeaderColor, 80);
        
        // Config Section
        CreateConfigSection(layoutGO, 600);
        
        // Status Section
        CreateStatusSection(layoutGO, 150);
        
        // Create LLMConfigPanel and attach script
        GameObject configPanelGO = new GameObject("LLMConfigPanel");
        configPanelGO.transform.SetParent(null);
        
        var configPanel = configPanelGO.AddComponent<LLMConfigPanel>();
        
        // Find and assign all UI elements
        AssignUIElements(configPanel, canvasGO);
        
        // Ensure EventSystem exists
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
        
        // Save scene
        string path = "Assets/Scenes/LLMConfigScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        
        string fontMsg = _pairingFont != null ? $"Font: ✓ {_pairingFont.name}" : "Font: ⚠ Using default TMP font";
        
        EditorUtility.DisplayDialog("LLM Config Scene Created ✓", 
            $"Saved LLMConfigScene to Assets/Scenes/LLMConfigScene.unity\n\n" +
            $"Resolution: 1920x1080\n" +
            $"{fontMsg}\n\n" +
            $"Configure LLM settings and click Save.", "OK");
        
        Debug.Log("✓ LLM Config Scene creation completed successfully!");
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
    
    private static void CreateConfigSection(GameObject parent, float height)
    {
        GameObject sectionGO = new GameObject("ConfigSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(Image));
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
        CreateSectionTitle("🔧 LLM PROVIDER SETTINGS", sectionGO, 28);
        
        // Provider Dropdown (50px)
        CreateLabeledDropdown("LLM Provider:", "ProviderDropdown", sectionGO.transform, 50, 
            new List<string> { "Claude (Anthropic)", "OpenAI (GPT-4)", "Ollama (Local)", "Google Gemini" });
        
        // API Key Input (50px)
        CreateLabeledInput("API Key:", "ApiKeyInput", sectionGO.transform, 50, true);
        
        // Model Dropdown (50px)
        CreateLabeledDropdown("Model:", "ModelDropdown", sectionGO.transform, 50,
            new List<string> { "claude-3-5-sonnet-20241022", "claude-3-opus-20250219" });
        
        // Spacer
        GameObject spacerGO = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacerGO.transform.SetParent(sectionGO.transform, false);
        var spacerLayout = spacerGO.GetComponent<LayoutElement>();
        spacerLayout.preferredHeight = 20;
        
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
        
        CreateButton("🧪 TEST", "TestButton", buttonContainerGO.transform, ButtonBlueColor, 28);
        CreateButton("💾 SAVE", "SaveButton", buttonContainerGO.transform, ButtonGreenColor, 28);
        CreateButton("↩️ CANCEL", "CancelButton", buttonContainerGO.transform, ButtonRedColor, 28);
    }
    
    private static void CreateStatusSection(GameObject parent, float height)
    {
        GameObject sectionGO = new GameObject("StatusSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(Image));
        sectionGO.transform.SetParent(parent.transform, false);
        
        var sectionImage = sectionGO.GetComponent<Image>();
        sectionImage.color = new Color(0, 0, 0, 0.3f);
        
        var sectionLayout = sectionGO.GetComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 10;
        sectionLayout.padding = new RectOffset(20, 20, 15, 15);
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.childForceExpandWidth = true;
        
        var layoutElement = sectionGO.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1;
        
        // Title
        CreateSectionTitle("📊 STATUS", sectionGO, 28);
        
        // Status Text (50px)
        GameObject statusGO = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        statusGO.transform.SetParent(sectionGO.transform, false);
        var statusText = statusGO.GetComponent<TextMeshProUGUI>();
        statusText.text = "준비 완료";
        statusText.fontSize = 24;
        statusText.color = TextColor;
        AssignFont(statusText);
        var statusLayout = statusGO.GetComponent<LayoutElement>();
        statusLayout.preferredHeight = 50;
        
        // Status Icon (20px)
        GameObject iconGO = new GameObject("StatusIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGO.transform.SetParent(sectionGO.transform, false);
        var iconImage = iconGO.GetComponent<Image>();
        iconImage.color = Color.white;
        var iconLayout = iconGO.GetComponent<LayoutElement>();
        iconLayout.preferredHeight = 20;
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
    
    private static void CreateLabeledInput(string label, string name, Transform parent, float height, bool isPassword = false)
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
        inputField.inputType = isPassword ? TMP_InputField.InputType.Password : TMP_InputField.InputType.Standard;
    }
    
    private static void CreateLabeledDropdown(string label, string name, Transform parent, float height, List<string> options)
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
        labelTextComponent.text = options.Count > 0 ? options[0] : "Select";
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
        
        dropdown.options = new List<TMP_Dropdown.OptionData>();
        foreach (var option in options)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        }
        dropdown.value = 0;
    }
    
    private static void AssignFont(TextMeshProUGUI tmpText)
    {
        if (tmpText != null && _pairingFont != null)
            tmpText.font = _pairingFont;
    }
    
    private static void AssignUIElements(LLMConfigPanel panel, GameObject canvas)
    {
        // Find and assign dropdowns
        foreach (var dropdown in canvas.GetComponentsInChildren<TMP_Dropdown>())
        {
            if (dropdown.gameObject.name == "ProviderDropdown")
                panel.providerDropdown = dropdown;
            else if (dropdown.gameObject.name == "ModelDropdown")
                panel.modelDropdown = dropdown;
        }
        
        // Find and assign input fields
        foreach (var inputField in canvas.GetComponentsInChildren<TMP_InputField>())
        {
            if (inputField.gameObject.name == "ApiKeyInput")
                panel.apiKeyInput = inputField;
        }
        
        // Find and assign buttons
        foreach (var button in canvas.GetComponentsInChildren<Button>())
        {
            if (button.gameObject.name == "TestButton")
                panel.testButton = button;
            else if (button.gameObject.name == "SaveButton")
                panel.saveButton = button;
            else if (button.gameObject.name == "CancelButton")
                panel.cancelButton = button;
        }
        
        // Find and assign texts
        foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (text.gameObject.name == "StatusText")
                panel.statusText = text;
        }
        
        // Find and assign status icon
        foreach (var image in canvas.GetComponentsInChildren<Image>())
        {
            if (image.gameObject.name == "StatusIcon")
                panel.statusIcon = image;
        }
        
        Debug.Log("✓ All UI elements assigned to LLMConfigPanel");
    }
}
