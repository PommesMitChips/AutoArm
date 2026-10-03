"""Native diagram and transparent panel export for the ToolSwap setup guide.

Branding is generated separately with built-in imagegen. This builder preserves
the established guide layout and uses exact geometric SVG models for mechanics.
Run --art, render_artwork.mjs, then this script without arguments.
"""
import copy
import hashlib
import html
import importlib.util
import json
import math
from pathlib import Path
import re
import sys
import zipfile

from PIL import Image, ImageFont

REPO = next(p for p in Path(__file__).resolve().parents if (p / 'stable').is_dir() and (p / 'experimental').is_dir())
TOOLS = REPO / 'tools/asset-builders'
ROOT = REPO / 'docs/assets/workshop/tool-swap'
DOCS = REPO / 'docs'
ART = ROOT / "artwork"
PANELS = ROOT / "panels"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


layout = load("panel_layout", TOOLS / "workshop/build_panels.py")
guide = load("guide_models", TOOLS / "models/build.py")
parts = guide.parts
layout.OUTPUT = PANELS
layout.ASSETS = ART


def rotor_head():
    m = parts.rotor()
    m.name = "Loose rotor head"
    m.description = "The rotor head and spindle stay rigidly attached to the arm."
    m.surfaces = [s for s in m.surfaces if s.get("part") == "head"]
    for surface in m.surfaces:
        if "cylinder" in surface:
            x, z, lo, hi, radius, top_radius = surface["cylinder"]
            surface["cylinder"] = (x, z, lo - 89, hi - 89, radius, top_radius)
        else:
            surface["points"] = [(x, y - 89, z) for x, y, z in surface["points"]]
    m.anchors = {"mount": (0, 0, 0), "tip": (0, -35, 0)}
    return m


def rotor_base():
    m = parts.rotor()
    m.name = "Tool rotor base"
    m.description = "A detached rotor base on the tool, with an open socket for the arm's rotor head."
    m.surfaces = [s for s in m.surfaces if s.get("part") != "head"]
    m.cylinder_y(0, 0, 54.05, 54.5, 17, "dark", "socket")
    m.anchors = {"mount": (0, 0, 0), "socket": (0, 54, 0)}
    return m


def merge_block():
    m = parts.Model("Merge block", "A support merge block with a mating face and status indicator.",
                    {"mount": (0, 0, 0), "face": (32, 32, 0)})
    m.bevel(0, 64, cut=5, material="shade")
    m.panel_x(32.2, 32, 0, 50, 50, "dark")
    for z in (-18, -6, 6, 18):
        m.panel_x(32.35, 32, z, 6, 42, "steel")
    m.panel_z(0, 30, 32.2, 38, 30, "recess")
    m.panel_z(0, 30, 32.4, 25, 14, "shade")
    m.panel_z(0, 52, 32.4, 16, 5, "signal")
    return m


def rack_drill():
    """Local two-cell drill: one housing cell and one aligned cutting cell."""
    m = parts.Model("Rack drill", "Compact technical drill with a full-size conveyor housing and three parallel toothed cutting drums.",
                    {"mount": (0, 0, 0), "housing-front": (0, 64, 0), "tip": (0, 128, 0), "axis": (0, 96, 0)})
    m.bevel(0, 58)
    m.ports(27)
    m.bevel(54, 10, material="shade")
    m.cylinder_y(0, 0, 64, 74, 22, "dark", "drive")
    m.bevel(74, 12, w=64, d=64, cut=9, part="guard")
    m.panel_z(0, 80, 32.2, 40, 7, "amber")
    m.panel_x(32.2, 80, 0, 32, 7, "amber")
    for xx, zz in [(-16, -10), (16, -10), (0, 18)]:
        m.cylinder_y(xx, zz, 86, 92, 13, "dark", "cutter")
        m.cylinder_y(xx, zz, 92, 114, 13, "shade", "cutter")
        m.cylinder_y(xx, zz, 114, 120, 13, "dark", "cutter")
        m.cylinder_y(xx, zz, 120, 128, 9, "steel", "cutter", top_r=6)
        for angle in range(0, 360, 60):
            a = math.radians(angle)
            m.cylinder_y(xx+11*math.cos(a), zz+11*math.sin(a), 109, 126, 2.4,
                         "steel", "teeth", top_r=1.1)
    return m


