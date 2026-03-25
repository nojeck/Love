"""
Hybrid Text Scorer: Rules (A) + LLM (C) for optimal performance.

Strategy:
1. Use fast rules-based scoring (A) first
2. If confidence is low, use LLM for verification (C)
3. For repetitive responses, use LLM to generate varied feedback
4. Combine scores intelligently

Usage:
  scorer = HybridTextScorer(use_llm=True, llm_provider='claude')
  result = scorer.evaluate(transcript)
"""
import json
import os
from typing import Dict, Optional
from text_scorer import TextScorer
from llm_provider import LLMProviderFactory


class HybridTextScorer:
    """Hybrid scorer combining rules and LLM."""
    
    def __init__(
        self,
        lexicon_path: Optional[str] = None,
        use_llm: bool = True,
        llm_provider: str = 'claude',
        confidence_threshold: float = 0.6,
        max_repetitions: int = 3
    ):
        """
        Initialize hybrid scorer.
        
        Args:
            lexicon_path: Path to emotion_lexicon.json
            use_llm: Whether to use LLM
            llm_provider: 'claude', 'openai', or 'ollama'
            confidence_threshold: Use LLM if rule confidence < this
            max_repetitions: After N times, use LLM for variation
        """
        self.text_scorer = TextScorer(lexicon_path=lexicon_path, use_llm=False)
        self.use_llm = use_llm
        self.confidence_threshold = confidence_threshold
        self.max_repetitions = max_repetitions
        
        # Initialize LLM provider
        self.llm_provider = None
        if use_llm:
            self.llm_provider = LLMProviderFactory.create(llm_provider)
            if not self.llm_provider:
                print(f"⚠ LLM provider '{llm_provider}' not available. Using rules-only mode.")
                self.use_llm = False
        
        # Track response history for variation
        self.response_history = {}
    
    def evaluate(
        self,
        transcript: str,
        question_context: Optional[str] = None,
        use_llm_if_uncertain: bool = True,
        track_repetition: bool = False
    ) -> Dict:
        """
        Evaluate text using hybrid approach.
        
        Args:
            transcript: User's response
            question_context: Optional context
            use_llm_if_uncertain: Use LLM when rules confidence < threshold
            track_repetition: Track and flag repeated responses
        
        Returns:
            {
                'text_score': float,
                'response_type': str,
                'emotion': str,
                'confidence': float,
                'reasoning': str,
                'is_hybrid': bool,
                'llm_used': bool,
                'has_repetition': bool,
                'emotion_breakdown': dict
            }
        """
        # Step 1: Get rules-based score
        rules_result = self.text_scorer.evaluate(
            transcript,
            question_context=question_context,
            use_llm_if_uncertain=False
        )
        
        llm_used = False
        llm_result = None
        
        # Step 2: Decide whether to use LLM
        should_use_llm = (
            self.use_llm and 
            use_llm_if_uncertain and 
            rules_result['confidence'] < self.confidence_threshold
        )
        
        if should_use_llm:
            # Get LLM evaluation for uncertain cases
            llm_result = self.llm_provider.evaluate_text(
                transcript,
                question_context=question_context,
                rules_feedback=rules_result['reasoning']
            )
            llm_used = (llm_result is not None)
        
        # Step 3: Combine scores (if LLM was used)
        if llm_used:
            # Weight: 40% rules + 60% LLM (LLM is more thorough for uncertain cases)
            combined_score = (
                0.4 * rules_result['text_score'] + 
                0.6 * llm_result['text_score']
            )
            final_emotion = llm_result['emotion']
            final_confidence = llm_result['confidence']
            final_reasoning = llm_result['reasoning']
        else:
            combined_score = rules_result['text_score']
            final_emotion = rules_result['emotion']
            final_confidence = rules_result['confidence']
            final_reasoning = rules_result['reasoning']
        
        # Step 4: Check for repetition
        has_repetition = False
        if track_repetition:
            has_repetition = self._check_repetition(transcript, final_emotion)
        
        return {
            'text_score': round(combined_score, 4),
            'response_type': rules_result['response_type'],
            'emotion': final_emotion,
            'confidence': round(final_confidence, 3),
            'reasoning': final_reasoning,
            'is_hybrid': True,
            'llm_used': llm_used,
            'has_repetition': has_repetition,
            'emotion_breakdown': rules_result.get('emotion_breakdown', {}),
            'rules_score': round(rules_result['text_score'], 4),
            'llm_score': round(llm_result['text_score'], 4) if llm_used else None
        }
    
    def generate_feedback(
        self,
        transcript: str,
        score: float,
        emotion: str,
        previous_feedback: Optional[str] = None,
        force_llm: bool = False
    ) -> Dict:
        """
        Generate NPC feedback with variation.
        
        Args:
            transcript: User's response
            score: Authenticity score
            emotion: Detected emotion
            previous_feedback: Previous response to avoid repetition
            force_llm: Force LLM usage even if score is clear
        
        Returns:
            {
                'feedback': str,
                'variation_type': 'rules' | 'llm',
                'confidence': float
            }
        """
        # Simple rule-based feedback
        if score > 0.9:
            feedback_options = [
                f"오, 정말 {emotion} 가득한 대답이네!",
                f"그런 마음이... 정말 좋은데?",
                f"나도 너와 똑같은 마음이야..."
            ]
        elif score > 0.7:
            feedback_options = [
                f"그래, 네 진심이 느껴져.",
                f"음... 그 정도면 나쁘지 않은데?",
                f"조금은 좋은 대답이야."
            ]
        elif score > 0.4:
            feedback_options = [
                f"흠... 뭔가 부족한데?",
                f"그 정도가 최선이야?",
                f"조금 더 진심이 있으면 좋겠어."
            ]
        else:
            feedback_options = [
                f"정말... 상처받았어.",
                f"그 말 듣고 마음이 아파.",
                f"왜 나한테 그러는 거야?"
            ]
        
        # Choose feedback avoiding repetition
        feedback = None
        for option in feedback_options:
            if option != previous_feedback:
                feedback = option
                break
        
        if feedback is None:
            feedback = feedback_options[0]
        
        # Step 2: If LLM available, enhance for high-confidence cases
        llm_feedback = None
        if self.use_llm and self.llm_provider and (force_llm or score < 0.5):
            llm_feedback = self.llm_provider.generate_feedback(
                transcript=transcript,
                score=score,
                emotion=emotion,
                previous_feedback=previous_feedback
            )
        
        # Use LLM feedback if available and different
        if llm_feedback and llm_feedback != previous_feedback:
            return {
                'feedback': llm_feedback,
                'variation_type': 'llm',
                'confidence': 0.95 if llm_feedback else 0.7
            }
        
        return {
            'feedback': feedback,
            'variation_type': 'rules',
            'confidence': 0.7
        }
    
    def _check_repetition(self, transcript: str, emotion: str) -> bool:
        """
        Check if response is similar to recent history.
        
        Simple implementation: track emotion sequences
        """
        key = f"{emotion}_{transcript[:10]}"
        
        if key not in self.response_history:
            self.response_history[key] = 0
        
        self.response_history[key] += 1
        
        # Mark as repetition if seen > max_repetitions times
        return self.response_history[key] > self.max_repetitions
    
    def get_provider_status(self) -> Dict:
        """Get status of LLM provider."""
        return {
            'llm_enabled': self.use_llm,
            'provider_available': self.llm_provider is not None,
            'provider_type': self.llm_provider.__class__.__name__ if self.llm_provider else None,
            'confidence_threshold': self.confidence_threshold
        }


if __name__ == '__main__':
    # Test
    print("=" * 60)
    print("HYBRID TEXT SCORER TEST")
    print("=" * 60)
    
    scorer = HybridTextScorer(use_llm=False)  # Set to True if API key available
    
    print(f"\nProvider Status: {scorer.get_provider_status()}\n")
    
    test_cases = [
        ("네, 정말 사랑해요!", "high_confidence"),
        ("뭐가 뭐여?", "low_confidence"),
        ("아니, 싫어", "clear_negative"),
    ]
    
    for transcript, label in test_cases:
        result = scorer.evaluate(transcript, track_repetition=True)
        print(f"\n[{label}] '{transcript}'")
        print(f"  Score: {result['text_score']:.3f}")
        print(f"  Emotion: {result['emotion']}")
        print(f"  Confidence: {result['confidence']:.3f}")
        print(f"  LLM Used: {result['llm_used']}")
        print(f"  Has Repetition: {result['has_repetition']}")
        
        # Generate feedback
        feedback = scorer.generate_feedback(
            transcript,
            result['text_score'],
            result['emotion']
        )
        print(f"  Feedback: {feedback['feedback']}")
        print(f"  Variation Type: {feedback['variation_type']}")
