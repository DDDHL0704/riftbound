#!/usr/bin/env python3
"""Derive card-registration hotspot counts from the reviewed batch ledger."""

import argparse
import csv
import json
from pathlib import Path


TARGETS = (
    "src/Riftbound.Engine/CardBehaviorRegistry.cs",
    "src/Riftbound.Engine/CoreRuleEngine.cs",
    "tests/Riftbound.ConformanceTests/ReviewedRuntimeCatalogAdmissionTests.cs",
    "src/Riftbound.CardCatalog/RuleTextParsers.cs",
    "src/Riftbound.CardCatalog/VendettaCardSources.cs",
    "src/Riftbound.CardCatalog/RadiantCardSources.cs",
    "src/Riftbound.Engine/PrintedCardFactory.cs",
    "tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs",
)
REQUIRED = ("pr", "branch", "title", "status", *TARGETS)
STATUSES = frozenset(("merged", "closed", "open"))


def load(path):
    with Path(path).open(newline="", encoding="utf-8") as stream:
        reader = csv.DictReader(stream)
        if tuple(reader.fieldnames or ()) != REQUIRED:
            raise ValueError("registration ledger columns do not match the reviewed schema")
        rows = []
        seen = set()
        for row in reader:
            try:
                pr = int(row["pr"])
            except (TypeError, ValueError) as error:
                raise ValueError("PR numbers must be integers") from error
            if pr in seen:
                raise ValueError(f"duplicate PR #{pr}")
            seen.add(pr)
            if row["status"] not in STATUSES:
                raise ValueError(f"unknown status for PR #{pr}: {row['status']}")
            if not row["branch"] or not row["title"]:
                raise ValueError(f"PR #{pr} is missing branch or title")
            values = {}
            for target in TARGETS:
                if row[target] not in {"0", "1"}:
                    raise ValueError(f"PR #{pr} has a non-binary value for {target}")
                values[target] = int(row[target])
            rows.append(dict(pr=pr, branch=row["branch"], title=row["title"],
                             status=row["status"], files=values))
    if not rows:
        raise ValueError("registration ledger is empty")
    return rows


def summarize(rows, merged_only=False):
    selected = [row for row in rows if not merged_only or row["status"] == "merged"]
    counts = {target: sum(row["files"][target] for row in selected) for target in TARGETS}
    return {
        "batches": [
            {"pr": row["pr"], "branch": row["branch"], "title": row["title"],
             "status": row["status"], "files": row["files"]}
            for row in selected
        ],
        "counts": counts,
        "batchCount": len(selected),
        "statusCounts": {
            status: sum(row["status"] == status for row in rows) for status in sorted(STATUSES)
        },
    }


def markdown(result):
    lines = [
        "| 登记文件 | 批次数 |",
        "| --- | ---: |",
    ]
    lines.extend(f"| `{target}` | {result['counts'][target]} |" for target in TARGETS)
    lines.extend(("", "| PR | 分支 | 状态 | 标题 |", "| ---: | --- | --- | --- |"))
    for row in result["batches"]:
        title = row["title"].replace("|", "\\|")
        lines.append(f"| #{row['pr']} | `{row['branch']}` | {row['status']} | {title} |")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--merged-only", action="store_true",
                        help="derive counts from merged batches only")
    parser.add_argument("--format", choices=("json", "markdown"), default="json")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = summarize(load(args.input), args.merged_only)
    rendered = (json.dumps(result, ensure_ascii=False, indent=2) + "\n"
                if args.format == "json" else markdown(result))
    if args.output:
        args.output.write_text(rendered, encoding="utf-8")
    else:
        print(rendered, end="")


if __name__ == "__main__":
    main()
