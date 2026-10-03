"""Render the user's BBCode copy as transparent, illustrated guide panels.

The BBCode is read-only. Original SVGs supply the artwork, and the original
Inter/Oxanium fonts supply the typesetting. Run --prepare, render_panel_assets.mjs,
then this script without arguments. Only the final panels enter the ZIP.
"""
import copy
import hashlib
import html
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image, ImageDraw, ImageFont

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop'
DOCS = REPO / 'docs'
SOURCE = REPO / 'docs/steam-workshop/getting-started.bbcode.txt'
ASSETS = ROOT / ".panel-assets"
OUTPUT = ROOT / "panels"
SCALE = 2
WIDTH = 620
INK = "#e5edf0"
STRONG = "#f8faf9"
SECONDARY = "#b4c5cd"
AMBER = "#e9b34c"
LINE = "#496370"
NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", NS)

ART = {
    "IMAGE_01_ROTOR_ON_GRID": "assets/models/autoarm/rotor-on-grid.svg",
    "IMAGE_02_NAME_BASE": "assets/models/autoarm/name-base-terminal.svg",
    "IMAGE_03_ARM_FRONT": "assets/models/autoarm/arm-front.svg",
    "IMAGE_03A_MISALIGNED_STAGES": "assets/models/autoarm/unsupported-misaligned.svg",
    "IMAGE_03B_HYDRAULIC_LINKAGE": "assets/models/autoarm/unsupported-hydraulic.svg",
    "IMAGE_04_NAME_HEAD": "assets/models/autoarm/name-head-terminal.svg",
    "IMAGE_05_CONTROLLER": "assets/blocks/controller-isometric.svg",
    "IMAGE_06_ARM_AND_PROGRAMMABLE_BLOCK": "assets/models/autoarm/arm-isometric-pb.svg",
    "IMAGE_07_RUN_ON": "assets/models/autoarm/run-terminal.svg",
}
NAMES = [
    "00-getting-started", "01-place-first-actuator", "02-name-base",
    "03-build-arm", "04-name-head", "05-place-controller",
    "06-place-programmable-block", "07-load-and-run", "08-control-arm",
]


def plain(text):
    return re.sub(r"\[/?(?:b|i|u)\]", "", text)


def parse(source):
    """Keep every prose/list/heading block, without bringing in canonical copy."""
    sections, current, paragraph, listing = [], None, [], None

    def flush():
        if paragraph:
            current["blocks"].append({"type": "p", "text": " ".join(paragraph)})
            paragraph.clear()

    for raw in source.splitlines():
        line = raw.strip()
        heading = re.fullmatch(r"\[h([123])\](.*?)\[/h\1\]", line)
        image = re.fullmatch(r"\[img\]\{\{(IMAGE_[A-Z0-9_]+)\}\}\[/img\]", line)
        if heading and heading[1] in ("1", "2"):
            if current:
                flush()
            current = {"heading": heading[2], "level": int(heading[1]), "blocks": []}
            sections.append(current)
        elif not current:
            if line:
                raise ValueError("Text appears before the guide title")
        elif not line:
            flush()
        elif heading:
            flush()
            current["blocks"].append({"type": "h3", "text": heading[2]})
        elif image:
            flush()
            if image[1] not in ART:
                raise ValueError("Unknown artwork: " + image[1])
            current["blocks"].append({"type": "image", "token": image[1]})
        elif line in ("[olist]", "[list]"):
            flush()
            listing = {"type": "ol" if line == "[olist]" else "ul", "items": []}
            current["blocks"].append(listing)
        elif line in ("[/olist]", "[/list]"):
            listing = None
        elif line.startswith("[*]"):
            if listing is None:
                raise ValueError("List item outside a list")
            listing["items"].append(line[3:])
        else:
            paragraph.append(line)
    flush()
    return sections


def prepare(sections):
    ASSETS.mkdir(exist_ok=True)
    used = {b["token"] for s in sections for b in s["blocks"] if b["type"] == "image"}
    for token in sorted(used):
        source = DOCS / ART[token]
        root = ET.fromstring(source.read_text(encoding="utf-8"))
        _, _, width, height = map(float, root.get("viewBox").split())
        terminal = "terminal" in source.name
        if terminal:
            for node in list(root):
                if node.tag == f"{{{NS}}}rect" and float(node.get("x", 0)) == 0 and float(node.get("y", 0)) == 0:
                    if float(node.get("width", 0)) == width and float(node.get("height", 0)) == height:
                        root.remove(node)
        for node in root.iter():
            if node.tag == f"{{{NS}}}text":
                node.set("fill", INK)
                # At the Workshop column width, the original diagram labels
                # need a larger type size, while all joint geometry stays put.
                if token == "IMAGE_03_ARM_FRONT":
                    node.set("font-size", "26")
                elif token == "IMAGE_06_ARM_AND_PROGRAMMABLE_BLOCK":
                    node.set("font-size", "34")
            if not terminal and node.get("stroke", "").lower() == "#9db1bb":
                node.set("stroke", "#849da9")
        # Keep the original steel/amber materials. Brighten only error marks.
        parents = {c: p for p in root.iter() for c in p}
        for node in list(root.iter()):
            if node.get("stroke", "").lower() == "#ad5447":
                contour = copy.deepcopy(node)
                contour.set("stroke", "#263e4b")
                contour.set("stroke-width", str(float(node.get("stroke-width", 1.6)) + 2))
                parent = parents[node]
                parent.insert(list(parent).index(node), contour)
                node.set("stroke", "#e99f91")
        (ASSETS / (token + ".svg")).write_text(ET.tostring(root, encoding="unicode"), encoding="utf-8")
    (ASSETS / "assets.json").write_text(json.dumps(sorted(used)), encoding="utf-8")


