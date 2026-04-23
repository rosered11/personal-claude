from .base import BaseAgent

SYSTEM = """You are a senior software architect. Analyze a software problem and extract a structured representation.

Return ONLY a JSON object — no markdown fences, no explanation — with this exact shape:
{
  "title": "Short descriptive title (5-10 words)",
  "summary": "2-3 sentence summary of the problem",
  "context": "Background context, constraints, and environment",
  "constraints": ["constraint 1", "constraint 2"],
  "tags": ["tag1", "tag2"],
  "has_code": true,
  "code_language": "python"
}

Tags must be lowercase domain-specific keywords (e.g., etl, kafka, batch, api-design, state-machine, caching, idempotency, ordering, retry, saga, outbox, cqrs, soft-delete, watermark, circuit-breaker)."""


class ProblemAnalystAgent(BaseAgent):
    def run(self, raw_markdown: str) -> dict:
        return self._call_json(SYSTEM, [{"role": "user", "content": raw_markdown}])
