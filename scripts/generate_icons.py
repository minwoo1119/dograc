#!/usr/bin/env python3
"""Generate high-quality PNG and ICO icon assets for dograc launcher."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT_DIR = Path(__file__).resolve().parent.parent
ASSETS_DIR = ROOT_DIR / "launcher" / "assets"
ASSETS_DIR.mkdir(parents=True, exist_ok=True)


def draw_dograc_logo(size: int = 256) -> Image.Image:
    # 32x32 design coordinate base scaled to requested size
    scale = size / 32.0
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 1. KDS Indigo Rounded Base Container (#5055B1)
    bg_color = (80, 85, 177, 255)
    radius = int(7.0 * scale)
    draw.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=bg_color)

    # 2. Crisp Document Base Sheet (White)
    doc_color = (255, 255, 255, 255)
    doc_left = int(9.0 * scale)
    doc_top = int(6.0 * scale)
    doc_right = int(23.0 * scale)
    doc_bottom = int(26.0 * scale)
    doc_corner = int(1.5 * scale)
    draw.rounded_rectangle([doc_left, doc_top, doc_right, doc_bottom], radius=doc_corner, fill=doc_color)

    # 3. Document Fold Corner (Top Right)
    fold_color = (211, 214, 220, 255)
    fold_w = max(1, int(1.5 * scale))
    draw.line([(int(18.0 * scale), int(6.0 * scale)), (int(18.0 * scale), int(11.0 * scale))], fill=fold_color, width=fold_w)
    draw.line([(int(18.0 * scale), int(11.0 * scale)), (int(23.0 * scale), int(11.0 * scale))], fill=fold_color, width=fold_w)

    # 4. Text Content Lines (#5055B1, #8B919D)
    line1_color = (80, 85, 177, 255)
    line2_color = (139, 145, 157, 255)
    line_w = max(1, int(1.5 * scale))
    draw.line([(int(12.0 * scale), int(14.0 * scale)), (int(20.0 * scale), int(14.0 * scale))], fill=line1_color, width=line_w)
    draw.line([(int(12.0 * scale), int(17.5 * scale)), (int(20.0 * scale), int(17.5 * scale))], fill=line1_color, width=line_w)
    draw.line([(int(12.0 * scale), int(21.0 * scale)), (int(16.5 * scale), int(21.0 * scale))], fill=line2_color, width=line_w)

    # 5. Modern Accent Dot (#4DAC27)
    dot_color = (77, 172, 39, 255)
    dot_cx = int(22.5 * scale)
    dot_cy = int(22.5 * scale)
    dot_r = int(3.5 * scale)
    draw.ellipse([dot_cx - dot_r, dot_cy - dot_r, dot_cx + dot_r, dot_cy + dot_r], fill=dot_color, outline=(255, 255, 255, 255), width=max(1, int(1.5 * scale)))

    return img


def generate_all_icons():
    # 256x256 Master PNG
    master = draw_dograc_logo(256)
    master.save(ASSETS_DIR / "icon.png", format="PNG")
    print(f"Saved: {ASSETS_DIR / 'icon.png'}")

    # Standard icon sizes
    sizes = [16, 32, 48, 64, 128, 256]
    images = [master.resize((s, s), Image.Resampling.LANCZOS) for s in sizes]

    # Save Windows multi-resolution .ico
    master.save(
        ASSETS_DIR / "icon.ico",
        format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=images,
    )
    print(f"Saved: {ASSETS_DIR / 'icon.ico'}")

    # UI Header PNG (40x40)
    logo_40 = master.resize((40, 40), Image.Resampling.LANCZOS)
    logo_40.save(ASSETS_DIR / "logo_40.png", format="PNG")
    print(f"Saved: {ASSETS_DIR / 'logo_40.png'}")


if __name__ == "__main__":
    generate_all_icons()
