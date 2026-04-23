import json

from .base import BaseAgent

SYSTEM = """You are the lead architect on a consulting team. Two architects analyzed the same problem from different lenses. Your job is to:

1. Compare their recommendations
2. Choose the best overall option — you may blend insights from both
3. Explain the decision clearly: what was chosen, what was not chosen, and why
4. Select or synthesize the best code snippet if any were provided

Return ONLY a JSON object — no markdown fences, no explanation:
{
  "chosen_option": "Name of the chosen option",
  "chosen_lens": "Which lens most influenced the decision",
  "rationale": "Clear explanation of why this option was chosen and what was rejected and why",
  "implementation_notes": ["note 1", "note 2"],
  "consequences": ["accepted tradeoff or limitation 1", "future trigger to revisit 2"],
  "final_code_snippet": "Best code snippet, or empty string if none",
  "code_language": "language name or null",
  "has_snippet": true
}"""


class DecisionSynthesizerAgent(BaseAgent):
    def run(self, problem: dict, architect_a: dict, architect_b: dict) -> dict:
        payload = json.dumps(
            {
                "problem": problem,
                "architect_a_analysis": architect_a,
                "architect_b_analysis": architect_b,
            },
            indent=2,
        )
        return self._call_json(SYSTEM, [{"role": "user", "content": payload}])
