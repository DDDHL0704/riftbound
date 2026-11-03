#!/usr/bin/env python3
"""Attribute verified CanonicalGame phase traces to complete local parameter rows."""

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re
import xml.etree.ElementTree as ET

NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
PARTS = ('frameMilliseconds', 'jsonMilliseconds', 'registryMilliseconds',
         'viewsMilliseconds', 'duplicateMilliseconds')


def sha(value):
    return hashlib.sha256(value).hexdigest()


def milliseconds(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError('Phase durations must be finite nonnegative numbers.')
    return value


def integer(value, minimum=0):
    if type(value) is not int or value < minimum:
        raise ValueError('Command counts and ticks must be nonnegative integers.')
    return value


def duration(value):
    if not isinstance(value, str) or not re.fullmatch(r'\d+:[0-5]\d:[0-5]\d(?:\.\d+)?', value):
        raise ValueError('Invalid TRX duration.')
    hours, minutes, seconds = value.split(':')
    result = int(hours) * 3600 + int(minutes) * 60 + float(seconds)
    if not math.isfinite(result):
        raise ValueError('Invalid TRX duration.')
    return result


def report(trx, cases, phases, cold_counts):
    observed = {}

    def read(path):
        path = Path(path).resolve()
        value = path.read_bytes()
        if path in observed and observed[path] != value:
            raise ValueError('Input changed while being read.')
        observed[path] = value
        return value

    tree = ET.fromstring(read(trx))
    rows = tree.findall('.//t:UnitTestResult', NS)
    counters = tree.find('.//t:Counters', NS)
    names = [name for name, _ in cases]
    if not names or len(set(names)) != len(names) or Counter(row.get('testName') for row in rows) != Counter(names):
        raise ValueError('TRX must contain exactly the uniquely named requested parameter rows.')
    if (counters is None or any(row.get('outcome') != 'Passed' for row in rows)
            or {key: int(counters.get(key, '-1')) for key in ('total', 'passed', 'failed', 'notExecuted')}
            != dict(total=len(names), passed=len(names), failed=0, notExecuted=0)):
        raise ValueError('Complete passing TRX counters are required.')
    seconds = {row.get('testName'): duration(row.get('duration')) for row in rows}
    histories = []
    for name, path in cases:
        frame = json.loads(read(path))['frame']
        if frame.get('isConsistent') is not True or frame.get('validationErrors') != [] or not frame.get('commands'):
            raise ValueError('A consistent nonempty canonical history is required.')
        commands = frame['commands']
        if any(command.get('accepted') is not True for command in commands):
            raise ValueError('Canonical phase histories must contain accepted commands only.')
        vector = [(index, integer(command['completedTick']), command['commandType'])
                  for index, command in enumerate(commands, 1)]
        if (not isinstance(frame['roomId'], str) or not frame['roomId']
                or any(not isinstance(command['commandType'], str) or not command['commandType'] for command in commands)):
            raise ValueError('History room and command types must be nonempty strings.')
        if commands[-1]['completedTick'] != integer(frame['currentTick']):
            raise ValueError('History final tick differs from its last command.')
        histories.append(dict(name=name, path=str(Path(path).resolve()), room=frame['roomId'], vector=vector,
                              expectedColdRecoveries=integer(cold_counts[name], 1)))

    folder = Path(phases).resolve()
    files = sorted(folder.glob('*.jsonl'))
    if len(files) != len(histories):
        raise ValueError('A complete phase file is required for every history.')
    assigned, results = set(), []
    for path in files:
        values = [json.loads(line) for line in read(path).splitlines() if line.strip()]
        commands = [row for row in values if row.get('phase') == 'command']
        for row in values:
            integer(row['commandCount'], 1)
            integer(row['tick'])
        vector = [(row['commandCount'], row['tick'], row['command']) for row in commands]
        matches = [index for index, history in enumerate(histories)
                   if vector == history['vector'] and {row['room'] for row in values} == {history['room']}]
        if len(matches) != 1 or matches[0] in assigned:
            raise ValueError('Phase trace does not identify one unique complete accepted history.')
        index = matches[0]
        assigned.add(index)
        history = histories[index]
        totals = dict(commandMilliseconds=0, coldMilliseconds=0, **{key: 0 for key in PARTS})
        cold, command_count, by_command = [], 0, {}
        for row in values:
            total = milliseconds(row['totalMilliseconds'])
            if row['phase'] == 'command':
                command_count += 1
                totals['commandMilliseconds'] += total
                group = by_command.setdefault(row['command'], dict(executions=0, totalSeconds=0))
                group['executions'] += 1
                group['totalSeconds'] += total / 1000
            elif row['phase'] == 'cold-recovery':
                count = row['commandCount']
                if (not command_count or count != command_count
                        or row['tick'] != history['vector'][count - 1][1]):
                    raise ValueError('Cold recovery does not bind the current accepted command prefix.')
                parts = {key: milliseconds(row[key]) for key in PARTS}
                if abs(sum(parts.values()) - total) > 0.001:
                    raise ValueError('Cold phase components differ from their total.')
                totals['coldMilliseconds'] += total
                for key, value in parts.items():
                    totals[key] += value
                cold.append(dict(commandCount=count, tick=row['tick'], totalSeconds=total / 1000,
                                 **{key.replace('Milliseconds', 'Seconds'): value / 1000 for key, value in parts.items()}))
            else:
                raise ValueError('Unknown canonical phase.')
        if not cold:
            raise ValueError('At least one complete cold recovery is required.')
        if len(cold) != history['expectedColdRecoveries']:
            raise ValueError('Phase trace differs from the expected cold recovery count.')
        measured = (totals['commandMilliseconds'] + totals['coldMilliseconds']) / 1000
        milliseconds(measured)
        residual = seconds[history['name']] - measured
        if residual < -0.001:
            raise ValueError('Phase durations exceed the corresponding TRX row duration.')
        results.append(dict(name=history['name'], history=history['path'], phaseFile=str(path),
                            trxSeconds=seconds[history['name']], commands=len(vector), coldRecoveries=len(cold),
                            unattributedSeconds=max(0, residual),
                            **{key.replace('Milliseconds', 'Seconds'): value / 1000 for key, value in totals.items()},
                            commandTypes=by_command,
                            slowestCommands=[dict(commandCount=row['commandCount'], tick=row['tick'],
                                                  command=row['command'], totalSeconds=row['totalMilliseconds'] / 1000)
                                             for row in sorted(commands, key=lambda row: row['totalMilliseconds'], reverse=True)[:10]],
                            checkpoints=cold))
    if files != sorted(folder.glob('*.jsonl')) or any(path.read_bytes() != value for path, value in observed.items()):
        raise ValueError('Inputs changed while reporting.')
    return dict(status='CANONICAL_PHASE_TIMINGS_REPORTED', executions=len(results),
                cases=sorted(results, key=lambda row: row['name']),
                inputs=[dict(path=str(path), sha256=sha(value)) for path, value in sorted(observed.items())],
                limitation='Phase values are wall time. Unattributed time includes assertions, forgery probes, exports and other work; '
                           'it is not assigned to any of those activities. This analysis does not establish CI speedup or replace validation.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--trx', required=True, type=Path)
    parser.add_argument('--case', required=True, action='append', nargs=3,
                        metavar=('TRX_NAME', 'HISTORY_JSON', 'COLD_RECOVERIES'),
                        help='Complete parameter row, exported history, and known number of cold checkpoints.')
    parser.add_argument('--phases', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    result = report(args.trx, [(name, path) for name, path, count in args.case], args.phases,
                    {name: int(count) for name, path, count in args.case})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('x') as output:
        json.dump(result, output, indent=2, allow_nan=False)
        output.write('\n')
    print(result['status'], result['executions'], 'parameter rows')


if __name__ == '__main__':
    main()
