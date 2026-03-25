from flask import Flask, request, jsonify
import logging
import os
import tempfile
import json
import traceback
import hashlib
import time
import uuid
import wave
from scorer import evaluate_response
from calibration_db import CalibrationDB
from llm_text_scorer import LLMTextScorer
from conversation_history import ConversationHistory
from npc_response_generator import NPCResponseGenerator
from config_manager import ConfigManager
import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

# Optional audio libs
try:
    import numpy as np
    import librosa
    import parselmouth
    from parselmouth.praat import call as praat_call
except Exception:
    np = None
    librosa = None
    parselmouth = None
    praat_call = None

app = Flask(__name__)


def log_event(level, event, **fields):
    payload = {
        'ts_ms': int(time.time() * 1000),
        'event': event,
        **fields,
    }
    line = json.dumps(payload, ensure_ascii=False)
    try:
        if level == 'debug':
            app.logger.debug(line)
        elif level == 'warning':
            app.logger.warning(line)
        elif level == 'error':
            app.logger.error(line)
        else:
            app.logger.info(line)
    except Exception as e:
        # Fallback for encoding issues
        try:
            line_ascii = json.dumps(payload, ensure_ascii=True)
            if level == 'debug':
                app.logger.debug(line_ascii)
            elif level == 'warning':
                app.logger.warning(line_ascii)
            elif level == 'error':
                app.logger.error(line_ascii)
            else:
                app.logger.info(line_ascii)
        except Exception:
            pass


def ensure_writable_dir(path):
    os.makedirs(path, exist_ok=True)
    test_file = os.path.join(path, '.write_test')
    with open(test_file, 'w', encoding='utf-8') as fh:
        fh.write('ok')
    os.remove(test_file)


def create_http_session():
    session = requests.Session()
    retries = Retry(
        total=2,
        connect=2,
        read=2,
        backoff_factor=0.4,
        status_forcelist=[429, 500, 502, 503, 504],
        allowed_methods=frozenset(['POST']),
        raise_on_status=False,
    )
    session.mount('https://', HTTPAdapter(max_retries=retries))
    return session


def env_bool(name, default=False):
    raw = os.environ.get(name)
    if raw is None:
        return default
    return str(raw).strip().lower() in ('1', 'true', 'yes', 'on')


def env_int(name, default):
    raw = os.environ.get(name)
    if raw is None:
        return default
    try:
        return int(raw)
    except Exception:
        return default


HTTP_SESSION = create_http_session()


def file_sha256(path, chunk_size=8192):
    try:
        h = hashlib.sha256()
        with open(path, 'rb') as f:
            while True:
                chunk = f.read(chunk_size)
                if not chunk:
                    break
                h.update(chunk)
        return h.hexdigest()
    except Exception:
        return None

# Calibration and lexicon paths
BASE_DIR = os.path.dirname(__file__)
CALIB_DIR = os.path.join(BASE_DIR, 'calibration')
os.makedirs(CALIB_DIR, exist_ok=True)
DEEPGRAM_LOG_DIR = os.path.join(BASE_DIR, 'deepgram_logs')
os.makedirs(DEEPGRAM_LOG_DIR, exist_ok=True)
SERVER_DEBUG_DIR = os.path.join(BASE_DIR, 'server_debug')
os.makedirs(SERVER_DEBUG_DIR, exist_ok=True)
LEXICON_PATH = os.path.join(BASE_DIR, 'emotion_lexicon.json')

# Initialize calibration database
CALIB_DB = CalibrationDB(os.path.join(CALIB_DIR, 'calibration.db'))

# Initialize configuration manager
CONFIG = ConfigManager()

# Initialize NPC response generator
try:
    llm_provider_name = CONFIG.get('llm_provider') or 'claude'
    print(f"[INFO] Initializing NPC generator with provider: {llm_provider_name}")
    NPC_GENERATOR = NPCResponseGenerator(
        llm_provider=llm_provider_name,
        config_file='llm_config.json'
    )
    provider_info = NPC_GENERATOR.get_provider_info()
    print(f"[OK] NPC generator initialized: {provider_info['provider_name']}")
    log_event('info', 'startup.npc_generator_initialized', 
             provider=provider_info['provider_name'],
             is_available=provider_info['is_available'])
except Exception as e:
    NPC_GENERATOR = None
    print(f"[ERROR] NPC generator initialization failed: {e}")
    log_event('error', 'startup.npc_generator_failed', error=str(e))

# Initialize LLM text scorer (pure LLM mode)
try:
    LLM_SCORER = LLMTextScorer(
        llm_provider=os.environ.get('LLM_PROVIDER', 'gemini'),
        config_file='llm_config.json'
    )
    print(f"[OK] LLM Text Scorer initialized")
except Exception as e:
    LLM_SCORER = None
    print(f"[WARN] LLM Text Scorer initialization failed: {e}")

# Keep HYBRID_SCORER for backward compatibility (fallback)
from hybrid_text_scorer import HybridTextScorer
HYBRID_SCORER = HybridTextScorer(
    lexicon_path=LEXICON_PATH,
    use_llm=env_bool('USE_LLM', False),  # Controlled by environment variable
    llm_provider=os.environ.get('LLM_PROVIDER', 'claude'),
    confidence_threshold=0.3
)

# Initialize conversation history tracker
HISTORY = ConversationHistory(
    db_path=os.path.join(CALIB_DIR, 'conversation_history.db')
)

EMOTION_LEXICON = {}
try:
    if os.path.exists(LEXICON_PATH):
        with open(LEXICON_PATH, 'r', encoding='utf-8') as fh:
            EMOTION_LEXICON = json.load(fh)
    else:
        default_lex = {"사랑": {"love": 3}, "사랑해": {"love": 4}, "좋아": {"joy": 2}, "좋다": {"joy": 2}}
        with open(LEXICON_PATH, 'w', encoding='utf-8') as fh:
            json.dump(default_lex, fh, ensure_ascii=False, indent=2)
        EMOTION_LEXICON = default_lex
except Exception:
    EMOTION_LEXICON = {}


