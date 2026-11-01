"""Exact L2 execution selectors; scheduling never changes the required case multiset."""
from collections import Counter, defaultdict
import math
import xml.etree.ElementTree as ET

SPLIT_MIN_SECONDS = 600
ESTIMATED_CLASS_WORKERS = 4

# Both classes await BattleLimitedRecoveryTests.Actual. Keeping them together
# builds that accepted fixture once instead of once in each shard process.
SHARED_FIXTURE_CLASSES = (
    frozenset({'Riftbound.ConformanceTests.BattleLimitedRecoveryTests',
               'Riftbound.ConformanceTests.BattleLimitedRoleMemoMechanismTests'}),
)


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
    if not 1 <= count <= 9 or len(units) < count:
        raise ValueError('Require 1..9 nonempty L2 shards.')
    if any(not isinstance(v, (int, float)) or not math.isfinite(v) or v < 0 for v in weights.values()):
        raise ValueError('Invalid duration history; it never controls membership.')
    def weight(unit):
        method_seconds = weights.get(unit['method'], 1)
        if unit['kind'] == 'case':
            return weights.get(unit['case'], method_seconds / len(cases[unit['method']]))
        return method_seconds
    key = lambda unit: (unit['method'], unit.get('case', ''))
    # xUnit runs methods of one class serially. Balance both that class load and
    # the total work on the observed four-worker Linux runners; these estimates
    # never change selectors or required coverage.
    target = max(sum(weight(unit) for unit in units) / (ESTIMATED_CLASS_WORKERS * count),
                 max(weight(unit) for unit in units))
    remaining = list(units)
    bundles = []
    for classes in SHARED_FIXTURE_CLASSES:
        together = [unit for unit in remaining if unit['method'].rsplit('.', 1)[0] in classes]
        loads = defaultdict(float)
        for unit in together:
            loads[unit['method'].rsplit('.', 1)[0]] += weight(unit)
        # Preserve class parallelism when a shared fixture would create a lane
        # longer than the existing target, and always leave every shard work.
        if (len(together) > 1 and max(loads.values()) <= target
                and len(remaining) - len(together) + len(bundles) + 1 >= count):
            bundles.append(together)
            remaining = [unit for unit in remaining if unit not in together]
    bundles.extend([unit] for unit in remaining)
    bins = [dict(name=f'shard-{i + 1}', selectors=[], estimatedSeconds=0.0) for i in range(count)]
    class_loads = {shard['name']: defaultdict(float) for shard in bins}
    class_peaks = {shard['name']: 0.0 for shard in bins}
    for bundle in sorted(bundles, key=lambda items: (-sum(weight(item) for item in items),
                                                    min(key(item) for item in items))):
        seconds = sum(weight(unit) for unit in bundle)
        added = defaultdict(float)
        for unit in bundle:
            added[unit['method'].rsplit('.', 1)[0]] += weight(unit)
        def score(shard):
            loads = class_loads[shard['name']]
            class_seconds = max(loads.get(test_class, 0) + amount
                                for test_class, amount in added.items())
            projected = max(target, (shard['estimatedSeconds'] + seconds) / ESTIMATED_CLASS_WORKERS,
                            class_peaks[shard['name']], class_seconds)
            return projected, class_seconds, shard['estimatedSeconds'], len(shard['selectors']), shard['name']
        shard = min(bins, key=score)
        shard['selectors'].extend(bundle)
        shard['estimatedSeconds'] += seconds
        for test_class, amount in added.items():
            class_loads[shard['name']][test_class] += amount
        class_peaks[shard['name']] = max(class_peaks[shard['name']],
                                       max(class_loads[shard['name']][test_class] for test_class in added))
    for shard in bins:
        shard['selectors'].sort(key=key)
        shard['methods'] = sorted({unit['method'] for unit in shard['selectors']})
        shard['estimatedClassSeconds'] = dict(sorted(class_loads[shard['name']].items()))
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
