from typing import Dict, Optional


def compute_memory_penalty(repeat_count: int) -> float:
    return min(0.15 * repeat_count, 0.45)


def compute_audio_score(jitter: float, shimmer: float, pitch_dev_percent: float, hnr_db: float) -> float:
    # Jitter scoring
    if jitter <= 1.0:
        jitter_s = 1.0
    elif jitter <= 1.5:
        jitter_s = 0.6
    else:
        jitter_s = 0.2

    # Shimmer scoring
    if shimmer <= 3.5:
        shimmer_s = 1.0
    elif shimmer <= 4.0:
        shimmer_s = 0.7
    else:
        shimmer_s = 0.2

    # Pitch deviation scoring
    if pitch_dev_percent <= 30.0:
        pitch_s = 1.0
    elif pitch_dev_percent <= 60.0:
        pitch_s = 0.6
    else:
        pitch_s = 0.3

    # HNR scoring
    if hnr_db >= 20.0:
        hnr_s = 1.0
    elif hnr_db >= 15.0:
        hnr_s = 0.6
    else:
        hnr_s = 0.3

    # Weighted combination
    score = jitter_s * 0.35 + shimmer_s * 0.25 + pitch_s * 0.2 + hnr_s * 0.2
    # Clamp
    return max(0.0, min(1.0, score))


def apply_calibration_correction(
    jitter: float,
    shimmer: float,
    pitch_dev_percent: float,
    hnr_db: float,
    baseline: Optional[Dict] = None
) -> Dict[str, float]:
    """
    Apply device calibration correction to audio metrics.
    
    If baseline is provided, adjusts metrics toward calibrated values.
    The correction reduces deviation from the device's baseline.
    
    baseline: {
        'jitter': (mean, std),
        'shimmer': (mean, std),
        'pitch_dev': (mean, std),
        'hnr_db': (mean, std),
        'f0_mean': (mean, std)
    }
    
    Returns:
        Corrected metrics dict with same keys
    """
    corrected = {
        'jitter': jitter,
        'shimmer': shimmer,
        'pitch_dev_percent': pitch_dev_percent,
        'hnr_db': hnr_db
    }
    
    if baseline is None:
        return corrected
    
    # Apply Z-score based correction: penalize deviation from baseline
    # corrected_value = original_value - (deviation from baseline) * 0.3
    # This smoothly adjusts the score without completely replacing it
    
    try:
        jitter_mean, jitter_std = baseline.get('jitter', (jitter, 0))
        if jitter_std > 0:
            deviation = (jitter - jitter_mean) / jitter_std
            corrected['jitter'] = jitter - deviation * 0.3 * jitter_std
    except Exception:
        pass
    
    try:
        shimmer_mean, shimmer_std = baseline.get('shimmer', (shimmer, 0))
        if shimmer_std > 0:
            deviation = (shimmer - shimmer_mean) / shimmer_std
            corrected['shimmer'] = shimmer - deviation * 0.3 * shimmer_std
    except Exception:
        pass
    
    try:
        pitch_mean, pitch_std = baseline.get('pitch_dev', (pitch_dev_percent, 0))
        if pitch_std > 0:
            deviation = (pitch_dev_percent - pitch_mean) / pitch_std
            corrected['pitch_dev_percent'] = pitch_dev_percent - deviation * 0.3 * pitch_std
    except Exception:
        pass
    
    try:
        hnr_mean, hnr_std = baseline.get('hnr_db', (hnr_db, 0))
        if hnr_std > 0:
            deviation = (hnr_db - hnr_mean) / hnr_std
            corrected['hnr_db'] = hnr_db + deviation * 0.3 * hnr_std  # Inverted: higher HNR is better
    except Exception:
        pass
    
    return corrected


def compute_authenticity(text_score: float, audio_score: float, memory_penalty: float) -> float:
    return 0.5 * text_score + 0.35 * audio_score + 0.15 * (1 - memory_penalty)


def evaluate_response(metrics: Dict, baseline: Optional[Dict] = None) -> Dict:
    """Helper that accepts a metrics dict and returns computed scores.
    metrics: {
      'text_score': float (0..1),
      'jitter': float (%),
      'shimmer': float (%),
      'pitch_dev_percent': float (%),
      'hnr_db': float,
      'repeat_count': int
    }
    baseline: optional calibration baseline (see apply_calibration_correction)
    """
    text_score = float(metrics.get('text_score', 0.0))
    jitter = float(metrics.get('jitter', 0.0))
    shimmer = float(metrics.get('shimmer', 0.0))
    pitch_dev = float(metrics.get('pitch_dev_percent', 0.0))
    hnr = float(metrics.get('hnr_db', 0.0))
    repeat = int(metrics.get('repeat_count', 0))

    # Apply calibration correction if baseline provided
    if baseline:
        corrected = apply_calibration_correction(jitter, shimmer, pitch_dev, hnr, baseline)
        jitter = corrected['jitter']
        shimmer = corrected['shimmer']
        pitch_dev = corrected['pitch_dev_percent']
        hnr = corrected['hnr_db']

    audio_score = compute_audio_score(jitter, shimmer, pitch_dev, hnr)
    memory_penalty = compute_memory_penalty(repeat)
    authenticity = compute_authenticity(text_score, audio_score, memory_penalty)

    return {
        'audio_score': round(audio_score, 4),
        'memory_penalty': round(memory_penalty, 4),
        'authenticity': round(authenticity, 4),
        'calibration_applied': baseline is not None
    }


if __name__ == '__main__':
    sample = {
        'text_score': 0.95,
        'jitter': 0.9,
        'shimmer': 3.0,
        'pitch_dev_percent': 10.0,
        'hnr_db': 25.0,
        'repeat_count': 0
    }
    print(evaluate_response(sample))
