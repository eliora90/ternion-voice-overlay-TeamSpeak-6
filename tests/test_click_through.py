"""Клики не должны активировать окно оверлея: иначе игра теряет фокус и показывает курсор."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = (ROOT / "src/TernionOverlay/NativeMethods.cs").read_text(encoding="utf-8")
OVERLAY = (ROOT / "src/TernionOverlay/OverlayForm.cs").read_text(encoding="utf-8")


class FocusSafetyTests(unittest.TestCase):
    def test_noactivate_flag_is_declared(self):
        self.assertIn("WS_EX_NOACTIVATE = 0x08000000", NATIVE)

    def test_plain_window_never_activates(self):
        plain = NATIVE.split("public static void SetPlainWindow", 1)[1].split("/// <summary>", 1)[0]
        self.assertIn("WS_EX_NOACTIVATE", plain)

    def test_click_through_never_activates(self):
        through = NATIVE.split("public static void SetClickThrough", 1)[1]
        self.assertIn("WS_EX_NOACTIVATE", through)
        self.assertIn("WS_EX_TRANSPARENT", through)

    def test_create_params_survive_handle_recreation(self):
        # Хендл пересоздаётся при смене размера и DPI: стили должны задаваться в CreateParams,
        # иначе режим «клики насквозь» тихо теряется посреди игры.
        self.assertIn("protected override CreateParams CreateParams", OVERLAY)
        params = OVERLAY.split("protected override CreateParams CreateParams", 1)[1].split("protected override", 1)[0]
        self.assertIn("WS_EX_NOACTIVATE", params)
        self.assertIn("WS_EX_TRANSPARENT", params)

    def test_window_shows_without_activation(self):
        self.assertIn("ShowWithoutActivation => true", OVERLAY)

    def test_mouse_activate_is_ignored(self):
        self.assertIn("WM_MOUSEACTIVATE", NATIVE)
        self.assertIn("MA_NOACTIVATE", NATIVE)
        self.assertIn("MA_NOACTIVATE", OVERLAY)


if __name__ == "__main__":
    unittest.main()
