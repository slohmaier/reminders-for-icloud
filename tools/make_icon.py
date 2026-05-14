"""Generate icon.ico (and a PNG preview) for Reminders for iCloud.

Design inspired by Microsoft Office / Outlook / Edge: a free-form "document
page" silhouette with a dog-eared corner, vivid two-stop gradient, and a
short list of checked items suggesting a to-do list. No background plate —
the colored shape itself IS the icon.

Color: warm orange -> deep red-orange. Chosen distinctly from
notes-for-icloud (teal/blue) so the two apps are trivially separable at any
size.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT_ICO = ROOT / "icon.ico"
OUT_PNG = ROOT / "icon-preview.png"

PAGE_TOP = (255, 138, 36, 255)        # #FF8A24
PAGE_BOTTOM = (210, 50, 30, 255)      # #D2321E
FOLD_BACK = (255, 210, 175, 255)
HIGHLIGHT = (255, 255, 255, 38)
INK = (255, 255, 255, 240)


def _vgradient(width: int, height: int, top: tuple[int, int, int, int],
               bottom: tuple[int, int, int, int]) -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(height):
        t = y / max(1, height - 1)
        c = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(4))
        d.line([(0, y), (width, y)], fill=c)
    return img


def _page_polygon(size: int):
    margin_x = size * 0.16
    margin_y = size * 0.10
    fold = size * 0.24

    x0, y0 = margin_x, margin_y
    x1, y1 = size - margin_x, size - margin_y

    page = [
        (x0, y0),
        (x1 - fold, y0),
        (x1, y0 + fold),
        (x1, y1),
        (x0, y1),
    ]
    fold_tri = [
        (x1 - fold, y0),
        (x1 - fold, y0 + fold),
        (x1, y0 + fold),
    ]
    return page, fold_tri, (x0, y0, x1, y1, fold)


def _draw_check(d: ImageDraw.ImageDraw, cx: float, cy: float, box: float, stroke: float) -> None:
    p1 = (cx - box * 0.30, cy + box * 0.02)
    p2 = (cx - box * 0.05, cy + box * 0.25)
    p3 = (cx + box * 0.32, cy - box * 0.22)
    d.line([p1, p2], fill=INK, width=int(max(1, stroke)))
    d.line([p2, p3], fill=INK, width=int(max(1, stroke)))
    r = max(0.5, stroke / 2)
    for x, y in (p1, p2, p3):
        d.ellipse([(x - r, y - r), (x + r, y + r)], fill=INK)


def _draw_items(img: Image.Image, geom) -> None:
    size = img.size[0]
    x0, y0, x1, y1, fold = geom
    if size < 24:
        return

    d = ImageDraw.Draw(img)

    inner_x0 = x0 + size * 0.10
    inner_x1 = x1 - size * 0.10
    content_y0 = y0 + fold + size * 0.06
    content_y1 = y1 - size * 0.10

    n = 3
    line_h = max(2, int(round(size / 30)))
    box = max(4, int(round(size * 0.08)))
    avail = content_y1 - content_y0
    gap = (avail - n * box) / max(1, n - 1)

    line_widths = [0.85, 0.95, 0.70]

    for i, w in enumerate(line_widths):
        row_y = content_y0 + i * (box + gap)
        # checkbox stroke around left of row
        bx0 = inner_x0
        by0 = row_y
        bx1 = bx0 + box
        by1 = by0 + box
        stroke = max(1, size / 96)
        # outer rounded checkbox outline
        d.rounded_rectangle([(bx0, by0), (bx1, by1)], radius=box * 0.22,
                            outline=INK, width=int(max(1, stroke)))
        # checkmark inside (only when size big enough to look right)
        if size >= 32:
            _draw_check(d, (bx0 + bx1) / 2, (by0 + by1) / 2, box, max(1, size / 64))

        # horizontal "task text" line to the right of the checkbox
        text_x0 = bx1 + size * 0.05
        text_y_center = (by0 + by1) / 2
        text_y0 = text_y_center - line_h / 2
        text_y1 = text_y_center + line_h / 2
        text_x1 = inner_x0 + (inner_x1 - inner_x0) * w
        if text_x1 < text_x0 + line_h:
            text_x1 = text_x0 + line_h * 2
        d.rounded_rectangle([(text_x0, text_y0), (text_x1, text_y1)],
                            radius=line_h / 2, fill=INK)


def render(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    page, fold_tri, geom = _page_polygon(size)

    grad = _vgradient(size, size, PAGE_TOP, PAGE_BOTTOM)
    page_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(page_mask).polygon(page, fill=255)
    img.paste(grad, (0, 0), page_mask)

    ImageDraw.Draw(img).polygon(fold_tri, fill=FOLD_BACK)

    if size >= 48:
        sheen = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        ImageDraw.Draw(sheen).polygon(page, fill=HIGHLIGHT)
        smask = Image.new("L", (size, size), 0)
        ImageDraw.Draw(smask).rectangle([(0, 0), (size, int(size * 0.45))], fill=255)
        img = Image.alpha_composite(img, Image.composite(
            sheen, Image.new("RGBA", (size, size), (0, 0, 0, 0)), smask))

    _draw_items(img, geom)
    return img


def main() -> None:
    sizes = [16, 24, 32, 48, 64, 128, 256]
    images = [render(s) for s in sizes]
    images[-1].save(
        OUT_ICO,
        format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=images[:-1],
    )
    images[-1].save(OUT_PNG, format="PNG")
    print(f"Wrote {OUT_ICO}")
    print(f"Wrote {OUT_PNG}")


if __name__ == "__main__":
    main()
