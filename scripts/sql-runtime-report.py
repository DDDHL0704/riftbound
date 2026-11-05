#!/usr/bin/env python3
"""Observe completed SQL partitions from checksum-bound artifacts, including partial runs."""
import argparse
from collections import Counter
import hashlib
import io
import json
import math
from pathlib import Path, PurePosixPath
import re
import zipfile


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(value):
    return hashlib.sha256(value).hexdigest()


def nonnegative(value):
    return type(value) in (int, float) and math.isfinite(value) and value >= 0


def process_counters(row):
    value = row.get('runtimeCounters')
    if value is None:
        return None
    require(isinstance(value, dict)
            and all(nonnegative(value.get(key)) for key in ('processCpuSeconds', 'gcPauseSeconds'))
            and type(value.get('allocatedBytes')) is int and value['allocatedBytes'] >= 0
            and isinstance(value.get('collections'), list) and value['collections']
            and all(type(count) is int and count >= 0 for count in value['collections']),
            'Malformed process counter deltas.')
    return value


def report(commit, manifest_path, archives):
    require(isinstance(commit, str) and re.fullmatch(r'[0-9a-f]{40}', commit), 'A fixed commit is required.')
    manifest_path = Path(manifest_path).resolve()
    manifest_bytes = manifest_path.read_bytes()
    manifest = json.loads(manifest_bytes)
    count, cases = manifest.get('defaultPartitionCount'), manifest.get('cases')
    require(manifest.get('schemaVersion') == 2 and manifest.get('kind') == 'SQL_EXECUTION_IDENTITIES_NOT_RULE_CERTIFICATION'
            and type(count) is int and count > 0 and isinstance(cases, list) and len(cases) >= count,
            'A complete SQL partition manifest is required.')
    ids = [case.get('id') for case in cases]
    assignments = [case.get('defaultPartition') for case in cases]
    require(all(isinstance(name, str) and re.fullmatch(r'[a-z0-9-]+', name) for name in ids)
            and len(set(ids)) == len(ids) and ids.count('core-opening') == 1
            and all(type(index) is int and 0 <= index < count for index in assignments)
            and set(assignments) == set(range(count)), 'Invalid SQL identities or assignments.')
    require(archives, 'At least one original SQL partition artifact is required.')
    observed = {manifest_path: manifest_bytes}
    indices, partitions, rows, identities = set(), [], [], []
    required_modules = {'Riftbound.' + name + '.dll' for name in
                        ('PostgresAcceptance', 'Engine', 'Contracts', 'CardCatalog', 'Persistence')}
    for archive_path, expected_sha in archives:
        path = Path(archive_path).resolve()
        expected_sha = expected_sha.removeprefix('sha256:') if isinstance(expected_sha, str) else ''
        require(path not in observed and re.fullmatch(r'[0-9a-f]{64}', expected_sha) is not None,
                'Each artifact needs its own path and expected SHA256.')
        raw = path.read_bytes()
        require(sha(raw) == expected_sha, 'Artifact SHA256 differs from the expected original bytes.')
        observed[path] = raw
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            names = archive.namelist()
            require(len(names) == len(set(names)), 'Duplicate ZIP members.')
            data = json.loads(archive.read('ci-postgres/partition-receipt.json'))
            require(archive.read('ci-postgres-partition-commit.txt').decode().strip() == commit,
                    'Artifact checkout differs from the fixed commit.')
            index = data.get('partitionIndex')
            require(type(index) is int and 0 <= index < count and index not in indices, 'Wrong or duplicate partition index.')
            indices.add(index)
            expected = [name for name, assigned in zip(ids, assignments) if assigned == index]
            require(data.get('status') == 'REAL_POSTGRES_PARTITION_COMPLETE' and data.get('fixedCommit') == commit
                    and data.get('manifestSha256') == sha(manifest_bytes) and data.get('partitionCount') == count
                    and data.get('orderedDefaultCases') == ids and data.get('expectedCases') == expected
                    and data.get('completedCases') == expected, 'Incomplete partition or wrong SQL inputs.')
            executions = data.get('executions')
            require(isinstance(executions, list) and [row.get('id') for row in executions] == expected
                    and all(row.get('status') == 'PASSED' and type(row.get('ordinal')) is int
                            and row['ordinal'] == ids.index(row['id']) and nonnegative(row.get('seconds')) for row in executions),
                    'Missing, reordered, duplicated or failed SQL case.')
            outputs = data.get('outputFiles')
            require(isinstance(outputs, list) and outputs, 'Retained SQL outputs are required.')
            files = set()
            for item in outputs:
                name = item.get('path')
                require(isinstance(name, str) and name and name not in files and '\\' not in name
                        and not PurePosixPath(name).is_absolute() and '..' not in PurePosixPath(name).parts,
                        'Unsafe or duplicate SQL output path.')
                files.add(name)
                payload = archive.read('ci-postgres/' + name)
                require(type(item.get('bytes')) is int and item['bytes'] == len(payload)
                        and item.get('sha256') == sha(payload), 'Retained SQL output bytes changed.')
            require(all('result.json' in files if name == 'core-opening'
                        else any(path.startswith(name + '/') for path in files) for name in expected), 'Missing case output.')
            modules, rule_inputs = data.get('runtimeAssemblies', []), data.get('actualRuleInputs', {})
            require(isinstance(modules, list) and len(modules) == 5 and {row.get('file') for row in modules} == required_modules
                    and all(re.fullmatch(r'[0-9a-f]{64}', row.get('sha256', ''))
                            and re.fullmatch(r'[0-9a-f-]{36}', row.get('mvid', '')) for row in modules), 'Missing SQL module identities.')
            require(rule_inputs.get('schemaVersion') == 1 and rule_inputs.get('region') == 'CN' and rule_inputs.get('language') == 'zh-CN'
                    and all(re.fullmatch(r'[0-9a-f]{64}', rule_inputs.get(key, '')) for key in
                            ('authoritySha256', 'catalogSha256', 'implementationSha256', 'identitySha256')), 'Missing bound rule inputs.')
            identity = dict(runtimeAssemblies=sorted(modules, key=lambda row: row['file']), actualRuleInputs=rule_inputs)
            require(not identities or identity == identities[0], 'Observed partitions use different modules or rule inputs.')
            identities.append(identity)
            scope = data.get('runtimeCounterScope')
            for execution in executions:
                counters = process_counters(execution)
                if 'runtimeCounters' in execution:
                    require(isinstance(scope, str) and scope.strip(), 'Process counters need their recorded scope.')
                rows.append(dict(partitionIndex=index, id=execution['id'], seconds=execution['seconds'],
                                 counterAvailability='legacy' if 'runtimeCounters' not in execution
                                 else 'unavailable' if counters is None else 'available', runtimeCounters=counters))
            partitions.append(dict(partitionIndex=index, cases=len(expected), outputFiles=len(files),
                                   totalCaseSeconds=sum(row['seconds'] for row in executions),
                                   runtimeCounterScope=scope, executionRuntime=data.get('executionRuntime'),
                                   receiptSha256=sha(archive.read('ci-postgres/partition-receipt.json'))))
    require(all(path.read_bytes() == raw for path, raw in observed.items()), 'An input changed while reporting.')
    return dict(status='SQL_PARTITION_RUNTIME_OBSERVATIONS', fixedCommit=commit,
                observedPartitions=sorted(indices), missingPartitions=sorted(set(range(count)) - indices),
                observedCases=len(rows), manifestCases=len(ids), totalCaseSeconds=sum(row['seconds'] for row in rows),
                counterCoverage=dict(Counter(row['counterAvailability'] for row in rows)),
                partitions=sorted(partitions, key=lambda row: row['partitionIndex']),
                cases=sorted(rows, key=lambda row: (-row['seconds'], row['id'])), **identities[0],
                inputs=[dict(path=str(path), bytes=len(raw), sha256=sha(raw)) for path, raw in observed.items()],
                fullAcceptanceVerified=False,
                limitation='Completed partitions only; missing partitions and unavailable counters are not zero. '
                           'Case times include all case work and are summed across parallel partitions, not run wall time. '
                           'Process counters include concurrent work and sampling; they do not attribute SQL, replay or GC CPU. '
                           'This report does not establish CI speedup or replace full SQL, network or rule acceptance.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--archive', nargs=2, action='append', required=True, metavar=('ZIP', 'SHA256'),
                        help='Original artifact ZIP and its expected SHA256, for example from GitHub artifact metadata.')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = report(args.commit, args.manifest, args.archive)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('x') as output:
        json.dump(result, output, indent=2, allow_nan=False)
        output.write('\n')
    print(result['status'], result['observedCases'], 'cases; observation only')


if __name__ == '__main__':
    main()
