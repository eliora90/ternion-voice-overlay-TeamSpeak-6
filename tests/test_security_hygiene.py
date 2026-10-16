"""Public-release hygiene checks: no personal data leaks in tracked sources.

Two layers:

* optional local blocklist ``tests/personal-tokens.txt`` (git-ignored, never
  published) listing real nicknames / names of the owner's TeamSpeak server;
  it is read when present so the repository never has to contain them;
* generic shape checks (consumer e-mail domains, non-synthetic GUIDs) that work
  in a fresh clone.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
BLOCKLIST = Path(__file__).resolve().parent / "personal-tokens.txt"
TRACKED_SOURCES = sorted(
    list((ROOT / "src").rglob("*.cs")) + list((ROOT / "tests").glob("*.py"))
    + [ROOT / "README.md", ROOT / "README.ru.md"]
)
SOURCE_FILES = sorted(list((ROOT / "src").rglob("*.cs")) + list((ROOT / "tests").glob("*.py")))

CONSUMER_MAIL = re.compile(r"@(gmail|googlemail|yandex|ya|mail|inbox|bk|list|outlook|hotmail)\.(ru|com|by|kz)")
GUID = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")
URL = re.compile(r"https?://\S+")
# Public constants from specifications, not service identifiers.
KNOWN_PUBLIC_GUIDS = {"258eafa5-e914-47da-95ca-c5ab0dc85b11"}


def without_urls(text: str) -> str:
    """Drop links: the publishing account name legitimately appears in repository URLs."""
    return URL.sub("", text)


class SecurityHygieneTests(unittest.TestCase):
    def test_local_blocklist_is_absent_from_every_tracked_source(self):
        if not BLOCKLIST.exists():
            self.skipTest("no local blocklist")
        tokens = [t.strip() for t in BLOCKLIST.read_text(encoding="utf-8").splitlines()
                  if t.strip() and not t.startswith("#")]
        for path in TRACKED_SOURCES:
            text = without_urls(path.read_text(encoding="utf-8"))
            for token in tokens:
                self.assertNotIn(token, text, f"{token!r} must not appear in {path.name}")

    def test_no_consumer_email_addresses(self):
        for path in TRACKED_SOURCES:
            found = CONSUMER_MAIL.search(without_urls(path.read_text(encoding="utf-8")))
            self.assertIsNone(found, f"{path.name} must not contain a personal e-mail address")

    def test_service_identifiers_stay_obviously_synthetic(self):
        # Documentation legitimately links GitHub attachment UUIDs; only code matters here.
        for path in SOURCE_FILES:
            for value in GUID.findall(path.read_text(encoding="utf-8")):
                if value.lower() in KNOWN_PUBLIC_GUIDS:
                    continue
                distinct = len(set(value.replace("-", "").lower()))
                self.assertLessEqual(distinct, 6, f"{path.name} contains a realistic GUID: {value}")

    def test_avatar_downloads_are_size_limited(self):
        source = (ROOT / "src/TernionOverlay/AvatarCache.cs").read_text(encoding="utf-8")
        self.assertIn("private const long MaxAvatarBytes", source)
        self.assertIn("ContentLength", source)
        self.assertIn("ReadAsStreamAsync", source)

    def test_avatar_downloads_accept_only_http_and_https(self):
        source = (ROOT / "src/TernionOverlay/AvatarCache.cs").read_text(encoding="utf-8")
        self.assertIn("Uri.UriSchemeHttps", source)
        self.assertIn("Uri.UriSchemeHttp", source)
        self.assertIn("Uri.TryCreate(url, UriKind.Absolute, out var uri)", source)


if __name__ == "__main__":
    unittest.main()
