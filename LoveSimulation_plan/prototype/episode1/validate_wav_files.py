"""
Simple WAV file validation (메타데이터 검증).

Usage:
  python validate_wav_files.py <wav_file> ...
"""
import sys
import os
import wave
import struct


def validate_wav(path):
    """Validate WAV file and extract metadata."""
    if not os.path.exists(path):
        print(f"✗ File not found: {path}")
        return False
    
    try:
        with wave.open(path, 'rb') as wf:
            channels = wf.getnchannels()
            sample_rate = wf.getframerate()
            sample_width = wf.getsampwidth()
            frames = wf.getnframes()
            duration_sec = frames / sample_rate if sample_rate > 0 else 0
            
            # Read first 100 frames to check data
            wf.rewind()
            frame_data = wf.readframes(min(100, frames))
            
            print(f"✓ {os.path.basename(path)}")
            print(f"    Channels: {channels}")
            print(f"    Sample Rate: {sample_rate}Hz")
            print(f"    Sample Width: {sample_width} bytes")
            print(f"    Frames: {frames}")
            print(f"    Duration: {duration_sec:.2f}s")
            print(f"    Data size: {len(frame_data)} bytes (first 100 frames)")
            
            # Check amplitude
            if sample_width == 2:  # 16-bit
                amp_data = struct.unpack(f'<{len(frame_data)//2}h', frame_data)
                max_amp = max(abs(v) for v in amp_data) if amp_data else 0
                norm_amp = max_amp / 32768
                print(f"    Max amplitude: {norm_amp:.3f} (normalized)")
            
            return True
    except Exception as e:
        print(f"✗ Error reading {path}: {e}")
        return False


def main():
    if len(sys.argv) < 2:
        print("Usage: python validate_wav_files.py <wav_file> [wav_file2] ...")
        sys.exit(1)
    
    print("=" * 60)
    print("WAV File Validation")
    print("=" * 60)
    print()
    
    all_valid = True
    for wav_path in sys.argv[1:]:
        if not validate_wav(wav_path):
            all_valid = False
        print()
    
    if all_valid:
        print("=" * 60)
        print("✅ All WAV files are valid and ready for use")
        print("=" * 60)
    
    return 0 if all_valid else 1


if __name__ == '__main__':
    sys.exit(main())
