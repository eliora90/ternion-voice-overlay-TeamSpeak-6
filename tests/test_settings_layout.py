"""Preflight checks for the settings-card spacing regression."""
from pathlib import Path
import unittest

SOURCE = (Path(__file__).resolve().parents[1] / "src/TernionOverlay/SettingsForm.cs").read_text()


class SettingsLayoutTests(unittest.TestCase):
    def test_approved_brand_blue_replaces_old_accent(self):
        self.assertTrue('internal static readonly Color Blue = Color.FromArgb(12, 74, 160);' in SOURCE,
                        'Settings accent should be #0C4AA0')
        self.assertNotIn('Color.FromArgb(19, 108, 231)', SOURCE)
        self.assertTrue('using var background = new SolidBrush(Checked ? SettingsForm.Blue' in SOURCE,
                        'Switches should use the same blue')
        self.assertTrue('using var fill = new SolidBrush(Blue);' in SOURCE,
                        'Section icon tiles should use the approved blue')
        self.assertTrue('using var pen = new Pen(Color.White, 1.7f)' in SOURCE,
                        'White symbols should remain legible on blue tiles')

    def test_tall_window_and_short_screen_fallback(self):
        self.assertIn('Size = new Size(760, 960);', SOURCE)
        self.assertIn('body.AutoScroll = true;', SOURCE)
        self.assertIn('Math.Min(Height, r.Height)', SOURCE)

    def test_header_shows_application_logo_instead_of_gear(self):
        self.assertIn('using var logo = Icon.ToBitmap();', SOURCE)
        self.assertIn('e.Graphics.DrawImage(logo, new Rectangle(20, 16, 44, 44));', SOURCE)
        self.assertNotIn('TextRenderer.DrawText(e.Graphics, "⚙"', SOURCE)

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
