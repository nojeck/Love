"""
NPC Response Generator using LLM

Generates natural NPC responses based on:
- User transcript
- Detected emotion
- Authenticity score
- NPC personality
- Conversation history

Usage:
    generator = NPCResponseGenerator(llm_provider='claude')
    response = generator.generate(
        transcript="사랑해요",
        emotion="love",
        score=0.95,
        personality="romantic",
        session_id="player_123"
    )
"""

from typing import Optional, Dict, List
from llm_provider import LLMProviderFactory, LLMProvider
from config_manager import ConfigManager


class NPCResponseGenerator:
    """NPC Response Generator using LLM"""
    
    def __init__(self, llm_provider: str = None, config_file: str = 'llm_config.json'):
        """
        Initialize NPC Response Generator
        
        Args:
            llm_provider: LLM provider name ('claude', 'openai', 'ollama', 'gemini')
                         None uses config file or environment variable
            config_file: Path to configuration file
        """
        # Load configuration
        try:
            self.config = ConfigManager(config_file)
        except Exception as e:
            print(f"Warning: Failed to load config: {e}")
            self.config = ConfigManager()
        
        # Get LLM provider
        if llm_provider is None:
            llm_provider = self.config.get('llm_provider', 'claude')
        
        # Try to create provider, but don't fail if it's not available
        try:
            self.provider = LLMProviderFactory.create(llm_provider)
            if not self.provider:
                print(f"Warning: LLM provider '{llm_provider}' not available, using fallback")
                self.provider = None
        except Exception as e:
            print(f"Warning: Failed to initialize LLM provider '{llm_provider}': {e}")
            self.provider = None
        
        # Conversation history tracking
        self.conversation_history = {}
        
        # Personality-specific system prompts
        self.personality_prompts = self._load_personality_prompts()
    
    def _load_personality_prompts(self) -> Dict[str, str]:
        """Load personality-specific system prompts"""
        return {
            "romantic": """당신은 낭만적이고 감정적인 NPC입니다.
- 사랑과 감정을 중시합니다
- 따뜻하고 부드러운 톤으로 말합니다
- 상대방의 감정에 공감합니다
- 시적이고 아름다운 표현을 사용합니다
- 한국어로 자연스럽게 대답합니다
- 한 문장 또는 두 문장으로 간결하게 답합니다
- 이모지는 사용하지 않습니다""",
            
            "mysterious": """당신은 신비로운 NPC입니다.
- 조용하고 신비로운 분위기를 유지합니다
- 상대방을 궁금하게 만드는 대답을 합니다
- 깊이 있는 질문을 던집니다
- 약간 거리감 있는 톤입니다
- 한국어로 자연스럽게 대답합니다
- 한 문장 또는 두 문장으로 간결하게 답합니다
- 이모지는 사용하지 않습니다""",
            
            "playful": """당신은 장난스럽고 밝은 NPC입니다.
- 재미있고 가벼운 톤입니다
- 상대방을 웃게 만드는 대답을 합니다
- 장난스러운 질문을 던집니다
- 밝고 긍정적입니다
- 한국어로 자연스럽게 대답합니다
- 한 문장 또는 두 문장으로 간결하게 답합니다
- 이모지는 사용하지 않습니다""",
            
            "serious": """당신은 진지하고 차분한 NPC입니다.
- 진지하고 신중한 톤입니다
- 상대방의 말을 깊이 있게 받아줍니다
- 논리적이고 이성적입니다
- 상대방을 존중합니다
- 한국어로 자연스럽게 대답합니다
- 한 문장 또는 두 문장으로 간결하게 답합니다
- 이모지는 사용하지 않습니다"""
        }
    
    def generate(
        self,
        transcript: str,
        emotion: str,
        score: float,
        personality: str = "romantic",
        session_id: str = None
    ) -> str:
        """
        Generate NPC response
        
        Args:
            transcript: User's input text
            emotion: Detected emotion (love, joy, sadness, anger, etc.)
            score: Authenticity score (0..1)
            personality: NPC personality (romantic, mysterious, playful, serious)
            session_id: Session ID for conversation history tracking
        
        Returns:
            Generated NPC response
        """
        
        # Get system prompt for personality
        system_prompt = self.personality_prompts.get(
            personality.lower(),
            self.personality_prompts["romantic"]
        )
        
        # Create user message with context
        user_message = self._create_user_message(
            transcript, emotion, score, personality
        )
        
        # Initialize conversation history for session if needed
        if session_id and session_id not in self.conversation_history:
            self.conversation_history[session_id] = []
        
        # Generate response using LLM
        try:
            if self.provider:
                npc_response = self.provider.generate(system_prompt, user_message)
            else:
                # Provider not available, use fallback
                npc_response = self._fallback_response(emotion, score)
        except Exception as e:
            print(f"LLM generation failed: {e}")
            # Fallback to rule-based response
            npc_response = self._fallback_response(emotion, score)
        
        # Store in conversation history
        if session_id:
            self.conversation_history[session_id].append({
                "user": transcript,
                "emotion": emotion,
                "score": score,
                "npc": npc_response
            })
            
            # Keep only recent history (last 20 exchanges)
            if len(self.conversation_history[session_id]) > 20:
                self.conversation_history[session_id] = \
                    self.conversation_history[session_id][-20:]
        
        return npc_response
    
    def _create_user_message(
        self,
        transcript: str,
        emotion: str,
        score: float,
        personality: str
    ) -> str:
        """Create user message with context for LLM"""
        
        # Determine tone based on score
        if score > 0.8:
            tone = "매우 긍정적이고 따뜻하게"
        elif score > 0.6:
            tone = "긍정적으로"
        elif score > 0.4:
            tone = "중립적으로"
        else:
            tone = "약간 걱정스럽게"
        
        # Emotion descriptions
        emotion_desc = {
            "love": "사랑",
            "joy": "기쁨",
            "sadness": "슬픔",
            "anger": "분노",
            "fear": "두려움",
            "surprise": "놀람",
            "neutral": "중립"
        }
        
        emotion_text = emotion_desc.get(emotion, emotion)
        
        # Create message
        message = f"""사용자가 다음과 같이 말했습니다:
"{transcript}"

감정: {emotion_text}
진정성 점수: {score:.2f}/1.0

{tone} 반응해주세요."""
        
        return message
    
    def _fallback_response(self, emotion: str, score: float) -> str:
        """Fallback rule-based response when LLM fails"""
        
        # Score-based responses
        if score > 0.8:
            responses = {
                "love": "오, 정말 사랑이 가득한 마음이네.",
                "joy": "너의 기쁨이 나도 기쁘게 해.",
                "sadness": "뭔가 슬픈 일이 있어?",
                "anger": "화난 마음도 이해해.",
                "neutral": "음... 계속 말해봐."
            }
        elif score > 0.6:
            responses = {
                "love": "그런 마음이... 정말 좋은데?",
                "joy": "좋은 감정을 가지고 있군.",
                "sadness": "그런 기분도 있을 수 있지.",
                "anger": "뭔가 화난 것 같은데?",
                "neutral": "음... 뭔가 있어?"
            }
        elif score > 0.4:
            responses = {
                "love": "흠... 뭔가 부족한데?",
                "joy": "조금은 좋은 대답이야.",
                "sadness": "그런 마음도 있구나.",
                "anger": "뭔가 불만이 있어?",
                "neutral": "음... 계속 말해봐."
            }
        else:
            responses = {
                "love": "정말... 상처받았어.",
                "joy": "그 정도가 최선이야?",
                "sadness": "왜 그런 마음이 드는 거야?",
                "anger": "왜 나한테 그러는 거야?",
                "neutral": "뭐가 뭐여?"
            }
        
        return responses.get(emotion, "음... 계속 말해봐.")
    
    def get_conversation_history(self, session_id: str) -> List[Dict]:
        """
        Get conversation history for a session
        
        Args:
            session_id: Session ID
        
        Returns:
            List of conversation exchanges
        """
        return self.conversation_history.get(session_id, [])
    
    def clear_conversation_history(self, session_id: str = None):
        """
        Clear conversation history
        
        Args:
            session_id: Session ID to clear (None clears all)
        """
        if session_id:
            if session_id in self.conversation_history:
                del self.conversation_history[session_id]
        else:
            self.conversation_history.clear()
    
    def get_provider_info(self) -> Dict:
        """Get information about current LLM provider"""
        if self.provider:
            return {
                'provider_name': self.provider.get_name(),
                'is_available': self.provider.is_available()
            }
        else:
            return {
                'provider_name': 'fallback',
                'is_available': True
            }


