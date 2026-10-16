from pathlib import Path
import hashlib, json

# Independent Python reference; fixed vectors, not production code used as an oracle.
def shuffle(count, seed, cursor, scope):
    result = list(range(count))
    counter = 0
    for index in range(count - 1, 0, -1):
        bound = index + 1
        threshold = (2**64 - bound) % bound
        while True:
            raw = f'opening-v1:{seed}:{cursor}:{scope}:{counter}'.encode('utf-8')
            counter += 1
            value = int.from_bytes(hashlib.sha256(raw).digest()[:8], 'big')
            if value >= threshold:
                break
        selected = value % bound
        result[index], result[selected] = result[selected], result[index]
    return result

cases = [(2, 424242, 0, 'TURN_ORDER')]
for player, first in [('alice', 1), ('bob', 4)]:
    for count, offset, suffix in [(39, 0, 'MAIN_DECK'), (12, 1, 'RUNE_DECK'), (3, 2, 'BATTLEFIELDS')]:
        cases.append((count, 424242, first + offset, player + ':' + suffix))
cases += [(0, 0, 0, 'EMPTY'), (1, 0, 0, 'SINGLE'), (12, 0, 0, '符文:零种子'),
          (12, -(2**63), 2**63-1, 'unicode:符文'), (39, 2**63-1, 0, 'alice:MAIN_DECK')]
rows = [dict(count=count, seed=seed, cursor=cursor, scope=scope, permutation=shuffle(count, seed, cursor, scope))
        for count, seed, cursor, scope in cases]
target = Path('tests/Riftbound.ConformanceTests/Fixtures/opening-randomization-v1.json')
assert not target.exists()
target.write_text(json.dumps({'reference': 'Independent Python hashlib SHA-256 / Fisher-Yates / rejection sampling',
    'format': 'UTF8 opening-v1:seed:cursor:scope:counter; first SHA256 uint64 big endian', 'cases': rows},
    ensure_ascii=False, indent=2) + '\n')
print(len(rows), 'independent stable vectors', hashlib.sha256(target.read_bytes()).hexdigest())
