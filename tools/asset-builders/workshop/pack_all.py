"""Package the latest finished Workshop PNGs without rebuilding their sources."""
from pathlib import Path
import json
import zipfile

from PIL import Image

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop'


def main():
    files = []
    sets = [("autoarm-guide", ROOT / "panels"),
            ("toolswap-guide", ROOT / "tool-swap/panels"),
            ("commands/panels", ROOT / "commands/panels")]
    for folder, path in sets:
        for p in sorted(path.glob("*.png")):
            image = Image.open(p)
            assert image.mode == "RGBA" and image.getchannel("A").getextrema() == (0, 255)
            assert all(image.getpixel(c)[3] == 0 for c in [(0, 0), (image.width-1, 0),
                                                        (0, image.height-1), (image.width-1, image.height-1)])
            files.append((p, folder + "/" + p.name))
    transparent_count = len(files)
    for p in sorted((ROOT / "tool-swap/branding").glob("*.png")):
        files.append((p, "branding/" + p.name))
    png_count = len(files)
    for path, destination in [
        ("commands/commands.txt", "commands/commands.txt"),
        ("commands/catalog.json", "commands/catalog.json"),
        ("commands/manifest.json", "commands/manifest.json"),
        ("commands/preview.html", "commands/preview.html"),
        ("tool-swap/autoarm-pairing.ini", "autoarm-pairing.ini"),
        ("tool-swap/toolswap-pairing.ini", "toolswap-pairing.ini"),
        ("tool-swap/correction-prompts.json", "correction-prompts.json"),
    ]:
        files.append((ROOT / path if (ROOT / path).exists() else REPO / "docs/steam-workshop" / path, destination))
    readme = '''AutoArm Workshop assets

34 PNGs: 9 AutoArm guide panels, 8 ToolSwap setup panels, 14 command-reference
panels and 3 branding images. Guide/reference canvases are transparent. Branding
retains the supplied illustration's pale background.

ToolSwap guide images 1 and 3 use this stand layout:
working head | tool housing | rotor base
air          | tool merge   | air
air          | stand merge  | air
The foundation is below. Image 1's drill and welder point right/up/away; image 3
preserves the arm presentation. Both guide families use scene-wide surface ordering.

The branding sockets face left/foreground while working ends recede right/background.
The edited AutoArm BBCode remains unchanged. Commands were checked against the local
5.1.0 dispatchers, including aliases, shortcuts, PivotProbe and ToolSwap forwarding.
The control wrapper and its short alias are omitted at the user's request.
commands/commands.txt is the copyable reference. Pairing INI snippets are included.
Artwork correction prompts are in correction-prompts.json (built-in imagegen).
'''
    (ROOT / "ALL-ASSETS-README.txt").write_text(readme, encoding="utf-8")
    files.append((ROOT / "ALL-ASSETS-README.txt", "README.txt"))
    archive = ROOT / "AutoArm-Workshop-All-Assets.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for path, destination in files:
            z.write(path, destination)
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert sum(n.endswith(".png") for n in z.namelist()) == png_count == 34
    report = {"pngs": png_count, "transparentPanels": transparent_count,
              "commandPanels": 14, "archiveBytes": archive.stat().st_size}
    (ROOT / "all-assets-verification.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report))


if __name__ == "__main__":
    main()
