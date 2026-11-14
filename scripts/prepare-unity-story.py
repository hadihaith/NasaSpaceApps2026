#!/usr/bin/env python3
"""Sync Homecoming media into the separate Unity project.

Does not generate artwork, modify the web game or rebuild an edited Unity scene.
"""
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "unity/Homecoming/Assets/Homecoming/Resources"


def main():
    crew = TARGET / "Crew"
    crew.mkdir(exist_ok=True)
    for asset in (ROOT / "public/assets/characters").glob("*.glb"):
        shutil.copy2(asset, crew / asset.name)
    film = TARGET / "Film"
    film.mkdir(exist_ok=True)
    for name in ["apollo11.mp4", "apollo11.jpg", "moon-albedo.jpg", "moon-height.jpg"]:
        shutil.copy2(ROOT / "public/homecoming" / name, film / name)
    for name, destination in [("ku-day.webp", "earth.png"), ("ku-night.webp", "earth-night.png")]:
        original = ROOT / "public/homecoming/planets" / name
        if shutil.which("sips"):
            subprocess.run(["sips", "-s", "format", "png", str(original), "--out", str(film / destination)], check=True, capture_output=True)
        else:
            try:
                from PIL import Image
            except ImportError as error:
                raise RuntimeError("Install Pillow to convert existing WebP textures outside macOS.") from error
            with Image.open(original) as image:
                image.save(film / destination)
    print("Synced Homecoming crew models and planet textures.")


if __name__ == "__main__":
    main()
