"""Load existing native SVG art for editable panel composition.

Public API: load_vector_asset(path, prefix=None) -> ElementTree.Element.
The returned root stays an SVG with its original viewBox and drawing order.
Terminal text is restored from the source's known outlined Oxanium labels;
all other vectors retain their original coordinates and styling.
"""
from itertools import count
from pathlib import Path
import re
import xml.etree.ElementTree as ET

SVG_NS = "http://www.w3.org/2000/svg"
XLINK_NS = "http://www.w3.org/1999/xlink"
XML_NS = "http://www.w3.org/XML/1998/namespace"
_DEFAULT_PREFIXES = count(1)
_NUMBER = r"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?"
_OUTLINED_LABEL = re.compile(
    rf"\s*translate\(\s*({_NUMBER})[\s,]+({_NUMBER})\s*\)"
    rf"\s*scale\(\s*({_NUMBER})[\s,]+({_NUMBER})\s*\)\s*"
)
_DRAWABLE = {"polygon", "polyline", "path", "rect", "line", "circle", "ellipse"}


def svg_tag(name):
    return f"{{{SVG_NS}}}{name}"


def local_name(tag):
    return tag.rsplit("}", 1)[-1]


def _slug(value):
    result = re.sub(r"[^A-Za-z0-9_.-]+", "-", str(value)).strip("-.")
    if not result or not re.match(r"[A-Za-z_]", result):
        result = "asset-" + result
    return result


def restore_terminal_text(root):
    """Replace only the known glyph-outline groups, in their same position.

    outline_font() placed each glyph by its unkerned advance in a 1000-unit
    Oxanium font. Its translate already includes centring, so the restored
    string uses that exact left/start x coordinate, with no text-anchor shift.
    The input SVG element is modified in memory; files are never written.
    """
    restored = 0
    for parent in list(root.iter()):
        for index, node in enumerate(list(parent)):
            if local_name(node.tag) != "g" or "aria-label" not in node.attrib:
                continue
            match = _OUTLINED_LABEL.fullmatch(node.get("transform", ""))
            if not match or not len(node) or not all(local_name(c.tag) == "path" for c in node):
                continue
            x, y, scale_x, scale_y = map(float, match.groups())
            if scale_x <= 0 or abs(scale_x + scale_y) > 1e-10:
                continue
            # Keep non-transform presentation and accessible attributes. Fill
            # may be inherited by some copied assets; do not invent a colour.
            attrs = {k: v for k, v in node.attrib.items() if k != "transform"}
            attrs.update({"x": f"{x:g}", "y": f"{y:g}", "font-family": "Oxanium",
                          "font-weight": "500", "font-size": f"{abs(scale_x)*1000:g}",
                          "text-anchor": "start", "font-kerning": "none", "kerning": "0",
                          "font-variant-ligatures": "none", "letter-spacing": "0",
                          "style": "font-kerning:none;font-variant-ligatures:none;font-feature-settings:'kern' 0,'liga' 0,'clig' 0;"})
            # Preserve any unrelated inline style while enforcing the source's
            # unkerned glyph placement. The final declarations intentionally win.
            if node.get("style"):
                attrs["style"] = node.get("style").rstrip(";") + ";" + attrs["style"]
            attrs[f"{{{XML_NS}}}space"] = "preserve"
            text = ET.Element(svg_tag("text"), attrs)
            text.text = node.get("aria-label")
            text.tail = node.tail
            parent.remove(node)
            parent.insert(index, text)
            restored += 1
    return restored


def normalise_font_families(root):
    """Use bundled Inter/Oxanium rather than machine-dependent UI fallbacks."""
    for node in root.iter():
        family = node.get("font-family")
        if family and ("inter" in family.lower() or "segoe" in family.lower()):
            node.set("font-family", "Inter")
        style = node.get("style")
        if style:
            def family_rule(match):
                family = match.group(2)
                return match.group(1) + ("Inter" if "inter" in family.lower() or "segoe" in family.lower() else family)
            node.set("style", re.sub(r"(font-family\s*:\s*)([^;]+)", family_rule, style, flags=re.I))
    return root


