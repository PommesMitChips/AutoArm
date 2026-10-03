"""Build the illustrated guide's vector assemblies and terminal mockups.

Uses the established blocks/build.py models. fontTools is needed only to outline
the bundled Oxanium font in the portable terminal mockups.
"""
import copy
import html
import importlib.util
import json
import math
from pathlib import Path
import re
import sys
import zipfile

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/models/autoarm'
DOCS = REPO / 'docs'
spec = importlib.util.spec_from_file_location("parts", TOOLS/"blocks/build.py")
parts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parts)
scene_spec = importlib.util.spec_from_file_location("guide_scene_renderer", TOOLS/"models/scene_renderer.py")
scene_renderer = importlib.util.module_from_spec(scene_spec)
scene_spec.loader.exec_module(scene_renderer)


def yaw(model, angle):
    model = copy.deepcopy(model)
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    def rotate(v):
        x, y, z = v
        return (x*c+z*s, y, -x*s+z*c)
    for surface in model.surfaces:
        if "cylinder" in surface:
            x, z, *rest = surface["cylinder"]
            xx, _, zz = rotate((x, 0, z))
            surface["cylinder"] = (xx, zz, *rest)
        else:
            surface["points"] = [rotate(p) for p in surface["points"]]
            surface["normal"] = rotate(surface["normal"])
    model.anchors = {k: rotate(p) for k, p in model.anchors.items()}
    return model


FACTORIES = {"block": parts.block, "rotor": parts.rotor, "hinge": parts.hinge,
             "piston": parts.piston, "drill": parts.drill,
             "pb": parts.programmable_block, "controller": parts.controller}
HEIGHTS = {"block": 64, "rotor": 89, "hinge": 88, "piston": 208, "drill": 157}
ARM = [{"part": "block", "pos": (0, 0, 0), "label": "Grid"},
       {"part": "rotor", "pos": (0, 64, 0), "label": "Base rotor"}]
y = 64+89
for x in (-64, 0, 64):
    ARM.append({"part": "block", "pos": (x, y, 0), "label": "Lower crossbar"})
y += 64
for x in (-64, 64):
    ARM.append({"part": "hinge", "pos": (x, y, 0), "yaw": 90, "label": "Two hinges"})
y += 88
for x in (-64, 64):
    ARM.append({"part": "piston", "pos": (x, y, 0), "label": "Two pistons"})
y += 208
for x in (-64, 0, 64):
    ARM.append({"part": "block", "pos": (x, y, 0), "label": "Upper crossbar"})
y += 64
SERIAL = ["hinge", "rotor", "hinge", "piston", "rotor", "hinge", "hinge", "rotor", "drill"]
for index, part in enumerate(SERIAL):
    angle = 90 if index == 6 else 0
    label = "Drill head" if part == "drill" else part.capitalize()
    ARM.append({"part": part, "pos": (0, y, 0), "yaw": angle, "label": label})
    y += HEIGHTS[part]
ARM_HEIGHT = y


def rotate_z(point, angle):
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    x, y, z = point
    return (x*c-y*s, x*s+y*c, z)


def rotation(axis, angle):
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    if axis == "X":
        return ((1, 0, 0), (0, c, -s), (0, s, c))
    if axis == "Y":
        return ((c, 0, s), (0, 1, 0), (-s, 0, c))
    return ((c, -s, 0), (s, c, 0), (0, 0, 1))


def matvec(matrix, point):
    return tuple(parts.dot(row, point) for row in matrix)


def matmul(a, b):
    return tuple(tuple(sum(a[i][k]*b[k][j] for k in range(3)) for j in range(3)) for i in range(3))


