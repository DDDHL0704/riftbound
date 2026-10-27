"""Exact L2 execution selectors; scheduling never changes the required case multiset."""
from collections import Counter, defaultdict
import math
import xml.etree.ElementTree as ET

SPLIT_MIN_SECONDS = 1200


def escape(value):
    return ''.join('\\' + char if char in '\\()&|=!~' else char for char in value)


def eligible_methods(discovery, cases):
    expanded = defaultdict(list)
    folded = Counter(name.casefold() for name in discovery)
    for name in discovery:
        expanded[name.split('(', 1)[0]].append(name)
    # Start with the audited ASCII class of names. Other names still execute whole.
    return {method for method, rows in cases.items()
            if len(rows) > 1 and Counter(rows) == Counter(expanded[method])
            and all(name.isascii() and name.startswith(method + '(') and folded[name.casefold()] == 1
                    for name in rows)}


def selectors(discovery, cases, weights):
    eligible = eligible_methods(discovery, cases)
    result = []
    for method in sorted(cases):
        if method in eligible and weights.get(method, 1) >= SPLIT_MIN_SECONDS:
            result.extend(dict(kind='case', method=method, case=name) for name in cases[method])
        else:
            result.append(dict(kind='method', method=method))
    return result


def partition(units, cases, weights, count):
    if not 1 <= count <= 8 or len(units) < count:
        raise ValueError('Require 1..8 nonempty L2 shards.')
    if any(not isinstance(v, (int, float)) or not math.isfinite(v) or v < 0 for v in weights.values()):
        raise ValueError('Invalid duration history; it never controls membership.')
    def weight(unit):
        return weights.get(unit['method'], 1) / (len(cases[unit['method']]) if unit['kind'] == 'case' else 1)
    key = lambda unit: (unit['method'], unit.get('case', ''))
    bins = [dict(name=f'shard-{i + 1}', selectors=[], estimatedSeconds=0.0) for i in range(count)]
    for unit in sorted(units, key=lambda item: (-weight(item), key(item))):
        shard = min(bins, key=lambda item: (item['estimatedSeconds'], len(item['selectors']), item['name']))
        shard['selectors'].append(unit)
        shard['estimatedSeconds'] += weight(unit)
    for shard in bins:
        shard['selectors'].sort(key=key)
        shard['methods'] = sorted({unit['method'] for unit in shard['selectors']})
    return bins


def case_names(shard, cases):
    return [name for unit in shard['selectors']
            for name in ([unit['case']] if unit['kind'] == 'case' else cases[unit['method']])]


def validate(shards, cases, discovery):
    names = [shard['name'] for shard in shards]
    if not names or len(names) != len(set(names)):
        raise ValueError('Invalid case partition: missing or repeated shard.')
    groups = defaultdict(list)
    eligible = eligible_methods(discovery, cases)
    for shard in shards:
        units = shard.get('selectors', [])
        if not units or shard['methods'] != sorted({unit['method'] for unit in units}):
            raise ValueError('Invalid case partition: shard method/selector mismatch.')
        for unit in units:
            method = unit.get('method')
            if method not in cases:
                raise ValueError('Invalid case partition: foreign method.')
            if unit.get('kind') == 'method' and set(unit) == {'kind', 'method'}:
                pass
            elif (unit.get('kind') == 'case' and set(unit) == {'kind', 'method', 'case'}
                  and method in eligible and unit['case'] in cases[method]):
                pass
            else:
                raise ValueError('Invalid case partition: unsafe, ambiguous or unexpanded row selector.')
            groups[method].append(unit)
    if set(groups) != set(cases):
        raise ValueError('Invalid case partition: missing method.')
    for method, units in groups.items():
        whole = [unit for unit in units if unit['kind'] == 'method']
        if whole and len(units) != 1:
            raise ValueError('Invalid case partition: whole method overlaps another selector.')
        actual = Counter(name for unit in units for name in
                         ([unit['case']] if unit['kind'] == 'case' else cases[method]))
        if actual != Counter(cases[method]):
            raise ValueError('Invalid case partition: missing or repeated parameter rows.')


def expression(shard):
    parts = []
    for unit in shard['selectors']:
        method = 'FullyQualifiedName=' + escape(unit['method'])
        parts.append('(' + method + '&DisplayName=' + escape(unit['case']) + ')'
                     if unit['kind'] == 'case' else method)
    return '|'.join(parts)


def write_settings(path, shard):
    tree = ET.Element('RunSettings')
    config = ET.SubElement(tree, 'RunConfiguration')
    ET.SubElement(config, 'TestCaseFilter').text = expression(shard)
    ET.SubElement(config, 'TreatNoTestsAsError').text = 'true'
    ET.ElementTree(tree).write(path, encoding='utf-8', xml_declaration=True)


def verify_settings(path, shard):
    config = ET.parse(path).getroot().find('RunConfiguration')
    if (config is None or config.findtext('TestCaseFilter') != expression(shard)
            or config.findtext('TreatNoTestsAsError') != 'true'):
        raise ValueError('Execution settings differ from the exact planned selectors.')
