"""
Episode Manager - 에피소드 및 상황 관리

에피소드 진행, 상황 전환, NPC 대화 생성을 담당

Usage:
    manager = EpisodeManager()
    result = manager.start_episode('player_001', 1)
    dialogue = manager.get_npc_dialogue(1, 'sit_001')
"""

import json
import os
from typing import Dict, List, Optional, Any
from datetime import datetime
import sqlite3


class EpisodeManager:
    """에피소드 및 상황 관리 클래스"""
    
    def __init__(self, data_dir: str = None, db_path: str = None):
        """
        초기화
        
        Args:
            data_dir: 데이터 파일 디렉토리 경로
            db_path: SQLite DB 경로
        """
        if data_dir is None:
            # 기본 경로 설정
            current_dir = os.path.dirname(os.path.abspath(__file__))
            self.data_dir = os.path.join(current_dir, 'data')
        else:
            self.data_dir = data_dir
        
        if db_path is None:
            self.db_path = os.path.join(self.data_dir, 'episode_state.db')
        else:
            self.db_path = db_path
        
        # 캐시
        self.episodes_cache = {}
        self.npcs_cache = {}
        
        # DB 초기화
        self._init_db()
    
    def _init_db(self):
        """SQLite DB 초기화"""
        os.makedirs(os.path.dirname(self.db_path), exist_ok=True)
        
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        # 플레이어 상태 테이블
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS player_state (
                player_id TEXT PRIMARY KEY,
                episode_id INTEGER,
                situation_id TEXT,
                affection REAL DEFAULT 50.0,
                turn_count INTEGER DEFAULT 0,
                chaos_level REAL DEFAULT 0.0,
                created_at TIMESTAMP,
                updated_at TIMESTAMP
            )
        ''')
        
        # 대화 기록 테이블
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS dialogue_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player_id TEXT,
                episode_id INTEGER,
                situation_id TEXT,
                speaker TEXT,
                text TEXT,
                emotion TEXT,
                score REAL,
                timestamp TIMESTAMP
            )
        ''')
        
        conn.commit()
        conn.close()
    
    def _load_episode(self, episode_id: int) -> Dict:
        """에피소드 데이터 로드"""
        if episode_id in self.episodes_cache:
            return self.episodes_cache[episode_id]
        
        episode_path = os.path.join(self.data_dir, 'episodes', f'episode_{episode_id}.json')
        
        if not os.path.exists(episode_path):
            raise FileNotFoundError(f"Episode file not found: {episode_path}")
        
        with open(episode_path, 'r', encoding='utf-8') as f:
            episode_data = json.load(f)
        
        self.episodes_cache[episode_id] = episode_data
        return episode_data
    
    def _load_npc(self, npc_id: str) -> Dict:
        """NPC 데이터 로드"""
        if npc_id in self.npcs_cache:
            return self.npcs_cache[npc_id]
        
        npc_path = os.path.join(self.data_dir, 'npcs', f'npc_{npc_id}.json')
        
        if not os.path.exists(npc_path):
            # 기본 NPC 반환
            return self._get_default_npc()
        
        with open(npc_path, 'r', encoding='utf-8') as f:
            npc_data = json.load(f)
        
        self.npcs_cache[npc_id] = npc_data
        return npc_data
    
    def _get_default_npc(self) -> Dict:
        """기본 NPC 데이터 반환"""
        return {
            "npc_id": "default",
            "name": "여자친구",
            "personality": {"type": "romantic"},
            "base_mood": 0.5,
            "speech_patterns": {}
        }
    
    def _get_episode_info(self, episode_id: int) -> Dict:
        """에피소드 요약 정보 반환"""
        episode = self._load_episode(episode_id)
        return {
            "id": episode["episode_id"],
            "title": episode["episode_title"],
            "description": episode.get("description", ""),
            "target_affection": episode.get("target_affection", 100),
            "max_turns": episode.get("max_turns", 10)
        }
    
    def _get_npc_info(self, npc_id: str = "suji") -> Dict:
        """NPC 요약 정보 반환"""
        npc = self._load_npc(npc_id)
        return {
            "id": npc["npc_id"],
            "name": npc["name"],
            "mood": npc.get("base_mood", 0.5),
            "personality": npc.get("personality", {}).get("type", "romantic")
        }
    
    def start_episode(self, player_id: str, episode_id: int, npc_id: str = "suji") -> Dict:
        """
        에피소드 시작
        
        Args:
            player_id: 플레이어 ID
            episode_id: 에피소드 ID
            npc_id: NPC ID
            
        Returns:
            시작 결과 (에피소드, 상황, NPC 정보)
        """
        episode = self._load_episode(episode_id)
        npc = self._load_npc(npc_id)
        
        # 첫 상황 찾기 (trigger: episode_start)
        first_situation = None
        for situation in episode.get("situations", []):
            if situation.get("trigger") == "episode_start":
                first_situation = situation
                break
        
        if first_situation is None:
            first_situation = episode["situations"][0]
        
        # 플레이어 상태 초기화
        initial_affection = episode.get("initial_affection", 50.0)
        
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            INSERT OR REPLACE INTO player_state 
            (player_id, episode_id, situation_id, affection, turn_count, chaos_level, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ''', (
            player_id,
            episode_id,
            first_situation["situation_id"],
            initial_affection,
            0,
            0.0,
            datetime.now().isoformat(),
            datetime.now().isoformat()
        ))
        
        conn.commit()
        conn.close()
        
        return {
            "episode": self._get_episode_info(episode_id),
            "situation": {
                "id": first_situation["situation_id"],
                "text": first_situation["situation_text"],
                "context": first_situation.get("context", {}),
                "phase": first_situation.get("phase", "opening")
            },
            "npc": {
                "id": npc["npc_id"],
                "name": npc["name"],
                "mood": npc.get("base_mood", 0.5),
                "state": first_situation.get("context", {}).get("npc_state", "보통")
            },
            "next_action": "npc_dialogue"
        }
    
    def get_npc_dialogue(self, episode_id: int, situation_id: str, npc_id: str = "suji") -> Dict:
        """
        NPC 대화 조회
        
        Args:
            episode_id: 에피소드 ID
            situation_id: 상황 ID
            npc_id: NPC ID
            
        Returns:
            NPC 대화 데이터
        """
        episode = self._load_episode(episode_id)
        npc = self._load_npc(npc_id)
        
        # 상황 찾기
        situation = None
        for sit in episode.get("situations", []):
            if sit["situation_id"] == situation_id:
                situation = sit
                break
        
        if situation is None:
            raise ValueError(f"Situation not found: {situation_id}")
        
        npc_dialogue = situation.get("npc_dialogue", {})
        
        return {
            "dialogue": {
                "id": f"dlg_{situation_id}",
                "speaker": "npc",
                "speaker_name": npc["name"],
                "text": npc_dialogue.get("text", "..."),
                "emotion": npc_dialogue.get("emotion", "neutral"),
                "tone": npc_dialogue.get("tone", "보통"),
                "gesture": npc_dialogue.get("gesture", ""),
                "eye_contact": npc_dialogue.get("eye_contact", "")
            },
            "ui": {
                "show_dialogue_box": True,
                "auto_recording": True,
                "time_limit": 10,
                "max_recording_time": 30
            },
            "expected_keywords": situation.get("expected_keywords", []),
            "hint_text": situation.get("hint_text", "")
        }
    
    def get_player_state(self, player_id: str) -> Dict:
        """
        플레이어 상태 조회
        
        Args:
            player_id: 플레이어 ID
            
        Returns:
            플레이어 상태
        """
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            SELECT episode_id, situation_id, affection, turn_count, chaos_level
            FROM player_state WHERE player_id = ?
        ''', (player_id,))
        
        row = cursor.fetchone()
        conn.close()
        
        if row is None:
            return None
        
        return {
            "episode_id": row[0],
            "situation_id": row[1],
            "affection": row[2],
            "turn_count": row[3],
            "chaos_level": row[4]
        }
    
    def update_affection(self, player_id: str, change: float) -> Dict:
        """
        호감도 업데이트
        
        Args:
            player_id: 플레이어 ID
            change: 변화량
            
        Returns:
            업데이트된 상태
        """
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        # 현재 상태 조회
        cursor.execute('''
            SELECT affection, turn_count FROM player_state WHERE player_id = ?
        ''', (player_id,))
        
        row = cursor.fetchone()
        if row is None:
            conn.close()
            return None
        
        current_affection = row[0]
        turn_count = row[1]
        
        # 새 호감도 계산 (0-100 범위)
        new_affection = max(0.0, min(100.0, current_affection + change))
        
        # 업데이트
        cursor.execute('''
            UPDATE player_state 
            SET affection = ?, turn_count = ?, updated_at = ?
            WHERE player_id = ?
        ''', (new_affection, turn_count + 1, datetime.now().isoformat(), player_id))
        
        conn.commit()
        conn.close()
        
        return {
            "affection": new_affection,
            "change": change,
            "turn_count": turn_count + 1
        }
    
    def add_chaos(self, player_id: str, amount: float) -> Dict:
        """
        Chaos 레벨 추가
        
        Args:
            player_id: 플레이어 ID
            amount: 추가량
            
        Returns:
            업데이트된 Chaos 레벨
        """
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            SELECT chaos_level FROM player_state WHERE player_id = ?
        ''', (player_id,))
        
        row = cursor.fetchone()
        if row is None:
            conn.close()
            return None
        
        current_chaos = row[0]
        new_chaos = min(1.0, current_chaos + amount)
        
        cursor.execute('''
            UPDATE player_state SET chaos_level = ?, updated_at = ?
            WHERE player_id = ?
        ''', (new_chaos, datetime.now().isoformat(), player_id))
        
        conn.commit()
        conn.close()
        
        return {
            "chaos_level": new_chaos,
            "change": amount,
            "is_max": new_chaos >= 1.0
        }
    
    def check_game_status(self, player_id: str) -> Dict:
        """
        게임 상태 확인 (Clear/Fail/Continue)
        
        Args:
            player_id: 플레이어 ID
            
        Returns:
            게임 상태
        """
        state = self.get_player_state(player_id)
        if state is None:
            return {"status": "error", "message": "Player not found"}
        
        episode = self._load_episode(state["episode_id"])
        target = episode.get("target_affection", 100)
        
        if state["affection"] >= target:
            return {
                "status": "clear",
                "affection": state["affection"],
                "message": "호감도 달성! 클리어!"
            }
        elif state["affection"] <= 0:
            return {
                "status": "fail",
                "affection": state["affection"],
                "message": "호감도가 바닥났다..."
            }
        elif state["chaos_level"] >= 1.0:
            return {
                "status": "chaos",
                "chaos_level": state["chaos_level"],
                "message": "혼돈에 빠졌다..."
            }
        else:
            return {
                "status": "continue",
                "affection": state["affection"],
                "chaos_level": state["chaos_level"],
                "turn_count": state["turn_count"]
            }
    
    def get_next_situation(self, player_id: str, current_result: str = "neutral") -> Optional[Dict]:
        """
        다음 상황 결정
        
        Args:
            player_id: 플레이어 ID
            current_result: 현재 결과 (success/neutral/fail)
            
        Returns:
            다음 상황 데이터 또는 None
        """
        state = self.get_player_state(player_id)
        if state is None:
            return None
        
        episode = self._load_episode(state["episode_id"])
        
        # 다음 상황 찾기
        for situation in episode.get("situations", []):
            trigger = situation.get("trigger", "")
            
            # 트리거 조건 확인
            if trigger == "episode_start":
                continue
            
            if trigger == "first_response_success" and current_result == "success":
                return situation
            elif trigger == "first_response_fail" and current_result == "fail":
                return situation
            elif trigger == "affection_high" and state["affection"] >= 80:
                return situation
            elif trigger == "affection_low" and state["affection"] <= 20:
                return situation
        
        # 기본: 현재 상황 유지
        return None
    
    def save_dialogue(self, player_id: str, speaker: str, text: str, 
                      emotion: str = "neutral", score: float = None):
        """
        대화 기록 저장
        
        Args:
            player_id: 플레이어 ID
            speaker: 발화자 (player/npc)
            text: 대화 내용
            emotion: 감정
            score: 점수
        """
        state = self.get_player_state(player_id)
        if state is None:
            return
        
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            INSERT INTO dialogue_history 
            (player_id, episode_id, situation_id, speaker, text, emotion, score, timestamp)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ''', (
            player_id,
            state["episode_id"],
            state["situation_id"],
            speaker,
            text,
            emotion,
            score,
            datetime.now().isoformat()
        ))
        
        conn.commit()
        conn.close()
    
    def get_dialogue_history(self, player_id: str, limit: int = 20) -> List[Dict]:
        """
        대화 기록 조회
        
        Args:
            player_id: 플레이어 ID
            limit: 최대 개수
            
        Returns:
            대화 기록 리스트
        """
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            SELECT speaker, text, emotion, score, timestamp
            FROM dialogue_history 
            WHERE player_id = ?
            ORDER BY timestamp DESC
            LIMIT ?
        ''', (player_id, limit))
        
        rows = cursor.fetchall()
        conn.close()
        
        return [
            {
                "speaker": row[0],
                "text": row[1],
                "emotion": row[2],
                "score": row[3],
                "timestamp": row[4]
            }
            for row in reversed(rows)
        ]


# 테스트용
if __name__ == "__main__":
    manager = EpisodeManager()
    
    # 에피소드 시작 테스트
    result = manager.start_episode("test_player", 1)
    print("=== Episode Start ===")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    
    # NPC 대화 조회 테스트
    dialogue = manager.get_npc_dialogue(1, "sit_001")
    print("\n=== NPC Dialogue ===")
    print(json.dumps(dialogue, ensure_ascii=False, indent=2))
    
    # 상태 확인
    state = manager.get_player_state("test_player")
    print("\n=== Player State ===")
    print(json.dumps(state, ensure_ascii=False, indent=2))
