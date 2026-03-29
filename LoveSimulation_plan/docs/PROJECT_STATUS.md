# 무한 회귀 연애 (LoveSimulation) - 프로젝트 현황 및 계획

**최종 업데이트:** 2026-03-29  
**프로젝트 상태:** Phase 1-7 완료 (Phase 7.6 계획 중)

### 2026-03-29 주요 변경 사항
- `LoveConversationUI` 전면 리팩터링: SituationPanel 제거, 메인 화면 3텍스트(NPC/플레이어/가이드) 고정.
- 클릭/Space 입력으로만 다음 단계 진행 (녹음은 자동 유지).
- `statusText`에 현재 점수와 최근 5개의 무드 변화 로그 표시.
- `ENABLE_INPUT_SYSTEM` 환경에서 Unity Input System API를 사용하도록 입력 처리 분기.
- Episode 시작 HTTP 요청에 재시도(기본 3회) 및 대기 메시지 도입.

---

## 📊 프로젝트 개요

**무한 회귀 연애**는 AI 음성 분석과 실제 설문 데이터를 기반으로 한 진지한 병맛 연애 시뮬레이션 게임입니다.

### 핵심 특징
- **음성 감정 분석 (V.E.A.)**: Jitter, Shimmer, Pitch, HNR 등 음성 지표로 진정성 판정
- **설문 데이터 기반**: 실제 여성들의 선호도 데이터를 가중치로 사용
- **혼돈 지수 (Chaos Meter)**: 비상식적 응답 시 병맛 연출 트리거
- **기억 시스템**: AI 여친이 이전 루프의 실수를 기억하고 언급
- **사망 회귀**: 실패 시 황당한 연출과 함께 루프 재시작

---

## 🎯 완료된 Phase 요약

### Phase 1: 오디오 캘리브레이션 시스템 ✅
**상태:** 완료 (2026-03-23)

**구현 내용:**
- Praat + Librosa 기반 음성 지표 추출
- SQLite 캘리브레이션 DB (device_baseline 저장)
- Z-score 정규화를 통한 점수 보정
- 안정성 검증 스크립트 (`validate_audio_metrics.py`)

**주요 파일:**
- `calibration_db.py` - 캘리브레이션 DB 클래스
- `validate_audio_metrics.py` - 메트릭 안정성 검증
- `test_calibration.py` - 통합 테스트

---

### Phase 2: 텍스트 점수화 (규칙 기반) ✅
**상태:** 완료 (2026-03-23)

**구현 내용:**
- 응답 유형 분류: 정답/오답/딴소리/침묵
- 감정 탐지: 100+ 감정 어휘 + 강도 수정자
- 신뢰도 계산 (0.0~1.0)
- 응답 타입(60%) + 감정(40%) 조합 점수

**주요 파일:**
- `text_scorer.py` - TextScorer 클래스
- `emotion_lexicon.json` - 감정 어휘 사전
- `test_text_scoring.py` - 통합 테스트

**정확도:**
- 정답 분류: 83%
- 침묵 감지: 100%
- love 감정: 100%

---

### Phase 3.5: 하이브리드 A+C 시스템 ✅
**상태:** 완료 (2026-03-23)

**구현 내용:**
- LLM Provider Interface (Claude, OpenAI, Ollama)
- 신뢰도 기반 LLM 폴백 (threshold: 0.6)
- 반복 감지 및 피드백 다양화
- Server 통합 (`/analyze`, `/feedback`, `/hybrid-status`)

**주요 파일:**
- `llm_provider.py` - LLM 제공자 인터페이스
- `hybrid_text_scorer.py` - 하이브리드 스코러
- `test_hybrid_scoring.py` - 통합 테스트

**특징:**
- 규칙만으로도 완벽히 작동 (LLM 선택사항)
- 신뢰도 < 0.6인 경우만 LLM 호출 (~20%)
- 월 비용: ~$60 (Claude 기준, 100K 응답)

---

### Phase 4: 대화 히스토리 & 맥락 추적 ✅
**상태:** 완료 (2026-03-23)

**구현 내용:**
- SQLite 기반 대화 저장
- 반복 감지 (3회 이상 같은 감정)
- 감정 카운팅 및 평균 점수 추적
- LLM 맥락 제공

**주요 파일:**
- `conversation_history.py` - 대화 히스토리 관리
- `PHASE_4_REPORT.md` - 상세 문서

**기능:**
```python
history = ConversationHistory()
history.create_session('player_001')
history.add_exchange('player_001', '사랑해', 'love', 0.95, 'NPC: 나도...')
context = history.get_context('player_001')
# → repeated_emotions: ['love'] (3회 이상)
```