def analyze_wav(path):
    # Skip audio analysis if requested (for testing)
    if env_bool('SKIP_AUDIO_ANALYSIS', True):
        log_event('info', 'analyze_wav.skipped', reason='SKIP_AUDIO_ANALYSIS=true')
        return {'jitter': 1.0, 'shimmer': 3.5, 'pitch_dev_percent': 10.0, 'hnr_db': 20.0, 'f0_mean': 0.0, 'extraction_status': 'skipped'}
    
    out = {'jitter': 1.0, 'shimmer': 3.5, 'pitch_dev_percent': 10.0, 'hnr_db': 20.0, 'f0_mean': 0.0, 'extraction_status': 'fallback'}
    if parselmouth is None or librosa is None or np is None or praat_call is None:
        app.logger.warning('Audio analysis libs missing; returning fallbacks')
        return out

    try:
        snd = parselmouth.Sound(path)
        pitch = snd.to_pitch()
        freqs = pitch.selected_array['frequency']
        f0 = freqs[freqs > 0]
        if len(f0) > 0:
            f0_mean = float(np.mean(f0))
            f0_std = float(np.std(f0))
            pitch_dev_percent = (f0_std / f0_mean) * 100.0 if f0_mean > 0 else 0.0
        else:
            f0_mean = 0.0
            pitch_dev_percent = 0.0

        extraction_method = 'praat'
        try:
            point_process = praat_call(snd, "To PointProcess (periodic, cc)", 75, 500)
            jitter_local = float(praat_call(point_process, "Get jitter (local)", 0.0001, 0.02, 1.3))
            shimmer_local = float(praat_call([snd, point_process], "Get shimmer (local)", 0.0001, 0.02, 1.3, 1.0, 0.0001, 0.02, 1.3, 1.3))
            jitter_pct = jitter_local * 100.0
            shimmer_pct = shimmer_local * 100.0
        except Exception as ex:
            extraction_method = 'librosa'
            try:
                y, sr = librosa.load(path, sr=None)
                hop = 512
                frame_length = 2048
                rms = librosa.feature.rms(y=y, frame_length=frame_length, hop_length=hop)[0]
                if np.mean(rms) > 0:
                    shimmer_pct = float(np.mean(np.abs(np.diff(rms))) / np.mean(rms) * 100.0)
                else:
                    shimmer_pct = 3.5
                if len(f0) > 1:
                    periods = 1.0 / f0
                    jitter_pct = float(np.mean(np.abs(np.diff(periods))) / np.mean(periods) * 100.0)
                else:
                    jitter_pct = 1.0
            except Exception:
                jitter_pct = 1.0
                shimmer_pct = 3.5

        try:
            harmonicity = praat_call(snd, "To Harmonicity (cc)", 0.01, 75, 0.1)
            hnr = float(praat_call(harmonicity, "Get mean", 0, 0))
        except Exception:
            hnr = 20.0

        jitter_val = round(jitter_pct, 3)
        shimmer_val = round(shimmer_pct, 3)
        pitch_val = round(pitch_dev_percent, 3)
        hnr_val = round(hnr, 3)
        f0_val = round(f0_mean, 2)
        
        out.update({
            'jitter': jitter_val,
            'shimmer': shimmer_val,
            'pitch_dev_percent': pitch_val,
            'hnr_db': hnr_val,
            'f0_mean': f0_val,
            'extraction_status': 'success',
            'extraction_method': extraction_method
        })
        
        log_event('debug', 'analyze_wav.success', path=path, method=extraction_method, 
                 jitter=jitter_val, shimmer=shimmer_val, pitch_dev_percent=pitch_val, 
                 hnr_db=hnr_val, f0_mean=f0_val)
        return out
    except Exception as e:
        app.logger.exception('analyze_wav failed')
        log_event('error', 'analyze_wav.exception', path=path, error=str(e))
        return out


def validate_wav(path):
    info = {'path': path, 'is_valid': False}
    if not os.path.exists(path):
        info['error'] = 'file_not_found'
        return False, info

    try:
        with wave.open(path, 'rb') as wf:
            channels = wf.getnchannels()
            sample_rate = wf.getframerate()
            sample_width = wf.getsampwidth()
            frames = wf.getnframes()
            duration_sec = round(float(frames) / float(sample_rate), 3) if sample_rate > 0 else 0.0
        info.update({
            'channels': channels,
            'sample_rate': sample_rate,
            'sample_width_bytes': sample_width,
            'frames': frames,
            'duration_sec': duration_sec,
        })
        if frames <= 0:
            info['error'] = 'empty_audio'
            return False, info
        if sample_width not in (1, 2, 3, 4):
            info['error'] = 'unsupported_sample_width'
            return False, info
        info['is_valid'] = True
        return True, info
    except Exception as ex:
        info['error'] = f'wave_parse_error: {type(ex).__name__}: {ex}'
        return False, info


def extract_transcript_candidates(payload):
    candidates = []
    confidences = []

    def walk(o):
        if isinstance(o, dict):
            for k, v in o.items():
                kl = str(k).lower()
                if kl == 'transcript' and isinstance(v, str):
                    candidates.append(v)
                elif kl == 'confidence' and isinstance(v, (int, float)):
                    confidences.append(float(v))
                else:
                    walk(v)
        elif isinstance(o, list):
            for item in o:
                walk(item)

    walk(payload)
    if not candidates:
        return '', None

    best = max((c for c in candidates if isinstance(c, str)), key=len, default='')
    best = best.strip()
    confidence = max(confidences) if confidences else None
    return best, confidence


def write_json_file(path, data):
    with open(path, 'w', encoding='utf-8') as fh:
        json.dump(data, fh, ensure_ascii=False, indent=2)


def write_debug_snapshot(debug_path, updates):
    try:
        if os.path.exists(debug_path):
            with open(debug_path, 'r', encoding='utf-8') as fh:
                cur = json.load(fh)
        else:
            cur = {}
    except Exception:
        cur = {}
    cur.update(updates)
    try:
        write_json_file(debug_path, cur)
    except Exception:
        app.logger.exception('Failed to write request debug snapshot')


