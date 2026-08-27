from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "assets" / "nulltrap-icon-source.png"
ICO_OUT = ROOT / "src" / "Nulltrap.App" / "Nulltrap.ico"
PNG_OUT = ROOT / "assets" / "nulltrap-icon.png"

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

BACKGROUND_TOLERANCE = 18
CUTOUT_TOLERANCE = 45
CUTOUT_OPENING_RADIUS = 8
CUTOUT_EDGE_BLEED = 10
MARGIN = 0.04

def border_seeds(height, width):
    for x in range(width):
        yield 0, x
        yield height - 1, x
    for y in range(height):
        yield y, 0
        yield y, width - 1

def flood_clear(rgba, seeds, reference, tolerance):
    height, width = rgba.shape[:2]
    similar = np.all(np.abs(rgba[:, :, :3] - reference) <= tolerance, axis=2)

    visited = np.zeros((height, width), dtype=bool)
    queue = deque()

    for y, x in seeds:
        if similar[y, x] and not visited[y, x]:
            visited[y, x] = True
            queue.append((y, x))

    while queue:
        y, x = queue.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < height and 0 <= nx < width:
                if similar[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((ny, nx))

    rgba[visited, 3] = 0
    return int(visited.sum())

def clear_centre_cutout(rgba, tolerance, radius, bleed):
    height, width = rgba.shape[:2]
    centre = (height // 2, width // 2)

    similar = np.all(
        np.abs(rgba[:, :, :3] - rgba[centre[0], centre[1], :3]) <= tolerance,
        axis=2,
    )

    element = np.ones((2 * radius + 1, 2 * radius + 1), dtype=bool)
    eroded = ndimage.binary_erosion(similar, structure=element)

    labels, _ = ndimage.label(eroded)
    label = labels[centre]
    if label == 0:
        raise SystemExit(
            "No cutout found at the centre of the artwork. Check CUTOUT_TOLERANCE "
            "and CUTOUT_OPENING_RADIUS."
        )

    core = labels == label
    region = ndimage.binary_dilation(core, structure=element) & similar

    if bleed > 0:
        halo = np.ones((2 * bleed + 1, 2 * bleed + 1), dtype=bool)
        region = ndimage.binary_dilation(region, structure=halo)

    rgba[region, 3] = 0
    return int(region.sum())

def prepare(image: Image.Image) -> Image.Image:
    rgba = np.array(image.convert("RGBA"), dtype=np.int16)
    height, width = rgba.shape[:2]

    corners = [rgba[0, 0, :3], rgba[0, -1, :3], rgba[-1, 0, :3], rgba[-1, -1, :3]]
    cleared = flood_clear(
        rgba,
        border_seeds(height, width),
        np.mean(corners, axis=0),
        BACKGROUND_TOLERANCE,
    )
    print(f"background {cleared:>10,} px cleared")

    cleared = clear_centre_cutout(
        rgba, CUTOUT_TOLERANCE, CUTOUT_OPENING_RADIUS, CUTOUT_EDGE_BLEED
    )
    print(f"cutout     {cleared:>10,} px cleared")

    return Image.fromarray(rgba.astype(np.uint8), "RGBA")

def crop_to_square(image: Image.Image) -> Image.Image:
    bbox = image.getbbox()
    if bbox is None:
        raise SystemExit("The source image is fully transparent - nothing to crop.")

    cropped = image.crop(bbox)
    edge = max(cropped.size)
    side = edge + int(edge * MARGIN * 2)

    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(
        cropped,
        ((side - cropped.width) // 2, (side - cropped.height) // 2),
    )
    return canvas

def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Source artwork not found: {SOURCE}")

    source = Image.open(SOURCE)
    print(f"source     {source.size[0]}x{source.size[1]} {source.mode}")

    artwork = crop_to_square(prepare(source))
    print(f"cropped    {artwork.size[0]}x{artwork.size[1]} RGBA")

    ICO_OUT.parent.mkdir(parents=True, exist_ok=True)
    frames = [
        artwork.resize((size, size), Image.LANCZOS) for size in ICO_SIZES
    ]
    frames[-1].save(ICO_OUT, format="ICO", sizes=[(s, s) for s in ICO_SIZES])
    print(f"wrote      {ICO_OUT.relative_to(ROOT)}  ({', '.join(map(str, ICO_SIZES))})")

    artwork.resize((1024, 1024), Image.LANCZOS).save(PNG_OUT, format="PNG")
    print(f"wrote      {PNG_OUT.relative_to(ROOT)}  (1024x1024)")

if __name__ == "__main__":
    main()
