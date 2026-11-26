"""Bake evaluated Blender meshes into the Unity ship importer's grouped vertex format."""
import gzip
import hashlib
import struct
from collections import defaultdict
from pathlib import Path

import bpy
import numpy as np


AXIS = np.array(((0, -1, 0), (0, 0, 1), (1, 0, 0)))
VERTEX = np.dtype([("p", "<f4", 3), ("n", "<i2", 3), ("c", "u1", 4), ("uv", "<i2", 2)])


def source_identity(source):
    with source.open("rb") as stream:
        return {"sourceModel": source.name, "sourceSha256": hashlib.file_digest(stream, "sha256").hexdigest()}


def ladder_name(obj):
    side = "Port" if obj["side"] > 0 else "Starboard"
    return f'BoardingLadder_{side}_{obj["梯中心 X"]:.2f}'


def tread_half_widths(points, edges, levels, side, station, name):
    outer, inner = [], []
    for height in levels:
        at_height = np.abs(points[:, 2] - height) < .001
        neighbours = {index: [] for index in np.flatnonzero(at_height)}
        for a, b in edges[np.all(at_height[edges], axis=1)]:
            neighbours[a].append(b)
            neighbours[b].append(a)
        boards = []
        while neighbours:
            pending = [next(iter(neighbours))]
            vertices = []
            while pending:
                index = pending.pop()
                if index in neighbours:
                    pending.extend(neighbours.pop(index))
                    vertices.append(index)
            surface = points[vertices]
            assert len(vertices) > 1, (name, height)
            half_width = min(station - min(surface[:, 0]), max(surface[:, 0]) - station)
            boards.append((side * np.mean(surface[:, 1]), half_width))
        # At shared heights the detached exterior and interior boards span different widths.
        boards.sort()
        assert 1 <= len(boards) <= 2 and all(width > 0 for _, width in boards), (name, height)
        outer.append(boards[-1][1])
        if len(boards) == 2:
            inner.append(boards[0][1])
    assert inner, name
    return round(float(min(outer)), 5), round(float(min(inner)), 5)


def boarding_ladder(obj, cap, deck_height, baseline):
    """Measure the tread run and the continuous cap that the sailor steps over."""
    side = obj["side"]
    station, crossing = obj["梯中心 X"], obj["越舷线 X"]
    points = np.asarray([obj.matrix_world @ v.co for v in obj.data.vertices])
    tread_edges = np.asarray([tuple(edge.vertices) for edge in obj.data.edges])
    treads = sorted(obj["舷外踏面 Z"])
    # Blender X is fore/aft, corresponding to the ladder's width along Unity Z.
    half_width, inner_half_width = tread_half_widths(points, tread_edges, treads, side, station, obj.name)
    edges = [points[np.abs(points[:, 2] - height) < .001, 1] * side for height in treads]

    # Slice the authored cap at the crossing, including slopes along the hull.
    verts = np.asarray([cap.matrix_world @ v.co for v in cap.data.vertices])
    ends = verts[np.asarray([tuple(edge.vertices) for edge in cap.data.edges])]
    start, end = ends[:, 0], ends[:, 1]
    cross = ((start[:, 0] - crossing) * (end[:, 0] - crossing) <= 0) & (abs(end[:, 0] - start[:, 0]) > 1e-6)
    start, end = start[cross], end[cross]
    f = (crossing - start[:, 0]) / (end[:, 0] - start[:, 0])
    cap_height = max(start[:, 2] + f * (end[:, 2] - start[:, 2]))
    low, high = treads[0], cap_height + .12
    assert high > treads[-1], obj.name

    # A straight, upright sailor has to clear the flaring hull even with their
    # feet on the lowest, recessed rung. Fit the exterior line to that envelope.
    outside = [max(edge) + .10 for edge in edges]
    top_x = max(outside)
    bottom_x = outside[0]
    for z, width in zip(treads, outside):
        progress = max(0., z - 1.56 - low) / (high - low)
        bottom_x = max(bottom_x, (width - top_x * progress) / (1. - progress))
    # The uppermost tread level includes both the outside and inside steps.
    inside_x = min(edges[-1]) - .40
    def point(x, height, longitudinal):
        return dict(x=round(float(-side * x), 5), y=round(float(baseline + height), 5),
                    z=round(float(longitudinal), 5))
    return dict(name=ladder_name(obj), halfWidth=half_width, innerHalfWidth=inner_half_width,
                bottom=point(bottom_x, low, station),
                top=point(top_x, high, crossing), landing=point(inside_x, deck_height(crossing, inside_x), crossing))


def _read(collection, prop, width, dtype):
    data = np.empty((len(collection), width), dtype=dtype)
    collection.foreach_get(prop, data.ravel())
    return data


