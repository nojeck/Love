# 무한 회귀 연애 (LoveSimulation) - 문서 정리

**최종 업데이트:** 2026-03-27  
**프로젝트 상태:** Phase 1-6 완료 (개발 진행 중)

---

## 📚 문서 구조

이 폴더에는 무한 회귀 연애 프로젝트의 모든 문서가 정리되어 있습니다.

### 🎯 핵심 문서 (먼저 읽기)

1. **PROJECT_STATUS.md** ⭐
   - 프로젝트 전체 현황 및 진행 상태
   - Phase 1-6 완료 요약
   - 기술 아키텍처 및 성능 특성
   - 다음 단계 및 우선 순위
   - **→ 이 문서부터 시작하세요**

2. **무한 회귀 연애!.md**
   - 게임 기획서 (핵심 컨셉)
   - 게임 시스템 설명
   - 에피소드 기획 예시
   - 기술 스택 및 구현 가이드

### 📊 상세 보고서

3. **PHASE_1_2_REPORT.md**
   - Phase 1: 오디오 캘리브레이션 시스템
   - Phase 2: 텍스트 점수화 (규칙 기반)
   - 구현 내용 및 테스트 결과

4. **PHASE_3_REPORT.md**
   - Phase 3: 텍스트 점수화 고도화
   - 감정 탐지 및 신뢰도 계산
   - 응답 분류 및 통합 테스트

### 📈 진행 상황

5. **PROGRESS.md**
   - 현재 진행 상황 요약
   - 완료된 Phase별 결과
   - 기술 스택 및 환경 설정
   - 비용 분석

### 🔍 참고 자료

6. **AI 연애 시뮬레이션 기획서 유사 콘텐츠 조사.md**
   - 시장 조사 및 선행 사례
   - 기술적 토대 분석
   - 게임 이론적 접근

### 🛠️ 기술 가이드

7. **design.md**
   - Episode 1 설계서
   - 게임 흐름 및 분기
   - 판정 공식 및 임계값

8. **BUILD_AND_PACKAGE.md**
   - Unity 빌드 가이드
   - 패키지 릴리스 방법
   - 배포 절차

9. **CALIBRATION_TEST_GUIDE.md**
   - Phase 1-2 테스트 가이드
   - 캘리브레이션 워크플로우
   - 트러블슈팅

10. **DEEPGRAM_README.md**
    - Deepgram STT 통합 가이드
    - API 설정 및 사용법

11. **TEST_WAVS_GUIDE.md**
    - 테스트 WAV 파일 생성 및 검증
    - 3가지 환경별 특성
    - 성능 벤치마크

12. **episode1_README.md**
    - Episode 1 프로토타입 개요
    - 빠른 시작 가이드
    - 테스트 방법

---

## 🚀 빠른 시작

### 1단계: 현황 파악
```
PROJECT_STATUS.md 읽기
→ 프로젝트 전체 상태 이해
```

### 2단계: 기획 이해
```
무한 회귀 연애!.md 읽기
→ 게임 컨셉 및 시스템 이해
```

### 3단계: 기술 상세 학습
```
PHASE_1_2_REPORT.md → PHASE_3_REPORT.md 순서로 읽기
→ 각 Phase별 구현 내용 이해
```

### 4단계: 개발 시작
```
design.md + 기술 가이드 참고
→ 실제 개발 진행
```

---

## 📋 문서 요약

| 문서 | 용도 | 읽는 시간 |
|------|------|---------|
| PROJECT_STATUS.md | 전체 현황 파악 (Phase 1-6) | 10분 |
| 무한 회귀 연애!.md | 게임 기획 이해 | 15분 |
| PHASE_1_2_REPORT.md | Phase 1-2 상세 | 20분 |
| PHASE_3_REPORT.md | Phase 3 상세 | 15분 |
| PROGRESS.md | 진행 상황 확인 (Phase 1-6) | 5분 |
| design.md | Episode 1 설계 | 10분 |
| 기술 가이드들 | 필요시 참고 | 가변 |

---

## 🔧 환경 설정

### 기본 (규칙만 사용)
```bash
cd LoveSimulation_plan/prototype/episode1
python server.py
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
# 비용: 90% 절감
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

### Claude LLM 활성화
```bash
export ANTHROPIC_API_KEY=sk-ant-...
export USE_LLM=true
python server.py
```

---

## 📞 문의

- **기술 문제**: `server.py` 로그 확인
- **테스트**: `python test_*.py` 실행
- **설정**: `llm_config.json` 수정

---

**프로젝트 상태:** 개발 진행 중 (Phase 1-6 완료)  
**마지막 업데이트:** 2026-03-27
