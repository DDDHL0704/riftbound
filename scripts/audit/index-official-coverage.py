#!/usr/bin/env python3
"""Inventory every official document page and text block without inheriting old pass claims.

Extraction is coverage bookkeeping, not semantic verification. Review judgments
and executable evidence belong in a separate file so regeneration cannot erase them.
"""
import hashlib
import json
from pathlib import Path
import re
import unicodedata

ROOT = Path(__file__).resolve().parents[2]
DATE = "2026-10-08"
source = ROOT / "data/official/upstream" / DATE
manifest = json.loads((source / "rules-manifest.json").read_text())
destination = ROOT / "docs/evidence/rules-reaudit-2026-10-08"
destination.mkdir(parents=True, exist_ok=True)
documents, records = [], []
clause_start = re.compile(r"^\s*(\d{3}(?:\.(?:\d+|[a-z]))*)\.\s+")
for ordinal, rule in enumerate(manifest["rules"], 1):
    pdf = ROOT / rule["localPath"]
    assert hashlib.sha256(pdf.read_bytes()).hexdigest() == rule["sha256"]
    pages = pdf.with_suffix(".txt").read_text().split("\f")
    if not pages[-1].strip():
        pages.pop()
    document_id = f"DOC-{ordinal:02}"
    document = {"id": document_id, "title": rule["ruleName"], "url": rule["ruleDocUrl"],
                "sha256": rule["sha256"], "updated": rule.get("updateDate"), "pages": []}
    for page_number, raw in enumerate(pages, 1):
        text = unicodedata.normalize("NFKC", raw)
        # Keep every paragraph, including unnumbered examples, exceptions,
        # errata and FAQ answers; question-only extraction loses rulings.
        blocks = [block.strip() for block in re.split(r"\n\s*\n", text) if block.strip()]
        ids = []
        for number, block in enumerate(blocks, 1):
            rid = f"{document_id}-P{page_number:03}-B{number:03}"
            ids.append(rid)
            clauses = [match[1] for line in block.splitlines() if (match := clause_start.match(line))]
            records.append({"id": rid, "document": document_id, "page": page_number,
                            "clauseAnchors": clauses, "text": block,
                            "reviewStatus": "NOT_REVIEWED", "implementation": [], "tests": []})
        document["pages"].append({"page": page_number, "blocks": ids,
                                  "requiresVisualReview": not blocks})
    documents.append(document)
result = {"date": DATE, "scope": "All official CN core rules, FAQs, judge QAs, errata, tournament rules and banlist",
          "source": "https://www.playloltcg.com/rules.html", "documents": documents,
          "records": records,
          "coveragePolicy": "Only explicit current-source judgments with behavioral evidence establish coverage. Old registry/test/report status is not inherited."}
(destination / "official-corpus.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
summary = {"documents": len(documents), "pages": sum(len(doc["pages"]) for doc in documents),
           "textBlocks": len(records), "uniqueCoreClauses": len({c for r in records if r["document"] == "DOC-01" for c in r["clauseAnchors"]}),
           "verifiedByIndexing": 0}
(destination / "coverage-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
print(json.dumps(summary, ensure_ascii=False))