def transcribe_deepgram(path, req_uuid, language='ko', model=None):
    result = {
        'transcript': '',
        'request_id': None,
        'log_file': None,
        'status': 'not_called',
        'error': None,
        'source': 'none',
        'confidence': None,
        'attempt_count': 0,
        'http_status': None,
        'sdk_tried': False,
        'rest_tried': False,
    }

    api_key = os.environ.get('DEEPGRAM_API_KEY')
    if not api_key:
        result['status'] = 'no_key'
        result['error'] = 'DEEPGRAM_API_KEY not set'
        log_event('warning', 'deepgram.skipped', req_uuid=req_uuid, reason=result['error'])
        return result

    is_valid_wav, wav_info = validate_wav(path)
    if not is_valid_wav:
        result['status'] = 'invalid_wav'
        result['error'] = wav_info.get('error')
        log_event('warning', 'deepgram.invalid_wav', req_uuid=req_uuid, wav_info=wav_info)
        return result

    env_model = os.environ.get('DEEPGRAM_MODEL')
    used_model = env_model or model or 'nova-3'
    mode = os.environ.get('DEEPGRAM_TRANSCRIBE_MODE', 'rest_first').strip().lower()
    # Backward-compatible override: set DEEPGRAM_SDK_ENABLED=1 to include SDK attempts.
    sdk_enabled = env_bool('DEEPGRAM_SDK_ENABLED', default=False)
    if mode in ('sdk_first', 'sdk_only'):
        sdk_enabled = True
    rest_enabled = mode not in ('sdk_only',)
    if mode == 'rest_only':
        sdk_enabled = False

    rest_connect_timeout = max(2, env_int('DEEPGRAM_REST_CONNECT_TIMEOUT_SEC', 5))
    rest_read_timeout = max(5, env_int('DEEPGRAM_REST_READ_TIMEOUT_SEC', 30))

    log_event(
        'info',
        'deepgram.start',
        req_uuid=req_uuid,
        path=path,
        model=used_model,
        wav_info=wav_info,
        mode=mode,
        sdk_enabled=sdk_enabled,
        rest_enabled=rest_enabled,
        rest_timeout_sec=[rest_connect_timeout, rest_read_timeout],
    )

    sdk_error = None
    sdk_empty = False

    # SDK is optional and disabled by default to avoid latency spikes from failed SDK calls.
    if sdk_enabled:
        sdk_started = int(time.time() * 1000)
        try:
            import asyncio
            deepgram_mod = __import__('deepgram')
            DeepgramClient = getattr(deepgram_mod, 'Deepgram')
            result['sdk_tried'] = True
            result['attempt_count'] += 1
            log_event('debug', 'deepgram.sdk.import_ok', req_uuid=req_uuid)

            async def _dg_prerecorded(buf, options):
                dg = DeepgramClient(api_key)
                transcription = getattr(dg, 'transcription', None)
                if transcription is None:
                    raise RuntimeError('Deepgram client has no transcription attribute')
                return await transcription.prerecorded({'buffer': buf, 'mimetype': 'audio/wav'}, options)

            with open(path, 'rb') as fh:
                audio_bytes = fh.read()
            log_event('debug', 'deepgram.sdk.audio_loaded', req_uuid=req_uuid, bytes=len(audio_bytes))

            options = {'model': used_model, 'language': language}
            punctuate = os.environ.get('DEEPGRAM_PUNCTUATE')
            if punctuate is not None:
                # SDK expects booleans or values; convert common env flags
                options['punctuate'] = punctuate.lower() in ('1', 'true', 'yes')

            try:
                sdk_resp = asyncio.run(_dg_prerecorded(audio_bytes, options))
                # write SDK raw response for debugging
                try:
                    req_id = sdk_resp.get('metadata', {}).get('request_id')
                except Exception:
                    req_id = None
                safe_id = req_id or f"sdk_noid_{int(time.time()*1000)}"
                log_base = os.path.join(DEEPGRAM_LOG_DIR, f"{req_uuid}__{safe_id}")
                sdk_log_file = log_base + '.body.json'
                try:
                    write_json_file(sdk_log_file, sdk_resp)
                    log_event('info', 'deepgram.sdk.log_written', req_uuid=req_uuid, file=os.path.basename(sdk_log_file), request_id=req_id)
                except Exception:
                    app.logger.exception('failed to write deepgram sdk raw response')

                best, confidence = extract_transcript_candidates(sdk_resp)
                result.update({
                    'request_id': req_id,
                    'log_file': os.path.basename(sdk_log_file),
                    'source': 'sdk',
                    'confidence': confidence,
                })
                if best:
                    result.update({'transcript': best, 'status': 'sdk_success'})
                    log_event('info', 'deepgram.sdk.success', req_uuid=req_uuid, request_id=req_id, transcript_len=len(best), confidence=confidence, sdk_elapsed_ms=int(time.time() * 1000) - sdk_started)
                    return result

                sdk_empty = True
                log_event('warning', 'deepgram.sdk.empty_transcript', req_uuid=req_uuid, request_id=req_id, sdk_elapsed_ms=int(time.time() * 1000) - sdk_started)
            except Exception as ex:
                sdk_error = f'{type(ex).__name__}: {ex}'
                log_event('warning', 'deepgram.sdk.exception', req_uuid=req_uuid, error=sdk_error, sdk_elapsed_ms=int(time.time() * 1000) - sdk_started)
        except Exception as ex:
            sdk_error = f'{type(ex).__name__}: {ex}'
            log_event('debug', 'deepgram.sdk.unavailable', req_uuid=req_uuid, error=sdk_error, sdk_elapsed_ms=int(time.time() * 1000) - sdk_started)
    else:
        log_event('debug', 'deepgram.sdk.disabled', req_uuid=req_uuid, mode=mode)

    if not rest_enabled:
        result['status'] = 'not_called'
        result['error'] = 'rest_disabled_by_mode'
        log_event('warning', 'deepgram.rest.disabled', req_uuid=req_uuid, mode=mode)
        return result

    # Fallback: use REST API (previous behavior)
    result['rest_tried'] = True
    result['attempt_count'] += 1
    params = {'language': language, 'model': used_model}
    # Do not set streaming-only `endpointing` param for pre-recorded requests.
    punctuate_env = os.environ.get('DEEPGRAM_PUNCTUATE')
    if punctuate_env is not None:
        params['punctuate'] = punctuate_env

    url = 'https://api.deepgram.com/v1/listen'
    headers = {'Authorization': f'Token {api_key}', 'Content-Type': 'audio/wav'}

    try:
        rest_started = int(time.time() * 1000)
        with open(path, 'rb') as fh:
            resp = HTTP_SESSION.post(
                url,
                headers=headers,
                params=params,
                data=fh,
                timeout=(rest_connect_timeout, rest_read_timeout),
            )
        # detailed logging for debugging
        try:
            hdrs = dict(resp.headers)
        except Exception:
            hdrs = {}
        # Deepgram returns 'dg-request-id'; older docs mention 'x-dg-request-id' as alias
        req_id = (
            hdrs.get('dg-request-id')
            or hdrs.get('x-dg-request-id')
            or hdrs.get('Dg-Request-Id')
            or hdrs.get('X-Dg-Request-Id')
        )
        result['request_id'] = req_id
        result['http_status'] = int(resp.status_code)
        # redact Authorization if present
        try:
            redacted = {k: (v if k.lower() != 'authorization' else 'REDACTED') for k, v in headers.items()}
        except Exception:
            redacted = {}
        log_event('info', 'deepgram.rest.response', req_uuid=req_uuid, status_code=resp.status_code, request_id=req_id, url=getattr(resp.request, 'url', None), rest_elapsed_ms=int(time.time() * 1000) - rest_started)
        log_event('debug', 'deepgram.rest.headers', req_uuid=req_uuid, request_headers=redacted, response_headers=hdrs)
        # Save raw response (headers + body) for debugging
        try:
            safe_id = req_id or f"noid_{int(time.time()*1000)}"
            log_base = os.path.join(DEEPGRAM_LOG_DIR, f"{req_uuid}__{safe_id}")
            rest_headers_file = log_base + '.headers.json'
            rest_body_file = log_base + '.body'
            # headers
            write_json_file(rest_headers_file, hdrs)
            # body (binary-safe)
            with open(rest_body_file, 'wb') as bb:
                bb.write(resp.content)
            result['log_file'] = os.path.basename(rest_body_file)
            log_event('info', 'deepgram.rest.log_written', req_uuid=req_uuid, headers_file=os.path.basename(rest_headers_file), body_file=os.path.basename(rest_body_file))

            # Also store decoded JSON when possible.
            decoded_json = None
            try:
                decoded_json = resp.json()
            except Exception:
                decoded_json = None

            if decoded_json is None and str(hdrs.get('content-encoding', '')).lower() == 'zstd':
                try:
                    zstd_mod = __import__('zstandard')
                    decompressed = zstd_mod.ZstdDecompressor().decompress(resp.content)
                    decoded_json = json.loads(decompressed.decode('utf-8', errors='replace'))
                except Exception as zex:
                    log_event('debug', 'deepgram.rest.zstd_decode_failed', req_uuid=req_uuid, error=f'{type(zex).__name__}: {zex}')

            if isinstance(decoded_json, dict):
                decoded_path = log_base + '.body.json'
                write_json_file(decoded_path, decoded_json)
                result['log_file'] = os.path.basename(decoded_path)
                log_event('debug', 'deepgram.rest.decoded_json_written', req_uuid=req_uuid, file=os.path.basename(decoded_path))
        except Exception:
            app.logger.exception('failed to write deepgram raw response')

        if resp.status_code != 200:
            snippet = (resp.text or '')[:240]
            result.update({
                'status': 'rest_http_error',
                'error': f'HTTP {resp.status_code}: {snippet}',
                'source': 'rest',
            })
            log_event('warning', 'deepgram.rest.non_200', req_uuid=req_uuid, request_id=req_id, status_code=resp.status_code, body_snippet=snippet)
            return result

        # Reuse already-decoded JSON (handles zstd compression); fall back to resp.json() for uncompressed
        if decoded_json is None:
            try:
                decoded_json = resp.json()
            except Exception as ex:
                result.update({
                    'status': 'rest_parse_error',
                    'error': f'json_parse_error: {type(ex).__name__}: {ex}',
                    'source': 'rest',
                })
                log_event('warning', 'deepgram.rest.parse_error', req_uuid=req_uuid, request_id=req_id,
                          error=result['error'],
                          content_encoding=hdrs.get('content-encoding', 'none'))
                return result

        if not isinstance(decoded_json, dict):
            result.update({'status': 'rest_parse_error', 'error': 'decoded_json_not_dict', 'source': 'rest'})
            log_event('warning', 'deepgram.rest.parse_error', req_uuid=req_uuid, request_id=req_id, error=result['error'])
            return result

        data = decoded_json
        log_event('debug', 'deepgram.rest.decoded_ok', req_uuid=req_uuid, request_id=req_id,
                  content_encoding=hdrs.get('content-encoding', 'none'))
        best, confidence = extract_transcript_candidates(data)
        result.update({'source': 'rest', 'confidence': confidence})
        if best:
            status = 'rest_success'
            if sdk_error:
                status = 'sdk_failed_rest_success'
            elif sdk_empty:
                status = 'sdk_empty_rest_success'
            result.update({'transcript': best, 'status': status})
            log_event('info', 'deepgram.rest.success', req_uuid=req_uuid, request_id=req_id, transcript_len=len(best), confidence=confidence, status=status)
            return result

        result['status'] = 'rest_empty_transcript'
        if sdk_empty:
            result['status'] = 'sdk_empty_rest_empty'
        elif sdk_error:
            result['status'] = 'sdk_failed_rest_empty'
        result['error'] = 'transcript_candidates_empty'
        log_event('warning', 'deepgram.rest.empty_transcript', req_uuid=req_uuid, request_id=req_id, status=result['status'])
        return result
    except Exception as ex:
        result.update({
            'status': 'rest_exception',
            'error': f'{type(ex).__name__}: {ex}',
            'source': 'rest',
        })
        log_event('error', 'deepgram.rest.exception', req_uuid=req_uuid, error=result['error'])
        return result


