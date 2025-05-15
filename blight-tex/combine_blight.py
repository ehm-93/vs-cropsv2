#!/usr/bin/env python3
"""
combine_blight.py

Usage:
    python combine_blight.py /path/to/texture/folder
Requires:
    pip install pillow
"""

import sys
from pathlib import Path
from PIL import Image

OVERLAYS = [f"blight-{i}.png" for i in range(1, 6)]  # overlay filenames
OVERLAY_OPACITY = 0.75  # Blend ratio (0 = invisible, 1 = full strength)

def tile_overlay(overlay: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Tile an overlay image to cover a base of given size."""
    tiled = Image.new("RGBA", size)
    ow, oh = overlay.size
    for y in range(0, size[1], oh):
        for x in range(0, size[0], ow):
            tiled.paste(overlay, (x, y))
    return tiled

def set_overlay_opacity(overlay: Image.Image, opacity: float) -> Image.Image:
    """Return a new overlay with adjusted alpha."""
    if overlay.mode != "RGBA":
        overlay = overlay.convert("RGBA")

    r, g, b, a = overlay.split()
    a = a.point(lambda p: int(p * opacity))
    return Image.merge("RGBA", (r, g, b, a))

def blend_preserve_alpha(base: Image.Image, overlay: Image.Image) -> Image.Image:
    """Overlay image with transparency, preserving original alpha mask."""
    blended = Image.alpha_composite(base, overlay)
    # Preserve original alpha mask
    r, g, b, _ = blended.split()
    _, _, _, alpha = base.split()
    return Image.merge("RGBA", (r, g, b, alpha))

def main(folder: Path) -> None:
    if not folder.is_dir():
        sys.exit(f"{folder} is not a directory")

    out_dir = Path("out")
    out_dir.mkdir(exist_ok=True)

    overlays = {
        name: Image.open(name).convert("RGBA") for name in OVERLAYS
    }

    for img_path in folder.glob("*"):
        if img_path.name.lower().endswith((".png", ".jpg", ".jpeg")) and img_path.name not in OVERLAYS:
            base = Image.open(img_path).convert("RGBA")

            for tier, ov_name in enumerate(OVERLAYS, start=1):
                overlay = overlays[ov_name]

                # Tile overlay if needed
                if overlay.size != base.size:
                    overlay = tile_overlay(overlay, base.size)

                overlay = set_overlay_opacity(overlay, OVERLAY_OPACITY)
                combined = blend_preserve_alpha(base, overlay)

                out_name = f"{img_path.stem}-blight{tier}{img_path.suffix}"
                combined.save(out_dir / out_name)

            print(f"✓ {img_path.name} → 5 blighted textures")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("Usage: combine_blight.py <folder>")
    main(Path(sys.argv[1]))
