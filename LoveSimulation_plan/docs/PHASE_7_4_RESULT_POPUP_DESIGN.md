# Phase 7.4: 클리어/실패 & 회귀 시스템 설계서

**작성일**: 2026-03-27  
**상태**: 설계 중  
**버전**: 1.0

---

## 1. 개요

### 1.1 목표
- 명확한 목표와 피드백 제공
- 성공/실패 시 시각적 보상
- 실패 시 재도전 기회 제공

### 1.2 핵심 기능
1. **Clear 팝업**: 호감도 100 달성 시
2. **Fail 팝업**: 호감도 0 도달 시
3. **대화 분석 피드백**: 실패 원인 분석
4. **회귀(Revert) 기능**: 에피소드 처음으로 이동

---

## 2. UI 구조

### 2.1 Clear 팝업

```
┌─────────────────────────────────────┐
│         ★ CLEAR! ★                  │
│                                     │
│    [NPC 초상화 - 행복한 표정]        │
│                                     │
│    "달콤한 데이트"                   │
│    호감도 100 달성!                 │
│                                     │
│    총 턴 수: 7                       │
│    평균 점수: 0.75                   │
│                                     │
│    [다음 에피소드]  [메인 메뉴]      │
└─────────────────────────────────────┘
```

### 2.2 Fail 팝업

```
┌─────────────────────────────────────┐
│         ✗ FAIL...                   │
│                                     │
│    [NPC 초상화 - 슬픈 표정]          │
│                                     │
│    "어색한 침묵"                     │
│    호감도가 바닥났다...              │
│                                     │
│    ─── 대화 분석 ───                │
│    • 3턴: 침묵 (10초 응답 실패)     │
│    • 5턴: 무례한 발언 감지           │
│    • 7턴: 주제 이탈                  │
│                                     │
│    💡 개선 팁:                       │
│    그녀의 변화에 더 관심을 가져보세요│
│                                     │
│    [회귀하기]  [메인 메뉴]           │
└─────────────────────────────────────┘
```

### 2.3 Unity 계층 구조

```
Canvas
└── ResultPopup (Panel)
    ├── Background (Image) - 반투명
    ├── PopupFrame (Image)
    │   ├── TitleText (TMP_Text)
    │   ├── ResultIcon (Image)
    │   ├── NPCPortrait (Image)
    │   ├── ResultTitle (TMP_Text)
    │   ├── ResultDescription (TMP_Text)
    │   │
    │   ├── StatsPanel (GameObject)
    │   │   ├── TurnCount (TMP_Text)
    │   │   ├── AvgScore (TMP_Text)
    │   │   └── FinalAffection (TMP_Text)
    │   │
    │   ├── AnalysisPanel (GameObject) - Fail만
    │   │   ├── AnalysisTitle (TMP_Text)
    │   │   ├── AnalysisContent (TMP_Text)
    │   │   └── TipsContent (TMP_Text)
    │   │
    │   └── ButtonPanel (GameObject)
    │       ├── RevertButton (Button)
    │       ├── NextEpisodeButton (Button)
    │       └── MainMenuButton (Button)
```

---

## 3. Unity 스크립트 설계

### 3.1 ResultPopupController.cs

```csharp
public class ResultPopupController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject popupPanel;
    public TMP_Text titleText;
    public Image resultIcon;
    public Image npcPortrait;
    public TMP_Text resultTitle;
    public TMP_Text resultDescription;
    
    [Header("Stats")]
    public TMP_Text turnCountText;
    public TMP_Text avgScoreText;
    public TMP_Text finalAffectionText;
    
    [Header("Analysis (Fail Only)")]
    public GameObject analysisPanel;
    public TMP_Text analysisContent;
    public TMP_Text tipsContent;
    
    [Header("Buttons")]
    public Button revertButton;
    public Button nextEpisodeButton;
    public Button mainMenuButton;
    
    [Header("Sprites")]
    public Sprite clearIcon;
    public Sprite failIcon;
    public Sprite happyPortrait;
    public Sprite sadPortrait;
    
    // 이벤트
    public UnityEvent OnRevert;
    public UnityEvent OnNextEpisode;
    public UnityEvent OnMainMenu;
    
    /// <summary>
    /// Clear 팝업 표시
    /// </summary>
    public void ShowClearPopup(int turnCount, float avgScore, int episodeId);
    
    /// <summary>
    /// Fail 팝업 표시
    /// </summary>
    public void ShowFailPopup(int turnCount, float avgScore, List<AnalysisItem> analysis);
    
    /// <summary>
    /// 팝업 숨김
    /// </summary>
    public void HidePopup();
}
```

### 3.2 DialogueAnalyzer.cs

```csharp
public class DialogueAnalyzer
{
    public class AnalysisResult
    {
        public List<string> issues;       // 문제점 목록
        public List<string> tips;         // 개선 팁
        public float silenceRatio;        // 침묵 비율
        public float insultRatio;         // 무례 발언 비율
        public float offtopicRatio;       // 주제 이탈 비율
    }
    
    /// <summary>
    /// 대화 기록 분석
    /// </summary>
    public AnalysisResult Analyze(List<DialogueHistory> history);
    
    /// <summary>
    /// 문제점 추출
    /// </summary>
    private List<string> ExtractIssues(List<DialogueHistory> history);
    
    /// <summary>
    /// 개선 팁 생성
    /// </summary>
    private List<string> GenerateTips(AnalysisResult result);
}
```

---

## 4. 회귀(Revert) 기능

### 4.1 동작 흐름

