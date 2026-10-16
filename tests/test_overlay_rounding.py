"""Regression checks for symmetric colour-key-safe overlay capsule corners."""
from pathlib import Path
import unittest

SOURCE = (Path(__file__).resolve().parents[1] / "src/TernionOverlay/OverlayForm.cs").read_text()


class OverlayRoundingTests(unittest.TestCase):
    def test_capsules_use_consistent_pixel_aligned_curves(self):
        self.assertIn('Math.Min(pillH, 16)', SOURCE)
        self.assertIn('float right = x + w - 1f, bottom = y + h - 1f;', SOURCE)
        self.assertIn('g.SmoothingMode = SmoothingMode.None;', SOURCE)
        self.assertIn('new Pen(Color.FromArgb(68, 222, 126), 1f)', SOURCE)
        self.assertIn('g.DrawRoundRect(border, 4.5f + offset, y + 2.5f, pillW - 1, pillH - 1, Math.Min(pillH, 16));', SOURCE)


if __name__ == '__main__':
    unittest.main()
