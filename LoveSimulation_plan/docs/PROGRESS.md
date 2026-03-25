# 진행 상황 및 다음 계획 (Episode 1 프로토타입)

작성일: 2026-03-20 (업데이트)

## 현재 진행상황 (Phase 1-3.5 완료)

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

## 다음 단계 (우선 순위)

1. **Server 통합** (이번주)
   - `/analyze`에 conversation context 추가
   - `/feedback`에서 반복 감지 시 LLM 강제

2. **NPC 반응 조정** (이번달)
   - 감정 호에 따른 NPC 기분 변화
   - 반복 감지 시 피드백 다양화

3. **플레이테스트** (다음달)
   - 신뢰도 임계값 (현재 0.6) 튜닝
   - 감정 반복 임계값 (현재 3회) 조정
   - NPC 반응 자연스러움 평가

4. **LLM API 통합** (선택사항)
   - Claude/OpenAI API 설정
   - 불명확한 응답 자동 개선

5. **사용자 개성 학습** (향후)
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
- **LLM 제공자**: `prototype/episode1/llm_provider.py` ← **새로**
- **하이브리드**: `prototype/episode1/hybrid_text_scorer.py` ← **새로**
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

### Claude LLM 활성화
```bash
export ANTHROPIC_API_KEY=sk-ant-...
export USE_LLM=true
export LLM_PROVIDER=claude
python server.py
```

### OpenAI GPT 활성화
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
| Rules Only | 100K | $0 | 추천 |
| Claude | 100K | $60 | 신뢰도 <0.6만 |
| OpenAI | 100K | $600 | 비쌈 |
| Ollama | 100K | $0 | 느림 |

---

저장 위치: `PROGRESS.md` (업데이트됨)
최종 업데이트: 2026-03-20
담당자: Copilot
