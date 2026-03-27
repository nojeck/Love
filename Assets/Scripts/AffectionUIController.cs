using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Phase 7.2: 호감도 UI 컨트롤러
/// 
/// 실시간 호감도 변화 피드백 시스템
/// - 호감도 바 애니메이션
/// - 변화량 표시
/// - 색상/이모지 변화
/// </summary>
public class AffectionUIController : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("호감도 프로그레스 바")]
    public Slider affectionBar;
    
    [Tooltip("현재 호감도 수치 텍스트")]
    public TMP_Text affectionValue;
    
    [Tooltip("호감도 라벨")]
    public TMP_Text affectionLabel;
    
    [Tooltip("변화량 텍스트 (+10, -5 등)")]
    public TMP_Text changeValue;
    
    [Tooltip("변화 방향 아이콘")]
    public Image changeIcon;
    
    [Tooltip("상태 이모지")]
    public TMP_Text statusEmoji;
    
    [Tooltip("바 채우기 이미지 (색상 변화용)")]
    public Image fillImage;
    
    [Tooltip("변화량 표시 컨테이너")]
    public GameObject changeIndicator;
    
    [Header("Animation Settings")]
    [Tooltip("호감도 바 애니메이션 지속 시간")]
    public float animationDuration = 0.5f;
    
    [Tooltip("변화량 표시 지속 시간")]
    public float changeDisplayDuration = 2.0f;
    
    [Tooltip("애니메이션 커브")]
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Colors")]
    [Tooltip("사랑 (80-100)")]
    public Color loveColor = new Color(1f, 0.41f, 0.71f);    // #FF69B4
    
    [Tooltip("호감 (60-79)")]
    public Color likeColor = new Color(1f, 0.71f, 0.76f);    // #FFB6C1
    
    [Tooltip("보통 (40-59)")]
    public Color neutralColor = new Color(1f, 0.84f, 0f);    // #FFD700
    
    [Tooltip("냉소 (20-39)")]
    public Color coldColor = new Color(1f, 0.65f, 0f);       // #FFA500
    
    [Tooltip("위험 (0-19)")]
    public Color dangerColor = new Color(1f, 0.27f, 0.27f);  // #FF4444
    
    [Header("Server Config")]
    [Tooltip("서버 URL")]
    public string serverUrl = "http://127.0.0.1:5000";
    
    [Header("Sprites")]
    [Tooltip("증가 아이콘")]
    public Sprite increaseIcon;
    
    [Tooltip("감소 아이콘")]
    public Sprite decreaseIcon;
    
    // 내부 상태
    private float currentAffection = 50f;
    private float displayedAffection = 50f;
    private Coroutine animationCoroutine;
    private Coroutine changeDisplayCoroutine;
    private string currentPlayerId;
    
    // 이벤트
    [Header("Events")]
    public UnityEvent<float> OnAffectionChanged;
    public UnityEvent OnAffectionMax;  // 100 도달 (Clear)
    public UnityEvent OnAffectionMin;  // 0 도달 (Fail)
    
    // 이모지 매핑
    private readonly Dictionary<string, string> emojiMap = new Dictionary<string, string>
    {
        {"love", "😍"},
        {"like", "😊"},
        {"neutral", "😐"},
        {"cold", "😕"},
        {"danger", "😢"}
    };
    
    // 상태 텍스트 매핑
    private readonly Dictionary<string, string> statusTextMap = new Dictionary<string, string>
    {
        {"love", "사랑"},
        {"like", "호감"},
        {"neutral", "보통"},
        {"cold", "냉소"},
        {"danger", "위험"}
    };
    
    void Start()
    {
        InitializeUI();
        
        // 기본값 설정
        if (affectionLabel != null)
            affectionLabel.text = "호감도";
    }
    
    void InitializeUI()
    {
        // 초기 호감도 표시
        SetAffectionInstant(50f);
        
        // 변화량 숨김
        if (changeIndicator != null)
            changeIndicator.SetActive(false);
    }
    
    /// <summary>
    /// 플레이어 ID 설정 및 서버에서 호감도 로드
    /// </summary>
    public void SetPlayerId(string playerId)
    {
        currentPlayerId = playerId;
        StartCoroutine(FetchAffectionFromServer());
    }
    
    /// <summary>
    /// 서버에서 현재 호감도 조회
    /// </summary>
    public IEnumerator FetchAffectionFromServer()
    {
        if (string.IsNullOrEmpty(currentPlayerId))
            yield break;
        
        string url = $"{serverUrl}/episode/status?player_id={currentPlayerId}";
        
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.timeout = 5;
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<EpisodeStatusResponse>(www.downloadHandler.text);
                    if (response != null && response.status == "ok")
                    {
                        float newAffection = response.affection;
                        SetAffectionInstant(newAffection);
                        
                        Debug.Log($"[AffectionUI] Loaded affection: {newAffection}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AffectionUI] Failed to parse status: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[AffectionUI] Failed to fetch status: {www.error}");
            }
        }
    }
    
    /// <summary>
    /// 호감도 업데이트 (애니메이션 포함)
    /// </summary>
    /// <param name="newValue">새 호감도 값 (0-100)</param>
    /// <param name="change">변화량</param>
    public void UpdateAffection(float newValue, float change)
    {
        // 범위 제한
        newValue = Mathf.Clamp(newValue, 0f, 100f);
        
        float oldValue = currentAffection;
        currentAffection = newValue;
        
        // 애니메이션 시작
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateAffection(displayedAffection, newValue));
        
        // 변화량 표시
        if (changeDisplayCoroutine != null)
            StopCoroutine(changeDisplayCoroutine);
        changeDisplayCoroutine = StartCoroutine(ShowChangeIndicator(change));
        
        // 이벤트 발생
        OnAffectionChanged?.Invoke(newValue);
        
        // Clear/Fail 체크
        if (newValue >= 100f)
        {
            OnAffectionMax?.Invoke();
            Debug.Log("[AffectionUI] Affection MAX! Clear!");
        }
        else if (newValue <= 0f)
        {
            OnAffectionMin?.Invoke();
            Debug.Log("[AffectionUI] Affection MIN! Fail!");
        }
        
        Debug.Log($"[AffectionUI] Affection updated: {oldValue} → {newValue} (change: {change})");
    }
    
    /// <summary>
    /// 즉시 호감도 설정 (애니메이션 없음)
    /// </summary>
    public void SetAffectionInstant(float value)
    {
        currentAffection = Mathf.Clamp(value, 0f, 100f);
        displayedAffection = currentAffection;
        
        UpdateBarDisplay(currentAffection);
        UpdateColor(currentAffection);
        UpdateEmoji(currentAffection);
    }
    
    /// <summary>
    /// 호감도 바 애니메이션
    /// </summary>
    private IEnumerator AnimateAffection(float from, float to)
    {
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float curveT = animationCurve.Evaluate(t);
            
            float currentValue = Mathf.Lerp(from, to, curveT);
            displayedAffection = currentValue;
            
            UpdateBarDisplay(currentValue);
            UpdateColor(currentValue);
            UpdateEmoji(currentValue);
            
            yield return null;
        }
        
        // 최종값 설정
        displayedAffection = to;
        UpdateBarDisplay(to);
        UpdateColor(to);
        UpdateEmoji(to);
        
        animationCoroutine = null;
    }
    
    /// <summary>
    /// 바 표시 업데이트
    /// </summary>
    private void UpdateBarDisplay(float value)
    {
        // 슬라이더 값 (0-1 범위)
        if (affectionBar != null)
            affectionBar.value = value / 100f;
        
        // 수치 텍스트
        if (affectionValue != null)
            affectionValue.text = Mathf.RoundToInt(value).ToString();
    }
    
    /// <summary>
    /// 색상 업데이트
    /// </summary>
    private void UpdateColor(float affection)
    {
        if (fillImage == null)
            return;
        
        Color targetColor;
        
        if (affection >= 80f)
            targetColor = loveColor;
        else if (affection >= 60f)
            targetColor = likeColor;
        else if (affection >= 40f)
            targetColor = neutralColor;
        else if (affection >= 20f)
            targetColor = coldColor;
        else
            targetColor = dangerColor;
        
        fillImage.color = targetColor;
    }
    
    /// <summary>
    /// 이모지 업데이트
    /// </summary>
    private void UpdateEmoji(float affection)
    {
        if (statusEmoji == null)
            return;
        
        string status = GetStatusKey(affection);
        
        if (emojiMap.TryGetValue(status, out string emoji))
        {
            statusEmoji.text = emoji;
        }
    }
    
    /// <summary>
    /// 상태 키 가져오기
    /// </summary>
    private string GetStatusKey(float affection)
    {
        if (affection >= 80f) return "love";
        if (affection >= 60f) return "like";
        if (affection >= 40f) return "neutral";
        if (affection >= 20f) return "cold";
        return "danger";
    }
    
    /// <summary>
    /// 변화량 표시
    /// </summary>
    private IEnumerator ShowChangeIndicator(float change)
    {
        if (changeIndicator == null || changeValue == null)
            yield break;
        
        // 변화가 없으면 표시하지 않음
        if (Mathf.Abs(change) < 0.01f)
        {
            changeIndicator.SetActive(false);
            yield break;
        }
        
        // 텍스트 설정
        string sign = change > 0 ? "+" : "";
        changeValue.text = $"{sign}{Mathf.RoundToInt(change)}";
        
        // 색상 설정
        if (change > 0)
        {
            changeValue.color = Color.green;
            if (changeIcon != null && increaseIcon != null)
                changeIcon.sprite = increaseIcon;
        }
        else
        {
            changeValue.color = Color.red;
            if (changeIcon != null && decreaseIcon != null)
                changeIcon.sprite = decreaseIcon;
        }
        
        // 표시
        changeIndicator.SetActive(true);
        
        // 등장 애니메이션
        CanvasGroup cg = changeIndicator.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            float fadeInTime = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < fadeInTime)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInTime);
                yield return null;
            }
            cg.alpha = 1f;
            
            // 유지
            yield return new WaitForSeconds(changeDisplayDuration - 0.6f);
            
            // 페이드아웃
            elapsed = 0f;
            float fadeOutTime = 0.3f;
            while (elapsed < fadeOutTime)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutTime);
                yield return null;
            }
            cg.alpha = 0f;
        }
        else
        {
            // CanvasGroup 없으면 단순 대기
            yield return new WaitForSeconds(changeDisplayDuration);
        }
        
        // 숨김
        changeIndicator.SetActive(false);
        changeDisplayCoroutine = null;
    }
    
    /// <summary>
    /// 서버로 호감도 업데이트 전송
    /// </summary>
    public IEnumerator SendAffectionUpdate(float change)
    {
        if (string.IsNullOrEmpty(currentPlayerId))
            yield break;
        
        string url = $"{serverUrl}/episode/affection";
        
        var requestData = new AffectionUpdateRequest
        {
            player_id = currentPlayerId,
            change = change
        };
        
        string jsonData = JsonUtility.ToJson(requestData);
        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 5;
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<AffectionUpdateResponse>(www.downloadHandler.text);
                    if (response != null && response.status == "ok")
                    {
                        UpdateAffection(response.affection, response.change);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AffectionUI] Failed to parse update response: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[AffectionUI] Failed to update affection: {www.error}");
            }
        }
    }
    
    /// <summary>
    /// 현재 호감도 값 반환
    /// </summary>
    public float GetCurrentAffection()
    {
        return currentAffection;
    }
    
    /// <summary>
    /// 현재 상태 텍스트 반환
    /// </summary>
    public string GetStatusText()
    {
        string status = GetStatusKey(currentAffection);
        return statusTextMap.TryGetValue(status, out string text) ? text : "알 수 없음";
    }
    
    #region Data Classes
    
    [System.Serializable]
    private class EpisodeStatusResponse
    {
        public string status;
        public string game_status;
        public float affection;
        public float chaos_level;
        public int turn_count;
    }
    
    [System.Serializable]
    private class AffectionUpdateRequest
    {
        public string player_id;
        public float change;
    }
    
    [System.Serializable]
    private class AffectionUpdateResponse
    {
        public string status;
        public float affection;
        public float change;
        public int turn_count;
    }
    
    #endregion
}