FONTS = {}


def font(size, weight=400, family="Inter"):
    key = (size, weight, family)
    if key not in FONTS:
        face = ImageFont.truetype(str(DOCS / "assets/fonts" / (family + ".ttf")), round(size * SCALE))
        face.set_variation_by_axes([max(14, min(32, size)), weight] if family == "Inter" else [weight])
        FONTS[key] = face
    return FONTS[key]


def runs(text):
    bold = False
    result = []
    for piece in re.split(r"(\[/?b\])", text):
        if piece == "[b]":
            bold = True
        elif piece == "[/b]":
            bold = False
        elif piece:
            result.append((piece, bold))
    return result


class Canvas:
    def __init__(self):
        self.image = Image.new("RGBA", (WIDTH * SCALE, 3300 * SCALE), (0, 0, 0, 0))
        self.draw = ImageDraw.Draw(self.image)

    def text(self, text, x, y, width, size=16, leading=26, color=INK, weight=400, family="Inter"):
        """Wrap rich text by actual glyph advances, preserving bold emphasis."""
        words = []
        for value, bold in runs(text):
            words.extend((v, bold) for v in re.findall(r"\S+|\s+", value))
        line, used, lines = [], 0, []
        pending_space = False
        for word, bold in words:
            if word.isspace():
                pending_space = True
                continue
            f = font(size, 600 if bold and weight < 600 else weight, family)
            space = " " if pending_space and line else ""
            length = f.getlength(space + word) / SCALE
            if line and used + length > width:
                lines.append(line)
                line, used, space = [], 0, ""
                length = f.getlength(word) / SCALE
            if length > width:
                raise ValueError("Unbreakable text exceeds its column: " + word)
            line.append((space + word, f, bold, length))
            used += length
            pending_space = False
        if line:
            lines.append(line)
        for index, values in enumerate(lines):
            cursor = x
            for value, f, bold, length in values:
                self.draw.text((round(cursor * SCALE), round((y + index * leading + size) * SCALE)), value,
                               font=f, fill=STRONG if bold else color, anchor="ls")
                cursor += length
        return y + len(lines) * leading

    def rule(self, x, y, width):
        self.draw.line((x * SCALE, y * SCALE, (x + width) * SCALE, y * SCALE), fill=LINE, width=SCALE)

    def art(self, token, x, y, width):
        artwork = Image.open(ASSETS / (token + ".png")).convert("RGBA")
        height = artwork.height / artwork.width * width
        artwork = artwork.resize((round(width * SCALE), round(height * SCALE)), Image.Resampling.LANCZOS)
        self.image.alpha_composite(artwork, (round(x * SCALE), round(y * SCALE)))
        return y + height

    def blocks(self, blocks, x, y, width, size=16, leading=26, warnings=False):
        for block in blocks:
            if block["type"] == "p":
                prefix = re.match(r"^\[b\](.*?)\[/b\]\s+(.+)$", block["text"]) if warnings else None
                if prefix:
                    y = self.text(prefix[1], x, y + 8, width, 17, 24, "#e99f91", 600) + 8
                    y = self.text(prefix[2], x, y, width, size, leading) + 16
                else:
                    y = self.text(block["text"], x, y, width, size, leading) + 16
            elif block["type"] == "h3":
                y = self.text(block["text"], x, y + 8, width, 22, 29, STRONG, 600) + 14
            elif block["type"] in ("ol", "ul"):
                for i, item in enumerate(block["items"]):
                    marker = str(i + 1) + "." if block["type"] == "ol" else "•"
                    self.text(marker, x, y, 25, size, leading, SECONDARY)
                    y = self.text(item, x + 27, y, width - 27, size, leading) + 7
                y += 9
            elif block["type"] == "image":
                y = self.art(block["token"], x, y + 8, width) + 24
        return y

    def save(self, filename, bottom):
        self.rule(24, bottom + 15, 572)
        height = round((bottom + 38) * SCALE)
        if height >= self.image.height:
            raise ValueError("Panel exceeds allocated canvas")
        result = self.image.crop((0, 0, WIDTH * SCALE, height))
        assert result.getchannel("A").getextrema() == (0, 255)
        assert all(result.getpixel(p)[3] == 0 for p in ((0, 0), (result.width - 1, 0), (0, height - 1), (result.width - 1, height - 1)))
        result.save(OUTPUT / filename, optimize=True)
        return {"filename": filename, "width": result.width, "height": height, "transparent": True}


