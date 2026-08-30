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
MATTE_TOLERANCE = 120
MATTE_REACH = 6
CUTOUT_TOLERANCE = 45
CUTOUT_OPENING_RADIUS = 8
CUTOUT_GROW = 2

MARGIN = 0.012

SHARPEN_BELOW = 512
SHARPEN_PERCENT = 120
SHARPEN_THRESHOLD = 2

SATURATION_GAIN = 1.25

def touching_the_border(similar):
    labels, _ = ndimage.label(similar)
    edges = np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]])
    outer = np.unique(edges)

    return np.isin(labels, outer[outer > 0])


def lift_background(rgba, reference):
    colour = rgba[:, :, :3].astype(np.float64)
    background = reference.astype(np.float64)

    pure = touching_the_border(
        np.all(np.abs(colour - background) <= BACKGROUND_TOLERANCE, axis=2)
    )

    near = ndimage.distance_transform_edt(~pure) <= MATTE_REACH
    tinted = np.all(np.abs(colour - background) <= MATTE_TOLERANCE, axis=2)
    band = near & tinted & ~pure

    if band.any():
        rows, columns = ndimage.distance_transform_edt(
            band | pure, return_distances=False, return_indices=True
        )
        artwork = colour[rows, columns]

        spread = artwork - background
        cast = ((colour - background) * spread).sum(2)
        length = np.maximum((spread * spread).sum(2), 1.0)
        alpha = np.clip(cast / length, 0.0, 1.0)

        rgba[band, :3] = artwork[band]
        rgba[band, 3] = np.rint(alpha[band] * 255.0)

    rgba[pure, 3] = 0
    carry_colour_outwards(rgba)

    return int(pure.sum()), int(band.sum())


def carry_colour_outwards(rgba):
    solid = rgba[:, :, 3] > 0

    if not solid.any() or solid.all():
        return

    rows, columns = ndimage.distance_transform_edt(
        ~solid, return_distances=False, return_indices=True
    )

    rgba[~solid, :3] = rgba[:, :, :3][rows, columns][~solid]


def find_cutout(rgba):
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

    return region

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
    rgba = np.array(image.convert("RGBA"), dtype=np.float64)

    corners = [rgba[0, 0, :3], rgba[0, -1, :3], rgba[-1, 0, :3], rgba[-1, -1, :3]]
    cleared, feathered = lift_background(rgba, np.mean(corners, axis=0))
    print(f"background {cleared:>10,} px cleared, {feathered:,} px feathered")

    region = find_cutout(rgba)
    print(f"cutout     {cut_hole(rgba, region):>10,} px cleared")

    return Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), "RGBA")

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


def saturate(rgb, gain):
    grey = rgb @ np.array([0.2126, 0.7152, 0.0722])
    return np.clip(grey[:, :, None] + (rgb - grey[:, :, None]) * gain, 0.0, 1.0)


def downscale(artwork: Image.Image, size: int) -> Image.Image:
    source = np.asarray(artwork.convert("RGBA"), dtype=np.float64) / 255.0
    factor = artwork.width / size

    linear = to_linear(source[:, :, :3])
    alpha = source[:, :, 3]

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
    carried = np.minimum(np.maximum(shrunk[:, :, :3], 0.0), safe)
    small_linear = np.clip(carried / safe, 0.0, 1.0)

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
