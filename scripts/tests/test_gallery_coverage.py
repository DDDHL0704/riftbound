import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('gallery_coverage',
    Path(__file__).parents[1] / 'audit/gallery_coverage.py')
gallery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gallery)


def view(name, records, total=4):
    return dict(name=name, records=records, reportedTotal=total)


class GalleryCoverageTests(unittest.TestCase):
    def test_matching_raw_count_with_duplicate_does_not_hide_missing_printing(self):
        a = dict(id='SET-001', text='first')
        b = dict(id='SET-001a', text='variant')
        c = dict(id='SET-002', text='second')
        rows, receipt = gallery.merge_gallery_views([
            view('pagination', [a, a, c]), view('wholeList', [a, b, c]),
            view('pageSsr', [c, b, a])])
        self.assertEqual([a, b, c], rows)
        self.assertEqual(1, receipt['reportedMinusReturned'])
        self.assertEqual({'SET-001': 2}, receipt['views'][0]['identicalDuplicateIds'])
        self.assertEqual(['SET-001a'], receipt['views'][0]['missingFromCurrentUnion'])

    def test_each_current_view_contributes_exact_identities_without_historical_fill(self):
        a, b, c = [dict(id=identity) for identity in ['A', 'B', 'C']]
        rows, receipt = gallery.merge_gallery_views([
            view('pagination', [a]), view('wholeList', [b]), view('pageSsr', [c])])
        self.assertEqual([a, b, c], rows)
        self.assertEqual(1, receipt['reportedMinusReturned'])

    def test_content_conflicts_fail_instead_of_silently_preferring_a_view(self):
        for changed in [dict(id='A', text='changed', flags=[]),
                        dict(id='A', text='old', flags=['new'])]:
            with self.subTest(changed=changed), self.assertRaisesRegex(ValueError, 'Conflicting'):
                gallery.merge_gallery_views([view('page', [dict(id='A', text='old', flags=[])]),
                    view('whole', [changed])])

    def test_reported_count_drift_fails_before_claiming_a_complete_collection(self):
        with self.assertRaisesRegex(ValueError, 'count changed'):
            gallery.merge_gallery_views([view('page', [dict(id='A')], 2),
                view('whole', [dict(id='B')], 3)])

    def test_missing_identity_and_excess_identity_count_fail(self):
        for card in [{}, dict(id=None), dict(id='')]:
            with self.subTest(card=card), self.assertRaisesRegex(ValueError, 'exact identity'):
                gallery.merge_gallery_views([view('page', [card])])
        with self.assertRaisesRegex(ValueError, 'exceed'):
            gallery.merge_gallery_views([view('page', [dict(id='A'), dict(id='B')], 1)])

    def test_empty_views_and_invalid_reported_count_cannot_become_zero_gap_success(self):
        with self.assertRaisesRegex(ValueError, 'No current'):
            gallery.merge_gallery_views([])
        for total in [None, -1, True, '1']:
            with self.subTest(total=total), self.assertRaisesRegex(ValueError, 'Invalid'):
                gallery.merge_gallery_views([view('page', [], total)])


if __name__ == '__main__':
    unittest.main()
