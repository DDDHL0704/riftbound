import csv
import importlib.util
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "registration-hotspots.py"
SPEC = importlib.util.spec_from_file_location("registration_hotspots", SCRIPT)
hotspots = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(hotspots)


class RegistrationHotspotTests(unittest.TestCase):
    def ledger(self, rows):
        temp = tempfile.NamedTemporaryFile(mode="w", suffix=".csv", delete=False,
                                           newline="", encoding="utf-8")
        self.addCleanup(lambda: Path(temp.name).unlink(missing_ok=True))
        fields = list(hotspots.REQUIRED)
        writer = csv.DictWriter(temp, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
        temp.close()
        return Path(temp.name)

    def row(self, pr, status="merged", value="0"):
        return {"pr": str(pr), "branch": f"line/{pr}", "title": f"batch {pr}",
                "status": status, **{target: value for target in hotspots.TARGETS}}

    def test_merged_only_can_exclude_pending_batches(self):
        path = self.ledger([self.row(1, value="1"), self.row(2, status="open", value="1")])
        rows = hotspots.load(path)
        all_batches = hotspots.summarize(rows)
        merged = hotspots.summarize(rows, merged_only=True)
        self.assertEqual(2, all_batches["batchCount"])
        self.assertEqual(2, all_batches["counts"][hotspots.TARGETS[0]])
        self.assertEqual(1, merged["batchCount"])
        self.assertEqual(1, merged["counts"][hotspots.TARGETS[0]])

    def test_duplicate_pr_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "duplicate PR"):
            hotspots.load(self.ledger([self.row(1), self.row(1)]))

    def test_non_binary_source_value_is_rejected(self):
        row = self.row(1)
        row[hotspots.TARGETS[0]] = "2"
        with self.assertRaisesRegex(ValueError, "non-binary"):
            hotspots.load(self.ledger([row]))


if __name__ == "__main__":
    unittest.main()
