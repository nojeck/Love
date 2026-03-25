# Test WAV 3종 수집 및 캘리브레이션 완료 가이드

## 📋 생성된 파일

### 테스트 WAV 파일 (test_wavs/)
```
test_wavs/
├── test_wav_low_noise.wav      (SNR ~25dB) - 청정한 환경
├── test_wav_high_noise.wav     (SNR ~20dB) - 시끄러운 환경
└── test_wav_distance.wav       (SNR ~5dB)  - 거리감 있는 음성
```

**특성:**
| 파일 | SNR | 환경 설명 | 사용처 |
|------|-----|---------|--------|
| low_noise | 24.7dB | 조용한 실내 (이상적) | 최대 점수 수집 |
| high_noise | 20.1dB | 사무실/카페 | 실제 환경 시뮬레이션 |
| distance | 5.4dB | 거리(에코) | 악조건 테스트 |

## 🔧 3단계 워크플로우

### 1단계: WAV 파일 검증
```bash
python validate_wav_files.py test_wavs/*.wav
```
✅ 결과: 모든 파일이 유효한 WAV 형식 확인됨

### 2단계: 데이터베이스 캘리브레이션 (선택)
```bash
python demo_calibration_workflow.py
```

**출력 예:**
```
Environment: Clean speech (SNR ~25dB)
  Baseline computed:
    jitter: 1.199 ± 0.000
    shimmer: 3.369 ± 0.000
    hnr_db: 21.662 ± 0.000
```

### 3단계: 실제 서버 테스트 (선택)

**서버 시작:**
```bash
python server.py
```

**캘리브레이션 요청:**
```bash
curl -X POST http://127.0.0.1:5000/calibrate \
  -F "files=@test_wavs/test_wav_low_noise.wav" \
  -F "files=@test_wavs/test_wav_high_noise.wav" \
  -F "files=@test_wavs/test_wav_distance.wav" \
  -G --data-urlencode "device_id=demo_device" \
  --data-urlencode "notes=3-environment calibration"
```

**응답 예:**
```json
{
  "status": "ok",
  "device_id": "demo_device",
  "session_id": 1,
  "num_files": 3,
  "baseline": {
    "jitter": [1.2, 0.01],
    "shimmer": [3.35, 0.05],
    "pitch_dev": [12.0, 1.0],
    "hnr_db": [21.7, 0.1],
    "f0_mean": [110.0, 0.5],
    "num_samples": 3
  }
}
```

## 📊 각 환경별 특성

### Low Noise (청정 환경)
- **생성 방식:** Formant 신호 + 최소 Gaussian 노이즈
- **SNR:** 24.7dB (실제 조용한 실내)
- **사용 처:** 
  - 기준 성능 수집
  - 최대값 설정

### High Noise (시끄러운 환경)
- **생성 방식:** Formant 신호 + Pink 노이즈 (배경음 시뮬레이션)
- **SNR:** 20.1dB (사무실, 카페)
- **사용 처:**
  - 실제 사용 환경 기준
  - 로버스트한스 확인

### Distance (거리감 있는 음성)
- **생성 방식:** Formant 신호 + 거리 감쇠 + 에코
- **SNR:** 5.4dB (극악조건)
- **사용 처:**
  - 극한 환경 테스트
  - 알고리즘 한계 파악

## 🧪 성능 테스트

### 메트릭 추출 안정성 (추후 실행)
```bash
# Praat/Librosa 설치 후 실행 가능
python validate_audio_metrics.py test_wavs/test_wav_low_noise.wav --runs 10 --tolerance 5.0
```

**기대 결과:**
```
jitter               mean=     1.200  variance=  0.50%  ✅ PASS
shimmer              mean=     3.370  variance=  1.20%  ✅ PASS
pitch_dev_percent    mean=    12.000  variance=  2.80%  ✅ PASS
hnr_db               mean=    21.700  variance=  0.95%  ✅ PASS
f0_mean              mean=   110.000  variance=  0.32%  ✅ PASS

✅ All metrics are stable (variance within tolerance)
```

## 🎯 다음 단계

### 즉시 (Phase 3)
1. **텍스트 점수화 규칙 정의**
   - 정답/오답/딴소리/침묵 분기
   - 룰 기반 점수 계산

2. **LLM 통합 (선택)**
   - Deepgram STT 이미 통합됨
   - LLM 기반 감정 분석 (선택사항)

3. **감정 인식 고도화**
   - 음성 특성과 텍스트 결합
   - Valence/Arousal 계산

