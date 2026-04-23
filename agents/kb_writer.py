import re
from datetime import date
from pathlib import Path

import frontmatter

KB_ROOT = Path(__file__).parent.parent / "knowledge-base"

_LANG_EXT = {
    "python": ".py",
    "go": ".go",
    "golang": ".go",
    "csharp": ".cs",
    "c#": ".cs",
    "typescript": ".ts",
    "javascript": ".js",
    "sql": ".sql",
    "yaml": ".yaml",
    "json": ".json",
    "bash": ".sh",
    "shell": ".sh",
}


def _slug(title: str) -> str:
    s = title.lower()
    s = re.sub(r"[^\w\s-]", "", s)
    s = re.sub(r"[\s_]+", "-", s)
    s = re.sub(r"-+", "-", s).strip("-")
    return s[:60]


def _next_seq(prefix: str) -> int:
    if prefix == "S":
        d = KB_ROOT / "snippets"
        if not d.exists():
            return 1
        candidates = [x for x in d.iterdir() if x.is_dir()]
    else:
        dirs = {"P": KB_ROOT / "problems", "D": KB_ROOT / "decisions"}
        d = dirs[prefix]
        if not d.exists():
            return 1
        candidates = list(d.glob("*.md"))

    numbers = []
    for item in candidates:
        m = re.match(rf"^{prefix}(\d+)", item.name)
        if m:
            numbers.append(int(m.group(1)))
    return max(numbers, default=0) + 1


def _fmt_id(prefix: str, n: int) -> str:
    return f"{prefix}{n:03d}"


def _write_post(path: Path, meta: dict, body: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    post = frontmatter.Post(body.strip(), **meta)
    path.write_text(frontmatter.dumps(post), encoding="utf-8")


def _bullet_list(items: list[str]) -> str:
    return "\n".join(f"- {item}" for item in items) if items else "- (none)"


class KBWriterAgent:
    def run(
        self,
        problem: dict,
        arch_a: dict,
        arch_b: dict,
        decision: dict,
        today: str | None = None,
    ) -> dict:
        today = today or date.today().isoformat()
        tags = problem.get("tags", [])

        p_num = _next_seq("P")
        d_num = _next_seq("D")
        p_id = _fmt_id("P", p_num)
        d_id = _fmt_id("D", d_num)

        snippet_id = None
        if decision.get("has_snippet") and decision.get("final_code_snippet", "").strip():
            s_num = _next_seq("S")
            snippet_id = _fmt_id("S", s_num)

        slug = _slug(problem["title"])

        # --- Problem file ---
        p_path = KB_ROOT / "problems" / f"{p_id}-{slug}.md"
        _write_post(
            p_path,
            {
                "id": p_id,
                "title": problem["title"],
                "date": today,
                "tags": tags,
                "related_decisions": [d_id],
                "related_snippets": [snippet_id] if snippet_id else [],
            },
            f"""## Problem Description

{problem['summary']}

## Context

{problem['context']}

## Constraints

{_bullet_list(problem.get('constraints', []))}
""",
        )

        # --- Decision file ---
        d_path = KB_ROOT / "decisions" / f"{d_id}-{slug}.md"
        _write_post(
            d_path,
            {
                "id": d_id,
                "title": problem["title"],
                "date": today,
                "problem_id": p_id,
                "chosen_option": decision["chosen_option"],
                "tags": tags,
                "related_snippets": [snippet_id] if snippet_id else [],
            },
            f"""## Options Considered

### Option A: {arch_a['option_name']} ({arch_a['lens']} lens)

**Pros:**
{_bullet_list(arch_a.get('pros', []))}

**Cons:**
{_bullet_list(arch_a.get('cons', []))}

### Option B: {arch_b['option_name']} ({arch_b['lens']} lens)

**Pros:**
{_bullet_list(arch_b.get('pros', []))}

**Cons:**
{_bullet_list(arch_b.get('cons', []))}

## Decision

**{decision['chosen_option']}** (primary lens: {decision['chosen_lens']})

## Rationale

{decision['rationale']}

## Implementation Notes

{_bullet_list(decision.get('implementation_notes', []))}

## Consequences

{_bullet_list(decision.get('consequences', []))}
""",
        )

        # --- Snippet ---
        if snippet_id:
            ext = _LANG_EXT.get((decision.get("code_language") or "").lower(), ".txt")
            s_dir = KB_ROOT / "snippets" / f"{snippet_id}-{slug}"
            s_dir.mkdir(parents=True, exist_ok=True)

            _write_post(
                s_dir / "context.md",
                {
                    "id": snippet_id,
                    "title": problem["title"],
                    "date": today,
                    "language": decision.get("code_language", ""),
                    "tags": tags,
                    "when_to_use": f"When solving problems related to: {', '.join(tags)}",
                    "related_problems": [p_id],
                    "related_decisions": [d_id],
                },
                "## When to Use\n\nApply this snippet when facing problems tagged: "
                + ", ".join(tags)
                + ".\n",
            )
            (s_dir / f"code{ext}").write_text(
                decision["final_code_snippet"], encoding="utf-8"
            )

        # --- Update index ---
        self._update_index(p_id, d_id, snippet_id, problem["title"], today, tags)

        return {
            "problem_id": p_id,
            "decision_id": d_id,
            "snippet_id": snippet_id,
            "problem_path": str(p_path),
            "decision_path": str(d_path),
        }

    @staticmethod
    def _update_index(p_id, d_id, snippet_id, title, today, tags):
        index_path = KB_ROOT / "index.md"
        if not index_path.exists():
            index_path.write_text(
                "# Knowledge Base Index\n\n"
                "| ID | Title | Type | Date | Tags |\n"
                "|---|---|---|---|---|\n",
                encoding="utf-8",
            )
        content = index_path.read_text(encoding="utf-8")
        tag_str = ", ".join(tags)
        rows = (
            f"| {p_id} | {title} | Problem | {today} | {tag_str} |\n"
            f"| {d_id} | {title} | Decision | {today} | {tag_str} |\n"
        )
        if snippet_id:
            rows += f"| {snippet_id} | {title} | Snippet | {today} | {tag_str} |\n"
        index_path.write_text(content + rows, encoding="utf-8")
