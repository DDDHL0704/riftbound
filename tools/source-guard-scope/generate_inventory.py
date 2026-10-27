#!/usr/bin/env python3
"""Inventory fixed Git test declarations against externally captured real discovery.

Does not build or execute the game or change CI lanes. Caller must independently
bind the discovery capture to its real source and loaded test modules.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import re
import subprocess

from validate_reference import read_discovery


HERE = Path(__file__).resolve().parent


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def git(repo, *args, data=None):
    return subprocess.check_output(['git', *args], cwd=repo, input=data)


def read_blobs(repo, objects):
    objects = sorted(set(objects))
    if not objects:
        return {}
    raw = git(repo, 'cat-file', '--batch', data=''.join(x + '\n' for x in objects).encode())
    blobs, offset = {}, 0
    for oid in objects:
        end = raw.index(b'\n', offset)
        actual, kind, size = raw[offset:end].split()
        size = int(size)
        if actual.decode() != oid or kind != b'blob':
            raise ValueError('Git returned an unexpected source object.')
        blobs[oid] = raw[end + 1:end + 1 + size]
        offset = end + size + 2
    if offset != len(raw):
        raise ValueError('Unconsumed Git object data.')
    return blobs


def generate(args):
    repo = args.repo.resolve()
    head = git(repo, 'rev-parse', '--verify', args.source_head + '^{commit}').decode().strip()
    if args.source_head != head:
        raise ValueError('--source-head must be the full immutable commit SHA.')
    if args.output.exists():
        raise ValueError('Output already exists; preserve original evidence and choose a new directory.')
    tree = {}
    for row in git(repo, 'ls-tree', '-rz', head, '--', 'tests/Riftbound.ConformanceTests').split(b'\0'):
        if row:
            meta, path = row.split(b'\t', 1)
            parts, path = meta.split(), path.decode()
            if parts[1] == b'blob' and path.endswith('.cs'):
                tree[path] = parts[2].decode()
    if not tree:
        raise ValueError('No tracked conformance C# source found.')
    scanner_sha = sha((HERE / 'scanner/Program.cs').read_bytes())
    cache = json.loads(args.syntax_cache.read_text()) if args.syntax_cache else {}
    if cache and cache.get('scannerProgramSha256') != scanner_sha:
        raise ValueError('Syntax cache uses a different scanner; regenerate without it.')
    cached_files = cache.get('sourceFiles', {})
    reused = {path for path, oid in tree.items() if cached_files.get(path, {}).get('gitBlob') == oid}
    changed = {path: oid for path, oid in tree.items() if path not in reused}
    blobs = read_blobs(repo, changed.values())
    inputs = [{'path': path, 'code': blobs[oid].decode('utf-8')} for path, oid in sorted(changed.items())]
    parsed = json.loads(subprocess.check_output(
        [args.dotnet, str(args.scanner)], input=json.dumps(inputs, ensure_ascii=False).encode()))
    if parsed['diagnostics']:
        raise ValueError('Source syntax errors: ' + json.dumps(parsed['diagnostics']))
    parsed['nodes'] += [row for row in cache.get('nodes', []) if row['path'] in reused]
    parsed['classes'] += [row for row in cache.get('classes', []) if row['path'] in reused]
    source_files = {path: cached_files[path] if path in reused else
                    {'gitBlob': oid, 'sha256': sha(blobs[oid])} for path, oid in tree.items()}
    groups, class_markers, class_game = defaultdict(list), defaultdict(set), defaultdict(bool)
    for row in parsed['nodes']:
        groups[row['key']].append(row)
    for row in parsed['classes']:
        class_markers[row['key']].update(row['markers'])
        class_game[row['key']] |= row['gameplay']
        if re.search(r'Parser|Catalog|Cn16|Metadata|Printing|FunctionalUnit|Registry|Source',
                     row['key'].rsplit('.', 1)[1]):
            class_markers[row['key']].add('named_source_or_metadata_family')
    simple = defaultdict(set)
    for key in class_markers:
        simple[key.rsplit('.', 1)[1]].add(key)
    classes = set(class_markers)
    links, markers, games, tests = {}, {}, {}, {}
    for key, nodes in groups.items():
        owner, deps = nodes[0]['owner'], set()
        markers[key], games[key] = set(class_markers[owner]), class_game[owner]
        for node in nodes:
            markers[key].update(node['markers'])
            games[key] |= node['gameplay']
            if any(re.search(r'\b(?:[A-Za-z0-9_]*Parser|RuleTextParsers|PrintedPowerCostRules|BehaviorSpecCatalogBuilder)\b',
                             call['receiver']) for call in node['calls']):
                markers[key].add('rule_text_metadata')
            for call in node['calls']:
                member, receiver, owners = call['member'], call['receiver'], set()
                if not receiver or receiver in ('this', 'base'):
                    owners.add(owner)
                    owners.update(node['staticUsings'])
                else:
                    normalized = re.sub(r'<.*>', '', receiver).replace('global::', '')
                    if normalized in classes:
                        owners.add(normalized)
                    owners.update(simple.get(normalized, set()))
                    owners.add(owner.rsplit('.', 1)[0] + '.' + normalized)
                deps.update(target + '.' + member for target in owners if target + '.' + member in groups)
            if node['isTest']:
                if key in tests:
                    raise ValueError('Duplicate test declaration: ' + key)
                tests[key] = node
        links[key] = deps
    changed_graph = True
    while changed_graph:
        changed_graph = False
        for key, deps in links.items():
            reasons, gameplay = set(markers[key]), games[key]
            for dep in deps:
                reasons.update(markers[dep])
                gameplay |= games[dep]
            if reasons != markers[key] or gameplay != games[key]:
                markers[key], games[key], changed_graph = reasons, gameplay, True
    discovered = read_discovery(args.discovery)
    actual = {method.replace('+', '.'): method for method in discovered}
    if set(actual) != set(tests):
        raise ValueError(json.dumps({'discoveredWithoutDeclaration': sorted(set(actual) - set(tests)),
                                    'declaredWithoutDiscovery': sorted(set(tests) - set(actual))}))
    l3 = json.loads(git(repo, 'show', head + ':scripts/l3-rule-acceptance-methods.json'))
    l3_names = {row['method'] for row in l3['methods']}
    source_families = {node['owner'] for key, node in tests.items() if markers[key]}
    records = []
    for key, node in sorted(tests.items()):
        if not markers[key] and node['owner'] not in source_families:
            continue
        direct = set(node['markers'])
        mandatory = node['owner'].rsplit('.', 1)[1] in ('CardCatalogBaselineTests', 'Cn16RuleInputSelectionTests')
        preservation = 'functional_groups' in direct or bool(re.search(
            r'(?:Historical|Prior).*(?:Catalog|Group|Member)|(?:Catalog|Printing).*(?:Preserv|Keep|AddsOne)|CatalogStatistics',
            node['name'], re.I))
        candidate = mandatory or preservation or bool(markers[key] and not games[key])
        records.append({'method': actual[key], 'source': node['path'], 'line': node['line'],
                        'sourceGitBlob': tree[node['path']], 'sourceFileSha256': source_files[node['path']]['sha256'],
                        'attributes': node['attributes'], 'directSourceReasons': sorted(direct),
                        'transitiveSourceReasons': sorted(markers[key]),
                        'sourceHelperDependencies': sorted(dep for dep in links[key] if markers[dep]),
                        'gameplayOrRecoveryOperationsReachable': games[key],
                        'role': 'historical_or_group_guard' if preservation else 'mandatory_source_family' if mandatory
                        else 'source_metadata_candidate' if candidate else 'source_family_semantic_impact',
                        'includeInSourceGuardBatch': candidate,
                        'existingLane': 'L3' if actual[key] in l3_names else 'L2', 'laneChanged': False})
    selected = {row['method'] for row in records if row['includeInSourceGuardBatch']}
    args.output.mkdir(parents=True)
    manifest = {'schemaVersion': 1, 'sourceHead': head, 'sourceFiles': len(tree),
                'discoveredMethods': len(discovered), 'declaredTestMethods': len(tests),
                'unmatchedDeclaredMethods': [], 'unmatchedDiscoveredMethods': [],
                'discoverySha256': sha(args.discovery.read_bytes()),
                'discoveredMethodSetSha256': sha(('\n'.join(sorted(discovered)) + '\n').encode()),
                'sourceRelatedMethodCount': len(records), 'sourceGuardBatchCount': len(selected),
                'roles': dict(Counter(row['role'] for row in records)),
                'syntaxFilesReused': len(reused), 'syntaxFilesParsed': len(changed),
                'semanticTestsExecuted': False, 'existingLanesChanged': False,
                'discoverySourceBindingVerifiedByThisTool': False,
                'unknownMethodsRemainInExistingRuleSuite': True, 'methods': records}
    write_json(args.output / 'source-guard-methods.json', manifest)
    (args.output / 'source-guard-batch-methods.txt').write_text('\n'.join(sorted(selected)) + '\n')
    write_json(args.output / 'syntax-cache.json', {'scannerProgramSha256': scanner_sha,
               'sourceFiles': source_files, 'nodes': parsed['nodes'], 'classes': parsed['classes']})
    print(json.dumps({key: value for key, value in manifest.items() if key != 'methods'}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--source-head', required=True)
    parser.add_argument('--discovery', type=Path, required=True)
    parser.add_argument('--scanner', type=Path, required=True, help='Built Scanner.dll from sibling scanner project')
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--syntax-cache', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    generate(parser.parse_args())
