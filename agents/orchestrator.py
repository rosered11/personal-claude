import concurrent.futures

from .architect import ArchitectAgent
from .decision_synthesizer import DecisionSynthesizerAgent
from .kb_search import KBSearchAgent
from .kb_writer import KBWriterAgent
from .lens_determiner import LensDeterminerAgent
from .problem_analyst import ProblemAnalystAgent


class Orchestrator:
    def run(self, raw_markdown: str) -> dict:
        print("  [1/6] Analyzing problem...")
        problem = ProblemAnalystAgent().run(raw_markdown)
        print(f"        title : {problem['title']}")
        print(f"        tags  : {', '.join(problem.get('tags', []))}")

        print("  [2/6] Searching knowledge base...")
        kb_context = KBSearchAgent().run(problem.get("tags", []))
        related_count = (
            len(kb_context["related_problems"])
            + len(kb_context["related_decisions"])
            + len(kb_context["related_snippets"])
        )
        print(f"        {related_count} related items found")

        print("  [3/6] Determining architectural lenses...")
        lenses = LensDeterminerAgent().run(problem)["lenses"]
        print(f"        {lenses[0]['name']}  vs  {lenses[1]['name']}")

        print("  [4/6] Running architect analyses (parallel)...")
        arch_a_agent = ArchitectAgent(lenses[0])
        arch_b_agent = ArchitectAgent(lenses[1])

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            fut_a = pool.submit(arch_a_agent.run, problem, kb_context)
            fut_b = pool.submit(arch_b_agent.run, problem, kb_context)
            arch_a = fut_a.result()
            arch_b = fut_b.result()

        print(f"        A: {arch_a['option_name']}")
        print(f"        B: {arch_b['option_name']}")

        print("  [5/6] Synthesizing decision...")
        decision = DecisionSynthesizerAgent().run(problem, arch_a, arch_b)
        print(f"        chosen: {decision['chosen_option']}")

        print("  [6/6] Writing to knowledge base...")
        kb = KBWriterAgent().run(problem, arch_a, arch_b, decision)
        ids = f"{kb['problem_id']}, {kb['decision_id']}"
        if kb["snippet_id"]:
            ids += f", {kb['snippet_id']}"
        print(f"        {ids}")

        return {
            "problem": problem,
            "lenses": lenses,
            "arch_a": arch_a,
            "arch_b": arch_b,
            "decision": decision,
            "kb": kb,
        }