def posed_model(item, view):
    model = FACTORIES[item["part"]]()
    if item.get("yaw"):
        model = yaw(model, item["yaw"])
    head_angle = item.get("head_angle", 0)
    if head_angle:
        pivot = (0, 46, 0)
        head_rotation = rotation("X" if item.get("yaw") == 90 else "Z", head_angle)
        def head_point(point):
            return parts.add(pivot, matvec(head_rotation, tuple(a-b for a, b in zip(point, pivot))))
        for surface in model.surfaces:
            if surface.get("part") == "head":
                surface["points"] = [head_point(p) for p in surface["points"]]
                surface["normal"] = matvec(head_rotation, surface["normal"])
        model.anchors["head"] = head_point(model.anchors["head"])
    head_yaw = item.get("head_yaw", 0)
    if head_yaw:
        head_rotation = rotation("Y", head_yaw)
        for surface in model.surfaces:
            if surface.get("part") != "head":
                continue
            if "cylinder" in surface:
                x, z, *rest = surface["cylinder"]
                xx, _, zz = matvec(head_rotation, (x, 0, z))
                surface["cylinder"] = (xx, zz, *rest)
            else:
                surface["points"] = [matvec(head_rotation, p) for p in surface["points"]]
                surface["normal"] = matvec(head_rotation, surface["normal"])
        model.anchors["head"] = matvec(head_rotation, model.anchors["head"])
    frame = item.get("rotation") or rotation("Z", item.get("tilt", 0))
    size = item.get("size", 1)
    camera = (0, 0, 1) if view == "side" else (1, 1, 1)
    local_camera = matvec(tuple(zip(*frame)), camera)
    surfaces = []
    for surface in model.surfaces:
        # Expand cylinder silhouettes in the local camera before rotating them.
        # This preserves the actual elliptical profile of a tilted piston/rotor.
        if "cylinder" in surface:
            x, z, lo, hi, r, rr = surface["cylinder"]
            angle = math.atan2(local_camera[2], local_camera[0])
            for tangent in (angle-math.pi/2, angle+math.pi/2):
                surfaces.append({"points": [(x+r*math.cos(tangent), lo, z+r*math.sin(tangent)),
                                             (x+rr*math.cos(tangent), hi, z+rr*math.sin(tangent))],
                                 "normal": parts.unit(local_camera), "material": "ink", "line": 2.2,
                                 "part": surface["part"]})
        else:
            surfaces.append(surface)
    for surface in surfaces:
        surface["points"] = [matvec(frame, tuple(v*size for v in p)) for p in surface["points"]]
        surface["normal"] = matvec(frame, surface["normal"])
    model.surfaces = surfaces
    model.anchors = {k: matvec(frame, tuple(v*size for v in p)) for k, p in model.anchors.items()}
    return model


def component(item, view, scale, ox, oy):
    out = []
    add_scene(out, [item], view, scale, ox, oy)
    return '\n'.join(out)


def folded_arm():
    result = copy.deepcopy(ARM[:5])
    # Both coaxial lower hinges bend their moving heads backward together.
    lower_angle = -45
    frame = rotation("X", lower_angle)
    piston_heads = []
    for hinge_source, piston_source in zip(ARM[5:7], ARM[7:9]):
        hinge = {**copy.deepcopy(hinge_source), "head_angle": lower_angle}
        result.append(hinge)
        piston = {**copy.deepcopy(piston_source), "rotation": frame,
                  "pos": parts.add(hinge["pos"], posed_model(hinge, "isometric").anchors["head"])}
        result.append(piston)
        piston_heads.append(parts.add(piston["pos"], posed_model(piston, "isometric").anchors["head"]))
    center = tuple((a+b)/2 for a, b in zip(*piston_heads))
    for source in ARM[9:12]:
        result.append({**copy.deepcopy(source), "rotation": frame,
                       "pos": parts.add(center, matvec(frame, (source["pos"][0], 0, 0)))})
    origin = parts.add(center, matvec(frame, (0, 64, 0)))
    # Clock the upper hinge plane with the intervening rotor. The upper piston
    # then folds forward, followed by the wrist bringing the drill downward.
    angles = [0, 0, 90, 0, 0, 90, 0, 0, 0]
    for index, (part, angle) in enumerate(zip(SERIAL, angles)):
        axis = "X" if index == 6 else "Z"
        head_yaw = 90 if index == 1 else 0
        item = {"part": part, "pos": origin, "rotation": frame,
                "yaw": 90 if index == 6 else 0, "head_angle": angle, "head_yaw": head_yaw}
        result.append(item)
        model = posed_model(item, "isometric")
        head = model.anchors.get("head", model.anchors.get("tip"))
        origin = parts.add(origin, head)
        frame = matmul(frame, rotation(axis, angle))
        if head_yaw:
            frame = matmul(frame, rotation("Y", head_yaw))
    return result


def scene_fit(scene, view, width, height, padding=48):
    points = [parts.projection(parts.add(point, item["pos"]), view)
              for item in scene for surface in posed_model(item, view).surfaces for point in surface["points"]]
    xmin, xmax = min(p[0] for p in points), max(p[0] for p in points)
    ymin, ymax = min(p[1] for p in points), max(p[1] for p in points)
    scale = min((width-2*padding)/(xmax-xmin), (height-2*padding)/(ymax-ymin))
    return scale, (width-scale*(xmin+xmax))/2, (height-scale*(ymin+ymax))/2


