# Phase 5.5: Unity 클라이언트 업데이트 - 연애대화 테스트 씬

## 개요

Phase 5에서 구현한 Server 통합 기능을 Unity 클라이언트에서 활용하는 새로운 테스트 씬을 구현했습니다.

- **기존 테스트 씬**: `TestRecordScene` - 음성 분석 및 캘리브레이션 테스트
- **신규 테스트 씬**: `LoveConversationScene` - 연애대화 전체 플로우 테스트

## 구현 내용

### 1. LoveConversationUI.cs 생성

새로운 클라이언트 스크립트로, 다음 기능을 포함합니다:

#### 주요 기능
- **음성 녹음**: UnityMicRecorder를 통한 음성 캡처
- **Server /analyze 연동**:
  - 음성 전송 및 텍스트 점수 수신
  - `conversation_context` 필드 처리 (총 턴, 평균 점수, 반복 감정)
  - 감정 및 신뢰도 표시

- **Server /feedback 연동**:
  - NPC 피드백 자동 요청
  - `was_force_llm` 플래그로 LLM 사용 여부 표시
  - `should_vary_response` 기반 NPC 반응 다양화 추적

- **Server /conversation-status 연동**:
  - 현재 대화 상태 조회
  - 총 턴, 평균 점수, 반복 감정 확인

- **대화 히스토리 관리**:
  - 플레이어 발화, 감정, 점수, NPC 반응 저장
  - 실시간 대화 표시

#### UI 구성
```
┌─────────────────────────────────────────────┐
│         LOVE CONVERSATION TEST              │
├─────────────────────────────────────────────┤
│                                             │
│  [Start Recording] [Stop Recording]        │
│                                             │
│  Status: Ready                              │
│                                             │
│  ┌───────────────────────────────────────┐ │
│  │ Conversation Display                  │ │
│  │ [12:34:56] love (0.95)               │ │
│  │ Player: 정말 사랑해요                  │ │
│  │ NPC: 그런 마음이... 정말 좋은데?      │ │
│  └───────────────────────────────────────┘ │
│                                             │
│  Emotion: love          Context:            │
│  Valence: 0.85          Turns: 1            │
│  Arousal: 0.72          Avg Score: 0.95    │
│                         Repeated: None      │
│                         Vary: false         │
│                                             │
│  NPC Response:                              │
│  그런 마음이... 정말 좋은데? (Rules)       │
│                                             │
└─────────────────────────────────────────────┘
```

### 2. 데이터 구조

```csharp
// /analyze 응답
{
  "transcript": "정말 사랑해요",
  "text_score": 0.95,
  "emotion": {
    "emotion": "love",
    "valence": 0.85,
    "arousal": 0.72
  },
  "conversation_context": {
    "total_turns": 1,
    "avg_score": 0.95,
    "repeated_emotions": [],
    "should_vary_response": false,
    "personality": "romantic"
  }
}

// /feedback 요청
{
  "session_id": "player_20260323_133121",
  "transcript": "정말 사랑해요",
  "emotion": "love",
  "score": 0.95,
  "audio_score": 0.95
}

// /feedback 응답
{
  "feedback": "그런 마음이... 정말 좋은데?",
  "variation_type": "rules",
  "was_force_llm": false,
  "session_id": "player_20260323_133121"
}

// /conversation-status 응답
{
  "total_turns": 1,
  "average_score": 0.95,
  "repeated_emotions": [],
  "emotion_counts": {"love": 1}
}
```

### 3. 클라이언트 플로우

```
1. 사용자가 "Start Recording" 클릭
   ↓
2. 음성 캡처 (UnityMicRecorder)
   ↓
3. 사용자가 "Stop Recording" 클릭
   ↓
4. Server /analyze 호출
   ├─ 음성 분석
   ├─ 텍스트 점수 계산
   ├─ 감정 인식
   └─ 대화 컨텍스트 반환
   ↓
5. 응답 파싱 (AnalyzeResponse)
   ├─ 감정 표시
   ├─ 컨텍스트 표시 (반복 감정, should_vary_response)
   └─ NPC 피드백 자동 요청
   ↓
6. Server /feedback 호출
   ├─ NPC 피드백 생성
   ├─ 반복 감정 시 LLM 강제
   └─ was_force_llm 플래그 반환
   ↓
7. NPC 반응 표시
   ├─ 피드백 텍스트
   ├─ 생성 방식 (rules/llm)
   └─ 대화 히스토리에 추가
   ↓
8. 사용자가 다시 녹음 (스텝 1로 돌아감)
```

### 4. 주요 메소드

#### `OnAnalyzeResponse(string response)`
- Server /analyze 응답 처리
- 감정, 점수, 컨텍스트 파싱
- NPC 피드백 자동 요청

