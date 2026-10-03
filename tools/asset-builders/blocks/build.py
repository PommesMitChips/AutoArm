"""Build AutoArm's diagram SVGs using only the Python standard library.

Both views project the same small geometric models. These are schematic
illustrations, not dimensionally exact copies of the Space Engineers meshes.
Run: python docs/assets/blocks/build.py
"""
from __future__ import annotations

import html
import json
import math
from pathlib import Path
import zipfile

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/blocks'
PALETTE = {
    "ink": "#263e4b",
    "light": "#e5edf0",
    "steel": "#b4c5cd",
    "shade": "#849da9",
    "dark": "#496370",
    "recess": "#314b59",
    "amber": "#e9b34c",
    "amberShade": "#c98c30",
    "signal": "#58a28d",
    "display": "#bddfe1",
}


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def unit(v):
    length = math.sqrt(dot(v, v))
    return tuple(x / length for x in v)


class Model:
    def __init__(self, name, description, anchors):
        self.name = name
        self.description = description
        self.anchors = anchors
        self.surfaces = []

    def face(self, points, normal, material="steel", part="base", edge=True):
        self.surfaces.append({"points": points, "normal": unit(normal),
                              "material": material, "part": part, "edge": edge})

    def line(self, points, normal, part="detail", color="ink", width=1.4):
        self.surfaces.append({"points": points, "normal": unit(normal),
                              "material": color, "part": part, "line": width})

    def prism(self, footprint, lo, hi, material="steel", part="base", edge=True):
        self.face([(x, hi, z) for x, z in footprint], (0, 1, 0), material, part, edge)
        self.face([(x, lo, z) for x, z in footprint], (0, -1, 0), material, part, edge)
        for i, (x, z) in enumerate(footprint):
            xx, zz = footprint[(i + 1) % len(footprint)]
            self.face([(x, lo, z), (xx, lo, zz), (xx, hi, zz), (x, hi, z)],
                      (zz - z, 0, x - xx), material, part, edge)

    def box(self, x, y, z, w, h, d, material="steel", part="base"):
        self.prism([(x-w/2, z-d/2), (x+w/2, z-d/2),
                    (x+w/2, z+d/2), (x-w/2, z+d/2)], y, y+h, material, part)

    def bevel(self, y, h, w=64, d=64, cut=7, material="steel", part="base"):
        x, z = w/2, d/2
        self.prism([(-x+cut, -z), (x-cut, -z), (x, -z+cut),
                    (x, z-cut), (x-cut, z), (-x+cut, z),
                    (-x, z-cut), (-x, -z+cut)], y, y+h, material, part)

    def cylinder_y(self, x, z, lo, hi, r, material="steel", part="base", top_r=None):
        n = 48
        band = len(self.surfaces)
        rr = r if top_r is None else top_r
        low = [(x+r*math.cos(t*2*math.pi/n), lo, z+r*math.sin(t*2*math.pi/n)) for t in range(n)]
        high = [(x+rr*math.cos(t*2*math.pi/n), hi, z+rr*math.sin(t*2*math.pi/n)) for t in range(n)]
        self.face(high, (0, 1, 0), material, part)
        self.face(low, (0, -1, 0), material, part)
        for i in range(n):
            j = (i+1) % n
            angle = (i+.5)*2*math.pi/n
            normal = (math.cos(angle), (r-rr)/(hi-lo), math.sin(angle))
            self.face([low[i], low[j], high[j], high[i]], normal, material, part, False)
            self.surfaces[-1].update(band=band, segment=i, segments=n)
            self.line([low[i], low[j]], normal, part, width=1.8)
        # Silhouette lines are added by the renderer for the current camera.
        self.surfaces.append({"cylinder": (x, z, lo, hi, r, rr), "part": part})

    def cylinder_z(self, x, y, lo, hi, r, material="steel", part="base"):
        n = 64
        band = len(self.surfaces)
        a = [(x+r*math.cos(i*2*math.pi/n), y+r*math.sin(i*2*math.pi/n), lo) for i in range(n)]
        b = [(xx, yy, hi) for xx, yy, _ in a]
        self.face(a, (0, 0, -1), material, part)
        self.face(b, (0, 0, 1), material, part)
        for i in range(n):
            j = (i+1) % n
            t = (i+.5)*2*math.pi/n
            self.face([a[i], a[j], b[j], b[i]], (math.cos(t), math.sin(t), 0), material, part, False)
            self.surfaces[-1].update(band=band, segment=i, segments=n)

    def panel_z(self, x, y, z, w, h, material="recess", part="detail"):
        self.face([(x-w/2, y-h/2, z), (x+w/2, y-h/2, z),
                   (x+w/2, y+h/2, z), (x-w/2, y+h/2, z)],
                  (0, 0, 1), material, part)
        self.surfaces[-1]["decal"] = True

    def panel_x(self, x, y, z, w, h, material="recess", part="detail"):
        self.face([(x, y-h/2, z-w/2), (x, y-h/2, z+w/2),
                   (x, y+h/2, z+w/2), (x, y+h/2, z-w/2)],
                  (1, 0, 0), material, part)
        self.surfaces[-1]["decal"] = True

    def ports(self, y, z=32.15, x=32.15, size=28):
        self.panel_z(0, y, z, size, size, "dark")
        self.panel_z(0, y, z+.1, size-9, size-9, "recess")
        self.panel_x(x, y, 0, size, size, "dark")
        self.panel_x(x+.1, y, 0, size-9, size-9, "recess")
        for v in (-6, 0, 6):
            self.line([(-8, y+v, z+.2), (8, y+v, z+.2)], (0, 0, 1), color="shade")
            self.line([(x+.2, y+v, -8), (x+.2, y+v, 8)], (1, 0, 0), color="shade")


