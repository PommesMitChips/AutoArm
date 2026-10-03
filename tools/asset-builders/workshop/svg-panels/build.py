"""Reassemble the 31 approved panels as editable SVG, without raster artwork.

The existing layout functions are reused with an SVG canvas. This preserves the
edited copy and layouts without calling the PNG builders' generation/packaging
entry points or their current-code command audits.
"""
import argparse
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image
from vector_assets import load_vector_asset

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop/svg-panels'
WORKSHOP = ROOT.parent
DOCS = REPO / 'docs'
SVG = "http://www.w3.org/2000/svg"
INKSCAPE = "http://www.inkscape.org/namespaces/inkscape"
XML = "http://www.w3.org/XML/1998/namespace"
ET.register_namespace("", SVG)
ET.register_namespace("inkscape", INKSCAPE)


def q(tag):
    return "{" + SVG + "}" + tag


def n(value):
    return f"{value:.5f}".rstrip("0").rstrip(".") or "0"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def freeze_edit_keys(root):
    """Keep the assembler's existing text/stroke/shape/span keys before removal."""
    nodes = list(root.iter())[1:]
    parents = {child: parent for parent in root.iter() for child in parent}
    used = {node.get("data-edit-key") for node in nodes if node.get("data-edit-key")}
    index = 0

    def style(node, name):
        values = dict(part.split(":", 1) for part in node.get("style", "").split(";") if ":" in part)
        return next((re.sub(r"\s*!important\s*$", "", value.strip()) for key, value in values.items() if key.strip() == name), "")

    def assign(node):
        nonlocal index
        while f"node-{index}" in used:
            index += 1
        key = f"node-{index}"
        node.set("data-edit-key", key)
        used.add(key)
        index += 1

    for node in nodes:
        stroke = style(node, "stroke")
        if not node.get("data-edit-key") and (node.tag == q("text") or
                "stroke" in node.attrib and node.get("stroke") != "none" or stroke and stroke != "none"):
            assign(node)
    index = max([-1, *[int(key[5:]) for key in used]]) + 1

    def visible(node):
        while node is not None:
            if node.tag in {q(tag) for tag in ("defs", "clipPath", "mask", "marker", "symbol")}:
                return False
            display = style(node, "display") or node.get("display")
            visibility = style(node, "visibility") or node.get("visibility")
            opacity = style(node, "opacity") or node.get("opacity")
            if display == "none" or visibility in ("hidden", "collapse"):
                return False
            if opacity not in (None, ""):
                try:
                    if float(opacity) == 0:
                        return False
                except ValueError:
                    pass
            node = parents.get(node)
        return True

    drawables = {q(tag) for tag in ("polygon", "rect", "path", "circle", "ellipse", "line", "polyline")}
    for node in nodes:
        if not node.get("data-edit-key") and node.tag in drawables and visible(node):
            assign(node)
    for node in nodes:
        if (not node.get("data-edit-key") and node.tag == q("tspan") and
                "".join(node.itertext()).strip() and not list(node.iter(q("tspan")))[1:] and visible(node)):
            assign(node)


def remove_separators(root):
    freeze_edit_keys(root)
    removed = []
    for parent in root.iter():
        for node in list(parent):
            if node.tag == q("line") and node.get("{" + INKSCAPE + "}label") == "Separator":
                removed.append({key: float(node.get(key, "1" if key == "stroke-width" else "0"))
                                for key in ("x1", "y1", "x2", "y2", "stroke-width")})
                parent.remove(node)
    return removed


def write_separator():
    path = ROOT / "common/separator.svg"
    path.parent.mkdir(exist_ok=True)
    root = ET.Element(q("svg"), {"version": "1.1", "width": "1240", "height": "48",
                                 "viewBox": "0 0 620 24", "id": "separator"})
    ET.SubElement(root, q("title")).text = "Panel separator"
    ET.SubElement(root, q("desc")).text = "Reusable horizontal separator on a transparent canvas. Place between panels as needed."
    ET.SubElement(root, q("line"), {"id": "separator-line", "x1": "24", "y1": "12", "x2": "596", "y2": "12",
                                  "stroke": "#496370", "stroke-width": "1", "data-edit-key": "node-0",
                                  "{" + INKSCAPE + "}label": "Separator"})
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
    return {"folder": "common", "filename": "separator.svg", "width": 1240, "height": 48, "transparent": True,
            "editableText": False, "vectorArtwork": True, "textObjects": 0, "artworkGroups": 0}