def compute_emotion_from_text_and_audio(transcript, metrics):
    import re
    emotions = ["love", "joy", "sadness", "anger", "fear", "neutral"]
    probs = {e: 0.01 for e in emotions}
    if transcript:
        txt = transcript.strip()
        tokens = [w for w in re.split(r"\W+", txt) if w]
        for tk in tokens:
            if tk in EMOTION_LEXICON:
                mapping = EMOTION_LEXICON[tk]
                for k, v in mapping.items():
                    probs[k] = probs.get(k, 0.01) + float(v)
    else:
        probs['neutral'] += 0.1

    try:
        f0 = float(metrics.get('f0_mean') or 0)
    except Exception:
        f0 = 0.0
    try:
        hnr = float(metrics.get('hnr_db') or 0)
    except Exception:
        hnr = 0.0
    try:
        jitter = float(metrics.get('jitter') or 0)
    except Exception:
        jitter = 0.0
    try:
        shimmer = float(metrics.get('shimmer') or 0)
    except Exception:
        shimmer = 0.0

    valence_audio = 0.0
    if hnr > 0:
        valence_audio += (min(hnr, 40) / 40.0) * 0.6
    if 90 < f0 < 250:
        valence_audio += 0.2
    audio_confidence = max(0.05, 1.0 - (jitter / 10.0 + shimmer / 50.0))

    total = sum(probs.values()) or 1.0
    for k in list(probs.keys()):
        probs[k] = probs[k] / total

    sorted_em = sorted(probs.items(), key=lambda x: x[1], reverse=True)
    top_em, top_score = sorted_em[0]

    pos = probs.get('love', 0) + probs.get('joy', 0)
    neg = probs.get('sadness', 0) + probs.get('anger', 0) + probs.get('fear', 0)
    valence = 0.5 + (pos - neg) * 0.5
    valence = max(0.0, min(1.0, valence * 0.9 + valence_audio * 0.1))

    arousal = 0.5 + (probs.get('anger', 0) - probs.get('sadness', 0)) * 0.5
    if f0 > 200:
        arousal += 0.1
    arousal = max(0.0, min(1.0, arousal))

    confidence = min(0.99, 0.2 + top_score * 0.6 + audio_confidence * 0.2)

    return {'emotion': top_em, 'valence': round(float(valence), 3), 'arousal': round(float(arousal), 3), 'confidence': round(float(confidence), 3), 'scores': {k: round(float(v), 3) for k, v in probs.items()}}


