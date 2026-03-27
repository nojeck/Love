# Phase 7.1: NPC 주도 대화 시스템 설계서

**작성일**: 2026-03-27  
**상태**: 설계 중  
**버전**: 1.0

---

## 1. 개요

### 1.1 목표
- NPC가 먼저 말을 걸어 게임 몰입감 향상
- 미연시 스타일 다이얼로그 UI로 상황 표현
- 에피소드별 상황 설정을 통한 스토리텔링 강화

### 1.2 핵심 기능
1. **상황 설정 출력**: 에피소드 시작 시 상황 텍스트 표시
2. **NPC 주도 대화**: NPC가 먼저 말을 걸며 대화 시작
3. **다이얼로그 UI**: 미연시 스타일 텍스트 출력 창
4. **자동 진행**: 상황 → NPC 대화 → 녹음 모드 자동 전환

---

## 2. 데이터 구조 설계

### 2.1 에피소드 상황 데이터

```json
{
  "episode_id": 1,
  "episode_title": "달라진 점 찾기의 미궁",
  "situations": [
    {
      "situation_id": "sit_001",
      "trigger": "episode_start",
      "context": {
        "location": "카페",
        "time": "오후 3시",
        "weather": "맑음",
        "npc_state": "고민 중"
      },
      "situation_text": "주말 오후, 카페 창가 자리.\n여자친구가 창밖을 바라보며 무언가 고민하는 듯하다.\n분위기가 평소와 다르다...",
      "npc_dialogue": {
        "text": "나 오늘 뭐 달라진 거 없어?",
        "emotion": "curiosity",
        "tone": "약간 긴장된"
      },
      "expected_keywords": ["귀걸이", "머리", "옷", "화장"],
      "success_threshold": 0.70
    }
  ]
}
```

### 2.2 NPC 캐릭터 데이터

```json
{
  "npc_id": "girlfriend_01",
  "name": "수진",
  "personality": {
    "type": "romantic",
    "traits": ["감성적", "직관적", "배려심 깊음"],
    "speech_style": "부드러운 존댓말"
  },
  "base_mood": 0.5,
  "mood_range": [0.0, 1.0],
  "preferences": {
    "liked_topics": ["사랑", "미래", "추억"],
    "disliked_topics": ["외모 비교", "무시"]
  }
}
```

### 2.3 다이얼로그 데이터

```json
{
  "dialogue_id": "dlg_001",
  "speaker": "npc",
  "text": "나 오늘 뭐 달라진 거 없어?",
  "emotion": "curiosity",
  "voice_tone": "nervous",
  "duration_estimate": 3.0,
  "effects": {
    "fade_in": true,
    "typewriter": true,
    "typing_speed": 0.05
  }
}
```

---

## 3. 서버 API 설계

### 3.1 새 엔드포인트

| 엔드포인트 | Method | 설명 |
|-----------|--------|------|
| `/episode/start` | POST | 에피소드 시작 (상황 로드) |
| `/episode/situation` | GET | 현재 상황 조회 |
| `/npc/dialogue` | GET | NPC 대화 생성 |
| `/npc/greeting` | POST | NPC 첫 인사 생성 |

### 3.2 API 상세

#### POST /episode/start
```json
// Request
{
  "episode_id": 1,
  "player_id": "player_001"
}

// Response
{
  "status": "ok",
  "episode": {
    "id": 1,
    "title": "달라진 점 찾기의 미궁"
  },
  "situation": {
    "id": "sit_001",
    "text": "주말 오후, 카페 창가 자리...",
    "location": "카페",
    "npc_state": "고민 중"
  },
  "npc": {
    "name": "수진",
    "mood": 0.5
  },
  "next_action": "npc_dialogue"
}
```

#### GET /npc/dialogue
```json
// Request
GET /npc/dialogue?episode_id=1&situation_id=sit_001

// Response
{
  "status": "ok",
  "dialogue": {
    "id": "dlg_001",
    "speaker": "npc",
    "text": "나 오늘 뭐 달라진 거 없어?",
    "emotion": "curiosity",
    "tone": "약간 긴장된"
  },
  "ui": {
    "show_dialogue_box": true,
    "auto_recording": true,
    "time_limit": 10
  }
}
```

---

## 4. Unity 클라이언트 설계

### 4.1 UI 컴포넌트 구조

```
Canvas
├── SituationPanel (상황 설명 패널)
│   ├── BackgroundImage (배경)
│   ├── LocationText ("카페")
│   ├── TimeText ("오후 3시")
│   └── SituationText (상황 설명 - 타이핑 효과)
│
├── DialoguePanel (다이얼로그 패널)
│   ├── CharacterPortrait (NPC 초상화)
│   ├── SpeakerName ("수진")
│   ├── DialogueText (대화 텍스트 - 타이핑 효과)
│   └── EmotionIndicator (감정 아이콘)
│
├── RecordingIndicator (녹음 상태)
│   ├── MicIcon
│   ├── TimerText
│   └── WaveformVisualizer
│
└── MoodIndicator (호감도)
    ├── MoodBar
    └── MoodValue
```

### 4.2 C# 클래스 설계

