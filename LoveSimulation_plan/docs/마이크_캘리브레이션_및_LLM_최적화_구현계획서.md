# 마이크 캘리브레이션 및 LLM 최적화 구현 계획서

**작성일**: 2026년 3월 26일  
**버전**: 1.0  
**상태**: 구현 완료

---

## 1. 개요

### 1.1 목적
- 사용자별 마이크 특성을 반영한 정확한 오디오 점수 산정
- LLM API 비용 최적화 및 다중 모델 라우팅 도입
- NPC Mood 회복 시스템으로 자연스러운 대화 흐름 개선

### 1.2 배경
기존 시스템은 모든 사용자에게 동일한 오디오 기준을 적용하여:
- 저품질 마이크 사용자가 불이익을 받음
- 고품질 마이크 사용자가 과도한 점수를 받음
- LLM API 비용이 최적화되지 않음

---

## 2. 마이크 캘리브레이션 시스템

### 2.1 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    Calibration Workflow                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Unity Client                      Server                    │
│  ────────────                      ──────                    │
│       │                               │                      │
│       │ POST /calibrate/start        │                      │
│       │ ─────────────────────────────>│                      │
│       │     {device_id}               │                      │
│       │                               │                      │
│       │ ← session_id, samples_needed  │                      │
│       │                               │                      │
│       │ [Record Sample 1]             │                      │
│       │ POST /calibrate/sample        │                      │
│       │ ─────────────────────────────>│                      │
│       │     {session_id, wav}         │                      │
│       │                               │ → Extract metrics    │
│       │ ← samples_received: 1         │   (jitter, shimmer,  │
│       │                               │    pitch, HNR)       │
│       │ [Record Sample 2]             │                      │
│       │ POST /calibrate/sample        │                      │
│       │ ─────────────────────────────>│                      │
│       │ ← samples_received: 2         │                      │
│       │                               │                      │
│       │ [Record Sample 3]             │                      │
│       │ POST /calibrate/sample        │                      │
│       │ ─────────────────────────────>│                      │
│       │ ← is_complete: true           │                      │
│       │                               │                      │
│       │ POST /calibrate/finish        │                      │
│       │ ─────────────────────────────>│                      │
│       │                               │ → Compute baseline   │
│       │                               │   (mean, std)        │
│       │                               │ → Save to SQLite     │
│       │ ← calibration_applied: true   │                      │
│       │                               │                      │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 API 엔드포인트

| 엔드포인트 | Method | 설명 |
|-----------|--------|------|
| `/calibrate/start` | POST | 캘리브레이션 세션 시작 |
| `/calibrate/sample` | POST | 오디오 샘플 제출 |
| `/calibrate/finish` | POST | 캘리브레이션 완료 및 베이스라인 계산 |
| `/calibrate/status` | GET | 캘리브레이션 상태 조회 |

### 2.3 데이터베이스 구조

**SQLite Schema (calibration.db)**:
```sql
CREATE TABLE calibration_sessions (
    session_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    created_at TIMESTAMP,
    status TEXT
);

CREATE TABLE calibration_samples (
    sample_id TEXT PRIMARY KEY,
    session_id TEXT,
    jitter REAL,
    shimmer REAL,
    pitch_dev REAL,
    hnr_db REAL,
    f0_mean REAL,
    created_at TIMESTAMP
);

CREATE TABLE device_baselines (
    device_id TEXT PRIMARY KEY,
    jitter_mean REAL, jitter_std REAL,
    shimmer_mean REAL, shimmer_std REAL,
    pitch_dev_mean REAL, pitch_dev_std REAL,
    hnr_db_mean REAL, hnr_db_std REAL,
    f0_mean_mean REAL, f0_mean_std REAL,
    num_samples INTEGER,
    updated_at TIMESTAMP
);
```

### 2.4 점수 정규화 알고리즘

**Z-Score 기반 정규화**:
```python
def compute_audio_score_v2(jitter, shimmer, pitch_dev, hnr_db, baseline=None):
    if baseline:
        # 사용자별 베이스라인 사용
        j_z = abs(jitter - baseline['jitter'][0]) / baseline['jitter'][1]
        s_z = abs(shimmer - baseline['shimmer'][0]) / baseline['shimmer'][1]
        p_z = abs(pitch_dev - baseline['pitch_dev'][0]) / baseline['pitch_dev'][1]
        h_z = max(0, (baseline['hnr_db'][0] - hnr_db)) / baseline['hnr_db'][1]
        
        # Z-score → 0-1 점수 변환
        score = 1.0 - (z * 0.25)  # z=0→1.0, z=1→0.75, z=2→0.5
    else:
        # 개선된 기본 임계값 사용
        # 부드러운 전환, 최소 0.3 보장
```

### 2.5 Unity UI 구현

**캘리브레이션 문장**:
```
1. "안녕하세요, 오늘 기분이 정말 좋아요."  (기본 인사)
2. "오늘 날씨가 참 맑고 화창하네요."        (평범한 관찰)
3. "당신을 만나서 정말 기뻐요."             (감정 표현)
```

**UI 컴포넌트**:
- `calibrateButton`: 캘리브레이션 시작
- `calibrationStatusText`: 상태 표시 (○/✓)
- `calibrationPanel`: 진행 중 오버레이

---

## 3. Mood 회복 시스템

### 3.1 메커니즘

