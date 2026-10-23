#!/usr/bin/env python3
"""Check whether official pagination supports a bounded source-gap observation.

This reads captured JSON responses only. A conserved window is not evidence of
the identity, applicability or cause of entries absent from an official list.
"""

import argparse
from collections import Counter
import gzip
import hashlib
import json
from pathlib import Path


SCOPE_FIELDS = ('channelMachineName', 'smartListMachineName', 'locale')


def canonical(value):
    # Keep missing keys, explicit nulls, booleans and numeric values distinct.
    return json.dumps(value, sort_keys=True, ensure_ascii=False,
                      separators=(',', ':'), allow_nan=False).encode()


def inspect_window(response):
    metadata = response['metadata']
    for key in ('from', 'limit', 'totalItems'):
        value = metadata[key]
        if type(value) is not int or value < 0 or (key == 'limit' and value == 0):
            raise ValueError('Invalid window metadata: ' + key)
    for key in SCOPE_FIELDS:
        if not isinstance(metadata[key], str) or not metadata[key]:
            raise ValueError('Missing official scope: ' + key)
    rows = response['data']
    if not isinstance(rows, list):
        raise ValueError('Official data must be a list')
    capacity = max(0, min(metadata['limit'], metadata['totalItems'] - metadata['from']))
    if len(rows) > capacity:
        raise ValueError('Returned rows exceed the reported window')
    for row in rows:
        if not isinstance(row, dict) or not isinstance(row.get('id'), str) or not row['id'].strip():
            raise ValueError('Official row has no exact identity')
        canonical(row)
    counts = Counter(row['id'] for row in rows)
    return dict(offset=metadata['from'], limit=metadata['limit'], capacity=capacity,
                reportedTotal=metadata['totalItems'], returnedRows=len(rows),
                distinctIds=len(counts), unreturnedRowCount=capacity-len(rows),
                duplicateIds={key: n for key, n in sorted(counts.items()) if n > 1},
                orderedRowsSha256=hashlib.sha256(canonical(rows)).hexdigest())


def compare_rows(before, after):
    def indexed(rows):
        result = {}
        for row in rows:
            key = row['id']
            if key in result:
                raise ValueError('Duplicate identity in full-row comparison: ' + key)
            result[key] = canonical(row)
        return result

    left, right = indexed(before), indexed(after)
    return dict(addedIds=sorted(right.keys()-left.keys()),
                missingIds=sorted(left.keys()-right.keys()),
                changedIds=sorted(key for key in left.keys() & right.keys()
                                  if left[key] != right[key]))


def diagnose_partition(parent, children):
    parent_summary = inspect_window(parent)
    if not children:
        raise ValueError('No child windows')
    children = sorted(children, key=lambda child: child['metadata']['from'])
    child_summaries = [inspect_window(child) for child in children]
    metadata = parent['metadata']
    cursor = metadata['from']
    end = cursor + parent_summary['capacity']
    if cursor >= end:
        raise ValueError('Parent has no reported positions to partition')
    for child, summary in zip(children, child_summaries):
        cm = child['metadata']
        if any(cm[key] != metadata[key] for key in SCOPE_FIELDS + ('totalItems',)):
            raise ValueError('Official scope or reported total changed between windows')
        if cm['from'] != cursor or not summary['capacity']:
            raise ValueError('Child windows must partition the parent without gaps or overlaps')
        cursor += summary['capacity']
        if cursor > end:
            raise ValueError('Child windows extend beyond the parent')
    if cursor != end:
        raise ValueError('Child windows do not cover the parent')

    original = parent['data']
    combined = [row for child in children for row in child['data']]
    left_counts = Counter(row['id'] for row in original)
    right_counts = Counter(row['id'] for row in combined)
    left_content = Counter(canonical(row) for row in original)
    right_content = Counter(canonical(row) for row in combined)
    left_variants, right_variants = {}, {}
    for rows, variants in ((original, left_variants), (combined, right_variants)):
        for row in rows:
            variants.setdefault(row['id'], set()).add(canonical(row))
    duplicates = {key: n for key, n in sorted(right_counts.items()) if n > 1}
    identity_equal = left_counts == right_counts
    content_equal = left_content == right_content
    order_equal = [r['id'] for r in original] == [r['id'] for r in combined]
    stable = (identity_equal and content_equal and order_equal and not duplicates
              and not parent_summary['duplicateIds'])
    return dict(
        schemaVersion=1, scope={k: metadata[k] for k in SCOPE_FIELDS},
        parent=parent_summary, children=child_summaries,
        missingFromChildren=dict(sorted((left_counts-right_counts).items())),
        extraInChildren=dict(sorted((right_counts-left_counts).items())),
        combinedChildDuplicateIds=duplicates,
        changedSharedIds=sorted(key for key in left_variants.keys() & right_variants.keys()
                                if left_variants[key] != right_variants[key]),
        identityMultisetConserved=identity_equal,
        fullRowMultisetConserved=content_equal, orderedIdentitiesConserved=order_equal,
        supportsStableWindowObservation=stable,
        missingPrintingIdentities=None, providerRootCause='UNKNOWN',
        sourceGapClosed=False, semanticCoverageCertified=False,
        interpretation='WINDOW_CONSERVED_IDENTITY_OF_UNRETURNED_ENTRIES_UNKNOWN' if stable
        else 'WINDOW_NOT_CONSERVED_DO_NOT_LOCALIZE_MISSING_PRINTINGS')


def read_capture(path):
    raw = path.read_bytes()
    payload = gzip.decompress(raw) if path.suffix == '.gz' else raw
    return json.loads(payload), dict(path=str(path), archiveSha256=hashlib.sha256(raw).hexdigest(),
                                    rawSha256=hashlib.sha256(payload).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--parent', type=Path, required=True)
    parser.add_argument('--children', type=Path, nargs='+', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit('Output already exists; preserve earlier observations')
    parent, parent_ref = read_capture(args.parent)
    children = [read_capture(path) for path in args.children]
    report = diagnose_partition(parent, [data for data, _ in children])
    report['capturedInputs'] = dict(parent=parent_ref, children=[ref for _, ref in children])
    with args.output.open('x') as stream:
        stream.write(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(report['interpretation'])


if __name__ == '__main__':
    main()
