import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location('gap_diagnostics',
    Path(__file__).parents[1] / 'audit/gallery_gap_diagnostics.py')
gap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gap)


def window(start, limit, ids, total=4):
    return dict(metadata=dict(channelMachineName='riftbound_website',
        smartListMachineName='riftbound_gallery_cards', locale='en-us',
        **{'from': start, 'limit': limit, 'totalItems': total}),
        data=[dict(id=identity) for identity in ids])


class GalleryGapDiagnosticsTests(unittest.TestCase):
    def test_equal_counts_can_hide_a_missing_printing_and_repeated_neighbor(self):
        report = gap.diagnose_partition(window(0, 4, ['A', 'B', 'C', 'D']),
            [window(0, 2, ['A', 'C']), window(2, 2, ['C', 'D'])])
        self.assertEqual({'B': 1}, report['missingFromChildren'])
        self.assertEqual({'C': 1}, report['extraInChildren'])
        self.assertEqual({'C': 2}, report['combinedChildDuplicateIds'])
        self.assertFalse(report['supportsStableWindowObservation'])

    def test_sparse_consistent_window_never_certifies_the_unreturned_identity(self):
        report = gap.diagnose_partition(window(0, 4, ['A', 'C', 'D']),
            [window(2, 2, ['C', 'D']), window(0, 2, ['A'])])
        self.assertTrue(report['supportsStableWindowObservation'])
        self.assertEqual(1, report['parent']['unreturnedRowCount'])
        self.assertIsNone(report['missingPrintingIdentities'])
        self.assertFalse(report['sourceGapClosed'])
        self.assertFalse(report['semanticCoverageCertified'])

    def test_reordered_equal_set_cannot_locate_stable_offsets(self):
        report = gap.diagnose_partition(window(0, 4, ['A', 'B', 'C', 'D']),
            [window(0, 2, ['B', 'A']), window(2, 2, ['C', 'D'])])
        self.assertTrue(report['identityMultisetConserved'])
        self.assertFalse(report['orderedIdentitiesConserved'])
        self.assertFalse(report['supportsStableWindowObservation'])

    def test_same_id_changed_fields_null_presence_or_json_type_fail_conservation(self):
        for before, after in [({}, {'text': None}), ({'value': 1}, {'value': True}),
                              ({'text': 'old'}, {'text': 'new'})]:
            with self.subTest(before=before, after=after):
                parent, child = window(0, 1, ['A'], 1), window(0, 1, ['A'], 1)
                parent['data'][0].update(before)
                child['data'][0].update(after)
                report = gap.diagnose_partition(parent, [child])
                self.assertEqual(['A'], report['changedSharedIds'])
                self.assertFalse(report['supportsStableWindowObservation'])

    def test_consistent_duplicates_do_not_become_unique_position_evidence(self):
        report = gap.diagnose_partition(window(0, 2, ['A', 'A'], 2),
            [window(0, 1, ['A'], 2), window(1, 1, ['A'], 2)])
        self.assertTrue(report['fullRowMultisetConserved'])
        self.assertFalse(report['supportsStableWindowObservation'])

    def test_mixed_language_list_channel_or_count_is_not_comparable(self):
        for key, value in [('locale', 'zh-cn'), ('smartListMachineName', 'other'),
                           ('channelMachineName', 'other'), ('totalItems', 5)]:
            child = window(0, 2, ['A', 'B'])
            child['metadata'][key] = value
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'scope or reported total'):
                gap.diagnose_partition(window(0, 4, ['A', 'B']),
                    [child, window(2, 2, [])])

    def test_gaps_overlaps_and_outside_windows_are_rejected(self):
        for children in [[window(0, 2, [])], [window(0, 2, []), window(1, 2, [])],
                         [window(0, 1, []), window(2, 2, [])], [window(0, 5, [], 8)]]:
            with self.subTest(children=children), self.assertRaises(ValueError):
                gap.diagnose_partition(window(0, 4, []), children)

    def test_last_page_uses_actual_reported_capacity(self):
        report = gap.diagnose_partition(window(4, 200, ['E'], 6),
            [window(4, 1, ['E'], 6), window(5, 1, [], 6)])
        self.assertEqual(2, report['parent']['capacity'])
        self.assertTrue(report['supportsStableWindowObservation'])

    def test_invalid_numbers_or_rows_do_not_become_empty_success(self):
        for key, value in [('from', True), ('from', -1), ('limit', 0),
                           ('limit', '2'), ('totalItems', False)]:
            value_window = window(0, 2, [])
            value_window['metadata'][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                gap.inspect_window(value_window)
        for row in [None, {}, {'id': ''}, {'id': 3}]:
            value_window = window(0, 2, [])
            value_window['data'] = [row]
            with self.subTest(row=row), self.assertRaises(ValueError):
                gap.inspect_window(value_window)
        with self.assertRaises(ValueError):
            gap.inspect_window(window(0, 1, ['A', 'B']))

    def test_full_row_diff_preserves_exact_variants_and_absent_fields(self):
        result = gap.compare_rows([{'id': 'A'}, {'id': 'B'}],
                                  [{'id': 'A', 'text': None}, {'id': 'Ba'}])
        self.assertEqual(dict(addedIds=['Ba'], missingIds=['B'], changedIds=['A']), result)
        with self.assertRaisesRegex(ValueError, 'Duplicate identity'):
            gap.compare_rows([{'id': 'A'}, {'id': 'A'}], [])


if __name__ == '__main__':
    unittest.main()
