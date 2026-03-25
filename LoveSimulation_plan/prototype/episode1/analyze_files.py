"""Post WAV files under sample_wavs/ to local server /analyze and print JSON results."""
import os
import requests
import json
import argparse

ROOT = os.path.dirname(__file__)
SAMPLES_DIR = os.path.join(ROOT, 'sample_wavs')
DEFAULT_URL = 'http://127.0.0.1:5000/analyze'


def post_file(path, url, device_id=None):
    with open(path, 'rb') as f:
        files = {'file': (os.path.basename(path), f, 'audio/wav')}
        params = {}
        if device_id:
            params['device_id'] = device_id
        resp = requests.post(url, params=params, files=files)
        try:
            return resp.status_code, resp.json()
        except Exception:
            return resp.status_code, resp.text


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--device', help='device id to include as calibration', default=None)
    p.add_argument('--url', help='analyze endpoint', default=DEFAULT_URL)
    args = p.parse_args()

    if not os.path.isdir(SAMPLES_DIR):
        print('No sample_wavs directory found:', SAMPLES_DIR)
        return
    files = [os.path.join(SAMPLES_DIR, p) for p in os.listdir(SAMPLES_DIR) if p.lower().endswith('.wav')]
    if not files:
        print('No WAV files found in', SAMPLES_DIR)
        return

    for pth in sorted(files):
        print('Posting', pth)
        code, body = post_file(pth, args.url, device_id=args.device)
        print('Status:', code)
        print(json.dumps(body, indent=2, ensure_ascii=False) if isinstance(body, dict) else body)
        print('-' * 60)


if __name__ == '__main__':
    main()