def rack_welder():
    """Keep the native welder housing; fit both electrodes in the head cell."""
    m = parts.welder()
    m.name = "Rack welder"
    m.description = "A full-size conveyor housing with two reservoirs and parallel electrodes occupying the working-head cell."
    ratio = 64/81

    def axial(y):
        return y if y <= 64 else 64+(y-64)*ratio

    for surface in m.surfaces:
        if "cylinder" in surface:
            x, z, lo, hi, radius, top_radius = surface["cylinder"]
            surface["cylinder"] = (x, z, axial(lo), axial(hi), radius, top_radius)
        else:
            old_y = sum(p[1] for p in surface["points"])/len(surface["points"])
            surface["points"] = [(x, axial(y), z) for x, y, z in surface["points"]]
            nx, ny, nz = surface["normal"]
            surface["normal"] = parts.unit((nx, ny/ratio if old_y > 64 else ny, nz))
    m.anchors = {k: (x, axial(y), z) for k, (x, y, z) in m.anchors.items()}
    m.anchors["housing-front"] = (0, 64, 0)
    return m


guide.FACTORIES.update({"rotor-head": rotor_head, "rotor-base": rotor_base, "merge": merge_block,
                        "rack-drill": rack_drill, "rack-welder": rack_welder})


def slot(tool):
    """Three top cells, with two merges directly under the housing cell.

    Working head | tool housing | rotor base
             air | tool merge   | air
             air | stand merge  | air
    """
    return [
        {"part": "merge", "pos": (-64, 48, 0), "rotation": guide.rotation("Z", 90), "role": "stand-merge"},
        {"part": "merge", "pos": (-128, 112, 0), "rotation": guide.rotation("Z", -90), "role": "tool-merge"},
        {"part": "rotor-base", "pos": (-64, 176, 0), "rotation": guide.rotation("Z", -90), "role": "tool-mount"},
        {"part": "rack-"+tool, "pos": (-64, 176, 0), "rotation": guide.rotation("Z", 90), "role": "tool-reference"},
    ]


def arm_tip():
    return [
        {"part": "block", "pos": (216, 144, 0), "role": "arm-body"},
        {"part": "hinge", "pos": (184, 176, 0), "rotation": guide.rotation("Z", 90), "role": "last-arm-joint"},
        {"part": "rotor-head", "pos": (96, 176, 0), "rotation": guide.rotation("Z", -90), "role": "arm-tip"},
    ]


def shifted(scene, delta):
    return [{**item, "pos": parts.add(item["pos"], delta)} for item in copy.deepcopy(scene)]


def stand_slot(tool):
    """Image 1 only: working ends point screen-right/up and recede.

    Rotate the entire rack about the vertical centre of its housing. The two
    merges stay directly underneath it and rotate with their supporting tool;
    the separate pickup/arm illustration retains its established orientation.
    """
    centre = (-96, 176, 0)
    frame = guide.rotation("Y", -90)
    result = []
    for item in slot(tool):
        delta = tuple(a-b for a, b in zip(item["pos"], centre))
        result.append({**copy.deepcopy(item), "pos": parts.add(centre, guide.matvec(frame, delta)),
                       "rotation": guide.matmul(frame, item["rotation"])})
    return result


