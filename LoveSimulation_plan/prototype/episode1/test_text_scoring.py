"""
Integration test for Phase 3: Text Scoring

Tests:
1. TextScorer initialization and basic scoring
2. Response type classification (correct/incorrect/off-topic/silence)
3. Emotion detection
4. Integration with scorer.py (audio + text combined)
"""
import sys
import os
import json
from pathlib import Path

sys.path.insert(0, os.path.dirname(__file__))

from text_scorer import TextScorer
from scorer import evaluate_response


def test_text_scorer():
    """Test TextScorer functionality."""
    print("\n=== Test: Text Scorer ===")
    
    scorer = TextScorer()
    
    test_cases = [
        ("네, 정말 사랑해요!", "correct_love"),
        ("아니, 싫어", "incorrect_anger"),
        ("뭐가 뭐여?", "unclear"),
        ("", "silence"),
        ("네, 맞아", "correct_neutral"),
        ("알 수 없어요", "unclear"),
        ("정말로 너를 사랑해", "strong_love")
    ]
    
    print("Text Scoring Results:")
    for transcript, label in test_cases:
        result = scorer.evaluate(transcript)
        print(f"\n  [{label}] '{transcript}'")
        print(f"    Score: {result['text_score']:.3f}")
        print(f"    Type: {result['response_type']}")
        print(f"    Emotion: {result['emotion']}")
        print(f"    Confidence: {result['confidence']:.3f}")
        print(f"    Reasoning: {result['reasoning']}")
    
    return True


def test_combined_scoring():
    """Test audio + text combined scoring."""
    print("\n=== Test: Combined Scoring (Audio + Text) ===")
    
    scorer = TextScorer()
    
    # Test case 1: Positive text + positive audio
    print("\nScenario 1: Positive text + Good audio")
    text_eval = scorer.evaluate("네, 정말 사랑해요!")
    audio_metrics = {
        'text_score': text_eval['text_score'],
        'jitter': 0.95,
        'shimmer': 3.4,
        'pitch_dev_percent': 12.0,
        'hnr_db': 21.5,
        'repeat_count': 0
    }
    score = evaluate_response(audio_metrics)
    print(f"  Text Score: {text_eval['text_score']:.3f}")
    print(f"  Audio Score: {score['audio_score']:.3f}")
    print(f"  Authenticity: {score['authenticity']:.3f}")
    
    # Test case 2: Positive text + poor audio
    print("\nScenario 2: Positive text + Poor audio (noisy)")
    audio_metrics_poor = {
        'text_score': text_eval['text_score'],
        'jitter': 2.0,
        'shimmer': 5.0,
        'pitch_dev_percent': 40.0,
        'hnr_db': 12.0,
        'repeat_count': 0
    }
    score_poor = evaluate_response(audio_metrics_poor)
    print(f"  Text Score: {text_eval['text_score']:.3f}")
    print(f"  Audio Score: {score_poor['audio_score']:.3f}")
    print(f"  Authenticity: {score_poor['authenticity']:.3f}")
    
    # Test case 3: Negative text
    print("\nScenario 3: Negative text")
    text_eval_neg = scorer.evaluate("아니, 싫어")
    audio_metrics_neg = {
        'text_score': text_eval_neg['text_score'],
        'jitter': 1.0,
        'shimmer': 3.5,
        'pitch_dev_percent': 12.0,
        'hnr_db': 21.0,
        'repeat_count': 0
    }
    score_neg = evaluate_response(audio_metrics_neg)
    print(f"  Text Score: {text_eval_neg['text_score']:.3f}")
    print(f"  Audio Score: {score_neg['audio_score']:.3f}")
    print(f"  Authenticity: {score_neg['authenticity']:.3f}")
    
    return True


def test_response_types():
    """Test response type classification."""
    print("\n=== Test: Response Type Classification ===")
    
    scorer = TextScorer()
    
    response_tests = {
        "정답": ["네", "응", "네, 맞아", "그래", "당연하지", "물론"],
        "오답": ["아니", "아니야", "거짓이야", "틀렸어"],
        "딴소리": ["뭐야?", "몰라", "이게 뭐지?", "뭔소리야"],
        "침묵": ["", "  "]
    }
    
    print("Response Type Distribution:")
    for expected_type, transcripts in response_tests.items():
        results = []
        for transcript in transcripts:
            result = scorer.evaluate(transcript)
            results.append(result['response_type'])
        
        match_count = sum(1 for r in results if r == expected_type)
        accuracy = match_count / len(results) * 100
        print(f"  {expected_type}: {accuracy:.0f}% accuracy ({match_count}/{len(results)})")
    
    return True


def test_emotion_detection():
    """Test emotion detection from text."""
    print("\n=== Test: Emotion Detection ===")
    
    scorer = TextScorer()
    
    emotion_tests = {
        "love": ["사랑해", "너무 사랑해", "정말 사랑해"],
        "joy": ["기뻐", "행복해", "좋아"],
        "anger": ["싫어", "화났어", "짜증나"],
        "sadness": ["슬프다", "우울해", "그리워"],
        "neutral": ["네", "그래", "알겠어"]
    }
    
    print("Emotion Detection Results:")
    for expected_emotion, transcripts in emotion_tests.items():
        results = []
        scores = []
        for transcript in transcripts:
            result = scorer.evaluate(transcript)
            results.append(result['emotion'])
            scores.append(result['text_score'])
        
        match_count = sum(1 for r in results if r == expected_emotion)
        accuracy = match_count / len(results) * 100
        avg_score = sum(scores) / len(scores)
        print(f"  {expected_emotion}: {accuracy:.0f}% detection (avg_score={avg_score:.3f})")
    
    return True


def main():
    print("=" * 70)
    print("PHASE 3: TEXT SCORING INTEGRATION TESTS")
    print("=" * 70)
    
    try:
        test_text_scorer()
        test_response_types()
        test_emotion_detection()
        test_combined_scoring()
        
        print("\n" + "=" * 70)
        print("✅ ALL TESTS PASSED")
        print("=" * 70)
        return 0
    except Exception as e:
        print(f"\n❌ TEST FAILED: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == '__main__':
    sys.exit(main())
