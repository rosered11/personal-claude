import json

from .base import BaseAgent

_SYSTEM_TEMPLATE = """You are a senior software architect evaluating a technical problem through a specific lens.

Your lens: {lens_name}
Your focus: {lens_focus}

Analyze the problem strictly from this lens. Propose the best approach you see from this angle. If the problem includes code, provide an improved or alternative snippet that illustrates your recommendation.

Return ONLY a JSON object — no markdown fences, no explanation:
{{
  "lens": "{lens_name}",
  "option_name": "Name of the proposed option or approach",
  "rationale": "Why this option is best from the {lens_name} perspective",
  "pros": ["pro 1", "pro 2"],
  "cons": ["con 1", "con 2"],
  "implementation_notes": ["note 1", "note 2"],
  "code_snippet": "Code illustrating the approach, or empty string if none",
  "code_language": "language name or null"
}}"""


class ArchitectAgent(BaseAgent):
    def __init__(self, lens: dict):
        super().__init__()
        self.lens = lens
        self._system = _SYSTEM_TEMPLATE.format(
            lens_name=lens["name"],
            lens_focus=lens["focus"],
        )

    def run(self, problem: dict, kb_context: dict) -> dict:
        payload = json.dumps(
            {"problem": problem, "related_kb_context": kb_context}, indent=2
        )
        return self._call_json(self._system, [{"role": "user", "content": payload}])
