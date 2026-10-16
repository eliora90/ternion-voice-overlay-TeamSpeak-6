"""Draw a small-size legible Ternion app icon and a multi-resolution Windows ICO.

Run from the repository root: python3 scripts/generate_icon.py
Requires Pillow and the DejaVu Serif Bold font. The generated assets are committed;
end users do not need that font installed.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
size = 1024
im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
navy = (18, 31, 53, 255)
white = (244, 249, 255, 255)
green = (61, 225, 143, 255)
# Dark rounded tile; no thin border to disappear at tray scale.
d.rounded_rectangle((35, 35, 989, 989), radius=232, fill=navy)
# The selected serif T (option 2) remains separated from the green voice bars.
font_path = Path('/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf')
font = ImageFont.truetype(str(font_path), 690)
left, top, right, bottom = d.textbbox((0, 0), 'T', font=font)
x = 445 - (left + right) / 2
y = 527 - (top + bottom) / 2
d.text((x, y), 'T', font=font, fill=white)
# Two rising voice bars double as a distinctive right-hand accent.
d.rounded_rectangle((645, 555, 723, 697), radius=36, fill=green)
d.rounded_rectangle((759, 464, 837, 755), radius=36, fill=green)

preview = root / "assets" / "ternion-icon.png"
ico = root / "src" / "TernionOverlay" / "Assets" / "Ternion.ico"
preview.parent.mkdir(parents=True, exist_ok=True)
ico.parent.mkdir(parents=True, exist_ok=True)
im.resize((512, 512), Image.Resampling.LANCZOS).save(preview)
im.save(ico, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print(f"Wrote {ico} and {preview}")
