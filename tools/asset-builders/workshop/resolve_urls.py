"""Replace image placeholders after uploading the PNG files.

python resolve_urls.py [image-urls.json] [output.bbcode.txt]
"""
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlsplit

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop'
mapping = Path(sys.argv[1]) if len(sys.argv)>1 else REPO/'docs/steam-workshop/image-urls.json'
output = Path(sys.argv[2]) if len(sys.argv)>2 else ROOT/"getting-started.ready.bbcode.txt"
values = json.loads(mapping.read_text(encoding="utf-8"))
source = (REPO/'docs/steam-workshop/getting-started.bbcode.txt').read_text(encoding="utf-8")
tokens = re.findall(r"\{\{(IMAGE_[A-Z0-9_]+)\}\}", source)
missing = [key for key in tokens if not values.get(key)]
if missing:
    raise SystemExit("Add direct HTTPS image URLs for: "+", ".join(missing))
for key in tokens:
    value=values[key]
    parsed=urlsplit(value)
    if parsed.scheme!="https" or not parsed.netloc or any(c.isspace() or c in "[]<>\"'" for c in value):
        raise SystemExit("Invalid direct HTTPS image URL for "+key)
    source=source.replace("{{"+key+"}}",value)
output.write_text(source,encoding="utf-8")
print("Ready BBCode written to "+str(output))
