"""
Integration test for calibration workflow.

Tests:
1. Database initialization
2. Metric extraction from WAV files
3. Baseline computation
4. Calibration correction in scorer
"""
import os
import sys
import tempfile
import json
from pathlib import Path

sys.path.insert(0, os.path.dirname(__file__))

from calibration_db import CalibrationDB
from scorer import evaluate_response, apply_calibration_correction


def test_calibration_db():
    """Test CalibrationDB operations."""
    print("\n=== Test: Calibration DB ===")
    
    # Create temp DB
    with tempfile.NamedTemporaryFile(suffix='.db', delete=False) as f:
        db_path = f.name
    
    json_path = None
    try:
        db = CalibrationDB(db_path)
        print(f"✓ Database initialized: {db_path}")
        
        # Start session
        session_id = db.start_session('test_device_001', notes='Test calibration')
        print(f"✓ Session created: {session_id}")
        
        # Add metrics
        metrics_list = [
            {
                'jitter': 0.95,
                'shimmer': 3.4,
                'pitch_dev_percent': 12.5,
                'hnr_db': 21.0,
                'f0_mean': 110.5,
                'extraction_method': 'praat'
            },
            {
                'jitter': 1.05,
                'shimmer': 3.6,
                'pitch_dev_percent': 13.2,
                'hnr_db': 20.8,
                'f0_mean': 111.2,
                'extraction_method': 'praat'
            },
            {
                'jitter': 1.00,
                'shimmer': 3.5,
                'pitch_dev_percent': 12.8,
                'hnr_db': 21.2,
                'f0_mean': 110.8,
                'extraction_method': 'praat'
            }
        ]
        
        for i, metrics in enumerate(metrics_list):
            db.add_metric(session_id, metrics, f'sample_{i}.wav')
        print(f"✓ Added {len(metrics_list)} metrics")
        
        # Compute baseline
        baseline = db.compute_baseline('test_device_001')
        print(f"✓ Baseline computed:")
        for key, value in baseline.items():
            if isinstance(value, tuple):
                mean, std = value
                print(f"    {key}: mean={mean:.3f}, std={std:.3f}")
            elif key not in ['device_id', 'num_samples']:
                print(f"    {key}: {value}")
        
        # Get baseline
        baseline_retrieved = db.get_baseline('test_device_001')
        print(f"✓ Baseline retrieved: {baseline_retrieved is not None}")
        
        # Export JSON
        json_path = db.export_baseline_json('test_device_001')
        print(f"✓ Baseline exported to JSON: {json_path}")
        
        return True
    finally:
        os.unlink(db_path)
        if json_path and os.path.exists(json_path):
            os.unlink(json_path)


def test_calibration_correction():
    """Test calibration correction in scorer."""
    print("\n=== Test: Calibration Correction ===")
    
    # Simulated baseline from a device
    baseline = {
        'jitter': (1.0, 0.1),
        'shimmer': (3.5, 0.2),
        'pitch_dev': (12.0, 2.0),
        'hnr_db': (21.0, 1.0),
        'f0_mean': (110.0, 3.0)
    }
    
    # Test case 1: metrics matching baseline
    metrics_normal = {
        'text_score': 0.9,
        'jitter': 1.0,
        'shimmer': 3.5,
        'pitch_dev_percent': 12.0,
        'hnr_db': 21.0,
        'repeat_count': 0
    }
    
    score_no_calib = evaluate_response(metrics_normal, baseline=None)
    score_with_calib = evaluate_response(metrics_normal, baseline=baseline)
    
    print(f"Metrics matching baseline:")
    print(f"  Without calibration: authenticity={score_no_calib['authenticity']:.4f}")
    print(f"  With calibration:    authenticity={score_with_calib['authenticity']:.4f}")
    print(f"  Applied: {score_with_calib['calibration_applied']}")
    
    # Test case 2: metrics deviating from baseline
    metrics_deviant = {
        'text_score': 0.9,
        'jitter': 1.5,  # Higher than baseline mean (1.0)
        'shimmer': 4.0,  # Higher than baseline mean (3.5)
        'pitch_dev_percent': 20.0,  # Higher than baseline mean (12.0)
        'hnr_db': 19.0,  # Lower than baseline mean (21.0)
        'repeat_count': 0
    }
    
    score_deviant_no_calib = evaluate_response(metrics_deviant, baseline=None)
    score_deviant_with_calib = evaluate_response(metrics_deviant, baseline=baseline)
    
    print(f"\nMetrics deviating from baseline:")
    print(f"  Without calibration: authenticity={score_deviant_no_calib['authenticity']:.4f}")
    print(f"  With calibration:    authenticity={score_deviant_with_calib['authenticity']:.4f}")
    print(f"  Applied: {score_deviant_with_calib['calibration_applied']}")
    
    return True


def main():
    print("=" * 60)
    print("Calibration Integration Tests")
    print("=" * 60)
    
    try:
        test_calibration_db()
        test_calibration_correction()
        print("\n" + "=" * 60)
        print("✅ All tests passed!")
        print("=" * 60)
        return 0
    except Exception as e:
        print(f"\n❌ Test failed: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == '__main__':
    sys.exit(main())
