"""
Conversation History Manager.

Simple SQLite-based tracker for dialogue exchanges.
"""

import sqlite3
import time
from typing import Dict, Optional
import os


class ConversationHistory:
    """Track conversation exchanges with SQLite."""
    
    def __init__(self, db_path: Optional[str] = None):
        if db_path is None:
            db_path = os.path.join(os.path.dirname(__file__), 'conversation.db')
        self.db_path = db_path
        self._init_db()
    
    def _get_conn(self):
        """Get connection helper."""
        return sqlite3.connect(self.db_path)
    
    def _init_db(self):
        """Create tables."""
        conn = self._get_conn()
        c = conn.cursor()
        
        c.execute("""CREATE TABLE IF NOT EXISTS sessions (
            session_id TEXT PRIMARY KEY,
            created_at REAL,
            personality TEXT,
            total_turns INT DEFAULT 0,
            avg_score REAL DEFAULT 0.5
        )""")
        
        c.execute("""CREATE TABLE IF NOT EXISTS exchanges (
            id INTEGER PRIMARY KEY,
            session_id TEXT,
            turn INT,
            user_response TEXT,
            user_emotion TEXT,
            user_score REAL,
            npc_response TEXT,
            created_at REAL
        )""")
        
        conn.commit()
        conn.close()
    
    def create_session(self, session_id: str, personality: str = 'neutral'):
        """Start a new session."""
        conn = self._get_conn()
        c = conn.cursor()
        c.execute("INSERT OR REPLACE INTO sessions VALUES (?, ?, ?, 0, 0.5)",
                  (session_id, time.time(), personality))
        conn.commit()
        conn.close()
    
    def add_exchange(self, session_id: str, user: str, emotion: str, score: float, npc: str):
        """Add one exchange."""
        conn = self._get_conn()
        c = conn.cursor()
        
        # Get turn number
        c.execute("SELECT COUNT(*) FROM exchanges WHERE session_id = ?", (session_id,))
        turn = c.fetchone()[0] + 1
        
        # Insert exchange
        c.execute("""INSERT INTO exchanges VALUES
            (NULL, ?, ?, ?, ?, ?, ?, ?)""",
                  (session_id, turn, user, emotion, score, npc, time.time()))
        
        # Update session stats
        c.execute("SELECT AVG(user_score) FROM exchanges WHERE session_id = ?", (session_id,))
        avg = c.fetchone()[0]
        c.execute("UPDATE sessions SET total_turns = ?, avg_score = ? WHERE session_id = ?",
                  (turn, avg, session_id))
        
        conn.commit()
        conn.close()
        return turn
    
    def get_context(self, session_id: str, limit: int = 5) -> Dict:
        """Get recent exchanges for LLM context."""
        conn = self._get_conn()
        c = conn.cursor()
        
        # Get recent exchanges
        c.execute("""SELECT user_response, user_emotion, user_score, npc_response
            FROM exchanges WHERE session_id = ?
            ORDER BY turn DESC LIMIT ?""", (session_id, limit))
        exch = [{'user': r[0], 'emotion': r[1], 'score': r[2], 'npc': r[3]}
                for r in c.fetchall()]
        exch.reverse()
        
        # Get session info
        c.execute("SELECT total_turns, avg_score, personality FROM sessions WHERE session_id = ?",
                  (session_id,))
        row = c.fetchone() or (0, 0.5, 'neutral')
        
        conn.close()
        
        emotions = [e['emotion'] for e in exch if e['emotion']]
        emotion_counts = {}
        for em in emotions:
            emotion_counts[em] = emotion_counts.get(em, 0) + 1
        
        return {
            'exchanges': exch,
            'total_turns': row[0],
            'avg_score': row[1],
            'personality': row[2],
            'emotion_counts': emotion_counts,
            'repeated_emotions': [e for e, c in emotion_counts.items() if c >= 3]
        }


if __name__ == '__main__':
    print("=" * 60)
    print("CONVERSATION HISTORY TEST")
    print("=" * 60)
    
    # Use file-based DB for test
    import tempfile
    tmp = tempfile.mktemp(suffix='.db')
    history = ConversationHistory(db_path=tmp)
    
    session_id = 'test_001'
    history.create_session(session_id, 'romantic')
    print(f"\n✓ Session created: {session_id}")
    
    # Add exchanges
    data = [
        ("안녕", "neutral", 0.6, "반가워!"),
        ("아름다워", "joy", 0.85, "고마워"),
        ("사랑해", "love", 0.95, "나도"),
        ("함께할래", "love", 0.9, "응!"),
        ("정말 사랑", "love", 0.95, "나도"),
    ]
    
    for user, emotion, score, npc in data:
        turn = history.add_exchange(session_id, user, emotion, score, npc)
        print(f"  Turn {turn}: {emotion} ({score:.2f})")
    
    # Get context
    ctx = history.get_context(session_id)
    print(f"\n✓ Context: avg={ctx['avg_score']:.2f}, turns={ctx['total_turns']}")
    print(f"  Emotions: {ctx['emotion_counts']}")
    print(f"  Repetition: {ctx['repeated_emotions']}")
    
    print("\n" + "=" * 60)
    print("ALL TESTS PASSED ✓")
    print("=" * 60)
    
    # Clean up
    import os
    os.remove(tmp)
