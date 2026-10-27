#!/usr/bin/env python3
"""Read-only validation of a source-scope reference against real test discovery."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys


def read_discovery(discovery):
    text = Path(discovery).read_text()
    if text.lstrip().startswith('['):
        rows = json.loads(text)
    else:
        rows = [line.strip() for line in text.splitlines() if line.startswith('    ') and line.strip()]
    if not isinstance(rows, list) or not rows or any(
            not isinstance(row, str) or not row.startswith('Riftbound.ConformanceTests.') for row in rows):
        raise ValueError('Missing, empty or unparseable real test discovery.')
    return {row.split('(', 1)[0] for row in rows}


def validate(reference, profile_name, source_head, discovery):
    profiles = [row for row in reference['profiles'] if row['profile'] == profile_name]
    if len(profiles) != 1:
        raise ValueError('Select exactly one fixed-input source profile.')
    profile = profiles[0]
    if not re.fullmatch(r'[0-9a-f]{40}', source_head) or source_head != profile['sourceHead']:
        raise ValueError('Source SHA changed; regenerate from that source and its actual discovery.')
    actual = read_discovery(discovery)
    selected = reference['commonSourceGuardMethods'] + profile['extraSourceGuardMethods']
    if not selected or len(selected) != len(set(selected)):
        raise ValueError('Source manifest is empty or contains duplicate method identities.')
    missing = sorted(set(selected) - actual)
    if missing:
        raise ValueError('Manifest methods absent from discovery: ' + ', '.join(missing))
    digest = hashlib.sha256(('\n'.join(sorted(actual)) + '\n').encode()).hexdigest()
    if digest != profile['discoveredMethodSetSha256']:
        raise ValueError('Discovery method set changed; regenerate rather than dropping new methods.')
    if len(selected) != profile['sourceGuardMethodCount']:
        raise ValueError('Manifest count does not match its unique methods.')
    return {'status': 'VERIFIED_DISCOVERY_SCOPE_ONLY', 'sourceHead': source_head,
            'profile': profile_name, 'discoveredMethods': len(actual), 'sourceGuardMethods': len(selected),
            'semanticTestsExecuted': False, 'semanticPassClaim': False, 'existingLanesChanged': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=Path(__file__).with_name('reference.json'))
    parser.add_argument('--profile', required=True)
    parser.add_argument('--source-head', required=True)
    parser.add_argument('--discovery', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = validate(json.loads(args.reference.read_text()), args.profile, args.source_head, args.discovery)
    except (ValueError, KeyError, TypeError) as error:
        print(str(error), file=sys.stderr)
        return 2
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
