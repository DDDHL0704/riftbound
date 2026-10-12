import importlib.util
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location('desktop_gate', Path(__file__).resolve().parents[2] / 'clients/godot/tools/ci-desktop.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class DesktopExportConfigTests(unittest.TestCase):
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
