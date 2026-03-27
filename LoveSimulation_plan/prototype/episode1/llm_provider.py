"""
LLM Provider Plugin System

Supports multiple LLM providers:
- Claude (Anthropic)
- OpenAI (GPT-4, GPT-3.5)
- Ollama (Local)
- Google Gemini
- OpenRouter (Multi-model gateway)

Usage:
    from llm_provider import LLMProviderFactory
    
    provider = LLMProviderFactory.create('openrouter')
    response = provider.generate(system_prompt, user_message)
"""

from abc import ABC, abstractmethod
from typing import Optional, Dict, Any
from dataclasses import dataclass
import os
import time
import json
import logging

# Configure logging
logger = logging.getLogger(__name__)


@dataclass
class TokenUsage:
    """Token usage tracking"""
    prompt_tokens: int = 0
    completion_tokens: int = 0
    total_tokens: int = 0
    estimated_cost_usd: float = 0.0
    model: str = ""
    timestamp: float = 0.0


class CostTracker:
    """Track LLM API costs"""
    
    # Pricing per 1M tokens (as of 2026-03, OpenRouter prices)
    PRICING = {
        'deepseek/deepseek-chat': {'input': 0.28, 'output': 0.42},
        'deepseek/deepseek-r1': {'input': 0.28, 'output': 0.42},  # Reasoning model
        'deepseek/deepseek-v3.2': {'input': 0.28, 'output': 0.42},
        'google/gemini-2.5-flash-lite-preview-06-17': {'input': 0.25, 'output': 1.00},
        'google/gemini-2.5-flash-preview-05-20': {'input': 0.50, 'output': 2.00},
        'anthropic/claude-4.6-sonnet': {'input': 3.00, 'output': 15.00},
        'anthropic/claude-4.5-haiku': {'input': 1.00, 'output': 5.00},
        'openai/gpt-5.4-nano': {'input': 0.20, 'output': 1.25},
        'openai/gpt-5.4-mini': {'input': 0.75, 'output': 4.50},
        'openai/gpt-5.4': {'input': 2.50, 'output': 15.00},
    }
    
    def __init__(self, log_file: str = None):
        self.total_usage = TokenUsage()
        self.session_usage = TokenUsage()
        self.usages: list = []
        self.log_file = log_file or os.environ.get('LLM_COST_LOG', 'llm_costs.jsonl')
    
    def calculate_cost(self, model: str, prompt_tokens: int, completion_tokens: int) -> float:
        """Calculate cost in USD"""
        pricing = self.PRICING.get(model, {'input': 1.0, 'output': 3.0})  # Default fallback
        input_cost = (prompt_tokens / 1_000_000) * pricing['input']
        output_cost = (completion_tokens / 1_000_000) * pricing['output']
        return input_cost + output_cost
    
    def track(self, model: str, prompt_tokens: int, completion_tokens: int) -> TokenUsage:
        """Track token usage and calculate cost"""
        cost = self.calculate_cost(model, prompt_tokens, completion_tokens)
        
        usage = TokenUsage(
            prompt_tokens=prompt_tokens,
            completion_tokens=completion_tokens,
            total_tokens=prompt_tokens + completion_tokens,
            estimated_cost_usd=cost,
            model=model,
            timestamp=time.time()
        )
        
        # Update totals
        self.total_usage.prompt_tokens += prompt_tokens
        self.total_usage.completion_tokens += completion_tokens
        self.total_usage.total_tokens += usage.total_tokens
        self.total_usage.estimated_cost_usd += cost
        
        # Update session
        self.session_usage.prompt_tokens += prompt_tokens
        self.session_usage.completion_tokens += completion_tokens
        self.session_usage.total_tokens += usage.total_tokens
        self.session_usage.estimated_cost_usd += cost
        
        # Store usage record
        self.usages.append(usage)
        
        # Log to file
        self._log_usage(usage)
        
        return usage
    
    def _log_usage(self, usage: TokenUsage):
        """Log usage to file"""
        try:
            log_entry = {
                'timestamp': usage.timestamp,
                'model': usage.model,
                'prompt_tokens': usage.prompt_tokens,
                'completion_tokens': usage.completion_tokens,
                'total_tokens': usage.total_tokens,
                'cost_usd': usage.estimated_cost_usd
            }
            with open(self.log_file, 'a', encoding='utf-8') as f:
                f.write(json.dumps(log_entry) + '\n')
        except Exception as e:
            logger.warning(f"Failed to log cost: {e}")
    
    def reset_session(self):
        """Reset session usage"""
        self.session_usage = TokenUsage()
    
    def get_summary(self) -> Dict[str, Any]:
        """Get usage summary"""
        return {
            'session': {
                'prompt_tokens': self.session_usage.prompt_tokens,
                'completion_tokens': self.session_usage.completion_tokens,
                'total_tokens': self.session_usage.total_tokens,
                'cost_usd': round(self.session_usage.estimated_cost_usd, 6)
            },
            'total': {
                'prompt_tokens': self.total_usage.prompt_tokens,
                'completion_tokens': self.total_usage.completion_tokens,
                'total_tokens': self.total_usage.total_tokens,
                'cost_usd': round(self.total_usage.estimated_cost_usd, 4)
            },
            'num_requests': len(self.usages)
        }


