"""Pre-release checks for the unified Ternion icon."""
from pathlib import Path
import struct
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "TernionOverlay"


class AppIconTests(unittest.TestCase):
    def test_icon_tile_uses_approved_blue(self):
        image = Image.open(ROOT / "assets" / "ternion-icon.png").convert("RGBA")
        self.assertEqual(image.getpixel((256, 30)), (12, 74, 160, 255))

    def test_icon_contains_windows_sizes(self):
        data = (PROJECT / "Assets" / "Ternion.ico").read_bytes()
        reserved, icon_type, count = struct.unpack_from("<HHH", data)
        self.assertEqual((reserved, icon_type), (0, 1))
        sizes = set()
        for i in range(count):
            w, h = struct.unpack_from("<BB", data, 6 + i * 16)
            sizes.add((w or 256, h or 256))
        self.assertTrue({(16, 16), (32, 32), (48, 48), (256, 256)}.issubset(sizes))

    def test_chosen_serif_t_is_the_source_design(self):
        script = (ROOT / "scripts" / "generate_icon.py").read_text()
        self.assertIn("DejaVuSerif-Bold.ttf", script)
        self.assertIn("d.text((x, y), 'T'", script)

    def test_exe_embeds_icon(self):
        xml = (PROJECT / "TernionOverlay.csproj").read_text()
        self.assertIn("<ApplicationIcon>Assets/Ternion.ico</ApplicationIcon>", xml)

    def test_tray_and_settings_use_embedded_icon(self):
        program = (PROJECT / "Program.cs").read_text()
        settings = (PROJECT / "SettingsForm.cs").read_text()
        self.assertIn("Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)", program)
        self.assertIn("Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)", settings)


if __name__ == "__main__":
    unittest.main()
