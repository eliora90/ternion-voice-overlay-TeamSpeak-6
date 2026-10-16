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
        self.assertIn('Size = new Size(760, 980);', SOURCE)
        self.assertIn('body.AutoScroll = true;', SOURCE)
        self.assertIn('Math.Min(Height, r.Height)', SOURCE)

    def test_reference_uses_airy_light_blue_cards(self):
        self.assertIn('Canvas = Color.FromArgb(239, 247, 255)', SOURCE)
        self.assertIn('body.Padding = new Padding(26, 2, 26, 0)', SOURCE)
        self.assertIn('Margin = new Padding(0, 0, 0, 10)', SOURCE)

    def test_header_shows_application_logo_instead_of_gear(self):
        self.assertIn('using var logo = Icon.ToBitmap();', SOURCE)
        self.assertIn('e.Graphics.DrawImage(logo, new Rectangle(34, 16, 44, 44));', SOURCE)
        self.assertIn('title.Location = new Point(92, 7)', SOURCE)
        self.assertIn('subtitle.Location = new Point(94, 39)', SOURCE)
        self.assertNotIn('TextRenderer.DrawText(e.Graphics, "⚙"', SOURCE)

    def test_behavior_switches_use_an_inset_block(self):
        self.assertIn('var switches = new TableLayoutPanel', SOURCE)
        self.assertIn('Height = 116,', SOURCE)
        self.assertIn('Padding = new Padding(66, 8, 18, 12)', SOURCE)
        self.assertIn('Add(behavior, switches);', SOURCE)
        self.assertNotIn('Add(behavior, box);', SOURCE)

    def test_hotkey_card_uses_three_separated_full_width_rows(self):
        self.assertIn('var hotkeyRows = new TableLayoutPanel', SOURCE)
        self.assertIn('Height = 153, ColumnCount = 2, RowCount = 3', SOURCE)
        self.assertIn('hotkeyRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));', SOURCE)
        self.assertIn('hotkeyRows.Controls.Add(hotkey, 1, 0);', SOURCE)
        self.assertIn('hotkeyRows.Controls.Add(fontSize, 1, 1);', SOURCE)
        self.assertIn('hotkeyRows.Controls.Add(width, 1, 2);', SOURCE)
        self.assertIn('e.Graphics.DrawLine(line, 66, 51, hotkeyRows.Width - 18, 51);', SOURCE)
        self.assertIn('e.Graphics.DrawLine(line, 66, 102, hotkeyRows.Width - 18, 102);', SOURCE)
        self.assertNotIn('Add(appearance, Row("settings.hotkey", hotkey));', SOURCE)

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