def rotor():
    m = Model("Rotor", "Standard rotor with a round moving head, exposed spindle, ribbed stator and mounting foot.",
              {"mount": (0, 0, 0), "head": (0, 89, 0), "axis": (0, 57, 0)})
    m.cylinder_y(0, 0, 0, 9, 31, "dark")
    m.cylinder_y(0, 0, 9, 46, 25, "recess")
    for xx, zz in [(-24, -24), (24, -24), (24, 24), (-24, 24)]:
        m.box(xx, 5, zz, 12, 42, 12, "steel")
    m.cylinder_y(0, 0, 42, 54, 30)
    m.cylinder_y(0, 0, 54, 70, 13, "dark", "head")
    m.cylinder_y(0, 0, 68, 73, 23, "shade", "head")
    m.cylinder_y(0, 0, 73, 86, 32, "steel", "head")
    m.cylinder_y(0, 0, 86, 89, 25, "dark", "head")
    m.cylinder_y(0, 0, 89, 90, 14, "shade", "head")
    m.panel_z(24, 18, 30.15, 5, 9, "amber")
    return m


def hinge():
    m = Model("Hinge", "Hinge at its straight pose, with a forked base, transverse circular pivot and moving top plate.",
              {"mount": (0, 0, 0), "head": (0, 88, 0), "axis": (0, 46, 0)})
    m.bevel(0, 14)
    # The two stationary ears leave the central moving knuckle visible.
    for zz in (-25, 25):
        m.box(0, 14, zz, 56, 32, 14, "steel")
        m.cylinder_z(0, 46, zz-7, zz+7, 28, "steel")
    m.cylinder_z(0, 46, -17, 17, 23, "dark", "head")
    m.box(0, 46, 0, 32, 29, 30, "shade", "head")
    m.bevel(75, 13, w=64, d=52, cut=5, part="head")
    m.cylinder_z(0, 46, 32.15, 33.15, 20, "shade", "hardware")
    m.cylinder_z(0, 46, 33.3, 34.1, 12, "steel", "hardware")
    m.cylinder_z(0, 46, 34.2, 35, 4, "dark", "hardware")
    for angle in range(0, 360, 90):
        a = math.radians(angle+45)
        m.cylinder_z(15.8*math.cos(a), 46+15.8*math.sin(a), 34.2, 34.7, 1.7, "dark", "hardware")
    m.panel_x(28.2, 30, 25, 7, 18, "recess")
    m.panel_x(28.35, 37, 25, 5, 3, "amber")
    return m


