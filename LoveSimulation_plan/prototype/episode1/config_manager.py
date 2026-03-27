"""
Configuration Manager for LLM Settings

Manages LLM provider configuration with priority:
1. Environment variables (highest priority)
2. Configuration file (llm_config.json)
3. Default values (lowest priority)

Usage:
    config = ConfigManager()
    provider = config.get('llm_provider')
    api_key = config.get('anthropic_api_key')
"""

import json
import os
from typing import Dict, Optional


class ConfigManager:
    """Configuration Manager for LLM Settings"""
    
    def __init__(self, config_file: str = 'llm_config.json'):
        """
        Initialize configuration manager
        
        Args:
            config_file: Path to configuration file
        """
        self.config_file = self._find_config_file(config_file)
        self.config = self._load_config()
    
    def _find_config_file(self, config_file: str) -> str:
        """
        Find configuration file with multiple search paths
        
        Search priority:
        1. Current directory (relative path)
        2. server.py directory
        3. Project root directory
        4. User home directory
        
        Args:
            config_file: Configuration file name
        
        Returns:
            Path to configuration file (existing or default location)
        """
        search_paths = [
            config_file,  # Relative path
            os.path.join(os.path.dirname(__file__), config_file),  # server.py location
            os.path.join(os.path.dirname(__file__), '..', '..', '..', config_file),  # Project root
            os.path.join(os.path.expanduser('~'), '.lovesim', config_file),  # Home directory
        ]
        
        # Try to find existing file
        for path in search_paths:
            abs_path = os.path.abspath(path)
            if os.path.exists(abs_path):
                print(f"[OK] Found config at: {abs_path}")
                return abs_path
        
        # If not found, use server.py directory as default
        default_path = os.path.abspath(search_paths[1])
        print(f"[INFO] Config file not found, will create at: {default_path}")
        return default_path
    
    def _load_config(self) -> Dict:
        """Load configuration from environment variables, file, or defaults"""
        
        # 1. Try environment variables first
        env_config = self._get_env_config()
        if env_config:
            print(f"[OK] Loaded config from environment variables")
            return env_config
        
        # 2. Try configuration file
        if os.path.exists(self.config_file):
            try:
                with open(self.config_file, 'r', encoding='utf-8') as f:
                    file_config = json.load(f)
                    print(f"[OK] Loaded config from {self.config_file}")
                    return file_config
            except Exception as e:
                print(f"[WARN] Failed to load config file: {e}")
        else:
            print(f"[INFO] Config file not found: {self.config_file}")
        
        # 3. Use default configuration
        print(f"[INFO] Using default configuration (llm_provider='claude')")
        return self._get_default_config()
    
    def _get_env_config(self) -> Optional[Dict]:
        """Get configuration from environment variables"""
        
        llm_provider = os.environ.get('LLM_PROVIDER')
        if not llm_provider:
            return None
        
        config = {'llm_provider': llm_provider}
        
        # Load provider-specific settings
        if llm_provider.lower() == 'claude':
            api_key = os.environ.get('ANTHROPIC_API_KEY')
            if api_key:
                config['anthropic_api_key'] = api_key
        
        elif llm_provider.lower() == 'openai':
            api_key = os.environ.get('OPENAI_API_KEY')
            if api_key:
                config['openai_api_key'] = api_key
        
        elif llm_provider.lower() == 'ollama':
            config['ollama_base_url'] = os.environ.get(
                'OLLAMA_BASE_URL', 'http://localhost:11434'
            )
            config['ollama_model'] = os.environ.get('OLLAMA_MODEL', 'mistral')
        
        elif llm_provider.lower() == 'gemini':
            api_key = os.environ.get('GOOGLE_API_KEY')
            if api_key:
                config['google_api_key'] = api_key
        
        elif llm_provider.lower() == 'openrouter':
            api_key = os.environ.get('OPENROUTER_API_KEY')
            if api_key:
                config['openrouter_api_key'] = api_key
            model = os.environ.get('OPENROUTER_MODEL')
            if model:
                config['openrouter_model'] = model
        
        return config if len(config) > 1 else None
    
    def _get_default_config(self) -> Dict:
        """Get default configuration"""
        return {
            'llm_provider': 'openrouter',
            'openrouter_api_key': '',
            'openrouter_model': 'deepseek/deepseek-chat',
            'anthropic_api_key': '',
            'openai_api_key': '',
            'ollama_base_url': 'http://localhost:11434',
            'ollama_model': 'mistral',
            'google_api_key': ''
        }
    
    def get(self, key: str, default=None):
        """
        Get configuration value
        
        Args:
            key: Configuration key
            default: Default value if key not found
        
        Returns:
            Configuration value
        """
        return self.config.get(key, default)
    
    def set(self, key: str, value):
        """
        Set configuration value
        
        Args:
            key: Configuration key
            value: Configuration value
        """
        self.config[key] = value
    
    def save(self) -> bool:
        """
        Save configuration to file
        
        Returns:
            True if successful, False otherwise
        """
        try:
            with open(self.config_file, 'w', encoding='utf-8') as f:
                json.dump(self.config, f, indent=2, ensure_ascii=False)
            print(f"[OK] Configuration saved to {self.config_file}")
            return True
        except Exception as e:
            print(f"[ERROR] Failed to save configuration: {e}")
            return False
    
    def to_dict(self) -> Dict:
        """
        Get configuration as dictionary
        
        Returns:
            Configuration dictionary
        """
        return self.config.copy()
    
    def to_dict_masked(self) -> Dict:
        """
        Get configuration as dictionary with API keys masked
        
        Returns:
            Configuration dictionary with masked API keys
        """
        config = self.config.copy()
        
        # Mask API keys
        for key in ['anthropic_api_key', 'openai_api_key', 'google_api_key', 'openrouter_api_key']:
            if key in config and config[key]:
                config[key] = self._mask_api_key(config[key])
        
        return config
    
    @staticmethod
    def _mask_api_key(api_key: str) -> str:
        """
        Mask API key for display
        
        Args:
            api_key: API key to mask
        
        Returns:
            Masked API key
        """
        if not api_key or len(api_key) < 8:
            return '***'
        return api_key[:4] + '*' * (len(api_key) - 8) + api_key[-4:]


if __name__ == '__main__':
    # Test
    print("=" * 60)
    print("CONFIG MANAGER TEST")
    print("=" * 60)
    
    config = ConfigManager()
    
    print("\nCurrent configuration:")
    for key, value in config.to_dict_masked().items():
        print(f"  {key}: {value}")
    
    print("\nTesting configuration operations:")
    config.set('llm_provider', 'openai')
    print(f"  Set llm_provider to: {config.get('llm_provider')}")
    
    print("\nSaving configuration...")
    if config.save():
        print("  Configuration saved successfully")
    else:
        print("  Failed to save configuration")
