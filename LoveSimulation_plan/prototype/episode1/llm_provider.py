"""
LLM Provider Plugin System

Supports multiple LLM providers:
- Claude (Anthropic)
- OpenAI (GPT-4, GPT-3.5)
- Ollama (Local)
- Google Gemini

Usage:
    from llm_provider import LLMProviderFactory
    
    provider = LLMProviderFactory.create('claude')
    response = provider.generate(system_prompt, user_message)
"""

from abc import ABC, abstractmethod
from typing import Optional
import os


class LLMProvider(ABC):
    """Base LLM Provider Interface"""
    
    @abstractmethod
    def generate(self, system_prompt: str, user_message: str) -> str:
        """
        Generate LLM response
        
        Args:
            system_prompt: System prompt
            user_message: User message
        
        Returns:
            Generated text
        """
        pass
    
    @abstractmethod
    def is_available(self) -> bool:
        """Check if provider is available (API key configured, etc.)"""
        pass
    
    @abstractmethod
    def get_name(self) -> str:
        """Get provider display name"""
        pass


class ClaudeProvider(LLMProvider):
    """Claude (Anthropic) Provider"""
    
    def __init__(self, api_key: Optional[str] = None):
        self.api_key = api_key or os.environ.get('ANTHROPIC_API_KEY')
        self.client = None
        
        if self.api_key:
            try:
                from anthropic import Anthropic
                self.client = Anthropic(api_key=self.api_key)
            except ImportError:
                print("Warning: anthropic package not installed. Install with: pip install anthropic")
    
    def generate(self, system_prompt: str, user_message: str) -> str:
        if not self.is_available():
            raise RuntimeError("Claude API key not configured")
        
        try:
            response = self.client.messages.create(
                model="claude-3-5-sonnet-20241022",
                max_tokens=150,
                system=system_prompt,
                messages=[{"role": "user", "content": user_message}]
            )
            return response.content[0].text.strip()
        except Exception as e:
            raise RuntimeError(f"Claude API error: {str(e)}")
    
    def is_available(self) -> bool:
        return self.client is not None
    
    def get_name(self) -> str:
        return "Claude (Anthropic)"


class OpenAIProvider(LLMProvider):
    """OpenAI (GPT) Provider"""
    
    def __init__(self, api_key: Optional[str] = None):
        self.api_key = api_key or os.environ.get('OPENAI_API_KEY')
        self.client = None
        
        if self.api_key:
            try:
                from openai import OpenAI
                self.client = OpenAI(api_key=self.api_key)
            except ImportError:
                print("Warning: openai package not installed. Install with: pip install openai")
    
    def generate(self, system_prompt: str, user_message: str) -> str:
        if not self.is_available():
            raise RuntimeError("OpenAI API key not configured")
        
        try:
            response = self.client.chat.completions.create(
                model="gpt-4-turbo",
                max_tokens=150,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_message}
                ]
            )
            return response.choices[0].message.content.strip()
        except Exception as e:
            raise RuntimeError(f"OpenAI API error: {str(e)}")
    
    def is_available(self) -> bool:
        return self.client is not None
    
    def get_name(self) -> str:
        return "OpenAI (GPT-4)"


class OllamaProvider(LLMProvider):
    """Ollama (Local) Provider"""
    
    def __init__(self, base_url: str = None, model: str = None):
        self.base_url = base_url or os.environ.get('OLLAMA_BASE_URL', 'http://localhost:11434')
        self.model = model or os.environ.get('OLLAMA_MODEL', 'mistral')
        self.client = None
        self._init_client()
    
    def _init_client(self):
        try:
            import requests
            # Test connection to Ollama server
            response = requests.get(f"{self.base_url}/api/tags", timeout=2)
            if response.status_code == 200:
                self.client = requests.Session()
        except Exception:
            self.client = None
    
    def generate(self, system_prompt: str, user_message: str) -> str:
        if not self.is_available():
            raise RuntimeError("Ollama server not available")
        
        try:
            import requests
            response = requests.post(
                f"{self.base_url}/api/generate",
                json={
                    "model": self.model,
                    "prompt": f"{system_prompt}\n\n{user_message}",
                    "stream": False
                },
                timeout=30
            )
            
            if response.status_code == 200:
                return response.json()['response'].strip()
            else:
                raise RuntimeError(f"Ollama error: {response.status_code}")
        except Exception as e:
            raise RuntimeError(f"Ollama error: {str(e)}")
    
    def is_available(self) -> bool:
        return self.client is not None
    
    def get_name(self) -> str:
        return f"Ollama ({self.model})"


