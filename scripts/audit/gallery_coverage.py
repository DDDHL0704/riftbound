"""Reconcile current official gallery views without importing historical cards."""

import json


def merge_gallery_views(views):
    if not views:
        raise ValueError('No current gallery views')
    total = views[0]['reportedTotal']
    if type(total) is not int or total < 0:
        raise ValueError('Invalid reported gallery count')
    merged = {}
    contents = {}
    summaries = []
    identities = []
    for view in views:
        if type(view['reportedTotal']) is not int or view['reportedTotal'] != total:
            raise ValueError('Gallery reported count changed between current views')
        counts = {}
        for card in view['records']:
            identity = card.get('id')
            if not isinstance(identity, str) or not identity:
                raise ValueError('Current gallery record has no exact identity')
            # Python equality merges True with 1 (including nested fields).
            # Compare JSON content while ignoring object member order only.
            content = json.dumps(card, sort_keys=True, ensure_ascii=False,
                                 separators=(',', ':'), allow_nan=False)
            if identity in contents and contents[identity] != content:
                raise ValueError('Conflicting current gallery identity: ' + identity)
            contents[identity] = content
            merged[identity] = card
            counts[identity] = counts.get(identity, 0) + 1
        identities.append(set(counts))
        summaries.append(dict(name=view['name'], rawEntries=len(view['records']),
            distinctEntries=len(counts), identicalDuplicateIds={
                identity: count for identity, count in sorted(counts.items()) if count > 1}))
    if len(merged) > total:
        raise ValueError('Current gallery identities exceed reported total')
    for summary, ids in zip(summaries, identities):
        summary['missingFromCurrentUnion'] = sorted(merged.keys() - ids)
    return [merged[identity] for identity in sorted(merged)], dict(
        reportedTotal=total, returnedEntries=len(merged),
        reportedMinusReturned=total - len(merged), views=summaries)