---

### Phase 5: Server 통합 & API 완성 ✅
**상태:** 완료 (2026-03-23)

**구현 내용:**
- `/analyze` 엔드포인트: 음성 분석 + 텍스트 점수 + 대화 맥락
- `/feedback` 엔드포인트: NPC 피드백 생성 + 반복 감지
- `/conversation-status` 엔드포인트: 현재 대화 상태 조회
- `/hybrid-status` 엔드포인트: 시스템 상태 확인

**주요 파일:**
- `server.py` - Flask 서버 (완전 통합)
- `PHASE_5_REPORT.md` - 상세 문서

**API 응답 예시:**
```json
{
  "authenticity": 0.85,
  "text_evaluation": {
    "text_score": 0.95,
    "emotion": "love",
    "confidence": 0.845,
    "llm_used": false
  },
  "conversation_context": {
    "total_turns": 5,
    "repeated_emotions": ["love"],
    "should_vary_response": true
  }
}
```

---

### Phase 6: LLM API 최적화 ✅
**상태:** 완료 (2026-03-27)

**구현 내용:**

#### Phase 1: OpenRouter 통합
- OpenRouterProvider 클래스 구현 (500+ 모델 접근)
- DeepSeek Chat/R1 기본 모델 설정
- CostTracker 클래스로 실시간 비용 모니터링
- `/llm-costs`, `/llm-costs/reset` API 추가

#### Phase 2: SmartRouter 구현
- SmartRouter 클래스 구현 (복잡도 기반 모델 자동 선택)
- 복잡도 분석: 텍스트 길이, 감정 키워드, 점수, 컨텍스트
- 3단계 티어: cheap (DeepSeek Chat) → standard (DeepSeek R1) → premium (Claude Sonnet)
- NPC 응답 생성 및 텍스트 점수 계산에 스마트 라우팅 적용
- `/llm-routing-stats` API 추가

#### Phase 3: 문맥 캐싱 & 배치 처리
- OpenRouterProvider에 시스템 프롬프트 캐싱 추가 (500자 이상 자동 캐싱)
- 캐시 적중 시 토큰 절약 (27%+)
- BatchProcessor 클래스 구현 (최대 10개 요청 병렬 처리)
- `/llm-cache-stats`, `/llm-cache-clear`, `/llm-batch` API 추가

**주요 파일:**
- `llm_provider.py` - OpenRouterProvider, SmartRouter, BatchProcessor, CostTracker
- `npc_response_generator_v2.py` - 스마트 라우팅 적용
- `llm_text_scorer.py` - 스마트 라우팅 적용
- `server.py` - 비용/라우팅/캐시/배치 API

**비용 절감 효과:**
```
기존 (Gemini Flash): ~$0.50-2.00 / 1M 토큰
현재 (DeepSeek + 최적화): ~$0.03-0.10 / 1M 토큰
총 절감: 90%+

세부 절감:
- DeepSeek 모델 사용: 80%
- 스마트 라우팅: 추가 30-50%
- 문맥 캐싱: 추가 27%
```

**새 API 엔드포인트:**
| 엔드포인트 | 설명 |
|-----------|------|
| `GET /llm-costs` | 비용 통계 (토큰, USD) |
| `POST /llm-costs/reset` | 세션 비용 초기화 |
| `GET /llm-routing-stats` | 라우팅 통계 (티어별 사용량) |
| `GET /llm-cache-stats` | 캐시 통계 (히트율, 저장 토큰) |
| `POST /llm-cache-clear` | 캐시 초기화 |
| `POST /llm-batch` | 배치 처리 (다중 요청 병렬) |

---

## 🔄 기술 아키텍처