```python
def _update_npc_state(session_id, score, emotion, mood_change, ...):
    # 1. 시간 경과에 따른 자연 회복
    time_elapsed = current_time - last_interaction_time
    if time_elapsed > 10:
        recovery = min(0.05, 0.01 * (time_elapsed / 10))
        if mood < 0.5:
            mood = min(0.5, mood + recovery)
    
    # 2. 관용 (Forgiveness)
    if mood_change < 0 and mood < 0.4:
        mood_change *= 0.7  # 부정적 영향 감소
    
    # 3. 보상 (Appreciation)
    if mood_change > 0 and mood < 0.3:
        mood_change *= 1.3  # 긍정적 노력 가중
```

### 3.2 효과

| 상황 | 이전 | 이후 |
|------|------|------|
| 기분 나쁜 상태에서 실수 | 100% 패널티 | 70% 패널티 |
| 기분 나쁜 상태에서 개선 노력 | 100% 보상 | 130% 보상 |
| 10초 이상 대화 없음 | 무변화 | 최대 0.05 회복 |

---

## 4. LLM API 최적화 계획

### 4.1 현재 상태

| 용도 | 모델 | 비용 (1M 토큰) |
|------|------|----------------|
| NPC 응답 | Gemini | ~$0.50-2.00 |
| 텍스트 점수 | Gemini | ~$0.50-2.00 |

### 4.2 권장 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    Smart Routing Layer                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  요청 분석                                                    │
│  ├── 단순 인사/반응 → Gemini Flash-Lite ($0.25/1M)          │
│  ├── 일반 대화     → Gemini Flash ($0.50/1M)                │
│  ├── 복잡한 감정   → Claude Sonnet 4.6 ($3.00/1M)           │
│  └── 배치 분석     → DeepSeek V3.2 ($0.28/1M)               │
│                                                              │
│  문맥 캐싱 (90% 할인)                                         │
│  ├── 시스템 프롬프트 캐싱                                     │
│  └── NPC 성격/규칙 캐싱                                       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 비용 절감 예상

| Phase | 변경사항 | 월 100만 토큰 기준 |
|-------|----------|-------------------|
| 현재 | Gemini 기본 | ~$50-100 |
| Phase 1 | OpenRouter + Flash-Lite | ~$15-25 |
| Phase 2 | 스마트 라우팅 + 캐싱 | ~$5-10 |
| **절감율** | | **80-90%** |

### 4.4 구현 로드맵

**Phase 1 (즉시)**:
- [ ] OpenRouter API 통합
- [ ] 기본 모델을 Gemini Flash-Lite로 변경
- [ ] 비용 모니터링 로깅 추가

**Phase 2 (단기)**:
- [ ] 스마트 라우팅 구현
  - 점수 기반 모델 선택
  - 응답 복잡도 분석
- [ ] 문맥 캐싱 적용

**Phase 3 (중기)**:
- [ ] 배치 API로 대화 요약 분리
- [ ] DeepSeek로 야간 분석 작업
- [ ] 한국 AI 기본법 준수 기능

---

## 5. 파일 변경 목록

### 5.1 서버 사이드

| 파일 | 변경 내용 |
|------|-----------|
| `server.py` | `/calibrate/*` 엔드포인트 추가, device_id 기반 베이스라인 조회 |
| `scorer.py` | `compute_audio_score_v2()` Z-score 정규화, 부드러운 전환 |
| `calibration_db.py` | SQLite 기반 캘리브레이션 데이터 관리 |
| `npc_response_generator_v2.py` | Mood 회복, 관용/보상 메커니즘 |

### 5.2 클라이언트 사이드

| 파일 | 변경 내용 |
|------|-----------|
| `LoveConversationUI.cs` | 캘리브레이션 UI, 문장 표시, 샘플 제출 |
| `UnityMicRecorder.cs` | `StopAndGetClip()` 메서드 추가 |

---

## 6. 테스트 계획

### 6.1 캘리브레이션 테스트

```
1. 캘리브레이션 시작 → session_id 반환 확인
2. 3개 샘플 제출 → 각각 samples_received 증가 확인
3. 완료 → device_baselines 테이블 저장 확인
4. 분석 요청 → 정규화된 점수 적용 확인
```

### 6.2 점수 정규화 테스트

```python
# 미캘리브레이션 기기
jitter=6.0, shimmer=15.0 → audio_score >= 0.3 (최소 보장)

# 캘리브레이션된 기기 (baseline: jitter_mean=5.0, jitter_std=1.5)
jitter=5.2 → z=0.13 → score ≈ 0.97
jitter=8.0 → z=2.0 → score ≈ 0.5
```

### 6.3 Mood 회복 테스트

```
1. NPC mood = 0.2 (나쁨)
2. 15초 대기
3. mood >= 0.25 확인 (회복)
4. 점수 0.3 입력 → mood_change *= 1.3 (보상)
```

---

## 7. 향후 개선 사항

### 7.1 단기
- [ ] Unity 캘리브레이션 UI 시각적 개선
- [ ] 캘리브레이션 진행률 바 추가
- [ ] 음성 가이드 재생 옵션

### 7.2 중기
- [ ] 다중 NPC 각각의 캘리브레이션
- [ ] 환경 변화(소음) 자동 감지
- [ ] 캘리브레이션 유효기간 설정

### 7.3 장기
- [ ] OpenRouter 게이트웨이 통합
- [ ] 실시간 비용 모니터링 대시보드
- [ ] A/B 테스트를 통한 모델 선택 최적화

---

## 8. 참조 문서

- [LLM API 가성비 비교 리서치](./LLM%20API%20가성비%20비교%20리서치.md)
- [Unity MicRecorder 문서](../../Assets/Scripts/UnityMicRecorder.cs)
- [CalibrationDB 구현](../prototype/episode1/calibration_db.py)

---

**작성자**: Cascade AI  
**검토자**: (대기중)  
**승인일**: (대기중)
