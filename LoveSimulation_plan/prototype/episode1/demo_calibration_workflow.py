"""
Demo: Calibration workflow with test WAV files.

This script demonstrates the complete calibration workflow:
1. Load test WAV files
2. Simulate server-side metric extraction
3. Store baselines in SQLite
4. Compare score adjustments with/without calibration

Usage:
  python demo_calibration_workflow.py
"""
import sys
import os
import json
from pathlib import Path

sys.path.insert(0, os.path.dirname(__file__))

from calibration_db import CalibrationDB
from scorer import evaluate_response


def analyze_wav_simple(path):
    """Simple synthetic metric extraction (without Praat/Librosa).
    
    For demo purposes, we'll use mock metrics based on file characteristics.
    In production, this would use actual Praat/Librosa analysis.
    """
    import struct
    
    # Read WAV file
    with open(path, 'rb') as f:
        f.read(12)  # Skip RIFF header
        
        # Find 'fmt ' chunk
        while True:
            chunk_id = f.read(4)
            chunk_size = int.from_bytes(f.read(4), 'little')
            if chunk_id == b'fmt ':
                break
            f.seek(chunk_size, 1)
        
        # Parse format
        f.read(chunk_size)  # Skip fmt chunk
        
        # Find 'data' chunk
        while True:
            chunk_id = f.read(4)
            chunk_size = int.from_bytes(f.read(4), 'little')
            if chunk_id == b'data':
                break
            f.seek(chunk_size, 1)
        
        # Read audio data
        audio_bytes = f.read(chunk_size)
    
    # Extract amplitude statistics
    if len(audio_bytes) >= 4:
        samples = struct.unpack(f'<{len(audio_bytes)//2}h', audio_bytes)
        samples = [abs(s) / 32768 for s in samples[:1000]]  # First 1000 samples
        
        if samples:
            mean_amp = sum(samples) / len(samples)
            variance = sum((x - mean_amp) ** 2 for x in samples) / len(samples)
            
            # Estimate metrics from amplitude characteristics
            # (This is synthetic - real extraction uses Praat/Librosa)
            jitter = 0.8 + (1 - mean_amp) * 0.5  # Less amplitude → more jitter
            shimmer = 3.2 + variance * 5
            pitch_dev = 10.0 + variance * 20
            hnr_db = 22.0 - variance * 10
            f0_mean = 110.0
            
            return {
                'jitter': min(3.0, max(0.5, jitter)),
                'shimmer': min(5.0, max(2.0, shimmer)),
                'pitch_dev_percent': min(60.0, max(5.0, pitch_dev)),
                'hnr_db': min(25.0, max(10.0, hnr_db)),
                'f0_mean': f0_mean,
                'extraction_method': 'synthetic',
                'status': 'success'
            }
    
    return {
        'jitter': 1.0,
        'shimmer': 3.5,
        'pitch_dev_percent': 10.0,
        'hnr_db': 20.0,
        'f0_mean': 110.0,
        'extraction_method': 'synthetic',
        'status': 'fallback'
    }


