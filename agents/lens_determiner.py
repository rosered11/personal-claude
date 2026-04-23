import json

from .base import BaseAgent

_LENS_POOL = [
    ("consistency-first", "data integrity, correctness, idempotency, and ACID guarantees"),
    ("performance-first", "throughput, latency, and resource efficiency"),
    ("operability-first", "simplicity, debuggability, failure recovery, and on-call burden"),
    ("scalability-first", "horizontal scaling, load handling, and future growth"),
    ("security-first", "data protection, access control, and threat modeling"),
    ("cost-first", "infrastructure cost, licensing, and operational spend"),
    ("coupling-first", "service boundaries, dependency management, and team autonomy"),
]

_LENS_DESCRIPTIONS = "\n".join(f"- {name}: {desc}" for name, desc in _LENS_POOL)
_LENS_NAMES = ", ".join(name for name, _ in _LENS_POOL)

SYSTEM = f"""You are a software architecture strategist. Given a structured problem, select exactly 2 architectural lenses from the pool below that provide the most valuable and contrasting perspectives for evaluating solutions.

Available lenses:
{_LENS_DESCRIPTIONS}

Choose lenses that meaningfully contrast with each other for this specific problem.

Return ONLY a JSON object — no markdown fences, no explanation:
{{
  "lenses": [
    {{"name": "lens-name", "focus": "one sentence on what this lens emphasizes for this specific problem"}},
    {{"name": "lens-name", "focus": "one sentence on what this lens emphasizes for this specific problem"}}
  ]
}}"""


class LensDeterminerAgent(BaseAgent):
    def run(self, problem: dict) -> dict:
        return self._call_json(
            SYSTEM,
            [{"role": "user", "content": json.dumps(problem, indent=2)}],
        )