### 중기 (Phase 4-5)
1. **메모리 시스템**
   - 대화 기록 저장 (SQLite/JSON)
   - 반복 인식 및 페널티

2. **NPC 반응 파이프라인**
   - Chaos Meter 연동
   - 감정 기반 대사 변화

3. **플레이테스트**
   - 내부 테스터 10명 이상
   - 임계값 튜닝
   - 데이터 기반 밸런싱

## 📁 파일 구조

```
prototype/episode1/
├── test_wavs/                          # 테스트 WAV 저장소 (신규)
│   ├── test_wav_low_noise.wav
│   ├── test_wav_high_noise.wav
│   └── test_wav_distance.wav
│
├── generate_test_wavs.py               # WAV 생성 스크립트 (신규)
├── validate_wav_files.py               # WAV 검증 스크립트 (신규)
├── demo_calibration_workflow.py        # 데모 스크립트 (신규)
│
├── calibration_db.py                   # DB 클래스 (Phase 2)
├── validate_audio_metrics.py           # 안정성 검증 (Phase 1)
│
├── server.py                           # Flask 서버 (개선)
├── scorer.py                           # 점수 계산 (확장)
│
└── CALIBRATION_TEST_GUIDE.md          # 기존 가이드
```

## ⚙️ 기술 상세

### 합성 WAV 생성 알고리즘

**Formants (음성 모양)**
```python
# 음성은 여러 공명 주파수의 조합
signal = (
    0.5 * sin(2π * 700Hz * t) +   # F1: 입 벌림
    0.3 * sin(2π * 1200Hz * t) +  # F2: 혀 위치
    0.2 * sin(2π * 2500Hz * t)    # F3: 이차 공명
)
```

**Envelope (음성 모양)**
```python
# Attack-Decay로 자연스러운 음성
envelope(t) = [
    0 → 1 (0-50ms),    # Attack: 빨리 올라옴
    1 → 0.3 (끝 100ms) # Decay: 천천히 내려옴
]
```

**Noise 종류**
- **White Noise:** 균등한 주파수 분포 (랜덤 소리)
- **Pink Noise:** 저주파 강화 (배경음, 에어컨 소리)

**Distance Simulation**
```python
# 거리에 따른 감쇠 (역제곱 법칙)
attenuation = 0.5  # 2배 거리 = 50% 음성

# 반사음 (에코)
echo_delay = 30ms
echo_amplitude = 0.3
```

## 📝 실험 아이디어

### 1. 환경별 기준선 비교
```python
baselines = {
    'low_noise': db.get_baseline('low_noise'),
    'high_noise': db.get_baseline('high_noise'),
    'distance': db.get_baseline('distance')
}

# 각 환경에서 추출된 지표의 차이 분석
for env_id, baseline in baselines.items():
    print(f"{env_id}: jitter={baseline['jitter'][0]:.3f}")
```

예상: distance 환경에서 jitter/shimmer 증가

### 2. 점수 보정 효과 측정
```python
# 극악 환경 메트릭
bad_metrics = {
    'jitter': 2.5,
    'shimmer': 5.0,
    'pitch_dev': 50.0,
    'hnr_db': 10.0
}

# 보정 전/후 비교
score_before = evaluate_response(bad_metrics, baseline=None)
score_after = evaluate_response(bad_metrics, baseline=distance_baseline)

improvement = (score_after['authenticity'] - score_before['authenticity']) / score_before['authenticity'] * 100
# 예상: 15-30% 개선
```

### 3. 안정성 검증 (실제 오디오 라이브러리)
```bash
# 동일 WAV를 100회 분석하여 추출 오차 측정
python validate_audio_metrics.py test_wavs/test_wav_low_noise.wav --runs 100 --tolerance 3.0
```

## 🎓 학습 포인트

1. **음성 신호 처리:** Formant, Envelope, Noise의 역할
2. **통계 기반 보정:** Z-score를 이용한 편차 감소
3. **DB 설계:** 시계열 데이터(메트릭)를 기준선으로 집계
4. **테스트 환경:** 합성 신호로 재현 가능한 테스트 케이스 작성

## ✅ 체크리스트

- [x] 테스트 WAV 3종 생성 및 검증
- [x] 데모 스크립트 구현 및 실행
- [x] 데이터베이스 통합 확인
- [ ] 실제 Praat/Librosa로 메트릭 추출 (librosa 설치 필요)
- [ ] 환경별 기준선 생성 및 비교
- [ ] Phase 3: 텍스트 점수화 고도화

---

**생성일:** 2026-03-23  
**Phase:** 1-2 완료, Phase 3 준비 중
