import json
import os
import uuid

import requests


url = 'http://127.0.0.1:5000/analyze'
base = os.path.dirname(__file__)
wav = os.path.join(base, 'sample_wavs', 'auto_rec_20260314_231117.wav')

client_req_id = uuid.uuid4().hex

try:
    with open(wav, 'rb') as fh:
        files = {'file': fh}
        data = {'client_req_id': client_req_id}
        r = requests.post(url, files=files, data=data, timeout=120)

    print('STATUS', r.status_code)
    try:
        payload = r.json()
    except Exception:
        payload = None

    if isinstance(payload, dict):
        print('req_uuid:', payload.get('req_uuid'))
        print('client_req_id(sent):', client_req_id)
        print('client_req_id(resp):', payload.get('client_req_id'))
        print('deepgram_status:', payload.get('deepgram_status'))
        print('deepgram_request_id:', payload.get('deepgram_request_id'))
        print('deepgram_log_file:', payload.get('deepgram_log_file'))
        print('deepgram_error:', payload.get('deepgram_error'))
        print('transcript_source:', payload.get('transcript_source'))
        print('transcript_is_empty:', payload.get('transcript_is_empty'))
        transcript = ((payload.get('inputs') or {}).get('transcript') or '')
        print('transcript_len:', len(transcript))
        print('RAW_JSON')
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        print(r.text)
except Exception as e:
    print('ERROR', e)