if __name__ == '__main__':
    # Test
    print("=" * 60)
    print("NPC RESPONSE GENERATOR TEST")
    print("=" * 60)
    
    try:
        generator = NPCResponseGenerator(llm_provider='claude')
        
        print(f"\nProvider: {generator.get_provider_info()['provider_name']}")
        
        # Test cases
        test_cases = [
            {
                "transcript": "사랑해요",
                "emotion": "love",
                "score": 0.95,
                "personality": "romantic"
            },
            {
                "transcript": "싫어",
                "emotion": "anger",
                "score": 0.25,
                "personality": "serious"
            },
            {
                "transcript": "뭐가 뭐여?",
                "emotion": "neutral",
                "score": 0.5,
                "personality": "playful"
            }
        ]
        
        for i, test in enumerate(test_cases, 1):
            print(f"\n[Test {i}]")
            print(f"  User: {test['transcript']}")
            print(f"  Emotion: {test['emotion']}")
            print(f"  Score: {test['score']}")
            print(f"  Personality: {test['personality']}")
            
            response = generator.generate(
                transcript=test['transcript'],
                emotion=test['emotion'],
                score=test['score'],
                personality=test['personality'],
                session_id=f"test_session_{i}"
            )
            
            print(f"  NPC: {response}")
    
    except Exception as e:
        print(f"Error: {e}")
        print("\nNote: Make sure ANTHROPIC_API_KEY is set")
