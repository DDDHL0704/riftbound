#!/usr/bin/env python3
"""One-shot CN printed-cost fixture migration; no runtime accommodation.

Only fixtures with one or more ordinary PLAY_CARD commands and matching expected CARD_PLAYED events
are eligible. Adds precisely its printed [C] amount to the initial rune pool;
leaves final pools and gameplay expectations intact. Event cost totals are
increased only where the existing fixture explicitly asserts those fields.
"""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
CATALOG = {card['cardNo']: card for card in json.loads((ROOT / 'data/official/card-catalog.zh-CN.json').read_text())['cards']}
MANIFEST = ROOT / 'docs/evidence/printed-power-fixture-migration.json'
previous = json.loads(MANIFEST.read_text())['fixtures'] if MANIFEST.exists() else []
already_migrated = {record['file'] for record in previous}


def replace_property(text, key, value, after=0):
    match = re.search(r'"' + re.escape(key) + r'"\s*:\s*', text[after:])
    start = after + match.end()
    _, length = json.JSONDecoder().raw_decode(text[start:])
    indent = len(text[:start].rsplit('\n', 1)[-1]) - len(text[:start].rsplit('\n', 1)[-1].lstrip())
    replacement = json.dumps(value, ensure_ascii=False, indent=2).replace('\n', '\n' + ' ' * indent)
    return text[:start] + replacement + text[start + length:]


records = list(previous)
for path in sorted((ROOT / 'tests/Riftbound.ConformanceTests/Fixtures').rglob('*.fixture.json')):
    if str(path.relative_to(ROOT)) in already_migrated:
        continue
    original = path.read_text()
    fixture = json.loads(original)
    plays = [command for command in fixture.get('commands', []) if command.get('cmd', {}).get('cmdType') == 'PLAY_CARD']
    if not plays or fixture.get('expected', {}).get('eventKinds', []).count('CARD_PLAYED') != len(plays):
        continue
    if any(play['cmd'].get('mode') in ['AMBUSH', 'STANDBY_REACTION', 'STANDBY_REVEAL'] for play in plays):
        continue
    if any(command.get('cmd', {}).get('cmdType') == 'END_TURN' for command in fixture.get('commands', [])):
        continue
    pools = fixture.get('initialState', {}).get('runePools', {})
    if any(play['playerId'] not in pools for play in plays):
        continue
    deltas = []
    for play in plays:
        card = CATALOG.get(play['cmd'].get('cardNo'), {})
        amount = card.get('returnEnergy') or 0
        player = play['playerId']
        traits = sorted(set(card.get('cardColorList', [])) & {'red', 'green', 'blue', 'yellow', 'orange', 'purple'})
        trait = traits[0] if traits else None
        deltas.append((play, amount, trait))
        if amount <= 0:
            continue
        pool = pools[player]
        if trait:
            typed = pool.setdefault('powerByTrait', {})
            typed[trait] = typed.get(trait, 0) + amount
        else:
            pool['power'] = pool.get('power', 0) + amount
    if not any(amount for _, amount, _ in deltas):
        continue
    changed = replace_property(original, 'runePools', pools, original.index('"initialState"'))
    events = fixture['expected'].get('events')
    event_changed = False
    play_index = -1
    if events:
        for index, event in enumerate(events):
            if event['kind'] == 'CARD_PLAYED':
                play_index += 1
            if event['kind'] != 'COST_PAID' or index == 0 or events[index - 1]['kind'] != 'CARD_PLAYED':
                continue
            _, amount, trait = deltas[play_index]
            if amount == 0:
                continue
            payload = event.get('payload', {})
            for key in ['power', 'totalPowerCost']:
                if key in payload:
                    payload[key] += amount
                    event_changed = True
            key = 'powerByTrait'
            if key in payload and trait:
                payload[key][trait] = payload[key].get(trait, 0) + amount
                event_changed = True
        if event_changed:
            changed = replace_property(changed, 'events', events, changed.index('"expected"'))
    if changed == original:
        continue
    path.write_text(changed)
    for play, amount, trait in deltas:
        if amount > 0:
            records.append({'file': str(path.relative_to(ROOT)), 'cardNo': play['cmd']['cardNo'], 'playerId': play['playerId'], 'addedPrintedPower': amount, 'trait': trait})
MANIFEST.parent.mkdir(parents=True, exist_ok=True)
MANIFEST.write_text(json.dumps({'authority': 'CN core 131.3 / 135.2.e.5-6 / 356; data/official/card-catalog.zh-CN.json', 'purpose': 'Supply the formerly omitted printed cost; preserve the original effects and final resource expectations.', 'fixtures': records}, ensure_ascii=False, indent=2) + '\n')
print(f'Migrated {len(records)} fixtures; manifest: {MANIFEST}')
