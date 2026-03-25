# Phase 1-2 테스트 가이드

## 빠른 시작

### 1. 의존성 설치 확인
```bash
pip list | grep -E "parselmouth|librosa|numpy"
```

필요시 설치:
```bash
pip install parselmouth librosa numpy
```

### 2. 통합 테스트 실행
```bash
python test_calibration.py
```

예상 출력:
```
============================================================
Calibration Integration Tests
============================================================

=== Test: Calibration DB ===
✓ Database initialized: ...
✓ Session created: 1
✓ Added 3 metrics
✓ Baseline computed:
    jitter: mean=1.000, std=0.050
    ...
✓ Baseline exported to JSON: baseline_test_device_001.json

=== Test: Calibration Correction ===
Metrics matching baseline:
  Without calibration: authenticity=0.9500
  With calibration:    authenticity=0.9500
  ...

✅ All tests passed!
============================================================
```

### 3. 기본 서버 테스트

**서버 시작:**
```bash
python server.py
# Listening on http://127.0.0.1:5000
```

**헬스 체크:**
```bash
curl http://127.0.0.1:5000/health
# {"status":"ok"}
```

### 4. 캘리브레이션 엔드포인트 테스트

기존 WAV 파일이 있다면:

```bash
# 캘리브레이션 (3개 파일 추천)
curl -X POST http://127.0.0.1:5000/calibrate \
  -F "files=@sample1.wav" \
  -F "files=@sample2.wav" \
  -F "files=@sample3.wav" \
  -G --data-urlencode "device_id=test_device_001" \
  --data-urlencode "notes=Test calibration"

# 응답 예:
# {
#   "status": "ok",
#   "device_id": "test_device_001",
#   "session_id": 1,
#   "num_files": 3,
#   "baseline": {
#     "jitter": [1.0, 0.05],
#     "shimmer": [3.5, 0.1],
#     ...
#   }
# }
```

### 5. 안정성 검증

WAV 파일이 있을 때:

```bash
# 동일 파일 10회 분석하여 편차 검증
python validate_audio_metrics.py path/to/sample.wav --runs 10 --tolerance 5.0

# 예상 출력:
# === Audio Metric Validation ===
# File: path/to/sample.wav
# Runs: 10
# Tolerance: 5.0%
#
# Run 1: {'jitter': 1.0, 'shimmer': 3.5, ...}
# ...
#
# === Variance Analysis ===
# jitter               mean=     1.000  variance=  0.50%  ✅ PASS
# shimmer              mean=     3.500  variance=  1.20%  ✅ PASS
# ...
#
# === Summary ===
# ✅ All metrics are stable (variance within tolerance)
```

## 상세 테스트

### CalibrationDB 직접 테스트

```python
from calibration_db import CalibrationDB

# 새 DB 생성
db = CalibrationDB('my_test.db')

# 세션 시작
session_id = db.start_session('my_device', notes='Test')

# 메트릭 추가 (여러 개)
metrics = {
    'jitter': 0.98,
    'shimmer': 3.4,
    'pitch_dev_percent': 12.5,
    'hnr_db': 21.0,
    'f0_mean': 110.0,
    'extraction_method': 'praat'
}
db.add_metric(session_id, metrics, 'sample1.wav')
db.add_metric(session_id, metrics, 'sample2.wav')
db.add_metric(session_id, metrics, 'sample3.wav')

# 기준선 계산
baseline = db.compute_baseline('my_device')
print(baseline)
# {
#   'device_id': 'my_device',
#   'jitter': (0.98, 0.0),
#   'shimmer': (3.4, 0.0),
#   ...
#   'num_samples': 3
# }

# 기준선 조회
retrieved = db.get_baseline('my_device')
print(retrieved)

# JSON 내보내기
json_path = db.export_baseline_json('my_device')
print(f"Exported to {json_path}")
```

### Scorer 기준선 적용 테스트

```python
from scorer import evaluate_response, apply_calibration_correction

# 디바이스 기준선
baseline = {
    'jitter': (1.0, 0.1),
    'shimmer': (3.5, 0.2),
    'pitch_dev': (12.0, 2.0),
    'hnr_db': (21.0, 1.0),
    'f0_mean': (110.0, 3.0)
}

# 측정 메트릭
metrics = {
    'text_score': 0.9,
    'jitter': 0.95,
    'shimmer': 3.4,
    'pitch_dev_percent': 11.8,
    'hnr_db': 21.2,
    'repeat_count': 0
}

# 기준선 없이
score_no_calib = evaluate_response(metrics, baseline=None)
print(score_no_calib)
# {'audio_score': ..., 'authenticity': ..., 'calibration_applied': False}

# 기준선 적용
score_with_calib = evaluate_response(metrics, baseline=baseline)
print(score_with_calib)
# {'audio_score': ..., 'authenticity': ..., 'calibration_applied': True}

# 보정 효과 확인
print(f"Score difference: {score_with_calib['authenticity'] - score_no_calib['authenticity']}")
```

## 트러블슈팅

### Parselmouth 설치 오류
```
ModuleNotFoundError: No module named 'parselmouth'
```

**해결:**
```bash
pip install --upgrade parselmouth
# 또는 Anaconda 사용:
conda install -c conda-forge parselmouth
```

### WAV 파일 인식 오류
```
ValueError: file format not supported
```

**확인:**
- WAV 파일 형식 확인: `ffprobe file.wav`
- 샘플레이트 확인: 16kHz, 44.1kHz, 48kHz 권장
- 채널 확인: 모노 권장

### 데이터베이스 잠금
```
sqlite3.OperationalError: database is locked
```

**해결:**
- 서버가 실행 중이면 종료
- calibration/calibration.db 파일 삭제 후 재시작

### Deepgram API 오류 (서버 사용 시)
```
DEEPGRAM_API_KEY not set
```

**해결:**
```bash
export DEEPGRAM_API_KEY=your_key_here
python server.py
```

## 성능 벤치마크

### 메트릭 추출 시간 (WAV 1초, 16kHz 모노)
| 방법 | 시간 |
|------|------|
| Praat | ~400-600ms |
| Librosa (fallback) | ~80-150ms |

### DB 성능
| 작업 | 시간 |
|------|------|
| 세션 생성 | ~2ms |
| 메트릭 저장 | ~5-10ms |
| 기준선 계산 (10개 샘플) | ~15ms |
| 기준선 조회 | ~3ms |

## 다음 단계

1. **실제 환경 테스트**
   - 저소음/고소음/거리 변화 WAV 수집
   - 각 환경에서 기준선 계산
   - 보정 효과 측정

2. **Phase 3 준비**
   - 텍스트 점수화 규칙 정의
   - LLM 통합 (선택사항)
   - 감정 인식 고도화

3. **플레이테스트**
   - 내부 테스터 10명 이상
   - 피드백 수집
   - 임계값 튜닝
