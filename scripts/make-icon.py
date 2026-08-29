from collections import deque
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "assets" / "nulltrap-icon-source.png"
ICO_OUT = ROOT / "src" / "Nulltrap.App" / "Nulltrap.ico"
PNG_OUT = ROOT / "assets" / "nulltrap-icon.png"
APP_PNG_OUT = ROOT / "src" / "Nulltrap.App" / "Nulltrap.png"
CAPTION_PNG_OUT = ROOT / "src" / "Nulltrap.App" / "NulltrapSmall.png"
APP_PNG_SIZE = 144
CAPTION_PNG_SIZE = 32

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

BACKGROUND_TOLERANCE = 18
CUTOUT_TOLERANCE = 45
CUTOUT_OPENING_RADIUS = 8
CUTOUT_SIMPLIFY = 0.01
CUTOUT_GROW = 2

HIGHLIGHT_THRESHOLD = 130
HIGHLIGHT_BAND = 0
HIGHLIGHT_REACH = 24
HIGHLIGHT_EDGE_INSET = 0.05

MARGIN = 0.012

SHARPEN_BELOW = 512
SHARPEN_PERCENT = 120
SHARPEN_THRESHOLD = 2

GLOW_THRESHOLD = 0.06
GLOW_SPREAD = 0.85
GLOW_CHROMA_GAIN = 1.7
GLOW_LUMA_PART = 0.45
SATURATION_GAIN = 1.25

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

def find_cutout_quad(rgba):
    height, width = rgba.shape[:2]
    centre = (height // 2, width // 2)

    similar = np.all(
        np.abs(rgba[:, :, :3] - rgba[centre[0], centre[1], :3]) <= CUTOUT_TOLERANCE,
        axis=2,
    )

    element = np.ones((2 * CUTOUT_OPENING_RADIUS + 1,) * 2, dtype=bool)
    eroded = ndimage.binary_erosion(similar, structure=element)

    labels, _ = ndimage.label(eroded)
    label = labels[centre]
    if label == 0:
        raise SystemExit(
            "No cutout found at the centre of the artwork. Check CUTOUT_TOLERANCE "
            "and CUTOUT_OPENING_RADIUS."
        )

    region = ndimage.binary_dilation(labels == label, structure=element) & similar

    contours, _ = cv2.findContours(
        (region * 255).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
    )
    contour = max(contours, key=cv2.contourArea)
    quad = cv2.approxPolyDP(
        contour, CUTOUT_SIMPLIFY * cv2.arcLength(contour, True), True
    ).reshape(-1, 2)

    if len(quad) != 4:
        raise SystemExit(
            f"The cutout simplified to {len(quad)} corners, not 4. Adjust CUTOUT_SIMPLIFY."
        )

    return quad.astype(float), region

def outward_edges(quad):
    centre = quad.mean(0)
    for index in range(len(quad)):
        start = quad[index]
        end = quad[(index + 1) % len(quad)]
        direction = end - start
        normal = np.array([direction[1], -direction[0]]) / float(np.hypot(*direction))
        if np.dot((start + end) / 2 + normal - centre, normal) < 0:
            normal = -normal
        yield start, direction, normal

def remove_edge_highlight(rgba, quad):
    height, width = rgba.shape[:2]
    colour = rgba[:, :, :3].astype(np.uint8)
    gray = cv2.cvtColor(colour, cv2.COLOR_RGB2GRAY)
    replaced = 0

    for start, direction, normal in outward_edges(quad):
        near = start + direction * HIGHLIGHT_EDGE_INSET
        far = start + direction * (1 - HIGHLIGHT_EDGE_INSET)
        strip = np.array([
            near,
            far,
            far + normal * HIGHLIGHT_BAND,
            near + normal * HIGHLIGHT_BAND,
        ])

        stencil = np.zeros((height, width), np.uint8)
        cv2.fillPoly(stencil, [strip.astype(np.int32)], 255)

        selection = (stencil > 0) & (gray > HIGHLIGHT_THRESHOLD)
        shift_y = int(round(normal[1] * HIGHLIGHT_REACH))
        shift_x = int(round(normal[0] * HIGHLIGHT_REACH))
        sampled = np.roll(np.roll(colour, -shift_y, axis=0), -shift_x, axis=1)

        rgba[selection, :3] = sampled[selection]
        replaced += int(selection.sum())

    hole = np.zeros((height, width), np.uint8)
    cv2.fillPoly(hole, [quad.astype(np.int32)], 255)
    band = cv2.dilate(hole, np.ones((2 * HIGHLIGHT_BAND * 2 + 1,) * 2, np.uint8)) & ~cv2.dilate(
        hole, np.ones((3, 3), np.uint8)
    )

    current = cv2.cvtColor(rgba[:, :, :3].astype(np.uint8), cv2.COLOR_RGB2GRAY)
    leftover = ((band > 0) & (current > HIGHLIGHT_THRESHOLD)).astype(np.uint8) * 255

    if leftover.any():
        patched = cv2.inpaint(
            rgba[:, :, :3].astype(np.uint8),
            cv2.dilate(leftover, np.ones((3, 3), np.uint8)),
            3,
            cv2.INPAINT_NS,
        )
        rgba[:, :, :3] = patched
        replaced += int((leftover > 0).sum())

    return replaced

def cut_hole(rgba, region):
    hole = cv2.dilate(
        (region * 255).astype(np.uint8),
        np.ones((2 * CUTOUT_GROW + 1,) * 2, np.uint8),
    )
    hole = cv2.morphologyEx(
        hole, cv2.MORPH_CLOSE, np.ones((2 * CUTOUT_GROW + 5,) * 2, np.uint8)
    )
    rgba[hole > 0, 3] = 0
    return int((hole > 0).sum())

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

    quad, region = find_cutout_quad(rgba)
    print(f"cutout     corners {[tuple(int(v) for v in p) for p in quad]}")

    if HIGHLIGHT_BAND > 0:
        print(f"highlight  {remove_edge_highlight(rgba, quad):>10,} px recoloured")
    print(f"cutout     {cut_hole(rgba, region):>10,} px cleared")

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

def to_linear(srgb):
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)


