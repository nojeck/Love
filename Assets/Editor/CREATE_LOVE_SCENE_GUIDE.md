# CreateLoveConversationScene.cs 사용 가이드

## 개요

Unity Editor의 Tools 메뉴에서 한 번의 클릭으로 LoveConversationScene을 자동 생성할 수 있습니다.

**위치**: `Assets/Editor/CreateLoveConversationScene.cs`  
**메뉴**: Tools → Create → Love Conversation Scene

## 빠른 시작 (30초)

### 1단계: 메뉴 실행
```
Unity Editor에서:
Tools → Create → Love Conversation Scene
```

### 2단계: 자동 생성
- 모든 UI 요소가 자동으로 생성됨
- LoveConversationScene.unity가 저장됨
- "Love Conversation Scene Created" 다이얼로그 표시

### 3단계: 완료
```
Assets/Scenes/LoveConversationScene.unity
```

완전하게 구성된 씬이 준비되었습니다!

## 생성되는 구조

```
LoveConversationScene
├─ Canvas
│  ├─ Background (어두운 파란색)
│  ├─ MainLayout (VerticalLayoutGroup)
│  │  ├─ HeaderText (❤️ LOVE CONVERSATION TEST)
│  │  ├─ ControlSection
│  │  │  ├─ StatusText
│  │  │  ├─ ButtonContainer
│  │  │  │  ├─ StartButton (▶ START, 초록)
│  │  │  │  └─ StopButton (⏹ STOP, 빨강)
│  │  │  └─ SessionIdInput
│  │  ├─ ConversationSection
│  │  │  ├─ Title
│  │  │  ├─ ConversationDisplay (ScrollView)
│  │  │  │  └─ ConversationDisplayText
│  │  │  └─ NpcResponseText
│  │  └─ InfoSection
│  │     ├─ EmotionPanel
│  │     │  ├─ Title (😊 EMOTION)
│  │     │  └─ EmotionText
│  │     └─ ContextPanel
│  │        ├─ Title (📊 CONTEXT)
│  │        └─ ContextText
│  └─ EventSystem
├─ LoveConversationManager
│  ├─ LoveConversationUI
│  └─ UnityMicRecorder
└─ (모든 필드 자동 할당)
```

## 색상 설정

| 요소 | 색상 | RGB 값 |
|------|------|--------|
| 배경 | 짙은 파란색 | (0.08, 0.08, 0.12) |
| Header | 분홍색 | (1.0, 0.8, 0.8) |
| Start Button | 초록색 | (0.2, 0.8, 0.2) |
| Stop Button | 빨강색 | (0.8, 0.2, 0.2) |
| 텍스트 | 흰색 | (1.0, 1.0, 1.0) |
| 패널 | 어두운 보라색 | (0.15, 0.15, 0.25) |
| 제목 | 금색 | (1.0, 0.9, 0.7) |

## UI 요소 명세

### 버튼

**StartButton**
- 이름: "▶ START"
- 색상: 초록색 (0.2, 0.8, 0.2)
- 자동 와이어링: LoveConversationUI.OnStartClicked()

**StopButton**
- 이름: "⏹ STOP"
- 색상: 빨강색 (0.8, 0.2, 0.2)
- 자동 와이어링: LoveConversationUI.OnStopClicked()

### 텍스트 필드

**StatusText** (상태 표시)
- 기본값: "Status: Ready"
- 폰트: 20pt
- 색상: 흰색

**ConversationDisplayText** (대화 히스토리, ScrollView 내)
- 기본값: "=== Conversation History ===\n(대화가 여기에 표시됩니다)"
- 폰트: 18pt
- 스크롤: Y축 활성화

**NpcResponseText** (NPC 응답)
- 기본값: "NPC Response: (waiting...)"
- 폰트: 20pt
- 색상: 밝은 분홍색 (1, 0.8, 0.8)

**SessionIdInput** (세션 ID)
- 라벨: "Session ID:"
- 폰트: 18pt
- 자동 생성: "player_YYYYMMDD_HHmmss"

### 정보 패널

**EmotionPanel**
- 제목: "😊 EMOTION"
- 내용: emotionText (감정, Valence, Arousal)

**ContextPanel**
- 제목: "📊 CONTEXT"
- 내용: contextText (턴, 평균점수, 반복감정)

## 스크립트 기능

### CreateScene() 메인 메서드

```csharp
[MenuItem("Tools/Create/Love Conversation Scene")]
public static void CreateScene()
```

**처리 순서**:
1. 새 씬 생성 (DefaultGameObjects 포함)
2. Canvas 및 배경 생성
3. MainLayout (VerticalLayoutGroup) 생성
4. 3개 섹션 생성:
   - ControlSection (버튼 & 입력)
   - ConversationSection (대화 표시)
   - InfoSection (정보 표시)
5. LoveConversationManager 생성
6. 모든 UI 요소 자동 할당
7. EventSystem 확인
8. 씬 저장 (Assets/Scenes/LoveConversationScene.unity)