def render_section(section, index):
    canvas = Canvas()
    if index == 0:
        # The approved title's line break and lettering, without site chrome.
        title = plain(section["heading"])
        split = title.rsplit(" with ", 1)
        y = canvas.text(split[0], 24, 28, 572, 42, 50, STRONG, 500, "Oxanium")
        if len(split) > 1:
            y = canvas.text("with " + split[1], 24, y, 572, 42, 50, STRONG, 500, "Oxanium")
        y = canvas.blocks(section["blocks"], 24, y + 23, 555, 18, 29)
        return canvas.save(NAMES[index] + ".png", y)

    heading = re.sub(r"^\d+\.\s*", "", section["heading"])
    canvas.text(str(index), 24, 28, 30, 28, 35, AMBER, 500, "Oxanium")
    content = section["blocks"]
    if index == 3:
        y = canvas.text(heading, 62, 28, 534, 24, 31, STRONG, 600) + 18
        split = next(i for i, b in enumerate(content) if b["type"] == "image")
        y = canvas.blocks(content[:split], 62, y, 534)
        y += 16
        left = canvas.art(content[split]["token"], 24, y, 284)
        right = canvas.blocks(content[split + 1:], 334, y - 8, 262, 14.5, 23, warnings=True)
        bottom = max(left, right)
    elif index == 8:
        y = canvas.text(heading, 62, 28, 534, 24, 31, STRONG, 600) + 18
        y = canvas.blocks([b for b in content if b["type"] != "ul"], 62, y, 534)
        y += 18
        items = next(b["items"] for b in content if b["type"] == "ul")
        for i, item in enumerate(items):
            keys, direction = plain(item).split(" — ", 1)
            center = 119 + i * 191
            key_width = max(108, font(18, 600).getlength(keys) / SCALE + 26)
            box = ((center - key_width / 2) * SCALE, y * SCALE, (center + key_width / 2) * SCALE, (y + 54) * SCALE)
            canvas.draw.rounded_rectangle(box, radius=5 * SCALE, fill="#314b59", outline="#849da9", width=SCALE)
            canvas.draw.line((box[0] + 8, box[3] - 3, box[2] - 8, box[3] - 3), fill="#849da9", width=2 * SCALE)
            tw = font(18, 600).getlength(keys) / SCALE
            canvas.text(keys, center - tw / 2, y + 15, key_width, 18, 24, STRONG, 600)
            tw = font(13.5).getlength(direction) / SCALE
            canvas.text(direction, center - tw / 2, y + 69, 184, 13.5, 21, SECONDARY)
        bottom = y + 96
    else:
        copy_width = 236
        y = canvas.text(heading, 62, 28, copy_width, 24, 31, STRONG, 600) + 18
        body = [b for b in content if b["type"] != "image"]
        bottom = canvas.blocks(body, 62, y, copy_width)
        art = [b for b in content if b["type"] == "image"]
        art_y = 25
        for b in art:
            art_y = canvas.art(b["token"], 320, art_y, 276)
        bottom = max(bottom, art_y)
    return canvas.save(NAMES[index] + ".png", bottom)


def main():
    source_bytes = SOURCE.read_bytes()
    source_hash = hashlib.sha256(source_bytes).hexdigest()
    sections = parse(source_bytes.decode("utf-8-sig"))
    if len(sections) != 9:
        raise ValueError("Expected the edited title and eight steps")
    if "--prepare" in sys.argv:
        prepare(sections)
        print("Prepared original SVG artwork; edited BBCode remains unchanged.")
        return
    OUTPUT.mkdir(exist_ok=True)
    panels = [render_section(section, index) for index, section in enumerate(sections)]
    manifest = {"copySource": SOURCE.name, "copySha256": source_hash, "logicalWidth": WIDTH, "scale": SCALE,
                "panels": [{**panel, "textBlocks": section} for panel, section in zip(panels, sections)]}
    (ROOT / "panels-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    images = "\n".join(f'<img src="panels/{p["filename"]}" width="{p["width"]}" height="{p["height"]}" alt="{html.escape(plain(s["heading"]))}">' for p, s in zip(panels, sections))
    preview = '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AutoArm — PNG guide</title><style>html{scrollbar-gutter:stable}body{margin:0;background:#1b2838}main{width:min(620px,100%);margin:20px auto 48px}img{display:block;width:100%;height:auto;margin:0 0 20px}</style><main>' + images + '</main></html>'
    (ROOT / "panels-preview.html").write_text(preview, encoding="utf-8")
    with zipfile.ZipFile(ROOT / "AutoArm-Getting-Started-PNG.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for panel in panels:
            archive.write(OUTPUT / panel["filename"], panel["filename"])
    assert SOURCE.read_bytes() == source_bytes
    print(json.dumps({"panels": len(panels), "transparent": True, "editedCopyUnchanged": True, "sizes": [(p['width'], p['height']) for p in panels]}))


if __name__ == "__main__":
    main()
