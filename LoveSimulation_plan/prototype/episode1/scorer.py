from typing import Dict, Optional
import math


def compute_memory_penalty(repeat_count: int) -> float:
    return min(0.15 * repeat_count, 0.45)


def compute_audio_score_v2(
    jitter: float, 
    shimmer: float, 
    pitch_dev_percent: float, 
    hnr_db: float,
    baseline: Optional[Dict] = None
) -> Dict:
    """
    Compute normalized audio score with optional device calibration.
    
    If baseline is provided, uses z-score normalization against user's own baseline.
    If not, uses improved default thresholds.
    
    Returns:
        audio_score: 0.0-1.0
        calibration_used: bool
        component_scores: dict of individual scores
    """
    # Default thresholds (improved for better distribution)
    DEFAULTS = {
        'jitter': {'ideal': 1.0, 'acceptable': 3.0, 'max': 8.0},
        'shimmer': {'ideal': 3.5, 'acceptable': 8.0, 'max': 20.0},
        'pitch_dev': {'ideal': 15.0, 'acceptable': 40.0, 'max': 80.0},
        'hnr_db': {'ideal': 25.0, 'acceptable': 18.0, 'min': 10.0}
    }
    
    if baseline:
        # Z-score based normalization using user's baseline
        # Lower deviation from user's average = higher score
        j_mean, j_std = baseline.get('jitter', (1.0, 0.5))
        s_mean, s_std = baseline.get('shimmer', (3.5, 2.0))
        p_mean, p_std = baseline.get('pitch_dev', (15.0, 10.0))
        h_mean, h_std = baseline.get('hnr_db', (20.0, 5.0))
        
        # Calculate z-scores (how many std from mean)
        j_z = abs(jitter - j_mean) / max(j_std, 0.1) if j_std > 0 else 0
        s_z = abs(shimmer - s_mean) / max(s_std, 0.1) if s_std > 0 else 0
        p_z = abs(pitch_dev_percent - p_mean) / max(p_std, 0.1) if p_std > 0 else 0
        
        # HNR: higher is better, so invert the z-score
        h_z = max(0, (h_mean - hnr_db)) / max(h_std, 0.1) if h_std > 0 else 0
        
        # Convert z-scores to 0-1 range
        # z=0 -> 1.0, z=1 -> 0.75, z=2 -> 0.5, z=3 -> 0.25, z=4+ -> 0.0
        def z_to_score(z):
            return max(0.0, min(1.0, 1.0 - (z * 0.25)))
        
        jitter_score = z_to_score(j_z)
        shimmer_score = z_to_score(s_z)
        pitch_score = z_to_score(p_z)
        hnr_score = z_to_score(h_z)
        
        calibration_used = True
    else:
        # Use improved default scoring with smooth transitions
        # Jitter: lower is better
        if jitter <= DEFAULTS['jitter']['ideal']:
            jitter_score = 1.0
        elif jitter <= DEFAULTS['jitter']['acceptable']:
            # Smooth transition from 1.0 to 0.6
            jitter_score = 1.0 - 0.4 * ((jitter - DEFAULTS['jitter']['ideal']) / 
                                        (DEFAULTS['jitter']['acceptable'] - DEFAULTS['jitter']['ideal']))
        else:
            # Smooth transition from 0.6 to 0.2
            jitter_score = max(0.2, 0.6 - 0.4 * ((jitter - DEFAULTS['jitter']['acceptable']) / 
                                                  (DEFAULTS['jitter']['max'] - DEFAULTS['jitter']['acceptable'])))
        
        # Shimmer: lower is better
        if shimmer <= DEFAULTS['shimmer']['ideal']:
            shimmer_score = 1.0
        elif shimmer <= DEFAULTS['shimmer']['acceptable']:
            shimmer_score = 1.0 - 0.4 * ((shimmer - DEFAULTS['shimmer']['ideal']) / 
                                          (DEFAULTS['shimmer']['acceptable'] - DEFAULTS['shimmer']['ideal']))
        else:
            shimmer_score = max(0.2, 0.6 - 0.4 * ((shimmer - DEFAULTS['shimmer']['acceptable']) / 
                                                  (DEFAULTS['shimmer']['max'] - DEFAULTS['shimmer']['acceptable'])))
        
        # Pitch deviation: lower is better
        if pitch_dev_percent <= DEFAULTS['pitch_dev']['ideal']:
            pitch_score = 1.0
        elif pitch_dev_percent <= DEFAULTS['pitch_dev']['acceptable']:
            pitch_score = 1.0 - 0.4 * ((pitch_dev_percent - DEFAULTS['pitch_dev']['ideal']) / 
                                       (DEFAULTS['pitch_dev']['acceptable'] - DEFAULTS['pitch_dev']['ideal']))
        else:
            pitch_score = max(0.2, 0.6 - 0.4 * ((pitch_dev_percent - DEFAULTS['pitch_dev']['acceptable']) / 
                                                (DEFAULTS['pitch_dev']['max'] - DEFAULTS['pitch_dev']['acceptable'])))
        
        # HNR: higher is better
        if hnr_db >= DEFAULTS['hnr_db']['ideal']:
            hnr_score = 1.0
        elif hnr_db >= DEFAULTS['hnr_db']['acceptable']:
            hnr_score = 0.6 + 0.4 * ((hnr_db - DEFAULTS['hnr_db']['acceptable']) / 
                                     (DEFAULTS['hnr_db']['ideal'] - DEFAULTS['hnr_db']['acceptable']))
        else:
            hnr_score = max(0.2, 0.6 * ((hnr_db - DEFAULTS['hnr_db']['min']) / 
                                        (DEFAULTS['hnr_db']['acceptable'] - DEFAULTS['hnr_db']['min'])))
        
        calibration_used = False
    
    # Weighted combination (jitter and shimmer are most important for voice quality)
    audio_score = (
        jitter_score * 0.30 + 
        shimmer_score * 0.30 + 
        pitch_score * 0.25 + 
        hnr_score * 0.15
    )
    
    # Ensure minimum score for uncalibrated devices (give benefit of doubt)
    if not calibration_used:
        audio_score = max(0.3, audio_score)
    
    return {
        'audio_score': round(max(0.0, min(1.0, audio_score)), 4),
        'calibration_used': calibration_used,
        'component_scores': {
            'jitter': round(jitter_score, 3),
            'shimmer': round(shimmer_score, 3),
            'pitch': round(pitch_score, 3),
            'hnr': round(hnr_score, 3)
        }
    }


