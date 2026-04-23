#!/usr/bin/env python3
"""
Architecture Consultant Team
Usage:
  python consult.py <inbox-file.md>
  python consult.py --all
"""
import argparse
import sys
from pathlib import Path

from agents.orchestrator import Orchestrator

INBOX = Path(__file__).parent / "inbox"


def _summary(result: dict):
    p = result["problem"]
    d = result["decision"]
    kb = result["kb"]
    lenses = result["lenses"]

    sep = "=" * 70
    print()
    print(sep)
    print("  ARCHITECTURE CONSULTATION COMPLETE")
    print(sep)
    print(f"  Problem  : {p['title']}")
    print(f"  Tags     : {', '.join(p.get('tags', []))}")
    print(f"  Lenses   : {lenses[0]['name']}  vs  {lenses[1]['name']}")
    print()
    print(f"  Decision : {d['chosen_option']}")
    print(f"  Lens     : {d['chosen_lens']}")
    print()
    print("  Rationale:")
    for sentence in d["rationale"].split(". ")[:3]:
        if sentence.strip():
            print(f"    {sentence.strip()}.")
    print()
    notes = d.get("implementation_notes", [])
    if notes:
        print("  Key Implementation Notes:")
        for note in notes[:3]:
            print(f"    - {note}")
        print()
    print("  Knowledge Base:")
    print(f"    Problem  → {kb['problem_path']}")
    print(f"    Decision → {kb['decision_path']}")
    if kb.get("snippet_id"):
        snippets_dir = Path(kb["decision_path"]).parent.parent / "snippets"
        print(f"    Snippet  → {snippets_dir / kb['snippet_id']}-*")
    print(sep)
    print()


def process(path: Path):
    if not path.exists():
        print(f"Error: not found: {path}", file=sys.stderr)
        sys.exit(1)
    raw = path.read_text(encoding="utf-8")
    print(f"\nProcessing: {path.name}")
    result = Orchestrator().run(raw)
    _summary(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("file", nargs="?", help="Markdown problem file")
    parser.add_argument("--all", action="store_true", help="Process all .md files in inbox/")
    args = parser.parse_args()

    if args.all:
        files = sorted(INBOX.glob("*.md"))
        if not files:
            print("No .md files found in inbox/")
            sys.exit(0)
        for f in files:
            process(f)
    elif args.file:
        process(Path(args.file))
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