```
┌─────────────────────────────────────────────────────────┐
│                    Unity 클라이언트                       │
│              (마이크 녹음 + WAV 업로드)                   │
└────────────────────┬────────────────────────────────────┘
                     │ WAV + 메타데이터
                     ↓
┌─────────────────────────────────────────────────────────┐
│                   Flask 서버 (server.py)                 │
├─────────────────────────────────────────────────────────┤
│ 1. 음성 분석 (Phase 1)                                   │
│    ├─ Praat/Librosa: Jitter, Shimmer, Pitch, HNR 추출   │
│    └─ 캘리브레이션 DB: 기준선 조회 및 보정              │
│                                                          │
│ 2. 텍스트 분석 (Phase 2-3.5)                             │
│    ├─ 규칙 기반: 응답 분류 + 감정 탐지 (7ms)            │
│    └─ LLM 폴백: 신뢰도 < 0.6일 때만 호출 (300ms)        │
│                                                          │
│ 3. 대화 추적 (Phase 4)                                   │
│    ├─ 대화 저장: SQLite conversation_history.db          │
│    ├─ 반복 감지: 3회 이상 같은 감정                      │
│    └─ 맥락 제공: 최근 5개 교환 + 감정 통계              │
│                                                          │
│ 4. 피드백 생성 (Phase 3.5-5)                             │
│    ├─ 규칙 기반: 점수별 템플릿 (기본)                    │
│    └─ LLM 기반: 반복 감지 시 자동 강제                   │
└────────────────────┬────────────────────────────────────┘
                     │ JSON (점수 + 피드백 + 맥락)
                     ↓
┌─────────────────────────────────────────────────────────┐
│                    Unity 클라이언트                       │
│         (NPC 반응 출력 + 게임 로직 업데이트)             │
└─────────────────────────────────────────────────────────┘
```

---

## 📈 성능 특성

| 작업 | 시간 | 비고 |
|------|------|------|
| 음성 지표 추출 | ~500ms (Praat) | Librosa fallback: ~100ms |
| 텍스트 점수화 | ~7ms | 규칙 기반 (LLM 제외) |
| LLM 호출 | ~300ms | Claude (신뢰도 < 0.6일 때만) |
| DB 저장 | ~10ms | 메트릭당 |
| 기준선 계산 | O(N) | N개 메트릭 |
| **총 응답 시간** | **~100ms** | 규칙만 사용 시 |

### Phase 7: 게임플레이 몰입 시스템 ✅
**상태:** 완료 (2026-03-29)

#### Phase 7.1: NPC 주도 대화 시스템 ✅
- 에피소드별 상황 설정 텍스트 출력
- NPC(여자친구)가 먼저 대화 시작
- 에피소드 JSON 데이터 구조 설계
- EpisodeManager 클래스 구현 (SQLite 기반)

**주요 파일:**
- `episode_manager.py` - 에피소드 관리
- `episode_1.json` - 에피소드 데이터
- `npc_suji.json` - NPC 데이터
- `PHASE_7_1_NPC_DIALOGUE_DESIGN.md` - 설계 문서

---

#### Phase 7.2: 호감도 UI 시스템 
**상태:** 완료 (2026-03-27)

**구현 내용:**
- 0-100 호감도 수치 UI 표시
- 유저 발화 시 호감도 변화 애니메이션
- 5단계 색상/이모지 변화 (사랑/호감/보통/냉소/위험)
- 서버 연동 (/episode/status, /episode/affection)

**주요 파일:**
- `Assets/Scripts/AffectionUIController.cs` - Unity 호감도 UI
- `PHASE_7_2_AFFECTION_UI_DESIGN.md` - 설계 문서

---

#### Phase 7.3: 타임 리미트 & 자동 녹음 시스템 ✅
**상태:** 완료 (2026-03-27)

**구현 내용:**
- 첫 마디 10초 타임 리미트
- 최대 30초 녹음 제한
- 실시간 음성 감지 (RMS 기반)
- 침묵 자동 종료 (2초 연속 침묵)
- 상태 머신 (Idle/WaitingVoice/Recording/Processing)

**주요 파일:**
- `Assets/Scripts/AutoRecordingController.cs` - 자동 녹음 컨트롤러
- `PHASE_7_3_AUTO_RECORDING_DESIGN.md` - 설계 문서

---

#### Phase 7.4: 클리어/실패 & 회귀 시스템 ✅
**상태:** 완료 (2026-03-27)

**구현 내용:**
- Clear 팝업: 호감도 100 달성 시 통계 표시
- Fail 팝업: 호감도 0 도달 시 대화 분석 피드백
- 회귀(Revert) 기능: 에피소드 처음으로 이동
- 대화 분석: 침묵/부정반응/부정감정 비율 계산

**주요 파일:**
- `Assets/Scripts/ResultPopupController.cs` - 결과 팝업 UI
- `episode_manager.py` - revert_episode, analyze_dialogue 메서드
- `server.py` - /episode/revert, /episode/analyze API
- `PHASE_7_4_RESULT_POPUP_DESIGN.md` - 설계 문서

**새 API 엔드포인트:**
| 엔드포인트 | 설명 |
|-----------|------|
| `POST /episode/revert` | 에피소드 회귀 (초기화) |
| `GET /episode/analyze` | 대화 분석 |

---

