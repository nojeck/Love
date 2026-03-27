using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Phase 7.4: 결과 팝업 컨트롤러
/// 
/// Clear/Fail 팝업 관리
/// - Clear: 호감도 100 달성 시
/// - Fail: 호감도 0 도달 시
/// - 대화 분석 피드백
/// - 회귀(Revert) 기능
/// </summary>
public class ResultPopupController : MonoBehaviour
{
    #region UI References
    
    [Header("Popup Container")]
    [Tooltip("팝업 전체 패널")]
    public GameObject popupPanel;
    
    [Tooltip("반투명 배경")]
    public Image backgroundOverlay;
    
    [Tooltip("팝업 프레임")]
    public Image popupFrame;
    
    [Header("Title Section")]
    [Tooltip("결과 타이틀 (CLEAR!/FAIL...)")]
    public TMP_Text titleText;
    
    [Tooltip("결과 아이콘")]
    public Image resultIcon;
    
    [Header("NPC Section")]
    [Tooltip("NPC 초상화")]
    public Image npcPortrait;
    
    [Tooltip("결과 제목 (달콤한 데이트/어색한 침묵)")]
    public TMP_Text resultTitle;
    
    [Tooltip("결과 설명")]
    public TMP_Text resultDescription;
    
    [Header("Stats Section")]
    [Tooltip("통계 패널")]
    public GameObject statsPanel;
    
    [Tooltip("총 턴 수")]
    public TMP_Text turnCountText;
    
    [Tooltip("평균 점수")]
    public TMP_Text avgScoreText;
    
    [Tooltip("최종 호감도")]
    public TMP_Text finalAffectionText;
    
    [Header("Analysis Section (Fail Only)")]
    [Tooltip("분석 패널")]
    public GameObject analysisPanel;
    
    [Tooltip("분석 제목")]
    public TMP_Text analysisTitle;
    
    [Tooltip("분석 내용")]
    public TMP_Text analysisContent;
    
    [Tooltip("개선 팁")]
    public TMP_Text tipsContent;
    
    [Header("Buttons")]
    [Tooltip("버튼 패널")]
    public GameObject buttonPanel;
    
    [Tooltip("회귀 버튼")]
    public Button revertButton;
    
    [Tooltip("다음 에피소드 버튼")]
    public Button nextEpisodeButton;
    
    [Tooltip("메인 메뉴 버튼")]
    public Button mainMenuButton;
    
    [Header("Sprites")]
    [Tooltip("Clear 아이콘")]
    public Sprite clearIcon;
    
    [Tooltip("Fail 아이콘")]
    public Sprite failIcon;
    
    [Tooltip("행복한 초상화")]
    public Sprite happyPortrait;
    
    [Tooltip("슬픈 초상화")]
    public Sprite sadPortrait;
    
    [Header("Animation")]
    [Tooltip("등장 애니메이션 시간")]
    public float appearDuration = 0.5f;
    
    [Tooltip("애니메이션 커브")]
    public AnimationCurve appearCurve;
    
    [Header("Server Config")]
    [Tooltip("서버 URL")]
    public string serverUrl = "http://127.0.0.1:5000";
    
    #endregion
    
    #region Events
    
    [Header("Events")]
    [Tooltip("회귀 버튼 클릭")]
    public UnityEvent OnRevert;
    
    [Tooltip("다음 에피소드")]
    public UnityEvent<int> OnNextEpisode;
    
    [Tooltip("메인 메뉴")]
    public UnityEvent OnMainMenu;
    
    [Tooltip("팝업 닫힘")]
    public UnityEvent OnPopupClosed;
    
    #endregion
    
    #region Internal State
    
    private int currentEpisodeId = 1;
    private string currentPlayerId;
    private bool isClear = false;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        // 버튼 이벤트 연결
        if (revertButton != null)
            revertButton.onClick.AddListener(OnRevertClicked);
        
