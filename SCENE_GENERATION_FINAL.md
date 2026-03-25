# 씬 생성 가이드 (최종)

## 완성된 기능

✅ **1920x1080 해상도 최적화**
- Canvas CanvasScaler reference resolution: 1920x1080
- MainLayout: 전체 캔버스 크기에 맞춤 (padding 40px)
- 모든 섹션 높이 명시적 정의

✅ **레이아웃 구조**
- Header: 80px (제목)
- Control Section: 180px (녹음 제어)
- Conversation Section: 500px (대화 표시)
- Info Section: 150px (감정/컨텍스트)
- 총합: 910px + padding 80px = 990px (1080px 영역에 적절함)

✅ **폰트 설정**
- 모든 TextMeshProUGUI 요소: YPairingFont-Regular SDF 적용
- 자동 로드: AssetDatabase.LoadAssetAtPath

✅ **UI 요소**
- StartButton / StopButton (녹색/빨간색)
- StatusText (상태 표시)
- ConversationDisplayText (대화 이력 표시)
- NpcResponseText (NPC 응답)
- SessionIdInput (세션 ID)
- EmotionText / ContextText (정보 패널)

## 씬 생성 방법

### 방법 1: Unity Editor (권장)

1. Unity Editor가 열려있는 상태에서
2. 상단 메뉴: **Tools > Create > Love Conversation Scene**
3. 팝업이 나타나면 **OK** 클릭
4. 씬이 자동 생성됨: `Assets/Scenes/LoveConversationScene.unity`

### 검증 항목

생성 후 씬 구조를 확인하세요:

```
Canvas (1920x1080)
├── Background (어두운 배경)
└── MainLayout (메인 레이아웃)
    ├── HeaderText ("❤️ LOVE CONVERSATION TEST")
    ├── ControlSection (녹음 제어)
    │   ├── SectionTitle ("🎤 RECORDING CONTROL")
    │   ├── StatusText
    │   ├── ButtonContainer
    │   │   ├── StartButton (▶)
    │   │   └── StopButton (⏹)
    │   └── SessionIdInput (라벨+입력필드)
    ├── ConversationSection (대화 표시)
    │   ├── SectionTitle ("💬 CONVERSATION HISTORY")
    │   ├── ConversationDisplay (스크롤뷰)
    │   └── NpcResponseText
    └── InfoSection (정보 패널)
        ├── EmotionPanel (감정)
        └── ContextPanel (컨텍스트)

LoveConversationManager (루트 GameObject)
├── UnityMicRecorder (오디오 녹음)
└── LoveConversationUI (UI 제어 스크립트, 자동 연결됨)
```

## 문제 해결

### 폰트 로드 실패
- **증상**: "Cannot find YPairingFont-Regular SDF.asset" 에러
- **해결**: 
  1. Assets/TextMesh Pro/Fonts/ 폴더 확인
  2. `YPairingFont-Regular SDF.asset` 파일 존재 확인
  3. 파일 이름 대소문자 정확히 맞추기

### UI 요소가 왼쪽에 쏠림
- **증상**: UI가 화면 왼쪽에만 표시됨
- **해결**: 이미 모든 LayoutElement에서 flexibleWidth = 1 설정됨
  - 메인 LayoutGroup childForceExpandWidth = true
  - 생성 후 유지되므로 레이아웃 자동 맞춤

### 텍스트 크기가 이상함
- **증상**: 폰트가 너무 크거나 작음
- **원인**: Canvas CanvasScaler 설정
- **확인**: Canvas > CanvasScaler > Reference Resolution = 1920, 1080

## 다음 단계

씬 생성 후:
1. Play 버튼으로 테스트
2. StartButton 클릭 시 녹음 시작 확인
3. Server 연동 테스트 (/analyze, /feedback, /conversation-status)
