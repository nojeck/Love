# Phase 7.3: 타임 리미트 & 자동 녹음 시스템 설계서

**작성일**: 2026-03-27  
**상태**: 설계 중  
**버전**: 1.0

---

## 1. 개요

### 1.1 목표
- 실제 상황 같은 Live 느낌 제공
- NPC 대화 시작 시 자동 녹음 모드 진입
- 타임 리미트로 긴장감 부여

### 1.2 핵심 기능
1. **첫 마디 타임 리미트**: 10초 내 응답 시작
2. **자동 녹음**: 음성 감지 시 자동 녹음 시작
3. **최대 녹음 시간**: 30초 제한
4. **침묵 처리**: 10초 초과 시 silence 반환
5. **과다 발화 처리**: 30초 초과 시 "너무 많이 말함" 반응

---

## 2. 게임플레이 흐름

### 2.1 전체 절차

```
에피소드 시작
    ↓
상황 설명 텍스트 출력 (2초)
    ↓
NPC 대화 시작 ("나 오늘 뭐 달라진 거 없어?")
    ↓
[자동 녹음 모드 ON]
    ↓
┌─────────────────────────────────────┐
│  타이머 시작 (10초)                  │
│  ↓                                   │
│  음성 감지?                           │
│  ├─ YES → 녹음 시작                  │
│  │         ↓                         │
│  │       최대 30초 녹음              │
│  │         ↓                         │
│  │       음성 종료 감지 → 분석       │
│  │                                    │
│  └─ NO (10초 초과)                   │
│            ↓                         │
│          silence 반환                │
└─────────────────────────────────────┘
    ↓
분석 & 피드백
    ↓
호감도 업데이트
    ↓
다음 턴 or Clear/Fail
```

### 2.2 타이밍 다이어그램

```
NPC 대화 완료
│
├─ 0초: 타이머 시작, 녹음 대기
│
├─ 0~10초: 음성 감지 대기
│   │
│   ├─ 음성 감지 시: 녹음 시작
│   │   └─ 최대 30초 녹음
│   │
│   └─ 10초 초과: silence 처리
│
└─ 분석 진행
```

---

## 3. Unity 컴포넌트 설계

### 3.1 UI 구조

```
Canvas
└── RecordingUI (Panel)
    ├── TimerDisplay (GameObject)
    │   ├── TimerBar (Slider) - 10초 카운트다운
    │   ├── TimerText (TMP_Text) - "10"
    │   └── TimerLabel (TMP_Text) - "응답 대기"
    │
    ├── RecordingIndicator (GameObject)
    │   ├── MicIcon (Image) - 마이크 아이콘
    │   ├── RecordingStatus (TMP_Text) - "녹음 중..."
    │   ├── RecordingTime (TMP_Text) - "00:05"
    │   └── Waveform (Image) - 파형 표시
    │
    └── StatusMessage (TMP_Text)
        - "말씀해주세요!"
        - "녹음 중입니다..."
        - "시간 초과..."
```

### 3.2 AutoRecordingController.cs

```csharp
public class AutoRecordingController : MonoBehaviour
{
    [Header("Timer Settings")]
    public float firstResponseLimit = 10f;     // 첫 마디 제한
    public float maxRecordingTime = 30f;       // 최대 녹음 시간
    public float silenceThreshold = 0.01f;     // 침묵 감지 임계값
    public float silenceDuration = 2f;         // 침묵 지속 시간 (자동 종료)
    
    [Header("UI References")]
    public Slider timerBar;
    public TMP_Text timerText;
    public GameObject recordingIndicator;
    public TMP_Text recordingTime;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public int sampleRate = 16000;
    
    // 상태
    public enum RecordingState
    {
        Idle,           // 대기
        WaitingVoice,   // 음성 대기 (10초 타이머)
        Recording,      // 녹음 중
        Processing      // 분석 중
    }
    
    private RecordingState currentState;
    private float stateTimer;
    private List<float> recordedSamples;
    private bool voiceDetected;
    
    // 이벤트
    public UnityEvent OnRecordingStart;
    public UnityEvent<float> OnRecordingComplete;  // 녹음 완료 (지속 시간)
    public UnityEvent OnSilenceTimeout;            // 10초 침묵
    public UnityEvent OnMaxTimeExceeded;           // 30초 초과
}
```

### 3.3 주요 메서드

```csharp
// 녹음 모드 시작 (NPC 대화 완료 시 호출)
public void StartRecordingMode();

// 음성 감지
private bool DetectVoice(float[] samples);

// 녹음 시작
private void StartRecording();

// 녹음 종료 및 분석 요청
private void StopRecordingAndAnalyze();

// 침묵 처리 (10초 초과)
private void HandleSilenceTimeout();

// 과다 발화 처리 (30초 초과)
private void HandleMaxTimeExceeded();

// 타이머 업데이트
private void UpdateTimerDisplay();

// 상태 전환
private void ChangeState(RecordingState newState);
```

