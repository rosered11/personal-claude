import json
import re

import anthropic

MODEL = "claude-opus-4-7"
MAX_TOKENS = 8000


class BaseAgent:
    def __init__(self):
        self.client = anthropic.Anthropic()

    def _call_json(self, system_text: str, messages: list[dict]) -> dict:
        response = self.client.messages.create(
            model=MODEL,
            max_tokens=MAX_TOKENS,
            thinking={"type": "adaptive"},
            system=[
                {
                    "type": "text",
                    "text": system_text,
                    "cache_control": {"type": "ephemeral"},
                }
            ],
            messages=messages,
        )
        raw = ""
        for block in response.content:
            if block.type == "text":
                raw = block.text
                break
        raw = re.sub(r"^```(?:json)?\s*", "", raw.strip())
        raw = re.sub(r"\s*```$", "", raw.strip())
        return json.loads(raw)
