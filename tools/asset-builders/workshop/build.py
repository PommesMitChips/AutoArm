"""Prepare the Steam Workshop BBCode and dark-background PNG sources.

Run python docs/steam-workshop/build.py, then node docs/steam-workshop/render.mjs.
PNG rendering needs sharp. --pack-only packages already-rendered files.
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

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop'
DOCS = REPO / 'docs'
NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", NS)

ASSETS = [
    ("assets/models/autoarm/rotor-on-grid.svg", "01-rotor-on-grid", "Rotor mounted on a grid", 1240),
    ("assets/models/autoarm/name-base-terminal.svg", "02-name-base", "Terminal: Arm 1 - Base", 1240),
    ("assets/models/autoarm/arm-front.svg", "03-arm-front", "Complete arm, front view", 1040),
    ("assets/models/autoarm/unsupported-misaligned.svg", "03a-misaligned-stages", "Unsupported misaligned parallel stages", 880),
    ("assets/models/autoarm/unsupported-hydraulic.svg", "03b-hydraulic-linkage", "Unsupported hydraulic linkage", 1000),
    ("assets/models/autoarm/name-head-terminal.svg", "04-name-head", "Terminal: Arm 1 - Head", 1240),
    ("assets/blocks/controller-isometric.svg", "05-controller", "Control seat", 640),
    ("assets/models/autoarm/arm-isometric-pb.svg", "06-arm-and-programmable-block", "Classical arm pose with programmable block", 1240),
    ("assets/models/autoarm/run-terminal.svg", "07-run-on", "Terminal: Argument On and Run", 1240),
]

MATERIALS = {
    "#263e4b": "#294554",  # Dark contours stay distinct against light steel.
    "#e5edf0": "#edf5f8",
    "#b4c5cd": "#c1d2dc",
    "#849da9": "#8eadbf",
    "#496370": "#64879b",
    "#314b59": "#405d70",
    "#e9b34c": "#ffc66b",
    "#c98c30": "#dca047",
    "#58a28d": "#77ceb8",
    "#bddfe1": "#d0f1f5",
}
UI_COLORS = {
    "#d7e9eb": "#eef6fb", "#293941": "#263d4e", "#5b747d": "#547482",
    "#50606a": "#93b5c8", "#50636c": "#93b5c8", "#e9b34c": "#ffc66b",
}


def token(stem):
    return "IMAGE_"+stem.upper().replace("-", "_")


def themed_svg(source, terminal=False):
    root = ET.fromstring(source)
    _, _, w, h = map(float, root.attrib["viewBox"].split())
    if terminal:
        # Remove the full-canvas ground only. Real control/button fills remain.
        for child in list(root):
            if child.tag == f"{{{NS}}}rect" and float(child.get("x", 0)) == 0 and float(child.get("y", 0)) == 0:
                if float(child.get("width", 0)) == w and float(child.get("height", 0)) == h:
                    root.remove(child)
    palette = UI_COLORS if terminal else MATERIALS
    for element in root.iter():
        for name in ("fill", "stroke"):
            value = element.get(name, "").lower()
            if value in palette:
                element.set(name, palette[value])
        if element.tag == f"{{{NS}}}text":
            element.set("fill", "#eef6fb")
        elif not terminal and element.get("stroke", "").lower() == "#9db1bb":
            element.set("stroke", "#9bbfd2")
        elif not terminal and element.tag == f"{{{NS}}}circle" and element.get("fill") == "#64879b":
            element.set("fill", "#d2e6ef")
    # Error marks cross both light steel and dark canvas. A dark contour keeps
    # the bright mark readable over either surface without painting a ground.
    parents = {child: parent for parent in root.iter() for child in parent}
    for element in list(root.iter()):
        if element.get("stroke", "").lower() == "#ad5447":
            contour = copy.deepcopy(element)
            contour.set("stroke", "#1b2838")
            contour.set("stroke-width", str(float(element.get("stroke-width", 1.6))+2.5))
            parent = parents[element]
            parent.insert(list(parent).index(element), contour)
            element.set("stroke", "#ff9a8a")
    return ET.tostring(root, encoding="unicode"), (w, h)


def inline_bb(value):
    value = re.sub(r"\*\*`([^`]+)`\*\*", r"[b]\1[/b]", value)
    value = re.sub(r"`([^`]+)`", r"[b]\1[/b]", value)
    value = re.sub(r"\*\*(.+?)\*\*", r"[b]\1[/b]", value)
    value = re.sub(r"\[([^\]]+)\]\(([^)]+)\)",
                   lambda m: f"[url={remote_url(m[2])}]{m[1]}[/url]", value)
    return value


def remote_url(value):
    return {
        "../README.md": "https://github.com/PommesMitChips/AutoArm#readme",
        "TROUBLESHOOTING.md": "https://github.com/PommesMitChips/AutoArm/blob/main/docs/TROUBLESHOOTING.md",
        "MODULES.md": "https://github.com/PommesMitChips/AutoArm/blob/main/docs/MODULES.md",
    }.get(value, value)


def convert_markdown(source):
    images = {path: token(stem) for path, stem, _, _ in ASSETS}
    output = []
    ordered = False
    table = False
    for line in source.splitlines():
        # This authoring/resource sentence belongs to the local guide only.
        if line.startswith("The [illustrated HTML guide]"):
            continue
        number = re.match(r"^\d+\.\s+(.+)$", line)
        if ordered and not number:
            output.append("[/olist]")
            ordered = False
        if table and not line.startswith("|"):
            output.append("[/list]")
            table = False
        if line.startswith("|"):
            cells = [c.strip() for c in line.strip("|").split("|")]
            if cells[0] == "Key":
                output.append("[list]")
                table = True
            elif not set(cells[0]) <= {"-", " ", ":"}:
                output.append(f"[*][b]{cells[0]}[/b] — {cells[1]}")
            continue
        heading = re.match(r"^(#{1,3})\s+(.+)$", line)
        image = re.fullmatch(r"!\[.*\]\(([^)]+)\)", line)
        if heading:
            level = len(heading[1])
            output.append(f"[h{level}]{inline_bb(heading[2])}[/h{level}]")
        elif image:
            output.append("[img]{{"+images[image[1]]+"}}[/img]")
        elif number:
            if not ordered:
                output.append("[olist]")
                ordered = True
            output.append("[*]"+inline_bb(number[1]))
        else:
            output.append(inline_bb(line))
    if ordered:
        output.append("[/olist]")
    if table:
        output.append("[/list]")
    return re.sub(r"\n{3,}", "\n\n", "\n".join(output)).strip()+"\n"


def preview_inline(value):
    value = html.escape(value)
    value = re.sub(r"\[b\](.*?)\[/b\]", r"<strong>\1</strong>", value)
    value = re.sub(r"\[i\](.*?)\[/i\]", r"<em>\1</em>", value)
    value = re.sub(r"\[url=([^\]]+)\](.*?)\[/url\]",
                   r'<a href="\1" target="_blank" rel="noopener">\2</a>', value)
    return value


def preview_body(bbcode, assets):
    output, paragraph = [], []
    lookup = {a["placeholder"]: a for a in assets}
    def flush():
        if paragraph:
            output.append("<p>"+preview_inline(" ".join(paragraph))+"</p>")
            paragraph.clear()
    for line in bbcode.splitlines():
        heading = re.fullmatch(r"\[h([123])\](.*?)\[/h\1\]", line)
        image = re.fullmatch(r"\[img\]\{\{([^}]+)\}\}\[/img\]", line)
        if not line:
            flush()
        elif heading:
            flush()
            output.append(f"<h{heading[1]}>"+preview_inline(heading[2])+f"</h{heading[1]}>")
        elif image:
            flush()
            item = lookup[image[1]]
            extra = " controller" if item["filename"].startswith("05-") else ""
            output.append(f'<figure class="image{extra}"><img src="png/{item["filename"]}" '
                          f'width="{item["width"]}" height="{item["height"]}" alt="{html.escape(item["title"])}"></figure>')
        elif line in ("[olist]", "[list]", "[/olist]", "[/list]"):
            flush()
            output.append({"[olist]": "<ol>", "[list]": "<ul>", "[/olist]": "</ol>", "[/list]": "</ul>"}[line])
        elif line.startswith("[*]"):
            flush()
            output.append("<li>"+preview_inline(line[3:])+"</li>")
        else:
            paragraph.append(line)
    flush()
    return "\n".join(output)


def make_preview(bbcode, assets):
    body = preview_body(bbcode, assets)
    return '''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>AutoArm — Steam Workshop preview</title>
<style>
*{box-sizing:border-box}html{color-scheme:dark;scrollbar-gutter:stable}body{margin:0;background:#171a21;color:#c6d4df;font:15px/1.6 Arial,sans-serif}
a{color:#9bc7df;text-underline-offset:4px}a:hover{color:#e8f4fb}a:focus-visible,button:focus-visible,summary:focus-visible,textarea:focus-visible{outline:2px solid #ffc66b;outline-offset:4px}
::selection{background:#50758c;color:#fff}.tools{width:min(900px,calc(100% - 32px));margin:28px auto 22px}.tools p{font-size:13px;max-width:70ch;margin:0 0 14px}
.actions{display:flex;gap:18px;flex-wrap:wrap;align-items:center}.actions a{font-size:13px}button{font:inherit;font-size:13px;padding:8px 14px;border:1px solid #597a90;border-radius:3px;background:#29465b;color:#eef6fb;cursor:pointer}button:hover{background:#385d76}
details{margin-top:18px}summary{cursor:pointer;font-size:13px}textarea{display:block;width:100%;min-height:320px;margin-top:12px;padding:14px;background:#101923;border:1px solid #597a90;color:#c6d4df;font:13px/1.6 Consolas,monospace;resize:vertical;caret-color:#ffc66b}
.workshop-description{width:min(660px,calc(100% - 32px));margin:24px auto 50px;padding:24px 20px 32px;background:#1b2838;overflow-wrap:anywhere}
h1,h2,h3{color:#fff;line-height:1.35}h1{font-size:26px;margin:0 0 22px}h2{font-size:22px;margin:38px 0 16px}h3{font-size:18px;margin:28px 0 14px}p{margin:0 0 16px}strong{color:#eef6fb}ol,ul{padding-left:26px;margin:0 0 18px}li{margin-bottom:7px}
.image{margin:22px 0 30px}.image img{display:block;width:100%;height:auto;max-width:100%}.controller img{max-width:360px;margin:auto}
footer{width:min(660px,calc(100% - 32px));margin:0 auto 32px;color:#9bb2c1;font-size:12px}
@media(max-width:480px){.workshop-description{width:100%;padding:22px 16px}h1{font-size:24px}h2{font-size:21px}}
</style></head><body>
<section class="tools" aria-label="Workshop export tools">
<p>Steam Workshop layout preview. The images are transparent PNGs. Upload them and replace the nine image URL placeholders before pasting the BBCode into your Workshop description.</p>
<div class="actions"><button id="copy" type="button">Copy BBCode template</button><a href="AutoArm-Steam-Workshop.zip" download>Download Workshop pack</a><a href="getting-started.bbcode.txt" download>Download BBCode</a></div>
<p id="status" role="status"></p><details><summary>View BBCode template</summary><textarea id="bbcode" readonly aria-label="Steam Workshop BBCode template">'''+html.escape(bbcode)+'''</textarea></details>
</section><article class="workshop-description">'''+body+'''</article>
<footer>Local layout preview. Steam supplies its own text styling; the PNGs shown here are the exported files.</footer>
<script>document.getElementById('copy').addEventListener('click',async()=>{const source=document.getElementById('bbcode');const status=document.getElementById('status');try{await navigator.clipboard.writeText(source.value);status.textContent='BBCode template copied. Replace the image URL placeholders after uploading the PNGs.';}catch{source.closest('details').open=true;source.focus();source.select();status.textContent='Select and copy the BBCode below, then replace the image URL placeholders.';}});</script>
</body></html>'''


def pack():
    manifest = json.loads((ROOT/"manifest.json").read_text(encoding="utf-8"))
    bbcode = (ROOT/"getting-started.bbcode.txt").read_text(encoding="utf-8")
    (ROOT/"preview.html").write_text(make_preview(bbcode, manifest["images"]), encoding="utf-8")
    files = [ROOT/name for name in ("getting-started.bbcode.txt", "image-urls.json", "README.md", "manifest.json", "preview.html", "resolve_urls.py")]
    files += sorted((ROOT/"png").glob("*.png"))
    if len(files) != 15:
        raise RuntimeError("Expected nine PNGs and six support files before packaging.")
    with zipfile.ZipFile(ROOT/"AutoArm-Steam-Workshop.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for file in files:
            archive.write(file, file.relative_to(ROOT))
    print("Packaged BBCode, nine transparent PNGs, preview and image URL helper.")


def main():
    if "--pack-only" in sys.argv:
        pack()
        return
    (ROOT/".render").mkdir(parents=True, exist_ok=True)
    (ROOT/"png").mkdir(exist_ok=True)
    records = []
    for path, stem, title, width in ASSETS:
        source = (DOCS/path).read_text(encoding="utf-8")
        terminal = "terminal.svg" in path
        vector, (sw, sh) = themed_svg(source, terminal)
        (ROOT/".render"/(stem+".svg")).write_text(vector, encoding="utf-8")
        records.append({"filename": stem+".png", "source": path, "title": title,
                        "placeholder": token(stem), "width": width, "height": round(sh*width/sw),
                        "transparent": True, "sourceSha256": hashlib.sha256(source.encode()).hexdigest()})
    markdown = (DOCS/"GETTING_STARTED.md").read_text(encoding="utf-8")
    bbcode = convert_markdown(markdown)
    (ROOT/"getting-started.bbcode.txt").write_text(bbcode, encoding="utf-8")
    (ROOT/"manifest.json").write_text(json.dumps({"version": 1, "backgroundsChecked": ["#1b2838", "#171a21"],
                                                "images": records}, indent=2)+"\n", encoding="utf-8")
    url_file = ROOT/"image-urls.json"
    if not url_file.exists():
        url_file.write_text(json.dumps({a["placeholder"]: "" for a in records}, indent=2)+"\n", encoding="utf-8")
    (ROOT/"preview.html").write_text(make_preview(bbcode, records), encoding="utf-8")
    print("Prepared Steam BBCode, preview and nine transparent vector render sources.")


if __name__ == "__main__":
    main()
