"""Simple client to POST multiple WAV files to /calibrate for device calibration.
Usage:
  python calibrate_client.py --device editor_pc sample1.wav sample2.wav
"""
import sys
import requests
import argparse

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--device', required=True, help='device id to store calibration under')
    p.add_argument('files', nargs='+', help='wav files to upload')
    p.add_argument('--url', default='http://127.0.0.1:5000/calibrate')
    args = p.parse_args()

    files = [('files', (open(f,'rb'))) for f in args.files]
    # requests expects tuples of (name, file-tuple) but simple form works too
    file_tuples = []
    for f in args.files:
        file_tuples.append(('files', (f, open(f,'rb'), 'audio/wav')))

    resp = requests.post(args.url, params={'device_id': args.device}, files=file_tuples)
    print('Status:', resp.status_code)
    print(resp.text)

if __name__ == '__main__':
    main()