class GeminiProvider(LLMProvider):
    """Google Gemini Provider"""
    
    def __init__(self, api_key: Optional[str] = None):
        self.api_key = api_key or os.environ.get('GOOGLE_API_KEY')
        self.client = None
        
        if self.api_key:
            try:
                import google.generativeai as genai
                genai.configure(api_key=self.api_key)
                self.client = genai.GenerativeModel('gemini-pro')
            except ImportError:
                print("Warning: google-generativeai package not installed. Install with: pip install google-generativeai")
    
    def generate(self, system_prompt: str, user_message: str) -> str:
        if not self.is_available():
            raise RuntimeError("Google API key not configured")
        
        try:
            response = self.client.generate_content(
                f"{system_prompt}\n\n{user_message}"
            )
            return response.text.strip()
        except Exception as e:
            raise RuntimeError(f"Gemini API error: {str(e)}")
    
    def is_available(self) -> bool:
        return self.client is not None
    
    def get_name(self) -> str:
        return "Google Gemini"


class LLMProviderFactory:
    """LLM Provider Factory"""
    
    _providers = {
        'claude': ClaudeProvider,
        'openai': OpenAIProvider,
        'ollama': OllamaProvider,
        'gemini': GeminiProvider,
    }
    
    @classmethod
    def create(cls, provider_name: str = None, api_key: str = None, **kwargs) -> Optional[LLMProvider]:
        """
        Create LLM provider
        
        Args:
            provider_name: Provider name ('claude', 'openai', 'ollama', 'gemini')
                          None uses 'LLM_PROVIDER' environment variable
            api_key: API key for the provider
                    None tries to load from config or environment
            **kwargs: Additional arguments for provider initialization
        
        Returns:
            LLMProvider instance or None if not available
        """
        if provider_name is None:
            provider_name = os.environ.get('LLM_PROVIDER', 'claude').lower()
        
        provider_class = cls._providers.get(provider_name)
        if not provider_class:
            print(f"Unknown provider: {provider_name}")
            return None
        
        try:
            # If API key not provided, try to load from ConfigManager
            if api_key is None:
                try:
                    from config_manager import ConfigManager
                    config = ConfigManager()
                    
                    # Map provider name to config key
                    api_key_map = {
                        'claude': 'anthropic_api_key',
                        'openai': 'openai_api_key',
                        'gemini': 'google_api_key',
                    }
                    
                    config_key = api_key_map.get(provider_name)
                    if config_key:
                        api_key = config.get(config_key)
                        if api_key:
                            print(f"[OK] Loaded {provider_name} API key from config")
                except Exception as e:
                    print(f"[DEBUG] Could not load API key from config: {e}")
            
            # Create provider with API key
            if provider_name == 'ollama':
                # Ollama uses base_url and model
                base_url = kwargs.get('base_url') or os.environ.get('OLLAMA_BASE_URL') or 'http://localhost:11434'
                model = kwargs.get('model') or os.environ.get('OLLAMA_MODEL') or 'mistral'
                provider = provider_class(base_url=base_url, model=model)
            else:
                # Other providers use api_key
                provider = provider_class(api_key=api_key)
            
            if provider.is_available():
                print(f"[OK] LLM Provider: {provider.get_name()}")
                return provider
            else:
                print(f"[WARN] {provider.get_name()} not available (API key missing?)")
                return None
        except Exception as e:
            print(f"[ERROR] Failed to initialize {provider_name}: {e}")
            return None
    
    @classmethod
    def list_available(cls) -> list:
        """Get list of available providers"""
        available = []
        for name in cls._providers.keys():
            provider = cls.create(name)
            if provider:
                available.append({
                    'name': name,
                    'display_name': provider.get_name()
                })
        return available
    
    @classmethod
    def register(cls, name: str, provider_class):
        """Register custom provider"""
        cls._providers[name] = provider_class


if __name__ == '__main__':
    # Test
    print("=" * 60)
    print("LLM PROVIDER TEST")
    print("=" * 60)
    
    print("\nAvailable providers:")
    for provider_info in LLMProviderFactory.list_available():
        print(f"  - {provider_info['name']}: {provider_info['display_name']}")
    
    print("\nTesting Claude provider:")
    provider = LLMProviderFactory.create('claude')
    if provider:
        try:
            response = provider.generate(
                system_prompt="You are a helpful assistant.",
                user_message="Say 'Hello' in one word."
            )
            print(f"Response: {response}")
        except Exception as e:
            print(f"Error: {e}")
    else:
        print("Claude provider not available")
