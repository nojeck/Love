# Quick Test Script for LoveConversationUI Integration

This script provides a minimal example of how the new LoveConversationScene works.

## Manual Test Steps

### 1. Create LoveConversationScene Canvas

```
GameObject > UI > TextMeshPro > Canvas
  - Rename: "LoveConversationCanvas"
  - RenderMode: Screen Space - Overlay
  
Inside Canvas, create:
  - Panel (Background) with Layout Group
    - Name: "ContentPanel"
    - Color: Dark gray/blue
    
  - Vertical Layout Group for sections:
    
    [Controls Section]
    ├─ Button: "StartButton" (Start Recording)
    ├─ Button: "StopButton" (Stop Recording)
    └─ Text: "StatusText" → "Status: Ready"
    
    [Input Section]
    ├─ InputField: "SessionIdInput"
    └─ Dropdown: "NpcPersonalityDropdown"
    
    [Display Section]
    ├─ Text: "ConversationDisplayText" (large scrollable area)
    └─ Text: "NpcResponseText"
    
    [Info Section]
    ├─ Text: "EmotionText"
    └─ Text: "ContextText"
```

### 2. Create Manager GameObject

```
GameObject > Create Empty
  - Name: "LoveConversationManager"
  - Add Component: LoveConversationUI
  - Add Component: UnityMicRecorder
  
Assign UI fields:
  - startButton → StartButton
  - stopButton → StopButton
  - statusText → StatusText
  - conversationDisplayText → ConversationDisplayText
  - npcResponseText → NpcResponseText
  - emotionText → EmotionText
  - contextText → ContextText
  - sessionIdInput → SessionIdInput
  - npcPersonalityDropdown → NpcPersonalityDropdown
```

### 3. Test Flow

```bash
# 1. Start Server
cd LoveSimulation_sample/Assets/..
python server.py

# 2. In Unity:
# - Play
# - Click "Start Recording"
# - Say something romantic: "정말 사랑해요"
# - Click "Stop Recording"
# - Wait for Server response
# 
# Expected output:
# - Transcript displayed
# - Emotion: love, Valence: 0.8+, Arousal: 0.7+
# - NPC Response: "그런 마음이... 정말 좋은데?" (or similar)
# - Conversation turns: 1
```

## Debugging

### Check in Console

```
LoveConversationUI: Analyze response: {
  "transcript": "정말 사랑해요",
  "text_score": 0.95,
  "emotion": {"emotion": "love", "valence": 0.85, "arousal": 0.72},
  "conversation_context": {"total_turns": 1, "avg_score": 0.95, ...}
}

Requesting feedback: {
  "session_id": "player_20260323_133121",
  "transcript": "정말 사랑해요",
  "emotion": "love",
  "score": 0.95,
  "audio_score": 0.95
}

NPC Feedback: 그런 마음이... 정말 좋은데? (LLM: false)
```

### Common Issues

1. **"No microphone device"**
   - Check Microphone.devices in Unity Settings
   - May need to use virtual microphone (VB-Cable) for testing

2. **"Server connection error"**
   - Verify python server.py is running
   - Check serverUrl = "http://127.0.0.1:5000" in LoveConversationUI
   - Test with curl: `curl http://127.0.0.1:5000/analyze`

3. **"JsonUtility parse error"**
   - Check Server response format matches expected JSON structure
   - Verify all required fields are present

## Repeat Testing: 3-Turn Scenario

```
Turn 1: "정말 사랑해요" (love, 0.95)
  Context: turns=1, avg=0.95, repeated=[], should_vary=false
  NPC: (Rules) "그런 마음이... 정말 좋은데?"

Turn 2: "너를 정말 좋아해" (love, 0.92)
  Context: turns=2, avg=0.935, repeated=[], should_vary=false
  NPC: (Rules) "넌 정말 특별한데..."

Turn 3: "자꾸만 생각나" (love, 0.88)
  Context: turns=3, avg=0.917, repeated=["love"], should_vary=true
  NPC: (LLM - FORCED) "나도 자꾸 너를 생각해... 이 마음을 어쩌지?"
```

Expected behavior on Turn 3:
- Server detects repeated "love" emotion (≥3 times)
- Sets should_vary_response = true
- Calls /feedback with force_llm = true
- Returns was_force_llm = true in response
- NPC response is LLM-generated (more varied than rules)

## Optional: Query Conversation Status

Add a button to UI and call:

```csharp
// In Update()
if (Input.GetKeyDown(KeyCode.Q))
{
    GetComponent<LoveConversationUI>().OnQueryConversationStatus();
}
```

Response:
```json
{
  "total_turns": 3,
  "average_score": 0.917,
  "repeated_emotions": ["love"],
  "emotion_counts": {"love": 3}
}
```

---

Next step: Create Canvas and test full flow.
