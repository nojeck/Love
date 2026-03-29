using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

/// <summary>
/// 에피소드형 게임 씬 생성 에디터
/// 기존 테스트 씬과 분리된 신규 씬 파일 생성
/// </summary>
public class EpisodeSceneGenerator
{
    private const string SCENE_PATH = "Assets/Scenes/EpisodeGameScene.unity";
    private const string DEMO_SCENE_PATH = "Assets/Scenes/Episode1DemoScene.unity";
    
    [MenuItem("File/New Scene/Episode Game Scene", false, 200)]
    public static void CreateEpisodeScene()
    {
        CreateEpisodeSceneInternal(SCENE_PATH, "EpisodeGameScene", false);
    }

    [MenuItem("File/New Scene/Episode1 Demo Scene", false, 201)]
    public static void CreateEpisode1DemoScene()
    {
        CreateEpisodeSceneInternal(DEMO_SCENE_PATH, "Episode1DemoScene", true);
    }

    private static void CreateEpisodeSceneInternal(string scenePath, string sceneName, bool isDemo)
    {
        // 씬 디렉토리 확인
        string sceneDir = Path.GetDirectoryName(scenePath);
        if (!Directory.Exists(sceneDir))
        {
            Directory.CreateDirectory(sceneDir);
            AssetDatabase.Refresh();
        }
        
        // 기존 씬이 있으면 확인
        if (File.Exists(scenePath))
        {
            if (!EditorUtility.DisplayDialog("씬 덮어쓰기", 
                $"{Path.GetFileName(scenePath)}이 이미 존재합니다.\n덮어쓰시겠습니까?", 
                "덮어쓰기", "취소"))
            {
                return;
            }
        }
        
        // 새 씬 생성
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // Canvas 찾기 또는 생성
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        
        // EventSystem 확인 (Input System 호환)
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Input System 패키지 사용 시 InputSystemUIInputModule 사용
            #if UNITY_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            #else
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            #endif
        }
        
        // 메인 게임 컨테이너 생성
        GameObject episodeGame = CreateEpisodeGameContainer(canvas.transform);
        
        // ResultPopup 생성
        GameObject resultPopup = CreateResultPopup(canvas.transform);
        
        // AffectionUI 생성
        GameObject affectionUI = CreateAffectionUI(canvas.transform);
        
        // AutoRecording UI 생성
        GameObject autoRecordingUI = CreateAutoRecordingUI(canvas.transform);
        
        // Debug 패널 생성 (개발용)
        GameObject debugPanel = CreateDebugPanel(canvas.transform);
        
        // 컴포넌트 참조 연결
        ConnectReferences(episodeGame, resultPopup, affectionUI, autoRecordingUI, debugPanel, isDemo);
        
        // 씬 저장
        EditorSceneManager.SaveScene(newScene, scenePath);
        
        Selection.activeGameObject = episodeGame;
        
