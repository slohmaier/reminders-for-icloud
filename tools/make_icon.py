"""Generate icon.ico for Reminders for iCloud.

Design: rounded orange square + bold white checkmark.
Saves a multi-size .ico (16/24/32/48/64/128/256) at the project root.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "icon.ico"

# Distinct from Notes (blue) so they're trivially separable in taskbar/Alt-Tab.
BG = (242, 130, 38, 255)        # warm orange #F28226
BG_DARK = (200, 90, 20, 255)
MARK = (255, 255, 255, 245)


def _rounded_square(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    radius = max(2, size // 5)

    grad = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    for y in range(size):
        t = y / max(1, size - 1)
        r = int(BG[0] * (1 - t) + BG_DARK[0] * t)
        g = int(BG[1] * (1 - t) + BG_DARK[1] * t)
        b = int(BG[2] * (1 - t) + BG_DARK[2] * t)
        gd.line([(0, y), (size, y)], fill=(r, g, b, 255))

    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([(0, 0), (size - 1, size - 1)], radius=radius, fill=255)

    img.paste(grad, (0, 0), mask)
    return img


def _draw_check(img: Image.Image) -> None:
    size = img.size[0]
    d = ImageDraw.Draw(img)

    # Bold checkmark. Three points: bottom-left of stroke, the V tip, top-right.
    # Coordinates in normalized space, scaled to icon.
    p1 = (size * 0.22, size * 0.52)
    p2 = (size * 0.43, size * 0.72)
    p3 = (size * 0.80, size * 0.30)

    stroke = max(3, size // 9)

    # Draw two line segments with round caps via wide line + filled circles at joins.
    d.line([p1, p2], fill=MARK, width=stroke)
    d.line([p2, p3], fill=MARK, width=stroke)

    r = stroke / 2
    for x, y in (p1, p2, p3):
        d.ellipse([(x - r, y - r), (x + r, y + r)], fill=MARK)


def render(size: int) -> Image.Image:
    img = _rounded_square(size)
    _draw_check(img)
    return img


def main() -> None:
    sizes = [16, 24, 32, 48, 64, 128, 256]
    images = [render(s) for s in sizes]
    largest = images[-1]
    largest.save(
        OUT,
        format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=images[:-1],
    )
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    main()