def piston():
    m = Model("Piston", "Partly extended piston, with a rectangular base, telescoping stages and separate moving head plate.",
              {"mount": (0, 0, 0), "head": (0, 208, 0), "axis": (0, 126, 0)})
    m.bevel(0, 10, material="shade")
    m.bevel(10, 110)
    m.panel_z(0, 58, 32.15, 40, 76, "dark")
    m.panel_z(0, 58, 32.3, 24, 65, "shade")
    for xx in (-25, 25):
        m.box(xx, 12, 32, 8, 100, 5, "light", "hardware")
    for yy in (27, 37, 47):
        m.line([(-10, yy, 32.6), (10, yy, 32.6)], (0, 0, 1), color="recess", width=2)
    m.ports(58)
    m.bevel(115, 13, material="shade")
    m.cylinder_y(0, 0, 128, 171, 22, "light", "shaft")
    m.cylinder_y(0, 0, 164, 175, 24, "dark", "shaft")
    m.cylinder_y(0, 0, 175, 195, 15, "light", "shaft")
    m.bevel(195, 13, material="steel", part="head")
    m.panel_z(0, 122, 32.2, 33, 5, "amber")
    return m


def drill():
    m = Model("Drill", "Ship drill with a conveyor housing, protective shoulders and three toothed cutting drums.",
              {"mount": (0, 0, 0), "tip": (0, 157, 0), "axis": (0, 110, 0)})
    m.bevel(0, 58)
    m.ports(27)
    m.bevel(54, 15, material="shade")
    m.cylinder_y(0, 0, 69, 90, 24, "dark", "drive")
    m.bevel(90, 18, w=88, d=76, cut=13, part="guard")
    m.panel_z(0, 99, 38.2, 42, 7, "amber")
    m.panel_x(44.2, 99, 0, 33, 7, "amber")
    for xx, zz in [(-22, -13), (22, -13), (0, 23)]:
        m.cylinder_y(xx, zz, 108, 116, 18, "dark", "cutter")
        m.cylinder_y(xx, zz, 116, 142, 18, "shade", "cutter")
        m.cylinder_y(xx, zz, 142, 149, 19, "dark", "cutter")
        m.cylinder_y(xx, zz, 149, 156, 12, "steel", "cutter", top_r=8)
        for angle in range(0, 360, 60):
            a = math.radians(angle)
            m.cylinder_y(xx+16*math.cos(a), zz+16*math.sin(a), 137, 153, 3.8,
                         "steel", "teeth", top_r=1.7)
    return m


def welder():
    m = Model("Welder", "Ship welder with a conveyor housing, paired reservoir cylinders and two tapered welding electrodes.",
              {"mount": (0, 0, 0), "tip": (0, 145, 0), "axis": (0, 80, 0)})
    m.bevel(0, 64)
    m.panel_x(32.2, 31, 0, 35, 41, "shade")
    m.panel_x(32.35, 31, 0, 20, 27, "recess")
    # Two visible reservoirs and two distinct working tips are the signature.
    for xx in (-19, 19):
        m.cylinder_y(xx, 33, 11, 50, 8.5, "light", "reservoir")
        m.cylinder_y(xx, 33, 8, 15, 10, "dark", "reservoir")
        m.cylinder_y(xx, 33, 48, 56, 10, "shade", "reservoir")
        m.cylinder_y(xx, 14, 64, 75, 12, "dark", "electrode")
        m.cylinder_y(xx, 14, 75, 106, 8.5, "steel", "electrode")
        m.cylinder_y(xx, 14, 103, 111, 9, "amber", "electrode")
        m.cylinder_y(xx, 14, 111, 130, 5, "light", "electrode")
        m.cylinder_y(xx, 14, 130, 145, 5, "dark", "electrode", top_r=.8)
    m.box(0, 66, -15, 54, 8, 16, "shade", "brace")
    m.panel_z(0, 30, 32.2, 11, 34, "recess")
    m.panel_z(0, 42, 32.4, 5, 8, "signal")
    return m


