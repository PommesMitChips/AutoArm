"""Scene-wide vector occlusion for the AutoArm and ToolSwap guide artwork.

The public integration point is add_scene(..., modeler, parts). It consumes the
existing posed native models and preserves their geometry, projection, palette
and outlined details. It changes only visibility and surface painting order.
"""

def scene_surfaces(scene, view, modeler, parts):
    """Flatten components before determining visibility or drawing order.

    Component origins are not a useful painter order: a long drill, hinge or
    rotor spans many depths. Keep each planar surface and its original edges
    separate so foreground surfaces can hide just the portion behind them.
    """
    camera = (0, 0, 1) if view == "side" else (1, 1, 1)
    primitives = []
    for item in scene:
        model = modeler(item, view)
        for surface in model.surfaces:
            if parts.dot(surface["normal"], camera) <= .001:
                continue
            surface = {**surface, "points": [parts.add(p, item["pos"]) for p in surface["points"]],
                       "component": item["part"], "role": item.get("role", item["part"])}
            primitives.append(surface)
            if "line" not in surface and surface.get("edge", True):
                # Splitting a face must not introduce false outlines across it.
                # Only the original perimeter gets a stroke; it is also split
                # by the scene's occluding planes.
                points = surface["points"]
                for a, b in zip(points, points[1:] + points[:1]):
                    primitives.append({**surface, "points": [a, b], "material": "ink", "line": 2.2})
    return primitives


def plane_order(primitives, parts, view):
    """BSP painter order for planar faces and edge segments.

    A surface that crosses another surface's plane is split geometrically.
    This handles actual occlusion instead of guessing from component origins
    or face centres, including the small drill teeth and hinge hardware.
    """
    import heapq
    camera = (0, 0, 1) if view == "side" else (1, 1, 1)
    epsilon = 1e-7

    def area(surface):
        points = surface["points"]
        if "line" in surface:
            return -1
        normal = surface["normal"]
        return abs(sum(parts.dot(normal, (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]))
                       for a, b in zip(points, points[1:] + points[:1]))) / 2

    def signed(points, normal, offset):
        return [parts.dot(p, normal) - offset for p in points]

    def plane(surface):
        # The source cylinder normals are smooth lighting normals. A tapered
        # facet's geometric normal differs slightly; using the lighting normal
        # as a partition plane would incorrectly split the facet itself.
        points = surface["points"]
        a = points[0]
        for b, c in zip(points[1:], points[2:]):
            u = tuple(x-y for x, y in zip(b, a))
            v = tuple(x-y for x, y in zip(c, a))
            normal = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
            if parts.dot(normal, normal) > 1e-14:
                normal = parts.unit(normal)
                if parts.dot(normal, surface["normal"]) < 0:
                    normal = tuple(-v for v in normal)
                return normal, parts.dot(a, normal)
        return surface["normal"], parts.dot(a, surface["normal"])

    def clean(points):
        result = []
        for point in points:
            if not result or sum((a-b)**2 for a, b in zip(point, result[-1])) > epsilon**2:
                result.append(point)
        if len(result) > 2 and sum((a-b)**2 for a, b in zip(result[0], result[-1])) < epsilon**2:
            result.pop()
        return result

    def split(surface, distances):
        points = surface["points"]
        if "line" in surface:
            a, b = points
            t = distances[0] / (distances[0] - distances[1])
            cut = tuple(x + t*(y-x) for x, y in zip(a, b))
            if distances[0] > 0:
                return ({**surface, "points": [a, cut]}, {**surface, "points": [cut, b]})
            return ({**surface, "points": [cut, b]}, {**surface, "points": [a, cut]})
        front, back = [], []
        for i, point in enumerate(points):
            nxt = (i + 1) % len(points)
            distance, next_distance = distances[i], distances[nxt]
            if distance >= -epsilon:
                front.append(point)
            if distance <= epsilon:
                back.append(point)
            if (distance > epsilon and next_distance < -epsilon) or (distance < -epsilon and next_distance > epsilon):
                t = distance / (distance - next_distance)
                cut = tuple(x + t*(y-x) for x, y in zip(point, points[nxt]))
                front.append(cut)
                back.append(cut)
        return ({**surface, "points": clean(front)}, {**surface, "points": clean(back)})

    def ordered(items):
        faces = [s for s in items if "line" not in s]
        if not faces:
            return sorted(items, key=lambda s: sum(parts.dot(p, camera) for p in s["points"]) / len(s["points"]))
        # Prefer broad supporting planes. Among them, choose one that avoids
        # unnecessary fragments and keeps the partition reasonably balanced.
        candidates = heapq.nlargest(8, faces, key=area)
        sample = items[::max(1, len(items)//200)]
        best, best_score = None, float("inf")
        for face in candidates:
            normal, offset = plane(face)
            front = back = crossing = 0
            for item in sample:
                distances = signed(item["points"], normal, offset)
                positive = max(distances) > epsilon
                negative = min(distances) < -epsilon
                front += positive
                back += negative
                crossing += positive and negative
            score = crossing*3 + abs(front-back)*.25
            if score < best_score:
                best, best_score = (normal, offset), score
        normal, offset = best
        front, back, coplanar = [], [], []
        for surface in items:
            distances = signed(surface["points"], normal, offset)
            positive = max(distances) > epsilon
            negative = min(distances) < -epsilon
            if positive and negative:
                a, b = split(surface, distances)
                front.append(a)
                back.append(b)
            elif positive:
                front.append(surface)
            elif negative:
                back.append(surface)
            else:
                coplanar.append(surface)
        # Every chosen face has a normal facing the orthographic camera.
        # Its negative half-space is therefore farther away.
        coplanar.sort(key=lambda s: ("line" in s, bool(s.get("decal"))))
        return ordered(back) + coplanar + ordered(front) if back or front else coplanar

    return ordered(primitives)


def add_scene(out, scene, view, scale, ox, oy, modeler, parts):
    for surface in plane_order(scene_surfaces(scene, view, modeler, parts), parts, view):
        points = []
        for point in surface["points"]:
            x, y = parts.projection(point, view)
            points.append(f"{ox+x*scale:g},{oy+y*scale:g}")
        attributes = f'data-component="{surface["component"]}" data-role="{surface["role"]}" data-part="{surface["part"]}"'
        if "line" in surface:
            out.append(f'<polyline {attributes} points="{" ".join(points)}" fill="none" '
                       f'stroke="{parts.PALETTE[surface["material"]]}" stroke-width="{min(1.4, surface["line"]):g}"/>')
        else:
            color = parts.PALETTE[parts.shade(surface["material"], surface["normal"])]
            # A small same-colour overlap prevents antialiasing seams between
            # adjacent cylinder facets; BSP edges are drawn independently.
            out.append(f'<polygon {attributes} points="{" ".join(points)}" fill="{color}" stroke="{color}" stroke-width=".45"/>')