def package(manifest):
    records = manifest["panels"] + manifest["components"]
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    images = '\n'.join('<object type="image/svg+xml" data="' + r['folder'] + '/' + r['filename'] + '" style="aspect-ratio:' + str(r['width']) + '/' + str(r['height']) + '"></object>' for r in records)
    preview = '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AutoArm — editable SVG panels</title><style>html{scrollbar-gutter:stable}body{margin:0;background:#1b2838}main{width:min(620px,100%);margin:20px auto 48px}object{display:block;width:100%;height:auto;margin:0 0 20px}</style><main>' + images + '</main></html>'
    (ROOT / "preview.html").write_text(preview, encoding="utf-8")
    publish = [ROOT / r['folder'] / r['filename'] for r in records]
    publish += list((ROOT / "fonts").glob("*")) + [ROOT / name for name in ("README.md", "manifest.json", "preview.html")]
    with zipfile.ZipFile(WORKSHOP / "AutoArm-Editable-SVG-Panels.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for path in publish:
            archive.write(path, str(path.relative_to(ROOT)))
        archive.write(REPO / "LICENSE", "LICENSE")
    print(json.dumps({"svgPanels": len(manifest["panels"]), "svgComponents": len(manifest["components"]),
                      "editableText": True, "rasterImages": 0,
                      "restoredTerminalLabels": manifest['totalRestoredTerminalLabels']}))


def strip_existing():
    """Change only separators and editing metadata; retain manual SVG edits."""
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
    for record in manifest["panels"]:
        path = ROOT / record["folder"] / record["filename"]
        tree = ET.parse(path)
        removed = remove_separators(tree.getroot())
        if removed:
            record["removedSeparatorLines"] = record.get("removedSeparatorLines", []) + removed
        tree.write(path, encoding="utf-8", xml_declaration=True)
    manifest["components"] = [write_separator()]
    package(manifest)


FONT_CSS = "\n".join(
    "@font-face{font-family:'" + family + "';font-style:normal;font-weight:" + weights + ";src:url(data:font/ttf;base64,"
    + base64.b64encode((DOCS / "assets/fonts" / (family + ".ttf")).read_bytes()).decode("ascii") + ") format('truetype');}"
    for family, weights in [("Inter", "100 900"), ("Oxanium", "200 800")]
)


class SVGDraw:
    """The small ImageDraw subset used by the approved panel layout functions."""
    def __init__(self, canvas):
        self.canvas = canvas

    def rectangle(self, box, fill=None, outline=None, width=1):
        return self.rounded_rectangle(box, 0, fill, outline, width)

    def rounded_rectangle(self, box, radius=0, fill=None, outline=None, width=1):
        x1, y1, x2, y2 = (v / 2 for v in box)
        attrs = {"x": n(x1), "y": n(y1), "width": n(x2-x1), "height": n(y2-y1),
                 "fill": fill or "none", "stroke": outline or "none", "stroke-width": n(width/2)}
        if radius:
            attrs.update({"rx": n(radius/2), "ry": n(radius/2)})
        return self.canvas.element("rect", attrs, "Control shape")

    def line(self, points, fill=None, width=1):
        if len(points) == 4 and isinstance(points[0], (int, float)):
            points = list(zip(points[::2], points[1::2]))
        return self.canvas.element("polyline", {"points": " ".join(n(x/2) + "," + n(y/2) for x,y in points),
                                               "fill": "none", "stroke": fill or "#496370", "stroke-width": n(width/2)}, "Rule")

    def text(self, position, value, font, fill, anchor=None):
        x, y = (v / 2 for v in position)
        family = font.getname()[0]
        if family.startswith("Consolas"):
            family = "Consolas, 'Liberation Mono', monospace"
        size = font.size / 2
        if anchor != "ls":
            # Pillow's default left/ascender origin becomes an SVG baseline.
            y += font.getmetrics()[0] / 2
        attrs = {"x": n(x), "y": n(y), "font-family": family, "font-size": n(size),
                 "font-weight": "400", "fill": fill, "text-anchor": "start"}
        text = self.canvas.element("text", attrs, "Code / field text")
        text.text = value
        return text


def canvas_type(layout, assets, output):
    class SVGCanvas(layout.Canvas):
        def __init__(self):
            self.root = ET.Element(q("svg"), {"version": "1.1", "width": "1240", "height": "6600",
                                              "viewBox": "0 0 620 3300", "{" + XML + "}space": "preserve"})
            definitions = ET.SubElement(self.root, q("defs"))
            ET.SubElement(definitions, q("style"), {"type": "text/css"}).text = FONT_CSS
            self.content = ET.SubElement(self.root, q("g"), {"id": "panel-content", "{" + INKSCAPE + "}groupmode": "layer",
                                                           "{" + INKSCAPE + "}label": "Panel content"})
            self.draw = SVGDraw(self)
            self.counter = 0
            self.artwork_count = 0
            self.restored_labels = 0

        def element(self, tag, attrs, label):
            self.counter += 1
            attrs = {**attrs, "id": tag + "-" + str(self.counter), "{"+INKSCAPE+"}label": label}
            return ET.SubElement(self.content, q(tag), attrs)

        def text(self, value, x, y, width, size=16, leading=26, color=layout.INK, weight=400, family="Inter"):
            words = []
            for string, bold in layout.runs(value):
                words.extend((v, bold) for v in re.findall(r"\S+|\s+", string))
            line, used, lines = [], 0, []
            pending_space = False
            for word, bold in words:
                if word.isspace():
                    pending_space = True
                    continue
                run_weight = 600 if bold and weight < 600 else weight
                face = layout.font(size, run_weight, family)
                space = " " if pending_space and line else ""
                length = face.getlength(space + word) / 2
                if line and used + length > width:
                    lines.append(line)
                    line, used, space = [], 0, ""
                    length = face.getlength(word) / 2
                if length > width:
                    raise ValueError("Unbreakable text exceeds column: " + word)
                line.append((space + word, run_weight, layout.STRONG if bold else color))
                used += length
                pending_space = False
            if line:
                lines.append(line)
            optical = max(14, min(32, size))
            text = self.element("text", {"font-family": family, "font-size": n(size), "font-weight": str(weight),
                                         "fill": color, "font-kerning": "normal", "data-wrap-width": n(width),
                                         "data-source-text": layout.plain(value),
                                         "style": f"font-optical-sizing:none;font-variation-settings:'opsz' {n(optical)},'wght' {weight}"}, "Text: " + layout.plain(value)[:70])
            for index, values in enumerate(lines):
                span = ET.SubElement(text, q("tspan"), {"x": n(x), "y": n(y + index * leading + size)})
                merged = []
                for string, run_weight, run_color in values:
                    if merged and merged[-1][1:] == [run_weight, run_color]:
                        merged[-1][0] += string
                    else:
                        merged.append([string, run_weight, run_color])
                if len(merged) == 1 and merged[0][1] == weight and merged[0][2] == color:
                    span.text = merged[0][0]
                else:
                    for string, run_weight, run_color in merged:
                        child = ET.SubElement(span, q("tspan"), {"font-weight": str(run_weight), "fill": run_color,
                                                                "style": f"font-optical-sizing:none;font-variation-settings:'opsz' {n(optical)},'wght' {run_weight}"})
                        child.text = string
            return y + len(lines) * leading

        def rule(self, x, y, width):
            self.element("line", {"x1": n(x), "y1": n(y), "x2": n(x+width), "y2": n(y),
                                   "stroke": layout.LINE, "stroke-width": "1"}, "Separator")

        def art(self, token, x, y, width):
            self.artwork_count += 1
            vector = load_vector_asset(assets / (token + ".svg"), prefix="art" + str(self.artwork_count))
            self.restored_labels += int(vector.get("data-restored-text-labels", "0"))
            # Use the raster source's exact aspect ratio to retain panel layout.
            with Image.open(assets / (token + ".png")) as reference:
                height = reference.height / reference.width * width
            vector.set("x", n(x)); vector.set("y", n(y))
            vector.set("width", n(width)); vector.set("height", n(height))
            vector.set("preserveAspectRatio", "xMidYMid meet")
            self.counter += 1
            group = ET.SubElement(self.content, q("g"), {"id": "artwork-" + str(self.artwork_count),
                                                        "{"+INKSCAPE+"}label": "Artwork: " + token})
            group.append(vector)
            return y + height

        def save(self, filename, bottom):
            self.rule(24, bottom + 15, 572)
            pixel_height = round((bottom + 38) * 2)
            filename = Path(filename).with_suffix(".svg").name
            self.root.set("height", str(pixel_height))
            self.root.set("viewBox", "0 0 620 " + n(pixel_height/2))
            self.root.set("id", Path(filename).stem)
            title = ET.Element(q("title")); title.text = Path(filename).stem.replace("-", " ")
            self.root.insert(0, title)
            desc = ET.Element(q("desc")); desc.text = "Editable native text and vector geometry, transparent background. No embedded raster artwork."
            self.root.insert(1, desc)
            removed = remove_separators(self.root)
            path = output / filename
            path.parent.mkdir(parents=True, exist_ok=True)
            ET.ElementTree(self.root).write(path, encoding="utf-8", xml_declaration=True)
            return {"filename": filename, "width": 1240, "height": pixel_height,
                    "transparent": True, "editableText": True, "vectorArtwork": True,
                    "textObjects": len(list(self.root.iter(q('text')))), "artworkGroups": self.artwork_count,
                    "restoredTerminalLabels": self.restored_labels, "removedSeparatorLines": removed}
    return SVGCanvas


def main():
    records = []
    source_paths = [REPO / 'docs/steam-workshop/getting-started.bbcode.txt', WORKSHOP / "commands/catalog.json"]
    hashes = {str(p.relative_to(REPO)): hashlib.sha256(p.read_bytes()).hexdigest() for p in source_paths}

    auto = load("svg_autoarm", TOOLS / "workshop/build_panels.py")
    auto.Canvas = canvas_type(auto, auto.ASSETS, ROOT / "autoarm-guide")
    sections = auto.parse(auto.SOURCE.read_text(encoding="utf-8-sig"))
    for i, section in enumerate(sections):
        record = auto.render_section(section, i)
        records.append({**record, "folder": "autoarm-guide"})

    tools = load("svg_toolswap", TOOLS / "workshop/tool-swap/build.py")
    tools.layout.Canvas = canvas_type(tools.layout, tools.ART, ROOT / "toolswap-guide")
    for i in range(len(tools.COPY)):
        records.append({**tools.panel(i), "folder": "toolswap-guide"})

    commands = load("svg_commands", TOOLS / "workshop/commands/build.py")
    commands.layout.Canvas = canvas_type(commands.layout, WORKSHOP / "commands", ROOT / "commands")
    catalog = json.loads((WORKSHOP / "commands/catalog.json").read_text(encoding="utf-8"))
    for panel in catalog["panels"]:
        records.append({**commands.render(panel), "folder": "commands"})

    assert len(records) == 31
    for record in records:
        root = ET.parse(ROOT / record["folder"] / record["filename"]).getroot()
        assert not list(root.iter(q("image"))) and not list(root.iter(q("foreignObject")))
        ids = [e.get("id") for e in root.iter() if e.get("id")]
        assert len(ids) == len(set(ids)), record["filename"]
        assert list(root.iter(q("text"))), record["filename"]
    for p in source_paths:
        assert hashlib.sha256(p.read_bytes()).hexdigest() == hashes[str(p.relative_to(REPO))]

    fonts = ROOT / "fonts"; fonts.mkdir(exist_ok=True)
    for source in (DOCS / "assets/fonts").glob("*"):
        if source.suffix in (".ttf", ".txt"):
            shutil.copy2(source, fonts / source.name)
    manifest = {"panels": records, "components": [write_separator()], "sourceHashes": hashes, "nativeText": True, "rasterImages": 0,
                "totalRestoredTerminalLabels": sum(r['restoredTerminalLabels'] for r in records)}
    package(manifest)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strip-separators", action="store_true", help="Update existing SVGs without regenerating their text or artwork.")
    args = parser.parse_args()
    strip_existing() if args.strip_separators else main()
