from typing import Protocol

import httpx


class GenerationModelError(OSError):
    """Raised when an LLM provider cannot generate a completion."""


class GenerationModel(Protocol):
    @property
    def model_name(self) -> str: ...

    async def generate(
        self,
        *,
        prompt: str,
        system_prompt: str | None = None,
    ) -> str: ...


class OpenAICompatibleGenerationModel:
    def __init__(
        self,
        *,
        model_name: str,
        base_url: str,
        api_key: str | None = None,
        timeout: float = 60.0,
    ) -> None:
        self._model_name = model_name
        self._base_url = base_url.rstrip("/")
        self._api_key = api_key or "local"
        self._timeout = timeout

    @property
    def model_name(self) -> str:
        return self._model_name

    async def generate(
        self,
        *,
        prompt: str,
        system_prompt: str | None = None,
    ) -> str:
        messages: list[dict[str, str]] = []
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        messages.append({"role": "user", "content": prompt})

        headers = {
            "Authorization": f"Bearer {self._api_key}",
            "Content-Type": "application/json",
        }
        payload = {
            "model": self._model_name,
            "messages": messages,
            "temperature": 0.2,
        }

        url = f"{self._base_url}/chat/completions"
        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(url, headers=headers, json=payload)
                response.raise_for_status()
                data = response.json()
                return data["choices"][0]["message"]["content"].strip()
        except (httpx.HTTPError, KeyError, IndexError, ValueError) as exc:
            raise GenerationModelError("failed to generate completion from LLM") from exc
