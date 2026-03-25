import sys
import requests
import time
import os

if len(sys.argv) < 2:
    print("Usage: test_deepgram_log.py <wav_path>")
    sys.exit(1)

wav = sys.argv[1]
url = "http://127.0.0.1:5000/analyze"
files = {'file': open(wav, 'rb')}
print(f"Posting {wav} -> {url}")
resp = requests.post(url, files=files, timeout=120)
print('Status:', resp.status_code)
try:
    j = resp.json()
    print('Response JSON keys:', list(j.keys()))
    rid = j.get('deepgram_request_id')
    print('deepgram_request_id:', rid)
except Exception:
    print('No JSON response, raw body:')
    print(resp.text[:2000])
    rid = None

# If running locally where server stores deepgram_logs, try to show files
log_dir = os.path.join(os.path.dirname(__file__), 'deepgram_logs')
if rid and os.path.isdir(log_dir):
    matches = [p for p in os.listdir(log_dir) if p.startswith(str(rid))]
    print('Deepgram log files found:', matches)
elif rid:
    print('deepgram_request_id present but deepgram_logs directory not found locally:', log_dir)
else:
    print('No deepgram_request_id returned')
