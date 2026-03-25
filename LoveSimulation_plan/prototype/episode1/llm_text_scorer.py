"""
LLM-based Text Scorer for Episode 1

Pure LLM approach: All text analysis is done by LLM (no hybrid fallback)
- Response type classification
- Emotion detection
- Confidence scoring
- Text score calculation

Usage:
    scorer = LLMTextScorer(llm_provider='gemini')
    result = scorer.evaluate(transcript, question_context=None)
"""

import json
import os
from typing import Dict, Optional
from llm_provider import LLMProviderFactory


class LLMTextScorer:
    """Pure LLM-based text scorer (no hybrid fallback)."""
    
    def __init__(self, llm_provider: str = 'gemini', config_file: str = 'llm_config.json'):
        """
        Initialize LLM text scorer.
        
        Args:
            llm_provider: LLM provider name ('gemini', 'claude', 'openai', 'ollama')
            config_file: Path to configuration file
        """
        self.llm_provider_name = llm_provider
        self.llm_provider = LLMProviderFactory.create(llm_provider)
        
        if self.llm_provider is None or not self.llm_provider.is_available():
            raise RuntimeError(f"LLM provider '{llm_provider}' not available or not configured")
        
        print(f"[OK] LLM Text Scorer initialized with: {self.llm_provider.get_name()}")
    
    def evaluate(
        self,
        transcript: str,
        question_context: Optional[str] = None
    ) -> Dict:
        """
        Evaluate text using LLM.
        
        Args:
            transcript: User's response
            question_context: Optional context about the question asked
        
        Returns:
            {
                'text_score': float (0..1),
                'response_type': str,
                'emotion': str,
                'confidence': float,
                'reasoning': str,
                'emotion_breakdown': dict
            }
        """
        if not transcript or len(transcript.strip()) == 0:
            return {
                'text_score': 0.1,
                'response_type': '침묵',
                'emotion': 'neutral',
                'confidence': 1.0,
                'reasoning': '응답이 없음',
                'emotion_breakdown': {}
            }
        
        # Build prompt for LLM
        system_prompt = self._build_system_prompt()
        user_message = self._build_user_message(transcript, question_context)
        
        try:
            # Call LLM
            response_text = self.llm_provider.generate(system_prompt, user_message)
            
            # Parse LLM response
            result = self._parse_llm_response(response_text, transcript)
            
            return result
        
        except Exception as e:
            print(f"[ERROR] LLM evaluation failed: {e}")
            # Fallback to basic response
            return {
                'text_score': 0.5,
                'response_type': '불명확',
                'emotion': 'neutral',
                'confidence': 0.3,
                'reasoning': f'LLM 오류: {str(e)}',
                'emotion_breakdown': {}
            }
    
    def _build_system_prompt(self) -> str:
        """Build system prompt for LLM."""
        return """당신은 한국어 대화 분석 전문가입니다.

사용자의 응답을 분석하여 다음을 판정하세요:

1. **응답 타입** (response_type):
   - "정답": 명확한 긍정 응답 (네, 응, 맞아, 그래)
   - "오답": 명확한 부정 응답 (아니, 거짓, 틀렸어)
   - "딴소리": 질문과 무관한 응답 (날씨, 농담, 모르겠어)
   - "침묵": 응답 없음 (빈 문자열)

2. **감정** (emotion):
   - "love": 사랑, 애정 표현
   - "joy": 기쁨, 행복
   - "sadness": 슬픔, 우울
   - "anger": 분노, 짜증
   - "fear": 두려움, 걱정
   - "surprise": 놀람
   - "neutral": 중립

3. **텍스트 점수** (text_score): 0.0 ~ 1.0
   - 1.0: 완벽한 긍정 응답
   - 0.7-0.9: 좋은 응답
   - 0.4-0.7: 중간 응답
   - 0.1-0.4: 약한 응답
   - 0.0-0.1: 매우 부정적 응답

4. **신뢰도** (confidence): 0.0 ~ 1.0
   - 판정의 확실성 정도

5. **이유** (reasoning): 간단한 설명

응답은 반드시 JSON 형식으로 제공하세요:
{
    "text_score": 0.85,
    "response_type": "정답",
    "emotion": "love",
    "confidence": 0.95,
    "reasoning": "명확한 긍정 표현"
}"""
    
    def _build_user_message(self, transcript: str, question_context: Optional[str]) -> str:
        """Build user message for LLM."""
        msg = f"사용자 응답: \"{transcript}\"\n\n"
        
        if question_context:
            msg += f"질문 맥락: {question_context}\n\n"
        
        msg += "위 응답을 분석하여 JSON으로 결과를 제공하세요."
        
        return msg
    
    def _parse_llm_response(self, response_text: str, transcript: str) -> Dict:
        """Parse LLM response and extract structured data."""
        try:
            # Try to extract JSON from response
            json_str = self._extract_json(response_text)
            data = json.loads(json_str)
            
            # Validate and normalize
            result = {
                'text_score': float(data.get('text_score', 0.5)),
                'response_type': str(data.get('response_type', '불명확')),
                'emotion': str(data.get('emotion', 'neutral')),
                'confidence': float(data.get('confidence', 0.7)),
                'reasoning': str(data.get('reasoning', '')),
                'emotion_breakdown': {}
            }
            
            # Clamp values
            result['text_score'] = max(0.0, min(1.0, result['text_score']))
            result['confidence'] = max(0.0, min(1.0, result['confidence']))
            
            return result
        
        except Exception as e:
            print(f"[WARN] Failed to parse LLM response: {e}")
            print(f"[DEBUG] Raw response: {response_text[:200]}")
            
            # Fallback: basic analysis
            return {
                'text_score': 0.5,
                'response_type': '불명확',
                'emotion': 'neutral',
                'confidence': 0.3,
                'reasoning': f'파싱 오류: {str(e)}',
                'emotion_breakdown': {}
            }
    
    def _extract_json(self, text: str) -> str:
        """Extract JSON from LLM response."""
        # Try to find JSON block
        start_idx = text.find('{')
        end_idx = text.rfind('}')
        
        if start_idx != -1 and end_idx != -1 and end_idx > start_idx:
            return text[start_idx:end_idx+1]
        
        raise ValueError("No JSON found in response")
    
    def get_provider_info(self) -> Dict:
        """Get LLM provider information."""
        if self.llm_provider:
            return {
                'provider_name': self.llm_provider.get_name(),
                'is_available': self.llm_provider.is_available()
            }
        return {
            'provider_name': 'unknown',
            'is_available': False
        }


if __name__ == '__main__':
    # Test
    print("=" * 70)
    print("LLM TEXT SCORER TEST")
    print("=" * 70)
    
    try:
        scorer = LLMTextScorer(llm_provider='gemini')
        
        test_cases = [
            ("네, 정말 사랑해요!", "긍정 응답"),
            ("아니, 싫어", "부정 응답"),
            ("뭐가 뭐여?", "불명확 응답"),
            ("", "침묵"),
        ]
        
        for transcript, label in test_cases:
            print(f"\n[{label}] '{transcript}'")
            result = scorer.evaluate(transcript)
            print(f"  Score: {result['text_score']:.3f}")
            print(f"  Type: {result['response_type']}")
            print(f"  Emotion: {result['emotion']}")
            print(f"  Confidence: {result['confidence']:.3f}")
            print(f"  Reasoning: {result['reasoning']}")
        
        print("\n" + "=" * 70)
        print("✅ LLM TEXT SCORER TEST COMPLETE")
        print("=" * 70)
    
    except Exception as e:
        print(f"❌ Test failed: {e}")
        import traceback
        traceback.print_exc()
