"""Validate the editable export and prepare an ignored browser comparison page."""
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop/svg-panels'
WORKSHOP = ROOT.parent
DOCS = REPO / "docs"
SVG = "http://www.w3.org/2000/svg"
ET.register_namespace("", SVG)
manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
rows = []
prefixes = {"autoarm-guide": "panels", "toolswap-guide": "tool-swap/panels", "commands": "commands/panels"}

for index, panel in enumerate(manifest["panels"]):
    path = ROOT / panel["folder"] / panel["filename"]
    root = ET.parse(path).getroot()
    assert not list(root.iter("{" + SVG + "}image"))
    assert not list(root.iter("{" + SVG + "}foreignObject"))
    assert list(root.iter("{" + SVG + "}text"))
    ids = [node.get("id") for node in root.iter() if node.get("id")]
    assert len(ids) == len(set(ids)), path
    png = WORKSHOP / prefixes[panel["folder"]] / path.with_suffix(".png").name
    with Image.open(png) as image:
        assert image.size == (panel["width"], panel["height"]), path
    # Prefix IDs only in the temporary inline comparison page.
    mapping = {identifier: "proof" + str(index) + "-" + identifier for identifier in ids}
    for node in root.iter():
        if node.get("id"):
            node.set("id", mapping[node.get("id")])
        for key, value in list(node.attrib.items()):
            if key == "id":
                continue
            value = re.sub(r"url\(#([^)]*)\)", lambda m: "url(#" + mapping.get(m[1], m[1]) + ")", value)
            if value.startswith("#") and value[1:] in mapping:
                value = "#" + mapping[value[1:]]
            if key in ("aria-labelledby", "aria-describedby"):
                value = " ".join(mapping.get(word, word) for word in value.split())
            node.set(key, value)
    root.set("data-file", str(path.relative_to(ROOT)).replace("\\", "/"))
    vector = ET.tostring(root, encoding="unicode")
    source = "/" + str(png.relative_to(REPO)).replace("\\", "/")
    rows.append('<section><p>SVG · ' + panel["filename"] + '</p><p>Original PNG</p><div>' + vector + '</div><img src="' + source + '"></section>')

missing_source_inputs = []
for relative, expected in manifest["sourceHashes"].items():
    path = REPO / relative
    if not path.exists():
        missing_source_inputs.append(relative)
        continue
    assert hashlib.sha256(path.read_bytes()).hexdigest() == expected
with zipfile.ZipFile(WORKSHOP / "AutoArm-Editable-SVG-Panels.zip") as archive:
    assert archive.testzip() is None
    assert len([name for name in archive.namelist() if name.endswith(".svg")]) == len(manifest["panels"]) + len(manifest.get("components", []))

proof = REPO / "docs/assets/review/svg-panel-review.html"
proof.parent.mkdir(parents=True, exist_ok=True)
proof.write_text('<!doctype html><meta charset="utf-8"><title>SVG export comparison</title><style>body{margin:0;background:#1b2838;color:#dce7ed;font:14px sans-serif}section{display:grid;grid-template-columns:620px 620px;gap:12px;margin:20px 0}p{margin:8px 24px}svg,img{display:block;width:620px;height:auto}div{min-width:0}</style>' + ''.join(rows), encoding="utf-8")
print(json.dumps({"panels": len(manifest["panels"]), "rasterImages": 0, "editableText": True, "uniqueIds": True, "dimensionsMatchPNGs": True, "copySourcesUnchanged": not missing_source_inputs, "missingHistoricalInputs": missing_source_inputs, "zipVerified": True, "proofPage": str(proof)}))