def block():
    m = Model("Armor block", "Generic light armor cube with shallow panel seams; a neutral structural part for arm diagrams.",
              {"mount": (0, 0, 0), "top": (0, 64, 0)})
    m.box(0, 0, 0, 64, 64, 64)
    m.panel_z(0, 32, 32.15, 50, 50, "steel")
    m.panel_x(32.15, 32, 0, 50, 50, "steel")
    m.face([(-25, 64.15, -25), (25, 64.15, -25), (25, 64.15, 25), (-25, 64.15, 25)],
           (0, 1, 0), "steel", "panel")
    for xx in (-21, 21):
        for yy in (11, 53):
            m.cylinder_z(xx, yy, 32.3, 32.6, 1.1, "shade", "fastener")
    return m


def programmable_block():
    m = Model("Programmable block", "Vanilla programmable block with a chamfered housing, inset display, lower controls and side vents.",
              {"mount": (0, 0, 0), "top": (0, 64, 0)})
    m.bevel(0, 7, material="dark")
    m.bevel(7, 57, cut=7)
    m.panel_z(0, 41, 32.15, 43, 35, "shade", "console")
    m.panel_z(0, 42, 32.3, 33, 26, "recess", "screen")
    m.panel_z(0, 43, 32.45, 27, 20, "display", "screen")
    m.panel_z(0, 18, 32.2, 27, 10, "recess", "controls")
    for xx in (-8, 0, 8):
        m.panel_z(xx, 18, 32.35, 4, 3.5, "light", "controls")
    m.panel_z(19, 19, 32.35, 3, 5, "signal", "indicator")
    m.panel_x(32.15, 34, 0, 38, 42, "shade", "vent")
    m.panel_x(32.3, 34, 0, 24, 31, "recess", "vent")
    for yy in (24, 29, 34, 39, 44):
        m.line([(32.45, yy, -8), (32.45, yy, 8)], (1, 0, 0), color="shade", width=1.4)
        m.surfaces[-1]["decal"] = True
    for xx in (-25, 25):
        for yy in (10, 60):
            m.cylinder_z(xx, yy, 32.4, 32.7, 1.5, "dark", "fastener")
    m.face([(-18, 64.15, -17), (18, 64.15, -17), (18, 64.15, 17), (-18, 64.15, 17)],
           (0, 1, 0), "shade", "panel")
    return m


def controller():
    m = Model("Control seat", "Open ship controller with a console screen, seat, backrest, armrests and mounting pedestal.",
              {"mount": (0, 0, 0), "top": (0, 100, 0)})
    m.bevel(0, 9, w=70, d=84, cut=9, material="dark")
    m.box(0, 9, 1, 38, 16, 46, "shade", "pedestal")
    m.box(0, 25, -12, 47, 12, 43, "steel", "seat")
    m.box(0, 37, -12, 37, 5, 33, "dark", "seat")
    m.box(0, 36, -31, 46, 52, 12, "steel", "backrest")
    m.panel_z(0, 63, -24.7, 35, 39, "dark", "backrest")
    for xx in (-29, 29):
        m.box(xx, 25, -6, 9, 21, 27, "shade", "armrest")
        m.box(xx, 46, -6, 12, 7, 34, "steel", "armrest")
    m.box(0, 10, 32, 12, 46, 12, "dark", "console")
    m.box(0, 48, 32, 57, 10, 24, "shade", "console")
    m.box(0, 60, 36, 8, 18, 7, "shade", "screen")
    m.box(0, 77, 36, 62, 42, 9, "steel", "screen")
    m.panel_z(0, 98, 40.65, 52, 32, "recess", "screen")
    m.panel_z(0, 98, 40.8, 46, 26, "display", "screen")
    return m


def shade(material, normal):
    if material == "steel":
        if normal[1] > .55:
            return "light"
        return "steel" if normal[2] >= normal[0] else "shade"
    if material == "amber" and normal[0] > normal[2]:
        return "amberShade"
    return material


def projection(p, view):
    x, y, z = p
    if view == "side":
        return x, -y
    return math.sqrt(3)/2*(x-z), .5*(x+z)-y