def add_scene(out, scene, view, scale, ox, oy):
    scene_renderer.add_scene(out, scene, view, scale, ox, oy, posed_model, parts)


def cross(out, world, view, scale, ox, oy, radius=10):
    x, y = parts.projection(world, view)
    x, y = ox+x*scale, oy+y*scale
    out.append(f'<path d="M{x-radius:g} {y-radius:g}L{x+radius:g} {y+radius:g}M{x+radius:g} {y-radius:g}L{x-radius:g} {y+radius:g}" '
               'fill="none" stroke="#ad5447" stroke-width="3.2" stroke-linecap="round"/>')


def unsupported_diagrams():
    floor = [{"part": "block", "pos": (x, 0, 0)} for x in (-64, 0, 64)]
    # Offset hinge stages create a rigid loop with non-coaxial rotary joints.
    scene = floor + [{"part": "block", "pos": (64, 64, 0)},
                     {"part": "hinge", "pos": (-64, 64, 0), "yaw": 90},
                     {"part": "hinge", "pos": (64, 128, 0), "yaw": 90},
                     {"part": "piston", "pos": (-64, 152, 0)},
                     {"part": "piston", "pos": (64, 216, 0)},
                     {"part": "block", "pos": (-64, 360, 0)}]
    scene += [{"part": "block", "pos": (x, 424, 0)} for x in (-64, 0, 64)]
    out = svg_start(440, 510, "Unsupported: misaligned parallel stages", "The right hinge and piston stage starts one block above the left. The hinge axes do not line up.")
    scale, ox, oy = scene_fit(scene, "side", 440, 510, 28)
    add_scene(out, scene, "side", scale, ox, oy)
    for yy in (110, 174):
        sy = oy-yy*scale
        out.append(f'<path d="M{ox-106*scale:g} {sy:g}H{ox+106*scale:g}" stroke="#ad5447" stroke-width="1.6" stroke-dasharray="6 5"/>')
    for xx, yy in ((-64, 110), (64, 174)):
        cross(out, (xx, yy, 0), "side", scale, ox, oy, 9)
    write_svg("unsupported-misaligned.svg", out)

    scene = [{"part": "block", "pos": (x, 0, 0)} for x in (-64, 0, 64, 128)]
    hinge = {"part": "hinge", "pos": (-64, 64, 0), "head_angle": -70}
    scene.append(hinge)
    boom_origin = parts.add(hinge["pos"], posed_model(hinge, "side").anchors["head"])
    for i in range(3):
        scene.append({"part": "block", "pos": parts.add(boom_origin, rotate_z((0, i*64, 0), -70)), "tilt": -70})
    foot, tip = (128, 64, 0), (72, 160, 0)
    vector = tuple(b-a for a, b in zip(foot, tip))
    tilt = math.degrees(math.atan2(-vector[0], vector[1]))
    size = math.sqrt(parts.dot(vector, vector))/208
    scene.append({"part": "piston", "pos": foot, "tilt": tilt, "size": size})
    out = svg_start(500, 320, "Unsupported: hydraulic linkage", "A diagonal piston braces the grid to a hinged boom and drives its rotation. This hydraulic-style structure is not supported.")
    scale, ox, oy = scene_fit(scene, "side", 500, 320, 35)
    add_scene(out, scene, "side", scale, ox, oy)
    for point in (foot, tip):
        px, py = parts.projection(point, "side")
        out.append(f'<circle cx="{ox+px*scale:g}" cy="{oy+py*scale:g}" r="6" fill="#b4c5cd" stroke="#263e4b" stroke-width="1.6"/>')
    cross(out, tuple((a+b)/2 for a, b in zip(foot, tip)), "side", scale, ox, oy, 12)
    write_svg("unsupported-hydraulic.svg", out)


def svg_start(width, height, title, desc):
    return [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-labelledby="title desc">',
            f'<title id="title">{html.escape(title)}</title><desc id="desc">{html.escape(desc)}</desc>']


def write_svg(name, output):
    (ROOT/name).write_text('\n'.join(output)+"\n</svg>\n", encoding="utf-8")


