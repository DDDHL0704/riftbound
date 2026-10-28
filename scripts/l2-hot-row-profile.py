#!/usr/bin/env python3
"""Select and validate one diagnostic row; never a substitute for L2/L3."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

import l2_case_selection as selection

METHOD = ('Riftbound.ConformanceTests.CanonicalDelayedAuraDeathOpeningTests.'
          'LegalLeonaAndJinxOpeningsKeepTheChosenInverseOrderAndConditionalDeathThroughColdRecovery')
CASE = METHOD + '(auraFirst: True)'
SELECTOR = {'selectors': [{'kind': 'case', 'method': METHOD, 'case': CASE}]}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def settings(path):
    selection.write_settings(path, SELECTOR)


def verify(folder, commit, assembly):
    selection.verify_settings(folder / 'execution.runsettings', SELECTOR)
    results = ET.parse(folder / 'results.trx').getroot().findall('.//{*}UnitTestResult')
    if len(results) != 1 or results[0].get('testName') != CASE or results[0].get('outcome') != 'Passed':
        raise ValueError('The diagnostic must execute exactly the selected original row and pass.')
    records = [json.loads(line) for line in (folder / 'profile.ndjson').read_text().splitlines()]
    checkpoints = [r for r in records if r.get('scope') == 'cold-checkpoint']
    required = ['frame', 'serialize-and-decode', 'registry-recovery', 'views-and-privacy', 'duplicate']
    if (not checkpoints or len(checkpoints) % len(required)
            or any([r.get('phase') for r in checkpoints[i:i + len(required)]] != required
                   for i in range(0, len(checkpoints), len(required)))
            or not any(r.get('scope', '').startswith('action:') and r.get('phase') == 'send' for r in records)
            or any(not isinstance(r.get('elapsedMs'), (int, float)) or not math.isfinite(r['elapsedMs'])
                   or r['elapsedMs'] < 0 for r in records)):
        raise ValueError('The selected row did not emit complete measured checkpoint phases.')
    result = {'status': 'DIAGNOSTIC_ROW_PASSED', 'commit': commit, 'case': CASE,
              'testAssemblySha256': sha(assembly), 'trxSha256': sha(folder / 'results.trx'),
              'settingsSha256': sha(folder / 'execution.runsettings'),
              'profileSha256': sha(folder / 'profile.ndjson'), 'records': len(records),
              'coldCheckpoints': len(checkpoints) // len(required),
              'instrumentedDiagnosticOnly': True, 'fullL2OrL3Certified': False}
    (folder / 'receipt.json').write_text(json.dumps(result, indent=2) + '\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['settings', 'verify'])
    parser.add_argument('--folder', type=Path, required=True)
    parser.add_argument('--commit')
    parser.add_argument('--assembly', type=Path)
    args = parser.parse_args()
    args.folder.mkdir(parents=True, exist_ok=True)
    if args.action == 'settings':
        settings(args.folder / 'execution.runsettings')
    else:
        if not args.commit or not args.assembly:
            parser.error('verify requires --commit and --assembly')
        print(json.dumps(verify(args.folder, args.commit, args.assembly)))