### 즉시 (이번주)
1. **Unity 통합 테스트**
   - AffectionUI, AutoRecording, ResultPopup 연동 테스트
   - 에피소드 전체 플로우 검증

2. **NPC TTS 시스템** (Phase 7.5)
   - 한국어 TTS API 연동
   - 감정별 톤/속도 조절

### 단기 (이번달)
1. **플레이테스트**
   - 신뢰도 임계값 (현재 0.6) 튜닝
   - 감정 반복 임계값 (현재 3회) 조정
   - NPC 반응 자연스러움 평가

2. **LLM API 통합** (선택사항)
   - Claude/OpenAI API 설정
   - 불명확한 응답 자동 개선

### 중기 (다음달)
1. **사용자 개성 학습**
   - 같은 사용자 응답 패턴 학습
   - 반복되는 사용자에게 맞춘 응답

2. **Chaos Meter 연동**
   - 점수 기반 감정 상태 변화
   - NPC 반응 매핑

---

## 📁 주요 파일 구조

```
LoveSimulation_plan/
├── PROGRESS.md                          # 진행 상황 (이전)
├── PHASE_1_2_REPORT.md                  # Phase 1-2 상세 보고서
├── PHASE_3_REPORT.md                    # Phase 3 상세 보고서
├── PROJECT_STATUS.md                    # 이 파일 (통합 현황)
│
└── prototype/episode1/
    ├── 설계 & 흐름
    │   ├── design.md                    # 에피소드 1 설계서
    │   ├── flow.json                    # 게임 흐름 (JSON)
    │   └── node_table.csv               # 노드 테이블
    │
    ├── Phase 1: 오디오 시스템
    │   ├── calibration_db.py            # 캘리브레이션 DB
    │   ├── validate_audio_metrics.py    # 메트릭 검증
    │   ├── test_calibration.py          # 통합 테스트
    │   └── PHASE_1_2_REPORT.md          # 상세 보고서
    │
    ├── Phase 2-3: 텍스트 시스템
    │   ├── text_scorer.py               # 규칙 기반 스코러
    │   ├── emotion_lexicon.json         # 감정 어휘 사전
    │   ├── test_text_scoring.py         # 통합 테스트
    │   └── PHASE_3_REPORT.md            # 상세 보고서
    │
    ├── Phase 3.5: 하이브리드 시스템
    │   ├── llm_provider.py              # LLM 제공자 인터페이스
    │   ├── hybrid_text_scorer.py        # 하이브리드 스코러
    │   ├── test_hybrid_scoring.py       # 통합 테스트
    │   └── PHASE_3_5_REPORT.md          # 상세 보고서
    │
    ├── Phase 4: 대화 추적
    │   ├── conversation_history.py      # 대화 히스토리 관리
    │   ├── test_server_integration.py   # 통합 테스트
    │   └── PHASE_4_REPORT.md            # 상세 보고서
    │
    ├── Phase 5: Server 통합
    │   ├── server.py                    # Flask 서버 (완전 통합)
    │   ├── config_manager.py            # 설정 관리
    │   ├── npc_response_generator.py    # NPC 응답 생성
    │   ├── quick_test.py                # 빠른 테스트
    │   └── PHASE_5_REPORT.md            # 상세 보고서
    │
    ├── 유틸리티
    │   ├── scorer.py                    # 점수 계산 (기본)
    │   ├── requirements.txt              # Python 의존성
    │   ├── start_server.ps1             # 서버 시작 스크립트
    │   └── README.md                    # 프로토타입 README
    │
    └── 테스트 & 검증
        ├── test_*.py                    # 각 Phase별 테스트
        ├── validate_*.py                # 검증 스크립트
        └── verify_system.py             # 시스템 검증
```

---

## 🔧 환경 설정

### 기본 (규칙만 사용)
```bash
cd LoveSimulation_plan/prototype/episode1
python server.py
# 의존성 없음, 빠름 (평균 응답 ~100ms)
```

### OpenRouter + DeepSeek (추천) ✅
```bash
# llm_config.json 설정
{
  "llm_provider": "openrouter",
  "openrouter_api_key": "sk-or-v1-...",
  "openrouter_model": "deepseek/deepseek-chat"
}

python server.py
# 비용: ~$0.0003/1K 토큰 (기존 대비 90% 절감)
# 스마트 라우팅으로 자동 모델 선택
```

### SmartRouter (복잡도 기반 자동 선택)
```bash
# llm_config.json 설정
{
  "llm_provider": "smart",
  "openrouter_api_key": "sk-or-v1-..."
}

python server.py
# 간단한 요청 → DeepSeek Chat (cheap)
# 복잡한 요청 → DeepSeek R1 (standard)
# 중요 이벤트 → Claude Sonnet (premium)
# 자동 라우팅으로 비용 최적화
```

