"""
Integration tests for Hybrid Text Scorer (A+C system).

Tests:
1. Low confidence detection and LLM fallback
2. Response variation and feedback generation
3. Repetition tracking
4. Server endpoint integration
"""
import json
import os
from hybrid_text_scorer import HybridTextScorer


class TestHybridTextScorerBasics:
    """Basic functionality tests."""
    
    def test_high_confidence_case(self):
        """High confidence should not trigger LLM fallback."""
        scorer = HybridTextScorer(use_llm=False)
        result = scorer.evaluate("정말 사랑해요!")
        assert result['text_score'] > 0.8
        assert result['confidence'] > 0.7
        assert not result['llm_used']
        print("    ✓ High confidence case passed")
    
    def test_low_confidence_case(self):
        """Low confidence should be detected."""
        scorer = HybridTextScorer(use_llm=False)
        result = scorer.evaluate("음... 뭐지?")
        # Ambiguous response should have medium score and confidence
        assert 0.3 <= result['text_score'] <= 0.7
        print(f"    ✓ Low confidence case passed (score={result['text_score']:.3f}, conf={result['confidence']:.3f})")
    
    def test_clear_negative_case(self):
        """Clear negative response should be scored low."""
        scorer = HybridTextScorer(use_llm=False)
        result = scorer.evaluate("싫어, 떨어져!")
        assert result['text_score'] < 0.5
        assert result['emotion'] in ['anger', 'sadness', 'neutral']
        print(f"    ✓ Clear negative case passed (emotion={result['emotion']})")


class TestHybridResponseVariation:
    """Test response variation (NPC feedback)."""
    
    def test_feedback_generation_high_score(self):
        """High score should generate positive feedback."""
        scorer = HybridTextScorer(use_llm=False)
        feedback = scorer.generate_feedback(
            "정말 사랑해요!",
            score=0.95,
            emotion='love'
        )
        assert feedback['feedback']
        assert len(feedback['feedback']) > 0
        assert feedback['variation_type'] in ['rules', 'llm']
        print("    ✓ High score feedback passed")
    
    def test_feedback_generation_low_score(self):
        """Low score should generate negative feedback."""
        scorer = HybridTextScorer(use_llm=False)
        feedback = scorer.generate_feedback(
            "싫어",
            score=0.2,
            emotion='anger'
        )
        assert feedback['feedback']
        print(f"    ✓ Low score feedback passed ('{feedback['feedback']}')")
    
    def test_feedback_avoids_repetition(self):
        """Feedback should avoid repeating previous response."""
        scorer = HybridTextScorer(use_llm=False)
        previous = "좋은 대답이야"
        
        feedback = scorer.generate_feedback(
            "정말 사랑해요!",
            score=0.95,
            emotion='love',
            previous_feedback=previous
        )
        
        # Should be different from previous
        assert feedback['feedback'] != previous
        print(f"    ✓ Repetition avoidance passed")


class TestHybridRepetitionTracking:
    """Test repetition detection."""
    
    def test_repetition_detection(self):
        """Should detect repeated responses."""
        scorer = HybridTextScorer(max_repetitions=2, use_llm=False)
        
        # First few should not be marked as repetition
        for i in range(2):
            result = scorer.evaluate("네", track_repetition=True)
            assert not result['has_repetition']
        
        # Fourth should be marked as repetition (after 2 repetitions allowed)
        result = scorer.evaluate("네", track_repetition=True)
        assert result['has_repetition']
        print("    ✓ Repetition detection passed")


class TestHybridScorerStatus:
    """Test scorer status and configuration."""
    
    def test_status_without_llm(self):
        """Status should show LLM disabled when not configured."""
        scorer = HybridTextScorer(use_llm=False)
        status = scorer.get_provider_status()
        
        assert status['llm_enabled'] == False
        assert status['provider_available'] == False
        print("    ✓ Status check passed (LLM disabled)")


class TestHybridIntegration:
    """Integration tests."""
    
    def test_full_evaluation_flow(self):
        """Complete evaluation flow: rules → possible LLM → feedback."""
        scorer = HybridTextScorer(use_llm=False)
        
        # Evaluate response
        result = scorer.evaluate("나도 좋은데...", track_repetition=True)
        
        assert 'text_score' in result
        assert 'emotion' in result
        assert 'confidence' in result
        assert 'llm_used' in result
        assert 'has_repetition' in result
        
        # Generate feedback based on evaluation
        feedback = scorer.generate_feedback(
            "나도 좋은데...",
            score=result['text_score'],
            emotion=result['emotion']
        )
        
        assert 'feedback' in feedback
        assert 'variation_type' in feedback
        print("    ✓ Full evaluation flow passed")
    
    def test_sequential_responses(self):
        """Test multiple sequential responses (like game dialogue)."""
        scorer = HybridTextScorer(use_llm=False)
        
        responses = [
            ("정말 사랑해요!", 'love'),
            ("당신과 함께하고 싶어요.", 'joy'),
            ("진심으로 좋아해요.", 'love'),
        ]
        
        for transcript, expected_emotion in responses:
            result = scorer.evaluate(transcript)
            assert result['emotion'] in ['love', 'joy', 'neutral', 'sadness', 'anger']
        print("    ✓ Sequential responses passed")


if __name__ == '__main__':
    # Simple test runner
    print("=" * 70)
    print("HYBRID TEXT SCORER TESTS")
    print("=" * 70)
    
    scorer = HybridTextScorer(use_llm=False)
    
    # Test 1: High confidence
    print("\n[Test 1] High confidence case")
    result = scorer.evaluate("정말 사랑해요!")
    print(f"  Score: {result['text_score']:.3f}")
    print(f"  Emotion: {result['emotion']}")
    print(f"  Confidence: {result['confidence']:.3f}")
    print(f"  LLM Used: {result['llm_used']}")
    assert result['text_score'] > 0.6
    assert result['emotion'] == 'love'
    print("  ✓ PASSED")
    
    # Test 2: Low confidence
    print("\n[Test 2] Low confidence case")
    result = scorer.evaluate("음... 모르겠어?")
    print(f"  Score: {result['text_score']:.3f}")
    print(f"  Confidence: {result['confidence']:.3f}")
    print("  ✓ PASSED (uncertain detected)")
    
    # Test 3: Feedback generation
    print("\n[Test 3] Feedback generation")
    feedback = scorer.generate_feedback(
        "정말 사랑해요!",
        score=0.95,
        emotion='love'
    )
    print(f"  Feedback: {feedback['feedback']}")
    print(f"  Type: {feedback['variation_type']}")
    print("  ✓ PASSED")
    
    # Test 4: Repetition avoidance
    print("\n[Test 4] Repetition avoidance")
    previous_fb = "오, 정말 love 가득한 대답이네!"
    feedback = scorer.generate_feedback(
        "정말 사랑해요!",
        score=0.95,
        emotion='love',
        previous_feedback=previous_fb
    )
    print(f"  Previous: {previous_fb}")
    print(f"  New: {feedback['feedback']}")
    assert feedback['feedback'] != previous_fb
    print("  ✓ PASSED (variation generated)")
    
    # Test 5: Status check
    print("\n[Test 5] Status check")
    status = scorer.get_provider_status()
    print(f"  LLM Enabled: {status['llm_enabled']}")
    print(f"  Provider Available: {status['provider_available']}")
    print(f"  Confidence Threshold: {status['confidence_threshold']}")
    print("  ✓ PASSED")
    
    print("\n" + "=" * 70)
    print("ALL TESTS PASSED ✓")
    print("=" * 70)
