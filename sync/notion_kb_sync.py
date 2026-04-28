#!/usr/bin/env python3
"""
sync/notion_kb_sync.py

Sync knowledge-base/ P/D/S records to three linked Notion databases.

Databases created:
  - Architecture Problems  (P records)
  - Architectural Decisions (D records)
  - Code Snippets          (S records)

Relations maintained:
  Problem.Decisions  ↔  Decision.Problem
  Problem.Snippets   ↔  Snippet.Problems
  Decision.Snippets  ↔  Snippet.Decisions

Usage:
  python sync/notion_kb_sync.py --setup            # first run: create DBs
  python sync/notion_kb_sync.py                    # full upsert sync
  python sync/notion_kb_sync.py --db p             # problems only
  python sync/notion_kb_sync.py --db d             # decisions only
  python sync/notion_kb_sync.py --db s             # snippets only
  python sync/notion_kb_sync.py --no-relations     # skip relation linking pass

Required env vars:
  NOTION_TOKEN              Integration secret (secret_xxx)
  NOTION_PARENT_PAGE_ID     Parent page ID (setup only — share page with integration first)
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

try:
    import requests
    import yaml
except ImportError:
    print("Missing dependencies. Install with:")
    print("  pip install requests pyyaml")
    sys.exit(1)

# Auto-load sync/.env if present
_env_file = Path(__file__).parent / ".env"
if _env_file.exists():
    for _line in _env_file.read_text().splitlines():
        _line = _line.strip()
        if _line and not _line.startswith("#") and "=" in _line:
            _k, _, _v = _line.partition("=")
            os.environ.setdefault(_k.strip(), _v.strip())

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

KB_ROOT = Path(__file__).parent.parent / "knowledge-base"
CONFIG_PATH = Path(__file__).parent / "notion_kb_config.json"
NOTION_API = "https://api.notion.com/v1"
NOTION_VERSION = "2022-06-28"
REQUEST_DELAY = 0.35  # seconds between API calls — stays under Notion's 3 req/s limit
MAX_CODE_PER_BLOCK = 8000  # chars; split into multiple blocks above this

LANG_MAP = {
    "cs": "c#", "csharp": "c#",
    "py": "python", "python": "python",
    "ts": "typescript", "typescript": "typescript",
    "js": "javascript", "javascript": "javascript",
    "go": "go",
    "sql": "sql",
    "yaml": "yaml", "yml": "yaml",
    "json": "json",
    "lua": "lua",
    "bash": "bash", "sh": "shell", "shell": "shell",
    "": "plain text",
}

LANG_DISPLAY = {
    "cs": "C#", "csharp": "C#",
    "py": "Python", "python": "Python",
    "go": "Go",
    "sql": "SQL",
    "ts": "TypeScript", "typescript": "TypeScript",
    "lua": "Lua",
    "bash": "Bash", "sh": "Bash",
}

SEVERITY_EMOJI = {"high": "🔴", "medium": "🟡", "low": "🟢"}

# ---------------------------------------------------------------------------
# Dataclasses
# ---------------------------------------------------------------------------

@dataclass
class Problem:
    id: str
    title: str
    date: str
    tags: list
    severity: str
    related_decisions: list
    related_snippets: list
    body: str

@dataclass
class Decision:
    id: str
    title: str
    chosen_option: str
    problem_id: Optional[str]
    tags: list
    related_snippets: list
    body: str

@dataclass
class Snippet:
    id: str
    title: str
    language: str
    when_to_use: str
    related_problems: list
    related_decisions: list
    body: str
    code: str

# ---------------------------------------------------------------------------
# KB Parsing
# ---------------------------------------------------------------------------

def parse_frontmatter(text: str) -> tuple[dict, str]:
    if not text.startswith("---"):
        return {}, text
    try:
        end = text.index("---", 3)
    except ValueError:
        return {}, text
    fm = yaml.safe_load(text[3:end]) or {}
    return fm, text[end + 3:].strip()

def as_list(value) -> list:
    if value is None:
        return []
    return value if isinstance(value, list) else [value]

def extract_h1(body: str) -> str:
    m = re.search(r"^#\s+(.+)$", body, re.MULTILINE)
    return m.group(1).strip() if m else ""

def load_problems() -> list[Problem]:
    out = []
    for path in sorted((KB_ROOT / "problems").glob("P*.md")):
        fm, body = parse_frontmatter(path.read_text(encoding="utf-8"))
        out.append(Problem(
            id=str(fm.get("id", path.stem[:4])),
            title=fm.get("title", extract_h1(body) or path.stem),
            date=str(fm.get("date", "")),
            tags=as_list(fm.get("tags")),
            severity=fm.get("severity", "medium"),
            related_decisions=as_list(fm.get("related_decisions")),
            related_snippets=as_list(fm.get("related_snippets")),
            body=body,
        ))
    return out

def load_decisions() -> list[Decision]:
    out = []
    for path in sorted((KB_ROOT / "decisions").glob("D*.md")):
        fm, body = parse_frontmatter(path.read_text(encoding="utf-8"))
        title = extract_h1(body) or path.stem
        title = re.sub(r"^Decision:\s*", "", title).strip()
        out.append(Decision(
            id=str(fm.get("id", path.stem[:4])),
            title=title,
            chosen_option=fm.get("chosen_option", ""),
            problem_id=str(fm["problem_id"]) if fm.get("problem_id") else None,
            tags=as_list(fm.get("tags")),
            related_snippets=as_list(fm.get("related_snippets")),
            body=body,
        ))
    return out

def load_snippets() -> list[Snippet]:
    out = []
    for ctx_path in sorted((KB_ROOT / "snippets").glob("S*/context.md")):
        snippet_dir = ctx_path.parent
        fm, body = parse_frontmatter(ctx_path.read_text(encoding="utf-8"))

        code_files = list(snippet_dir.glob("code.*"))
        code_content = code_files[0].read_text(encoding="utf-8") if code_files else ""
        code_ext = code_files[0].suffix.lstrip(".") if code_files else ""

        title = extract_h1(body) or snippet_dir.name[4:].replace("-", " ").title()
        title = re.sub(r"^(Async\s+)?Snippet:\s*", "", title).strip()

        language = fm.get("language", code_ext)

        raw_wtu = fm.get("when_to_use", "")
        if isinstance(raw_wtu, list):
            when_to_use_str = "; ".join(str(item) for item in raw_wtu)
        else:
            when_to_use_str = str(raw_wtu) if raw_wtu else ""

        out.append(Snippet(
            id=str(fm.get("id", snippet_dir.name[:4])),
            title=title,
            language=language,
            when_to_use=when_to_use_str,
            related_problems=as_list(fm.get("related_problems")),
            related_decisions=as_list(fm.get("related_decisions")),
            body=body,
            code=code_content,
        ))
    return out

# ---------------------------------------------------------------------------
# Notion Block Builders
# ---------------------------------------------------------------------------

def rt(text: str, bold: bool = False, code: bool = False) -> dict:
    return {
        "type": "text",
        "text": {"content": text[:2000]},
        "annotations": {
            "bold": bold, "italic": False, "code": code,
            "strikethrough": False, "underline": False, "color": "default",
        },
    }

def parse_inline(text: str) -> list:
    parts = []
    for seg in re.split(r"(\*\*[^*]+\*\*|`[^`]+`)", text):
        if not seg:
            continue
        if seg.startswith("**") and seg.endswith("**"):
            parts.append(rt(seg[2:-2], bold=True))
        elif seg.startswith("`") and seg.endswith("`"):
            parts.append(rt(seg[1:-1], code=True))
        else:
            parts.append(rt(seg))
    return parts or [rt("")]

def b_heading2(text: str) -> dict:
    return {"object": "block", "type": "heading_2", "heading_2": {"rich_text": parse_inline(text)}}

def b_heading3(text: str) -> dict:
    return {"object": "block", "type": "heading_3", "heading_3": {"rich_text": parse_inline(text)}}

def b_paragraph(text: str) -> dict:
    return {"object": "block", "type": "paragraph", "paragraph": {"rich_text": parse_inline(text)}}

def b_bullet(text: str) -> dict:
    return {"object": "block", "type": "bulleted_list_item", "bulleted_list_item": {"rich_text": parse_inline(text)}}

def b_numbered(text: str) -> dict:
    return {"object": "block", "type": "numbered_list_item", "numbered_list_item": {"rich_text": parse_inline(text)}}

def b_callout(text: str, emoji: str = "💡", color: str = "gray_background") -> dict:
    return {
        "object": "block",
        "type": "callout",
        "callout": {
            "rich_text": [rt(text[:2000])],
            "icon": {"type": "emoji", "emoji": emoji},
            "color": color,
        },
    }

def b_divider() -> dict:
    return {"object": "block", "type": "divider", "divider": {}}

def _table_cells(line: str) -> list[str]:
    return [cell.strip() for cell in line.strip().strip("|").split("|")]

def _is_separator(line: str) -> bool:
    return bool(re.match(r"^\|[-| :]+\|$", line.strip()))

def b_table(rows: list[list[str]]) -> dict:
    table_width = max(len(r) for r in rows)
    children = []
    for row in rows:
        padded = row + [""] * (table_width - len(row))
        children.append({
            "object": "block",
            "type": "table_row",
            "table_row": {"cells": [parse_inline(cell) for cell in padded]},
        })
    return {
        "object": "block",
        "type": "table",
        "table": {
            "table_width": table_width,
            "has_column_header": True,
            "has_row_header": False,
            "children": children,
        },
    }

def b_code(code: str, lang: str = "plain text") -> dict:
    notion_lang = LANG_MAP.get(lang.lower(), "plain text")
    chunks = [rt(code[i:i+2000]) for i in range(0, min(len(code), MAX_CODE_PER_BLOCK), 2000)]
    return {"object": "block", "type": "code", "code": {"rich_text": chunks or [rt("")], "language": notion_lang}}

def b_code_blocks(code: str, lang: str = "plain text") -> list:
    """Split long code into multiple Notion code blocks if needed."""
    if len(code) <= MAX_CODE_PER_BLOCK:
        return [b_code(code, lang)]
    blocks = []
    lines, current, current_len = code.split("\n"), [], 0
    for line in lines:
        if current_len + len(line) + 1 > MAX_CODE_PER_BLOCK and current:
            blocks.append(b_code("\n".join(current), lang))
            current, current_len = [line], len(line)
        else:
            current.append(line)
            current_len += len(line) + 1
    if current:
        blocks.append(b_code("\n".join(current), lang))
    return blocks

# ---------------------------------------------------------------------------
# Markdown → Notion Blocks
# ---------------------------------------------------------------------------

def extract_sections(body: str) -> dict:
    sections, current = {}, None
    for line in body.split("\n"):
        if line.startswith("## "):
            current = line[3:].strip()
            sections[current] = []
        elif not line.startswith("# ") and current is not None:
            sections[current].append(line)
    return {k: "\n".join(v).strip() for k, v in sections.items()}

def md_to_blocks(text: str) -> list:
    blocks = []
    lines = text.split("\n")
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if stripped.startswith("```"):
            lang = stripped[3:].strip()
            code_lines, i = [], i + 1
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code_lines.append(lines[i])
                i += 1
            blocks.extend(b_code_blocks("\n".join(code_lines), lang))

        # Markdown table — collect all consecutive pipe rows
        elif stripped.startswith("|") and stripped.endswith("|"):
            table_lines = [stripped]
            while i + 1 < len(lines) and lines[i + 1].strip().startswith("|") and lines[i + 1].strip().endswith("|"):
                i += 1
                table_lines.append(lines[i].strip())
            rows = [_table_cells(l) for l in table_lines if not _is_separator(l)]
            if rows:
                blocks.append(b_table(rows))

        elif stripped.startswith("### "):
            blocks.append(b_heading3(stripped[4:]))

        elif stripped.startswith("## "):
            blocks.append(b_heading2(stripped[3:]))

        elif stripped.startswith("- ") or stripped.startswith("* "):
            blocks.append(b_bullet(stripped[2:]))

        elif re.match(r"^\d+\.\s", stripped):
            blocks.append(b_numbered(re.sub(r"^\d+\.\s+", "", stripped)))

        elif stripped:
            blocks.append(b_paragraph(stripped))

        i += 1
    return blocks

# ---------------------------------------------------------------------------
# Page Body Builders
# ---------------------------------------------------------------------------

def build_problem_blocks(p: Problem) -> list:
    emoji = SEVERITY_EMOJI.get(p.severity, "🔵")
    blocks = [b_callout(f"{p.severity.upper()} — {p.id}: {p.title}", emoji)]

    sections = extract_sections(p.body)
    for section in ["Problem", "Root Cause", "Constraints", "Affected Components"]:
        content = sections.get(section, "")
        if content:
            blocks.append(b_heading2(section))
            blocks.extend(md_to_blocks(content))

    refs = []
    if p.related_decisions:
        refs.append(f"Decisions: {', '.join(str(d) for d in p.related_decisions)}")
    if p.related_snippets:
        refs.append(f"Snippets: {', '.join(str(s) for s in p.related_snippets)}")
    if refs:
        blocks.append(b_divider())
        blocks.append(b_heading3("Cross-References"))
        for r in refs:
            blocks.append(b_bullet(r))

    return blocks

def build_decision_blocks(d: Decision) -> list:
    blocks = [b_callout(f"Chosen: {d.chosen_option}", "✅")]

    if d.problem_id:
        blocks.append(b_paragraph(f"Problem: {d.problem_id}"))

    sections = extract_sections(d.body)
    for section in ["Context", "Options Considered", "Decision", "Consequences"]:
        content = sections.get(section, "")
        if content:
            blocks.append(b_heading2(section))
            blocks.extend(md_to_blocks(content))

    if d.related_snippets:
        blocks.append(b_divider())
        blocks.append(b_heading3("Code Snippets"))
        blocks.append(b_bullet(f"Snippets: {', '.join(str(s) for s in d.related_snippets)}"))

    return blocks

def build_snippet_blocks(s: Snippet) -> list:
    blocks = []

    if s.when_to_use:
        blocks.append(b_callout(s.when_to_use, "💡"))

    sections = extract_sections(s.body)
    for section in ["When to use", "When NOT to use"]:
        content = sections.get(section, "")
        if content:
            blocks.append(b_heading2(section))
            blocks.extend(md_to_blocks(content))

    if s.code:
        blocks.append(b_heading2("Code"))
        blocks.extend(b_code_blocks(s.code, s.language))

    if s.related_problems or s.related_decisions:
        blocks.append(b_divider())
        blocks.append(b_heading3("Cross-References"))
        if s.related_problems:
            blocks.append(b_bullet(f"Problems: {', '.join(str(p) for p in s.related_problems)}"))
        if s.related_decisions:
            blocks.append(b_bullet(f"Decisions: {', '.join(str(d) for d in s.related_decisions)}"))

    return blocks

# ---------------------------------------------------------------------------
# Notion API Client
# ---------------------------------------------------------------------------

class NotionClient:
    def __init__(self, token: str):
        self.headers = {
            "Authorization": f"Bearer {token}",
            "Notion-Version": NOTION_VERSION,
            "Content-Type": "application/json",
        }

    def _req(self, method: str, path: str, **kwargs) -> dict:
        resp = requests.request(method, f"{NOTION_API}{path}", headers=self.headers, timeout=30, **kwargs)
        if not resp.ok:
            raise RuntimeError(f"Notion {method} {path} → {resp.status_code}: {resp.text[:600]}")
        return resp.json()

    def create_database(self, parent_page_id: str, title: str, properties: dict) -> dict:
        return self._req("POST", "/databases", json={
            "parent": {"type": "page_id", "page_id": parent_page_id},
            "title": [{"type": "text", "text": {"content": title}}],
            "properties": properties,
        })

    def patch_database(self, db_id: str, properties: dict) -> dict:
        return self._req("PATCH", f"/databases/{db_id}", json={"properties": properties})

    def query_database(self, db_id: str) -> list:
        pages, cursor = [], None
        while True:
            body = {"page_size": 100}
            if cursor:
                body["start_cursor"] = cursor
            result = self._req("POST", f"/databases/{db_id}/query", json=body)
            pages.extend(result.get("results", []))
            if not result.get("has_more"):
                break
            cursor = result.get("next_cursor")
        return pages

    def create_page(self, db_id: str, properties: dict, children: list = None) -> dict:
        body = {"parent": {"database_id": db_id}, "properties": properties}
        if children:
            body["children"] = children[:100]
        return self._req("POST", "/pages", json=body)

    def update_page(self, page_id: str, properties: dict) -> dict:
        return self._req("PATCH", f"/pages/{page_id}", json={"properties": properties})

    def get_block_children(self, block_id: str) -> list:
        blocks, cursor = [], None
        while True:
            path = f"/blocks/{block_id}/children?page_size=100"
            if cursor:
                path += f"&start_cursor={cursor}"
            result = self._req("GET", path)
            blocks.extend(result.get("results", []))
            if not result.get("has_more"):
                break
            cursor = result.get("next_cursor")
        return blocks

    def delete_block(self, block_id: str) -> dict:
        return self._req("DELETE", f"/blocks/{block_id}")

    def append_children(self, block_id: str, children: list) -> None:
        for i in range(0, len(children), 100):
            self._req("POST", f"/blocks/{block_id}/children", json={"children": children[i:i+100]})
            if i + 100 < len(children):
                time.sleep(REQUEST_DELAY)

    def replace_body(self, page_id: str, blocks: list) -> None:
        # Append new blocks FIRST — if this fails we haven't touched old content.
        # Then delete old blocks so page ends with only new content.
        old_blocks = self.get_block_children(page_id)
        if blocks:
            self.append_children(page_id, blocks)
        for block in old_blocks:
            try:
                self.delete_block(block["id"])
                time.sleep(REQUEST_DELAY)
            except Exception:
                pass

# ---------------------------------------------------------------------------
# Notion Property Helpers
# ---------------------------------------------------------------------------

def prop_title(text: str) -> dict:
    return {"title": [{"type": "text", "text": {"content": text[:2000]}}]}

def prop_rich_text(text: str) -> dict:
    return {"rich_text": [{"type": "text", "text": {"content": text[:2000]}}]}

def prop_select(name: str) -> dict:
    return {"select": {"name": name}} if name else {"select": None}

def prop_multi_select(names: list) -> dict:
    return {"multi_select": [{"name": str(n)} for n in names if n]}

def prop_date(date_str: str) -> dict:
    s = str(date_str) if date_str else ""
    return {"date": {"start": s}} if s and s != "None" else {"date": None}

def prop_relation(page_ids: list) -> dict:
    return {"relation": [{"id": pid} for pid in page_ids if pid]}

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

def load_config() -> dict:
    if CONFIG_PATH.exists():
        return json.loads(CONFIG_PATH.read_text())
    return {}

def save_config(cfg: dict) -> None:
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    CONFIG_PATH.write_text(json.dumps(cfg, indent=2))

# ---------------------------------------------------------------------------
# Content Hash Tracking  (skip body rebuild when file unchanged)
# ---------------------------------------------------------------------------

HASH_PATH = Path(__file__).parent / "notion_kb_hashes.json"

def load_hashes() -> dict:
    if HASH_PATH.exists():
        return json.loads(HASH_PATH.read_text())
    return {}

def save_hashes(hashes: dict) -> None:
    HASH_PATH.write_text(json.dumps(hashes, indent=2))

def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def snippet_hash(snippet_dir: Path) -> str:
    ctx = snippet_dir / "context.md"
    code_files = list(snippet_dir.glob("code.*"))
    combined = ctx.read_bytes() if ctx.exists() else b""
    if code_files:
        combined += code_files[0].read_bytes()
    return hashlib.sha256(combined).hexdigest()

def content_changed(kb_id: str, current_hash: str, stored_hashes: dict) -> bool:
    return stored_hashes.get(kb_id) != current_hash

# ---------------------------------------------------------------------------
# First-Time Setup: Create Notion Databases
# ---------------------------------------------------------------------------

def setup_databases(client: NotionClient, parent_page_id: str) -> dict:
    print("Creating Notion databases...")

    # 1. Problems DB
    problems_db = client.create_database(parent_page_id, "Architecture Problems", {
        "Name": {"title": {}},
        "KB ID": {"rich_text": {}},
        "Severity": {"select": {"options": [
            {"name": "high", "color": "red"},
            {"name": "medium", "color": "yellow"},
            {"name": "low", "color": "green"},
        ]}},
        "Tags": {"multi_select": {}},
        "Date": {"date": {}},
    })
    p_db_id = problems_db["id"]
    print(f"  ✓ Architecture Problems: {p_db_id}")
    time.sleep(REQUEST_DELAY)

    # 2. Decisions DB
    decisions_db = client.create_database(parent_page_id, "Architectural Decisions", {
        "Name": {"title": {}},
        "KB ID": {"rich_text": {}},
        "Chosen Option": {"rich_text": {}},
        "Tags": {"multi_select": {}},
    })
    d_db_id = decisions_db["id"]
    print(f"  ✓ Architectural Decisions: {d_db_id}")
    time.sleep(REQUEST_DELAY)

    # 3. Snippets DB
    snippets_db = client.create_database(parent_page_id, "Code Snippets", {
        "Name": {"title": {}},
        "KB ID": {"rich_text": {}},
        "Language": {"select": {"options": [
            {"name": "C#", "color": "purple"},
            {"name": "Python", "color": "blue"},
            {"name": "Go", "color": "green"},
            {"name": "SQL", "color": "orange"},
            {"name": "TypeScript", "color": "pink"},
            {"name": "Lua", "color": "red"},
        ]}},
    })
    s_db_id = snippets_db["id"]
    print(f"  ✓ Code Snippets: {s_db_id}")
    time.sleep(1)

    # 4. Add cross-relations
    print("  Linking cross-relations...")
    client.patch_database(p_db_id, {
        "Decisions": {"relation": {"database_id": d_db_id, "single_property": {}}}
    })
    time.sleep(REQUEST_DELAY)
    client.patch_database(p_db_id, {
        "Snippets": {"relation": {"database_id": s_db_id, "single_property": {}}}
    })
    time.sleep(REQUEST_DELAY)
    client.patch_database(d_db_id, {
        "Problem": {"relation": {"database_id": p_db_id, "single_property": {}}}
    })
    time.sleep(REQUEST_DELAY)
    client.patch_database(d_db_id, {
        "Snippets": {"relation": {"database_id": s_db_id, "single_property": {}}}
    })
    time.sleep(REQUEST_DELAY)
    client.patch_database(s_db_id, {
        "Problems": {"relation": {"database_id": p_db_id, "single_property": {}}}
    })
    time.sleep(REQUEST_DELAY)
    client.patch_database(s_db_id, {
        "Decisions": {"relation": {"database_id": d_db_id, "single_property": {}}}
    })
    print("  ✓ Cross-relations configured")

    cfg = {
        "problems_db_id": p_db_id,
        "decisions_db_id": d_db_id,
        "snippets_db_id": s_db_id,
    }
    save_config(cfg)
    print(f"\nConfig saved → {CONFIG_PATH}")
    return cfg

# ---------------------------------------------------------------------------
# Sync Helpers
# ---------------------------------------------------------------------------

def get_id_map(client: NotionClient, db_id: str) -> dict[str, str]:
    """Return {kb_id: notion_page_id} for all pages in a database."""
    result = {}
    for page in client.query_database(db_id):
        rich = page.get("properties", {}).get("KB ID", {}).get("rich_text", [])
        if rich:
            kb_id = rich[0].get("plain_text", "")
            if kb_id:
                result[kb_id] = page["id"]
    return result

def upsert_page(
    client: NotionClient,
    db_id: str,
    kb_id: str,
    properties: dict,
    blocks: list,
    existing_map: dict,
    rebuild_body: bool = False,
) -> tuple[str, str]:
    if kb_id in existing_map:
        page_id = existing_map[kb_id]
        client.update_page(page_id, properties)
        if rebuild_body and blocks:
            time.sleep(REQUEST_DELAY)
            try:
                client.replace_body(page_id, blocks)
                return page_id, "rebuilt"
            except RuntimeError as e:
                print(f"    ⚠ Body update failed: {str(e)[:120]}")
                print(f"    → Properties updated. Enable 'Update content' in your Notion integration settings.")
                return page_id, "props only"
        return page_id, "skipped"
    else:
        page = client.create_page(db_id, properties, blocks)
        return page["id"], "created"

# ---------------------------------------------------------------------------
# Per-Type Sync
# ---------------------------------------------------------------------------

def sync_problems(client: NotionClient, cfg: dict, rebuild_body: bool = False) -> dict[str, str]:
    db_id = cfg["problems_db_id"]
    existing = get_id_map(client, db_id)
    hashes = load_hashes()
    id_map = {}

    all_problems = {p.id: p for p in load_problems()}
    for path in sorted((KB_ROOT / "problems").glob("P*.md")):
        p = all_problems.get(path.stem[:4])
        if not p:
            continue
        current_hash = file_hash(path)
        changed = content_changed(p.id, current_hash, hashes)
        props = {
            "Name": prop_title(p.title),
            "KB ID": prop_rich_text(p.id),
            "Severity": prop_select(p.severity),
            "Tags": prop_multi_select(p.tags),
            "Date": prop_date(p.date),
        }
        # Build blocks for: new pages always, existing pages only when rebuild requested
        need_blocks = p.id not in existing or (rebuild_body and changed)
        blocks = build_problem_blocks(p) if need_blocks else []
        page_id, action = upsert_page(client, db_id, p.id, props, blocks, existing, rebuild_body and changed)
        id_map[p.id] = page_id
        if action != "props only":
            hashes[p.id] = current_hash
        print(f"  [{action:8s}] {p.id} — {p.title[:58]}")
        time.sleep(REQUEST_DELAY)

    save_hashes(hashes)
    return id_map

def sync_decisions(client: NotionClient, cfg: dict, rebuild_body: bool = False) -> dict[str, str]:
    db_id = cfg["decisions_db_id"]
    existing = get_id_map(client, db_id)
    hashes = load_hashes()
    id_map = {}

    all_decisions = {d.id: d for d in load_decisions()}
    for path in sorted((KB_ROOT / "decisions").glob("D*.md")):
        d = all_decisions.get(path.stem[:4])
        if not d:
            continue
        current_hash = file_hash(path)
        changed = content_changed(d.id, current_hash, hashes)
        props = {
            "Name": prop_title(d.title),
            "KB ID": prop_rich_text(d.id),
            "Chosen Option": prop_rich_text(d.chosen_option),
            "Tags": prop_multi_select(d.tags),
        }
        need_blocks = d.id not in existing or (rebuild_body and changed)
        blocks = build_decision_blocks(d) if need_blocks else []
        page_id, action = upsert_page(client, db_id, d.id, props, blocks, existing, rebuild_body and changed)
        id_map[d.id] = page_id
        if action != "props only":
            hashes[d.id] = current_hash
        print(f"  [{action:8s}] {d.id} — {d.title[:58]}")
        time.sleep(REQUEST_DELAY)

    save_hashes(hashes)
    return id_map

def sync_snippets(client: NotionClient, cfg: dict, rebuild_body: bool = False) -> dict[str, str]:
    db_id = cfg["snippets_db_id"]
    existing = get_id_map(client, db_id)
    hashes = load_hashes()
    id_map = {}

    all_snippets = {s.id: s for s in load_snippets()}
    for snippet_dir in sorted((KB_ROOT / "snippets").glob("S*")):
        s = all_snippets.get(snippet_dir.name[:4])
        if not s:
            continue
        current_hash = snippet_hash(snippet_dir)
        changed = content_changed(s.id, current_hash, hashes)
        lang_display = LANG_DISPLAY.get(s.language.lower(), s.language.upper()) if s.language else ""
        props = {
            "Name": prop_title(s.title),
            "KB ID": prop_rich_text(s.id),
            "Language": prop_select(lang_display),
        }
        need_blocks = s.id not in existing or (rebuild_body and changed)
        blocks = build_snippet_blocks(s) if need_blocks else []
        page_id, action = upsert_page(client, db_id, s.id, props, blocks, existing, rebuild_body and changed)
        id_map[s.id] = page_id
        if action != "props only":
            hashes[s.id] = current_hash
        print(f"  [{action:8s}] {s.id} — {s.title[:58]}")
        time.sleep(REQUEST_DELAY)

    save_hashes(hashes)
    return id_map

def sync_relations(
    client: NotionClient,
    cfg: dict,
    p_map: dict[str, str],
    d_map: dict[str, str],
    s_map: dict[str, str],
) -> None:
    print("\n[Relations]")

    for p in load_problems():
        page_id = p_map.get(p.id)
        if not page_id:
            continue
        props = {}
        dec_ids = [d_map[d] for d in p.related_decisions if d in d_map]
        snip_ids = [s_map[s] for s in p.related_snippets if s in s_map]
        if dec_ids:
            props["Decisions"] = prop_relation(dec_ids)
        if snip_ids:
            props["Snippets"] = prop_relation(snip_ids)
        if props:
            client.update_page(page_id, props)
            print(f"  {p.id}: {len(dec_ids)} decisions, {len(snip_ids)} snippets")
        time.sleep(REQUEST_DELAY)

    for d in load_decisions():
        page_id = d_map.get(d.id)
        if not page_id:
            continue
        props = {}
        if d.problem_id and d.problem_id in p_map:
            props["Problem"] = prop_relation([p_map[d.problem_id]])
        snip_ids = [s_map[s] for s in d.related_snippets if s in s_map]
        if snip_ids:
            props["Snippets"] = prop_relation(snip_ids)
        if props:
            client.update_page(page_id, props)
            print(f"  {d.id}: problem={d.problem_id or '—'}, {len(snip_ids)} snippets")
        time.sleep(REQUEST_DELAY)

    for s in load_snippets():
        page_id = s_map.get(s.id)
        if not page_id:
            continue
        props = {}
        prob_ids = [p_map[p] for p in s.related_problems if p in p_map]
        dec_ids = [d_map[d] for d in s.related_decisions if d in d_map]
        if prob_ids:
            props["Problems"] = prop_relation(prob_ids)
        if dec_ids:
            props["Decisions"] = prop_relation(dec_ids)
        if props:
            client.update_page(page_id, props)
            print(f"  {s.id}: {len(prob_ids)} problems, {len(dec_ids)} decisions")
        time.sleep(REQUEST_DELAY)

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Sync knowledge-base/ P/D/S records to Notion",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--setup", action="store_true", help="Create Notion databases (first run only)")
    parser.add_argument("--db", choices=["p", "d", "s"], help="Sync one DB: p=problems, d=decisions, s=snippets")
    parser.add_argument("--rebuild-body", action="store_true",
                        help="Re-render body for changed pages (requires 'Update content' capability in Notion integration)")
    parser.add_argument("--no-relations", action="store_true", help="Skip relation linking pass")
    args = parser.parse_args()

    token = os.environ.get("NOTION_TOKEN")
    if not token:
        print("Error: NOTION_TOKEN not set")
        print("  export NOTION_TOKEN=secret_xxx")
        sys.exit(1)

    client = NotionClient(token)

    # --- Setup ---
    if args.setup:
        parent_id = os.environ.get("NOTION_PARENT_PAGE_ID")
        if not parent_id:
            print("Error: NOTION_PARENT_PAGE_ID not set")
            print("  1. Create an integration at https://www.notion.so/profile/integrations")
            print("  2. Share a Notion page with the integration")
            print("  3. Copy that page's ID and set: export NOTION_PARENT_PAGE_ID=<id>")
            sys.exit(1)
        setup_databases(client, parent_id)
        print("\nSetup complete. Now run without --setup to sync records.")
        return

    cfg = load_config()
    if not cfg:
        print("Error: No config found. Run --setup first:")
        print("  export NOTION_TOKEN=secret_xxx")
        print("  export NOTION_PARENT_PAGE_ID=<page_id>")
        print("  python sync/notion_kb_sync.py --setup")
        sys.exit(1)

    rebuild = args.rebuild_body
    p_map = d_map = s_map = {}

    if not args.db or args.db == "p":
        print("\n[Problems]")
        p_map = sync_problems(client, cfg, rebuild)

    if not args.db or args.db == "d":
        print("\n[Decisions]")
        d_map = sync_decisions(client, cfg, rebuild)

    if not args.db or args.db == "s":
        print("\n[Snippets]")
        s_map = sync_snippets(client, cfg, rebuild)

    if not args.db and not args.no_relations:
        # Reload full maps for any DB not just synced
        if not p_map:
            p_map = get_id_map(client, cfg["problems_db_id"])
        if not d_map:
            d_map = get_id_map(client, cfg["decisions_db_id"])
        if not s_map:
            s_map = get_id_map(client, cfg["snippets_db_id"])
        sync_relations(client, cfg, p_map, d_map, s_map)

    total = len(p_map) + len(d_map) + len(s_map)
    print(f"\nSync complete — {total} pages processed.")

if __name__ == "__main__":
    main()
