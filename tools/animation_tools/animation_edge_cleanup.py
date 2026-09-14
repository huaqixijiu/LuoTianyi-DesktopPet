"""Opt-in cleanup of audited one-bit sprite contours before resizing.

Never flood-fill by colour: white lettering, clothes and sticker strokes remain
foreground. Only the inner one-pixel transparent boundary can change. Geometry,
nonzero-alpha support and all interior RGBA values remain exactly unchanged.
"""

import numpy as np
from PIL import Image, ImageFilter


def clean_edges(frame: Image.Image, profile: str) -> Image.Image:
    if profile not in (
        "coverage",
        "coverage-white-matte",
        "dark-contour-white-matte",
        "dark-contour-white-matte-wide",
    ):
        raise ValueError(f"Unknown edge cleanup profile: {profile}")
    source = np.array(frame.convert("RGBA"))
    alpha = source[:, :, 3]
    # Existing soft RGBA assets must not be repeatedly feathered.
    if np.any((alpha > 0) & (alpha < 255)):
        raise ValueError("Edge cleanup requires an audited one-bit alpha source")
    radius = 2 if profile == "dark-contour-white-matte-wide" else 1
    size = radius * 2 + 1
    padded = np.pad(alpha == 0, radius, mode="edge")
    boundary = np.zeros(alpha.shape, dtype=bool)
    for dy in range(size):
        for dx in range(size):
            boundary |= padded[dy:dy+alpha.shape[0], dx:dx+alpha.shape[1]]
    boundary &= alpha > 0
    result = source.copy()
    # Fully transparent RGB is sampled by some WPF scaling paths. Clear it so
    # hidden source matte colors cannot bleed back into the visible contour.
    result[alpha == 0, :3] = 0
    # Subpixel coverage, clipped to the existing silhouette. No new haze outside
    # the sprite and no missing hair/letter islands, even a one-pixel mark.
    coverage = np.asarray(Image.fromarray(alpha).filter(ImageFilter.GaussianBlur(0.45)))
    result[boundary, 3] = np.maximum(1, coverage[boundary])
    if profile != "coverage":
        height, width = alpha.shape
        for y, x in zip(*np.nonzero(boundary)):
            color = source[y, x, :3].astype(float)
            # True white/cream strokes are artwork, not background. Only
            # neutral grey next to a solid dark contour is eligible to unmatte.
            full_matte = profile in ("dark-contour-white-matte", "dark-contour-white-matte-wide")
            if not 96 <= min(color) <= (255 if full_matte else 234) or max(color)-min(color) > 28:
                continue
            nearby = source[
                max(0,y-radius):min(height,y+radius+1),
                max(0,x-radius):min(width,x+radius+1),
            ]
            opaque = nearby[:, :, 3] == 255
            if not full_matte and np.any(opaque & (nearby[:, :, :3].min(axis=2) >= 235)):
                continue
            inks = nearby[:, :, :3][opaque & (nearby[:, :, :3].max(axis=2) <= 96)]
            fits = []
            for ink in inks.astype(float):
                distance = 255-ink
                a = float(np.dot(255-color, distance)/np.dot(distance, distance))
                if 0 <= a < .95 and max(abs(color-(255-a*distance))) <= 8:
                    fits.append((min(ink), a))
            if fits:
                _, a = min(fits)
                opacity = max(1, round(a*255), 255-int(min(color)))
                result[y, x, :3] = np.clip(np.rint(255+(color-255)*255/opacity), 0, 255)
                # Use recovered coverage directly; do not feather twice.
                result[y, x, 3] = opacity
    return Image.fromarray(result)
