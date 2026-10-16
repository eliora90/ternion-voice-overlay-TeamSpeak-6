"""Preflight checks for the settings-card spacing regression."""
from pathlib import Path
import unittest

SOURCE = (Path(__file__).resolve().parents[1] / "src/TernionOverlay/SettingsForm.cs").read_text()


class SettingsLayoutTests(unittest.TestCase):
    def test_behavior_switches_use_an_inset_block(self):
        self.assertIn('var switches = new TableLayoutPanel', SOURCE)
        self.assertIn('Padding = new Padding(58, 8, 18, 18)', SOURCE)
        self.assertIn('Add(behavior, switches);', SOURCE)
        self.assertNotIn('Add(behavior, box);', SOURCE)

    def test_hotkey_dimensions_are_evenly_spaced(self):
        self.assertIn('dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));', SOURCE)
        self.assertIn('dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));', SOURCE)
        self.assertIn('fontSize.Margin = new Padding(0, 0, 18, 0);', SOURCE)
        self.assertIn('width.Margin = new Padding(0, 0, 0, 0);', SOURCE)

    def test_section_icons_are_centered_in_their_tiles(self):
        self.assertIn('var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);', SOURCE)
        self.assertIn('int x1 = center.X', SOURCE)


if __name__ == '__main__':
    unittest.main()
