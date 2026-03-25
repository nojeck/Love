using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Example: Minimal LoveConversationUI Setup
/// 
/// This example shows how to programmatically create all necessary UI elements
/// for the LoveConversationScene without needing to build it manually in the editor.
/// </summary>
public class LoveConversationSceneSetup : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("Setting up LoveConversationScene...");
        SetupUI();
    }

    void SetupUI()
    {
        // Create Canvas
        var canvasGO = new GameObject("LoveConversationCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        // Create background panel
        var bgGO = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.GetComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Add VerticalLayoutGroup to main canvas
        var layoutGroup = canvasGO.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(20, 20, 20, 20);
        layoutGroup.spacing = 10;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;

        // ===== Control Section =====
        CreateControlSection(canvasGO);

        // ===== Conversation Display Section =====
        CreateConversationSection(canvasGO);

        // ===== Info Section =====
        CreateInfoSection(canvasGO);

        // ===== Create Manager GameObject =====
        var managerGO = new GameObject("LoveConversationManager", typeof(RectTransform));
        managerGO.transform.SetParent(canvasGO.transform, false);

        var loveUI = managerGO.AddComponent<LoveConversationUI>();
        var recorder = managerGO.AddComponent<UnityMicRecorder>();

        // Assign UI fields
        loveUI.startButton = GameObject.Find("StartButton").GetComponent<Button>();
        loveUI.stopButton = GameObject.Find("StopButton").GetComponent<Button>();
        loveUI.statusText = GameObject.Find("StatusText").GetComponent<TextMeshProUGUI>();
        loveUI.conversationDisplayText = GameObject.Find("ConversationDisplayText").GetComponent<TextMeshProUGUI>();
        loveUI.npcResponseText = GameObject.Find("NpcResponseText").GetComponent<TextMeshProUGUI>();
        loveUI.emotionText = GameObject.Find("EmotionText").GetComponent<TextMeshProUGUI>();
        loveUI.contextText = GameObject.Find("ContextText").GetComponent<TextMeshProUGUI>();
        loveUI.sessionIdInput = GameObject.Find("SessionIdInput").GetComponent<TMP_InputField>();
        loveUI.npcPersonalityDropdown = GameObject.Find("NpcPersonalityDropdown").GetComponent<TMP_Dropdown>();

        Debug.Log("LoveConversationScene setup complete!");
    }

    void CreateControlSection(GameObject parent)
    {
        var sectionGO = new GameObject("ControlSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
        sectionGO.transform.SetParent(parent.transform, false);

        var layoutGroup = sectionGO.GetComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 5;

        // Title
        var titleGO = CreateText("Control", parent: sectionGO, fontSize: 32);
        titleGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;

        // Status text
        CreateText("Ready", "StatusText", sectionGO, fontSize: 20);

        // Buttons container
        var buttonContainerGO = new GameObject("ButtonContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonContainerGO.transform.SetParent(sectionGO.transform, false);
        var buttonLayout = buttonContainerGO.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10;
        buttonLayout.childForceExpandHeight = true;
        buttonLayout.childForceExpandWidth = true;

        CreateButton("Start Recording", "StartButton", buttonContainerGO);
        CreateButton("Stop Recording", "StopButton", buttonContainerGO);

        var sessionInputGO = new GameObject("SessionIdInput", typeof(RectTransform), typeof(TMP_InputField));
        sessionInputGO.transform.SetParent(sectionGO.transform, false);
        var inputField = sessionInputGO.GetComponent<TMP_InputField>();
        inputField.text = "player_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    void CreateConversationSection(GameObject parent)
    {
        var sectionGO = new GameObject("ConversationSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
        sectionGO.transform.SetParent(parent.transform, false);

        var layoutGroup = sectionGO.GetComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 10;
        layoutGroup.childForceExpandHeight = false;

        // Title
        CreateText("Conversation", parent: sectionGO, fontSize: 32);

        // Large text display
        var displayGO = new GameObject("ConversationDisplayText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ScrollRect));
        displayGO.transform.SetParent(sectionGO.transform, false);

        var displayText = displayGO.GetComponent<TextMeshProUGUI>();
        displayText.text = "=== Conversation ===";
        displayText.fontSize = 20;

        var displayRect = displayGO.GetComponent<RectTransform>();
        displayRect.sizeDelta = new Vector2(0, 300);

        // NPC Response
        CreateText("NPC Response:\n(waiting...)", "NpcResponseText", sectionGO, fontSize: 22);
    }

    void CreateInfoSection(GameObject parent)
    {
        var sectionGO = new GameObject("InfoSection", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        sectionGO.transform.SetParent(parent.transform, false);

        var layoutGroup = sectionGO.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 20;
        layoutGroup.childForceExpandHeight = true;
        layoutGroup.childForceExpandWidth = true;

        // Emotion info
        var emotionGO = new GameObject("EmotionPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        emotionGO.transform.SetParent(sectionGO.transform, false);
        CreateText("Emotion Info", parent: emotionGO, fontSize: 24);
        CreateText("(analyzing...)", "EmotionText", emotionGO, fontSize: 18);

        // Context info
        var contextGO = new GameObject("ContextPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contextGO.transform.SetParent(sectionGO.transform, false);
        CreateText("Context", parent: contextGO, fontSize: 24);
        CreateText("(waiting...)", "ContextText", contextGO, fontSize: 18);
    }

    GameObject CreateText(string text, string name = null, GameObject parent = null, int fontSize = 20)
    {
        var go = new GameObject(name ?? text, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (parent != null)
            go.transform.SetParent(parent.transform, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 50);

        return go;
    }

    void CreateButton(string label, string name, GameObject parent)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
        btnGO.transform.SetParent(parent.transform, false);

        var image = btnGO.GetComponent<Image>();
        image.color = new Color(0.2f, 0.4f, 0.7f, 1f);

        var rect = btnGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150, 50);

        // Add text child
        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(btnGO.transform, false);
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
