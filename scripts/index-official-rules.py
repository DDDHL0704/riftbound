#!/usr/bin/env python3
"""Index each numbered CN core-rule clause; an index is not conformance evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--date", required=True)
args = parser.parse_args()
snapshot = ROOT / "data/official/upstream" / args.date
manifest = json.loads((snapshot / "rules-manifest.json").read_text())
core = next(rule for rule in manifest["rules"] if rule["ruleCategory"] == "核心规则")
pdf = ROOT / core["localPath"]
assert hashlib.sha256(pdf.read_bytes()).hexdigest() == core["sha256"], "PDF differs from recorded source"
clauses = {}
for page, text in enumerate(pdf.with_suffix(".txt").read_text().split("\f"), 1):
    for line in text.splitlines():
        match = re.match(r"^\s*(\d{3}(?:\.(?:\d+|[a-z]))*)\.\s+", line)
        if match:
            clause = match[1]
            clauses.setdefault(clause, {"rule": clause, "page": page,
                "status": "NEEDS_CURRENT_SOURCE_AUDIT", "tests": []})
result = {"source": core["ruleDocUrl"], "sha256": core["sha256"],
          "sourceDate": core["updateDate"], "clauseCount": len(clauses),
          "note": "Numbered clause inventory only. Existing regression tests do not automatically prove current-source coverage.",
          "clauses": list(clauses.values())}
path = snapshot / "core-rule-index.json"
if path.exists():
    raise SystemExit("Index already exists; preserve reviewed evidence instead of overwriting it.")
path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
print(f"Indexed {len(clauses)} numbered clauses: {path}")
