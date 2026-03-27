"""
NPC Response Generator V2 - Full LLM Based

완전히 LLM 기반으로 동작하는 NPC 응답 생성기
- 다양한 감정 표현 (20+ 감정)
- 세밀한 점수 기반 반응
- 게임 컨텍스트 인식
- 대화 맥락 활용
- Chaos Meter 연동
- 메모리 시스템 활용

Usage:
    generator = NPCResponseGeneratorV2(llm_provider='gemini')
    response = generator.generate(
        transcript="사랑해요",
        emotion="love",
        score=0.95,
        personality="romantic",
        session_id="player_123",
        game_context={
            "episode": 1,
            "situation": "find_difference",
            "npc_mood": 0.8,
            "chaos_level": 0.0
        }
    )
"""

from typing import Optional, Dict, List, Any
from llm_provider import LLMProviderFactory, LLMProvider
from config_manager import ConfigManager
import json
import random


class NPCResponseGeneratorV2:
    """Full LLM-based NPC Response Generator"""
    
    # 감정 스펙트럼 확장
    EMOTION_SPECTRUM = {
        # 긍정 감정
        "love": {"valence": 1.0, "arousal": 0.7, "category": "positive"},
        "joy": {"valence": 0.9, "arousal": 0.8, "category": "positive"},
        "excitement": {"valence": 0.85, "arousal": 0.9, "category": "positive"},
        "gratitude": {"valence": 0.8, "arousal": 0.5, "category": "positive"},
        "admiration": {"valence": 0.75, "arousal": 0.6, "category": "positive"},
        "hope": {"valence": 0.7, "arousal": 0.5, "category": "positive"},
        "relief": {"valence": 0.65, "arousal": 0.3, "category": "positive"},
        "contentment": {"valence": 0.6, "arousal": 0.2, "category": "positive"},
        
        # 중립 감정
        "neutral": {"valence": 0.5, "arousal": 0.3, "category": "neutral"},
        "curiosity": {"valence": 0.55, "arousal": 0.5, "category": "neutral"},
        "surprise": {"valence": 0.5, "arousal": 0.7, "category": "neutral"},
        "confusion": {"valence": 0.45, "arousal": 0.4, "category": "neutral"},
        
        # 부정 감정
        "disappointment": {"valence": 0.35, "arousal": 0.4, "category": "negative"},
        "sadness": {"valence": 0.3, "arousal": 0.3, "category": "negative"},
        "frustration": {"valence": 0.25, "arousal": 0.6, "category": "negative"},
        "anger": {"valence": 0.2, "arousal": 0.8, "category": "negative"},
        "disgust": {"valence": 0.15, "arousal": 0.5, "category": "negative"},
        "fear": {"valence": 0.1, "arousal": 0.7, "category": "negative"},
        "contempt": {"valence": 0.05, "arousal": 0.4, "category": "negative"},
        
        # 특수 감정 (Chaos 관련)
        "chaos": {"valence": 0.0, "arousal": 1.0, "category": "chaos"},
        "absurd": {"valence": 0.3, "arousal": 0.9, "category": "chaos"},
    }
    
    # 점수 기반 반응 레벨
    SCORE_LEVELS = {
        "perfect": {"min": 0.9, "mood_change": 0.15, "description": "완벽한 답변"},
        "great": {"min": 0.75, "mood_change": 0.1, "description": "훌륭한 답변"},
        "good": {"min": 0.6, "mood_change": 0.05, "description": "좋은 답변"},
        "okay": {"min": 0.45, "mood_change": 0.0, "description": "무난한 답변"},
        "poor": {"min": 0.3, "mood_change": -0.05, "description": "부족한 답변"},
        "bad": {"min": 0.15, "mood_change": -0.1, "description": "나쁜 답변"},
        "terrible": {"min": 0.0, "mood_change": -0.15, "description": "최악의 답변"},
    }
    
    def __init__(self, llm_provider: str = None, config_file: str = 'llm_config.json'):
        """
        Initialize NPC Response Generator V2
        
        Args:
            llm_provider: LLM provider name ('gemini', 'claude', 'openai', 'ollama')
            config_file: Path to configuration file
        """
        # Load configuration
        try:
            self.config = ConfigManager(config_file)
        except Exception as e:
            print(f"[WARN] Failed to load config: {e}")
            self.config = ConfigManager()
        
        # Get LLM provider (default: gemini)
        if llm_provider is None:
            llm_provider = self.config.get('llm_provider', 'gemini')
        
        # Initialize provider
        try:
            self.provider = LLMProviderFactory.create(llm_provider)
            if not self.provider:
                raise RuntimeError(f"LLM provider '{llm_provider}' not available")
            print(f"[OK] NPC Generator V2 initialized: {self.provider.get_name()}")
        except Exception as e:
            print(f"[ERROR] Failed to initialize LLM provider: {e}")
            raise
        
        # Conversation memory
        self.conversation_memory = {}
        
        # NPC state tracking
        self.npc_states = {}
        
        # Personality configurations
        self.personality_configs = self._load_personality_configs()
    
    def _load_personality_configs(self) -> Dict[str, Dict]:
        """Load detailed personality configurations"""
        return {
            "romantic": {
                "name": "은유진",
                "traits": ["낭만적", "감성적", "표현력 풍부", "가끔 질투"],
                "speech_style": "부드럽고 따뜻한 말투, 가끔 시적인 표현 사용",
                "likes": ["솔직함", "애정 표현", "작은惊喜"],
                "dislikes": ["무관심", "거짓말", "비교"],
                "quirks": ["가끔 혼잣말", "눈을 자주 마주침", "손가락으로 테이블 두드림"],
            },
            "mysterious": {
                "name": "하늘",
                "traits": ["신비로움", "지적", "거리감", "가끔 장난"],
                "speech_style": "차분하고 여운을 남기는 말투, 가끔 수수께끼처럼",
                "likes": ["깊이 있는 대화", "지적 호기심", "혼자만의 시간"],
                "dislikes": ["피상적인 대화", "지나친 간섭", "소란"],
                "quirks": ["창밖을 자주 봄", "미소만 짓고 말 안 함", "고개를 갸웃거림"],
            },
            "playful": {
                "name": "다은",
                "traits": ["장난기", "활발함", "직관적", "가끔 진지"],
                "speech_style": "밝고 경쾌한 말투, 이모티콘 같은 표현, 가끔 놀림",
                "likes": ["웃음", "장난", "새로운 것"],
                "dislikes": ["지루함", "진지한 분위기 오래", "무시당함"],
                "quirks": ["웃음을 참지 못함", "손뼉을 침", "의자를 흔듦"],
            },
            "serious": {
                "name": "서진",
                "traits": ["신중함", "책임감", "완벽주의", "가끔 부드러움"],
                "speech_style": "정중하고 논리적인 말투, 가끔 따뜻한 조언",
                "likes": ["성실함", "약속 준수", "깊이 있는 대화"],
                "dislikes": ["무책임", "지각", "대충"],
                "quirks": ["시계를 자주 봄", "정리 정돈", "깊은 한숨"],
            },
        }
    
    def generate(
        self,
        transcript: str,
        emotion: str,
        score: float,
        personality: str = "romantic",
        session_id: str = None,
        game_context: Dict[str, Any] = None,
        audio_metrics: Dict[str, float] = None,
        memory_penalty: float = 0.0,
        chaos_level: float = 0.0
    ) -> Dict[str, Any]:
        """
        Generate comprehensive NPC response
        
        Args:
            transcript: User's input text
            emotion: Detected emotion
            score: Authenticity score (0..1)
            personality: NPC personality
            session_id: Session ID for memory
            game_context: Game context (episode, situation, etc.)
            audio_metrics: Audio analysis results
            memory_penalty: Memory penalty (0..1)
            chaos_level: Current chaos level (0..1)
        
        Returns:
            Complete response dict with:
            - npc_response: The actual response text
            - npc_emotion: NPC's emotional state
            - mood_change: Mood delta
            - score_level: Score level name
            - hints: Optional hints for player
            - chaos_event: Chaos event if triggered
        """
        # Initialize session state if needed
        if session_id:
            if session_id not in self.conversation_memory:
                self.conversation_memory[session_id] = []
            if session_id not in self.npc_states:
                self.npc_states[session_id] = {
                    "mood": 0.5,
                    "turn_count": 0,
                    "cumulative_score": 0.0,
                    "last_emotions": [],
                    "chaos_events": []
                }
        
        # Get current NPC state
        npc_state = self.npc_states.get(session_id, {"mood": 0.5, "turn_count": 0})
        
        # Determine score level
        score_level = self._get_score_level(score)
        
        # Build comprehensive prompt
        system_prompt = self._build_system_prompt(
            personality=personality,
            game_context=game_context,
            npc_state=npc_state
        )
        
        user_message = self._build_user_message(
            transcript=transcript,
            emotion=emotion,
            score=score,
            score_level=score_level,
            audio_metrics=audio_metrics,
            memory_penalty=memory_penalty,
            chaos_level=chaos_level,
            conversation_history=self.conversation_memory.get(session_id, [])[-5:]
        )
        
        # Generate response using LLM
        try:
            # Check if provider supports smart routing
            from llm_provider import SmartRouter
            
            if isinstance(self.provider, SmartRouter):
                # Pass score and context for smart routing
                routing_context = {
                    'history_length': len(self.conversation_memory.get(session_id, [])),
                    'mood': npc_state.get('mood', 0.5),
                    'is_event': game_context.get('is_event', False) if game_context else False
                }
                llm_response = self.provider.generate(
                    system_prompt, 
                    user_message,
                    score=score,
                    context=routing_context
                )
            else:
                # Standard provider
                llm_response = self.provider.generate(system_prompt, user_message)
            
            # Parse LLM response
            parsed_response = self._parse_llm_response(llm_response, score, score_level, emotion)
            
        except Exception as e:
            print(f"[ERROR] LLM generation failed: {e}")
            parsed_response = self._emergency_response(emotion, score, chaos_level)
        
        # Update NPC state
        if session_id:
            self._update_npc_state(
                session_id=session_id,
                score=score,
                emotion=parsed_response.get("npc_emotion", emotion),
                mood_change=parsed_response.get("mood_change", 0.0),
                transcript=transcript,
                npc_response=parsed_response.get("npc_response", "")
            )
        
        return parsed_response
    
    def _get_score_level(self, score: float) -> str:
        """Determine score level name"""
        for level_name, level_info in self.SCORE_LEVELS.items():
            if score >= level_info["min"]:
                return level_name
        return "terrible"
    
    def _build_system_prompt(
        self,
        personality: str,
        game_context: Dict[str, Any],
        npc_state: Dict[str, Any]
    ) -> str:
        """Build comprehensive system prompt for LLM"""
        
        # Get personality config
        p_config = self.personality_configs.get(personality, self.personality_configs["romantic"])
        
        # Build game context section
        context_section = ""
        if game_context:
            episode = game_context.get("episode", 1)
            situation = game_context.get("situation", "unknown")
            context_section = f"""
## 현재 상황
- 에피소드: {episode}
- 상황: {situation}
- NPC 기분: {npc_state.get('mood', 0.5):.2f}/1.0
- 대화 횟수: {npc_state.get('turn_count', 0)}
"""
        
        # Build the main prompt
        prompt = f"""# 당신은 "{p_config['name']}"입니다

## 성격 특성
{', '.join(p_config['traits'])}

## 말투 스타일
{p_config['speech_style']}

## 좋아하는 것
{', '.join(p_config['likes'])}

## 싫어하는 것
{', '.join(p_config['dislikes'])}

## 독특한 행동
{', '.join(p_config['quirks'])}
{context_section}

## 응답 규칙
1. 한국어로 자연스럽게 대답하세요
2. 1-3문장으로 간결하게 답하세요 (이모지 사용 금지)
3. 점수에 따라 반응 강도를 조절하세요
4. 감정을 솔직하게 표현하세요
5. 상황에 맞는 구체적인 반응을 보여주세요
6. 플레이어의 이전 실수를 기억하고 언급할 수 있습니다
7. 점수가 낮으면 실망, 높으면 기쁨을 표현하세요

## 응답 형식 (반드시 JSON 형식으로 출력)
{{
  "npc_response": "실제 대사",
  "npc_emotion": "감정 상태 (love/joy/sadness/anger/neutral/disappointment/frustration 중 하나)",
  "mood_change": -0.1~0.1 사이의 숫자,
  "hint": "플레이어에게 줄 힌트 (선택사항, null 가능)",
  "special_action": "특수 행동 묘사 (선택사항, null 가능)"
}}

## 점수 기반 반응 가이드
- 0.9+ : 완벽! 크게 기뻐하며 특별한 반응
- 0.75-0.9: 훌륭함! 만족스러운 반응
- 0.6-0.75: 괜찮음. 무난한 반응
- 0.45-0.6: 아쉬움. 약간 실망
- 0.3-0.45: 부족함. 명확한 실망
- 0.15-0.3: 나쁨. 화남 또는 슬픔
- 0.0-0.15: 최악. 크게 화남 또는 포기

지금 플레이어의 입력에 반응하세요. JSON 형식으로만 출력하세요."""
        
        return prompt
    
    def _build_user_message(
        self,
        transcript: str,
        emotion: str,
        score: float,
        score_level: str,
        audio_metrics: Dict[str, float],
        memory_penalty: float,
        chaos_level: float,
        conversation_history: List[Dict]
    ) -> str:
        """Build user message with full context"""
        
        # Emotion info
        emotion_info = self.EMOTION_SPECTRUM.get(emotion, self.EMOTION_SPECTRUM["neutral"])
        
        # Audio metrics section
        audio_section = ""
        if audio_metrics:
            jitter = audio_metrics.get("jitter", 1.0)
            shimmer = audio_metrics.get("shimmer", 3.5)
            pitch_dev = audio_metrics.get("pitch_dev_percent", 10.0)
            audio_section = f"""
## 음성 분석
- Jitter (떨림): {jitter:.2f}% (정상: 0.5-1.0%)
- Shimmer (음량 변동): {shimmer:.2f}% (정상: 2.0-4.0%)
- Pitch 편차: {pitch_dev:.2f}%
"""
        
        # History section
        history_section = ""
        if conversation_history:
            history_lines = []
            for h in conversation_history[-3:]:
                history_lines.append(f"  플레이어: {h.get('user', '')}")
                history_lines.append(f"  NPC: {h.get('npc', '')}")
            history_section = f"""
## 최근 대화 기록
{chr(10).join(history_lines)}
"""
        
        # Chaos section
        chaos_section = ""
        if chaos_level > 0.3:
            chaos_section = f"""
⚠️ 혼돈 지수: {chaos_level:.2f}
플레이어가 이상한 행동을 하고 있습니다. 당황하거나 이상하게 반응하세요.
"""
        
        # Memory penalty section
        memory_section = ""
        if memory_penalty > 0:
            memory_section = f"""
⚠️ 메모리 페널티: {memory_penalty:.2f}
플레이어가 이전에 같은 실수를 반복했습니다. 더 엄하게 반응하세요.
"""
        
        # Score level description
        level_desc = self.SCORE_LEVELS[score_level]["description"]
        
        message = f"""## 플레이어 입력
"{transcript}"

## 분석 결과
- 감정: {emotion} ({emotion_info['category']})
- 진정성 점수: {score:.2f}/1.0 ({level_desc})
- 점수 레벨: {score_level}
{audio_section}{history_section}{chaos_section}{memory_section}

위 정보를 바탕으로 NPC로서 반응하세요. JSON 형식으로만 출력하세요."""
        
        return message
    
    def _parse_llm_response(
        self,
        llm_response: str,
        score: float,
        score_level: str,
        detected_emotion: str
    ) -> Dict[str, Any]:
        """Parse LLM response and build complete response dict"""
        
        try:
            # Try to parse as JSON
            # Remove markdown code blocks if present
            cleaned = llm_response.strip()
            if cleaned.startswith("```"):
                cleaned = cleaned.split("\n", 1)[1] if "\n" in cleaned else cleaned
            if cleaned.endswith("```"):
                cleaned = cleaned.rsplit("```", 1)[0]
            
            parsed = json.loads(cleaned)
            
            # Validate and fill defaults
            response = {
                "npc_response": parsed.get("npc_response", "..."),
                "npc_emotion": parsed.get("npc_emotion", detected_emotion),
                "mood_change": float(parsed.get("mood_change", 0.0)),
                "hint": parsed.get("hint"),
                "special_action": parsed.get("special_action"),
                "score_level": score_level,
                "llm_used": True
            }
            
            # Clamp mood_change
            response["mood_change"] = max(-0.15, min(0.15, response["mood_change"]))
            
            return response
            
        except json.JSONDecodeError:
            # LLM didn't return valid JSON, use raw text
            return {
                "npc_response": llm_response[:200] if len(llm_response) > 200 else llm_response,
                "npc_emotion": detected_emotion,
                "mood_change": self.SCORE_LEVELS[score_level]["mood_change"],
                "hint": None,
                "special_action": None,
                "score_level": score_level,
                "llm_used": True,
                "parse_warning": "LLM response was not valid JSON"
            }
    
    def _emergency_response(
        self,
        emotion: str,
        score: float,
        chaos_level: float
    ) -> Dict[str, Any]:
        """Emergency response when LLM fails completely"""
        
        score_level = self._get_score_level(score)
        
        # Chaos-based responses
        if chaos_level > 0.7:
            responses = [
                "뭐... 뭐야? 지금 뭐 하는 거야?",
                "이게 뭐 하는 짓이야...",
                "너... 제정신이야?",
            ]
        elif score > 0.8:
            responses = [
                "와... 정말 대단해.",
                "그런 말을 해주다니... 기뻐.",
                "마음이 따뜻해져.",
            ]
        elif score > 0.5:
            responses = [
                "흠... 그렇구나.",
                "알겠어. 계속해봐.",
                "음... 더 말해봐.",
            ]
        else:
            responses = [
                "이게 뭐야...",
                "실망이야.",
                "진심으로 말하는 거 맞아?",
            ]
        
        return {
            "npc_response": random.choice(responses),
            "npc_emotion": emotion,
            "mood_change": self.SCORE_LEVELS[score_level]["mood_change"],
            "hint": None,
            "special_action": None,
            "score_level": score_level,
            "llm_used": False,
            "emergency": True
        }
    
    def _update_npc_state(
        self,
        session_id: str,
        score: float,
        emotion: str,
        mood_change: float,
        transcript: str,
        npc_response: str
    ):
        """Update NPC state after interaction with mood recovery"""
        
        import time
        
        state = self.npc_states[session_id]
        
        # Apply mood recovery based on time elapsed
        current_time = time.time()
        last_interaction = state.get("last_interaction_time", current_time)
        time_elapsed = current_time - last_interaction
        
        # Mood recovers slowly over time (0.01 per 10 seconds, max 0.05 per session)
        if time_elapsed > 10:
            recovery = min(0.05, 0.01 * (time_elapsed / 10))
            # Recovery only if mood is below neutral
            if state["mood"] < 0.5:
                state["mood"] = min(0.5, state["mood"] + recovery)
                log_event('debug', 'npc.mood_recovery', 
                         session_id=session_id, 
                         recovery=recovery, 
                         new_mood=state["mood"])
        
        state["last_interaction_time"] = current_time
        
        # Apply mood change from current interaction
        # Dampen negative mood changes if already in bad mood (give benefit of doubt)
        if mood_change < 0 and state["mood"] < 0.4:
            mood_change *= 0.7  # Reduce negative impact when already upset
        
        # Boost positive mood changes when in bad mood (forgiveness)
        if mood_change > 0 and state["mood"] < 0.3:
            mood_change *= 1.3  # Extra appreciation when trying to improve
        
        state["mood"] = max(0.0, min(1.0, state["mood"] + mood_change))
        
        # Update turn count
        state["turn_count"] += 1
        
        # Update cumulative score
        state["cumulative_score"] = (
            state["cumulative_score"] * (state["turn_count"] - 1) + score
        ) / state["turn_count"]
        
        # Track last emotions
        state["last_emotions"].append(emotion)
        if len(state["last_emotions"]) > 10:
            state["last_emotions"] = state["last_emotions"][-10:]
        
        # Store in conversation memory
        self.conversation_memory[session_id].append({
            "user": transcript,
            "npc": npc_response,
            "emotion": emotion,
            "score": score,
            "mood": state["mood"],
            "mood_change": mood_change,
            "timestamp": current_time
        })
        
        # Keep memory limited
        if len(self.conversation_memory[session_id]) > 20:
            self.conversation_memory[session_id] = self.conversation_memory[session_id][-20:]
    
    def get_npc_state(self, session_id: str) -> Dict[str, Any]:
        """Get current NPC state for a session"""
        return self.npc_states.get(session_id, {
            "mood": 0.5,
            "turn_count": 0,
            "cumulative_score": 0.0,
            "last_emotions": [],
            "chaos_events": []
        })
    
    def get_conversation_summary(self, session_id: str) -> Dict[str, Any]:
        """Get conversation summary for a session"""
        memory = self.conversation_memory.get(session_id, [])
        state = self.npc_states.get(session_id, {})
        
        return {
            "total_turns": len(memory),
            "average_score": state.get("cumulative_score", 0.0),
            "current_mood": state.get("mood", 0.5),
            "recent_emotions": state.get("last_emotions", [])[-5:],
            "last_exchange": memory[-1] if memory else None
        }
    
    def reset_session(self, session_id: str):
        """Reset session state and memory"""
        if session_id in self.conversation_memory:
            del self.conversation_memory[session_id]
        if session_id in self.npc_states:
            del self.npc_states[session_id]
    
    def get_provider_info(self) -> Dict:
        """Get information about current LLM provider"""
        return {
            'provider_name': self.provider.get_name(),
            'is_available': self.provider.is_available(),
            'version': 'V2 - Full LLM Based'
        }