#### `RequestNPCFeedback()`
- Coroutine으로 /feedback 엔드포인트 호출
- 대화 세션 ID, 발화, 감정, 점수 전송

#### `OnFeedbackReceived(FeedbackResponse feedback)`
- NPC 반응 표시
- 대화 히스토리에 턴 추가
- `was_force_llm` 플래그 표시

#### `QueryConversationStatus()`
- (선택) /conversation-status 엔드포인트 호출
- 현재 대화 상태 확인

#### `UpdateConversationDisplay()`
- 대화 히스토리를 TextMeshPro 텍스트로 포맷
- 실시간 업데이트

## 테스트 시나리오

### 시나리오 1: 기본 연애대화 (반복 없음)
```
Turn 1: "정말 사랑해요" (love, 0.95)
  → NPC: "그런 마음이... 정말 좋은데?" (Rules)
  
Turn 2: "너를 정말 좋아해" (love, 0.92)
  → NPC: "넌 정말 특별한데..." (Rules)
  
Turn 3: "자꾸만 생각나" (love, 0.88)
  → NPC: "나도 자꾸 너를 생각해..." (Rules)
  
Conversation Context:
  - total_turns: 3
  - avg_score: 0.917
  - repeated_emotions: ["love"] (3회 반복 감지)
  - should_vary_response: true ← 반복 감지, 다양화 필요
```

### 시나리오 2: 반복 감지 후 LLM 강제
```
Turn 3 (위와 동일)에서:
  - repeated_emotions에 "love"가 감지됨
  - should_vary_response: true
  → Server /feedback에서 force_llm: true 자동 설정
  → NPC 응답이 LLM으로 생성됨
  → was_force_llm: true 플래그 반환
```

### 시나리오 3: 감정 변화
```
Turn 1-3: love 반복
Turn 4: "불안해" (anxiety, 0.75)
  → NPC: "뭔가 걱정되는 거 있어?" (Rules)
  → repeated_emotions이 갱신됨
  → should_vary_response: false로 리셋
```

## 기존 TestRecordScene과의 차이점

| 항목 | TestRecordScene | LoveConversationScene |
|------|-----------------|----------------------|
| 목적 | 음성 분석 & 캘리브레이션 | 연애대화 전체 플로우 |
| 주요 기능 | 음성 점수, 캘리브레이션 | 감정 인식, NPC 피드백, 대화 추적 |
| Server 엔드포인트 | /analyze, /calibrate | /analyze, /feedback, /conversation-status |
| 응답 처리 | 음성 메트릭 표시 | 대화 컨텍스트 기반 처리 |
| 대화 추적 | 없음 | 히스토리 관리 |
| session_id | 없음 | client_req_id / session_id 사용 |
| NPC 상호작용 | 없음 | 피드백 생성 & 표시 |

## Server 통합 체크리스트

- [x] `/analyze` 엔드포인트 연동
  - [x] 음성 전송
  - [x] `conversation_context` 파싱
  - [x] 감정 정보 표시
  
- [x] `/feedback` 엔드포인트 연동
  - [x] NPC 피드백 요청
  - [x] `was_force_llm` 플래그 처리
  - [x] 대화 히스토리 저장

- [x] `/conversation-status` 엔드포인트 연동
  - [x] 상태 조회 메소드 구현
  - [x] (선택) UI 버튼 추가 가능

- [x] Session 관리
  - [x] Session ID 생성 (client_req_id)
  - [x] 대화 추적 (player_text, emotion, score, npc_response)

## 사용 방법

### Unity에서 실행

1. **새 씬 로드**:
   - Assets/Scenes/LoveConversationScene.unity 열기

2. **Game 객체 설정**:
   - 빈 GameObject 생성 후 이름: "LoveConversationManager"
   - `LoveConversationUI` 스크립트 추가
   - `UnityMicRecorder` 스크립트도 추가

3. **UI 요소 생성** (또는 자동 와이어링):
   - Button: "StartButton" (Start Recording)
   - Button: "StopButton" (Stop Recording)
   - TMP_Text: "StatusText" (상태 표시)
   - TMP_Text: "ConversationDisplayText" (대화 히스토리)
   - TMP_Text: "NpcResponseText" (NPC 응답)
   - TMP_Text: "EmotionText" (감정 표시)
   - TMP_Text: "ContextText" (컨텍스트 정보)
   - TMP_InputField: "SessionIdInput" (세션 ID)
   - TMP_Dropdown: "NpcPersonalityDropdown" (NPC 성격 선택)

4. **Server 시작**:
   ```bash
   cd Assets/..  # Server 폴더로 이동
   python server.py
   ```