### 헬퍼 메서드

**CreateHeaderText()**
- 제목 텍스트 생성
- 폰트 크기, 색상 설정

**CreateControlSection()**
- 제어 섹션 생성
- 버튼, 입력 필드 포함

**CreateConversationSection()**
- 대화 표시 섹션 생성
- ScrollView로 스크롤 가능하게 구성

**CreateInfoSection()**
- 정보 패널 2개 생성
- 감정 & 컨텍스트 정보 표시

**CreateButton()**
- 스타일이 적용된 버튼 생성
- 색상, 텍스트, 상호작용 설정

**CreateInfoPanel()**
- 일관된 패널 생성
- 제목과 텍스트 포함

**AssignUIElements()**
- Canvas의 모든 UI 요소를 찾아 LoveConversationUI에 할당
- GameObject 이름으로 매칭

## 테스트 순서

### 1단계: 씬 생성
```
Tools → Create → Love Conversation Scene
```
✅ LoveConversationScene.unity 생성됨

### 2단계: Server 시작
```bash
cd Assets/..
python server.py
```
✅ Server running on http://127.0.0.1:5000

### 3단계: Scene 열기 및 Play
```
LoveConversationScene.unity 열기
Play 버튼 클릭
```
✅ 모든 UI 요소 표시됨

### 4단계: 기본 테스트 (1턴)
```
[▶ START] → 마이크 입력 "정말 사랑해요" → [⏹ STOP]
```

**예상 결과**:
- Status: "Analyzed: love (0.95)"
- Emotion: "love (Valence: 0.85, Arousal: 0.72)"
- NPC: "그런 마음이... 정말 좋은데? (Rules)"
- Conversation: 대화 히스토리 표시

### 5단계: 반복 감지 테스트 (3턴)

Turn 1-2: 동일하게 테스트

Turn 3:
```
[▶ START] → 마이크 입력 "자꾸만 생각나" → [⏹ STOP]
```

**예상 결과**:
- Context: "Repeated: love, Vary: true"
- NPC: "(LLM)" 표시
- was_force_llm: true

## 고급 커스터마이징

### 색상 변경

코드에서 상수 수정:
```csharp
private static readonly Color ButtonGreenColor = new Color(0.2f, 0.8f, 0.2f, 1f);
private static readonly Color ButtonRedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
// ... 기타 색상
```

### 레이아웃 변경

섹션 높이 조정:
```csharp
sectionRect.sizeDelta = new Vector2(0, 160);  // ControlSection height
sectionRect.sizeDelta = new Vector2(0, 400);  // ConversationSection height
sectionRect.sizeDelta = new Vector2(0, 120);  // InfoSection height
```

### 폰트 크기 변경

텍스트 생성 시 fontSize 수정:
```csharp
headerText.fontSize = 40;      // Header
titleText.fontSize = 24;       // Section titles
statusText.fontSize = 20;      // Status
emotionText.fontSize = 16;     // Info text
```

## 문제 해결

### 버튼이 클릭되지 않음
**확인사항**:
- EventSystem 존재 여부
- Canvas Render Mode: ScreenSpaceOverlay
- Button 컴포넌트의 Interactable 체크

**해결**:
```csharp
// 코드에서 자동으로 처리됨
if (Object.FindObjectOfType<EventSystem>() == null)
{
    GameObject es = new GameObject("EventSystem", 
        typeof(EventSystem), typeof(StandaloneInputModule));
}
```

### UI 요소가 할당되지 않음
**확인사항**:
- GameObject 이름이 정확한지 확인
- AssignUIElements() 메서드 로그 확인

**수정**:
AssignUIElements()의 찾기 로직에서:
```csharp
if (text.gameObject.name == "StatusText")
    ui.statusText = text;
```

### 모든 필드가 할당되어도 작동하지 않음
1. Play 모드에서 UI 요소가 표시되는지 확인
2. Console에서 오류 메시지 확인
3. Server 실행 여부 확인 (http://127.0.0.1:5000)

## 최종 체크리스트

- [x] 스크립트 생성됨 (CreateLoveConversationScene.cs)
- [x] Tools 메뉴에 항목 추가 ([MenuItem])
- [x] 모든 UI 요소 자동 생성
- [x] 색상/크기/폰트 설정
- [x] LoveConversationUI 필드 자동 할당
- [x] EventSystem 자동 생성
- [x] 씬 저장 기능
- [x] 완료 다이얼로그 표시

## 관련 파일

- **CreateLoveConversationScene.cs**: 씬 생성 스크립트
- **LoveConversationUI.cs**: 클라이언트 로직
- **LoveConversationScene.unity**: 생성된 씬 파일
- **CreateTestRecordScene.cs**: 참고용 (기존 씬 생성 스크립트)

---

**생성 방식**: Editor Script (한 번의 클릭)  
**소요 시간**: 30초  
**필요한 작업**: 없음 (자동 완성)  
**다음 단계**: Server 시작 → Play → 테스트