@app.route('/health')
def health():
    return jsonify({'status': 'ok'})


@app.route('/analyze', methods=['POST'])
def analyze():
    req_uuid = str(uuid.uuid4())
    started_ms = int(time.time() * 1000)
    # log basic request metadata for debugging when Unity uploads
    try:
        app.logger.debug('Incoming analyze request from %s, content-length=%s', request.remote_addr, request.content_length)
        # log request headers (filtered for safety)
        hdrs = {k: v for k, v in request.headers.items()}
        log_event('debug', 'analyze.request_headers', req_uuid=req_uuid, headers=hdrs)
        # log form keys and file keys
        log_event('debug', 'analyze.request_keys', req_uuid=req_uuid, form_keys=list(request.form.keys()), file_keys=list(request.files.keys()))
        log_event('info', 'analyze.START', req_uuid=req_uuid, timestamp_ms=started_ms)
    except Exception:
        app.logger.exception('Failed to log incoming request metadata')

    # create a per-request debug file to correlate Unity uploads with deepgram logs
    req_ts = int(time.time() * 1000)
    client_req_id = request.form.get('client_req_id')
    req_tag = f"req_{req_ts}_{req_uuid}"
    debug_path = os.path.join(SERVER_DEBUG_DIR, req_tag + '.json')
    dbg = {
        'req_uuid': req_uuid,
        'client_req_id': client_req_id,
        'timestamp': req_ts,
        'remote': request.remote_addr,
        'content_length': request.content_length,
        'headers': dict(request.headers),
        'form_keys': list(request.form.keys()),
        'files_keys': list(request.files.keys()),
    }
    try:
        write_json_file(debug_path, dbg)
        log_event('info', 'analyze.debug_file_created', req_uuid=req_uuid, file=os.path.basename(debug_path))
    except Exception:
        app.logger.exception('Failed to write request debug file')

    f = request.files.get('file')
    if f is None:
        log_event('warning', 'analyze.no_file', req_uuid=req_uuid)
        # echo back headers for client-side debugging
        try:
            return jsonify({'error': 'no file uploaded', 'req_uuid': req_uuid, 'headers': dict(request.headers), 'form_keys': list(request.form.keys())}), 400
        except Exception:
            return jsonify({'error': 'no file uploaded', 'req_uuid': req_uuid}), 400

    fd, path = tempfile.mkstemp(suffix='.wav')
    os.close(fd)
    f.save(path)
    try:
        fsize = os.path.getsize(path)
    except Exception:
        fsize = -1
    sha = file_sha256(path)
    log_event('info', 'analyze.file_saved', req_uuid=req_uuid, path=path, size=fsize, sha256=sha)
    # update debug file with saved file info
    write_debug_snapshot(debug_path, {'saved_path': path, 'saved_size': fsize, 'sha256': sha})

    try:
        provided_transcript = request.form.get('transcript')
        text_score = float(request.form.get('text_score', 0.5))
        repeat_count = int(request.form.get('repeat_count', 0))
    except Exception:
        provided_transcript = None
        text_score = 0.5
        repeat_count = 0

    metrics = analyze_wav(path)

    log_event('debug', 'analyze.metrics', req_uuid=req_uuid, metrics=metrics)

    transcript = provided_transcript
    deepgram_request_id = None
    deepgram_log_file = None
    deepgram_status = 'provided_by_client' if provided_transcript else 'not_called'
    deepgram_error = None
    deepgram_attempt_count = 0
    deepgram_confidence = None
    transcript_source = 'provided' if provided_transcript else 'none'
    deepgram_http_status = None
    if transcript is None or (isinstance(transcript, str) and len(transcript.strip()) == 0):
        log_event('debug', 'analyze.deepgram_invoked', req_uuid=req_uuid, path=path)
        dg = transcribe_deepgram(path, req_uuid=req_uuid)
        t = dg.get('transcript')
        deepgram_request_id = dg.get('request_id')
        deepgram_log_file = dg.get('log_file')
        deepgram_status = dg.get('status') or 'unknown'
        deepgram_error = dg.get('error')
        deepgram_attempt_count = int(dg.get('attempt_count') or 0)
        deepgram_confidence = dg.get('confidence')
        deepgram_http_status = dg.get('http_status')
        transcript_source = dg.get('source') or 'none'
        # record deepgram call outcome in debug file
        write_debug_snapshot(debug_path, {
            'deepgram_request_id': deepgram_request_id,
            'deepgram_log_file': deepgram_log_file,
            'deepgram_status': deepgram_status,
            'deepgram_error': deepgram_error,
            'deepgram_attempt_count': deepgram_attempt_count,
            'deepgram_confidence': deepgram_confidence,
            'deepgram_http_status': deepgram_http_status,
            'transcript_len': len(t or ''),
        })
        log_event('info', 'analyze.deepgram_result', req_uuid=req_uuid, deepgram_status=deepgram_status, deepgram_request_id=deepgram_request_id, transcript_len=len(t or ''), deepgram_log_file=deepgram_log_file)
        transcript = t or ''

    # compute text_score heuristic if not provided
    if not provided_transcript:
        # Use LLM text scorer (pure LLM mode)
        try:
            if LLM_SCORER:
                text_eval = LLM_SCORER.evaluate(
                    transcript,
                    question_context=None
                )
                text_score = text_eval['text_score']
                text_confidence = text_eval['confidence']
                emotion_detected = text_eval['emotion']
                response_type = text_eval['response_type']
                llm_used = True
                has_repetition = False
            else:
                # Fallback to hybrid scorer if LLM not available
                text_eval = HYBRID_SCORER.evaluate(
                    transcript,
                    question_context=None,
                    use_llm_if_uncertain=True,
                    track_repetition=True
                )
                text_score = text_eval['text_score']
                text_confidence = text_eval['confidence']
                emotion_detected = text_eval['emotion']
                response_type = text_eval['response_type']
                llm_used = text_eval['llm_used']
                has_repetition = text_eval['has_repetition']
        except Exception as e:
            print(f"[ERROR] LLM scorer failed: {e}, falling back to hybrid")
            text_eval = HYBRID_SCORER.evaluate(
                transcript,
                question_context=None,
                use_llm_if_uncertain=True,
                track_repetition=True
            )
            text_score = text_eval['text_score']
            text_confidence = text_eval['confidence']
            emotion_detected = text_eval['emotion']
            response_type = text_eval['response_type']
            llm_used = text_eval['llm_used']
            has_repetition = text_eval['has_repetition']
        
        log_event('info', 'analyze.text_score_computed',
                 req_uuid=req_uuid,
                 text_score=text_score,
                 response_type=response_type,
                 emotion=emotion_detected,
                 confidence=text_confidence,
                 llm_used=llm_used,
                 has_repetition=has_repetition)
    else:
        # Use provided score or default
        text_score = float(request.form.get('text_score', 0.5))
        text_confidence = 0.5
        emotion_detected = 'unknown'
        response_type = 'provided'
        llm_used = False
        has_repetition = False

    inputs = {
        'f0_mean': metrics.get('f0_mean'),
        'hnr_db': metrics.get('hnr_db'),
        'jitter': metrics.get('jitter'),
        'shimmer': metrics.get('shimmer'),
        'pitch_dev_percent': metrics.get('pitch_dev_percent'),
        'repeat_count': repeat_count,
        'text_score': text_score,
        'transcript': transcript
    }

    scores = evaluate_response(inputs)
    emotion = compute_emotion_from_text_and_audio(transcript, metrics)
    
    # Initialize session if needed and track conversation
    session_id = client_req_id or f"session_{req_uuid}"
    if session_id not in ['', None]:
        try:
            HISTORY.create_session(session_id, personality='romantic')
        except Exception:
            pass  # Session might already exist
    
    # Get conversation context for response generation
    conv_context = {}
    try:
        if session_id not in ['', None]:
            conv_context = HISTORY.get_context(session_id, limit=5)
    except Exception:
        conv_context = {}
    
    resp = {
        **scores,
        'req_uuid': req_uuid,
        'client_req_id': client_req_id,
        'calibration_used': False,
        'inputs': inputs,
        'emotion': emotion,
        'text_evaluation': {
            'text_score': text_score,
            'response_type': response_type if not provided_transcript else 'provided',
            'detected_emotion': emotion_detected if not provided_transcript else 'unknown',
            'confidence': text_confidence if not provided_transcript else 0.5,
            'llm_used': llm_used if not provided_transcript else False,
            'has_repetition': has_repetition if not provided_transcript else False,
        },
        'conversation_context': {
            'total_turns': conv_context.get('total_turns', 0),
            'avg_score': conv_context.get('avg_score', 0.5),
            'repeated_emotions': conv_context.get('repeated_emotions', []),
            'should_vary_response': len(conv_context.get('repeated_emotions', [])) > 0,
            'personality': conv_context.get('personality', 'romantic')
        },
        'deepgram_request_id': deepgram_request_id,
        'deepgram_called': bool(deepgram_attempt_count > 0),
        'deepgram_log_file': deepgram_log_file,
        'deepgram_status': deepgram_status,
        'deepgram_error': deepgram_error,
        'deepgram_attempt_count': deepgram_attempt_count,
        'deepgram_confidence': deepgram_confidence,
        'deepgram_http_status': deepgram_http_status,
        'transcript_source': transcript_source,
        'transcript_is_empty': len((transcript or '').strip()) == 0,
    }

    write_debug_snapshot(debug_path, {
        'response_summary': {
            'req_uuid': req_uuid,
            'client_req_id': client_req_id,
            'deepgram_status': deepgram_status,
            'deepgram_called': bool(deepgram_attempt_count > 0),
            'deepgram_request_id': deepgram_request_id,
            'deepgram_log_file': deepgram_log_file,
            'transcript_source': transcript_source,
            'transcript_is_empty': len((transcript or '').strip()) == 0,
            'latency_ms': int(time.time() * 1000) - started_ms,
        }
    })
    log_event('info', 'analyze.response_ready', req_uuid=req_uuid, deepgram_status=deepgram_status, transcript_len=len(transcript or ''), latency_ms=int(time.time() * 1000) - started_ms)

    # cleanup temp file
    try:
        os.remove(path)
    except Exception:
        pass

    return jsonify(resp)