        Debug.Log($"[EpisodeScene] 씬 생성 완료: {scenePath}");
        EditorUtility.DisplayDialog("씬 생성 완료", 
            $"{sceneName} 씬이 생성되었습니다.\n\n경로: {scenePath}\n\nInspector에서 참조를 확인하세요.", 
            "확인");
    }
    
    private static void ConnectReferences(
        GameObject episodeGame, 
        GameObject resultPopup,
        GameObject affectionUI,
        GameObject autoRecordingUI,
        GameObject debugPanel,
        bool isDemo)
    {
        // LoveConversationUI 참조 연결
        LoveConversationUI conversationUI = episodeGame.GetComponent<LoveConversationUI>();
        if (conversationUI != null)
        {
            // AutoRecordingController 참조 (에피소드형 필수)
            var autoRecording = autoRecordingUI.GetComponent<AutoRecordingController>();
            TMP_Text debugStatusText = null;
            if (autoRecording != null)
            {
                conversationUI.autoRecording = autoRecording;
                conversationUI.useAutoRecording = true;  // 에피소드형: 자동 녹음 필수
            }
            
            conversationUI.situationPanel = null;
            conversationUI.useSituationPanel = false;
            
            // AffectionUI 참조
            var affectionController = affectionUI.GetComponent<AffectionUIController>();
            if (affectionController != null)
            {
                conversationUI.affectionUI = affectionController;
            }
            
            // UI 텍스트 참조
            Transform responseDisplay = episodeGame.transform.Find("ResponseDisplay");
            if (responseDisplay != null)
            {
                Transform playerText = responseDisplay.Find("PlayerTranscriptText");
                Transform npcText = responseDisplay.Find("NpcResponseText");
                Transform guideText = responseDisplay.Find("GuideText");
                
                if (playerText != null)
                    conversationUI.playerTranscriptText = playerText.GetComponent<TMP_Text>();
                
                if (npcText != null)
                    conversationUI.npcResponseText = npcText.GetComponent<TMP_Text>();

                if (guideText != null)
                {
                    var guide = guideText.GetComponent<TMP_Text>();
                    if (guide != null)
                    {
                        conversationUI.statusText = guide;
                        debugStatusText = guide;
                    }
                }
            }
            
            // 버튼 참조 연결 (startButton only - episode mode uses auto recording)
            Transform buttons = episodeGame.transform.Find("ControlButtons");
            if (buttons != null)
            {
                Transform startBtn = buttons.Find("StartEpisodeButton");
                if (startBtn != null)
                    conversationUI.startButton = startBtn.GetComponent<Button>();
                
                // stopButton intentionally not set - episode mode uses auto recording only
            }
            
            // GuideText가 없을 경우에만 DebugPanel StatusText 연결
            if (debugPanel != null && conversationUI.statusText == null)
            {
                Transform statusText = debugPanel.transform.Find("StatusText");
                if (statusText != null)
                {
                    var txt = statusText.GetComponent<TMP_Text>();
                    if (txt != null)
                    {
                        conversationUI.statusText = txt;
                        debugStatusText = txt;
                    }
                }
            }

            if (isDemo)
            {
                ApplyEpisode1DemoDefaults(conversationUI, autoRecording, debugStatusText);
            }
        }
        
        // SituationPanel is intentionally not wired in this generator.
    }

    private static void ApplyEpisode1DemoDefaults(
        LoveConversationUI conversationUI,
        AutoRecordingController autoRecording,
        TMP_Text debugStatusText)
    {
        const string demoServerUrl = "http://127.0.0.1:5000";

        conversationUI.serverUrl = demoServerUrl;
        conversationUI.useAutoRecording = true;
        conversationUI.useSituationPanel = false;

        if (autoRecording != null)
        {
            autoRecording.serverUrl = demoServerUrl;
            autoRecording.firstResponseLimit = 10f;
            autoRecording.maxRecordingTime = 30f;
            autoRecording.silenceDuration = 2f;
            autoRecording.voiceThreshold = 0.02f;
            autoRecording.sampleRate = 16000;
            autoRecording.bufferLength = 30;
        }

        if (debugStatusText != null)
        {
            debugStatusText.text = "Episode1 데모 준비 완료";
        }
    }
    
    private static GameObject CreateEpisodeGameContainer(Transform parent)
    {
        GameObject container = new GameObject("EpisodeGame");
        container.transform.SetParent(parent, false);
        
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        
        // LoveConversationUI 컴포넌트 추가
        LoveConversationUI conversationUI = container.AddComponent<LoveConversationUI>();
        
        // UnityMicRecorder 추가 (필수)
        UnityMicRecorder recorder = container.AddComponent<UnityMicRecorder>();
        
        // LogCapture 추가
        container.AddComponent<LogCapture>();
        
        // 기본 UI 요소들 생성
        CreateConversationDisplay(container.transform);
        CreateResponseDisplay(container.transform);
        CreateControlButtons(container.transform);
        
        return container;
    }
    
    private static void CreateConversationDisplay(Transform parent)
    {
        // 대화 표시 영역
        GameObject displayArea = new GameObject("ConversationDisplay");
        displayArea.transform.SetParent(parent, false);
        
        RectTransform rect = displayArea.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.4f);
        rect.anchorMax = new Vector2(0.95f, 0.9f);
        rect.sizeDelta = Vector2.zero;
        
        Image bg = displayArea.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        
        // 스크롤 뷰 추가
        GameObject scrollArea = new GameObject("ScrollArea");
        scrollArea.transform.SetParent(displayArea.transform, false);
        
        RectTransform scrollRect = scrollArea.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.sizeDelta = new Vector2(-20, -20);
        
        ScrollRect scroll = displayArea.AddComponent<ScrollRect>();
        scroll.content = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        
        // 대화 텍스트
        GameObject textObj = new GameObject("ConversationText");
        textObj.transform.SetParent(scrollArea.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 1);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.sizeDelta = new Vector2(0, 200);
        textRect.anchoredPosition = Vector2.zero;
        
        TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "에피소드를 시작하세요...";
        text.fontSize = 18;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
    }
    
    private static void CreateResponseDisplay(Transform parent)
    {
        // NPC/플레이어/설명 텍스트 3역할 고정 표시
        GameObject responseArea = new GameObject("ResponseDisplay");
        responseArea.transform.SetParent(parent, false);
        
        RectTransform rect = responseArea.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.12f);
        rect.anchorMax = new Vector2(0.95f, 0.44f);
        rect.sizeDelta = Vector2.zero;

        Image bg = responseArea.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);

        // 설명/진행 텍스트
        GameObject guideText = new GameObject("GuideText");
        guideText.transform.SetParent(responseArea.transform, false);
        RectTransform gRect = guideText.AddComponent<RectTransform>();
        gRect.anchorMin = new Vector2(0.02f, 0.67f);
        gRect.anchorMax = new Vector2(0.98f, 0.98f);
        gRect.sizeDelta = Vector2.zero;

        TMP_Text gTxt = guideText.AddComponent<TextMeshProUGUI>();
        gTxt.text = "설명: 자동 녹음 후 Space/클릭으로 다음 단계 진행";
        gTxt.fontSize = 15;
        gTxt.alignment = TextAlignmentOptions.TopLeft;
        gTxt.color = new Color(0.95f, 0.92f, 0.72f);
        gTxt.enableWordWrapping = true;
        
        // 플레이어 텍스트
        GameObject playerText = new GameObject("PlayerTranscriptText");
        playerText.transform.SetParent(responseArea.transform, false);
        RectTransform pRect = playerText.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0.02f, 0.34f);
        pRect.anchorMax = new Vector2(0.98f, 0.64f);
        pRect.sizeDelta = Vector2.zero;
        
        TMP_Text pTxt = playerText.AddComponent<TextMeshProUGUI>();
        pTxt.text = "You: (대기 중)";
        pTxt.fontSize = 18;
        pTxt.alignment = TextAlignmentOptions.TopLeft;
        pTxt.enableWordWrapping = true;
        pTxt.color = new Color(0.7f, 0.9f, 1f);
        
        // NPC 응답 텍스트
        GameObject npcText = new GameObject("NpcResponseText");
        npcText.transform.SetParent(responseArea.transform, false);
        RectTransform nRect = npcText.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0.02f, 0.02f);
        nRect.anchorMax = new Vector2(0.98f, 0.3f);
        nRect.sizeDelta = Vector2.zero;
        
        TMP_Text nTxt = npcText.AddComponent<TextMeshProUGUI>();
        nTxt.text = "NPC: ";
        nTxt.fontSize = 19;
        nTxt.alignment = TextAlignmentOptions.TopLeft;
        nTxt.enableWordWrapping = true;
        nTxt.color = new Color(1f, 0.8f, 0.9f);
    }
    
    private static void CreateControlButtons(Transform parent)
    {
        // 버튼 영역 (에피소드형은 버튼 최소화)
        GameObject buttonArea = new GameObject("ControlButtons");
        buttonArea.transform.SetParent(parent, false);
        
        RectTransform rect = buttonArea.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.35f, 0.02f);
        rect.anchorMax = new Vector2(0.65f, 0.08f);
        rect.sizeDelta = Vector2.zero;
        
        // 에피소드 시작 버튼만 유지
        GameObject startBtn = CreateButton("StartEpisodeButton", "에피소드 시작", new Color(0.2f, 0.6f, 0.3f));
        startBtn.transform.SetParent(buttonArea.transform, false);
        
        RectTransform startRect = startBtn.GetComponent<RectTransform>();
        startRect.anchorMin = Vector2.zero;
        startRect.anchorMax = Vector2.one;
        startRect.sizeDelta = Vector2.zero;
        
        // Note: 수동 녹음/정지 버튼 제거 - 에피소드형은 자동 녹음만 사용
    }
    
    private static GameObject CreateSituationPanel(Transform parent)
    {
        const string prefabPath = "Assets/Prefabs/UI/SituationPanel.prefab";
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (panelPrefab != null)
        {
            GameObject panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
            panelInstance.transform.SetParent(parent, false);
            panelInstance.name = "SituationPanelContainer";

            RectTransform rect = panelInstance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
            }

            return panelInstance;
        }

        // 프리팹이 없으면 최소 구성으로 fallback
        GameObject fallback = new GameObject("SituationPanelContainer");
        fallback.transform.SetParent(parent, false);
        RectTransform fallbackRect = fallback.AddComponent<RectTransform>();
        fallbackRect.anchorMin = Vector2.zero;
        fallbackRect.anchorMax = Vector2.one;
        fallbackRect.sizeDelta = Vector2.zero;

        SituationPanelController controller = fallback.AddComponent<SituationPanelController>();
        GameObject visual = new GameObject("SituationPanel");
        visual.transform.SetParent(fallback.transform, false);
        RectTransform visualRect = visual.AddComponent<RectTransform>();
        visualRect.anchorMin = Vector2.zero;
        visualRect.anchorMax = Vector2.one;
        visualRect.sizeDelta = Vector2.zero;
        visual.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
        controller.situationPanel = visual;
        visual.SetActive(false);

        return fallback;
    }
    
    private static GameObject CreateResultPopup(Transform parent)
    {
        GameObject popup = new GameObject("ResultPopup");
        popup.transform.SetParent(parent, false);
        
        RectTransform rect = popup.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.3f, 0.3f);
        rect.anchorMax = new Vector2(0.7f, 0.7f);
        rect.sizeDelta = Vector2.zero;
        
        popup.AddComponent<CanvasGroup>();
        popup.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        
        // ResultPopupController 추가
        ResultPopupController controller = popup.AddComponent<ResultPopupController>();
        
        popup.SetActive(false);
        
        return popup;
    }
    
    private static GameObject CreateAffectionUI(Transform parent)
    {
        GameObject affectionUI = new GameObject("AffectionUI");
        affectionUI.transform.SetParent(parent, false);
        
        RectTransform rect = affectionUI.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f, 0.92f);
        rect.anchorMax = new Vector2(0.25f, 0.98f);
        rect.sizeDelta = Vector2.zero;
        
        // AffectionUIController 추가
        AffectionUIController controller = affectionUI.AddComponent<AffectionUIController>();
        
        // 하트 아이콘과 게이지
        GameObject heartIcon = new GameObject("HeartIcon");
        heartIcon.transform.SetParent(affectionUI.transform, false);
        Image heart = heartIcon.AddComponent<Image>();
        heart.color = Color.red;
        
        GameObject gaugeBar = new GameObject("GaugeBar");
        gaugeBar.transform.SetParent(affectionUI.transform, false);
        Slider gauge = gaugeBar.AddComponent<Slider>();
        
        return affectionUI;
    }
    
    private static GameObject CreateAutoRecordingUI(Transform parent)
    {
        GameObject autoUI = new GameObject("AutoRecordingUI");
        autoUI.transform.SetParent(parent, false);
        
        RectTransform rect = autoUI.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f, 0.88f);
        rect.anchorMax = new Vector2(0.3f, 0.98f);
        rect.sizeDelta = Vector2.zero;
        
        // 배경
        Image bg = autoUI.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        
        // AutoRecordingController 추가
        AutoRecordingController controller = autoUI.AddComponent<AutoRecordingController>();
        
        // === 마이크 아이콘 ===
        GameObject micIconObj = new GameObject("MicIcon");
        micIconObj.transform.SetParent(autoUI.transform, false);
        RectTransform micRect = micIconObj.AddComponent<RectTransform>();
        micRect.anchorMin = new Vector2(0.02f, 0.2f);
        micRect.anchorMax = new Vector2(0.15f, 0.8f);
        micRect.sizeDelta = Vector2.zero;
        Image micImg = micIconObj.AddComponent<Image>();
        micImg.color = Color.white;
        controller.micIcon = micImg;
        
        // === 상태 라벨 ===
        GameObject statusLabel = new GameObject("StatusLabel");
        statusLabel.transform.SetParent(autoUI.transform, false);
        RectTransform statusRect = statusLabel.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.18f, 0.55f);
        statusRect.anchorMax = new Vector2(0.98f, 0.9f);
        statusRect.sizeDelta = Vector2.zero;
        TMP_Text status = statusLabel.AddComponent<TextMeshProUGUI>();
        status.text = "대기 중";
        status.fontSize = 14;
        status.alignment = TextAlignmentOptions.Left;
        status.color = Color.white;
        controller.statusLabel = status;
        
        // === 타이머 텍스트 ===
        GameObject timerTextObj = new GameObject("TimerText");
        timerTextObj.transform.SetParent(autoUI.transform, false);
        RectTransform timerTextRect = timerTextObj.AddComponent<RectTransform>();
        timerTextRect.anchorMin = new Vector2(0.18f, 0.1f);
        timerTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerTextRect.sizeDelta = Vector2.zero;
        TMP_Text timerTxt = timerTextObj.AddComponent<TextMeshProUGUI>();
        timerTxt.text = "0.0s";
        timerTxt.fontSize = 12;
        timerTxt.alignment = TextAlignmentOptions.Left;
        timerTxt.color = new Color(0.7f, 0.7f, 0.7f);
        controller.timerText = timerTxt;
        
        // === 타이머 바 ===
        GameObject timerBarObj = new GameObject("TimerBar");
        timerBarObj.transform.SetParent(autoUI.transform, false);
        RectTransform timerBarRect = timerBarObj.AddComponent<RectTransform>();
        timerBarRect.anchorMin = new Vector2(0.5f, 0.3f);
        timerBarRect.anchorMax = new Vector2(0.98f, 0.5f);
        timerBarRect.sizeDelta = Vector2.zero;
        
        // 슬라이더 배경
        GameObject sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(timerBarObj.transform, false);
        RectTransform bgRect = sliderBg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = sliderBg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f);
        
        // 슬라이더 채움 영역
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(timerBarObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;
        
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 0.9f);
        
        Slider timerSlider = timerBarObj.AddComponent<Slider>();
        timerSlider.targetGraphic = fillImg;
        timerSlider.fillRect = fillRect;
        controller.timerBar = timerSlider;
        
        // === 녹음 인디케이터 ===
        GameObject recordingIndicator = new GameObject("RecordingIndicator");
        recordingIndicator.transform.SetParent(autoUI.transform, false);
        RectTransform indicatorRect = recordingIndicator.AddComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0.85f, 0.6f);
        indicatorRect.anchorMax = new Vector2(0.95f, 0.9f);
        indicatorRect.sizeDelta = Vector2.zero;
        Image indicatorImg = recordingIndicator.AddComponent<Image>();
        indicatorImg.color = Color.red;
        recordingIndicator.SetActive(false);
        controller.recordingIndicator = recordingIndicator;
        
        // === 녹음 시간 텍스트 ===
        GameObject recTimeObj = new GameObject("RecordingTimeText");
        recTimeObj.transform.SetParent(autoUI.transform, false);
        RectTransform recTimeRect = recTimeObj.AddComponent<RectTransform>();
        recTimeRect.anchorMin = new Vector2(0.18f, 0.0f);
        recTimeRect.anchorMax = new Vector2(0.98f, 0.25f);
        recTimeRect.sizeDelta = Vector2.zero;
        TMP_Text recTimeTxt = recTimeObj.AddComponent<TextMeshProUGUI>();
        recTimeTxt.text = "";
        recTimeTxt.fontSize = 11;
        recTimeTxt.alignment = TextAlignmentOptions.Left;
        recTimeTxt.color = new Color(0.5f, 0.8f, 1f);
        controller.recordingTimeText = recTimeTxt;
        
        return autoUI;
    }
    
    private static GameObject CreateDebugPanel(Transform parent)
    {
        GameObject debugPanel = new GameObject("DebugPanel");
        debugPanel.transform.SetParent(parent, false);
        
        RectTransform rect = debugPanel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f, 0.02f);
        rect.anchorMax = new Vector2(0.25f, 0.12f);
        rect.sizeDelta = Vector2.zero;
        
        Image bg = debugPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        
        // 상태 텍스트
        GameObject statusText = new GameObject("StatusText");
        statusText.transform.SetParent(debugPanel.transform, false);
        
        RectTransform textRect = statusText.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-10, -10);
        
        TMP_Text text = statusText.AddComponent<TextMeshProUGUI>();
        text.text = "서버 연결 대기";
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.yellow;
        
        return debugPanel;
    }
    
    private static GameObject CreateButton(string name, string text, Color bgColor)
    {
        GameObject btn = new GameObject(name);
        btn.AddComponent<RectTransform>();
        
        Image image = btn.AddComponent<Image>();
        image.color = bgColor;
        
        Button button = btn.AddComponent<Button>();
        
        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btn.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 16;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        
        return btn;
    }
}