def stand_direction_checks():
    """Pin image 1's actual projected direction, not a verbal left/right cue."""
    camera = (1, 1, 1)
    result = {}
    for name in ("drill", "welder"):
        scene = stand_slot(name)
        stand_merge, tool_merge, base, reference = scene
        working = guide.matvec(reference["rotation"], (0, 1, 0))
        socket = guide.matvec(base["rotation"], (0, 1, 0))
        screen_working = parts.projection(working, "isometric")
        screen_socket = parts.projection(socket, "isometric")
        assert screen_working[0] > .8 and screen_working[1] < -.49, "Image 1 working bits must point right and up."
        assert parts.dot(working, camera) < -.99, "Image 1 working bits must point away from the viewer."
        assert screen_socket[0] < -.8 and screen_socket[1] > .49, "The sockets must point in the opposite foreground/left direction."
        assert parts.dot(socket, camera) > .99 and parts.dot(working, socket) < -.99
        faces = []
        for item in (stand_merge, tool_merge):
            model = guide.posed_model(item, "isometric")
            faces.append(parts.add(item["pos"], model.anchors["face"]))
            points = [parts.add(p, item["pos"]) for s in model.surfaces for p in s["points"]]
            assert min(p[0] for p in points) >= -128.5 and max(p[0] for p in points) <= -63.5
            assert min(p[2] for p in points) >= -32.5 and max(p[2] for p in points) <= 32.5
        assert all(abs(a-b) < 1e-6 for a, b in zip(*faces)), "Whole-rack yaw must preserve the vertical merge contact."
        assert all(abs(a-b) < 1e-6 for a, b in zip(faces[0], (-96, 80, 0)))
        assert not any(i["part"] == "block" for i in scene), "The rack's air columns must stay clear."
        result[name] = {"workingAxisWorld": working, "workingAxisScreen": screen_working,
                        "workingDepth": parts.dot(working, camera), "socketAxisWorld": socket,
                        "socketAxisScreen": screen_socket, "socketDepth": parts.dot(socket, camera),
                        "mergeMatingCentre": faces[0]}
    return {"appliesTo": "tool-stands.png only", "rackYawDegrees": -90,
            "verified": ["working ends point screen-right and up", "working ends recede away from the viewer",
                         "sockets oppose the bits and face the foreground/left", "two merge blocks remain vertically below each housing",
                         "no structural blocks in either air column"], "racks": result}


def add_scene(out, scene, view, scale, ox, oy):
    # Both guide families share the same scene-wide surface visibility rules.
    guide.add_scene(out, scene, view, scale, ox, oy)


def mechanical_checks():
    """Audit the exact three-column rack and the unchanged arm pickup axis."""
    def world_anchor(item, anchor):
        return parts.add(item["pos"], guide.posed_model(item, "isometric").anchors[anchor])

    tool = slot("drill")
    arm = arm_tip()
    stand_merge, tool_merge = tool[0], tool[1]
    base, reference, head = tool[2], tool[3], arm[2]
    socket = world_anchor(base, "socket")
    spindle = world_anchor(head, "tip")
    merge_a = world_anchor(stand_merge, "face")
    merge_b = world_anchor(tool_merge, "face")
    assert all(abs(a-b) < 1e-6 for a, b in zip(merge_a, merge_b)), "Merge mating faces must coincide."
    assert all(abs(a-b) < 1e-6 for a, b in zip(merge_a, (-96, 80, 0))), "Merges must mate vertically beneath the central housing."
    stand_normal = guide.matvec(stand_merge["rotation"], (1, 0, 0))
    tool_normal = guide.matvec(tool_merge["rotation"], (1, 0, 0))
    assert all(abs(a-b) < 1e-6 for a, b in zip(stand_normal, (0, 1, 0)))
    assert all(abs(a-b) < 1e-6 for a, b in zip(tool_normal, (0, -1, 0)))
    assert abs(socket[1]-spindle[1]) < 1e-6 and abs(socket[2]-spindle[2]) < 1e-6, "Pickup must be coaxial."
    assert all(abs(a-b) < 1e-6 for a, b in zip(world_anchor(base, "mount"), world_anchor(reference, "mount"))), "Tool and rotor base must share their backing face."
    tool_bounds = {}
    for name in ("drill", "welder"):
        rack = slot(name)
        assert len(rack) == 4 and not any(i["part"] == "block" for i in rack), "Air columns must contain no armor or supporting columns."
        reference = rack[3]
        model = guide.posed_model(reference, "isometric")
        tip = model.anchors["tip"]
        assert tip[0] < 0 and abs(tip[1]) < 1e-6 and abs(tip[2]) < 1e-6, "Tool tips must point away from the pickup socket."
        working_axis = guide.matvec(reference["rotation"], (0, 1, 0))
        assert parts.dot(working_axis, (1, 1, 1)) < 0, "The working ends must face away from the viewer."
        assert parts.projection(working_axis, "isometric")[0] < 0, "The working ends must face screen-left."
        assert abs(tip[0]+128) < 1e-6, "The tool housing and working head must occupy exactly two axial cells."
        points = [parts.add(p, reference["pos"]) for s in model.surfaces for p in s["points"]]
        bounds = [(min(p[a] for p in points), max(p[a] for p in points)) for a in range(3)]
        assert bounds[0][0] >= -192.5 and bounds[0][1] <= -63.5, "The working head and housing must fit the left two top-row cells."
        assert bounds[1][0] >= 143.5 and bounds[1][1] <= 208.5, "Tool details must stay within the top row."
        tool_bounds[name] = bounds
        for item in rack[:2]:
            points = [parts.add(p, item["pos"]) for s in guide.posed_model(item, "isometric").surfaces for p in s["points"]]
            assert min(p[0] for p in points) >= -128.5 and max(p[0] for p in points) <= -63.5, "Only the middle column may contain merge blocks."
    assert all(abs(a-b) < 1e-6 for a, b in zip(world_anchor(arm[1], "head"), world_anchor(head, "mount"))), "The rotor head must remain attached to the last arm joint."
    return {"pickupAxis": "+X on tool rotor base; arm spindle approaches along -X", "toolWorkingDirection": "-X for every drill cutter and welder electrode",
            "rackOccupancy": [["working head", "tool housing", "rotor base"], ["air", "tool merge", "air"], ["air", "stand merge", "air"]],
            "rackCellBoundsXY": {"working head": [[-192, -128], [144, 208]], "tool housing": [[-128, -64], [144, 208]],
                                 "rotor base": [[-64, 0], [144, 208]], "tool merge": [[-128, -64], [80, 144]],
                                 "stand merge": [[-128, -64], [16, 80]]}, "toolGeometryBoundsXYZ": tool_bounds,
            "mergeMatingFace": merge_a, "standMergeFaceNormal": stand_normal, "toolMergeFaceNormal": tool_normal,
            "mergeCellCentres": {"tool": (-96, 112, 0), "stand": (-96, 48, 0)},
            "toolSocket": socket, "armSpindleTipBeforePickup": spindle,
            "rigidToolBackingFace": world_anchor(base, "mount"), "armHeadAttachment": world_anchor(head, "mount"),
            "verified": ["merge faces coincide vertically at the middle column", "merge face normals oppose along Y", "no structural blocks in either air column",
                         "rotor socket and loose spindle are coaxial", "tool and base backing faces coincide",
                         "all cutting/welding bits share the tool axis and face left/away", "loose rotor head is attached to last arm joint"]}