5. **Play 및 테스트**:
   - Play 클릭
   - "Start Recording" → 말하기 → "Stop Recording"
   - 자동으로 음성 분석 및 NPC 피드백 생성

### 프로그래밍 통합

```csharp
public class GameManager : MonoBehaviour
{
    private LoveConversationUI conversationUI;
    
    void Start()
    {
        conversationUI = GetComponent<LoveConversationUI>();
    }
    
    // 대화 상태 조회
    void CheckConversationStatus()
    {
        conversationUI.OnQueryConversationStatus();
    }
}
```

## 다음 단계

### Priority 1: Scene 구성
- [ ] Canvas 및 UI 레이아웃 생성
- [ ] 자동 와이어링 검증
- [ ] 초기 플레이 테스트

### Priority 2: 고급 기능
- [ ] NPC 성격별 피드백 스타일 (romantic, mysterious, playful, serious)
- [ ] 대화 저장 & 로드 (JSON 파일)
- [ ] 플레이어 감정 호 표시 (그래프)

### Priority 3: 게임 통합
- [ ] 실제 게임 씬에 LoveConversationUI 통합
- [ ] NPC 캐릭터 모델 연동
- [ ] 배경음악 & 효과음 추가

## 기술 세부사항

### 세션 관리
- Session ID: `player_YYYYMMDD_HHmmss` 형식으로 자동 생성
- 각 대화마다 고유 세션 유지
- Server의 SQLite DB에 자동 저장

### 감정 감지
- Server에서 규칙 기반 감정 인식 (TextScorer)
- 신뢰도 < 0.6일 때 LLM 폴백
- 3회 이상 반복 시 LLM 강제 활성화

### 대화 컨텍스트
- `total_turns`: 현재까지의 총 턴 수
- `avg_score`: 평균 감정 점수
- `repeated_emotions`: 3회 이상 반복된 감정 목록
- `should_vary_response`: 반복 감지 시 true (NPC가 다양한 반응 필요)

### 대역폭 최적화
- 음성 파일만 Server로 전송 (분석은 Server에서)
- JSON 응답으로 필요한 정보만 반환
- 대화 히스토리는 Server DB에서 관리

## 알려진 제한사항

1. **JsonUtility 제약**:
   - 중첩 배열이 있는 경우 직접 파싱 어려움
   - `repeated_emotions` 배열은 별도 처리 필요

2. **실시간 성능**:
   - 각 피드백 요청마다 Server 호출
   - 네트워크 지연이 대화 흐름에 영향

3. **다중 세션**:
   - 현재 하나의 LoveConversationUI만 지원
   - 동시에 여러 세션을 관리하려면 확장 필요

## 코드 구조

```
LoveConversationUI.cs
├── UI 와이어링
│   ├── Start() - 버튼 및 드롭다운 초기화
│   └── AutoWireUI() - 자동 UI 탐색 (TestRecorderUI 참고)
│
├── 음성 녹음
│   ├── OnStartClicked() - 녹음 시작
│   └── OnStopClicked() - 녹음 중지 및 전송
│
├── Server 연동
│   ├── OnAnalyzeResponse() - /analyze 응답 처리
│   ├── RequestNPCFeedback() - /feedback 요청
│   ├── OnFeedbackReceived() - /feedback 응답 처리
│   └── QueryConversationStatus() - /conversation-status 조회
│
├── UI 업데이트
│   ├── UpdateConversationDisplay() - 대화 히스토리 표시
│   └── UpdateStatus() - 상태 메시지 표시
│
└── 데이터 구조
    ├── ConversationTurn - 한 턴의 데이터
    ├── AnalyzeResponse - /analyze 응답 구조
    ├── FeedbackResponse - /feedback 응답 구조
    └── ConversationStatusResponse - /conversation-status 응답 구조
```

## 테스트 결과

### 단위 테스트
- [x] JsonUtility 파싱 검증
- [x] Coroutine 실행 검증
- [x] UI 업데이트 검증

### 통합 테스트 (Play 모드)
- [ ] 음성 캡처 → Server 전송 → 응답 수신
- [ ] 감정 인식 및 표시
- [ ] NPC 피드백 생성 및 표시
- [ ] 대화 히스토리 누적
- [ ] 반복 감지 후 LLM 강제

## 참고 문서

- [PHASE_5_REPORT.md](./PHASE_5_REPORT.md) - Server 통합 API 상세
- [HYBRID_SUMMARY.md](./HYBRID_SUMMARY.md) - 감정 점수 시스템
- [conversation_history.py](./conversation_history.py) - 대화 히스토리 관리 로직

---

**작성 일시**: 2026-03-23
**버전**: 1.0 (Phase 5.5)
**상태**: 구현 완료, 씬 구성 필요
