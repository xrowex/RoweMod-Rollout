"""Build 512 catalog icons with a cyan frame so mod tiles read as custom."""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
REF = ROOT / "art" / "previews" / "_ref" / "RollerSkate" / "Content" / "MainFolder" / "Character" / "upper"
OUT = ROOT / "art" / "previews"
SIZE = 512
BORDER = (0, 212, 255, 255)
BORDER_INNER = (8, 24, 36, 255)
WIDTH = 16
RADIUS = 36


def load_rgb(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def tint_navy(im: Image.Image) -> Image.Image:
    overlay = Image.new("RGBA", im.size, (18, 42, 92, 255))
    return ImageChops.multiply(im, overlay)


def crop_graphic(im: Image.Image) -> Image.Image:
    w, h = im.size
    box = (int(w * 0.02), int(h * 0.08), int(w * 0.48), int(h * 0.52))
    crop = im.crop(box)
    square = Image.new("RGBA", (max(crop.size), max(crop.size)), (18, 18, 18, 255))
    ox = (square.width - crop.width) // 2
    oy = (square.height - crop.height) // 2
    square.paste(crop, (ox, oy), crop)
    return square


def frame(im: Image.Image, path: Path) -> None:
    canvas = Image.new("RGBA", (SIZE, SIZE), (12, 14, 18, 255))
    inset = WIDTH + 8
    inner_size = SIZE - inset * 2
    fitted = ImageOps_fit(im, inner_size)
    canvas.paste(fitted, (inset, inset), fitted)
    glow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    gdraw.rounded_rectangle(
        [1, 1, SIZE - 2, SIZE - 2], radius=RADIUS + 2, outline=(0, 212, 255, 90), width=WIDTH + 8
    )
    canvas = Image.alpha_composite(canvas, glow.filter(ImageFilter.GaussianBlur(3)))
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle(
        [4, 4, SIZE - 5, SIZE - 5], radius=RADIUS, outline=BORDER_INNER, width=WIDTH + 4
    )
    draw.rounded_rectangle(
        [6, 6, SIZE - 7, SIZE - 7], radius=RADIUS - 2, outline=BORDER, width=WIDTH
    )
    OUT.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(path, quality=95)
    print("wrote", path, canvas.size)


def ImageOps_fit(im: Image.Image, size: int) -> Image.Image:
    im = im.convert("RGBA")
    src_ratio = im.width / im.height
    if src_ratio > 1:
        new_w, new_h = size, int(size / src_ratio)
    else:
        new_w, new_h = int(size * src_ratio), size
    resized = im.resize((max(1, new_w), max(1, new_h)), Image.Resampling.LANCZOS)
    out = Image.new("RGBA", (size, size), (12, 14, 18, 255))
    out.paste(resized, ((size - resized.width) // 2, (size - resized.height) // 2), resized)
    return out


def main() -> None:
    hoodie = REF / "hoodie" / "white" / "hoodie-white.png"
    albedo = ROOT / "art" / "textures" / "tshirt-baggy" / "albedo.jpg"
    if not hoodie.exists():
        raise SystemExit("missing hoodie preview " + str(hoodie))
    if not albedo.exists():
        raise SystemExit("missing albedo " + str(albedo))
    frame(tint_navy(load_rgb(hoodie)), OUT / "T_hoodie-navy-mod.png")
    frame(crop_graphic(load_rgb(albedo)), OUT / "T_tshirt-baggy-mod.png")


if __name__ == "__main__":
    main()