def diagram(name, scene, width=640, height=420, labels=(), arrow=None, camera=None):
    scale, ox, oy = camera or guide.scene_fit(scene, "isometric", width, height, 56)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
           f'<title>{html.escape(name)}</title>', '<g stroke-linejoin="round" stroke-linecap="round">']
    add_scene(out, scene, "isometric", scale, ox, oy)

    def screen(point):
        x, y = parts.projection(point, "isometric")
        return (ox + x * scale, oy + y * scale)

    for text, world, label in labels:
        x, y = screen(world)
        tx, ty = label
        out.append(f'<path d="M{x:g} {y:g}L{tx:g} {ty - 8:g}" fill="none" stroke="#849da9" stroke-width="1.7"/>')
        out.append(f'<circle cx="{x:g}" cy="{y:g}" r="3.2" fill="#e9b34c"/>')
        out.append(f'<text x="{tx:g}" y="{ty:g}" font-family="Inter, Segoe UI, sans-serif" font-size="21" fill="#e5edf0">{html.escape(text)}</text>')
    if arrow:
        a, b = map(screen, arrow)
        dx, dy = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dy)
        ux, uy = dx / length, dy / length
        end = (b[0] - ux * 8, b[1] - uy * 8)
        start = (a[0] + ux * 8, a[1] + uy * 8)
        out.append(f'<path d="M{start[0]:g} {start[1]:g}L{end[0]:g} {end[1]:g}" stroke="#e9b34c" stroke-width="3" fill="none"/>')
        pts = [end, (end[0] - ux * 12 - uy * 5, end[1] - uy * 12 + ux * 5),
               (end[0] - ux * 12 + uy * 5, end[1] - uy * 12 - ux * 5)]
        out.append('<polygon points="' + ' '.join(f'{x:g},{y:g}' for x, y in pts) + '" fill="#e9b34c"/>')
    out.append('</g></svg>')
    (ART / (name + ".svg")).write_text('\n'.join(out), encoding="utf-8")
    return {"filename": name + ".png", "scene": scene, "labels": [a[0] for a in labels], "camera": {"scale": scale, "origin": [ox, oy], "view": "isometric"}}