@app.route('/calibrate', methods=['POST'])
def calibrate():
    """
    Calibration endpoint: accepts multiple WAV files for a device.
    Computes and stores baseline metrics (mean + std).
    
    Params:
        device_id: device identifier (required)
        notes: optional calibration notes
    Files:
        files: multiple WAV files
    
    Response:
        baseline: computed baseline with mean/std for each metric
        session_id: database session ID for reference
    """
    device_id = request.args.get('device_id', 'unknown')
    notes = request.args.get('notes', None)
    
    files = request.files.getlist('files')
    if not files:
        return jsonify({'error': 'no files uploaded', 'device_id': device_id}), 400

    # Start calibration session
    try:
        session_id = CALIB_DB.start_session(device_id, notes=notes)
    except Exception as e:
        log_event('error', 'calibrate.session_start_failed', device_id=device_id, error=str(e))
        return jsonify({'error': 'database error', 'details': str(e)}), 500

    metrics_list = []
    saved_paths = []
    
    for f in files:
        fd, path = tempfile.mkstemp(suffix='.wav')
        os.close(fd)
        f.save(path)
        saved_paths.append(path)
        
        metrics = analyze_wav(path)
        metrics_list.append(metrics)
        
        # Store in database
        try:
            CALIB_DB.add_metric(session_id, metrics, file_path=f.filename)
        except Exception as e:
            app.logger.exception('Failed to store metric')
    
    # Compute and store baseline
    try:
        baseline = CALIB_DB.compute_baseline(device_id)
    except Exception as e:
        log_event('error', 'calibrate.baseline_compute_failed', device_id=device_id, error=str(e))
        baseline = None

    # Old-style JSON file backup (for compatibility)
    avg = {}
    keys = ['f0_mean', 'jitter', 'shimmer', 'hnr_db', 'pitch_dev_percent']
    for k in keys:
        vals = [m.get(k, 0) or 0 for m in metrics_list]
        avg[k] = round(float(sum(vals)) / max(1, len(vals)), 4)

    calib_path = os.path.join(CALIB_DIR, f'{device_id}.json')
    try:
        with open(calib_path, 'w', encoding='utf-8') as fh:
            json.dump({
                'device_id': device_id,
                'metrics_average': avg,
                'session_id': session_id,
                'num_files': len(metrics_list)
            }, fh, ensure_ascii=False, indent=2)
    except Exception:
        app.logger.exception('Failed to write calibration file')

    # Cleanup temp files
    for p in saved_paths:
        try:
            os.remove(p)
        except Exception:
            pass

    log_event('info', 'calibrate.success', device_id=device_id, session_id=session_id, 
             num_files=len(metrics_list), baseline=baseline)

    return jsonify({
        'status': 'ok',
        'device_id': device_id,
        'session_id': session_id,
        'num_files': len(metrics_list),
        'baseline': baseline,
        'metrics_average': avg  # legacy field
    })


