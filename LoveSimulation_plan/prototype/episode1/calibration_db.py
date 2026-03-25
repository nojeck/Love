"""
Calibration database schema and utilities for storing device baselines.

Tables:
- calibration_sessions: Records of calibration runs (device + timestamp)
- audio_metrics: Extracted metrics from calibration WAV files
- device_baseline: Computed baseline (mean + std) per device
"""
import sqlite3
import json
import os
from datetime import datetime
from pathlib import Path


class CalibrationDB:
    def __init__(self, db_path=None):
        if db_path is None:
            base_dir = os.path.dirname(__file__)
            db_path = os.path.join(base_dir, 'calibration.db')
        
        self.db_path = db_path
        self._init_db()

    def _init_db(self):
        """Initialize database schema."""
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        # Calibration sessions
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS calibration_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                num_samples INTEGER DEFAULT 0,
                notes TEXT,
                UNIQUE(device_id, timestamp)
            )
        ''')
        
        # Audio metrics from calibration WAVs
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS audio_metrics (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL,
                file_path TEXT,
                jitter REAL,
                shimmer REAL,
                pitch_dev_percent REAL,
                hnr_db REAL,
                f0_mean REAL,
                extraction_method TEXT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY(session_id) REFERENCES calibration_sessions(id)
            )
        ''')
        
        # Computed device baselines
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS device_baseline (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT UNIQUE NOT NULL,
                jitter_mean REAL,
                jitter_std REAL,
                shimmer_mean REAL,
                shimmer_std REAL,
                pitch_dev_mean REAL,
                pitch_dev_std REAL,
                hnr_db_mean REAL,
                hnr_db_std REAL,
                f0_mean_value REAL,
                f0_mean_std REAL,
                num_samples INTEGER,
                last_updated DATETIME DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        conn.commit()
        conn.close()

    def start_session(self, device_id, notes=None):
        """Start a new calibration session."""
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        cursor.execute(
            'INSERT INTO calibration_sessions (device_id, notes) VALUES (?, ?)',
            (device_id, notes)
        )
        conn.commit()
        session_id = cursor.lastrowid
        conn.close()
        return session_id

    def add_metric(self, session_id, metrics, file_path=None):
        """
        Add extracted metric from a WAV file.
        metrics: dict with keys: jitter, shimmer, pitch_dev_percent, hnr_db, f0_mean, extraction_method
        """
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        cursor.execute('''
            INSERT INTO audio_metrics 
            (session_id, file_path, jitter, shimmer, pitch_dev_percent, hnr_db, f0_mean, extraction_method)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ''', (
            session_id,
            file_path,
            metrics.get('jitter'),
            metrics.get('shimmer'),
            metrics.get('pitch_dev_percent'),
            metrics.get('hnr_db'),
            metrics.get('f0_mean'),
            metrics.get('extraction_method', 'unknown')
        ))
        conn.commit()
        conn.close()

    def compute_baseline(self, device_id):
        """Compute mean and std for all metrics for a device."""
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            SELECT jitter, shimmer, pitch_dev_percent, hnr_db, f0_mean
            FROM audio_metrics am
            JOIN calibration_sessions cs ON am.session_id = cs.id
            WHERE cs.device_id = ?
        ''', (device_id,))
        
        rows = cursor.fetchall()
        if not rows:
            conn.close()
            return None

        jitters = [r[0] for r in rows if r[0] is not None]
        shimmers = [r[1] for r in rows if r[1] is not None]
        pitch_devs = [r[2] for r in rows if r[2] is not None]
        hnrs = [r[3] for r in rows if r[3] is not None]
        f0s = [r[4] for r in rows if r[4] is not None]

        def mean_std(values):
            if not values:
                return 0.0, 0.0
            import statistics
            mean_val = statistics.mean(values)
            std_val = statistics.stdev(values) if len(values) > 1 else 0.0
            return round(mean_val, 3), round(std_val, 3)

        jitter_mean, jitter_std = mean_std(jitters)
        shimmer_mean, shimmer_std = mean_std(shimmers)
        pitch_dev_mean, pitch_dev_std = mean_std(pitch_devs)
        hnr_db_mean, hnr_db_std = mean_std(hnrs)
        f0_mean_value, f0_mean_std = mean_std(f0s)

        cursor.execute('''
            INSERT OR REPLACE INTO device_baseline
            (device_id, jitter_mean, jitter_std, shimmer_mean, shimmer_std,
             pitch_dev_mean, pitch_dev_std, hnr_db_mean, hnr_db_std,
             f0_mean_value, f0_mean_std, num_samples)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ''', (
            device_id, jitter_mean, jitter_std, shimmer_mean, shimmer_std,
            pitch_dev_mean, pitch_dev_std, hnr_db_mean, hnr_db_std,
            f0_mean_value, f0_mean_std, len(rows)
        ))
        conn.commit()
        conn.close()
        
        return {
            'device_id': device_id,
            'jitter': (jitter_mean, jitter_std),
            'shimmer': (shimmer_mean, shimmer_std),
            'pitch_dev': (pitch_dev_mean, pitch_dev_std),
            'hnr_db': (hnr_db_mean, hnr_db_std),
            'f0_mean': (f0_mean_value, f0_mean_std),
            'num_samples': len(rows)
        }

    def get_baseline(self, device_id):
        """Retrieve baseline for a device."""
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        cursor.execute('SELECT * FROM device_baseline WHERE device_id = ?', (device_id,))
        row = cursor.fetchone()
        conn.close()
        
        if not row:
            return None
        
        return {
            'device_id': row[1],
            'jitter': (row[2], row[3]),
            'shimmer': (row[4], row[5]),
            'pitch_dev': (row[6], row[7]),
            'hnr_db': (row[8], row[9]),
            'f0_mean': (row[10], row[11]),
            'num_samples': row[12],
            'last_updated': row[13]
        }

    def export_baseline_json(self, device_id, output_path=None):
        """Export baseline as JSON for easy sharing/backup."""
        baseline = self.get_baseline(device_id)
        if not baseline:
            return None
        
        if output_path is None:
            output_path = f'baseline_{device_id}.json'
        
        with open(output_path, 'w') as f:
            json.dump(baseline, f, indent=2, default=str)
        
        return output_path


if __name__ == '__main__':
    # Test
    db = CalibrationDB()
    print(f"Database created/initialized at: {db.db_path}")
    
    # Example
    session_id = db.start_session('test_device_001', notes='Initial calibration')
    print(f"Session created: {session_id}")
    
    db.add_metric(session_id, {
        'jitter': 1.05,
        'shimmer': 3.6,
        'pitch_dev_percent': 15.2,
        'hnr_db': 21.5,
        'f0_mean': 110.3,
        'extraction_method': 'praat'
    }, 'sample.wav')
    
    baseline = db.compute_baseline('test_device_001')
    print(f"Baseline: {baseline}")
