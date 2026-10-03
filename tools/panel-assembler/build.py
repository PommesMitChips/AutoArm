"""Bundle the offline panel utility, SVG panels/components and example templates."""
import base64
from io import BytesIO
import json
import math
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile
from PIL import Image, ImageDraw, PngImagePlugin

ROOT = Path(__file__).resolve().parent
WORKSHOP = ROOT.parents[1] / 'docs/assets/workshop'
PANELS = WORKSHOP / "svg-panels"
NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", NS)
ET.register_namespace("inkscape", "http://www.inkscape.org/namespaces/inkscape")


def main():
    manifest = json.loads((PANELS / "manifest.json").read_text(encoding="utf-8"))
    assets, font_css = [], None
    categories = {"autoarm-guide": "AutoArm guide", "toolswap-guide": "ToolSwap guide", "commands": "Command reference", "common": "Layout elements"}
    records = manifest["panels"] + manifest.get("components", [])
    for panel in records:
        identifier = panel["folder"] + "/" + panel["filename"]
        root = ET.parse(PANELS / identifier).getroot()
        for parent in root.iter():
            for child in list(parent):
                if child.tag == "{" + NS + "}style" and "@font-face" in (child.text or ""):
                    font_css = font_css or child.text
                    parent.remove(child)
        stem = Path(panel["filename"]).stem
        name = re.sub(r"^(?:autoarm|toolswap)-", "", stem).replace("-", " ").title()
        png_folders = {"autoarm-guide": "panels", "toolswap-guide": "tool-swap/panels", "commands": "commands/panels"}
        if panel["folder"] == "common":
            thumb = Image.new("RGBA", (panel["width"], panel["height"]))
            view = [float(value) for value in root.get("viewBox").split()]
            sx, sy = thumb.width / view[2], thumb.height / view[3]
            draw = ImageDraw.Draw(thumb)
            for line in root.iter("{" + NS + "}line"):
                draw.line((float(line.get("x1"))*sx, float(line.get("y1"))*sy,
                           float(line.get("x2"))*sx, float(line.get("y2"))*sy),
                          fill=line.get("stroke"), width=max(1, round(float(line.get("stroke-width", "1"))*sy)))
            origin = f"Origin: native vector component {identifier}; rendered with Pillow for the library thumbnail."
        else:
            png_path = WORKSHOP / png_folders[panel["folder"]] / Path(panel["filename"]).with_suffix(".png").name
            with Image.open(png_path) as image:
                thumb = image.convert("RGBA")
            view = [float(value) for value in root.get("viewBox").split()]
            sx, sy = thumb.width / view[2], thumb.height / view[3]
            draw = ImageDraw.Draw(thumb)
            for line in panel.get("removedSeparatorLines", []):
                margin = line["stroke-width"] * sy / 2 + 1
                draw.rectangle((math.floor(min(line["x1"], line["x2"])*sx-margin),
                                math.floor(min(line["y1"], line["y2"])*sy-margin),
                                math.ceil(max(line["x1"], line["x2"])*sx+margin),
                                math.ceil(max(line["y1"], line["y2"])*sy+margin)), fill=(0,0,0,0))
            origin = f"Origin: existing AutoArm workshop panel {png_path.relative_to(WORKSHOP).as_posix()}; layout separator rules cleared and resized with Pillow for the library thumbnail. Assembly exports use the editable source SVG {identifier}."
        thumb.thumbnail((128, 156), Image.Resampling.LANCZOS)
        buffer = BytesIO()
        provenance = PngImagePlugin.PngInfo()
        provenance.add_text("impeccable:prompt", origin)
        thumb.save(buffer, format="PNG", optimize=True, pnginfo=provenance)
        thumbnail = "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")
        assets.append({"id": identifier, "name": name, "category": categories[panel["folder"]], "thumbnail": thumbnail, "svg": ET.tostring(root, encoding="unicode")})
    assert len(assets) == len(records) and font_css

    def group(identifier, name, selection):
        return {"id": identifier, "name": name, "filename": identifier, "layout": "vertical", "width": 620,
                "gap": 20, "padding": 0, "theme": {}, "panels": [{"asset": asset["id"]} for asset in selection]}

    autoarm = [asset for asset in assets if asset["id"].startswith("autoarm-guide/")]
    toolswap = [asset for asset in assets if asset["id"].startswith("toolswap-guide/")]
    commands = [asset for asset in assets if asset["id"].startswith("commands/")]
    components = [asset for asset in assets if asset["id"].startswith("common/")]
    guides = [group("autoarm-guide", "AutoArm guide", autoarm), group("toolswap-guide", "ToolSwap guide", toolswap)]
    groups = [*guides, group("commands", "Command reference", commands)]
    if components:
        groups.append(group("layout-elements", "Layout elements", components))
    template = lambda name, assemblies: {"version": 1, "name": name, "theme": {"lineScope": "rules"}, "scale": 1, "groups": assemblies}
    templates = [
        template("AutoArm and ToolSwap guides", guides),
        template("All panels", groups),
        template("Command reference", [group("autoarm-commands", "AutoArm commands", [asset for asset in commands if not Path(asset["id"]).name.startswith("toolswap")]), group("toolswap-commands", "ToolSwap commands", [asset for asset in commands if Path(asset["id"]).name.startswith("toolswap")])]),
        template("One PNG per panel", [group(Path(asset["id"]).stem, asset["name"], [asset]) for asset in assets]),
    ]
    payload = json.dumps({"assets": assets, "fontCSS": font_css, "templates": templates}, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    html = (ROOT / "index.html").read_text(encoding="utf-8")
    for placeholder, content in [("/*__FONT_CSS__*/", font_css), ("/*__UI_CSS__*/", (ROOT / "style.css").read_text(encoding="utf-8")), ("/*__INITIAL_DATA__*/", payload), ("/*__CORE_JS__*/", (ROOT / "core.js").read_text(encoding="utf-8")), ("/*__APP_JS__*/", (ROOT / "app.js").read_text(encoding="utf-8"))]:
        assert placeholder in html
        html = html.replace(placeholder, content)
    (ROOT / 'dist').mkdir(exist_ok=True)
    output = ROOT / 'dist/AutoArm-Panel-Assembler.html'
    output.write_text(html, encoding="utf-8")
    samples = ROOT / "templates"
    samples.mkdir(exist_ok=True)
    for value in templates:
        filename = re.sub(r"[^a-z0-9]+", "-", value["name"].lower()).strip("-") + ".json"
        (samples / filename).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    with zipfile.ZipFile(ROOT / 'dist/AutoArm-Panel-Assembler.zip', "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(output, output.name)
        archive.write(ROOT / "README.md", "README.md")
        archive.write(ROOT.parents[1] / "LICENSE", "LICENSE")
        for path in samples.glob("*.json"):
            archive.write(path, "templates/" + path.name)
        for path in (PANELS / "fonts").glob("*.txt"):
            archive.write(path, "font-licenses/" + path.name)
    print(json.dumps({"html": str(output), "bundledSVGs": len(assets), "templates": len(templates), "bytes": output.stat().st_size, "offline": True}))


if __name__ == "__main__":
    main()
