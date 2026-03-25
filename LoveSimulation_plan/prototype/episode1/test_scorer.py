import unittest
from scorer import compute_audio_score, compute_memory_penalty, compute_authenticity, evaluate_response


class TestScorer(unittest.TestCase):
    def test_success_case(self):
        metrics = {
            'text_score': 0.95,
            'jitter': 0.9,
            'shimmer': 3.0,
            'pitch_dev_percent': 10.0,
            'hnr_db': 25.0,
            'repeat_count': 0
        }
        out = evaluate_response(metrics)
        self.assertGreaterEqual(out['authenticity'], 0.70)

    def test_insult_case(self):
        metrics = {
            'text_score': 0.1,
            'jitter': 1.8,
            'shimmer': 4.5,
            'pitch_dev_percent': 45.0,
            'hnr_db': 12.0,
            'repeat_count': 1
        }
        out = evaluate_response(metrics)
        self.assertLess(out['authenticity'], 0.70)

    def test_memory_penalty_effect(self):
        base = {
            'text_score': 0.8,
            'jitter': 1.0,
            'shimmer': 3.5,
            'pitch_dev_percent': 15.0,
            'hnr_db': 22.0
        }
        out0 = evaluate_response({**base, 'repeat_count': 0})
        out2 = evaluate_response({**base, 'repeat_count': 2})
        self.assertGreater(out0['authenticity'], out2['authenticity'])


if __name__ == '__main__':
    unittest.main()
