"""
Advanced text scoring engine for Episode 1.

Features:
1. Rule-based scoring (fast, deterministic)
2. Response classification (correct/incorrect/off-topic/silence)
3. Emotion detection from keywords
4. LLM interface (optional, for high-uncertainty cases)

Usage:
  from text_scorer import TextScorer
  
  scorer = TextScorer(lexicon_path='emotion_lexicon.json')
  result = scorer.evaluate(transcript)
  # {
  #   'text_score': 0.95,
  #   'response_type': 'correct',
  #   'emotion': 'love',
  #   'confidence': 0.92,
  #   'reasoning': 'Strong positive emotion detected'
  # }
"""
import json
import os
import re
from pathlib import Path
from typing import Dict, Tuple, Optional, List


class TextScorer:
    """Advanced text scoring engine with rule-based and optional LLM support."""
    
    def __init__(self, lexicon_path=None, use_llm=False):
        """
        Args:
            lexicon_path: Path to emotion_lexicon.json
            use_llm: Whether to use LLM for uncertain cases (default: False)
        """
        self.use_llm = use_llm
        self.lexicon = self._load_lexicon(lexicon_path)
        
        # Build keyword sets for faster matching
        self._build_keyword_sets()
    
    def _load_lexicon(self, lexicon_path):
        """Load emotion lexicon."""
        if lexicon_path is None:
            base_dir = os.path.dirname(__file__)
            lexicon_path = os.path.join(base_dir, 'emotion_lexicon.json')
        
        if not os.path.exists(lexicon_path):
            print(f"Warning: Lexicon not found at {lexicon_path}, using defaults")
            return self._get_default_lexicon()
        
        try:
            with open(lexicon_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            print(f"Error loading lexicon: {e}")
            return self._get_default_lexicon()
    
    def _get_default_lexicon(self):
        """Default lexicon if file not found."""
        return {
            "감정_어휘": {
                "사랑": {"love": 4},
                "사랑해": {"love": 5},
                "좋아": {"joy": 3},
                "행복": {"joy": 4},
                "싫어": {"anger": 2},
                "미워": {"anger": 3},
                "화나": {"anger": 2},
                "짜증": {"anger": 1},
                "슬프": {"sadness": 3},
                "외로": {"sadness": 2}
            },
            "주제별_점수": {
                "긍정_표현": {
                    "사랑": 4, "사랑해": 5, "좋아": 3, "행복": 4
                },
                "부정_표현": {
                    "싫어": 1, "미워": 0, "화나": 1, "짜증": 1
                },
                "강도_수정자": {
                    "정말": 0.2, "너무": 0.2, "약간": -0.15, "조금": -0.15
                }
            },
            "응답_분류": {
                "정답": {"키워드": ["네", "맞아", "응", "그래"], "점수": 0.95},
                "오답": {"키워드": ["아니", "아니야", "거짓", "틀렸"], "점수": 0.2},
                "딴소리": {"키워드": ["몰라", "모르겠", "뭐", "뭔"], "점수": 0.4},
                "침묵": {"표현": [""], "점수": 0.1}
            }
        }
    
    def _build_keyword_sets(self):
        """Build keyword sets for faster matching."""
        self.positive_keywords = set(
            self.lexicon.get("주제별_점수", {}).get("긍정_표현", {}).keys()
        )
        self.negative_keywords = set(
            self.lexicon.get("주제별_점수", {}).get("부정_표현", {}).keys()
        )
        self.neutral_keywords = set(
            self.lexicon.get("주제별_점수", {}).get("중립_표현", {}).keys()
        )
        self.modifiers = self.lexicon.get("주제별_점수", {}).get("강도_수정자", {})
    
    def _tokenize(self, text: str) -> List[str]:
        """Simple Korean tokenization with support for Korean without spaces."""
        # Replace underscores with spaces for compound keywords
        text = text.replace("_", " ")
        # Split by spaces and punctuation
        tokens = re.split(r'[\s\.,!?\-]', text.lower())
        return [t for t in tokens if t]  # Remove empty strings
    
    def _contains_keyword(self, text: str, keyword: str) -> bool:
        """Check if keyword is contained in text (partial matching)."""
        return keyword.lower() in text.lower()
    
    def _classify_response(self, transcript: str) -> Tuple[str, float]:
        """Classify response type: correct/incorrect/off-topic/silence."""
        if not transcript or len(transcript.strip()) == 0:
            return "침묵", 0.1
        
        tokens = self._tokenize(transcript)
        
        # Check response classification keywords using partial matching
        classification = self.lexicon.get("응답_분류", {})
        
        for resp_type, resp_config in classification.items():
            if resp_type == "침묵":
                continue
            
            keywords = resp_config.get("키워드", [])
            for kw in keywords:
                # Use partial matching for better Korean support
                if self._contains_keyword(transcript, kw):
                    return resp_type, resp_config.get("점수", 0.5)
        
        return "불명확", 0.5
    
    def _calculate_emotion_score(self, transcript: str) -> Tuple[str, float, float]:
        """Calculate emotion score from keywords.
        
        Returns:
            Tuple of (emotion, score, confidence)
        """
        tokens = self._tokenize(transcript)
        emotion_scores = {}
        
        # Get all emotion words from lexicon
        emotion_words = self.lexicon.get("감정_어휘", {})
        
        base_score = 0
        detected_emotion = "neutral"
        max_emotion_weight = 0
        
        for emotion_word, emotion_map in emotion_words.items():
            word_tokens = self._tokenize(emotion_word)
            for word_token in word_tokens:
                if word_token in tokens:
                    for emotion_type, weight in emotion_map.items():
                        if emotion_type not in emotion_scores:
                            emotion_scores[emotion_type] = 0
                        emotion_scores[emotion_type] += weight
        
        # Find dominant emotion
        if emotion_scores:
            detected_emotion = max(emotion_scores.items(), key=lambda x: x[1])[0]
            max_emotion_weight = max(emotion_scores.values())
        
        # Map emotion to score
        emotion_score_map = {
            "love": 0.95,
            "joy": 0.85,
            "surprise": 0.65,
            "sadness": 0.4,
            "anger": 0.25,
            "fear": 0.3,
            "embarrass": 0.35,
            "neutral": 0.5
        }
        
        base_score = emotion_score_map.get(detected_emotion, 0.5)
        
        # Adjust for positive/negative keywords
        positive_count = sum(1 for t in tokens if t in self.positive_keywords)
        negative_count = sum(1 for t in tokens if t in self.negative_keywords)
        
        # Apply modifier adjustments
        for modifier, modifier_value in self.modifiers.items():
            modifier_tokens = self._tokenize(modifier)
            for mod_token in modifier_tokens:
                if mod_token in tokens:
                    base_score += modifier_value
        
        # Clamp score
        base_score = max(0.0, min(1.0, base_score))
        
        confidence = min(0.99, 0.5 + max_emotion_weight * 0.3) if max_emotion_weight > 0 else 0.5
        
        return detected_emotion, base_score, confidence
    
    def _get_reasoning(self, response_type: str, emotion: str, score: float) -> str:
        """Generate human-readable reasoning."""
        if score > 0.9:
            return f"강한 긍정 감정 ({emotion}) - 높은 신뢰도"
        elif score > 0.7:
            return f"명확한 긍정 응답 ({emotion})"
        elif score > 0.5:
            return f"중립적 표현 ({emotion}) - 불확실"
        elif score > 0.3:
            return f"약간의 부정 감정 ({emotion})"
        else:
            return f"명확한 부정/거절 ({emotion})"
    
    def evaluate(
        self,
        transcript: str,
        question_context: Optional[str] = None,
        use_llm_if_uncertain: bool = False
    ) -> Dict:
        """
        Evaluate text and return detailed scoring.
        
        Args:
            transcript: User's spoken response (as text)
            question_context: Optional context about the question asked
            use_llm_if_uncertain: Use LLM for uncertain cases (experimental)
        
        Returns:
            {
                'text_score': float (0..1),
                'response_type': str (정답/오답/딴소리/침묵),
                'emotion': str,
                'confidence': float,
                'reasoning': str,
                'emotion_breakdown': dict
            }
        """
        # Step 1: Classify response type
        response_type, type_score = self._classify_response(transcript)
        
        # Step 2: Detect emotion
        emotion, emotion_score, emotion_confidence = self._calculate_emotion_score(transcript)
        
        # Step 3: Combine scores
        # Emotion score has more weight for better accuracy
        final_score = 0.4 * type_score + 0.6 * emotion_score
        final_score = max(0.0, min(1.0, final_score))
        
        confidence = min(1.0, (emotion_confidence + 0.7) / 2)
        
        # Step 4: Optional LLM verification for uncertain cases
        if use_llm_if_uncertain and confidence < 0.6:
            llm_score = self._evaluate_with_llm(transcript, question_context)
            if llm_score is not None:
                final_score = 0.4 * final_score + 0.6 * llm_score
                confidence = 0.9
        
        return {
            'text_score': round(final_score, 4),
            'response_type': response_type,
            'emotion': emotion,
            'confidence': round(confidence, 3),
            'reasoning': self._get_reasoning(response_type, emotion, final_score),
            'emotion_breakdown': {
                'emotion_score': round(emotion_score, 3),
                'response_type_score': round(type_score, 3)
            }
        }
    
    def _evaluate_with_llm(self, transcript: str, context: Optional[str]) -> Optional[float]:
        """
        Use LLM for scoring (optional, experimental).
        This is a placeholder for future LLM integration.
        
        Args:
            transcript: Text to evaluate
            context: Optional context
        
        Returns:
            Score (0..1) or None if LLM not available
        """
        if not self.use_llm:
            return None
        
        try:
            # Placeholder: This would integrate with Claude/GPT/Ollama
            # For now, just return None to fall back to rule-based
            return None
        except Exception as e:
            print(f"LLM evaluation failed: {e}")
            return None


if __name__ == '__main__':
    # Test
    scorer = TextScorer()
    
    test_cases = [
        "네, 정말 사랑해요!",
        "아니, 싫어",
        "뭐가 뭐여?",
        "",
        "네, 맞아"
    ]
    
    print("=" * 60)
    print("TEXT SCORING TEST")
    print("=" * 60)
    
    for test in test_cases:
        result = scorer.evaluate(test)
        print(f"\nTranscript: '{test}'")
        print(f"  Score: {result['text_score']:.3f}")
        print(f"  Type: {result['response_type']}")
        print(f"  Emotion: {result['emotion']}")
        print(f"  Confidence: {result['confidence']:.3f}")
        print(f"  Reasoning: {result['reasoning']}")
