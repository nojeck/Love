# 무한 회귀 연애 (LoveSimulation) - 프로젝트 현황 및 계획

**최종 업데이트:** 2026-03-25  
**프로젝트 상태:** Phase 1-5 완료 (개발 진행 중)

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

---

## 🚀 다음 단계 (우선 순위)

### 즉시 (이번주)
1. **Server 통합 테스트**
   - `/analyze` + `/feedback` 엔드투엔드 테스트
   - 반복 감지 시 LLM 강제 호출 검증

2. **NPC 반응 조정**
   - 감정 호에 따른 NPC 기분 변화
   - 반복 감지 시 피드백 다양화

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

### Claude LLM 활성화
```bash
export ANTHROPIC_API_KEY=sk-ant-...
export USE_LLM=true
export LLM_PROVIDER=claude
python server.py
# 신뢰도 < 0.6인 경우만 호출 (~20%)
# 월 비용: ~$60 (100K 응답)
```

### OpenAI GPT 활성화
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
| Rules Only | 100K | $0 | ✅ 추천 |
| Claude | 100K | $60 | 신뢰도 < 0.6만 호출 |
| OpenAI | 100K | $600 | 모든 응답 호출 |
| Ollama | 100K | $0 | 로컬, 느림 |

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

**프로젝트 상태:** 개발 진행 중 (Phase 1-5 완료, 플레이테스트 준비)  
**마지막 업데이트:** 2026-03-25  
**담당자:** Copilot
