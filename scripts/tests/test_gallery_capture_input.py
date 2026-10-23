import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


TOOL = Path(__file__).parents[1] / 'audit/gallery_gap_diagnostics.py'
spec = importlib.util.spec_from_file_location('gallery_capture_input', TOOL)
gap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gap)

METADATA = ('"channelMachineName":"riftbound_website",'
            '"smartListMachineName":"riftbound_gallery_cards",'
            '"locale":"en-us","from":0,"limit":1,"totalItems":1')
VALID = '{"metadata":{' + METADATA + '},"data":[{"id":"B"}]}'
DUPLICATES = [
    '{"metadata":{' + METADATA + '},"data":[{"id":"A","id":"B"}]}',
    '{"metadata":{"locale":"zh-cn",' + METADATA + '},"data":[{"id":"B"}]}',
    '{"metadata":{' + METADATA + '},"data":[{"id":"A"}],"data":[{"id":"B"}]}',
    '{"metadata":{' + METADATA + '},"data":[{"id":"B","text":"old","text":"new"}]}',
    '{"metadata":{' + METADATA + '},"data":[{"id":"B","id":"B"}]}',
    '{"metadata":{' + METADATA + '},"data":[{"id":"A","\\u0069d":"B"}]}',
]


class GalleryCaptureInputTests(unittest.TestCase):
    def test_duplicate_members_are_rejected_in_plain_and_gzip_captures(self):
        with tempfile.TemporaryDirectory() as directory:
            for i, text in enumerate(DUPLICATES):
                for suffix in ('.json', '.json.gz'):
                    raw = text.encode()
                    stored = gzip.compress(raw, mtime=0) if suffix.endswith('.gz') else raw
                    path = Path(directory) / (str(i) + suffix)
                    path.write_bytes(stored)
                    with self.subTest(case=i, suffix=suffix), \
                         self.assertRaisesRegex(ValueError, 'Duplicate JSON object member'):
                        gap.read_capture(path)
                    self.assertEqual(stored, path.read_bytes())

    def test_reused_names_in_separate_objects_preserve_values_and_input_hashes(self):
        value = json.loads(VALID)
        value['data'][0]['details'] = [dict(value=True), dict(value=1),
                                      dict(value=1.0), dict(value=None), dict(value='中文')]
        raw = json.dumps(value, ensure_ascii=False, indent=2).encode()
        with tempfile.TemporaryDirectory() as directory:
            for suffix in ('.json', '.json.gz'):
                stored = gzip.compress(raw, mtime=0) if suffix.endswith('.gz') else raw
                path = Path(directory) / ('valid' + suffix)
                path.write_bytes(stored)
                with self.subTest(suffix=suffix):
                    observed, receipt = gap.read_capture(path)
                    self.assertEqual(gap.canonical(value), gap.canonical(observed))
                    self.assertEqual(hashlib.sha256(stored).hexdigest(), receipt['archiveSha256'])
                    self.assertEqual(hashlib.sha256(raw).hexdigest(), receipt['rawSha256'])
                    self.assertEqual(stored, path.read_bytes())

    def test_cli_refuses_ambiguous_parent_or_child_without_creating_a_report(self):
        for bad_input in ('parent', 'child'):
            with self.subTest(bad_input=bad_input), tempfile.TemporaryDirectory() as directory:
                parent, child = Path(directory) / 'parent.json', Path(directory) / 'child.json.gz'
                parent.write_text(DUPLICATES[0] if bad_input == 'parent' else VALID)
                child.write_bytes(gzip.compress((DUPLICATES[1] if bad_input == 'child' else VALID).encode(), mtime=0))
                originals = {path: path.read_bytes() for path in (parent, child)}
                output = Path(directory) / 'report.json'
                result = subprocess.run([sys.executable, str(TOOL), '--parent', str(parent),
                    '--children', str(child), '--output', str(output)], capture_output=True, text=True)
                self.assertNotEqual(0, result.returncode)
                self.assertIn('Duplicate JSON object member', result.stderr)
                self.assertFalse(output.exists())
                for path, raw in originals.items():
                    self.assertEqual(raw, path.read_bytes())


if __name__ == '__main__':
    unittest.main()