def _paint(material):
    if not material:
        return (130, 100, 80, 0)
    name = material.name.lower()
    rgb = np.clip(material.diffuse_color[:3], 0, 1)
    srgb = np.where(rgb <= .0031308, 12.92 * rgb, 1.055 * np.power(rgb, 1 / 2.4) - .055)
    kind = 0
    if any(word in name for word in ("oak", "wood", "plank", "walnut", "ebonized", "wales", "ochre", "木", "橡")):
        kind = 85
    elif any(word in name for word in ("canvas", "linen", "hemp", "wool", "cloth", "flag", "帆", "绳", "旗", "麻")):
        kind = 170
    elif any(word in name for word in ("iron", "brass", "bronze", "铁", "铜", "金属")):
        kind = 255
    return (*np.rint(srgb * 255).astype(int), kind)


class ShipMesh:
    def __init__(self, offset=(0, 0, 0)):
        self.offset = np.asarray(offset)
        self.groups = defaultdict(list)
        self.pivots = {}

    def _append(self, group, points, normals, colors, uv, triangles, pivot):
        pivot = tuple(pivot)
        if self.pivots.setdefault(group, pivot) != pivot:
            raise ValueError(f"Inconsistent pivot on {group}")
        vertices = np.empty(len(points), dtype=VERTEX)
        vertices["p"] = points - pivot
        vertices["n"] = np.rint(normals * 32767).astype("<i2")
        vertices["c"] = colors
        vertices["uv"] = np.clip(np.rint(uv * 500), -32767, 32767)
        packed, inverse = np.unique(vertices, return_inverse=True)
        self.groups[group].append((packed, inverse[triangles].astype("<u4")))

    def add_object(self, obj, group, pivot=(0, 0, 0), materials=None, uv_layer=None):
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        try:
            if not mesh or not mesh.polygons:
                return
            mesh.calc_loop_triangles()
            matrix = np.asarray(evaluated.matrix_world)
            linear = AXIS @ matrix[:3, :3]
            coords = _read(mesh.vertices, "co", 3, "f4") @ linear.T + AXIS @ matrix[:3, 3] + self.offset
            normals = _read(mesh.corner_normals, "vector", 3, "f4") @ np.linalg.inv(linear)
            normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-8)
            points = coords[_read(mesh.loops, "vertex_index", 1, "i4")[:, 0]]
            triangles = _read(mesh.loop_triangles, "loops", 3, "i4")
            if np.linalg.det(linear) < 0:
                triangles = triangles[:, [0, 2, 1]]
            palette = [_paint(mat) for mat in (mesh.materials if materials is None else materials)] or [_paint(None)]
            indices = _read(mesh.loop_triangles, "material_index", 1, "i4")[:, 0]
            colors = np.empty((len(points), 4), "u1")
            colors[triangles.ravel()] = np.repeat(np.asarray(palette)[indices], 3, axis=0)
            if uv_layer is not None:
                uv = _read(mesh.uv_layers[uv_layer].uv, "vector", 2, "f4")
            else:
                axis = 2 if group.startswith(("HULL_", "INNER_", "D0_", "D1_", "D2_", "D3_", "D4", "D5_")) else int(np.argmax(np.ptp(coords, axis=0)))
                cross = [i for i in range(3) if i != axis]
                uv = np.column_stack((points[:, axis], points[:, cross[0]] + .87 * points[:, cross[1]]))
            self._append(group, points, normals, colors, uv, triangles, pivot)
        finally:
            evaluated.to_mesh_clear()

    def add_geometry(self, group, vertices, triangles, pivot=(0, 0, 0)):
        points = np.asarray(vertices, dtype=float).reshape(-1, 3)
        faces = np.asarray(triangles, dtype=int).reshape(-1, 3)
        n = np.cross(points[faces[:, 1]] - points[faces[:, 0]], points[faces[:, 2]] - points[faces[:, 0]])
        normals = np.zeros_like(points)
        for corner in range(3):
            np.add.at(normals, faces[:, corner], n)
        normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-8)
        self._append(group, points, normals, (140, 110, 75, 0), np.zeros((len(points), 2)), faces, pivot)

    def write(self, path):
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("wb") as source, gzip.GzipFile(fileobj=source, mode="wb", compresslevel=5, mtime=0) as stream:
            stream.write(struct.pack("<4sII", b"R13M", 1, len(self.groups)))
            for name, parts in sorted(self.groups.items()):
                key = name.encode("utf8")
                stream.write(struct.pack("<I", len(key)) + key)
                stream.write(struct.pack("<fffII", *self.pivots[name], sum(len(v) for v, _ in parts), sum(t.size for _, t in parts)))
                for vertices, _ in parts:
                    stream.write(vertices.tobytes())
                offset = 0
                for vertices, triangles in parts:
                    stream.write((triangles + offset).astype("<u4").tobytes())
                    offset += len(vertices)