# Global cost tracker
_cost_tracker = None


def get_cost_tracker() -> CostTracker:
    """Get global cost tracker instance"""
    global _cost_tracker
    if _cost_tracker is None:
        _cost_tracker = CostTracker()
    return _cost_tracker


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
    
    def get_last_usage(self) -> Optional[TokenUsage]:
        """Get last request's token usage (optional)"""
        return None
    
    def get_model_name(self) -> str:
        """Get current model name"""
        return "unknown"


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
        self.model = 'gemini-2.5-flash'
        self.last_usage = None
        
        if self.api_key:
            try:
                import google.generativeai as genai
                genai.configure(api_key=self.api_key)
                self.client = genai.GenerativeModel(self.model)
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
    
    def get_model_name(self) -> str:
        return self.model


class OpenRouterProvider(LLMProvider):
    """OpenRouter Multi-model Gateway Provider
    
    Supports 500+ models through a single API.
    Pricing: No markup on model prices.
    
    Default model: deepseek/deepseek-chat (cost-effective)
    """
    
    BASE_URL = "https://openrouter.ai/api/v1"
    
    # Model aliases for convenience
    MODEL_ALIASES = {
        'deepseek': 'deepseek/deepseek-chat',
        'deepseek-chat': 'deepseek/deepseek-chat',
        'deepseek-reasoner': 'deepseek/deepseek-r1',  # R1 for reasoning
        'deepseek-r1': 'deepseek/deepseek-r1',
        'deepseek-v3.2': 'deepseek/deepseek-v3.2',
        'gemini-lite': 'google/gemini-2.5-flash-lite-preview-06-17',
        'gemini-flash': 'google/gemini-2.5-flash-preview-05-20',
        'claude-sonnet': 'anthropic/claude-4.6-sonnet',
        'claude-haiku': 'anthropic/claude-4.5-haiku',
        'gpt-nano': 'openai/gpt-5.4-nano',
        'gpt-mini': 'openai/gpt-5.4-mini',
        'gpt-5': 'openai/gpt-5.4',
    }
    
    def __init__(self, api_key: Optional[str] = None, model: str = None):
        self.api_key = api_key or os.environ.get('OPENROUTER_API_KEY')
        self.model = model or os.environ.get('OPENROUTER_MODEL', 'deepseek/deepseek-chat')
        self.last_usage = None
        self.cost_tracker = get_cost_tracker()
        
        # Resolve alias
        if self.model in self.MODEL_ALIASES:
            self.model = self.MODEL_ALIASES[self.model]
    
    def generate(self, system_prompt: str, user_message: str) -> str:
        if not self.is_available():
            raise RuntimeError("OpenRouter API key not configured")
        
        try:
            import requests
            
            headers = {
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json",
                "HTTP-Referer": os.environ.get('OPENROUTER_REFERER', 'http://localhost:5000'),
                "X-Title": os.environ.get('OPENROUTER_TITLE', 'LoveSimulation')
            }
            
            data = {
                "model": self.model,
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_message}
                ],
                "max_tokens": 500
            }
            
            response = requests.post(
                f"{self.BASE_URL}/chat/completions",
                headers=headers,
                json=data,
                timeout=60
            )
            
            if response.status_code != 200:
                raise RuntimeError(f"OpenRouter error: {response.status_code} - {response.text}")
            
            result = response.json()
            
            # Extract response
            content = result['choices'][0]['message']['content'].strip()
            
            # Track usage
            usage = result.get('usage', {})
            prompt_tokens = usage.get('prompt_tokens', 0)
            completion_tokens = usage.get('completion_tokens', 0)
            
            self.last_usage = self.cost_tracker.track(
                self.model, prompt_tokens, completion_tokens
            )
            
            # Log cost
            logger.info(f"[OpenRouter] model={self.model}, tokens={prompt_tokens}+{completion_tokens}, cost=${self.last_usage.estimated_cost_usd:.6f}")
            
            return content
            
        except Exception as e:
            raise RuntimeError(f"OpenRouter API error: {str(e)}")
    
    def is_available(self) -> bool:
        return self.api_key is not None
    
    def get_name(self) -> str:
        return f"OpenRouter ({self.model})"
    
    def get_model_name(self) -> str:
        return self.model
    
    def get_last_usage(self) -> Optional[TokenUsage]:
        return self.last_usage
    
    def set_model(self, model: str):
        """Change model dynamically"""
        if model in self.MODEL_ALIASES:
            model = self.MODEL_ALIASES[model]
        self.model = model
        logger.info(f"[OpenRouter] Model changed to: {self.model}")