```
[회귀하기] 버튼 클릭
    ↓
1. 현재 에피소드 상태 초기화
   - 호감도 → 초기값 (50)
   - Chaos → 0
   - Turn → 0
   - 대화 기록 삭제
    ↓
2. 서버에 상태 리셋 요청
   POST /episode/revert
    ↓
3. 첫 상황부터 다시 시작
   - 상황 텍스트 출력
   - NPC 대화 시작
```

### 4.2 서버 API

**POST /episode/revert**
```json
// Request
{
  "player_id": "player_001",
  "episode_id": 1
}

// Response
{
  "status": "ok",
  "message": "Episode reverted",
  "episode": {
    "id": 1,
    "title": "달라진 점 찾기의 미궁"
  },
  "initial_state": {
    "affection": 50.0,
    "chaos_level": 0.0,
    "turn_count": 0
  }
}
```

---

## 5. 대화 분석 알고리즘

### 5.1 분석 항목

| 항목 | 감지 조건 | 비율 계산 |
|------|-----------|-----------|
| 침묵 | transcript == "" | silence_count / total_turns |
| 무례 발언 | insult_keywords 포함 | insult_count / total_turns |
| 주제 이탈 | score < 0.3 | low_score_count / total_turns |
| 반복 감정 | same_emotion 3회 연속 | repeated / total_turns |

### 5.2 분석 예시

```json
{
  "issues": [
    "3턴: 침묵 (10초 응답 실패)",
    "5턴: 무례한 발언 감지 ('짜증나')",
    "7턴: 주제 이탈 (score 0.2)"
  ],
  "tips": [
    "그녀의 변화에 더 관심을 가져보세요",
    "감정을 배려하는 발언을 시도해보세요",
    "침묵을 피하고 적극적으로 대화하세요"
  ],
  "silence_ratio": 0.14,
  "insult_ratio": 0.14,
  "offtopic_ratio": 0.14
}
```

---

## 6. 서버 구현

### 6.1 episode_manager.py 추가 메서드

```python
def revert_episode(self, player_id: str) -> Dict:
    """
    에피소드 회귀 (초기화)
    
    Args:
        player_id: 플레이어 ID
        
    Returns:
        초기화된 상태
    """
    # 현재 에피소드 ID 조회
    state = self.get_player_state(player_id)
    if state is None:
        return None
    
    episode_id = state["episode_id"]
    episode = self._load_episode(episode_id)
    
    # 상태 초기화
    initial_affection = episode.get("initial_affection", 50.0)
    first_situation = episode["situations"][0]
    
    conn = sqlite3.connect(self.db_path)
    cursor = conn.cursor()
    
    # 상태 리셋
    cursor.execute('''
        UPDATE player_state 
        SET situation_id = ?, affection = ?, turn_count = ?, chaos_level = ?, updated_at = ?
        WHERE player_id = ?
    ''', (
        first_situation["situation_id"],
        initial_affection,
        0,
        0.0,
        datetime.now().isoformat(),
        player_id
    ))
    
    # 대화 기록 삭제
    cursor.execute('DELETE FROM dialogue_history WHERE player_id = ?', (player_id,))
    
    conn.commit()
    conn.close()
    
    return {
        "episode": self._get_episode_info(episode_id),
        "initial_state": {
            "affection": initial_affection,
            "chaos_level": 0.0,
            "turn_count": 0
        }
    }

def analyze_dialogue(self, player_id: str) -> Dict:
    """
    대화 분석
    
    Args:
        player_id: 플레이어 ID
        
    Returns:
        분석 결과
    """
    history = self.get_dialogue_history(player_id)
    
    if not history:
        return {"issues": [], "tips": []}
    
    issues = []
    total_turns = len(history)
    
    # 침묵 감지
    silence_count = sum(1 for h in history if not h["text"] or h["text"] == "(silence)")
    if silence_count > 0:
        issues.append(f"침묵 {silence_count}회 ({silence_count/total_turns*100:.0f}%)")
    
    # 저점수 감지
    low_score_count = sum(1 for h in history if h.get("score", 0.5) < 0.3)
    if low_score_count > 0:
        issues.append(f"부정적 반응 {low_score_count}회")
    
    # 팁 생성
    tips = []
    if silence_count > 0:
        tips.append("침묵을 피하고 적극적으로 대화하세요")
    if low_score_count > 0:
        tips.append("상대방의 감정을 배려하는 발언을 시도해보세요")
    
    return {
        "issues": issues,
        "tips": tips,
        "silence_ratio": silence_count / total_turns if total_turns > 0 else 0,
        "low_score_ratio": low_score_count / total_turns if total_turns > 0 else 0
    }
```

---

## 7. 구현 파일 목록

### 7.1 Unity 스크립트

| 파일 | 설명 |
|------|------|
| `ResultPopupController.cs` | 결과 팝업 컨트롤러 |
| `DialogueAnalyzer.cs` | 대화 분석 유틸리티 |

### 7.2 서버

| 파일 | 설명 |
|------|------|
| `episode_manager.py` | revert_episode, analyze_dialogue 추가 |

---

## 8. 테스트 시나리오

### 8.1 Clear 테스트

1. 호감도 100 달성
2. Clear 팝업 표시 확인
3. 통계 표시 확인
4. 다음 에피소드 버튼 동작

### 8.2 Fail 테스트

1. 호감도 0 도달
2. Fail 팝업 표시 확인
3. 분석 내용 표시 확인
4. 회귀 버튼 동작
5. 에피소드 처음부터 재시작

---

**작성자**: Cascade AI
