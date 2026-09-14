"""Scan every runtime frame and render representative edge inspection sheets."""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageSequence


def runtime_frames(root, entry):
    path = root / "assets" / entry["atlasPath"]
    with Image.open(path) as image:
        if path.suffix == ".webp":
            for frame in ImageSequence.Iterator(image):
                yield frame.convert("RGBA")
        else:
            for index in range(len(entry["frameDurationsMilliseconds"])):
                x = index % entry["columns"] * entry["frameWidth"]
                y = index // entry["columns"] * entry["frameHeight"]
                yield image.crop((x, y, x + entry["frameWidth"], y + entry["frameHeight"])).convert("RGBA")


def dilate(mask, size):
    radius = size // 2
    height, width = mask.shape
    padded = np.pad(mask, ((0, 0), (radius, radius)))
    horizontal = np.zeros_like(mask)
    for offset in range(size):
        horizontal |= padded[:, offset:offset+width]
    padded = np.pad(horizontal, ((radius, radius), (0, 0)))
    result = np.zeros_like(mask)
    for offset in range(size):
        result |= padded[offset:offset+height, :]
    return result


def edge_stats(frame):
    data = np.asarray(frame)
    alpha = data[:, :, 3]
    rgb = data[:, :, :3]
    low = rgb.min(axis=2)
    high = rgb.max(axis=2)
    chroma = high - low
    edge = (alpha > 0) & dilate(alpha == 0, 3)
    dark_nearby = dilate((alpha >= 240) & (high <= 96), 7)
    bright_nearby = dilate((alpha >= 240) & (low >= 150), 7)
    opaque_bright = edge & (alpha >= 240) & (low >= 150) & (chroma <= 28) & dark_nearby
    opaque_dark = edge & (alpha >= 240) & (high <= 96) & (chroma <= 28) & bright_nearby
    partial = (alpha > 0) & (alpha < 240)
    partial_bright = edge & partial & (low >= 150) & (chroma <= 28) & dark_nearby
    partial_dark = edge & partial & (high <= 96) & (chroma <= 28) & bright_nearby
    hidden_rgb = (alpha == 0) & (rgb.max(axis=2) > 0)
    return {
        "opaqueBrightNearDark": int(opaque_bright.sum()),
        "opaqueDarkNearBright": int(opaque_dark.sum()),
        "partialBrightNearDark": int(partial_bright.sum()),
        "partialDarkNearBright": int(partial_dark.sum()),
        "hiddenRgbPixels": int(hidden_rgb.sum()),
        "edgePixels": int(edge.sum()),
        "partialAlphaPixels": int(((alpha > 0) & (alpha < 255)).sum()),
    }


def render_card(frame, label):
    card = Image.new("RGB", (380, 280), "#303844")
    draw = ImageDraw.Draw(card)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 13)
    draw.text((7, 5), label[:48], font=font, fill="white")
    for column, color in enumerate(["#102033", "#e8ebef"]):
        panel = Image.new("RGBA", (184, 245), color)
        size = (min(178, round(frame.width * 225 / frame.height)), min(225, round(frame.height * 178 / frame.width)))
        scaled = frame.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
        panel.alpha_composite(scaled, ((184-size[0])//2, (245-size[1])//2))
        card.paste(panel.convert("RGB"), (4 + 190*column, 30))
    return card


def audit(root, output):
    output.mkdir(parents=True, exist_ok=True)
    catalog = json.loads((root / "assets/manifests/animations.json").read_text(encoding="utf-8"))["animations"]
    report = []
    bright_cards = []
    dark_cards = []
    for entry in catalog:
        totals = {
            "opaqueBrightNearDark": 0,
            "opaqueDarkNearBright": 0,
            "partialBrightNearDark": 0,
            "partialDarkNearBright": 0,
            "hiddenRgbPixels": 0,
            "edgePixels": 0,
            "partialAlphaPixels": 0,
        }
        best_bright = None
        best_dark = None
        maximum_bright = -1
        maximum_dark = -1
        worst_bright_index = 0
        worst_dark_index = 0
        count = 0
        for index, frame in enumerate(runtime_frames(root, entry)):
            stats = edge_stats(frame)
            count += 1
            for key in totals:
                totals[key] += stats[key]
            if stats["opaqueBrightNearDark"] > maximum_bright:
                best_bright = frame.copy()
                maximum_bright = stats["opaqueBrightNearDark"]
                worst_bright_index = index
            if stats["opaqueDarkNearBright"] > maximum_dark:
                best_dark = frame.copy()
                maximum_dark = stats["opaqueDarkNearBright"]
                worst_dark_index = index
        # WebP may losslessly merge identical adjacent logical frames.
        record = {"id": entry["id"], "frameCount": count,
                  "declaredFrameCount": len(entry["frameDurationsMilliseconds"]),
                  # Keep the original field as an alias for the bright-edge frame.
                  "worstFrame": worst_bright_index,
                  "worstBrightFrame": worst_bright_index,
                  "worstDarkFrame": worst_dark_index,
                  "maxOpaqueBrightNearDark": maximum_bright,
                  "maxOpaqueDarkNearBright": maximum_dark,
                  **totals}
        report.append(record)
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        best_bright.save(output / (entry["id"] + ".png"))
        best_dark.save(output / (entry["id"] + "-dark.png"))
        bright_cards.append(render_card(best_bright, f"{entry['id']} bright {maximum_bright}"))
        dark_cards.append(render_card(best_dark, f"{entry['id']} dark {maximum_dark}"))
        print(entry["id"], count, maximum_bright, maximum_dark, flush=True)
    for offset in range(0, len(bright_cards), 9):
        page = Image.new("RGB", (1140, 840), "#303844")
        for index, card in enumerate(bright_cards[offset:offset+9]):
            page.paste(card, (index % 3 * 380, index // 3 * 280))
        page.save(output / f"page-bright-{offset//9+1:02}.png")
    for offset in range(0, len(dark_cards), 9):
        page = Image.new("RGB", (1140, 840), "#303844")
        for index, card in enumerate(dark_cards[offset:offset+9]):
            page.paste(card, (index % 3 * 380, index // 3 * 280))
        page.save(output / f"page-dark-{offset//9+1:02}.png")
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    audit(Path.cwd(), args.output)
