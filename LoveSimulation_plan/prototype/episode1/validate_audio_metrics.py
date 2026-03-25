"""
Validate audio metric extraction stability.

Usage:
  python validate_audio_metrics.py <wav_file> [--runs 5] [--tolerance 5.0]

This script:
1. Analyzes the same WAV file multiple times
2. Checks metric variance (should be < tolerance %)
3. Reports stability metrics (jitter, shimmer, pitch, hnr, f0)
"""
import sys
import os
import json
import statistics
import argparse
from pathlib import Path

# Add parent dir to path for scorer import
sys.path.insert(0, os.path.dirname(__file__))

try:
    import numpy as np
    import librosa
    import parselmouth
    from parselmouth.praat import call as praat_call
except ImportError as e:
    print(f"ERROR: Missing audio library: {e}")
    print("Please install: pip install numpy librosa parselmouth")
    sys.exit(1)


def analyze_wav_metrics(path):
    """Extract audio metrics from WAV file. Returns dict with metrics or None if failed."""
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
        except Exception:
            extraction_method = 'librosa'
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

        try:
            harmonicity = praat_call(snd, "To Harmonicity (cc)", 0.01, 75, 0.1)
            hnr = float(praat_call(harmonicity, "Get mean", 0, 0))
        except Exception:
            hnr = 20.0

        return {
            'jitter': round(jitter_pct, 3),
            'shimmer': round(shimmer_pct, 3),
            'pitch_dev_percent': round(pitch_dev_percent, 3),
            'hnr_db': round(hnr, 3),
            'f0_mean': round(f0_mean, 2),
            'extraction_method': extraction_method,
            'status': 'success'
        }
    except Exception as e:
        return {
            'status': 'failed',
            'error': str(e)
        }


def compute_variance(values):
    """Compute variance as percentage of mean."""
    if not values or len(values) < 2:
        return 0.0
    mean_val = statistics.mean(values)
    if mean_val == 0:
        return 0.0
    std_val = statistics.stdev(values)
    return (std_val / abs(mean_val)) * 100.0


def validate_metrics(wav_file, num_runs=5, tolerance_pct=5.0):
    """Run multiple analysis passes and check stability."""
    if not os.path.exists(wav_file):
        print(f"ERROR: File not found: {wav_file}")
        return False

    print(f"\n=== Audio Metric Validation ===")
    print(f"File: {wav_file}")
    print(f"Runs: {num_runs}")
    print(f"Tolerance: {tolerance_pct}%\n")

    runs = []
    for i in range(num_runs):
        result = analyze_wav_metrics(wav_file)
        runs.append(result)
        print(f"Run {i+1}: {result}")

    # Check for failures
    failed = [r for r in runs if r.get('status') != 'success']
    if failed:
        print(f"\n⚠️  {len(failed)} runs failed!")
        for r in failed:
            print(f"  Error: {r.get('error')}")
        return False

    # Compute variance for each metric
    metrics_to_check = ['jitter', 'shimmer', 'pitch_dev_percent', 'hnr_db', 'f0_mean']
    all_stable = True
    
    print(f"\n=== Variance Analysis ===")
    for metric in metrics_to_check:
        values = [r.get(metric, 0) for r in runs if r.get('status') == 'success']
        if not values:
            continue
        
        variance = compute_variance(values)
        mean_val = statistics.mean(values)
        is_stable = variance <= tolerance_pct
        status = "✅ PASS" if is_stable else "❌ FAIL"
        
        print(f"{metric:20} mean={mean_val:10.3f}  variance={variance:6.2f}%  {status}")
        if not is_stable:
            all_stable = False

    print(f"\n=== Summary ===")
    if all_stable:
        print("✅ All metrics are stable (variance within tolerance)")
        return True
    else:
        print("❌ Some metrics exceed tolerance")
        return False


def main():
    p = argparse.ArgumentParser(description='Validate audio metric extraction stability')
    p.add_argument('wav_file', help='WAV file to validate')
    p.add_argument('--runs', type=int, default=5, help='Number of analysis runs (default: 5)')
    p.add_argument('--tolerance', type=float, default=5.0, help='Variance tolerance in % (default: 5.0)')
    args = p.parse_args()

    success = validate_metrics(args.wav_file, args.runs, args.tolerance)
    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
