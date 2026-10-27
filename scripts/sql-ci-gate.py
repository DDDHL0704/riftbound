#!/usr/bin/env python3
"""Require every fixed SQL execution identity and the native network job."""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
import re


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify(commit, manifest_path, folder, jobs):
    require(re.fullmatch(r'[0-9a-f]{40}', commit) is not None, 'A fixed commit is required.')
    require(set(jobs) == {'scope', 'postgres_partitions', 'native_network', 'related_network'}
            and all(value == 'success' for value in jobs.values()), 'Every SQL/network child must succeed.')
    manifest_path, folder = Path(manifest_path), Path(folder)
    manifest = json.loads(manifest_path.read_text())
    require(manifest.get('schemaVersion') == 1 and manifest.get('kind') == 'SQL_EXECUTION_IDENTITIES_NOT_RULE_CERTIFICATION', 'Unknown SQL manifest.')
    count = manifest.get('defaultPartitionCount')
    rows = manifest.get('cases')
    require(type(count) is int and count > 0 and isinstance(rows, list) and len(rows) >= count, 'Empty SQL partition plan.')
    cases = [row['id'] for row in rows]
    require(all(isinstance(name, str) and re.fullmatch(r'[a-z0-9-]+', name) for name in cases)
            and len(set(cases)) == len(cases) and cases.count('core-opening') == 1, 'Unknown or duplicate SQL identities.')
    receipts = sorted(folder.rglob('partition-receipt.json'))
    require(len(receipts) == count, 'Missing or extra SQL partition receipt.')
    seen, completed, results, authority_catalog = set(), [], [], set()
    runtime_inputs = set()
    for path in receipts:
        data = json.loads(path.read_text())
        index = data.get('partitionIndex')
        require(type(index) is int and 0 <= index < count and index not in seen, 'Wrong or duplicate partition index.')
        seen.add(index)
        expected = cases[index::count]
        require(data.get('status') == 'REAL_POSTGRES_PARTITION_COMPLETE' and data.get('fixedCommit') == commit
                and data.get('partitionCount') == count and data.get('manifestSha256') == digest(manifest_path), 'Wrong SQL inputs or incomplete partition.')
        require(data.get('orderedDefaultCases') == cases and data.get('expectedCases') == expected
                and data.get('completedCases') == expected, 'SQL case order/coverage differs from fixed manifest.')
        executions = data.get('executions')
        require(isinstance(executions, list) and [row.get('id') for row in executions] == expected
                and all(row.get('status') == 'PASSED' and type(row.get('seconds')) in (int, float)
                        and math.isfinite(row['seconds']) and row['seconds'] >= 0 for row in executions), 'Missing, skipped, failed or duplicated SQL execution.')
        files = data.get('outputFiles')
        require(isinstance(files, list) and files, 'Actual SQL case outputs are required.')
        file_names = set()
        for row in files:
            name = row.get('path')
            require(isinstance(name, str) and name not in file_names and not Path(name).is_absolute()
                    and '..' not in Path(name).parts, 'Unsafe or duplicate SQL output path.')
            file_names.add(name)
            output = (path.parent / name).resolve()
            require(output.is_relative_to(path.parent.resolve()) and output.is_file(), 'Missing SQL output.')
            require(type(row.get('bytes')) is int and row['bytes'] == output.stat().st_size
                    and row.get('sha256') == digest(output), 'SQL output bytes changed.')
        for case in expected:
            require('result.json' in file_names if case == 'core-opening'
                    else any(name.startswith(case + '/') for name in file_names), 'SQL case has no retained actual output.')
        assemblies = data.get('runtimeAssemblies', [])
        require({row.get('file') for row in assemblies} == {'Riftbound.PostgresAcceptance.dll', 'Riftbound.Engine.dll',
                'Riftbound.Contracts.dll', 'Riftbound.CardCatalog.dll', 'Riftbound.Persistence.dll'} and len(assemblies) == 5
                and all(re.fullmatch(r'[0-9a-f]{64}', row.get('sha256', ''))
                        and re.fullmatch(r'[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}', row.get('mvid', ''))
                        for row in assemblies), 'Actual SQL runtime identities are required.')
        identity = data.get('actualRuleInputs', {})
        require(identity.get('schemaVersion') == 1 and identity.get('region') == 'CN' and identity.get('language') == 'zh-CN'
                and all(re.fullmatch(r'[0-9a-f]{64}', identity.get(key, '')) for key in
                        ['authoritySha256', 'catalogSha256', 'implementationSha256', 'identitySha256']), 'Actual bound rule inputs are required.')
        authority_catalog.add((identity['authoritySha256'], identity['catalogSha256']))
        runtime_inputs.add((identity['implementationSha256'], identity['identitySha256'],
                            tuple(sorted((row['file'], row['sha256'], row['mvid']) for row in assemblies))))
        completed.extend(expected)
        results.append({'partitionIndex': index, 'cases': expected, 'receiptSha256': digest(path),
                        'runtimeAssemblies': assemblies, 'actualRuleInputs': identity})
    require(seen == set(range(count)) and collections.Counter(completed) == collections.Counter(cases)
            and len(authority_catalog) == 1, 'The full SQL set or authority/catalog inputs do not match.')
    require(len(runtime_inputs) == 1, 'SQL runtime assemblies or bound rule inputs differ between partitions.')
    return {'status': 'FIXED_COMMIT_ALL_SQL_PARTITIONS_AND_NATIVE_NETWORK_PASS', 'fixedCommit': commit,
            'manifestSha256': digest(manifest_path), 'caseCount': len(cases), 'orderedDefaultCases': cases,
            'partitionCount': count, 'partitions': sorted(results, key=lambda row: row['partitionIndex']),
            'jobs': jobs, 'nativeVisibleOrWholeRuleCertification': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--inputs', type=Path, required=True)
    for key in ['scope', 'postgres-partitions', 'native-network', 'related-network']:
        parser.add_argument('--' + key, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = verify(args.commit, args.manifest, args.inputs, {'scope': args.scope,
                    'postgres_partitions': args.postgres_partitions, 'native_network': args.native_network,
                    'related_network': args.related_network})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('x') as stream:
        json.dump(result, stream, indent=2)
        stream.write('\n')
    print(result['status'], result['caseCount'], 'exact SQL execution identities')


if __name__ == '__main__':
    main()
