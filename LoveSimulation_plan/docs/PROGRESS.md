# 진행 상황 및 다음 계획 (Episode 1 프로토타입)

작성일: 2026-03-27 (업데이트)

## 현재 진행상황 (Phase 1-6 완료)

### Phase 1: Audio Calibration System ✅
- 음성 지표 추출 안정화: Praat + Librosa fallback
- 장치별 캘리브레이션 DB 구축 (SQLite)
- Z-score 정규화를 통한 보정 시스템
- 테스트 WAV 3종 생성 (clean/realistic/challenging)
- `calibration_db.py`, `validate_audio_metrics.py` 완성

### Phase 2: Text Scoring (규칙 기반) ✅
- 응답 유형 분류: 정답/오답/딴소리/침묵
- 감정 인식: 10+ 감정 카테고리 + 강도 수정자
- 감정 어휘사전 확장: 100+ 단어 (emotion_lexicon.json)
- `text_scorer.py` 완성, 83-100% 정확도

### Phase 3.5: Hybrid A+C System ✅ **← 새로 추가**
- **LLM Provider Interface**: Claude, OpenAI, Ollama 지원
- **Hybrid Text Scorer**: 신뢰도 기반 LLM 폴백
- **응답 다양화**: 반복 감지 및 피드백 변형
- **Server Integration**: `/analyze`, `/feedback`, `/hybrid-status` 엔드포인트
- `llm_provider.py`, `hybrid_text_scorer.py` 완성

## 주요 결과

| Phase | 결과 | 파일 |
|-------|------|------|
| Phase 1 | 캘리브레이션 시스템 | `calibration_db.py` |
| Phase 2 | 규칙 기반 텍스트 점수 | `text_scorer.py`, `emotion_lexicon.json` |
| Phase 3.5 | 하이브리드 A+C 스코러 | `llm_provider.py`, `hybrid_text_scorer.py` |

## 기술 스택

```
클라이언트 (Unity)
    ↓ WAV (음성)
서버 (Flask)
    ├─ 오디오 분석 (Praat/Librosa)
    ├─ 규칙 기반 텍스트 (TextScorer)
    ├─ LLM 폴백 (Claude/OpenAI/Ollama)
    └─ 피드백 생성 (반복 회피)
    ↓ JSON (점수 + 피드백)
클라이언트 (NPC 반응)
```

## 아키텍처 개선 사항

### Before (Phase 3)
```
음성 입력 → 규칙 점수 → 최종 점수
            (불명확할 수도 있음)
```

### After (Phase 3.5)
```
음성 입력 → 규칙 점수 (빠름)
         ├─ 신뢰도 > 0.6? → 최종 점수 ✓
         └─ 신뢰도 ≤ 0.6? → LLM 재평가 → 최종 점수
         
피드백:
규칙 기반 → 이전 피드백과 비교 → 반복? → LLM 생성
```

## Phase 4: 대화 히스토리 & 맵락 추적 ✅ **← 새로 추가**

**상태**: ✅ 완료
**파일**: `conversation_history.py`, `PHASE_4_REPORT.md`

구현:
- SQLite 기반 대화 저장
- 반복 감지 (3회 이상 같은 감정)
- 감정 카운팅 (love, joy, anger 등)
- 평균 점수 추적
- LLM 맥락 제공

예시:
```python
history = ConversationHistory()
history.create_session('player_001')
history.add_exchange('player_001', '사랑해', 'love', 0.95, 'NPC: 나도...')
context = history.get_context('player_001')
# → repeated_emotions: ['love'] (3회 이상)
```

## Phase 5: Server 통합 & API 완성 ✅

**상태**: ✅ 완료 (2026-03-23)
**파일**: `server.py`, `config_manager.py`, `npc_response_generator.py`

구현:
- `/analyze` 엔드포인트: 음성 분석 + 텍스트 점수 + 대화 맥락
- `/feedback` 엔드포인트: NPC 피드백 생성 + 반복 감지
- `/conversation-status` 엔드포인트: 현재 대화 상태 조회
- `/hybrid-status` 엔드포인트: 시스템 상태 확인

## Phase 6: LLM API 최적화 ✅ **← 최신**