def prefix_svg_ids(root, prefix):
    """Make title/desc, layer and paint-server IDs safe for repeated inlining."""
    prefix = _slug(prefix)
    mapping = {}
    duplicates = {}
    for node in root.iter():
        old = node.get("id")
        if old is None:
            continue
        number = duplicates.get(old, 0) + 1
        duplicates[old] = number
        new = f"{prefix}-{_slug(old)}" + (f"-{number}" if number > 1 else "")
        mapping.setdefault(old, new)
        node.set("id", new)
    for node in root.iter():
        for attribute, value in list(node.attrib.items()):
            if attribute == "id":
                continue
            if attribute in ("aria-labelledby", "aria-describedby"):
                node.set(attribute, " ".join(mapping.get(ref, ref) for ref in value.split()))
            elif attribute in ("href", f"{{{XLINK_NS}}}href") and value.startswith("#"):
                node.set(attribute, "#" + mapping.get(value[1:], value[1:]))
            elif "url(" in value:
                node.set(attribute, re.sub(r"url\(\s*(['\"]?)#([^)'\"\s]+)\1\s*\)",
                                          lambda m: "url(#" + mapping.get(m[2], m[2]) + ")", value))
        if local_name(node.tag) == "style" and node.text:
            for old, new in mapping.items():
                node.text = re.sub(r"#" + re.escape(old) + r"(?![A-Za-z0-9_.-])", "#" + new, node.text)
    return mapping


def group_artwork_runs(root, prefix):
    """Wrap consecutive same-role primitives without changing BSP paint order.

    A component can be split across many depths. Gathering all its faces into
    one layer would reintroduce the previous occlusion bug. Each contiguous run
    gets an editable named subgroup, while the flattened primitive order stays
    exactly the same as the input.
    """
    serial = count(1)
    prefix = _slug(prefix)

    def key(node):
        if local_name(node.tag) not in _DRAWABLE:
            return None
        role = node.get("data-role") or node.get("data-component") or node.get("data-part")
        return (role, node.get("data-component")) if role else None

    def wrap(parent):
        for node in list(parent):
            if local_name(node.tag) in ("g", "svg"):
                wrap(node)
        nodes = list(parent)
        result = []
        active_key = None
        active_group = None
        for node in nodes:
            node_key = key(node)
            if node_key is None:
                active_key = active_group = None
                result.append(node)
                continue
            if node_key != active_key:
                role, component = node_key
                attrs = {"id": f"{prefix}-layer-{_slug(role)}-{next(serial):04d}",
                         "aria-label": role.replace("-", " "), "data-role": role,
                         "data-paint-order": "consecutive-surface-run"}
                if component:
                    attrs["data-component"] = component
                active_group = ET.Element(svg_tag("g"), attrs)
                result.append(active_group)
                active_key = node_key
            active_group.append(node)
        parent[:] = result
    wrap(root)
    return root


def load_vector_asset(path, prefix=None):
    """Return an editable native SVG root, ready to inline in an SVG panel.

    ``path`` accepts str or pathlib.Path. Pass a unique ``prefix`` per placed
    asset for repeatable document IDs. If omitted, a unique session prefix is
    generated automatically. No raster conversion, outlining or file writes
    occur, and input images are rejected rather than embedded.
    """
    path = Path(path)
    root = ET.parse(path).getroot()
    if local_name(root.tag) != "svg":
        raise ValueError(f"Expected an SVG asset: {path}")
    if any(local_name(node.tag) in ("image", "foreignObject") for node in root.iter()):
        raise ValueError(f"Editable vector asset contains raster or foreign content: {path}")
    if prefix is None:
        prefix = f"{_slug(path.stem)}-{next(_DEFAULT_PREFIXES):04d}"
    prefix = _slug(prefix)
    restored = restore_terminal_text(root)
    normalise_font_families(root)
    prefix_svg_ids(root, prefix)
    group_artwork_runs(root, prefix)
    root.set("data-vector-asset", path.name)
    root.set("data-restored-text-labels", str(restored))
    return root
