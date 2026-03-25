"""Quick test: Check if server endpoints are working."""
import requests
import json

BASE_URL = "http://localhost:5000"

print("\n" + "=" * 60)
print("Quick Server Integration Test")
print("=" * 60)

# Test 1: Health check via hybrid-status
print("\n[1] Testing /hybrid-status endpoint...")
try:
    resp = requests.get(f"{BASE_URL}/hybrid-status", timeout=5)
    if resp.status_code == 200:
        result = resp.json()
        print(f"✓ Server is responding")
        print(f"  LLM Enabled: {result.get('llm_enabled', 'N/A')}")
        print(f"  LLM Provider: {result.get('llm_provider', 'N/A')}")
    else:
        print(f"✗ Error {resp.status_code}")
except Exception as e:
    print(f"✗ Failed: {e}")

# Test 2: Conversation status with dummy session
print("\n[2] Testing /conversation-status endpoint...")
try:
    resp = requests.get(f"{BASE_URL}/conversation-status", 
                       params={'session_id': 'test_001'},
                       timeout=5)
    if resp.status_code == 200:
        result = resp.json()
        print(f"✓ Endpoint is working")
        print(f"  Session: {result.get('session_id', 'N/A')}")
        print(f"  Turns: {result.get('total_turns', 0)}")
    else:
        print(f"✗ Error {resp.status_code}: {resp.text[:100]}")
except Exception as e:
    print(f"✗ Failed: {e}")

# Test 3: Feedback endpoint
print("\n[3] Testing /feedback endpoint...")
try:
    feedback_data = {
        'session_id': 'test_002',
        'transcript': '사랑해요',
        'emotion': 'love',
        'score': 0.95,
        'audio_score': 0.90,
        'npc_response': '나도...'
    }
    resp = requests.post(f"{BASE_URL}/feedback",
                        json=feedback_data,
                        timeout=5)
    if resp.status_code == 200:
        result = resp.json()
        print(f"✓ Feedback generated")
        print(f"  Feedback: {result.get('feedback', 'N/A')[:50]}")
        print(f"  Variation: {result.get('variation_type', 'N/A')}")
    else:
        print(f"✗ Error {resp.status_code}: {resp.text[:100]}")
except Exception as e:
    print(f"✗ Failed: {e}")

print("\n" + "=" * 60)
print("Quick test complete")
print("=" * 60)