def render(model, view):
    camera = (0, 0, 1) if view == "side" else (1, 1, 1)
    surfaces = []
    for surface in model.surfaces:
        if "cylinder" in surface:
            x, z, lo, hi, r, rr = surface["cylinder"]
            angle = math.atan2(camera[2], camera[0])
            for t in (angle-math.pi/2, angle+math.pi/2):
                surfaces.append({"points": [(x+r*math.cos(t), lo, z+r*math.sin(t)),
                                             (x+rr*math.cos(t), hi, z+rr*math.sin(t))],
                                 "normal": unit(camera), "material": "ink", "line": 2.2,
                                 "part": surface["part"]})
        elif dot(surface["normal"], camera) > .001:
            surfaces.append(surface)
    # Join adjacent pieces of curved walls into uninterrupted vector surfaces.
    # This removes tessellation seams while preserving exact projected contours.
    bands = {}
    plain = []
    for surface in surfaces:
        if "band" in surface:
            bands.setdefault(surface["band"], []).append(surface)
        else:
            plain.append(surface)
    for band in bands.values():
        band.sort(key=lambda s: s["segment"])
        n = band[0]["segments"]
        gap = next((i for i in range(len(band))
                    if (band[i]["segment"]+1) % n != band[(i+1) % len(band)]["segment"]), None)
        if gap is not None:
            band = band[gap+1:] + band[:gap+1]
        groups = []
        for face in band:
            color = shade(face["material"], face["normal"])
            if groups and groups[-1][0] == color:
                groups[-1][1].append(face)
            else:
                groups.append((color, [face]))
        for color, faces in groups:
            a = [faces[0]["points"][0]] + [f["points"][1] for f in faces]
            b = [faces[0]["points"][3]] + [f["points"][2] for f in faces]
            plain.append({"points": a+list(reversed(b)), "normal": unit(camera),
                          "material": color, "color": PALETTE[color],
                          "part": faces[0]["part"], "edge": False})
    surfaces = plain
    all_points = [projection(p, view) for s in surfaces for p in s["points"]]
    xmin, xmax = min(p[0] for p in all_points), max(p[0] for p in all_points)
    ymin, ymax = min(p[1] for p in all_points), max(p[1] for p in all_points)
    scale = min(248/(xmax-xmin), 248/(ymax-ymin))
    tx, ty = 160-scale*(xmin+xmax)/2, 160-scale*(ymin+ymax)/2

    def screen(p):
        x, y = projection(p, view)
        return round(tx+x*scale, 2), round(ty+y*scale, 2)

    def depth(s):
        # Lines and small decals follow their supporting surface at equal depth.
        value = sum(dot(p, camera) for p in s["points"])/len(s["points"])
        # Coplanar inset panels belong over their entire supporting housing face.
        # Their centers need not match the center of that much larger face.
        if s.get("decal"):
            value += 8 if view == "isometric" else 0
        return value

    fragments = []
    for s in sorted(surfaces, key=depth):
        points = " ".join(f"{x:g},{y:g}" for x, y in map(screen, s["points"]))
        part = s["part"]
        if "line" in s:
            fragments.append(f'<polyline data-part="{part}" points="{points}" fill="none" '
                             f'stroke="{PALETTE[s["material"]]}" stroke-width="{s["line"]}"/>')
        else:
            color = s.get("color") or PALETTE[shade(s["material"], s["normal"])]
            stroke = PALETTE["ink"] if s["edge"] else color
            width = "2.2" if s["edge"] else "0.55"
            fragments.append(f'<polygon data-part="{part}" points="{points}" fill="{color}" '
                             f'stroke="{stroke}" stroke-width="{width}"/>')
    anchors = {k: screen(v) for k, v in model.anchors.items()}
    meta = {"view": view, "units": "schematic", "projection": "orthographic" if view == "side" else "true-isometric-30deg",
            "canvas": [320, 320], "scale": round(scale, 5), "anchors": anchors}
    title = f"{model.name} — {view} view"
    body = '\n    '.join(fragments)
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="320" height="320" viewBox="0 0 320 320" '
           f'role="img" aria-labelledby="title desc">\n'
           f'  <title id="title">{html.escape(title)}</title>\n'
           f'  <desc id="desc">{html.escape(model.description)} Transparent, schematic guide artwork.</desc>\n'
           f'  <metadata>{html.escape(json.dumps(meta, separators=(",", ":")))}</metadata>\n'
           f'  <g id="artwork" stroke-linejoin="round" stroke-linecap="round">\n    {body}\n  </g>\n</svg>\n')
    return svg, meta, body