**상태**: ✅ 완료 (2026-03-27)
**파일**: `llm_provider.py`, `npc_response_generator_v2.py`, `llm_text_scorer.py`

### Phase 6.1: OpenRouter 통합
- OpenRouterProvider 클래스 구현 (500+ 모델 접근)
- DeepSeek Chat/R1 기본 모델 설정
- CostTracker 클래스로 실시간 비용 모니터링
- `/llm-costs`, `/llm-costs/reset` API 추가

### Phase 6.2: SmartRouter 구현
- SmartRouter 클래스 구현 (복잡도 기반 모델 자동 선택)
- 복잡도 분석: 텍스트 길이, 감정 키워드, 점수, 컨텍스트
- 3단계 티어: cheap → standard → premium
- NPC 응답 생성 및 텍스트 점수 계산에 스마트 라우팅 적용
- `/llm-routing-stats` API 추가

### Phase 6.3: 문맥 캐싱 & 배치 처리
- OpenRouterProvider에 시스템 프롬프트 캐싱 추가 (500자 이상 자동 캐싱)
- 캐시 적중 시 토큰 절약 (27%+)
- BatchProcessor 클래스 구현 (최대 10개 요청 병렬 처리)
- `/llm-cache-stats`, `/llm-cache-clear`, `/llm-batch` API 추가

**비용 절감 효과:**
```
기존 (Gemini Flash): ~$50-100/월
현재 (DeepSeek + 최적화): ~$3-5/월
총 절감: 90%+
```

## 다음 단계 (우선 순위)

### Phase 7: 게임플레이 몰입 시스템 (계획 중)

#### 7.1 NPC 주도 대화 시스템
- **목표**: NPC가 먼저 말을 걸어 몰입감 향상
- **구현 내용**:
  - 에피소드별 상황 설정 텍스트 출력
  - NPC(여자친구)가 먼저 대화 시작
  - 미연시 스타일 다이얼로그 창 UI
  - 상황 예시: "카페에서 데이트 중, 여자친구가 무언가 고민하는 듯하다..."
- **설계 문서**: `PHASE_7_1_NPC_DIALOGUE_DESIGN.md` ✅
- **상태**: 🔄 설계 완료, 구현 대기

#### 7.2 호감도 UI 시스템
- **목표**: 실시간 호감도 변화 피드백
- **구현 내용**:
  - 현재 호감도 수치 UI 표시 (0-100)
  - 유저 발화 시 호감도 변화 애니메이션
  - 한마디마다 얼마나 유동하는지 시각화
- **설계 문서**: `PHASE_7_2_AFFECTION_UI_DESIGN.md` ✅
- **구현 파일**: `Assets/Scripts/AffectionUIController.cs` ✅
- **상태**: 🔄 구현 완료, 테스트 대기

#### 7.3 타임 리미트 & 자동 녹음 시스템
- **목표**: 실제 상황 같은 Live 느낌 제공
- **구현 내용**:
  - NPC 말 시작 시 자동 녹음 모드 ON
  - 첫 마디 제한: 10초 (초과 시 silence 반환)
  - 녹음 최대 시간: 30초 (초과 시 "너무 많이 말함" 반응)
  - 전체 절차 자동 진행:
    ```
    에피소드 시작 → 상황 설명 텍스트 → NPC 대화 시작 
    → 데이트 모드 ON → 자동 녹음 시작 → 유저 응답 → 평가
    ```
- **상태**: ⏳ 계획 중

#### 7.4 클리어/실패 & 회귀 시스템
- **목표**: 명확한 목표와 피드백 제공
- **구현 내용**:
  - 호감도 100점 이상: Clear 팝업
  - 호감도 0점 이하: 실패 팝업
  - 실패 시 대화 분석 피드백 출력
  - '회귀하기' 버튼 → 에피소드 처음으로 이동
- **상태**: ⏳ 계획 중

#### 7.5 NPC TTS 시스템
- **목표**: NPC 음성 출력으로 몰입감 강화
- **구현 내용**:
  - NPC 대사 TTS 변환
  - 감정별 톤/속도 조절
  - 한국어 음성 합성 API 연동
- **상태**: ⏳ 계획 중

---

### 기존 계획 (Phase 7 이후)

