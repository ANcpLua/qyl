#!/usr/bin/env python3
"""
CSAM Thesis Feedback Gate

Deterministic quality-feedback loop for an AI-written bachelor-paper draft.
It does not replace a human examiner and it cannot guarantee a real 95/95.
It converts a thesis draft into:

- a rubric-like JSON assessment input,
- a Markdown feedback report,
- prioritized revision actions,
- optional OPA/Rego execution if opa is installed.

Designed for Markdown, LaTeX, or plain-text drafts.
No third-party Python dependencies are required.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path
from statistics import mean
from typing import Any, Iterable


TARGET_TOTAL_POINTS = 95.0
RAW_TOTAL_POINTS = 100.0  # The visible rubric rows sum to 100; this tool reports on the user's 95-point target scale.
SCALE_TO_TARGET = TARGET_TOTAL_POINTS / RAW_TOTAL_POINTS

CRITERIA = {
    "problem_definition": {
        "title": "Problem Statement and Objective Definition",
        "max_points": 5,
        "checks": {
            "problem_clear_precise": [r"problem statement", r"problem\b", r"gap\b", r"challenge\b"],
            "scientific_context_present": [r"state of the art", r"literature", r"research", r"existing work", r"prior work", r"background"],
            "objective_explicit": [r"objective", r"goal", r"aim", r"this thesis", r"research question", r"RQ\d"],
            "expected_result_measurable": [r"expected result", r"outcome", r"prototype", r"software", r"framework", r"measurement", r"metric"],
        },
    },
    "methodology_and_approach": {
        "title": "Methodology and Solution Approach",
        "max_points": 40,
        "checks": {
            "ideal_solution_before_constraints": [
                r"ideal solution", r"ideal architecture", r"independent(?:ly)? of .*constraints", r"independent(?:ly)? of .*technolog"
            ],
            "requirements_engineering_present": [r"requirement", r"functional", r"non-functional", r"user stor", r"quality criteria"],
            "method_selection_justified": [r"method selection", r"methodology", r"why", r"selected because", r"rationale", r"justification"],
            "alternatives_discussed": [r"alternative", r"compared", r"not selected", r"instead of", r"considered"],
            "development_process_phased": [r"phase", r"iteration", r"incremental", r"development process", r"input", r"output"],
            "technology_choice_argued": [r"technology selection", r"tool selection", r"chosen", r"selected", r"rationale"],
            "testing_methodology_defined": [r"test", r"testing methodology", r"unit test", r"integration test", r"system test", r"validation"],
            "implementation_reproducible": [r"reproducible", r"replicable", r"version", r"configuration", r"CI", r"harness", r"script"],
        },
    },
    "results_and_discussion": {
        "title": "Results and Discussion",
        "max_points": 40,
        "checks": {
            "artifact_evaluated_against_goals": [r"evaluat", r"against.*goal", r"quality criteria", r"research question", r"results"],
            "empirical_results_reported": [r"result", r"measurement", r"benchmark", r"table", r"metric", r"sample", r"run", r"latency", r"performance"],
            "analysis_interprets_results": [r"indicat", r"show", r"suggest", r"interpret", r"analysis", r"meaning"],
            "limitations_critical": [r"limitation", r"threats to validity", r"not generalizable", r"scope", r"weakness"],
            "generalizability_discussed": [r"generaliz", r"transfer", r"another context", r"production", r"broader application"],
            "improvements_future_work": [r"future work", r"improvement", r"enhancement", r"extension"],
            "raw_or_appendix_evidence_present": [r"appendix", r"source code", r"raw data", r"command output", r"artifact", r"listing"],
            "no_hypothetical_only_evaluation": [r"measured", r"executed", r"tested", r"collected", r"ran", r"observed"],
        },
    },
    "structure_and_organization": {
        "title": "Structure and Organization",
        "max_points": 5,
        "checks": {
            "four_main_parts_present": [r"introduction", r"methodology", r"solution", r"discussion"],
            "abstract_and_keywords_present": [r"abstract", r"keywords"],
            "bibliography_present": [r"bibliography", r"references"],
            "appendix_present": [r"appendix", r"anhang"],
            "figures_tables_integrated": [r"figure", r"table", r"listing", r"diagram"],
        },
    },
    "style_and_expression": {
        "title": "Style and Expression",
        "max_points": 5,
        "checks": {
            "technical_language_precise": [r"defined as", r"operational definition", r"criteria", r"threshold", r"metric"],
            "neutral_academic_tone": [r"results", r"indicat", r"limitations", r"evidence"],
            "gender_sensitive_language_compliant": [r"students", r"users", r"developers", r"administrators", r"participants"],
            "low_lexical_trap_density": [],
        },
    },
    "citations_and_sources": {
        "title": "Citation Rules and References",
        "max_points": 5,
        "checks": {
            "bibliography_present": [r"bibliography", r"references"],
            "citation_density_sufficient": [],
            "current_sources_indicated": [r"accessed", r"visited", r"2024", r"2025", r"2026"],
            "source_quality_mixed": [r"NIST", r"CIS", r"ACM", r"IEEE", r"Springer", r"documentation", r"arXiv", r"Kubernetes"],
        },
    },
}

GUIDELINE_CHECKS = {
    "non_trivial_artifact_documented": {
        "must": True,
        "patterns": [r"artifact", r"prototype", r"software", r"framework", r"implementation", r"tool", r"system"],
        "claim": "A non-trivial engineering artifact is described.",
    },
    "need_or_use_case_established": {
        "must": True,
        "patterns": [r"need", r"use case", r"motivation", r"practical", r"industry", r"work", r"problem"],
        "claim": "A concrete or potential need is established.",
    },
    "novelty_and_standard_software_gap_argued": {
        "must": True,
        "patterns": [r"does not already exist", r"standard software", r"existing solution", r"gap", r"alternative", r"insufficient"],
        "claim": "The paper argues why existing or standard solutions are insufficient.",
    },
    "structured_development_process": {
        "must": True,
        "patterns": [r"development process", r"phase", r"requirements", r"specification", r"implementation", r"testing"],
        "claim": "The development process is described in structured phases.",
    },
    "methods_tools_alternatives_justified": {
        "must": True,
        "patterns": [r"method selection", r"technology selection", r"alternative", r"selected because", r"rationale", r"why"],
        "claim": "Methods and tools are selected through argued alternatives.",
    },
    "artifact_evaluated_with_quality_criteria": {
        "must": True,
        "patterns": [r"evaluation", r"quality criteria", r"performance", r"usability", r"reliability", r"stability", r"benchmark"],
        "claim": "The artifact is evaluated against goals or quality criteria.",
    },
    "main_structure_covered": {
        "must": True,
        "patterns": [r"introduction", r"methodology", r"solution", r"discussion"],
        "claim": "The paper covers the standard four-part structure.",
    },
    "appendix_self_containment_balance": {
        "must": False,
        "patterns": [r"appendix", r"main part", r"self-contained", r"source code", r"raw data"],
        "claim": "Appendix material is present while the main text remains understandable.",
    },
    "discussion_critical_not_summary_only": {
        "must": True,
        "patterns": [r"limitation", r"weakness", r"potential", r"improvement", r"future work", r"generaliz"],
        "claim": "Discussion critically addresses usefulness, limits, improvements, and transferability.",
    },
}

LEXICAL_TRAPS = {
    r"\bsignificant\b(?!\s*(?:statistically|statistical|p\s*<\s*0\.0[1-5]))": "Use 'substantial', 'measurable', or report statistical significance explicitly.",
    r"\b(prove|proved|proves|proving)\b": "Prefer 'indicate', 'demonstrate', or 'support'.",
    r"\brandom\b(?!\s*(?:seed|number|distribution|generator|sample))": "Use 'arbitrary', 'uncontrolled', or specify the sampling procedure.",
    r"\bunique\b": "Avoid unless uniqueness is proven against an explicit comparison set.",
    r"\b(very|really|a lot|big)\b": "Replace vague intensifiers with measured quantities.",
    r"\b(obviously|clearly|of course)\b": "Academic writing should show evidence instead of assuming obviousness.",
    r"\bin order to\b": "Use 'to'.",
    r"\bdue to the fact that\b": "Use 'because'.",
    r"\bstate of the art\b": "Use only when the actual state of research is surveyed.",
    r"\bnovel\b": "Use only when novelty has been argued against existing solutions.",
    r"\b(scalable|robust|efficient|performant)\b(?!\s*(?:up to|to|by|with|under|for|against))": "Tie quality claims to metrics or conditions.",
    r"\bdata shows\b": "Use 'the data indicate' or 'the measurements suggest'.",
    r"\b(literally|basically|essentially)\b": "Usually unnecessary in academic style.",
}

SECTION_SYNONYMS = {
    "introduction": ["introduction", "einleitung", "background", "motivation"],
    "methodology": ["methodology", "methodische", "methods", "method", "vorgehensweise"],
    "solution": ["solution", "lösungsansatz", "implementation", "design", "artifact", "system architecture"],
    "discussion": ["discussion", "diskussion", "conclusion", "threats to validity", "limitations"],
    "appendix": ["appendix", "anhang", "source code", "raw data"],
    "bibliography": ["bibliography", "references", "literatur"],
}


@dataclass
class Evidence:
    section: str
    claim: str
    quote: str
    confidence: float
    source_location: str = "detected_text"

    def to_json(self) -> dict[str, Any]:
        return {
            "section": self.section,
            "claim": self.claim,
            "quote": self.quote[:360],
            "confidence": round(self.confidence, 3),
            "source_location": self.source_location,
        }


@dataclass
class CheckResult:
    found: bool
    score: float
    evidence: list[Evidence] = field(default_factory=list)


@dataclass
class Document:
    path: Path
    raw: str
    text: str
    lines: list[str]
    sentences: list[str]
    headings: list[tuple[int, str, int]]


def read_document(path: Path) -> Document:
    suffix = path.suffix.lower()
    if suffix == ".pdf":
        raw = pdf_to_text(path)
        text = raw
    else:
        raw = path.read_text(encoding="utf-8", errors="replace")
        text = raw
    if suffix == ".tex":
        raw = expand_latex_includes(path, raw)
        text = latex_to_text(raw)
    elif suffix == ".md":
        text = markdown_to_text(raw)
    text = normalize_text(text)
    headings = extract_headings(raw, suffix)
    sentences = split_sentences(text)
    return Document(path=path, raw=raw, text=text, lines=text.splitlines(), sentences=sentences, headings=headings)


def expand_latex_includes(path: Path, raw: str, seen: set[Path] | None = None) -> str:
    """Inline local LaTeX chapter files so the gate scores the thesis body."""
    if seen is None:
        seen = set()
    base_dir = path.parent
    seen.add(path.resolve())

    def replace_include(match: re.Match[str]) -> str:
        command = match.group(1)
        target = match.group(2).strip()
        if command == "bibliography":
            return match.group(0)
        target_path = Path(target)
        if not target_path.suffix:
            target_path = target_path.with_suffix(".tex")
        include_path = (base_dir / target_path).resolve()
        if include_path in seen or not include_path.exists():
            return match.group(0)
        include_raw = include_path.read_text(encoding="utf-8", errors="replace")
        return "\n" + expand_latex_includes(include_path, include_raw, seen) + "\n"

    return re.sub(r"\\(input|include|subfile|bibliography)\{([^}]+)\}", replace_include, raw)


def pdf_to_text(path: Path) -> str:
    pdftotext = shutil.which("pdftotext")
    if not pdftotext:
        raise RuntimeError("PDF input requires the 'pdftotext' command. Convert the PDF to .txt first or install poppler.")
    proc = subprocess.run([pdftotext, "-layout", str(path), "-"], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if proc.returncode != 0:
        raise RuntimeError(f"pdftotext failed: {proc.stderr}")
    return proc.stdout


def latex_to_text(raw: str) -> str:
    without_comments = re.sub(r"(?<!\\)%.*", "", raw)
    without_code_envs = re.sub(r"\\begin\{(?:lstlisting|verbatim|minted)\}.*?\\end\{(?:lstlisting|verbatim|minted)\}", " [code listing] ", without_comments, flags=re.S)
    keep_section_names = re.sub(r"\\(?:chapter|section|subsection|subsubsection)\*?\{([^}]*)\}", r"\n\1\n", without_code_envs)
    keep_cites = re.sub(r"\\(?:cite|parencite|textcite|autocite)\*?(?:\[[^]]*\])?\{([^}]*)\}", r" [citation:\1] ", keep_section_names)
    no_commands = re.sub(r"\\[a-zA-Z]+\*?(?:\[[^]]*\])?(?:\{([^{}]*)\})?", lambda m: m.group(1) or " ", keep_cites)
    no_braces = no_commands.replace("{", " ").replace("}", " ")
    return no_braces


def markdown_to_text(raw: str) -> str:
    text = re.sub(r"```.*?```", " [code listing] ", raw, flags=re.S)
    text = re.sub(r"`([^`]*)`", r"\1", text)
    text = re.sub(r"!\[[^]]*\]\([^)]*\)", " [figure] ", text)
    text = re.sub(r"\[([^]]+)\]\([^)]*\)", r"\1", text)
    text = re.sub(r"^#+\s+", "", text, flags=re.M)
    return text


def normalize_text(text: str) -> str:
    text = text.replace("\u00ad", "")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def split_sentences(text: str) -> list[str]:
    chunks = re.split(r"(?<=[.!?])\s+(?=[A-Z0-9])|\n+", text)
    return [c.strip() for c in chunks if len(c.strip()) > 25]


def extract_headings(raw: str, suffix: str) -> list[tuple[int, str, int]]:
    headings: list[tuple[int, str, int]] = []
    for i, line in enumerate(raw.splitlines(), start=1):
        stripped = line.strip()
        if suffix == ".md":
            m = re.match(r"^(#{1,6})\s+(.+)$", stripped)
            if m:
                headings.append((len(m.group(1)), m.group(2).strip(), i))
        elif suffix == ".tex":
            m = re.match(r"\\(chapter|section|subsection|subsubsection)\*?\{(.+)\}", stripped)
            if m:
                level = {"chapter": 1, "section": 2, "subsection": 3, "subsubsection": 4}[m.group(1)]
                headings.append((level, m.group(2).strip(), i))
        else:
            m = re.match(r"^(\d+(?:\.\d+)*)\s+(.+)$", stripped)
            if m and len(stripped) < 100:
                headings.append((m.group(1).count(".") + 1, m.group(2).strip(), i))
            else:
                # Common PDF text extraction pattern: '2 Methodology' or '4.3 Broader Application'.
                m2 = re.match(r"^(\d+(?:\.\d+)*)\s+([A-Z][A-Za-z0-9 &/.,:;()\-]{3,90})$", stripped)
                if m2:
                    headings.append((m2.group(1).count(".") + 1, m2.group(2).strip(), i))
    return headings


def word_count(text: str) -> int:
    return len(re.findall(r"\b[A-Za-zÀ-ÖØ-öø-ÿ0-9][A-Za-zÀ-ÖØ-öø-ÿ0-9'\-]*\b", text))


def find_first_evidence(doc: Document, patterns: Iterable[str], claim: str, section_hint: str = "Document") -> list[Evidence]:
    compiled = [re.compile(p, re.I) for p in patterns]
    out: list[Evidence] = []
    for sent in doc.sentences:
        hits = sum(1 for p in compiled if p.search(sent))
        if hits:
            out.append(Evidence(section=guess_section_for_sentence(doc, sent, section_hint), claim=claim, quote=sent, confidence=min(0.55 + 0.15 * hits, 0.98)))
            if len(out) >= 2:
                break
    return out


def guess_section_for_sentence(doc: Document, sentence: str, default: str) -> str:
    lower_sentence = sentence.lower()
    # Cheap heading hint by nearby keywords.
    for section, aliases in SECTION_SYNONYMS.items():
        if any(alias in lower_sentence for alias in aliases):
            return section.title()
    return default


def check_patterns(doc: Document, patterns: list[str], claim: str, min_hits: int = 1) -> CheckResult:
    if not patterns:
        return CheckResult(False, 0.0, [])
    hits = 0
    for p in patterns:
        if re.search(p, doc.text, re.I):
            hits += 1
    score = min(hits / max(min_hits, 1), 1.0)
    evidence = find_first_evidence(doc, patterns, claim) if hits else []
    return CheckResult(found=hits >= min_hits, score=score, evidence=evidence)


def citation_count(raw: str) -> int:
    latex_cites = len(re.findall(r"\\(?:cite|parencite|textcite|autocite)\*?(?:\[[^]]*\])?\{[^}]+\}", raw))
    bracket_cites = len(re.findall(r"\[(?:\d+|[A-Za-z][A-Za-z .,&-]+,?\s*\d{4})\]", raw))
    author_year = len(re.findall(r"\([A-Z][A-Za-z-]+(?:\s+et\s+al\.)?,\s*20\d{2}\)", raw))
    return latex_cites + bracket_cites + author_year


def count_figures_tables(raw: str) -> tuple[int, int, int]:
    figures = len(re.findall(r"\\begin\{figure\}|^Figure\s+\d+|!\[", raw, re.I | re.M))
    tables = len(re.findall(r"\\begin\{table\}|^Table\s+\d+|\|\s*[-:]+\s*\|", raw, re.I | re.M))
    listings = len(re.findall(r"\\begin\{lstlisting\}|^Listing\s+\d+|```", raw, re.I | re.M))
    return figures, tables, listings


def artifact_consistency_findings(doc: Document) -> list[dict[str, Any]]:
    """Detect a few high-signal contradictions between described artifact code and claims.

    This is intentionally conservative. It is not a full code reviewer; it catches patterns that
    commonly invalidate an otherwise polished AI-generated thesis.
    """
    raw = doc.raw
    text = doc.text
    findings: list[dict[str, Any]] = []

    calls_trivy = re.search(r'exec\.Command(?:Context)?\([^)]*["\']trivy["\']', raw, re.I) is not None
    dockerfile_alpine = re.search(r'FROM\s+alpine', raw, re.I) is not None
    installs_trivy = re.search(r'(apk\s+add[^\n]*trivy|curl[^\n]*(?:aquasecurity|trivy)|wget[^\n]*(?:aquasecurity|trivy)|COPY[^\n]*trivy)', raw, re.I) is not None
    if calls_trivy and dockerfile_alpine and not installs_trivy:
        findings.append({
            "severity": "high",
            "id": "trivy_called_but_not_installed",
            "message": "The artifact calls the `trivy` executable but the shown Alpine Dockerfile does not install or copy Trivy. This can invalidate implementation and evaluation claims.",
        })

    claims_port_registry = re.search(r'(localhost:5000|references with ports|port-qualified registr)', text, re.I) is not None
    split_colon_first = re.search(r'image\s*=\s*strings\.Split\(image\s*,\s*["\']:["\']\)\[0\]', raw, re.I) is not None
    if claims_port_registry and split_colon_first:
        findings.append({
            "severity": "high",
            "id": "port_registry_claim_conflicts_with_parser",
            "message": "The text claims port-qualified registries are handled, but the shown parser splits the image at ':' before parsing the registry. This likely breaks references such as localhost:5000/image.",
        })

    claims_load_testing = re.search(r'under load testing|load testing|concurrent admission', text, re.I) is not None
    concrete_load_metric = re.search(r'(requests per second|req/s|p95|p99|throughput|concurrent\s+requests|\d+\s+concurrent|\d+\s*req)', text, re.I) is not None
    if claims_load_testing and not concrete_load_metric:
        findings.append({
            "severity": "medium",
            "id": "load_testing_claim_without_load_metric",
            "message": "The paper claims load-testing behavior but no concrete load metric was detected.",
        })

    production_claim = re.search(r'production(?:-| )?(?:ready|suitable|deployment|environment)', text, re.I) is not None
    limitations = re.search(r'limitation|threats to validity|not generalizable|future work|scope', text, re.I) is not None
    if production_claim and not limitations:
        findings.append({
            "severity": "medium",
            "id": "production_claim_without_limitations",
            "message": "The paper makes production-suitability claims without a detected limitation/threats-to-validity discussion.",
        })

    return findings


def lexical_trap_findings(text: str) -> list[dict[str, Any]]:
    findings: list[dict[str, Any]] = []
    for pattern, advice in LEXICAL_TRAPS.items():
        for m in re.finditer(pattern, text, flags=re.I):
            start = max(0, m.start() - 80)
            end = min(len(text), m.end() + 100)
            findings.append({
                "token": m.group(0),
                "advice": advice,
                "context": re.sub(r"\s+", " ", text[start:end]).strip(),
            })
            if len(findings) >= 80:
                return findings
    return findings


def section_presence(doc: Document) -> dict[str, bool]:
    text = "\n".join(h[1].lower() for h in doc.headings) + "\n" + doc.text[:5000].lower()
    return {name: any(alias in text for alias in aliases) for name, aliases in SECTION_SYNONYMS.items()}


def penalty_for_hypothetical_evaluation(doc: Document) -> float:
    # Heavy penalty when the paper describes an evaluation framework but does not execute it.
    # This is the main difference between a good conceptual paper and a 95/95 engineering thesis.
    hard_patterns = [
        r"rather than a measured comparison",
        r"not constitute empirical execution",
        r"no measured corpus",
        r"does not produce (?:measured|empirical)",
        r"empirical execution .* future work",
        r"future empirical",
        r"this paper does not answer",
        r"producing those measurements is the work of subsequent",
        r"the present paper does not produce",
    ]
    soft_patterns = [
        r"hypothetical",
        r"future work must produce",
        r"specified rather than measured",
        r"framework specifies",
        r"no cost measurement",
    ]
    hard_hits = sum(1 for p in hard_patterns if re.search(p, doc.text, re.I))
    soft_hits = sum(1 for p in soft_patterns if re.search(p, doc.text, re.I))
    return min(0.55, hard_hits * 0.12 + soft_hits * 0.05)


def compute_assessment(doc: Document, target: str) -> dict[str, Any]:
    wc = word_count(doc.text)
    figs, tabs, listings = count_figures_tables(doc.raw)
    cites = citation_count(doc.raw)
    traps = lexical_trap_findings(doc.text)
    artifact_findings = artifact_consistency_findings(doc)
    artifact_penalty = min(0.25, sum(0.12 if f["severity"] == "high" else 0.05 for f in artifact_findings))
    presence = section_presence(doc)

    criteria_json: dict[str, Any] = {}
    points_by_criterion: dict[str, float] = {}
    semantic_findings: list[dict[str, Any]] = []

    for criterion_id, spec in CRITERIA.items():
        checks_json: dict[str, bool] = {}
        check_scores: list[float] = []
        evidences: list[dict[str, Any]] = []
        for check_id, patterns in spec["checks"].items():
            claim = f"Evidence for {spec['title']} / {check_id}."
            result: CheckResult
            if check_id == "low_lexical_trap_density":
                density = len(traps) / max(wc / 1000.0, 1)
                score = max(0.0, min(1.0, 1.0 - density / 8.0))
                result = CheckResult(score >= 0.75, score, [])
                if not result.found:
                    semantic_findings.append({"severity": "medium", "criterion": criterion_id, "message": f"High lexical-trap density: {density:.2f} findings per 1,000 words."})
            elif check_id == "citation_density_sufficient":
                density = cites / max(wc / 1000.0, 1)
                score = max(0.0, min(1.0, density / 8.0))
                result = CheckResult(score >= 0.65, score, [])
                if not result.found:
                    semantic_findings.append({"severity": "high", "criterion": criterion_id, "message": f"Citation density is low: {cites} citations over {wc} words."})
            elif check_id == "four_main_parts_present":
                score = sum(1 for k in ["introduction", "methodology", "solution", "discussion"] if presence.get(k)) / 4
                result = CheckResult(score >= 1.0, score, [])
            elif check_id == "abstract_and_keywords_present":
                score = (1 if re.search(r"abstract", doc.text, re.I) else 0) * 0.5 + (1 if re.search(r"keywords|schlagworte", doc.text, re.I) else 0) * 0.5
                result = CheckResult(score >= 1.0, score, [])
            elif check_id == "figures_tables_integrated":
                score = min((figs + tabs + listings) / 6, 1.0)
                result = CheckResult(score >= 0.65, score, [])
            elif check_id == "bibliography_present":
                score = 1.0 if presence.get("bibliography") else 0.0
                result = CheckResult(score >= 1.0, score, [])
            elif check_id == "appendix_present":
                score = 1.0 if presence.get("appendix") else 0.0
                result = CheckResult(score >= 1.0, score, [])
            else:
                result = check_patterns(doc, patterns, claim)

            checks_json[check_id] = result.found
            check_scores.append(result.score)
            evidences.extend(e.to_json() for e in result.evidence)
            if not result.found:
                semantic_findings.append({"severity": "medium", "criterion": criterion_id, "check": check_id, "message": f"Missing or weak evidence for {check_id}."})

        fulfillment = mean(check_scores) if check_scores else 0.0

        # Criterion-specific realistic penalties.
        if criterion_id == "methodology_and_approach" and artifact_penalty:
            fulfillment = max(0.0, fulfillment - artifact_penalty * 0.35)
        if criterion_id == "results_and_discussion":
            fulfillment = max(0.0, fulfillment - penalty_for_hypothetical_evaluation(doc) - artifact_penalty)
        if criterion_id == "structure_and_organization":
            # FH guideline range, but not a hard failure for drafts.
            if wc < 4500 or wc > 7500:
                fulfillment *= 0.85
                semantic_findings.append({"severity": "medium", "criterion": criterion_id, "message": f"Main-text word-count proxy is {wc}; target is roughly 5,000–6,000 words."})
        if target == "95":
            # stricter target mode: a 95/95 attempt cannot rely on weak partial matches.
            fulfillment = max(0.0, min(1.0, fulfillment))

        raw_points = spec["max_points"] * fulfillment
        points = raw_points * SCALE_TO_TARGET
        points_by_criterion[criterion_id] = round(points, 2)
        criteria_json[criterion_id] = {
            "title": spec["title"],
            "raw_max_points": spec["max_points"],
            "max_points": round(spec["max_points"] * SCALE_TO_TARGET, 2),
            "fulfillment_percent": round(fulfillment * 100, 1),
            "raw_points": round(raw_points, 2),
            "points": round(points, 2),
            "checks": checks_json,
            "evidence": evidences[:4],
        }

    guideline_checks_json: dict[str, Any] = {}
    hard_gate_failures: list[str] = []
    for gid, spec in GUIDELINE_CHECKS.items():
        result = check_patterns(doc, spec["patterns"], spec["claim"])
        status = "satisfied" if result.found else "missing"
        guideline_checks_json[gid] = {
            "status": status,
            "must": bool(spec["must"]),
            "claim": spec["claim"],
            "evidence": [e.to_json() for e in result.evidence],
        }
        if spec["must"] and not result.found:
            hard_gate_failures.append(gid)

    for finding in artifact_findings:
        semantic_findings.append({
            "severity": finding["severity"],
            "criterion": "technical_consistency",
            "check": finding["id"],
            "message": finding["message"],
        })

    total_points = min(TARGET_TOTAL_POINTS, sum(points_by_criterion.values()))
    total_percent = total_points / TARGET_TOTAL_POINTS * 100.0

    high_artifact_failures = [f["id"] for f in artifact_findings if f["severity"] == "high"]
    hard_gate_failures.extend(high_artifact_failures)

    # 95-attempt hard gates: no hard missing guideline check, no artifact contradictions, and high evidence criterion.
    quality_band = "excellent_candidate" if total_points >= 90 and not hard_gate_failures else "revision_needed"
    if total_points >= 80 and quality_band != "excellent_candidate":
        quality_band = "strong_but_not_95_safe"
    elif total_points >= 57 and quality_band == "revision_needed":
        quality_band = "numeric_pass_but_revision_needed"

    return {
        "schema_version": "csam-thesis-feedback-gate.v1",
        "policy": {
            "target": target,
            "target_max_points": TARGET_TOTAL_POINTS,
            "raw_visible_rubric_sum": RAW_TOTAL_POINTS,
            "pass_threshold_points": 57,
            "excellent_threshold_points": 90,
            "require_guideline_gates": True,
            "require_empirical_execution_for_95": True,
        },
        "document": {
            "path": str(doc.path),
            "word_count_proxy": wc,
            "headings_detected": [{"level": level, "title": title, "line": line} for level, title, line in doc.headings[:80]],
            "citation_count": cites,
            "figures": figs,
            "tables": tabs,
            "listings": listings,
            "lexical_trap_count": len(traps),
        },
        "assessment": {
            "criteria": criteria_json,
            "guideline_checks": guideline_checks_json,
        },
        "report": {
            "total_points": round(total_points, 2),
            "total_percent": round(total_percent, 1),
            "points_by_criterion": points_by_criterion,
            "quality_band": quality_band,
            "hard_gate_failures": hard_gate_failures,
            "semantic_findings": semantic_findings[:120],
            "top_lexical_traps": traps[:25],
            "artifact_consistency_findings": artifact_findings,
        },
    }


def priority_actions(assessment: dict[str, Any]) -> list[str]:
    actions: list[str] = []
    criteria = assessment["assessment"]["criteria"]
    report = assessment["report"]

    low = sorted(criteria.items(), key=lambda kv: kv[1]["points"] / kv[1]["max_points"])
    for cid, c in low[:3]:
        missing = [k for k, v in c.get("checks", {}).items() if not v]
        if missing:
            actions.append(f"Strengthen {c['title']}: add direct evidence for {', '.join(missing[:4])}.")

    for gid in report.get("hard_gate_failures", []):
        actions.append(f"Resolve hard-gate guideline check: {gid}. Add a paragraph with evidence, alternatives, and measurable verification.")

    if criteria["results_and_discussion"]["fulfillment_percent"] < 95:
        actions.append("Execute a small empirical evaluation. Minimum credible loop: 3–5 cases, 3 repeated runs each, raw outputs in appendix, aggregate table in results, limitations in discussion.")

    if criteria["methodology_and_approach"]["fulfillment_percent"] < 95:
        actions.append("Add an explicit methodology table with phases, inputs, outputs, artifacts, and validation method. Include at least two rejected alternatives for methods and tools.")

    if assessment["document"]["lexical_trap_count"]:
        actions.append("Run lexical/style pass and replace unsupported adjectives with measured claims.")

    return actions[:10]


def write_markdown_report(assessment: dict[str, Any], out: Path) -> None:
    actions = priority_actions(assessment)
    doc = assessment["document"]
    report = assessment["report"]
    criteria = assessment["assessment"]["criteria"]

    lines: list[str] = []
    lines.append("# CSAM Thesis Feedback Report")
    lines.append("")
    lines.append(f"Document: `{doc['path']}`")
    lines.append(f"Estimated score: **{report['total_points']} / 95** ({report['total_percent']}%)")
    lines.append(f"Quality band: **{report['quality_band']}**")
    lines.append("")
    lines.append("## Score by criterion")
    lines.append("")
    lines.append("| Criterion | Points | Fulfillment |")
    lines.append("|---|---:|---:|")
    for cid, c in criteria.items():
        lines.append(f"| {c['title']} | {c['points']} / {c['max_points']} | {c['fulfillment_percent']}% |")
    lines.append("")
    lines.append("## Priority actions")
    lines.append("")
    if actions:
        for i, action in enumerate(actions, start=1):
            lines.append(f"{i}. {action}")
    else:
        lines.append("No high-priority action detected by the deterministic gate.")
    lines.append("")
    lines.append("## Detected document signals")
    lines.append("")
    lines.append(f"- Word-count proxy: {doc['word_count_proxy']}")
    lines.append(f"- Citations detected: {doc['citation_count']}")
    lines.append(f"- Figures / tables / listings: {doc['figures']} / {doc['tables']} / {doc['listings']}")
    lines.append(f"- Lexical traps detected: {doc['lexical_trap_count']}")
    lines.append("")
    lines.append("## Hard-gate failures")
    lines.append("")
    if report["hard_gate_failures"]:
        for g in report["hard_gate_failures"]:
            lines.append(f"- {g}")
    else:
        lines.append("None detected.")
    lines.append("")
    lines.append("## Semantic findings")
    lines.append("")
    for finding in report["semantic_findings"][:40]:
        criterion = finding.get("criterion", "general")
        check = finding.get("check", "")
        lines.append(f"- **{finding.get('severity', 'info')}** `{criterion}` `{check}`: {finding['message']}")
    if not report["semantic_findings"]:
        lines.append("None detected.")
    lines.append("")
    lines.append("## Lexical traps")
    lines.append("")
    for trap in report["top_lexical_traps"][:20]:
        lines.append(f"- `{trap['token']}`: {trap['advice']} Context: “{trap['context']}”")
    if not report["top_lexical_traps"]:
        lines.append("None detected.")

    out.write_text("\n".join(lines) + "\n", encoding="utf-8")


def maybe_run_opa(policy_path: Path, input_path: Path, out_dir: Path) -> dict[str, Any] | None:
    opa = shutil.which("opa")
    if not opa or not policy_path.exists():
        return None
    cmd = [opa, "eval", "-f", "json", "-d", str(policy_path), "-i", str(input_path), "data.bachelor.rubric.report"]
    proc = subprocess.run(cmd, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    result = {"command": cmd, "returncode": proc.returncode, "stdout": proc.stdout, "stderr": proc.stderr}
    (out_dir / "opa_eval.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate deterministic CSAM thesis rubric feedback.")
    parser.add_argument("thesis", type=Path, help="Path to thesis draft (.md, .tex, .txt).")
    parser.add_argument("--out-dir", "--out", type=Path, default=Path("reports"), help="Output directory.")
    parser.add_argument("--target", choices=["pass", "95"], default="95", help="Feedback strictness target.")
    parser.add_argument("--rego", type=Path, default=Path("policies/bachelor_rubric_semantic_expanded.rego"), help="Optional Rego policy path.")
    args = parser.parse_args(argv)

    if not args.thesis.exists():
        parser.error(f"file not found: {args.thesis}")

    args.out_dir.mkdir(parents=True, exist_ok=True)
    doc = read_document(args.thesis)
    assessment = compute_assessment(doc, target=args.target)

    json_path = args.out_dir / "rubric_input.generated.json"
    md_path = args.out_dir / "feedback.md"
    json_path.write_text(json.dumps(assessment, indent=2, ensure_ascii=False), encoding="utf-8")
    write_markdown_report(assessment, md_path)

    opa_result = maybe_run_opa(args.rego, json_path, args.out_dir)

    print(f"Score estimate: {assessment['report']['total_points']} / 95 ({assessment['report']['total_percent']}%)")
    print(f"Quality band: {assessment['report']['quality_band']}")
    print(f"Wrote: {json_path}")
    print(f"Wrote: {md_path}")
    if opa_result is None:
        print("OPA not executed: opa binary or policy file not found.")
    else:
        print(f"OPA return code: {opa_result['returncode']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
