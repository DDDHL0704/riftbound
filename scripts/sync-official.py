#!/usr/bin/env python3
"""Fetch a complete, immutable CN official baseline without promoting unverified gameplay.

The active engine baseline changes only after rule/effect conformance is verified.
Run: python3 scripts/sync-official.py --date YYYY-MM-DD
"""
import argparse
from datetime import date
import hashlib
import json
import math
from pathlib import Path
import subprocess
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
API = "https://lol-api.playloltcg.com"


def post(path, body):
    request = urllib.request.Request(API + path, json.dumps(body).encode(),
                                     {"Content-Type": "application/json", "User-Agent": "RiftboundCatalogSync/1.0"})
    with urllib.request.urlopen(request, timeout=45) as response:
        data = json.load(response)
    if data.get("code") != 0:
        raise ValueError(f"Official API rejected {path}: {data.get('message')}")
    return data["result"]


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--date", required=True)
    args = parser.parse_args()
    date.fromisoformat(args.date)
    destination = ROOT / "data/official/upstream" / args.date
    if (destination / "rules-manifest.json").exists():
        raise SystemExit("Snapshot already exists; use a new date to preserve reviewed sources.")
    destination.mkdir(parents=True, exist_ok=True)
    params = dict(pageNum=1, pageSize=100, searchContent="", cardCategoryList=[],
                  cardColorList=[], rarityList=[], productCodeList=[])
    first = post("/xcx/card/searchCardCraftWeb", params)
    total = first["total"]
    cards = list(first["list"])
    for page in range(2, math.ceil(total / params["pageSize"]) + 1):
        result = post("/xcx/card/searchCardCraftWeb", {**params, "pageNum": page})
        if result["total"] != total:
            raise ValueError("Catalog changed while paging; retry for a consistent baseline")
        cards.extend(result["list"])
    if len(cards) != total or len({card["id"] for card in cards}) != total:
        raise ValueError("Official catalog pagination is incomplete or contains duplicate ids")
    # cardQaList is not a rule authority. Preserve card data, exclude this field.
    cards = sorted(({k: v for k, v in card.items() if k != "cardQaList"} for card in cards), key=lambda card: card["id"])
    catalog = dict(source="https://www.playloltcg.com/card.html", api=API + "/xcx/card/searchCardCraftWeb",
                   fetchedAt=args.date, total=total, cards=cards)
    write(destination / "card-catalog.zh-CN.json", catalog)
    rules = post("/xcx/gameRule/getGameRuleList", dict(pageNum=1, pageSize=1000))
    pdf_dir = ROOT / "var/official" / args.date
    pdf_dir.mkdir(parents=True, exist_ok=True)
    for index, rule in enumerate(rules):
        pdf = pdf_dir / f"{index + 1:02d}.pdf"
        url = urllib.parse.quote(rule["ruleDocUrl"], safe=":/?=&%")
        subprocess.run(["curl", "-fLsS", "--retry", "2", "--max-time", "120", url, "-o", str(pdf)], check=True)
        if not pdf.read_bytes().startswith(b"%PDF"):
            raise ValueError(f"Invalid PDF: {rule['ruleName']}")
        subprocess.run(["pdftotext", "-layout", str(pdf), str(pdf.with_suffix('.txt'))], check=True)
        rule["sha256"] = hashlib.sha256(pdf.read_bytes()).hexdigest()
        rule["localPath"] = str(pdf.relative_to(ROOT))
    write(destination / "rules-manifest.json", dict(source="https://www.playloltcg.com/rules.html", fetchedAt=args.date, rules=rules))
    active = json.loads((ROOT / "data/official/card-catalog.zh-CN.json").read_text())
    before = {card["cardNo"]: card for card in active["cards"]}
    after = {card["cardNo"]: card for card in cards}
    changed = [key for key in before.keys() & after.keys() if any(
        before[key].get(field) != after[key].get(field) for field in ("cardEffect", "energy", "returnEnergy", "power", "cardColorList", "cardGroupLimit"))]
    write(destination / "diff.json", dict(activeFetchedAt=active["fetchedAt"], fetchedAt=args.date,
        activeEntries=active["total"], upstreamEntries=total,
        added=sorted(after.keys() - before.keys()), removed=sorted(before.keys() - after.keys()),
        changedGameplayFields=sorted(changed), promotedToPlayable=False))
    print(json.dumps(dict(entries=total, rules=len(rules), added=len(after.keys()-before.keys()),
        removed=len(before.keys()-after.keys()), changedGameplayFields=len(changed), destination=str(destination)), ensure_ascii=False))


if __name__ == "__main__":
    main()