---

## 4. 상태 머신

### 4.1 상태 전이도

```
         ┌─────────────────────────────────────────┐
         │                                         │
         ▼                                         │
    ┌─────────┐                                    │
    │  Idle   │                                    │
    └────┬────┘                                    │
         │ StartRecordingMode()                    │
         ▼                                         │
    ┌─────────────┐                                │
    │ WaitingVoice│ ──10초 초과──→ [Silence] ────→│
    └─────┬───────┘                                │
          │ 음성 감지                               │
          ▼                                         │
    ┌───────────┐                                   │
    │ Recording │ ──30초 초과──→ [TooLong] ──────→│
    └─────┬─────┘                                   │
          │ 음성 종료                                │
          ▼                                         │
    ┌────────────┐                                  │
    │ Processing │ ──분석 완료──→ [Complete] ─────→│
    └────────────┘                                  │
         │                                          │
         └──────────────────────────────────────────┘
                          ↓
                      Idle 복귀
```

### 4.2 상태별 동작

| 상태 | 타이머 | UI | 이벤트 |
|------|--------|-----|--------|
| Idle | - | 숨김 | - |
| WaitingVoice | 10초 카운트다운 | "응답 대기" 타이머 표시 | 10초 초과 시 OnSilenceTimeout |
| Recording | 30초 카운트업 | "녹음 중" 표시 | 30초 초과 시 OnMaxTimeExceeded |
| Processing | - | "분석 중..." 표시 | 분석 완료 시 OnRecordingComplete |

---

## 5. 음성 감지 알고리즘

### 5.1 실시간 음성 감지

```csharp
private bool DetectVoice(float[] samples)
{
    // RMS 계산
    float sum = 0f;
    for (int i = 0; i < samples.Length; i++)
    {
        sum += samples[i] * samples[i];
    }
    float rms = Mathf.Sqrt(sum / samples.Length);
    
    // 임계값 이상이면 음성 감지
    return rms > silenceThreshold;
}
```

### 5.2 침묵 종료 감지

```csharp
// 녹음 중 연속 침묵 감지
private float silenceTimer = 0f;

void Update()
{
    if (currentState == RecordingState.Recording)
    {
        float[] samples = GetMicrophoneSamples();
        
        if (DetectVoice(samples))
        {
            silenceTimer = 0f;
        }
        else
        {
            silenceTimer += Time.deltaTime;
            
            if (silenceTimer >= silenceDuration)
            {
                // 2초 연속 침묵 → 녹음 종료
                StopRecordingAndAnalyze();
            }
        }
    }
}
```

---

## 6. 서버 연동

### 6.1 API 호출

| 상황 | API | 데이터 |
|------|-----|--------|
| 녹음 완료 | POST /analyze | 오디오 파일 |
| 침묵 처리 | POST /feedback | transcript="" |
| 과다 발화 | POST /feedback | transcript + 특수 처리 |

### 6.2 침묵 처리

```json
// POST /feedback (silence)
{
  "session_id": "player_001",
  "transcript": "",
  "emotion": "silence",
  "score": 0.0
}

// Response
{
  "feedback": "...침묵이 흐른다. 그녀의 표정이 어두워진다.",
  "npc_emotion": "disappointment",
  "mood_change": -10.0
}
```

### 6.3 과다 발화 처리

```json
// POST /feedback (too long)
{
  "session_id": "player_001",
  "transcript": "긴 텍스트...",
  "emotion": "neutral",
  "score": 0.3,
  "is_too_long": true
}

// Response
{
  "feedback": "...말이 너무 길어. 핵심만 말해봐.",
  "npc_emotion": "frustration",
  "mood_change": -5.0
}
```

---

## 7. Unity 구현 파일

### 7.1 스크립트

| 파일 | 설명 |
|------|------|
| `AutoRecordingController.cs` | 자동 녹음 컨트롤러 |
| `VoiceDetector.cs` | 음성 감지 유틸리티 |
| `TimerDisplay.cs` | 타이머 UI 컨트롤러 |

### 7.2 프리팹

| 파일 | 설명 |
|------|------|
| `RecordingUI.prefab` | 녹음 UI 프리팹 |

---

## 8. 테스트 시나리오

### 8.1 정상 흐름

1. NPC 대화 완료 → 자동 녹음 모드 진입
2. 3초 후 음성 감지 → 녹음 시작
3. 5초 발화 후 침묵 → 녹음 종료
4. 분석 → 피드백

### 8.2 침묵 처리

1. NPC 대화 완료 → 자동 녹음 모드 진입
2. 10초 동안 음성 없음
3. silence 처리 → 호감도 -10

### 8.3 과다 발화

1. NPC 대화 완료 → 자동 녹음 모드 진입
2. 음성 감지 → 녹음 시작
3. 30초 초과 → "너무 많이 말함" 피드백

---

**작성자**: Cascade AI