def contact_sheet(assets):
    # Each row pairs the two projections. Inline vectors, no linked images.
    height = 228 + len(PARTS)*202
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="1100" height="{height}" viewBox="0 0 1100 {height}" role="img" aria-labelledby="title desc">',
           '<title id="title">AutoArm parts — side and isometric</title>',
           '<desc id="desc">Eight reusable parts, each shown in side and isometric views.</desc>',
           f'<rect width="1100" height="{height}" fill="#f8faf9"/>',
           '<g font-family="Segoe UI, Arial, sans-serif" fill="#263e4b">',
           '<text x="56" y="62" font-size="34" font-weight="600">AutoArm parts</text>',
           '<text x="56" y="96" font-size="17" fill="#496370">A first set for the Getting Started guide</text>',
           '<text x="428" y="137" font-size="14" font-weight="600">SIDE</text>',
           '<text x="765" y="137" font-size="14" font-weight="600">ISOMETRIC</text>',
           '</g>']
    notes = ["Round head · rotary joint", "Fork and transverse pivot", "Partly extended", "Three cutting drums", "Twin welding electrodes", "Light armor cube", "Display and control panel", "Open ship controller"]
    for i, (slug, name) in enumerate(PARTS):
        top = 156 + i*202
        out += [f'<path d="M56 {top}H1044" stroke="#d4dfe2"/>',
                f'<text x="56" y="{top+82}" font-family="Segoe UI, Arial, sans-serif" font-size="23" font-weight="600" fill="#263e4b">{name}</text>',
                f'<text x="56" y="{top+108}" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#496370">{notes[i]}</text>']
        for view, xx in [("side", 352), ("isometric", 690)]:
            body = assets[(slug, view)][2]
            out.append(f'<g transform="translate({xx} {top+1}) scale(.625)" stroke-linejoin="round" stroke-linecap="round">{body}</g>')
    out.append(f'<text x="56" y="{height-30}" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#496370">Transparent SVG assets · consistent palette and strokes · schematic proportions</text></svg>')
    return '\n'.join(out) + '\n'


PARTS = [("rotor", "Rotor"), ("hinge", "Hinge"), ("piston", "Piston"),
         ("drill", "Drill"), ("welder", "Welder"), ("block", "Block"),
         ("programmable-block", "Programmable block"), ("controller", "Control seat")]


def main():
    assets = {}
    manifest = {"version": 1, "palette": PALETTE, "assets": []}
    for (slug, name), factory in zip(PARTS, [rotor, hinge, piston, drill, welder, block, programmable_block, controller]):
        model = factory()
        for view in ("side", "isometric"):
            result = render(model, view)
            assets[(slug, view)] = result
            filename = f"{slug}-{view}.svg"
            (ROOT/filename).write_text(result[0], encoding="utf-8")
            manifest["assets"].append({"file": filename, "name": name, **result[1]})
    (ROOT/"manifest.json").write_text(json.dumps(manifest, indent=2)+"\n", encoding="utf-8")
    (ROOT/"contact-sheet.svg").write_text(contact_sheet(assets), encoding="utf-8")
    with zipfile.ZipFile(ROOT/"autoarm-parts-svg.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for item in manifest["assets"]:
            archive.write(ROOT/item["file"], item["file"])
        for filename in ("contact-sheet.svg", "manifest.json", "README.md", "preview.html"):
            if (ROOT/filename).exists():
                archive.write(ROOT/filename, filename)
    print(f"Built {len(manifest['assets'])} SVGs, a vector contact sheet, manifest and ZIP in {ROOT}")


if __name__ == "__main__":
    main()