def prepare_art():
    ART.mkdir(exist_ok=True)
    floor = [{"part": "block", "pos": (x, -48, 0), "role": "foundation"} for x in range(-352, 161, 64)]
    stands = floor + shifted(stand_slot("drill"), (-256, 0, 0)) + shifted(stand_slot("welder"), (256, 0, 0))
    # Preserve the preceding mount illustration's exact screen frame. The rack
    # revision must not move, rotate or resize the arm presentation in panel 3.
    mount_camera = (0.7834229810261454, 311.70794838956897, 316.25662612507176)
    items = [diagram("tool-stands", stands, height=400),
             diagram("arm-rotor-head", arm_tip(), height=340,
                     labels=[("Loose rotor head", (80, 176, 0), (28, 309)), ("Last arm joint", (150, 176, 0), (355, 42))]),
             diagram("tool-mount", slot("drill") + arm_tip() + [{"part": "block", "pos": (-96, -48, 0), "role": "foundation"}], height=460,
                     labels=[("Rotor base / mount", (-35, 176, 0), (25, 38)),
                             ("Arm rotor head", (78, 176, 0), (405, 40)),
                             ("Connected merges", (-96, 80, 32), (305, 425))],
                     arrow=((61, 176, 0), (-10, 176, 0)), camera=mount_camera)]
    svg, _, _ = parts.render(parts.programmable_block(), "isometric")
    (ART / "programmable-block.svg").write_text(svg, encoding="utf-8")
    items.append({"filename": "programmable-block.png", "source": "existing programmable-block model"})
    (ROOT / "artwork-manifest.json").write_text(json.dumps({"artwork": items, "surfaceOrdering": "scene-wide BSP splitting of visible planar faces and original edge segments",
                                                        "mechanicalChecks": mechanical_checks(),
                                                        "standDirectionChecks": stand_direction_checks()}, indent=2), encoding="utf-8")
    print("Prepared tool stands, separate rotor head/base and programmable-block artwork.")


COPY = [
    {"title": "Getting Started with ToolSwap", "paragraphs": [
        "Park your tools on stands, pair the two programmable blocks, then let AutoArm change tools for you. Start with a working AutoArm arm."]},
    {"title": "Build the tool stands", "paragraphs": [
        "Build a stand for each tool on the arm's grid. Put a merge block on each stand and a facing merge block on the tool body.",
        "Start with every tool securely merged to its stand. Leave room for the arm to approach each mount and withdraw."]},
    {"title": "Keep the rotor head on the arm", "paragraphs": [
        "Put one loose, compatible rotor head at the moving end of your arm. It can attach directly to the last hinge's moving head.",
        "The rotor head stays on the arm when tools are swapped. The rotor base goes on the tool."]},
    {"title": "Add a rotor base to each tool", "paragraphs": [
        "Each tool carries a compatible rotor base with its socket facing the arm's rotor head. Remove the base's own rotor head before use.",
        "Keep the tool, rotor base and tool-side merge blocks rigidly connected as one tool body. Use one eligible rotor base per tool and leave its angle limits unlimited for the first pickup."]},
    {"title": "Name the tools and mounts", "paragraphs": [
        "Name one reference block on each tool using your arm's name and a unique Head number. Here, the drill is Head 1 and the welder is Head 2.",
        "For easy identification, name the tool rotor bases as Mount 1 and Mount 2. Mount names are optional; ToolSwap discovers these bases from the numbered tool references."]},
    {"title": "Install the second programmable block", "paragraphs": [
        "Load the ToolSwap script into a second programmable block on the same construct as AutoArm. Keep both scripts running.",
        "For this example, name the blocks AutoArm PB and ToolSwap PB. Use those exact names in the pairing settings next."]},
    {"title": "Pair the PBs in Custom Data", "paragraphs": [
        "Open AutoArm PB's Custom Data. In the existing arm section, set ToolSwapPB to the ToolSwap block's name.",
        "In ToolSwap PB's Custom Data, use the same arm section and set Link.ArmPB to AutoArm PB. Enable tools with Tools.Enabled=true.",
        "These are snippets: keep your other generated settings and replace Arm 1 with your own arm name."]},
    {"title": "Initialise and change tools", "paragraphs": [
        "With all tools merged to their stands, run On on AutoArm PB. AutoArm discovers the tool mounts and stand merge pairs.",
        "Run Tool 1 or Tool 2 on AutoArm PB to pick up that tool. A swap parks the current tool first and then restores arm control.",
        "Run Park to return the current tool to its stand. Parking leaves the arm OFF; run On to control the bare arm again."]},
]