def assemblies():
    out = svg_start(640, 440, "First actuator on a grid", "An upright rotor mounted on a light armor block, in isometric view.")
    add_scene(out, ARM[:2], "isometric", 1.9, 315, 344)
    write_svg("rotor-on-grid.svg", out)

    scale, ox, oy = .78, 135, 1280
    out = svg_start(520, 1350, "Example arm — front view", "Base rotor; crossbar; two hinges; two pistons; crossbar; hinge, rotor, hinge, piston, rotor, hinge, hinge, rotor and drill head. Read from bottom to top.")
    out.append('<metadata>'+html.escape(json.dumps({"readingOrder": "bottom-to-top", "components": ARM, "serialChain": SERIAL, "orthogonalHingeIndex": 6}))+ '</metadata>')
    add_scene(out, ARM, "side", scale, ox, oy)
    # One label for each row of the build, including each individual serial joint.
    rows = []
    seen = set()
    for item in ARM:
        key = (item["pos"][1], item["label"])
        if key in seen:
            continue
        seen.add(key)
        yy = item["pos"][1] + HEIGHTS[item["part"]]/2
        rows.append((yy, item["label"], item["part"]))
    for yy, label, part in rows:
        sy = oy-yy*scale
        parallel = label in ("Two hinges", "Two pistons", "Lower crossbar", "Upper crossbar")
        start = ox+(102 if parallel else 40)*scale
        out.append(f'<path d="M{start:g} {sy:g}H250" stroke="#9db1bb" stroke-width="1.2" fill="none"/>')
        out.append(f'<circle cx="{start:g}" cy="{sy:g}" r="2.4" fill="#496370"/>')
        out.append(f'<text x="264" y="{sy+6:g}" font-family="Inter, Segoe UI, sans-serif" font-size="18" fill="#263e4b">{html.escape(label)}</text>')
    write_svg("arm-front.svg", out)

    out = svg_start(850, 940, "Classical arm pose with programmable block — isometric view", "The same complete arm with a raised shoulder, bent elbow, returning forearm and downward-facing drill, with a control seat and programmable block on the connected foundation grid.")
    scene = [item for item in folded_arm() if item.get("label") != "Grid"]
    scene += [{"part": "block", "pos": (x, 0, 0)} for x in (-128, -64, 0, 64, 128)]
    scene += [{"part": "pb", "pos": (128, 64, 0)}, {"part": "controller", "pos": (-128, 64, 0)}]
    scale, ox, oy = scene_fit(scene, "isometric", 850, 860, 45)
    add_scene(out, scene, "isometric", scale, ox, oy)
    # Leaders point to the controller bodies rather than to the foundation.
    for world, label, tx, ty in [((128, 94, 32), "Programmable block", 470, 908),
                                ((-128, 100, 32), "Control seat", 100, 880)]:
        px, py = parts.projection(world, "isometric")
        sx, sy = ox+px*scale, oy+py*scale
        out.append(f'<path d="M{sx:g} {sy:g}L{tx:g} {ty-13:g}" stroke="#9db1bb" fill="none" stroke-width="1.2"/>')
        out.append(f'<text x="{tx}" y="{ty}" font-family="Inter, Segoe UI, sans-serif" font-size="18" fill="#263e4b">{label}</text>')
    write_svg("arm-isometric-pb.svg", out)


def outline_font():
    from fontTools.ttLib import TTFont
    from fontTools.varLib.instancer import instantiateVariableFont
    from fontTools.pens.svgPathPen import SVGPathPen
    font = instantiateVariableFont(TTFont(DOCS/"assets/fonts"/"Oxanium.ttf"), {"wght": 500}, inplace=True)
    glyphs = font.getGlyphSet()
    cmap = font.getBestCmap()
    units = font["head"].unitsPerEm
    def text(value, x, y, size=26, color="#d7e9eb", center=False):
        names = [cmap.get(ord(ch), ".notdef") for ch in value]
        width = sum(glyphs[name].width for name in names)*size/units
        if center:
            x -= width/2
        fragments = [f'<g aria-label="{html.escape(value)}" fill="{color}" transform="translate({x:g} {y:g}) scale({size/units:g} {-size/units:g})">']
        cursor = 0
        for name in names:
            pen = SVGPathPen(glyphs)
            glyphs[name].draw(pen)
            path = pen.getCommands()
            if path:
                fragments.append(f'<path d="{path}" transform="translate({cursor:g} 0)"/>')
            cursor += glyphs[name].width
        fragments.append('</g>')
        return ''.join(fragments)
    return text


