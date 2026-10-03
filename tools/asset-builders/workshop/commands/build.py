"""Render the audited public command catalog as transparent guide panels."""
import hashlib
import html
import importlib.util
import json
from pathlib import Path
import re
import zipfile

from PIL import ImageFont

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop/commands'

spec = importlib.util.spec_from_file_location("command_layout", TOOLS / "workshop/build_panels.py")
layout = importlib.util.module_from_spec(spec)
spec.loader.exec_module(layout)
layout.OUTPUT = ROOT / "panels"
MONO = {}


def mono(size):
    if size not in MONO:
        MONO[size] = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", round(size * 2))
    return MONO[size]


def code(c, text, x, y, width, size=17, leading=25, color=layout.STRONG):
    face = mono(size)
    lines, current = [], ""
    for word in text.split():
        trial = current + (" " if current else "") + word
        if current and face.getlength(trial) / 2 > width:
            lines.append(current)
            current = word
        else:
            current = trial
        if face.getlength(current) / 2 > width:
            raise ValueError("Code token exceeds the panel width: " + word)
    if current:
        lines.append(current)
    for i, line in enumerate(lines):
        c.draw.text((round(x * 2), round((y + i * leading + size) * 2)), line, font=face, fill=color, anchor="ls")
    return y + len(lines) * leading