def header(c, number, text, width=534):
    c.text(str(number), 24, 28, 30, 28, 35, layout.AMBER, 500, "Oxanium")
    return c.text(text, 62, 28, width, 24, 31, layout.STRONG, 600) + 18


def prose(c, index, x, y, width):
    for p in COPY[index]["paragraphs"]:
        y = c.text(p, x, y, width) + 16
    return y


def field(c, label, value, x, y, width=276):
    y = c.text(label, x, y, width, 13, 19, layout.SECONDARY) + 5
    box = (x * 2, y * 2, (x + width) * 2, (y + 43) * 2)
    c.draw.rectangle(box, fill="#293941", outline=layout.AMBER, width=2)
    c.text(value, x + 10, y + 12, width - 20, 15, 21, layout.STRONG)
    return y + 60


def custom_data(c, name, code, x, y, width=276):
    y = c.text(name, x, y, width, 14, 22, layout.STRONG, 600) + 8
    face = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 30)
    lines = code.splitlines()
    h = 24 * len(lines) + 28
    c.draw.rectangle((x * 2, y * 2, (x + width) * 2, (y + h) * 2), fill="#293941", outline="#849da9", width=2)
    for i, line in enumerate(lines):
        c.draw.text(((x + 12) * 2, (y + 11 + i * 24) * 2), line, font=face, fill=layout.INK)
    return y + h


def panel(index):
    c = layout.Canvas()
    if index == 0:
        y = c.text("Getting Started", 24, 28, 572, 42, 50, layout.STRONG, 500, "Oxanium")
        y = c.text("with ToolSwap", 24, y, 572, 42, 50, layout.STRONG, 500, "Oxanium")
        bottom = prose(c, index, 24, y + 23, 555)
    elif index in (1, 2, 3):
        y = header(c, index, COPY[index]["title"])
        y = prose(c, index, 62, y, 534)
        name = {1: "tool-stands", 2: "arm-rotor-head", 3: "tool-mount"}[index]
        bottom = c.art(name, 24, y + 12, 572)
        if index == 1:
            c.text("Drill", 131, bottom - 7, 100, 14, 22, layout.SECONDARY)
            c.text("Welder", 408, bottom - 7, 110, 14, 22, layout.SECONDARY)
            bottom += 25
    elif index == 4:
        y = header(c, index, COPY[index]["title"], 236)
        bottom = prose(c, index, 62, y, 236)
        y = field(c, "Drill reference · Name", "Arm 1 - Head 1 - Drill", 320, 29)
        y = field(c, "Drill rotor base · Name", "Arm 1 - Mount 1", 320, y)
        y += 18
        y = field(c, "Welder reference · Name", "Arm 1 - Head 2 - Welder", 320, y)
        y = field(c, "Welder rotor base · Name", "Arm 1 - Mount 2", 320, y)
        bottom = max(bottom, y)
    elif index == 5:
        y = header(c, index, COPY[index]["title"], 236)
        bottom = prose(c, index, 62, y, 236)
        y = c.art("programmable-block", 380, 21, 158)
        y = c.text("AutoArm PB", 364, y - 7, 210, 15, 22, layout.INK, 600)
        c.rule(348, y + 16, 220)
        y = c.art("programmable-block", 380, y + 36, 158)
        y = c.text("ToolSwap PB", 359, y - 7, 220, 15, 22, layout.INK, 600)
        bottom = max(bottom, y)
    elif index == 6:
        y = header(c, index, COPY[index]["title"])
        y = prose(c, index, 62, y, 534) + 10
        left = custom_data(c, "AutoArm PB · Custom Data", "[Arm 1]\nToolSwapPB=ToolSwap PB", 24, y)
        right = custom_data(c, "ToolSwap PB · Custom Data", "[Arm 1]\nLink.ArmPB=AutoArm PB\nTools.Enabled=true", 320, y)
        bottom = max(left, right) + 16
    else:
        y = header(c, index, COPY[index]["title"], 236)
        bottom = prose(c, index, 62, y, 236)
        y = c.text("AutoArm PB", 320, 28, 276, 16, 25, layout.STRONG, 600) + 22
        y = field(c, "Argument", "Tool 2", 320, y)
        c.draw.rectangle((350 * 2, y * 2, 566 * 2, (y + 49) * 2), fill="#293941", outline="#849da9", width=2)
        c.text("Run", 436, y + 12, 100, 19, 27, layout.STRONG, 500, "Oxanium")
        y += 85
        for command, purpose in [("On", "Initialise"), ("Tool 1", "Drill"), ("Tool 2", "Welder"), ("Park", "Return to stand")]:
            c.text(command, 320, y, 85, 15, 24, layout.STRONG, 600)
            c.text(purpose, 416, y, 178, 14, 24, layout.SECONDARY)
            y += 35
        bottom = max(bottom, y)
    filenames = ["00-toolswap-intro", "01-tool-stands", "02-arm-rotor-head", "03-tool-rotor-bases", "04-tool-and-mount-names",
                 "05-toolswap-programmable-block", "06-peer-custom-data", "07-initialise-and-swap"]
    return {**c.save(filenames[index] + ".png", bottom), **COPY[index]}


