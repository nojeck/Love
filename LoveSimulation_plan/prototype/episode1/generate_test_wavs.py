"""
Generate synthetic test WAV files for calibration testing.

Generates 3 environments:
1. Low noise: Clean speech with minimal noise
2. High noise: Speech with background noise
3. Distance: Speech with simulated distance attenuation

Usage:
  python generate_test_wavs.py [--output_dir ./test_wavs]

This creates:
  - test_wav_low_noise.wav (SNR ~40dB)
  - test_wav_high_noise.wav (SNR ~15dB)
  - test_wav_distance.wav (Attenuated speech simulating distance)
"""
import numpy as np
import os
import argparse
from pathlib import Path

try:
    import soundfile as sf
except ImportError:
    print("Installing soundfile...")
    import subprocess
    subprocess.check_call(['pip', 'install', 'soundfile', '-q'])
    import soundfile as sf


def generate_sine_wave(frequency, duration, sample_rate=16000, amplitude=0.3):
    """Generate a pure sine wave (formant-like).
    
    Simulates vocal resonance frequencies:
    - F1: ~700 Hz (mouth opening)
    - F2: ~1200 Hz (tongue position)
    - F3: ~2500 Hz (secondary resonance)
    """
    t = np.arange(int(sample_rate * duration)) / sample_rate
    # Combine three formants for speech-like quality
    signal = (
        amplitude * 0.5 * np.sin(2 * np.pi * 700 * t) +  # F1
        amplitude * 0.3 * np.sin(2 * np.pi * 1200 * t) +  # F2
        amplitude * 0.2 * np.sin(2 * np.pi * 2500 * t)    # F3
    )
    return signal


def generate_gaussian_noise(duration, sample_rate=16000, amplitude=0.05):
    """Generate white Gaussian noise."""
    num_samples = int(sample_rate * duration)
    return np.random.normal(0, amplitude, num_samples)


def generate_pink_noise(duration, sample_rate=16000, amplitude=0.1):
    """Generate pink noise (background/ambient noise).
    
    Simulates office environment, traffic, etc.
    """
    num_samples = int(sample_rate * duration)
    white = np.random.normal(0, amplitude, num_samples)
    
    # Simple pink noise filter (low-pass)
    b = np.array([0.049922035, -0.095993537, 0.050612699, -0.004408786])
    a = np.array([1, -2.494956002, 2.017265875, -0.522189400])
    
    try:
        from scipy import signal as sp_signal
        pink = sp_signal.lfilter(b, a, white)
    except ImportError:
        # Fallback: simple smoothing
        pink = np.convolve(white, np.hanning(100) / 100, mode='same')
    
    return pink


def apply_envelope(signal, attack=0.05, decay=0.1):
    """Apply ADSR-like envelope to make signal more speech-like."""
    duration = len(signal) / 16000
    t = np.arange(len(signal)) / 16000
    
    # Attack phase (first 50ms)
    attack_samples = int(0.05 * 16000)
    envelope = np.ones_like(signal, dtype=float)
    
    attack_slope = np.linspace(0, 1, attack_samples)
    envelope[:attack_samples] = attack_slope
    
    # Decay phase (last 100ms)
    decay_samples = int(0.1 * 16000)
    decay_slope = np.linspace(1, 0.3, decay_samples)
    envelope[-decay_samples:] = decay_slope
    
    return signal * envelope


def compute_snr(signal, noise):
    """Compute Signal-to-Noise Ratio in dB."""
    signal_power = np.mean(signal ** 2)
    noise_power = np.mean(noise ** 2)
    snr_db = 10 * np.log10(signal_power / noise_power + 1e-10)
    return snr_db


def generate_low_noise_wav(duration=3.0, sample_rate=16000):
    """
    Generate clean speech with minimal noise.
    SNR ~40dB (very clean environment)
    """
    print("Generating low noise WAV (SNR ~40dB)...")
    
    # Main speech signal
    speech = generate_sine_wave(1000, duration, sample_rate, amplitude=0.4)
    speech = apply_envelope(speech, attack=0.05, decay=0.1)
    
    # Minimal background noise
    noise = generate_gaussian_noise(duration, sample_rate, amplitude=0.01)
    
    # Combine
    signal = speech + noise
    
    # Normalize
    signal = signal / (np.max(np.abs(signal)) + 1e-6) * 0.95
    
    snr = compute_snr(speech, noise)
    print(f"  Computed SNR: {snr:.1f}dB")
    
    return signal.astype(np.float32), sample_rate, snr


