import sys
import requests
import json

API_URL = 'http://127.0.0.1:5000/analyze'

def main():
    if len(sys.argv) < 2:
        print('Usage: python deepgram_test_client.py path/to/file.wav')
        return
    path = sys.argv[1]
    files = {'file': open(path, 'rb')}
    try:
        resp = requests.post(API_URL, files=files)
        print('Status:', resp.status_code)
        try:
            print(json.dumps(resp.json(), ensure_ascii=False, indent=2))
        except Exception:
            print(resp.text)
    finally:
        try:
            files['file'].close()
        except Exception:
            pass

if __name__ == '__main__':
    main()
