# Phase 7.2: 호감도 UI 시스템 설계서

**작성일**: 2026-03-27  
**상태**: 설계 중  
**버전**: 1.0

---

## 1. 개요

### 1.1 목표
- 실시간 호감도 변화 피드백 제공
- 유저 발화 시 호감도 변화 애니메이션
- 한마디마다 얼마나 유동하는지 시각화

### 1.2 핵심 기능
1. **호감도 바**: 0-100 범위의 프로그레스 바
2. **변화 애니메이션**: 부드러운 증감 효과
3. **변화량 표시**: +/- 수치 표시
4. **색상 변화**: 호감도 레벨별 색상 구분

---

## 2. UI 컴포넌트 구조

### 2.1 계층 구조

```
Canvas
└── AffectionUI (Panel)
    ├── Background (Image)
    ├── AffectionBar (Slider)
    │   ├── Fill Area
    │   │   └── Fill (Image) - 색상 변화
    │   └── Background (Image)
    ├── AffectionValue (TMP_Text) - "50"
    ├── AffectionLabel (TMP_Text) - "호감도"
    ├── ChangeIndicator (GameObject)
    │   ├── ChangeValue (TMP_Text) - "+10" / "-5"
    │   └── ChangeIcon (Image) - ↑/↓
    └── StatusEmoji (TMP_Text) - "😊" / "😐" / "😢"
```

### 2.2 색상 체계

| 호감도 범위 | 색상 | 상태 | 이모지 |
|------------|------|------|--------|
| 80-100 | 분홍색 (#FF69B4) | 사랑 | 😍 |
| 60-79 | 연분홍 (#FFB6C1) | 호감 | 😊 |
| 40-59 | 노란색 (#FFD700) | 보통 | 😐 |
| 20-39 | 주황색 (#FFA500) | 냉소 | 😕 |
| 0-19 | 빨간색 (#FF4444) | 위험 | 😢 |

---

## 3. Unity 스크립트 설계

### 3.1 AffectionUIController.cs

```csharp
public class AffectionUIController : MonoBehaviour
{
    [Header("UI Components")]
    public Slider affectionBar;
    public TMP_Text affectionValue;
    public TMP_Text affectionLabel;
    public TMP_Text changeValue;
    public Image changeIcon;
    public TMP_Text statusEmoji;
    public Image fillImage;
    
    [Header("Animation Settings")]
    public float animationDuration = 0.5f;
    public float changeDisplayDuration = 2.0f;
    public AnimationCurve animationCurve;
    
    [Header("Colors")]
    public Color loveColor = new Color(1f, 0.41f, 0.71f);    // 분홍
    public Color likeColor = new Color(1f, 0.71f, 0.76f);   // 연분홍
    public Color neutralColor = new Color(1f, 0.84f, 0f);   // 노랑
    public Color coldColor = new Color(1f, 0.65f, 0f);      // 주황
    public Color dangerColor = new Color(1f, 0.27f, 0.27f); // 빨강
    
    [Header("Server Config")]
    public string serverUrl = "http://127.0.0.1:5000";
    
    private float currentAffection = 50f;
    private float targetAffection = 50f;
    private Coroutine animationCoroutine;
    private Coroutine changeDisplayCoroutine;
    
    // 이벤트
    public UnityEvent<float> OnAffectionChanged;
    public UnityEvent OnAffectionMax;  // 100 도달
    public UnityEvent OnAffectionMin;  // 0 도달
}
```

### 3.2 주요 메서드

```csharp
// 호감도 업데이트 (서버에서 호출)
public void UpdateAffection(float newValue, float change);

// 애니메이션으로 호감도 변경
private IEnumerator AnimateAffection(float from, float to);

// 변화량 표시
private IEnumerator ShowChangeIndicator(float change);

// 색상 업데이트
private void UpdateColor(float affection);

// 이모지 업데이트
private void UpdateEmoji(float affection);

// 서버에서 호감도 조회
public IEnumerator FetchAffectionFromServer(string playerId);

// 서버로 호감도 업데이트 전송
public IEnumerator SendAffectionUpdate(string playerId, float change);
```

---

## 4. 서버 연동

### 4.1 API 엔드포인트

| 엔드포인트 | Method | 설명 |
|-----------|--------|------|
| `/episode/status` | GET | 현재 호감도 조회 |
| `/episode/affection` | POST | 호감도 업데이트 |

### 4.2 데이터 포맷

**GET /episode/status?player_id=xxx**
```json
{
  "status": "ok",
  "game_status": "continue",
  "affection": 65.0,
  "chaos_level": 0.0,
  "turn_count": 3
}
```

**POST /episode/affection**
```json
// Request
{
  "player_id": "player_001",
  "change": 10.0
}

// Response
{
  "status": "ok",
  "affection": 75.0,
  "change": 10.0,
  "turn_count": 4
}
```

---

## 5. 애니메이션 설계

### 5.1 호감도 바 애니메이션

```
시작값 ────────────────────── 목표값
  │                              │
  └────── animationCurve ────────┘
  
예시: 50 → 75
- 0.0초: 50
- 0.25초: 62.5 (가속)
- 0.5초: 75 (완료)
```

### 5.2 변화량 표시 애니메이션

```
등장 (0.3초)
  ↓
유지 (1.4초)
  ↓
페이드아웃 (0.3초)
  
총 2초 후 자동 숨김
```

### 5.3 색상 전환

```
보통 (노랑) → 호감 (연분홍)
  
Lerp를 사용한 부드러운 전환
duration: 0.5초
```

---

## 6. 이벤트 플로우

### 6.1 유저 발화 시

```
1. 유저 발화 완료
   ↓
2. 서버 분석 (/analyze → /feedback)
   ↓
3. NPC 피드백 수신
   ↓
4. 호감도 변화 계산 (mood_change)
   ↓
5. AffectionUI.UpdateAffection(new, change) 호출
   ↓
6. 애니메이션 재생
   ↓
7. 상태 체크 (Clear/Fail)
```

### 6.2 Clear/Fail 감지

```
호감도 >= 100
   ↓
OnAffectionMax 이벤트 발생
   ↓
Clear 팝업 표시

호감도 <= 0
   ↓
OnAffectionMin 이벤트 발생
   ↓
Fail 팝업 표시
```

---

## 7. 구현 파일 목록

### 7.1 Unity 스크립트

| 파일 | 설명 |
|------|------|
| `AffectionUIController.cs` | 호감도 UI 메인 컨트롤러 |
| `AffectionAnimator.cs` | 애니메이션 헬퍼 클래스 |

### 7.2 Unity 프리팹

| 파일 | 설명 |
|------|------|
| `AffectionUI.prefab` | 호감도 UI 프리팹 |

---

## 8. 테스트 시나리오

### 8.1 기본 테스트

1. 호감도 50에서 시작
2. +10 업데이트 → 60으로 애니메이션
3. -5 업데이트 → 55로 애니메이션
4. 색상/이모지 변경 확인

### 8.2 경계값 테스트

1. 호감도 95 → +10 → 100 (Clear)
2. 호감도 5 → -10 → 0 (Fail)
3. 0 미만/100 초과 처리 확인

---

**작성자**: Cascade AI