@app.route('/feedback', methods=['POST'])
def generate_feedback():
    """
    Generate varied NPC feedback based on player response and score.
    
    Tracks conversation and forces LLM when repetition is detected.
    
    Params (JSON body):
        session_id: player session (for tracking)
        transcript: player's response
        npc_response: NPC response to store
        score: authenticity score (0..1)
        audio_score: audio authenticity score
        emotion: detected emotion
        previous_feedback: optional previous NPC response to avoid
        force_llm: force LLM usage
    """
    try:
        data = request.get_json() or {}
        session_id = data.get('session_id', '')
        transcript = data.get('transcript', '')
        npc_response = data.get('npc_response', '')
        score = float(data.get('score', 0.5))
        audio_score = float(data.get('audio_score', score))
        emotion = data.get('emotion', 'neutral')
        previous_feedback = data.get('previous_feedback')
        force_llm = data.get('force_llm', False)
        
        # Allow empty transcript (silence case)
        # if not transcript:
        #     return jsonify({'error': 'transcript required'}), 400
        
        # Use "(silence)" if transcript is empty
        if not transcript or transcript.strip() == '':
            transcript = '(silence)'
            log_event('debug', 'feedback.empty_transcript_replaced', session_id=session_id)
        
        # Track conversation
        if session_id and session_id not in ['', None]:
            try:
                HISTORY.add_exchange(
                    session_id=session_id,
                    user=transcript,
                    emotion=emotion,
                    score=score,
                    npc=npc_response or 'thinking...'
                )
                
                # Check for repetition in conversation
                conv_context = HISTORY.get_context(session_id, limit=5)
                if len(conv_context.get('repeated_emotions', [])) > 0:
                    force_llm = True  # Force LLM if repetition detected
                    log_event('info', 'feedback.repetition_detected',
                             session_id=session_id,
                             repeated_emotions=conv_context.get('repeated_emotions', []))
            except Exception as e:
                app.logger.debug(f'Conversation tracking failed: {e}')
        
        # Generate feedback with possible LLM forcing
        feedback_result = HYBRID_SCORER.generate_feedback(
            transcript=transcript,
            score=score,
            emotion=emotion,
            previous_feedback=previous_feedback,
            force_llm=force_llm
        )
        
        return jsonify({
            'status': 'ok',
            'feedback': feedback_result['feedback'],
            'variation_type': feedback_result['variation_type'],
            'confidence': feedback_result['confidence'],
            'is_llm_available': HYBRID_SCORER.llm_provider is not None,
            'was_force_llm': force_llm,
            'emotion': emotion,
            'score': round(score, 3),
            'session_id': session_id
        })
    
    except Exception as e:
        app.logger.exception('feedback generation failed')
        return jsonify({
            'error': str(e),
            'status': 'error'
        }), 500


@app.route('/hybrid-status', methods=['GET'])
def hybrid_status():
    """Get hybrid scorer status and configuration."""
    try:
        status = HYBRID_SCORER.get_provider_status()
        return jsonify({
            'status': 'ok',
            'hybrid_scoring': status,
            'rules_scorer_available': True,
            'llm_provider': status.get('provider_type'),
            'llm_enabled': status.get('llm_enabled')
        })
    except Exception as e:
        app.logger.exception('hybrid status check failed')
        return jsonify({
            'error': str(e),
            'status': 'error'
        }), 500