1. **Unity 통합 테스트** (이번주)
   - 캘리브레이션 UI 검증
   - STT → LLM → NPC 응답 플로우 테스트
   - 비용 모니터링 검증

2. **플레이테스트** (이번달)
   - 신뢰도 임계값 (현재 0.6) 튜닝
   - 감정 반복 임계값 (현재 3회) 조정
   - NPC 반응 자연스러움 평가

3. **Chaos Meter 연동** (다음달)
   - 점수 기반 감정 상태 변화
   - 히든 루트 트리거 구현

4. **사용자 개성 학습** (향후)
   - 같은 사용자 응답 패턴 학습
   - 반복되는 사용자에게 맞춘 응답

## 파일 위치

### Core Engine
- 설계서: `prototype/episode1/design.md`
- 흐름: `prototype/episode1/flow.json`
- 노드 테이블: `prototype/episode1/node_table.csv`

### Audio System
- 오디오 분석: `prototype/episode1/scorer.py`
- 캘리브레이션: `prototype/episode1/calibration_db.py`
- 지표 검증: `prototype/episode1/validate_audio_metrics.py`

### Text System
- 규칙 기반: `prototype/episode1/text_scorer.py`
- LLM 제공자: `prototype/episode1/llm_provider.py`
- 하이브리드: `prototype/episode1/hybrid_text_scorer.py`
- **SmartRouter**: `prototype/episode1/llm_provider.py` ← **Phase 6**
- **BatchProcessor**: `prototype/episode1/llm_provider.py` ← **Phase 6**
- 감정 어휘사전: `prototype/episode1/emotion_lexicon.json`

### Server
- Flask 서버: `prototype/episode1/server.py`
- 테스트: `prototype/episode1/test_*.py`

### Documentation
- Phase 1-2 보고서: `prototype/episode1/PHASE_1_2_REPORT.md`
- Phase 3 보고서: `prototype/episode1/PHASE_3_REPORT.md`
- **Phase 3.5 보고서**: `prototype/episode1/PHASE_3_5_REPORT.md` ← **새로**
- 이 파일: `PROGRESS.md`

## 환경 설정

### 규칙만 사용 (기본)
```bash
python server.py
# 의존성 없음, 빠름
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
# 비용: ~$0.0003/1K 토큰 (90% 절감)
```

### SmartRouter (복잡도 기반 자동 선택)
```bash
# llm_config.json 설정
{
  "llm_provider": "smart",
  "openrouter_api_key": "sk-or-v1-..."
}

python server.py
# 자동 라우팅으로 비용 최적화
```

### Claude LLM 활성화 (기존)
```bash
export ANTHROPIC_API_KEY=sk-ant-...
export USE_LLM=true
export LLM_PROVIDER=claude
python server.py
```

### OpenAI GPT 활성화 (기존)
```bash
export OPENAI_API_KEY=sk-...
export USE_LLM=true
export LLM_PROVIDER=openai
python server.py
```

### Ollama (로컬) 활성화
```bash
ollama pull mistral
ollama serve &
export USE_LLM=true
export LLM_PROVIDER=ollama
python server.py
```

## 개발/운영 주의사항

- 현재 `server.py`는 선택적으로 LLM을 사용합니다 (비용 관리)
- 신뢰도 낮은 응답 (~20%)만 LLM 호출 → 월 $60 정도
- 규칙만으로도 대부분의 명확한 응답 처리 가능
- LLM은 모호한 경우와 피드백 다양화에만 사용

## 비용 예상 (월단위)

| LLM | 응답 수 | 비용 | 비고 |
|-----|--------|------|------|
| Rules Only | 100K | $0 | 기본 |
| **OpenRouter + DeepSeek** | 100K | **$3-5** | ✅ 추천 (90% 절감) |
| **SmartRouter** | 100K | **$2-4** | ✅ 최적 (자동 라우팅) |
| Claude | 100K | $60 | 신뢰도 <0.6만 |
| OpenAI | 100K | $600 | 비쌈 |
| Ollama | 100K | $0 | 느림 |

---

저장 위치: `PROGRESS.md` (업데이트됨)
최종 업데이트: 2026-03-27
담당자: Copilot