class SmartRouter(LLMProvider):
    """Smart Model Router
    
    Automatically selects the optimal model based on request complexity:
    - Simple requests (greetings, short responses) → DeepSeek Chat
    - Complex requests (reasoning, emotional depth) → DeepSeek R1
    - Critical requests (important events) → Claude Sonnet
    
    This reduces costs by ~70% compared to always using premium models.
    """
    
    # Routing thresholds (adjusted for more sensitive routing)
    COMPLEXITY_THRESHOLDS = {
        'simple': 0.15,     # < 0.15 → cheap model
        'moderate': 0.35,   # 0.15-0.35 → standard model
        # > 0.35 → premium model
    }
    
    # Model tiers
    MODEL_TIERS = {
        'cheap': 'deepseek/deepseek-chat',      # Fast, cost-effective
        'standard': 'deepseek/deepseek-r1',     # Reasoning capability
        'premium': 'anthropic/claude-4.6-sonnet'  # Best quality
    }
    
    def __init__(self, api_key: str = None, default_tier: str = 'cheap'):
        self.provider = OpenRouterProvider(api_key=api_key)
        self.default_tier = default_tier
        self.routing_stats = {
            'cheap': 0,
            'standard': 0,
            'premium': 0
        }
        self.last_complexity = 0.0
        self.last_tier = 'cheap'
    
    def analyze_complexity(self, system_prompt: str, user_message: str, 
                           score: float = None, context: dict = None) -> float:
        """
        Analyze request complexity (0.0 = simple, 1.0 = complex)
        
        Factors:
        - Text length
        - Emotional keywords
        - Score (if provided)
        - Context (conversation history, NPC state)
        """
        complexity = 0.0
        
        # 1. Text length factor (0-0.3)
        total_length = len(system_prompt) + len(user_message)
        if total_length > 1000:
            complexity += 0.3
        elif total_length > 500:
            complexity += 0.2
        elif total_length > 200:
            complexity += 0.1
        
        # 2. Emotional keywords factor (0-0.3)
        emotional_keywords = [
            '슬픔', '기쁨', '사랑', '화남', '불안', '그리움', '고마움',
            '미안', '행복', '외로움', '설렘', '아쉬움', '실망', '희망',
            'sad', 'happy', 'love', 'angry', 'anxious', 'miss', 'thank',
            'sorry', 'lonely', 'excited', 'disappointed', 'hope'
        ]
        emotional_count = sum(1 for kw in emotional_keywords 
                             if kw in user_message.lower())
        complexity += min(0.3, emotional_count * 0.1)
        
        # 3. Score factor (0-0.3)
        if score is not None:
            # Low score = complex situation (need better reasoning)
            if score < 0.3:
                complexity += 0.3
            elif score < 0.5:
                complexity += 0.2
            elif score < 0.7:
                complexity += 0.1
        
        # 4. Context factor (0-0.1)
        if context:
            # Long conversation history
            history_length = context.get('history_length', 0)
            if history_length > 10:
                complexity += 0.1
            elif history_length > 5:
                complexity += 0.05
            
            # Low NPC mood = sensitive situation
            mood = context.get('mood', 0.5)
            if mood < 0.3:
                complexity += 0.1
        
        return min(1.0, complexity)
    
    def select_tier(self, complexity: float, force_tier: str = None) -> str:
        """Select model tier based on complexity"""
        if force_tier:
            return force_tier
        
        if complexity < self.COMPLEXITY_THRESHOLDS['simple']:
            return 'cheap'
        elif complexity < self.COMPLEXITY_THRESHOLDS['moderate']:
            return 'standard'
        else:
            return 'premium'
    
    def generate(self, system_prompt: str, user_message: str,
                 score: float = None, context: dict = None,
                 force_tier: str = None) -> str:
        """
        Generate response with automatic model selection
        
        Args:
            system_prompt: System prompt
            user_message: User message
            score: Optional authenticity score (affects routing)
            context: Optional context dict with:
                - history_length: conversation turn count
                - mood: current NPC mood (0-1)
                - is_event: special event flag
            force_tier: Force specific tier ('cheap', 'standard', 'premium')
        
        Returns:
            Generated text
        """
        # Analyze complexity
        complexity = self.analyze_complexity(system_prompt, user_message, score, context)
        self.last_complexity = complexity
        
        # Select tier
        tier = self.select_tier(complexity, force_tier)
        self.last_tier = tier
        
        # Update stats
        self.routing_stats[tier] += 1
        
        # Set model
        model = self.MODEL_TIERS[tier]
        self.provider.set_model(model)
        
        # Log routing decision
        logger.info(f"[SmartRouter] complexity={complexity:.2f}, tier={tier}, model={model}")
        
        # Generate
        return self.provider.generate(system_prompt, user_message)
    
    def is_available(self) -> bool:
        return self.provider.is_available()
    
    def get_name(self) -> str:
        return f"SmartRouter (last: {self.last_tier})"
    
    def get_model_name(self) -> str:
        return self.provider.get_model_name()
    
    def get_last_usage(self) -> Optional[TokenUsage]:
        return self.provider.get_last_usage()
    
    def get_routing_stats(self) -> dict:
        """Get routing statistics"""
        total = sum(self.routing_stats.values())
        if total == 0:
            return {'total': 0, 'distribution': {}}
        
        return {
            'total': total,
            'distribution': {
                tier: {
                    'count': count,
                    'percentage': round(count / total * 100, 1)
                }
                for tier, count in self.routing_stats.items()
            },
            'last_complexity': round(self.last_complexity, 2),
            'last_tier': self.last_tier
        }


class LLMProviderFactory:
    """LLM Provider Factory"""
    
    _providers = {
        'claude': ClaudeProvider,
        'openai': OpenAIProvider,
        'ollama': OllamaProvider,
        'gemini': GeminiProvider,
        'openrouter': OpenRouterProvider,
        'smart': SmartRouter,
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
                        'openrouter': 'openrouter_api_key',
                        'smart': 'openrouter_api_key',  # SmartRouter uses OpenRouter
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
            elif provider_name == 'openrouter':
                # OpenRouter uses api_key and optional model
                model = kwargs.get('model') or os.environ.get('OPENROUTER_MODEL', 'deepseek/deepseek-chat')
                provider = provider_class(api_key=api_key, model=model)
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