@app.route('/conversation-status', methods=['GET'])
def conversation_status():
    """Get conversation status for a session."""
    try:
        session_id = request.args.get('session_id', '')
        
        if not session_id or session_id in ['', None]:
            return jsonify({'error': 'session_id required'}), 400
        
        context = HISTORY.get_context(session_id, limit=5)
        
        return jsonify({
            'status': 'ok',
            'session_id': session_id,
            'total_turns': context.get('total_turns', 0),
            'average_score': context.get('avg_score', 0.5),
            'personality': context.get('personality', 'romantic'),
            'repeated_emotions': context.get('repeated_emotions', []),
            'emotion_counts': context.get('emotion_counts', {}),
            'recent_exchanges': [
                {
                    'turn': e.get('turn'),
                    'user': e.get('user'),
                    'emotion': e.get('emotion'),
                    'score': e.get('score')
                }
                for e in context.get('exchanges', [])
            ]
        })
    
    except Exception as e:
        app.logger.exception('conversation status check failed')
        return jsonify({
            'error': str(e),
            'status': 'error'
        }), 500


@app.route('/config/llm', methods=['GET'])
def get_llm_config():
    """Get current LLM configuration."""
    try:
        config_data = {
            'llm_provider': CONFIG.get('llm_provider', 'claude'),
            'anthropic_api_key': CONFIG.get('anthropic_api_key', ''),
            'openai_api_key': CONFIG.get('openai_api_key', ''),
            'google_api_key': CONFIG.get('google_api_key', ''),
            'ollama_base_url': CONFIG.get('ollama_base_url', 'http://localhost:11434'),
            'ollama_model': CONFIG.get('ollama_model', 'mistral')
        }
        
        return jsonify({
            'status': 'ok',
            'config': config_data
        })
    except Exception as e:
        app.logger.exception('get llm config failed')
        return jsonify({
            'error': str(e),
            'status': 'error'
        }), 500


@app.route('/config/llm', methods=['POST'])
def set_llm_config():
    """Set LLM configuration."""
    try:
        data = request.get_json() or {}
        
        # Update configuration
        if 'llm_provider' in data:
            CONFIG.set('llm_provider', data['llm_provider'])
        if 'anthropic_api_key' in data:
            CONFIG.set('anthropic_api_key', data['anthropic_api_key'])
        if 'openai_api_key' in data:
            CONFIG.set('openai_api_key', data['openai_api_key'])
        if 'google_api_key' in data:
            CONFIG.set('google_api_key', data['google_api_key'])
        if 'ollama_base_url' in data:
            CONFIG.set('ollama_base_url', data['ollama_base_url'])
        if 'ollama_model' in data:
            CONFIG.set('ollama_model', data['ollama_model'])
        
        # Save configuration
        if CONFIG.save():
            saved_path = os.path.abspath(CONFIG.config_file)
            log_event('info', 'config.llm_updated', 
                     provider=data.get('llm_provider', 'unknown'),
                     saved_path=saved_path)
            
            return jsonify({
                'status': 'ok',
                'message': 'LLM configuration updated',
                'saved_path': saved_path
            })
        else:
            raise Exception("Failed to save configuration file")
    
    except Exception as e:
        app.logger.exception('set llm config failed')
        log_event('error', 'config.llm_save_failed', error=str(e))
        return jsonify({
            'error': str(e),
            'status': 'error',
            'error_type': type(e).__name__
        }), 500


@app.route('/config/llm/test', methods=['POST'])
def test_llm_config():
    """Test LLM configuration."""
    try:
        data = request.get_json() or {}
        provider_name = data.get('provider', 'claude')
        
        # Try to create provider
        from llm_provider import LLMProviderFactory
        provider = LLMProviderFactory.create(provider_name)
        
        if provider and provider.is_available():
            # Try to generate a simple response
            try:
                response = provider.generate(
                    system_prompt="You are a helpful assistant.",
                    user_message="Say 'OK' in one word."
                )
                
                log_event('info', 'config.llm_test_success', provider=provider_name)
                
                return jsonify({
                    'status': 'ok',
                    'message': 'LLM configuration test successful',
                    'provider': provider.get_name(),
                    'response': response
                })
            except Exception as e:
                log_event('warning', 'config.llm_test_generation_failed', provider=provider_name, error=str(e))
                return jsonify({
                    'status': 'error',
                    'message': f'LLM generation failed: {str(e)}',
                    'provider': provider_name
                }), 400
        else:
            log_event('warning', 'config.llm_test_unavailable', provider=provider_name)
            return jsonify({
                'status': 'error',
                'message': f'LLM provider {provider_name} not available',
                'provider': provider_name
            }), 400
    
    except Exception as e:
        app.logger.exception('test llm config failed')
        log_event('error', 'config.llm_test_exception', error=str(e))
        return jsonify({
            'error': str(e),
            'status': 'error'
        }), 500


if __name__ == '__main__':
    # enable more verbose logging for debugging
    app.logger.setLevel(logging.DEBUG)
    logging.getLogger('werkzeug').setLevel(logging.DEBUG)
    # log Deepgram env state for debugging
    try:
        ensure_writable_dir(DEEPGRAM_LOG_DIR)
        ensure_writable_dir(SERVER_DEBUG_DIR)
        ensure_writable_dir(CALIB_DIR)
        app.logger.debug('DEEPGRAM_API_KEY present: %s', bool(os.environ.get('DEEPGRAM_API_KEY')))
        app.logger.debug('DEEPGRAM_MODEL env: %s', os.environ.get('DEEPGRAM_MODEL'))
        app.logger.debug('DEEPGRAM_PUNCTUATE env: %s', os.environ.get('DEEPGRAM_PUNCTUATE'))
        log_event('info', 'startup.ready', deepgram_log_dir=DEEPGRAM_LOG_DIR, server_debug_dir=SERVER_DEBUG_DIR)
    except Exception:
        app.logger.exception('startup validation failed')
    app.run(host='127.0.0.1', port=5000, debug=False)
