using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class CreateTestRecordScene
{
    private static readonly Color TextColor = Color.black;
    private static readonly Color UiBackgroundColor = Color.white;

    [MenuItem("Tools/Create/Test Record Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        // Create Canvas
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Create Status Text
        GameObject statusGO = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusGO.transform.SetParent(canvasGO.transform, false);
        TMP_Text statusText = statusGO.GetComponent<TMP_Text>();
        statusText.text = "Ready";
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.enableAutoSizing = true;
        statusText.color = TextColor;
        RectTransform st = statusGO.GetComponent<RectTransform>();
        st.anchorMin = new Vector2(0.1f, 0.8f);
        st.anchorMax = new Vector2(0.9f, 0.95f);
        st.offsetMin = Vector2.zero;
        st.offsetMax = Vector2.zero;

        // Create Start Button
        GameObject startBtnGO = CreateButton("StartButton", "Start Recording", new Vector2(0.15f, 0.12f), canvasGO.transform);
        // Create Stop Button
        GameObject stopBtnGO = CreateButton("StopButton", "Stop & Send", new Vector2(0.4f, 0.12f), canvasGO.transform);
        // Create Save Button
        GameObject saveBtnGO = CreateButton("SaveButton", "Save WAV", new Vector2(0.65f, 0.12f), canvasGO.transform);
        // Create AutoCal Button
        GameObject autoCalBtnGO = CreateButton("AutoCalButton", "Auto Calibrate", new Vector2(0.9f, 0.12f), canvasGO.transform);

        // Device ID input
        GameObject deviceInputGO = CreateTMPInputField("DeviceIdInput", "editor_pc", new Vector2(0.15f, 0.02f), canvasGO.transform);
        GameObject countInputGO = CreateTMPInputField("CountInput", "3", new Vector2(0.45f, 0.02f), canvasGO.transform);
        GameObject secondsInputGO = CreateTMPInputField("SecondsInput", "3", new Vector2(0.75f, 0.02f), canvasGO.transform);

        // Create transcript, metrics, emotion, and request id text fields
        GameObject transcriptGO = new GameObject("TranscriptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        transcriptGO.transform.SetParent(canvasGO.transform, false);
        TMP_Text transcriptText = transcriptGO.GetComponent<TMP_Text>();
        transcriptText.text = "(no transcript)";
        transcriptText.alignment = TextAlignmentOptions.TopLeft;
        transcriptText.enableAutoSizing = true;
        transcriptText.color = TextColor;
        RectTransform tt = transcriptGO.GetComponent<RectTransform>();
        tt.anchorMin = new Vector2(0.05f, 0.55f);
        tt.anchorMax = new Vector2(0.95f, 0.75f);
        tt.offsetMin = Vector2.zero; tt.offsetMax = Vector2.zero;

        GameObject metricsGO = new GameObject("MetricsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        metricsGO.transform.SetParent(canvasGO.transform, false);
        TMP_Text metricsText = metricsGO.GetComponent<TMP_Text>();
        metricsText.text = "metrics";
        metricsText.alignment = TextAlignmentOptions.TopLeft;
        metricsText.enableAutoSizing = true;
        metricsText.color = TextColor;
        RectTransform mt = metricsGO.GetComponent<RectTransform>();
        mt.anchorMin = new Vector2(0.05f, 0.35f);
        mt.anchorMax = new Vector2(0.45f, 0.55f);
        mt.offsetMin = Vector2.zero; mt.offsetMax = Vector2.zero;

        GameObject emotionGO = new GameObject("EmotionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        emotionGO.transform.SetParent(canvasGO.transform, false);
        TMP_Text emotionText = emotionGO.GetComponent<TMP_Text>();
        emotionText.text = "emotion";
        emotionText.alignment = TextAlignmentOptions.TopLeft;
        emotionText.enableAutoSizing = true;
        emotionText.color = TextColor;
        RectTransform et = emotionGO.GetComponent<RectTransform>();
        et.anchorMin = new Vector2(0.5f, 0.35f);
        et.anchorMax = new Vector2(0.95f, 0.55f);
        et.offsetMin = Vector2.zero; et.offsetMax = Vector2.zero;

        GameObject reqGO = new GameObject("RequestIdText", typeof(RectTransform), typeof(TextMeshProUGUI));
        reqGO.transform.SetParent(canvasGO.transform, false);
        TMP_Text requestIdText = reqGO.GetComponent<TMP_Text>();
        requestIdText.text = "request_id:";
        requestIdText.alignment = TextAlignmentOptions.TopLeft;
        requestIdText.enableAutoSizing = true;
        requestIdText.color = TextColor;
        RectTransform rt = reqGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.28f);
        rt.anchorMax = new Vector2(0.95f, 0.34f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        GameObject ddGO = CreateTMPDropdown("DeviceDropdown", new Vector2(0.175f, 0.245f), new Vector2(0.25f, 0.05f), canvasGO.transform);
        var dropdown = ddGO.GetComponent<TMP_Dropdown>();
        dropdown.options.Clear();
        dropdown.options.Add(new TMP_Dropdown.OptionData("(no devices)"));
        // Create Recorder object and attach scripts
        GameObject recorderGO = new GameObject("Recorder");
        recorderGO.AddComponent<UnityMicRecorder>();

        // Attach TestRecorderUI and wire references
        var ui = recorderGO.AddComponent<TestRecorderUI>();
        ui.startButton = startBtnGO.GetComponent<Button>();
        ui.stopButton = stopBtnGO.GetComponent<Button>();
        ui.saveButton = saveBtnGO.GetComponent<Button>();
        ui.autoCalButton = autoCalBtnGO.GetComponent<Button>();
        ui.deviceIdInput = deviceInputGO.GetComponent<TMP_InputField>();
        ui.countInput = countInputGO.GetComponent<TMP_InputField>();
        ui.secondsInput = secondsInputGO.GetComponent<TMP_InputField>();
        ui.statusText = statusText;
        ui.transcriptText = transcriptText;
        ui.metricsText = metricsText;
        ui.emotionText = emotionText;
        ui.requestIdText = requestIdText;
        ui.deviceDropdown = dropdown;

        // Ensure an EventSystem exists so UI buttons can receive clicks
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(null);
        }

        // Select scene path and save
        string path = "Assets/Scenes/TestRecordScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        EditorUtility.DisplayDialog("Test Scene Created", "Saved TestRecordScene to Assets/Scenes/TestRecordScene.unity", "OK");
    }

    private static GameObject CreateButton(string name, string label, Vector2 anchorPos, Transform parent)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorPos.x - 0.15f, anchorPos.y - 0.05f);
        rt.anchorMax = new Vector2(anchorPos.x + 0.15f, anchorPos.y + 0.05f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Button btn = btnGO.GetComponent<Button>();
        Image img = btnGO.GetComponent<Image>();
        img.color = UiBackgroundColor;

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        TMP_Text txt = txtGO.GetComponent<TMP_Text>();
        txt.text = label;
        txt.alignment = TextAlignmentOptions.Center;
        txt.enableAutoSizing = true;
        txt.color = TextColor;
        RectTransform trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return btnGO;
    }

    private static GameObject CreateTMPInputField(string name, string placeholder, Vector2 anchorPos, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorPos.x - 0.12f, anchorPos.y - 0.05f);
        rt.anchorMax = new Vector2(anchorPos.x + 0.12f, anchorPos.y + 0.05f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = UiBackgroundColor;

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(go.transform, false);
        var taRt = textArea.GetComponent<RectTransform>();
        taRt.anchorMin = new Vector2(0.05f, 0.1f);
        taRt.anchorMax = new Vector2(0.95f, 0.9f);
        taRt.offsetMin = Vector2.zero;
        taRt.offsetMax = Vector2.zero;

        GameObject placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGO.transform.SetParent(textArea.transform, false);
        var phText = placeholderGO.GetComponent<TMP_Text>();
        phText.text = placeholder;
        phText.color = TextColor;
        phText.alignment = TextAlignmentOptions.Center;
        phText.enableAutoSizing = true;
        var prt = placeholderGO.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(textArea.transform, false);
        var txt = textGO.GetComponent<TMP_Text>();
        txt.text = "";
        txt.color = TextColor;
        txt.alignment = TextAlignmentOptions.Center;
        txt.enableAutoSizing = true;
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = taRt;
        input.textComponent = txt;
        input.placeholder = phText;

        return go;
    }

    private static GameObject CreateTMPDropdown(string name, Vector2 anchorCenter, Vector2 anchorSize, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
        root.transform.SetParent(parent, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorCenter.x - anchorSize.x / 2f, anchorCenter.y - anchorSize.y / 2f);
        rt.anchorMax = new Vector2(anchorCenter.x + anchorSize.x / 2f, anchorCenter.y + anchorSize.y / 2f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = UiBackgroundColor;

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        var labelText = label.GetComponent<TMP_Text>();
        labelText.text = "Select Device";
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.enableAutoSizing = true;
        labelText.color = TextColor;
        var labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.05f, 0f);
        labelRt.anchorMax = new Vector2(0.85f, 1f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        GameObject arrow = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrow.transform.SetParent(root.transform, false);
        var arrowText = arrow.GetComponent<TMP_Text>();
        arrowText.text = "▼";
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.enableAutoSizing = true;
        arrowText.color = TextColor;
        var arrowRt = arrow.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(0.85f, 0f);
        arrowRt.anchorMax = new Vector2(0.98f, 1f);
        arrowRt.offsetMin = Vector2.zero;
        arrowRt.offsetMax = Vector2.zero;

        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(root.transform, false);
        template.SetActive(false);
        var templateRt = template.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.sizeDelta = new Vector2(0f, 120f);
        template.GetComponent<Image>().color = UiBackgroundColor;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = UiBackgroundColor;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 28f);

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2f;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
        item.transform.SetParent(content.transform, false);
        var itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0f, 1f);
        itemRt.anchorMax = new Vector2(1f, 1f);
        itemRt.pivot = new Vector2(0.5f, 1f);
        itemRt.sizeDelta = new Vector2(0f, 28f);
        item.GetComponent<Image>().color = UiBackgroundColor;

        GameObject itemCheck = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
        itemCheck.transform.SetParent(item.transform, false);
        var checkRt = itemCheck.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.03f, 0.2f);
        checkRt.anchorMax = new Vector2(0.09f, 0.8f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;
        itemCheck.GetComponent<Image>().color = TextColor;

        GameObject itemLabel = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        itemLabel.transform.SetParent(item.transform, false);
        var itemLabelText = itemLabel.GetComponent<TMP_Text>();
        itemLabelText.text = "Option";
        itemLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        itemLabelText.enableAutoSizing = true;
        itemLabelText.color = TextColor;
        var itemLabelRt = itemLabel.GetComponent<RectTransform>();
        itemLabelRt.anchorMin = new Vector2(0.12f, 0f);
        itemLabelRt.anchorMax = new Vector2(0.98f, 1f);
        itemLabelRt.offsetMin = Vector2.zero;
        itemLabelRt.offsetMax = Vector2.zero;

        var toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = item.GetComponent<Image>();
        toggle.graphic = itemCheck.GetComponent<Image>();

        var scrollRect = template.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var dd = root.GetComponent<TMP_Dropdown>();
        dd.targetGraphic = root.GetComponent<Image>();
        dd.template = templateRt;
        dd.captionText = labelText;
        dd.itemText = itemLabelText;

        return root;
    }
}