def render(panel):
    c = layout.Canvas()
    y = c.text(panel["family"] + " Commands", 24, 28, 572, 32, 40, layout.STRONG, 500, "Oxanium")
    y = c.text(panel["title"], 24, y + 12, 572, 23, 30, layout.AMBER, 600) + 15
    y = c.text(panel["intro"], 24, y, 572, 15.5, 24) + 22
    if panel.get("shortcuts"):
        for i, (number, action) in enumerate(panel["shortcuts"]):
            x = 24 + (i % 3) * 191
            yy = y + (i // 3) * 88
            c.draw.rounded_rectangle((x * 2, yy * 2, (x + 48) * 2, (yy + 43) * 2), radius=4 * 2,
                                    fill="#314b59", outline="#849da9", width=2)
            c.text(number, x + 16, yy + 10, 40, 18, 25, layout.STRONG, 600)
            code(c, action, x + 60, yy + 13, 119, 14, 21, layout.INK)
        y += 3 * 88 + 12
    for i, row in enumerate(panel["rows"]):
        c.rule(24, y, 572)
        y = code(c, row["syntax"], 24, y + 15, 572) + 4
        if row.get("aliases"):
            y = c.text("Aliases: " + row["aliases"], 24, y, 572, 13.5, 21, layout.SECONDARY) + 5
        y = c.text(row["description"], 24, y, 572, 15.5, 24) + 7
        if row.get("example"):
            label = "Format: " if "<" in row["example"] else "Example: "
            y = code(c, label + row["example"], 24, y + 2, 572, 13.5, 21, layout.SECONDARY) + 7
        if row.get("note"):
            y = c.text(row["note"], 24, y + 2, 572, 14, 22, layout.SECONDARY) + 7
        y += 13
    for note in panel.get("notes", []):
        y = c.text(note, 24, y + 10, 572, 14, 22, layout.SECONDARY) + 6
    return {**c.save(panel["file"] + ".png", y), "family": panel["family"], "title": panel["title"]}


def coverage(catalog):
    arm = (REPO / "stable/AutoArm_Source.txt").read_text(encoding="utf-8")
    tools = (REPO / "stable/AutoArm_ToolSwap_Source.txt").read_text(encoding="utf-8")
    arm_run = arm.split("void RunCommand(string command)", 1)[1].split("static bool InputToggle", 1)[0]
    expected_arm = set(re.findall(r'case "([^"]+)"', arm_run)) - {"move", "relmove", "runfrompanel"}
    expected_arm |= {"select", "release", "stopall", "tool", "toolswap", "park", "toolscan", "toolinfo", "swapcancel"}
    expected_arm |= {str(i) for i in range(1, 10)}
    start = tools.index("public void Main(string argument, UpdateType source)", tools.index("void ConfigureLink()"))
    tools_main = tools[start:tools.index("void Queue(string command)", start)]
    expected_tools = set(re.findall(r'verb == "([^"]+)"', tools_main)) | {"select", "stopall"}
    actual = {"AutoArm": set(), "ToolSwap": set()}
    for p in catalog["panels"]:
        if p["family"] in actual:
            for row in p["rows"]:
                actual[p["family"]].update(row["verbs"])
            actual[p["family"]].update(n for n, _ in p.get("shortcuts", []))
    omitted = {family: set(catalog.get("omittedFromPanels", {}).get(family, [])) for family in actual}
    for family, expected in [("AutoArm", expected_arm), ("ToolSwap", expected_tools)]:
        if not omitted[family] <= expected:
            raise ValueError("Unknown omitted command for " + family)
        selected = expected - omitted[family]
        if actual[family] != selected:
            raise ValueError(f"Command coverage for {family}: missing={sorted(selected-actual[family])}, extra={sorted(actual[family]-selected)}")
    return {"AutoArm": sorted(actual["AutoArm"]), "ToolSwap": sorted(actual["ToolSwap"]),
            "coverageMatchesSelection": True, "coverageComplete": not any(omitted.values()),
            "omittedFromPanels": {family: sorted(verbs) for family, verbs in omitted.items()}}


def main():
    catalog = json.loads((ROOT / "catalog.json").read_text(encoding="utf-8"))
    checked = coverage(catalog)
    layout.OUTPUT.mkdir(exist_ok=True)
    panels = [render(panel) for panel in catalog["panels"]]
    manifest = {"scriptBuild": catalog["scriptBuild"], "panels": panels, **checked,
                "sourceHashes": {name: hashlib.sha256((REPO / name).read_bytes()).hexdigest() for name in catalog["sourceFiles"]}}
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    images = '\n'.join(f'<img src="panels/{p["filename"]}" width="{p["width"]}" height="{p["height"]}" alt="{html.escape(p["family"] + ": " + p["title"])}">' for p in panels)
    preview = '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AutoArm and ToolSwap — command panels</title><style>html{scrollbar-gutter:stable}body{margin:0;background:#1b2838}main{width:min(620px,100%);margin:20px auto 48px}img{display:block;width:100%;height:auto;margin:0 0 20px}</style><main>' + images + '</main></html>'
    (ROOT / "preview.html").write_text(preview, encoding="utf-8")
    # Keep copyable reference text beside the raster panels.
    text = []
    for p in catalog["panels"]:
        text += [p["family"] + " — " + p["title"], "", p["intro"], ""]
        for number, action in p.get("shortcuts", []):
            text.append(number + " → " + action)
        for row in p["rows"]:
            text += [row["syntax"], row["description"]]
            for field in ("aliases", "example", "note"):
                if row.get(field):
                    text.append(field.title() + ": " + row[field])
            text.append("")
        text += p.get("notes", []) + ["", ""]
    (ROOT / "commands.txt").write_text('\n'.join(text), encoding="utf-8")
    files = list(layout.OUTPUT.glob("*.png")) + [(ROOT / name if (ROOT / name).exists() else REPO / "docs/steam-workshop/commands" / name) for name in ("commands.txt", "catalog.json", "manifest.json", "preview.html", "README.md")]
    with zipfile.ZipFile(ROOT / "AutoArm-ToolSwap-Command-Panels.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for p in files:
            archive.write(p, str(p.relative_to(ROOT)))
    print(json.dumps({"panels": len(panels), "AutoArmVerbs": len(checked['AutoArm']), "ToolSwapVerbs": len(checked['ToolSwap']), "coverageMatchesSelection": checked['coverageMatchesSelection'], "sizes": [(p['width'],p['height']) for p in panels]}))


if __name__ == "__main__":
    main()
