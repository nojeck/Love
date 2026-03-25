# LoveConversationScene 설정 가이드 (수동 구성)

## 빠른 설정 (5분)

### 1단계: Canvas 생성
```
Hierarchy에서 우클릭 → UI → Canvas - TextMeshPro
```

Canvas 설정:
- Render Mode: **Screen Space - Overlay**
- Canvas Scaler → Reference Resolution: **1920 x 1080**

### 2단계: 배경 생성
Canvas 자식으로:
```
우클릭 → UI → Panel - TextMeshPro
이름: "Background"
```

Background 설정:
- Image Color: **Dark Blue (R: 0.08, G: 0.08, B: 0.12, A: 1.0)**
- Rect Transform → Anchor Presets: **Stretch > Stretch (Alt+Shift+E)**
- Left/Right/Top/Bottom: **0**

### 3단계: 메인 레이아웃 생성
Canvas 자식으로:
```
우클릭 → Create Empty
이름: "MainLayout"
```

MainLayout 설정:
- **Add Component → Vertical Layout Group**
  - Child Force Expand Height: **OFF**
  - Child Force Expand Width: **ON**
  - Padding: **Left: 20, Right: 20, Top: 20, Bottom: 20**
  - Spacing: **15**
- Rect Transform → Anchor Presets: **Stretch > Stretch**
- Left/Right/Top/Bottom: **0**

### 4단계: Header 텍스트
MainLayout 자식으로:
```
우클릭 → TextMeshPro - Text
이름: "HeaderText"
```

HeaderText 설정:
- Text: **"❤️ LOVE CONVERSATION TEST"**
- Font Size: **40**
- Color: **Pink (R: 1, G: 0.8, B: 0.8)**
- Alignment: **Center**
- Rect Transform → Height: **60**

### 5단계: 제어 섹션 (Recording Controls)
MainLayout 자식으로:
```
우클릭 → Create Empty
이름: "ControlSection"
```

ControlSection 설정:
- **Add Component → Vertical Layout Group**
  - Child Force Expand Height: **OFF**
  - Spacing: **10**
- Rect Transform → Height: **160**

#### 5-1: 상태 텍스트
ControlSection 자식으로:
```
우클릭 → TextMeshPro - Text
이름: "StatusText"
```

StatusText 설정:
- Text: **"Status: Ready"**
- Font Size: **20**
- Color: **White**
- Rect Transform → Height: **30**

#### 5-2: 버튼 컨테이너
ControlSection 자식으로:
```
우클릭 → Create Empty
이름: "ButtonContainer"
```

ButtonContainer 설정:
- **Add Component → Horizontal Layout Group**
  - Child Force Expand Height: **ON**
  - Child Force Expand Width: **ON**
  - Spacing: **10**
- Rect Transform → Height: **60**

#### 5-3: Start 버튼
ButtonContainer 자식으로:
```
우클릭 → UI → Button - TextMeshPro
이름: "StartButton"
```

StartButton 설정:
- Image Color: **Green (R: 0.2, G: 0.8, B: 0.2)**
- Text → Text: **"▶ START"**
- Text → Font Size: **22**
- Text → Color: **White**

#### 5-4: Stop 버튼
ButtonContainer 자식으로:
```
우클릭 → UI → Button - TextMeshPro
이름: "StopButton"
```

StopButton 설정:
- Image Color: **Red (R: 0.8, G: 0.2, B: 0.2)**
- Text → Text: **"⏹ STOP"**
- Text → Font Size: **22**
- Text → Color: **White**

#### 5-5: Session ID 입력
ControlSection 자식으로:
```
우클릭 → UI → Input Field - TextMeshPro
이름: "SessionIdInput"
```

SessionIdInput 설정:
- Placeholder Text: **"Enter session ID or leave for auto-generate"**
- Text (Child) → Font Size: **18**
- Rect Transform → Height: **40**

### 6단계: 대화 히스토리 섹션
MainLayout 자식으로:
```
우클릭 → Create Empty
이름: "ConversationSection"
```

ConversationSection 설정:
- **Add Component → Vertical Layout Group**
  - Child Force Expand Height: **OFF**
  - Spacing: **10**
- Rect Transform → Height: **400**

#### 6-1: 섹션 제목
ConversationSection 자식으로:
```
우클릭 → TextMeshPro - Text
이름: "SectionTitle"
```

SectionTitle 설정:
- Text: **"💬 CONVERSATION HISTORY"**
- Font Size: **24**
- Color: **Gold (R: 1, G: 0.9, B: 0.7)**
- Rect Transform → Height: **35**

#### 6-2: 대화 Display
ConversationSection 자식으로:
```
우클릭 → UI → Scroll View - TextMeshPro
이름: "ConversationDisplay"
```

ConversationDisplay 설정:
- Image Color: **Dark (R: 0.1, G: 0.1, B: 0.15)**
- Rect Transform → Height: **250**
- Scroll Rect:
  - Horizontal: **OFF**
  - Vertical: **ON**

ConversationDisplay의 Content 자식:
- Rename to: **"ConversationDisplayText"**
- **Add Component → Text Mesh Pro - Text**
- Text: **"=== Conversation History ===\n(대화가 여기에 표시됩니다)"**
- Font Size: **18**
- Color: **White**
- Layout Element:
  - Preferred Height: **300**