def compute_audio_score(jitter: float, shimmer: float, pitch_dev_percent: float, hnr_db: float) -> float:
    """Legacy function for backward compatibility."""
    result = compute_audio_score_v2(jitter, shimmer, pitch_dev_percent, hnr_db)
    return result['audio_score']


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
    """Compute overall authenticity score."""
    # Text is most important, but audio still matters
    # Memory penalty reduces score for repeated mistakes
    return 0.50 * text_score + 0.35 * audio_score + 0.15 * (1 - memory_penalty)


def evaluate_response(metrics: Dict, baseline: Optional[Dict] = None, device_id: str = None) -> Dict:
    """
    Evaluate response with optional device calibration.
    
    metrics: {
      'text_score': float (0..1),
      'jitter': float (%),
      'shimmer': float (%),
      'pitch_dev_percent': float (%),
      'hnr_db': float,
      'repeat_count': int
    }
    baseline: optional calibration baseline from CalibrationDB
    device_id: optional device ID for logging
    """
    text_score = float(metrics.get('text_score', 0.0))
    jitter = float(metrics.get('jitter', 0.0))
    shimmer = float(metrics.get('shimmer', 0.0))
    pitch_dev = float(metrics.get('pitch_dev_percent', 0.0))
    hnr = float(metrics.get('hnr_db', 0.0))
    repeat = int(metrics.get('repeat_count', 0))

    # Use new v2 scoring with baseline
    audio_result = compute_audio_score_v2(jitter, shimmer, pitch_dev, hnr, baseline)
    audio_score = audio_result['audio_score']
    calibration_used = audio_result['calibration_used']
    
    memory_penalty = compute_memory_penalty(repeat)
    authenticity = compute_authenticity(text_score, audio_score, memory_penalty)

    return {
        'audio_score': round(audio_score, 4),
        'memory_penalty': round(memory_penalty, 4),
        'authenticity': round(authenticity, 4),
        'calibration_applied': calibration_used,
        'calibration_used': calibration_used,
        'component_scores': audio_result.get('component_scores', {}),
        'device_id': device_id
    }


if __name__ == '__main__':
    # Test with different scenarios
    print("=== Audio Score Tests ===")
    
    # Good audio
    good = {'text_score': 0.9, 'jitter': 0.8, 'shimmer': 3.0, 'pitch_dev_percent': 12.0, 'hnr_db': 25.0, 'repeat_count': 0}
    print(f"Good audio: {evaluate_response(good)}")
    
    # Average audio
    avg = {'text_score': 0.7, 'jitter': 2.5, 'shimmer': 8.0, 'pitch_dev_percent': 35.0, 'hnr_db': 18.0, 'repeat_count': 0}
    print(f"Average audio: {evaluate_response(avg)}")
    
    # Poor audio
    poor = {'text_score': 0.5, 'jitter': 6.0, 'shimmer': 15.0, 'pitch_dev_percent': 60.0, 'hnr_db': 12.0, 'repeat_count': 0}
    print(f"Poor audio: {evaluate_response(poor)}")
    
    # With baseline (calibrated device)
    baseline = {
        'jitter': (3.0, 1.5),
        'shimmer': (10.0, 4.0),
        'pitch_dev': (40.0, 15.0),
        'hnr_db': (18.0, 3.0)
    }
    calib = {'text_score': 0.9, 'jitter': 3.2, 'shimmer': 11.0, 'pitch_dev_percent': 38.0, 'hnr_db': 19.0, 'repeat_count': 0}
    print(f"With calibration: {evaluate_response(calib, baseline=baseline)}")
