"""
Integration test: Server with Conversation History
Tests the complete flow: analyze → feedback → conversation tracking
"""

import requests
import json
import time
import tempfile
import wave
import numpy as np


# Server config
BASE_URL = "http://localhost:5000"


def create_test_wav(duration=1.0, frequency=440, filename=None):
    """Create a simple test WAV file."""
    if filename is None:
        fd, filename = tempfile.mkstemp(suffix='.wav')
    else:
        fd = None
    
    sample_rate = 16000
    num_samples = int(sample_rate * duration)
    
    # Generate sine wave
    t = np.linspace(0, duration, num_samples)
    samples = (32767 * 0.5 * np.sin(2 * np.pi * frequency * t)).astype(np.int16)
    
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(samples.tobytes())
    
    if fd:
        import os
        os.close(fd)
    
    return filename


def test_complete_flow():
    """Test complete flow: analyze → feedback → conversation."""
    print("=" * 70)
    print("INTEGRATION TEST: Server with Conversation History")
    print("=" * 70)
    
    session_id = f"test_session_{int(time.time())}"
    print(f"\n[1] Session: {session_id}")
    
    # Test data
    exchanges = [
        ("정말 사랑해요", "love", 0.95),
        ("너를 정말 사랑해", "love", 0.93),
        ("사랑해", "love", 0.94),
    ]
    
    print("\n[2] Testing /analyze endpoint with multiple exchanges")
    for i, (transcript, emotion, score) in enumerate(exchanges, 1):
        try:
            # Create test WAV
            wav_file = create_test_wav()
            
            # Call /analyze
            with open(wav_file, 'rb') as f:
                files = {'file': f}
                data = {
                    'transcript': transcript,
                    'client_req_id': session_id,
                }
                
                resp = requests.post(f"{BASE_URL}/analyze", files=files, data=data, timeout=10)
            
            if resp.status_code == 200:
                result = resp.json()
                print(f"\n   Turn {i}: '{transcript}'")
                print(f"   - Text Score: {result.get('text_evaluation', {}).get('text_score', 'N/A'):.3f}")
                print(f"   - Emotion: {result.get('text_evaluation', {}).get('detected_emotion', 'N/A')}")
                print(f"   - Conversation Turns: {result.get('conversation_context', {}).get('total_turns', 0)}")
                print(f"   - Repeated Emotions: {result.get('conversation_context', {}).get('repeated_emotions', [])}")
            else:
                print(f"   Error {resp.status_code}: {resp.text[:100]}")
        
        except Exception as e:
            print(f"   Exception: {e}")
    
    print("\n[3] Testing /conversation-status endpoint")
    try:
        resp = requests.get(f"{BASE_URL}/conversation-status", 
                           params={'session_id': session_id},
                           timeout=10)
        
        if resp.status_code == 200:
            result = resp.json()
            print(f"   Status: {result['status']}")
            print(f"   Total Turns: {result['total_turns']}")
            print(f"   Average Score: {result['average_score']:.3f}")
            print(f"   Repeated Emotions: {result['repeated_emotions']}")
            print(f"   Emotion Counts: {result['emotion_counts']}")
        else:
            print(f"   Error {resp.status_code}: {resp.text[:100]}")
    
    except Exception as e:
        print(f"   Exception: {e}")
    
    print("\n[4] Testing /feedback endpoint with repetition detection")
    try:
        feedback_data = {
            'session_id': session_id,
            'transcript': '사랑해요',
            'npc_response': '나도...',
            'score': 0.95,
            'audio_score': 0.90,
            'emotion': 'love',
            'force_llm': False
        }
        
        resp = requests.post(f"{BASE_URL}/feedback",
                            json=feedback_data,
                            timeout=10)
        
        if resp.status_code == 200:
            result = resp.json()
            print(f"   Feedback: {result['feedback']}")
            print(f"   Variation Type: {result['variation_type']}")
            print(f"   Was Force LLM: {result['was_force_llm']}")
        else:
            print(f"   Error {resp.status_code}: {resp.text[:100]}")
    
    except Exception as e:
        print(f"   Exception: {e}")
    
    print("\n[5] Testing /hybrid-status endpoint")
    try:
        resp = requests.get(f"{BASE_URL}/hybrid-status", timeout=10)
        
        if resp.status_code == 200:
            result = resp.json()
            print(f"   Status: {result['status']}")
            print(f"   LLM Enabled: {result['llm_enabled']}")
            print(f"   LLM Provider: {result['llm_provider']}")
        else:
            print(f"   Error {resp.status_code}: {resp.text[:100]}")
    
    except Exception as e:
        print(f"   Exception: {e}")
    
    print("\n" + "=" * 70)
    print("INTEGRATION TEST COMPLETE")
    print("=" * 70)


if __name__ == '__main__':
    print("\nNote: Make sure server is running on localhost:5000")
    print("Start with: python server.py\n")
    
    try:
        test_complete_flow()
    except KeyboardInterrupt:
        print("\n\nTest interrupted by user")
    except Exception as e:
        print(f"\n\nTest failed: {e}")
        import traceback
        traceback.print_exc()