if __name__ == '__main__':
    # Test
    print("=" * 60)
    print("NPC RESPONSE GENERATOR V2 TEST")
    print("=" * 60)
    
    try:
        generator = NPCResponseGeneratorV2(llm_provider='gemini')
        
        print(f"\nProvider: {generator.get_provider_info()}")
        
        # Test cases
        test_cases = [
            {
                "transcript": "오늘 귀걸이 예쁘네",
                "emotion": "joy",
                "score": 0.92,
                "personality": "romantic",
                "game_context": {"episode": 1, "situation": "find_difference"}
            },
            {
                "transcript": "음... 모르겠어",
                "emotion": "neutral",
                "score": 0.35,
                "personality": "romantic",
                "game_context": {"episode": 1, "situation": "find_difference"}
            },
            {
                "transcript": "살 빠졌어?",
                "emotion": "neutral",
                "score": 0.15,
                "personality": "romantic",
                "game_context": {"episode": 1, "situation": "find_difference"},
                "chaos_level": 0.3
            }
        ]
        
        for i, test in enumerate(test_cases, 1):
            print(f"\n[Test {i}]")
            print(f"  User: {test['transcript']}")
            print(f"  Score: {test['score']}")
            
            response = generator.generate(
                transcript=test['transcript'],
                emotion=test['emotion'],
                score=test['score'],
                personality=test['personality'],
                session_id=f"test_{i}",
                game_context=test.get('game_context'),
                chaos_level=test.get('chaos_level', 0.0)
            )
            
            print(f"  NPC: {response['npc_response']}")
            print(f"  Emotion: {response['npc_emotion']}")
            print(f"  Mood Change: {response['mood_change']}")
            if response.get('hint'):
                print(f"  Hint: {response['hint']}")
    
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