        if (nextEpisodeButton != null)
            nextEpisodeButton.onClick.AddListener(OnNextEpisodeClicked);
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }
    
    void Start()
    {
        // 초기에는 숨김
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }
    
    void OnDestroy()
    {
        if (revertButton != null)
            revertButton.onClick.RemoveListener(OnRevertClicked);
        
        if (nextEpisodeButton != null)
            nextEpisodeButton.onClick.RemoveListener(OnNextEpisodeClicked);
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Clear 팝업 표시
    /// </summary>
    public void ShowClearPopup(int episodeId, int turnCount, float avgScore, string playerId)
    {
        currentEpisodeId = episodeId;
        currentPlayerId = playerId;
        isClear = true;
        
        // UI 설정
        if (titleText != null)
            titleText.text = "★ CLEAR! ★";
        
        if (resultIcon != null && clearIcon != null)
            resultIcon.sprite = clearIcon;
        
        if (npcPortrait != null && happyPortrait != null)
            npcPortrait.sprite = happyPortrait;
        
        if (resultTitle != null)
            resultTitle.text = "달콤한 데이트";
        
        if (resultDescription != null)
            resultDescription.text = "호감도 100 달성!";
        
        // 통계
        UpdateStats(turnCount, avgScore, 100);
        
        // 분석 패널 숨김
        if (analysisPanel != null)
            analysisPanel.SetActive(false);
        
        // 버튼 설정
        if (revertButton != null)
            revertButton.gameObject.SetActive(false);
        
        if (nextEpisodeButton != null)
            nextEpisodeButton.gameObject.SetActive(true);
        
        // 팝업 표시
        ShowPopup();
        
        Debug.Log($"[ResultPopup] Clear popup shown - Episode {episodeId}, Turns: {turnCount}");
    }
    
    /// <summary>
    /// Fail 팝업 표시
    /// </summary>
    public void ShowFailPopup(int episodeId, int turnCount, float avgScore, 
                              List<string> issues, List<string> tips, string playerId)
    {
        currentEpisodeId = episodeId;
        currentPlayerId = playerId;
        isClear = false;
        
        // UI 설정
        if (titleText != null)
            titleText.text = "✗ FAIL...";
        
        if (resultIcon != null && failIcon != null)
            resultIcon.sprite = failIcon;
        
        if (npcPortrait != null && sadPortrait != null)
            npcPortrait.sprite = sadPortrait;
        
        if (resultTitle != null)
            resultTitle.text = "어색한 침묵";
        
        if (resultDescription != null)
            resultDescription.text = "호감도가 바닥났다...";
        
        // 통계
        UpdateStats(turnCount, avgScore, 0);
        
        // 분석 내용
        if (analysisPanel != null)
        {
            analysisPanel.SetActive(true);
            
            if (analysisTitle != null)
                analysisTitle.text = "─── 대화 분석 ───";
            
            if (analysisContent != null)
            {
                string content = "";
                foreach (var issue in issues)
                {
                    content += $"• {issue}\n";
                }
                analysisContent.text = content;
            }
            
            if (tipsContent != null)
            {
                string tipsText = "💡 개선 팁:\n";
                foreach (var tip in tips)
                {
                    tipsText += $"{tip}\n";
                }
                tipsContent.text = tipsText;
            }
        }
        
        // 버튼 설정
        if (revertButton != null)
            revertButton.gameObject.SetActive(true);
        
        if (nextEpisodeButton != null)
            nextEpisodeButton.gameObject.SetActive(false);
        
        // 팝업 표시
        ShowPopup();
        
        Debug.Log($"[ResultPopup] Fail popup shown - Episode {episodeId}, Issues: {issues.Count}");
    }
    
    /// <summary>
    /// 서버에서 분석 데이터 가져와서 Fail 팝업 표시
    /// </summary>
    public void ShowFailPopupWithAnalysis(int episodeId, int turnCount, float avgScore, string playerId)
    {
        StartCoroutine(FetchAnalysisAndShow(episodeId, turnCount, avgScore, playerId));
    }
    
    /// <summary>
    /// 팝업 숨김
    /// </summary>
    public void HidePopup()
    {
        if (popupPanel != null)
        {
            StartCoroutine(HidePopupAnimated());
        }
    }
    
    #endregion
    
    #region Private Methods
    
    private void UpdateStats(int turnCount, float avgScore, int affection)
    {
        if (turnCountText != null)
            turnCountText.text = $"총 턴 수: {turnCount}";
        
        if (avgScoreText != null)
            avgScoreText.text = $"평균 점수: {avgScore:F2}";
        
        if (finalAffectionText != null)
            finalAffectionText.text = $"최종 호감도: {affection}";
    }
    
    private void ShowPopup()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            StartCoroutine(ShowPopupAnimated());
        }
    }
    
    private IEnumerator ShowPopupAnimated()
    {
        // 스케일 애니메이션
        Transform popupTransform = popupPanel.transform;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one;
        
        float elapsed = 0f;
        
        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / appearDuration);
            float curveT = appearCurve != null ? appearCurve.Evaluate(t) : t;
            
            popupTransform.localScale = Vector3.Lerp(startScale, endScale, curveT);
            
            // 페이드 인
            if (backgroundOverlay != null)
            {
                Color bgColor = backgroundOverlay.color;
                bgColor.a = Mathf.Lerp(0f, 0.5f, t);
                backgroundOverlay.color = bgColor;
            }
            
            yield return null;
        }
        
        popupTransform.localScale = endScale;
    }
    
    private IEnumerator HidePopupAnimated()
    {
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // 페이드 아웃
            if (backgroundOverlay != null)
            {
                Color bgColor = backgroundOverlay.color;
                bgColor.a = Mathf.Lerp(0.5f, 0f, t);
                backgroundOverlay.color = bgColor;
            }
            
            yield return null;
        }
        
        popupPanel.SetActive(false);
        OnPopupClosed?.Invoke();
    }
    
    private IEnumerator FetchAnalysisAndShow(int episodeId, int turnCount, float avgScore, string playerId)
    {
        string url = $"{serverUrl}/episode/analyze?player_id={playerId}";
        
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
            
            List<string> issues = new List<string>();
            List<string> tips = new List<string>();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<AnalysisResponse>(www.downloadHandler.text);
                    if (response != null && response.status == "ok")
                    {
                        issues = response.issues != null ? new List<string>(response.issues) : new List<string>();
                        tips = response.tips != null ? new List<string>(response.tips) : new List<string>();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ResultPopup] Failed to parse analysis: {ex.Message}");
                    issues.Add("분석 데이터를 불러오지 못했습니다");
                }
            }
            else
            {
                Debug.LogWarning($"[ResultPopup] Analysis request failed: {www.error}");
                issues.Add("서버 연결 실패");
            }
            
            // 기본값
            if (issues.Count == 0)
                issues.Add("호감도가 너무 낮아졌습니다");
            
            if (tips.Count == 0)
                tips.Add("상대방의 감정을 배려해보세요");
            
            ShowFailPopup(episodeId, turnCount, avgScore, issues, tips, playerId);
        }
    }
    
    #endregion
    
    #region Button Handlers
    
    private void OnRevertClicked()
    {
        Debug.Log($"[ResultPopup] Revert clicked - Episode {currentEpisodeId}");
        
        StartCoroutine(RevertEpisode());
    }
    
    private void OnNextEpisodeClicked()
    {
        Debug.Log($"[ResultPopup] Next episode clicked");
        
        int nextEpisode = currentEpisodeId + 1;
        HidePopup();
        OnNextEpisode?.Invoke(nextEpisode);
    }
    
    private void OnMainMenuClicked()
    {
        Debug.Log($"[ResultPopup] Main menu clicked");
        
        HidePopup();
        OnMainMenu?.Invoke();
    }
    
    private IEnumerator RevertEpisode()
    {
        string url = $"{serverUrl}/episode/revert";
        
        var requestData = new RevertRequest
        {
            player_id = currentPlayerId,
            episode_id = currentEpisodeId
        };
        
        string jsonData = JsonUtility.ToJson(requestData);
        
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
                Debug.Log($"[ResultPopup] Episode reverted successfully");
                
                // 팝업 닫기
                HidePopup();
                
                // 이벤트 발생
                OnRevert?.Invoke();
            }
            else
            {
                Debug.LogError($"[ResultPopup] Revert failed: {www.error}");
            }
        }
    }
    
    #endregion
    
    #region Data Classes
    
    [System.Serializable]
    private class AnalysisResponse
    {
        public string status;
        public string[] issues;
        public string[] tips;
    }
    
    [System.Serializable]
    private class RevertRequest
    {
        public string player_id;
        public int episode_id;
    }
    
    #endregion
}
