"""Fresh-install defaults for the overlay panel."""
from pathlib import Path
import unittest

CONFIG = (Path(__file__).resolve().parents[1] / "src/TernionOverlay/Config.cs").read_text(encoding="utf-8")
LANG = (Path(__file__).resolve().parents[1] / "src/TernionCore/Lang.cs").read_text(encoding="utf-8")


class DefaultsTests(unittest.TestCase):
    def test_panel_width_defaults_to_200(self):
        self.assertIn("public int WindowWidth { get; set; } = 200;", CONFIG)

    def test_font_size_defaults_to_12(self):
        self.assertIn("public int FontSize { get; set; } = 12;", CONFIG)

    def test_language_defaults_to_english(self):
        self.assertIn('public string Language { get; set; } = "en";', CONFIG)
        self.assertIn('public static string Current = "en";', LANG)

    def test_defaults_within_settings_control_ranges(self):
        settings = (Path(__file__).resolve().parents[1] / "src/TernionOverlay/SettingsForm.cs").read_text(encoding="utf-8")
        self.assertIn("width.Minimum = 180; width.Maximum = 700;", settings)
        self.assertIn("fontSize.Minimum = 10; fontSize.Maximum = 26;", settings)


if __name__ == "__main__":
    unittest.main()