### Claude LLM 활성화 (기존)
```bash
export ANTHROPIC_API_KEY=sk-ant-...
export USE_LLM=true
export LLM_PROVIDER=claude
python server.py
# 신뢰도 < 0.6인 경우만 호출 (~20%)
# 월 비용: ~$60 (100K 응답)
```

### OpenAI GPT 활성화 (기존)
```bash
export OPENAI_API_KEY=sk-...
export USE_LLM=true
export LLM_PROVIDER=openai
python server.py
# 월 비용: ~$600 (100K 응답)
```

### Ollama (로컬) 활성화
```bash
ollama pull mistral
ollama serve &
export USE_LLM=true
export LLM_PROVIDER=ollama
python server.py
# 무료, 느림 (~2초/응답)
```

---

## 📊 비용 분석 (월단위)

| 옵션 | 응답 수 | 비용 | 특징 |
|------|--------|------|------|
| Rules Only | 100K | $0 | ✅ 기본 |
| **OpenRouter + DeepSeek** | 100K | **$3-5** | ✅✅ 추천 (90% 절감) |
| **SmartRouter** | 100K | **$2-4** | ✅✅✅ 최적 (자동 라우팅) |
| Claude | 100K | $60 | 신뢰도 < 0.6만 호출 |
| OpenAI | 100K | $600 | 모든 응답 호출 |
| Ollama | 100K | $0 | 로컬, 느림 |

### 비용 절감 내역
```
Gemini Flash → DeepSeek Chat: 80% 절감
+ 스마트 라우팅: 추가 30-50% 절감
+ 문맥 캐싱: 추가 27% 절감
= 총 90%+ 절감
```

---

## ✅ 완료 기준 평가

| 항목 | 상태 | 설명 |
|------|------|------|
| 음성 지표 추출 | ✅ | Praat + Librosa, 안정성 검증 완료 |
| 캘리브레이션 시스템 | ✅ | SQLite DB, 기준선 저장/조회 완료 |
| 규칙 기반 텍스트 점수 | ✅ | 83-100% 정확도, 7ms 처리 |
| LLM 하이브리드 시스템 | ✅ | Claude/OpenAI/Ollama 지원 |
| 대화 히스토리 추적 | ✅ | SQLite, 반복 감지, 맥락 제공 |
| Server 통합 | ✅ | 5개 엔드포인트, 완전 통합 |
| **OpenRouter 통합** | ✅ | DeepSeek Chat/R1, 비용 모니터링 |
| **SmartRouter** | ✅ | 복잡도 기반 자동 모델 선택 |
| **문맥 캐싱** | ✅ | 시스템 프롬프트 캐싱, 27% 절감 |
| **배치 처리** | ✅ | 병렬 요청 처리, 60% 빠름 |
| **Phase 7.1 NPC 주도 대화** | ✅ | 완료 |
| **Phase 7.2 호감도 UI** | ✅ | 완료 |
| **Phase 7.3 타임 리미트** | ✅ | 완료 |
| **Phase 7.4 클리어/실패** | ✅ | 완료 |
| **Phase 7.5 NPC 주도 UI & 흐름** | ✅ | SituationPanel 제거, 고정 3텍스트 UI, 입력/로그 개선 |
| **Phase 7.6 NPC TTS** | ⏳ | 계획 중 |
| 플레이테스트 | ⏳ | 다음 단계 |
| 게임 연출 | ⏳ | Unity 통합 필요 |

---

## 🎓 주요 학습 포인트

1. **음성 신호 처리**: Jitter/Shimmer는 감정 상태의 신뢰할 수 있는 지표
2. **하이브리드 설계**: 규칙 + LLM 조합으로 비용과 정확도 균형
3. **한국어 NLP**: 감정 표현이 다양하므로 어휘 기반 접근 필수
4. **데이터 기반 게임**: 설문 데이터를 가중치로 사용하면 현실감 증대

---

## 📞 문의 & 지원

- **기술 문제**: `server.py` 로그 확인 (`server_err.log`, `server_out.log`)
- **테스트**: `python test_*.py` 실행
- **설정**: `llm_config.json` 수정 또는 환경 변수 설정

---

**프로젝트 상태:** 개발 진행 중 (Phase 1-7 완료, Phase 7.6 계획 중)  
**마지막 업데이트:** 2026-03-29  
**담당자:** Copilot
