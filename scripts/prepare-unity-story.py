#!/usr/bin/env python3
"""Sync the existing Homecoming story/media into the separate Unity project.

Does not generate artwork, modify the web game or rebuild an edited Unity scene.
"""
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "unity/Homecoming/Assets/Homecoming/Resources"


def main():
    source = (ROOT / "src/homecoming/story.ts").read_text()
    sequences = {}
    for name, key in [("PROLOGUE", "prologue"), ("LAB_INTRO", "briefing"), ("ENDING", "ending")]:
        body = source.split(f"export const {name}: Shot[] = [", 1)[1].split("\n];", 1)[0]
        shots = []
        for line in body.splitlines():
            if "id:" not in line:
                continue
            shot = {}
            for field in ["id", "description", "label", "line"]:
                match = re.search(r'\b' + field + r': "([^"]*)"', line)
                if match:
                    shot[field] = match.group(1)
            shot["seconds"] = float(re.search(r"seconds: ([\d.]+)", line).group(1))
            shot["cues"] = [{"at": float(at), "name": cue} for at, cue in re.findall(r'\[([\d.]+), "([^"]+)"\]', line)]
            shots.append(shot)
        if not shots:
            raise ValueError(f"No shots parsed for {name}; check the source story format.")
        sequences[key] = shots
    TARGET.mkdir(parents=True, exist_ok=True)
    (TARGET / "Story.json").write_text(json.dumps(sequences, ensure_ascii=False, indent=2) + "\n")
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
    print("Synced original story:", ", ".join(f"{len(shots)} {name} shots" for name, shots in sequences.items()))


if __name__ == "__main__":
    main()