#### 6-3: NPC 응답 표시
ConversationSection 자식으로:
```
우클릭 → TextMeshPro - Text
이름: "NpcResponseText"
```

NpcResponseText 설정:
- Text: **"NPC Response: (waiting...)"**
- Font Size: **20**
- Color: **Light Pink (R: 1, G: 0.8, B: 0.8)**
- Rect Transform → Height: **50**

### 7단계: 정보 섹션 (Emotion + Context)
MainLayout 자식으로:
```
우클릭 → Create Empty
이름: "InfoSection"
```

InfoSection 설정:
- **Add Component → Horizontal Layout Group**
  - Child Force Expand Height: **ON**
  - Child Force Expand Width: **ON**
  - Spacing: **20**
- Rect Transform → Height: **120**

#### 7-1: Emotion Panel
InfoSection 자식으로:
```
우클릭 → UI → Panel - TextMeshPro
이름: "EmotionPanel"
```

EmotionPanel 설정:
- Image Color: **Dark Purple (R: 0.15, G: 0.15, B: 0.25)**
- **Add Component → Vertical Layout Group**
  - Padding: **10 (all)**
  - Spacing: **5**

EmotionPanel 자식 - 제목:
```
우클릭 → TextMeshPro - Text
```
- Text: **"😊 EMOTION"**
- Font Size: **20**
- Color: **Gold**

EmotionPanel 자식 - 정보:
```
우클릭 → TextMeshPro - Text
이름: "EmotionText"
```
- Text: **"(analyzing...)"**
- Font Size: **16**
- Color: **White**

#### 7-2: Context Panel
InfoSection 자식으로:
```
우클릭 → UI → Panel - TextMeshPro
이름: "ContextPanel"
```

ContextPanel 설정:
- Image Color: **Dark Purple (R: 0.15, G: 0.15, B: 0.25)**
- **Add Component → Vertical Layout Group**
  - Padding: **10 (all)**
  - Spacing: **5**

ContextPanel 자식 - 제목:
```
우클릭 → TextMeshPro - Text
```
- Text: **"📊 CONTEXT"**
- Font Size: **20**
- Color: **Gold**

ContextPanel 자식 - 정보:
```
우클릭 → TextMeshPro - Text
이름: "ContextText"
```
- Text: **"(waiting...)"**
- Font Size: **16**
- Color: **White**

### 8단계: Manager 생성
Scene에 (Canvas 밖에):
```
우클릭 → Create Empty
이름: "LoveConversationManager"
```

LoveConversationManager 설정:
- **Add Component → Love Conversation UI**
- **Add Component → Unity Mic Recorder**

모든 필드를 아래처럼 할당:

```
LoveConversationUI:
- startButton → StartButton
- stopButton → StopButton
- statusText → StatusText
- conversationDisplayText → ConversationDisplayText
- npcResponseText → NpcResponseText
- emotionText → EmotionText
- contextText → ContextText
- sessionIdInput → SessionIdInput

UnityMicRecorder:
- uploadUrl: http://127.0.0.1:5000/analyze
```

### 9단계: EventSystem 생성
만약 자동 생성되지 않았다면:
```
우클릭 → UI → Event System
```

## 테스트

### 1. Server 시작
```bash
cd Assets/.. # Python 폴더로 이동
python server.py
```

Server running: http://127.0.0.1:5000

### 2. Play 모드 실행

Unity에서 **Play** 클릭

### 3. 테스트

- **[▶ START]** 클릭
- 마이크에 말하기: **"정말 사랑해요"**
- **[⏹ STOP]** 클릭
- 결과 확인:
  - Status: "Analyzed: love (0.95)" 
  - Emotion: "love, Valence: 0.85, Arousal: 0.72"
  - NPC Response: "그런 마음이... 정말 좋은데? (Rules)"
  - Conversation History에 턴 추가

### 4. 반복 테스트 (3턴)

위 과정을 3번 반복:

**Turn 1**: "정말 사랑해요" (love, 0.95)
- Status: Rules
- NPC: (Rules) "그런 마음이... 정말 좋은데?"

**Turn 2**: "너를 정말 좋아해" (love, 0.92)
- Status: Rules
- NPC: (Rules) "넌 정말 특별한데..."

**Turn 3**: "자꾸만 생각나" (love, 0.88)
- Context: repeated_emotions: ["love"], should_vary_response: **true** ← 반복 감지!
- Status: LLM (자동 강제)
- NPC: (LLM FORCED) "나도 자꾸 너를 생각해..."

## 완성 체크리스트

- [ ] Canvas 생성
- [ ] Background Panel
- [ ] Header 텍스트
- [ ] StatusText
- [ ] Start/Stop 버튼
- [ ] SessionIdInput
- [ ] ConversationDisplay (Scroll View)
- [ ] ConversationDisplayText
- [ ] NpcResponseText
- [ ] EmotionPanel + EmotionText
- [ ] ContextPanel + ContextText
- [ ] LoveConversationManager GameObject
- [ ] LoveConversationUI 컴포넌트 추가
- [ ] UnityMicRecorder 컴포넌트 추가
- [ ] 모든 필드 할당 완료
- [ ] EventSystem 존재 확인

완료 후 **Play** 버튼으로 테스트!

