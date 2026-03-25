# 에피소드 1 — 달라진 점 찾기의 미궁 (설계서)

요약
- 히로인 대사: "나 오늘 뭐 달라진 거 없어?"
- 플레이어는 마이크로 음성 응답. STT로 텍스트를 추출하고, Librosa/Praat로 비언어적 지표(Jitter/Shimmer/Pitch/HNR)를 계산하여 `진정성(audio_score)`을 산출.
- 텍스트 기반 가중치(text_score, 설문 RAG)와 `audio_score`, `memory_penalty`를 합성해 `authenticity`를 구하고 임계값으로 성공/실패를 판정.

판정 공식 (프로토타입)
- `authenticity = 0.50 * text_score + 0.35 * audio_score + 0.15 * (1 - memory_penalty)`
- 통과 임계값: `authenticity >= 0.70` → 성공

초기 캘리브레이션
- 플레이어에게 짧은 문장(예: "안녕하세요")을 읽게 하여 개인 기준 F0/HNR/Jitter/Shimmer를 저장.
- 이후의 상대 수치로 `audio_score`를 정규화.

음성 지표(권장 임계값)
- Jitter: 좋음 <=1.0% / 경계 1.0–1.5% / 문제 >1.5%
- Shimmer: 문제 >4.0%
- Pitch deviation: 기준 대비 >30% → 높은 각성(감정폭)
- HNR: 좋음 >20 dB / 문제 <15 dB

혼돈 지수(Chaos Meter)
- 오프토픽 응답: +0.3
- 반복적 오프토픽(메모리 플래그): +0.2
- 모욕/민감 발언: +0.6
- 침묵: +0.5
- 최대 1.0 도달 시 히든 루트(기상캐스터/헐크 등)
- Chaos는 루프 간 유지(메모리에 저장)

메모리 시스템
- 저장항목: 최근 응답(텍스트), normalized audio metrics, timestamp, branch_id
- 동일 실수 반복 시 `memory_penalty` = min(0.15 * repeat_count, 0.45)
- NPC는 루프 재시작 시 이전 실수를 참조하는 문장 노출

에피소드 분기(요약)
- 성공(정답): 키워드(예: 귀걸이) 탐지 + authenticity >=0.70 → 다음 씬
- 실패 — 모욕(예: "살 빠졌어?") : 즉시 분노 루트 → 시각 연출(헐크) → 회귀
- 실패 — 오프토픽(날씨/농담): Chaos +=0.3 → Chaos>=1.0이면 히든 루트 → 회귀
- 실패 — 침묵/모름: 즉시 회귀 + 큰 패널티

피드백(회귀 직후)
- 짧은 데이터 기반 힌트: "네 jitter는 1.6%야 — 긴장한 목소리로 들려. 이번엔 한결 차분하게 말해봐." 형식
- 텍스트 힌트와 함께 NPC TTS(화남/실망/냉정) 재생

디버그 UI(프로토타입)
- 화면에 실시간: transcript, jitter, shimmer, pitch, HNR, text_score, audio_score, authenticity, chaos
- 테스트 모드에서 키워드 화이트리스트 편집 가능

필요 에셋
- 간단한 3D 카페/테이블/의자, NPC 프리팹(스케일/모핑으로 헐크/기상캐스터 연출), 파티클(번개/홍수), SFX(익사/파괴)
- 음성: TTS 목소리 셋(중립/분노/비꼼)

테스트 체크리스트
1. 캘리브레이션이 정상적으로 저장되는가
2. 올바른 키워드에 대해 success 경로가 열린다
3. 모욕 키워드에 대해 immediate fail이 트리거된다
4. 오프토픽 반복으로 Chaos가 누적되어 히든 루트 진입한다
5. 회귀 후 메모리 문구가 적절히 출력된다

참고: 이 문서는 프로토타입 설계용으로, 실제 가중치와 임계값은 플레이테스트를 통해 튜닝해야 합니다.