def finish():
    PANELS.mkdir(exist_ok=True)
    panels = [panel(i) for i in range(len(COPY))]
    inputs = [DOCS.parent / "README.md", DOCS / "MODULES.md", DOCS / "examples/ArmWithTools.ini",
              DOCS / "examples/MultiArmTools.ini", DOCS.parent / "stable/AutoArm_ToolSwap_Source.txt"]
    manifest = {"panels": panels, "branding": [], "checkedAgainst": {str(p.relative_to(DOCS.parent)): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}}
    for path in sorted((ROOT / "branding").glob("*.png")):
        image = Image.open(path)
        manifest["branding"].append({"filename": "branding/" + path.name, "width": image.width, "height": image.height, "bytes": path.stat().st_size})
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    images = '\n'.join(f'<img src="panels/{p["filename"]}" width="{p["width"]}" height="{p["height"]}" alt="{html.escape(p["title"])}">' for p in panels)
    preview = '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>ToolSwap — setup panels</title><style>html{scrollbar-gutter:stable}body{margin:0;background:#1b2838}main{width:min(620px,100%);margin:20px auto 48px}img{display:block;width:100%;height:auto;margin:0 0 20px}</style><main>' + images + '</main></html>'
    (ROOT / "preview.html").write_text(preview, encoding="utf-8")
    (ROOT / "autoarm-pairing.ini").write_text(";Add to the existing arm section. Keep other settings.\n[Arm 1]\nToolSwapPB=ToolSwap PB\n", encoding="utf-8")
    (ROOT / "toolswap-pairing.ini").write_text(";Add to the existing arm section. Keep other settings.\n[Arm 1]\nLink.ArmPB=AutoArm PB\nTools.Enabled=true\n", encoding="utf-8")
    publish = list((ROOT / "branding").glob("*.png")) + list(PANELS.glob("*.png"))
    publish += [(ROOT / name if (ROOT / name).exists() else DOCS / "steam-workshop/tool-swap" / name) for name in ("README.md", "autoarm-pairing.ini", "toolswap-pairing.ini", "generation-prompts.json", "correction-prompts.json", "manifest.json", "preview.html")]
    with zipfile.ZipFile(ROOT / "AutoArm-ToolSwap-Workshop-Pack.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for path in publish:
            archive.write(path, str(path.relative_to(ROOT)))
    print(json.dumps({"transparentPanels": len(panels), "brandingImages": len(manifest['branding']), "sizes": [(p['width'],p['height']) for p in panels]}))


if __name__ == "__main__":
    if "--art" in sys.argv:
        prepare_art()
    else:
        finish()
