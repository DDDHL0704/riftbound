import importlib.util
from pathlib import Path
import tempfile
import unittest
import re
import struct


SPEC = importlib.util.spec_from_file_location('desktop_gate', Path(__file__).resolve().parents[2] / 'clients/godot/tools/ci-desktop.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class DesktopExportConfigTests(unittest.TestCase):
    def test_windows_icon_is_complete_and_generated_exports_are_excluded(self):
        project = Path(__file__).resolve().parents[2] / 'clients/godot'
        config = (project / 'export_presets.cfg').read_text()
        self.assertIn('application/icon="res://assets/windows-icon.ico"', config)
        self.assertIn('application/console_wrapper_icon="res://assets/windows-icon.ico"', config)
        excluded = re.findall(r'^exclude_filter="([^"]*)"', config, re.M)
        self.assertEqual(2, len(excluded))
        self.assertTrue(all('exports/*' in filters.split(',') for filters in excluded))
        raw = (project / 'assets/windows-icon.ico').read_bytes()
        self.assertEqual((0, 1, 6), struct.unpack_from('<HHH', raw))
        end = 6 + 16 * 6
        for index, size in enumerate([16, 32, 48, 64, 128, 256]):
            width, height, _, reserved, planes, bpp, length, offset = struct.unpack_from('<BBBBHHII', raw, 6 + 16 * index)
            self.assertEqual((size % 256, size % 256, 0, 1, 32), (width, height, reserved, planes, bpp))
            self.assertEqual(end, offset)
            png = raw[offset:offset+length]
            self.assertTrue(png.startswith(b'\x89PNG\r\n\x1a\n'))
            self.assertEqual((size, size), struct.unpack_from('>II', png, 16))
            end += length
        self.assertEqual(len(raw), end)

    def test_restores_exact_project_bytes_after_export_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            original = b'[application]\r\nenabled=true\r\n[editor_plugins]\r\nenabled=PackedStringArray("res://addons/developer/plugin.cfg")\r\n[rendering]\r\nrenderer="mobile"\r\n'
            config = project / 'project.godot'
            config.write_bytes(original)
            with self.assertRaisesRegex(RuntimeError, 'export failure'):
                with GATE.packaging_without_editor_plugins(project):
                    during = config.read_bytes()
                    self.assertIn(b'[application]\r\nenabled=true', during)
                    self.assertIn(b'enabled=PackedStringArray()', during)
                    self.assertIn(b'renderer="mobile"', during)
                    raise RuntimeError('export failure')
            self.assertEqual(original, config.read_bytes())

    def test_a_project_without_editor_plugins_remains_identical(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            original = b'[application]\nenabled=true\n'
            config = project / 'project.godot'
            config.write_bytes(original)
            with GATE.packaging_without_editor_plugins(project):
                self.assertEqual(original, config.read_bytes())
            self.assertEqual(original, config.read_bytes())


if __name__ == '__main__':
    unittest.main()