def generate_high_noise_wav(duration=3.0, sample_rate=16000):
    """
    Generate speech with significant background noise.
    SNR ~15dB (noisy environment like office/traffic)
    """
    print("Generating high noise WAV (SNR ~15dB)...")
    
    # Main speech signal
    speech = generate_sine_wave(1000, duration, sample_rate, amplitude=0.3)
    speech = apply_envelope(speech, attack=0.05, decay=0.1)
    
    # Pink noise (ambient/background)
    noise = generate_pink_noise(duration, sample_rate, amplitude=0.15)
    
    # Combine
    signal = speech + noise
    
    # Normalize
    signal = signal / (np.max(np.abs(signal)) + 1e-6) * 0.95
    
    snr = compute_snr(speech, noise)
    print(f"  Computed SNR: {snr:.1f}dB")
    
    return signal.astype(np.float32), sample_rate, snr


def generate_distance_wav(duration=3.0, sample_rate=16000):
    """
    Generate speech with simulated distance attenuation.
    Simulates speaker at 1-2 meters distance with some room reverb.
    """
    print("Generating distance WAV (simulated 2m distance)...")
    
    # Main speech signal
    speech = generate_sine_wave(1000, duration, sample_rate, amplitude=0.2)
    speech = apply_envelope(speech, attack=0.05, decay=0.1)
    
    # Simulate distance attenuation (inverse square law)
    # Reduce amplitude over distance
    attenuation = 0.5  # 50% amplitude at 2m vs 1m
    speech = speech * attenuation
    
    # Add simple echo/reverb (early reflection)
    delay_samples = int(0.03 * sample_rate)  # 30ms delay
    echo = np.zeros_like(speech)
    echo[delay_samples:] = speech[:-delay_samples] * 0.3
    speech = speech + echo
    
    # Light ambient noise (room tone)
    noise = generate_gaussian_noise(duration, sample_rate, amplitude=0.03)
    
    # Combine
    signal = speech + noise
    
    # Normalize
    signal = signal / (np.max(np.abs(signal)) + 1e-6) * 0.95
    
    snr = compute_snr(speech, noise)
    print(f"  Computed SNR: {snr:.1f}dB")
    
    return signal.astype(np.float32), sample_rate, snr


def main():
    p = argparse.ArgumentParser(description='Generate synthetic test WAV files for calibration')
    p.add_argument('--output_dir', default='test_wavs', help='Output directory for WAV files')
    p.add_argument('--duration', type=float, default=3.0, help='Duration of each WAV in seconds')
    args = p.parse_args()
    
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    
    print(f"\n{'='*60}")
    print("Test WAV Generation")
    print(f"{'='*60}\n")
    
    # Generate 3 test environments
    print("Environment 1: Low Noise (clean)")
    signal1, sr, snr1 = generate_low_noise_wav(args.duration)
    path1 = output_dir / 'test_wav_low_noise.wav'
    sf.write(path1, signal1, sr)
    print(f"  Saved: {path1}\n")
    
    print("Environment 2: High Noise (noisy)")
    signal2, sr, snr2 = generate_high_noise_wav(args.duration)
    path2 = output_dir / 'test_wav_high_noise.wav'
    sf.write(path2, signal2, sr)
    print(f"  Saved: {path2}\n")
    
    print("Environment 3: Distance (simulated 2m)")
    signal3, sr, snr3 = generate_distance_wav(args.duration)
    path3 = output_dir / 'test_wav_distance.wav'
    sf.write(path3, signal3, sr)
    print(f"  Saved: {path3}\n")
    
    # Summary
    print(f"{'='*60}")
    print("Summary")
    print(f"{'='*60}")
    print(f"Output directory: {output_dir.absolute()}")
    print(f"Duration: {args.duration}s")
    print(f"Sample rate: {sr}Hz")
    print()
    print("Files created:")
    print(f"  1. test_wav_low_noise.wav    (SNR={snr1:6.1f}dB) - Clean speech")
    print(f"  2. test_wav_high_noise.wav   (SNR={snr2:6.1f}dB) - Noisy environment")
    print(f"  3. test_wav_distance.wav     (SNR={snr3:6.1f}dB) - Simulated distance")
    print()
    print("Next steps:")
    print("  1. Validate metrics stability:")
    print("     python validate_audio_metrics.py test_wavs/test_wav_low_noise.wav --runs 10")
    print("  2. Run calibration for each environment:")
    print("     curl -X POST http://127.0.0.1:5000/calibrate \\")
    print("       -F 'files=@test_wavs/test_wav_low_noise.wav' \\")
    print("       -G --data-urlencode 'device_id=low_noise_baseline'")
    print()


if __name__ == '__main__':
    main()