```csharp
// SituationData.cs
[System.Serializable]
public class SituationData {
    public string situation_id;
    public string text;
    public string location;
    public string npc_state;
    public NPCDialogueData npc_dialogue;
}

// NPCDialogueData.cs
[System.Serializable]
public class NPCDialogueData {
    public string text;
    public string emotion;
    public string tone;
    public float duration_estimate;
}

// DialogueUIController.cs
public class DialogueUIController : MonoBehaviour {
    [Header("UI References")]
    public GameObject situationPanel;
    public GameObject dialoguePanel;
    public Text dialogueText;
    public Image characterPortrait;
    
    [Header("Settings")]
    public float typingSpeed = 0.05f;
    
    public void ShowSituation(SituationData situation) {
        // 상황 텍스트 타이핑 효과로 출력
    }
    
    public void ShowDialogue(NPCDialogueData dialogue) {
        // NPC 대화 텍스트 출력
    }
    
    public void OnDialogueComplete() {
        // 대화 완료 시 자동 녹음 시작
    }
}
```

### 4.3 게임플레이 흐름

```
1. 에피소드 시작
   ↓
2. 상황 패널 활성화
   - 배경 이미지 로드
   - 상황 텍스트 타이핑 효과
   - 2초 대기
   ↓
3. 다이얼로그 패널 활성화
   - NPC 초상화 표시
   - NPC 대화 텍스트 타이핑
   - 감정 아이콘 표시
   ↓
4. 녹음 모드 자동 시작
   - 녹음 인디케이터 표시
   - 10초 타이머 시작
   ↓
5. 유저 응답 대기
   - 음성 감지 시 녹음 시작
   - 최대 30초 녹음
   ↓
6. 분석 및 피드백
```

---

## 5. 서버 구현 설계

### 5.1 EpisodeManager 클래스

```python
# episode_manager.py

class EpisodeManager:
    """에피소드 및 상황 관리"""
    
    def __init__(self, db_path='episodes.db'):
        self.db_path = db_path
        self.current_episodes = {}  # player_id -> episode_state
    
    def start_episode(self, player_id: str, episode_id: int) -> dict:
        """에피소드 시작"""
        # 상황 데이터 로드
        situation = self._load_situation(episode_id)
        
        # 플레이어 상태 초기화
        self.current_episodes[player_id] = {
            'episode_id': episode_id,
            'situation_id': situation['situation_id'],
            'mood': 0.5,
            'turn_count': 0
        }
        
        return {
            'episode': self._get_episode_info(episode_id),
            'situation': situation,
            'npc': self._get_npc_info(),
            'next_action': 'npc_dialogue'
        }
    
    def get_npc_dialogue(self, episode_id: int, situation_id: str) -> dict:
        """NPC 대화 생성"""
        situation = self._load_situation(episode_id, situation_id)
        
        # LLM으로 NPC 대화 생성 (또는 미리 정의된 대화)
        dialogue = self._generate_dialogue(situation)
        
        return {
            'dialogue': dialogue,
            'ui': {
                'show_dialogue_box': True,
                'auto_recording': True,
                'time_limit': 10
            }
        }
```

### 5.2 server.py 추가 엔드포인트

```python
@app.route('/episode/start', methods=['POST'])
def episode_start():
    """에피소드 시작"""
    data = request.get_json()
    episode_id = data.get('episode_id', 1)
    player_id = data.get('player_id', 'default')
    
    episode_manager = EpisodeManager()
    result = episode_manager.start_episode(player_id, episode_id)
    
    return jsonify({
        'status': 'ok',
        **result
    })


@app.route('/npc/dialogue', methods=['GET'])
def npc_dialogue():
    """NPC 대화 조회"""
    episode_id = request.args.get('episode_id', 1)
    situation_id = request.args.get('situation_id', 'sit_001')
    
    episode_manager = EpisodeManager()
    result = episode_manager.get_npc_dialogue(episode_id, situation_id)
    
    return jsonify({
        'status': 'ok',
        **result
    })
```

---

## 6. 구현 우선순위

### Phase 1: 기본 구조 (1-2일)
- [ ] 에피소드/상황 JSON 데이터 파일 생성
- [ ] EpisodeManager 클래스 구현
- [ ] `/episode/start`, `/npc/dialogue` API 구현

### Phase 2: Unity UI (2-3일)
- [ ] SituationPanel UI 구현
- [ ] DialoguePanel UI 구현
- [ ] 타이핑 효과 구현

### Phase 3: 통합 (1-2일)
- [ ] 서버-클라이언트 연동
- [ ] 자동 녹음 모드 연동
- [ ] 테스트 및 검증

---

## 7. 테스트 시나리오

### 7.1 기본 흐름 테스트
```
1. POST /episode/start → 상황 데이터 반환 확인
2. GET /npc/dialogue → NPC 대화 반환 확인
3. Unity: 상황 패널 표시 → 다이얼로그 패널 표시 → 녹음 모드 진입
```

### 7.2 데이터 검증
- 상황 텍스트가 올바르게 표시되는가
- NPC 대화가 emotion과 함께 반환되는가
- UI 타이핑 효과가 정상 작동하는가

---

## 8. 참조

- 기존 설계서: `design.md`
- NPC 응답 생성기: `npc_response_generator_v2.py`
- LLM Provider: `llm_provider.py`

---

**작성자**: Cascade AI  
**검토자**: (대기중)
