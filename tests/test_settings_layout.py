"""Preflight checks for the settings-card spacing regression."""
from pathlib import Path
import unittest

SOURCE = (Path(__file__).resolve().parents[1] / "src/TernionOverlay/SettingsForm.cs").read_text()
LANG = (Path(__file__).resolve().parents[1] / "src/TernionCore/Lang.cs").read_text()


class SettingsLayoutTests(unittest.TestCase):
    def test_hotkey_label_does_not_repeat_a_stale_default_binding(self):
        self.assertIn('["settings.hotkey"] = "Показать/скрыть:"', LANG)
        self.assertIn('["settings.hotkey"] = "Show/hide hotkey:"', LANG)
        self.assertNotIn('Показать/скрыть (Alt+T):', LANG)

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
        self.assertIn('e.Graphics.DrawImage(logo, new Rectangle(34, 16, 44, 44));', SOURCE)
        self.assertIn('title.Location = new Point(92, 7)', SOURCE)
        self.assertIn('subtitle.Location = new Point(94, 39)', SOURCE)
        self.assertNotIn('TextRenderer.DrawText(e.Graphics, "⚙"', SOURCE)

    def test_behavior_switches_use_an_inset_block(self):
        self.assertIn('var switches = new TableLayoutPanel', SOURCE)
        self.assertIn('Padding = new Padding(58, 8, 18, 18)', SOURCE)
        self.assertIn('Add(behavior, switches);', SOURCE)
        self.assertNotIn('Add(behavior, box);', SOURCE)

    def test_hotkey_dimensions_are_evenly_spaced(self):
        self.assertIn('dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));', SOURCE)
        self.assertIn('dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));', SOURCE)
        self.assertIn('fontSize.Margin = new Padding(0, 8, 18, 0);', SOURCE)
        self.assertIn('width.Margin = new Padding(0, 8, 0, 0);', SOURCE)

    def test_last_card_constrains_inner_tables_to_one_row(self):
        self.assertIn('Height = 66, ColumnCount = 2, RowCount = 1', SOURCE)
        self.assertIn('dimensions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));', SOURCE)
        self.assertIn('var fontPair = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1', SOURCE)
        self.assertIn('fontPair.RowStyles.Add(new RowStyle(SizeType.Percent, 100));', SOURCE)
        self.assertIn('var widthPair = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1', SOURCE)
        self.assertIn('widthPair.RowStyles.Add(new RowStyle(SizeType.Percent, 100));', SOURCE)

    def test_section_icons_are_centered_in_their_tiles(self):
        self.assertIn('var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);', SOURCE)
        self.assertIn('int x1 = center.X', SOURCE)

    def test_action_buttons_draw_rounded_edges_without_jagged_region(self):
        self.assertIn('internal sealed class RoundedActionButton : Button', SOURCE)
        self.assertIn('SmoothingMode = SmoothingMode.AntiAlias', SOURCE)
        self.assertIn('g.FillPath(fill, path);', SOURCE)
        self.assertNotIn('button.Region = new Region(path)', SOURCE)


if __name__ == '__main__':
    unittest.main()