def terminal_mocks():
    text = outline_font()
    def button(out, label, x, y, w, h, selected=False, highlight=False):
        fill = "#5b747d" if selected else "#293941"
        stroke = "#e9b34c" if highlight else "#50606a"
        out.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke}" stroke-width="{2 if highlight else 1.5}"/>')
        out.append(text(label, x+w/2, y+h/2+9, center=True))
    def toggles(out, label, y, on=True):
        out.append(text(label, 280, y, size=24, center=True))
        button(out, "On", 167, y+15, 104, 58, selected=on)
        button(out, "Off", 289, y+15, 104, 58, selected=not on)
    def ground(title, desc, height):
        out = svg_start(560, height, title, desc)
        out.append(f'<rect width="560" height="{height}" fill="#202e35"/>')
        out.append(f'<path d="M26 22H534M26 {height-22}H534" stroke="#50636c"/>')
        return out
    for role in ("Base", "Head"):
        out = ground(f"Name the {role.lower()} block", f"Terminal mockup. Set Name to Arm 1 - {role}. The Arm 1 prefix is an example.", 466)
        toggles(out, "Toggle block", 60)
        out.append('<path d="M32 162H528" stroke="#50636c"/>')
        out.append(text("Name", 32, 213, size=26))
        out.append('<rect x="32" y="229" width="496" height="64" fill="#293941" stroke="#e9b34c" stroke-width="2"/>')
        out.append(text(f"Arm 1 - {role}", 49, 270, size=29))
        toggles(out, "Show on HUD", 347, on=False)
        write_svg(f"name-{role.lower()}-terminal.svg", out)
    out = ground("Run AutoArm", "Terminal mockup. Open Edit to load the AutoArm script. Enter On in Argument, then press Run.", 620)
    out.append(text("Code", 280, 66, center=True))
    button(out, "Edit", 78, 86, 404, 68)
    out.append(text("Argument", 32, 197))
    out.append('<rect x="32" y="212" width="496" height="62" fill="#293941" stroke="#e9b34c" stroke-width="2"/>')
    out.append(text("On", 49, 252, size=29))
    button(out, "Run", 78, 295, 404, 67, highlight=True)
    button(out, "Recompile", 78, 362, 404, 67)
    button(out, "Custom Data", 78, 429, 404, 67)
    out.append(text("Name", 32, 533))
    out.append('<rect x="32" y="548" width="496" height="49" fill="#293941" stroke="#50606a"/>')
    out.append(text("Programmable Block", 49, 581, size=25))
    write_svg("run-terminal.svg", out)


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    assemblies()
    unsupported_diagrams()
    if "--diagrams-only" not in sys.argv:
        terminal_mocks()
    manifest = {"diagramType": "schematic", "readingOrder": "bottom-to-top", "components": ARM,
                "surfaceOrdering": "scene-wide BSP splitting of visible planar faces and original edge segments",
                "actuatorCounts": {"rotor": 4, "hinge": 6, "piston": 3},
                "serialChainAboveUpperCrossbar": SERIAL, "orthogonalHingeIndex": 6,
                "foldedComponents": folded_arm(),
                "unsupportedExamples": ["misaligned", "hydraulic"],
                "sourceScreenshots": ["codex-clipboard-01627974-45fb-48a1-a885-8da0f1b0d10a.png", "codex-clipboard-faf1a543-7121-490d-a5a8-9a1c595642c3.png"]}
    (ROOT/"manifest.json").write_text(json.dumps(manifest, indent=2)+"\n", encoding="utf-8")
    with zipfile.ZipFile(DOCS/"assets/exports/autoarm-getting-started.zip", "w", zipfile.ZIP_DEFLATED) as z:
        for file in [DOCS/"getting-started.html", DOCS/"GETTING_STARTED.md", DOCS/"TROUBLESHOOTING.md", DOCS/"MODULES.md", DOCS.parent/"README.md"]:
            if file.exists():
                z.write(file, file.relative_to(DOCS.parent))
        for folder in (ROOT, DOCS/"assets/blocks", DOCS/"assets/fonts"):
            for file in folder.iterdir():
                if file.is_file() and file.suffix not in (".png", ".pyc"):
                    z.write(file, file.relative_to(DOCS.parent))
    print(f"Built guide diagrams and bundle; assembly has {len(ARM)} components and 13 actuators.")


if __name__ == "__main__":
    main()