def demo():
    print("=" * 70)
    print("CALIBRATION WORKFLOW DEMO")
    print("=" * 70)
    print()
    
    # Test WAV files
    test_wavs = [
        'test_wavs/test_wav_low_noise.wav',
        'test_wavs/test_wav_high_noise.wav',
        'test_wavs/test_wav_distance.wav'
    ]
    
    environments = [
        ('low_noise', 'Clean speech (SNR ~25dB)'),
        ('high_noise', 'Noisy environment (SNR ~20dB)'),
        ('distance', 'Simulated distance (SNR ~5dB)')
    ]
    
    # Create temporary DB for demo
    import tempfile
    with tempfile.NamedTemporaryFile(suffix='.db', delete=False) as f:
        db_path = f.name
    
    try:
        db = CalibrationDB(db_path)
        print(f"Database: {db_path}")
        print()
        
        baselines = {}
        
        # STEP 1: Create calibration baselines for each environment
        print("STEP 1: Calibration (creating baselines)")
        print("-" * 70)
        
        for (env_id, env_desc), wav_path in zip(environments, test_wavs):
            if not os.path.exists(wav_path):
                print(f"⚠️  Skipping {wav_path} (file not found)")
                continue
            
            print(f"\nEnvironment: {env_desc}")
            print(f"  WAV file: {wav_path}")
            
            # Start calibration session
            session_id = db.start_session(env_id, notes=env_desc)
            
            # Extract metrics from WAV (3 times to get better baseline)
            print("  Extracting metrics...")
            for i in range(3):
                metrics = analyze_wav_simple(wav_path)
                db.add_metric(session_id, metrics, f'{wav_path}_sample_{i}')
                print(f"    Run {i+1}: jitter={metrics['jitter']:.2f}, "
                      f"shimmer={metrics['shimmer']:.2f}, "
                      f"hnr_db={metrics['hnr_db']:.1f}")
            
            # Compute baseline
            baseline = db.compute_baseline(env_id)
            baselines[env_id] = baseline
            
            print(f"  Baseline computed:")
            print(f"    jitter: {baseline['jitter'][0]:.3f} ± {baseline['jitter'][1]:.3f}")
            print(f"    shimmer: {baseline['shimmer'][0]:.3f} ± {baseline['shimmer'][1]:.3f}")
            print(f"    hnr_db: {baseline['hnr_db'][0]:.3f} ± {baseline['hnr_db'][1]:.3f}")
        
        # STEP 2: Score comparison
        print()
        print("STEP 2: Score Comparison (with/without calibration)")
        print("-" * 70)
        
        # Test case: "normal" speech in each environment
        test_metrics = {
            'text_score': 0.85,
            'jitter': 1.2,
            'shimmer': 3.8,
            'pitch_dev_percent': 15.0,
            'hnr_db': 19.0,
            'repeat_count': 0
        }
        
        print(f"\nTest metrics: {test_metrics}")
        print()
        
        for env_id, env_desc in environments:
            if env_id not in baselines:
                continue
            
            baseline = baselines[env_id]
            
            # Score without calibration
            score_no_calib = evaluate_response(test_metrics, baseline=None)
            
            # Score with calibration
            score_with_calib = evaluate_response(test_metrics, baseline=baseline)
            
            # Difference
            diff = score_with_calib['authenticity'] - score_no_calib['authenticity']
            
            print(f"Environment: {env_desc}")
            print(f"  Authenticity (no calibration): {score_no_calib['authenticity']:.4f}")
            print(f"  Authenticity (with calib):     {score_with_calib['authenticity']:.4f}")
            print(f"  Adjustment: {diff:+.4f}")
            print()
        
        # STEP 3: Save for future use
        print("STEP 3: Baseline Export")
        print("-" * 70)
        
        for env_id in baselines.keys():
            json_path = db.export_baseline_json(env_id)
            print(f"  Exported {env_id} baseline to {json_path}")
        
        print()
        print("=" * 70)
        print("✅ CALIBRATION WORKFLOW COMPLETE")
        print("=" * 70)
        print()
        print("You can now:")
        print("1. Use baselines in real /analyze requests:")
        print("   score = evaluate_response(metrics, baseline=baseline)")
        print()
        print("2. Load previously saved baselines:")
        print("   baseline = db.get_baseline('device_id')")
        print()
        print("3. Run validation tests with actual audio libs:")
        print("   python validate_audio_metrics.py test_wavs/test_wav_low_noise.wav")
        
    finally:
        # Cleanup
        os.unlink(db_path)
        for env_id in baselines.keys():
            json_path = f'baseline_{env_id}.json'
            if os.path.exists(json_path):
                os.unlink(json_path)


if __name__ == '__main__':
    demo()
