from pathlib import Path

import frontmatter

KB_ROOT = Path(__file__).parent.parent / "knowledge-base"


class KBSearchAgent:
    def run(self, tags: list[str]) -> dict:
        tag_set = set(tags)
        related_problems = self._scan(KB_ROOT / "problems", tag_set, self._problem_entry)
        related_decisions = self._scan(KB_ROOT / "decisions", tag_set, self._decision_entry)
        related_snippets = self._scan_snippets(tag_set)

        return {
            "related_problems": related_problems[:3],
            "related_decisions": related_decisions[:3],
            "related_snippets": related_snippets[:3],
        }

    def _scan(self, directory: Path, tag_set: set, entry_fn) -> list[dict]:
        results = []
        if not directory.exists():
            return results
        for f in sorted(directory.glob("*.md")):
            post = frontmatter.load(str(f))
            file_tags = set(post.get("tags", []))
            overlap = len(tag_set & file_tags)
            if overlap > 0:
                entry = entry_fn(post)
                entry["overlap"] = overlap
                results.append(entry)
        results.sort(key=lambda x: x["overlap"], reverse=True)
        return results

    def _scan_snippets(self, tag_set: set) -> list[dict]:
        results = []
        snippets_dir = KB_ROOT / "snippets"
        if not snippets_dir.exists():
            return results
        for snippet_dir in sorted(snippets_dir.iterdir()):
            context_file = snippet_dir / "context.md"
            if not context_file.exists():
                continue
            post = frontmatter.load(str(context_file))
            file_tags = set(post.get("tags", []))
            overlap = len(tag_set & file_tags)
            if overlap > 0:
                results.append({
                    "id": post.get("id", ""),
                    "title": post.get("title", ""),
                    "when_to_use": post.get("when_to_use", ""),
                    "overlap": overlap,
                })
        results.sort(key=lambda x: x["overlap"], reverse=True)
        return results

    @staticmethod
    def _problem_entry(post) -> dict:
        return {"id": post.get("id", ""), "title": post.get("title", ""), "tags": list(post.get("tags", []))}

    @staticmethod
    def _decision_entry(post) -> dict:
        return {"id": post.get("id", ""), "title": post.get("title", ""), "chosen_option": post.get("chosen_option", "")}