def to_srgb(linear):
    clipped = np.clip(linear, 0.0, 1.0)
    return np.where(clipped <= 0.0031308, clipped * 12.92, 1.055 * clipped ** (1 / 2.4) - 0.055)


def strength(factor):
    return float(np.clip((factor - 2.0) / 10.0, 0.0, 1.0))


def widen_glow(linear, alpha, factor):
    reach = int(round(factor * GLOW_SPREAD))
    mix = strength(factor)

    if reach < 1 or mix <= 0.0:
        return linear

    grey = linear @ np.array([0.2126, 0.7152, 0.0722])
    chroma = (linear - grey[:, :, None]) * (alpha > 0.5)[:, :, None]

    element = np.ones((2 * reach + 1, 2 * reach + 1), bool)
    spread = np.stack(
        [
            ndimage.grey_dilation(np.maximum(chroma[:, :, channel], 0.0), footprint=element)
            - ndimage.grey_dilation(np.maximum(-chroma[:, :, channel], 0.0), footprint=element)
            for channel in range(3)
        ],
        axis=2,
    ) * (1.0 + (GLOW_CHROMA_GAIN - 1.0) * mix)

    lit = grey
    glow = max(1, int(reach * GLOW_LUMA_PART))

    if glow >= 1:
        lit = ndimage.grey_dilation(grey, footprint=np.ones((2 * glow + 1,) * 2, bool))
        lit = np.where(alpha > 0.5, lit, grey)

    return np.clip(lit[:, :, None] + spread, 0.0, 1.0)


def saturate(rgb, gain):
    grey = rgb @ np.array([0.2126, 0.7152, 0.0722])
    return np.clip(grey[:, :, None] + (rgb - grey[:, :, None]) * gain, 0.0, 1.0)


def downscale(artwork: Image.Image, size: int) -> Image.Image:
    source = np.asarray(artwork.convert("RGBA"), dtype=np.float64) / 255.0
    factor = artwork.width / size

    linear = to_linear(source[:, :, :3])
    alpha = source[:, :, 3]

    linear = widen_glow(linear, alpha, factor)

    premultiplied = np.dstack([linear * alpha[:, :, None], alpha])
    shrunk = np.stack(
        [
            np.asarray(
                Image.fromarray(
                    np.ascontiguousarray(premultiplied[:, :, channel], dtype=np.float32),
                    mode="F",
                ).resize((size, size), Image.LANCZOS),
                dtype=np.float64,
            )
            for channel in range(4)
        ],
        axis=2,
    )

    small_alpha = np.clip(shrunk[:, :, 3], 0.0, 1.0)
    safe = np.maximum(small_alpha, 1e-6)[:, :, None]
    small_linear = np.clip(shrunk[:, :, :3] / safe, 0.0, 1.0)

    rgb = to_srgb(small_linear)

    mix = strength(factor)

    if mix > 0.0:
        rgb = saturate(rgb, 1.0 + (SATURATION_GAIN - 1.0) * mix)

    result = Image.fromarray(
        np.dstack([rgb * 255.0, small_alpha * 255.0]).round().astype(np.uint8), "RGBA"
    )

    if size >= SHARPEN_BELOW:
        return result

    radius = max(0.4, size / 220)
    colour, mask = result.convert("RGB"), result.getchannel("A")
    crisp = colour.filter(
        ImageFilter.UnsharpMask(radius=radius, percent=SHARPEN_PERCENT, threshold=SHARPEN_THRESHOLD)
    )
    crisp.putalpha(mask)

    return crisp


def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Source artwork not found: {SOURCE}")

    source = Image.open(SOURCE)
    print(f"source     {source.size[0]}x{source.size[1]} {source.mode}")

    artwork = crop_to_square(prepare(source))
    print(f"cropped    {artwork.size[0]}x{artwork.size[1]} RGBA")

    ICO_OUT.parent.mkdir(parents=True, exist_ok=True)
    frames = [downscale(artwork, size) for size in ICO_SIZES]
    frames[-1].save(ICO_OUT, format="ICO", sizes=[(s, s) for s in ICO_SIZES])
    print(f"wrote      {ICO_OUT.relative_to(ROOT)}  ({', '.join(map(str, ICO_SIZES))})")

    downscale(artwork, 1024).save(PNG_OUT, format="PNG")
    print(f"wrote      {PNG_OUT.relative_to(ROOT)}  (1024x1024)")

    downscale(artwork, APP_PNG_SIZE).save(APP_PNG_OUT, format="PNG", optimize=True)
    print(f"wrote      {APP_PNG_OUT.relative_to(ROOT)}  ({APP_PNG_SIZE}x{APP_PNG_SIZE})")

    downscale(artwork, CAPTION_PNG_SIZE).save(CAPTION_PNG_OUT, format="PNG", optimize=True)
    print(f"wrote      {CAPTION_PNG_OUT.relative_to(ROOT)}  ({CAPTION_PNG_SIZE}x{CAPTION_PNG_SIZE})")

if __name__ == "__main__":
    main()
