#!/usr/bin/env python3
"""按剪影骨架的同一套变换，把吞食者画到洋红底上，数轮廓里的背景像素。

只依赖 Python 标准库。不进游戏运行时，也不进 src/ 或 plugins/。
在仓库根目录：

    python3 samples/CutoutSkeletonDemo/Art/Swallower/measure_holes.py

序列是 60 帧/秒、共 300 帧：游动循环，三次咬合从第 21、121、221 帧开始，
另外把尾鳍 Scale.X 收到正弦的两端各画一帧。轮廓是静息时各部件 alpha 的并集，
再向外扩 2 像素。单帧洞超过阈值就失败。
"""

from __future__ import annotations

import json
import math
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent
FPS = 60
FRAME_COUNT = 300
BITE_STARTS = (21, 121, 221)
BITE_LENGTH = 0.34
SWIM_LENGTH = 1.2
SINE_AMPLITUDE = 0.05
SINE_FREQUENCY = 1.35
SINE_PHASE_STEP = 0.72
SINE_BONES = ["Head", "SpineFront", "SpineMid", "SpineTail", "Tail", "Dorsal", "Pectoral"]
SINE_AMPLITUDE_SCALES = [0.0, 0.7, 1.05, 1.45, 1.9, 0.0, 0.0]
SINE_SCALE_X = [0.0, 0.05, 0.12, 0.28, 0.65, -0.07, -0.07]
SWIM_DORSAL = [(0.0, 0.0), (0.3, 0.18), (0.6, -0.1), (0.9, 0.16), (1.2, 0.0)]
SWIM_PECTORAL = [(0.0, 0.2), (0.3, -0.28), (0.6, 0.32), (0.9, -0.06), (1.2, 0.2)]
BITE_JAW = [(0.0, 0.0), (0.12, 0.72), (0.34, 0.0)]
Z_ORDER = ["Body", "Tail", "Anal", "Pectoral", "Dorsal", "Head", "Jaw"]
MAGENTA = (255, 0, 255, 255)
HOLE_LIMIT = 24
DILATE = 2


def read_png(path: Path) -> tuple[int, int, list[bytearray]]:
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"{path} 不是 PNG。")
    pos = 8
    width = height = None
    compressed = bytearray()
    while pos < len(data):
        length = struct.unpack(">I", data[pos : pos + 4])[0]
        kind = data[pos + 4 : pos + 8]
        chunk = data[pos + 8 : pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack(">IIBB", chunk[:10])
            if bit_depth != 8 or color_type != 6:
                raise SystemExit(f"{path} 需要 8 位 RGBA。")
        elif kind == b"IDAT":
            compressed.extend(chunk)
        elif kind == b"IEND":
            break
    raw = zlib.decompress(bytes(compressed))
    stride = width * 4
    rows: list[bytearray] = []
    i = 0
    prev = bytearray(stride)
    for _ in range(height):
        filt = raw[i]
        i += 1
        row = bytearray(raw[i : i + stride])
        i += stride
        if filt == 1:
            for x in range(stride):
                left = row[x - 4] if x >= 4 else 0
                row[x] = (row[x] + left) & 255
        elif filt == 2:
            for x in range(stride):
                row[x] = (row[x] + prev[x]) & 255
        elif filt == 3:
            for x in range(stride):
                left = row[x - 4] if x >= 4 else 0
                row[x] = (row[x] + ((left + prev[x]) // 2)) & 255
        elif filt == 4:
            for x in range(stride):
                left = row[x - 4] if x >= 4 else 0
                up = prev[x]
                ul = prev[x - 4] if x >= 4 else 0
                p = left + up - ul
                pa, pb, pc = abs(p - left), abs(p - up), abs(p - ul)
                pred = left if pa <= pb and pa <= pc else up if pb <= pc else ul
                row[x] = (row[x] + pred) & 255
        elif filt != 0:
            raise SystemExit(f"不认识的 PNG 过滤器 {filt}。")
        rows.append(row)
        prev = row
    return width, height, rows


def pixel(rows: list[bytearray], x: int, y: int) -> tuple[int, int, int, int]:
    offset = x * 4
    row = rows[y]
    return row[offset], row[offset + 1], row[offset + 2], row[offset + 3]


def lerp_keys(keys: list[tuple[float, float]], time: float) -> float:
    if time <= keys[0][0]:
        return keys[0][1]
    if time >= keys[-1][0]:
        return keys[-1][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t0 <= time <= t1:
            span = t1 - t0
            u = 0.0 if span == 0 else (time - t0) / span
            return v0 + ((v1 - v0) * u)
    return keys[-1][1]


def xform(rotation: float, scale: tuple[float, float], position: tuple[float, float]) -> tuple[float, ...]:
    cosine = math.cos(rotation)
    sine = math.sin(rotation)
    sx, sy = scale
    return (cosine * sx, sine * sx, -sine * sy, cosine * sy, position[0], position[1])


def compose(parent: tuple[float, ...], child: tuple[float, ...]) -> tuple[float, ...]:
    xx = (parent[0] * child[0]) + (parent[2] * child[1])
    xy = (parent[1] * child[0]) + (parent[3] * child[1])
    yx = (parent[0] * child[2]) + (parent[2] * child[3])
    yy = (parent[1] * child[2]) + (parent[3] * child[3])
    ox = parent[4] + (parent[0] * child[4]) + (parent[2] * child[5])
    oy = parent[5] + (parent[1] * child[4]) + (parent[3] * child[5])
    return (xx, xy, yx, yy, ox, oy)


def apply(transform: tuple[float, ...], x: float, y: float) -> tuple[float, float]:
    return (
        transform[4] + (transform[0] * x) + (transform[2] * y),
        transform[5] + (transform[1] * x) + (transform[3] * y),
    )


def invert_point(transform: tuple[float, ...], x: float, y: float) -> tuple[float, float]:
    dx = x - transform[4]
    dy = y - transform[5]
    det = (transform[0] * transform[3]) - (transform[2] * transform[1])
    lx = ((transform[3] * dx) - (transform[2] * dy)) / det
    ly = ((-transform[1] * dx) + (transform[0] * dy)) / det
    return lx, ly


class Rig:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.doc = json.loads((root / "slices.json").read_text(encoding="utf-8"))
        self.anchor = tuple(self.doc["anchor"]["image"])
        self.images: dict[str, tuple[int, int, list[bytearray]]] = {}
        self.parts: dict[str, dict] = {}
        for name, part in self.doc["parts"].items():
            self.parts[name] = part
            self.images[name] = read_png(root / part["file"])
        self.bones = {bone["name"]: bone for bone in self.doc["bones"]}
        self.parents = {name: bone["parent"] for name, bone in self.bones.items()}
        self.rest_local = {}
        for name, bone in self.bones.items():
            parent = bone["parent"]
            image = bone["image"]
            if parent is None:
                self.rest_local[name] = (0.0, 0.0)
            else:
                origin = self.bones[parent]["image"]
                self.rest_local[name] = (image[0] - origin[0], image[1] - origin[1])
        self.slot_position = {}
        for name, part in self.parts.items():
            pivot = part["pivot"]
            bone = part["bone"]
            size = self.images[name][0], self.images[name][1]
            local = (
                part["pivotInTexture"][0] + 0.5 - (size[0] * 0.5),
                part["pivotInTexture"][1] + 0.5 - (size[1] * 0.5),
            )
            centering = (-local[0], -local[1])
            bone_image = self.bones[bone]["image"]
            self.slot_position[name] = (
                pivot[0] - bone_image[0] + centering[0],
                pivot[1] - bone_image[1] + centering[1],
            )
        self.polygon = [tuple(point) for point in self.doc["bodyPolygon"]]
        self.triangles = self._triangles()
        self.weights = self._weights()
        crop = self.parts["Body"]["crop"]
        self.body_uv = [(point[0] - crop[0], point[1] - crop[1]) for point in self.polygon]

    def _triangles(self) -> list[tuple[int, int, int]]:
        # 轮廓是上沿从左到右，再下沿从右到左。按列成三角。
        count = len(self.polygon)
        half = count // 2
        triangles = []
        for index in range(half - 1):
            top0, top1 = index, index + 1
            bottom0, bottom1 = count - 1 - index, count - 2 - index
            triangles.append((top0, bottom0, top1))
            triangles.append((top1, bottom0, bottom1))
        return triangles

    def _weights(self) -> list[dict[str, float]]:
        names = ["SpineFront", "SpineMid", "SpineTail", "Tail"]
        centers = {name: self.bones[name]["image"][0] - self.anchor[0] for name in names}
        weights = []
        for point in self.polygon:
            local_x = point[0] - self.anchor[0]
            raw = {}
            for name in names:
                t = max(0.0, 1.0 - (abs(local_x - centers[name]) / 30.0))
                smooth = t * t * (3.0 - (2.0 * t))
                raw[name] = smooth
            total = sum(raw.values())
            if total <= 0.0001:
                raw = {names[0]: 1.0}
                total = 1.0
            weights.append({name: value / total for name, value in raw.items()})
        return weights

    def globals_for(self, rotations: dict[str, float], scales: dict[str, float]) -> dict[str, tuple[float, ...]]:
        root = (1.0, 0.0, 0.0, 1.0, float(self.anchor[0]), float(self.anchor[1]))
        done: dict[str, tuple[float, ...]] = {}

        def build(name: str) -> tuple[float, ...]:
            if name in done:
                return done[name]
            parent = self.parents[name]
            parent_global = root if parent is None else build(parent)
            local = xform(
                rotations.get(name, 0.0),
                (scales.get(name, 1.0), 1.0),
                self.rest_local[name],
            )
            done[name] = compose(parent_global, local)
            return done[name]

        for name in self.bones:
            build(name)
        return done

    def paint(self, rotations: dict[str, float], scales: dict[str, float], width: int, height: int, display_scale: float = 1.0) -> bytearray:
        image = bytearray(MAGENTA * (width * height))
        globals_ = self.globals_for(rotations, scales)
        anchor = (float(self.anchor[0]), float(self.anchor[1]))
        # 屏幕 = 锚点 * 显示缩放 + 显示缩放 * (骨架局部)。
        placed_root = (
            display_scale, 0.0, 0.0, display_scale,
            anchor[0] * display_scale, anchor[1] * display_scale,
        )
        # globals 已经含锚点平移。先去掉锚点，再乘显示缩放。
        def rooted(bone: tuple[float, ...]) -> tuple[float, ...]:
            unanchored = (
                bone[0], bone[1], bone[2], bone[3],
                bone[4] - anchor[0], bone[5] - anchor[1],
            )
            return compose(placed_root, unanchored)

        globals_ = {name: rooted(bone) for name, bone in globals_.items()}
        self._paint_body(image, width, height, globals_)
        for name in Z_ORDER:
            if name == "Body":
                continue
            self._paint_sprite(image, width, height, name, globals_[self.parts[name]["bone"]])
        return image

    def _paint_sprite(self, image: bytearray, width: int, height: int, name: str, bone: tuple[float, ...]) -> None:
        texture_w, texture_h, rows = self.images[name]
        position = self.slot_position[name]
        placed = compose(bone, (1.0, 0.0, 0.0, 1.0, position[0], position[1]))
        corners = [
            apply(placed, -texture_w / 2, -texture_h / 2),
            apply(placed, texture_w / 2, -texture_h / 2),
            apply(placed, texture_w / 2, texture_h / 2),
            apply(placed, -texture_w / 2, texture_h / 2),
        ]
        min_x = max(0, int(min(point[0] for point in corners)) - 1)
        max_x = min(width - 1, int(max(point[0] for point in corners)) + 1)
        min_y = max(0, int(min(point[1] for point in corners)) - 1)
        max_y = min(height - 1, int(max(point[1] for point in corners)) + 1)
        for y in range(min_y, max_y + 1):
            for x in range(min_x, max_x + 1):
                local_x, local_y = invert_point(placed, x + 0.5, y + 0.5)
                texel_x = math.floor(local_x + (texture_w / 2) - 0.5 + 1e-4)
                texel_y = math.floor(local_y + (texture_h / 2) - 0.5 + 1e-4)
                if not (0 <= texel_x < texture_w and 0 <= texel_y < texture_h):
                    continue
                color = pixel(rows, texel_x, texel_y)
                if color[3] <= 16:
                    continue
                offset = ((y * width) + x) * 4
                image[offset : offset + 4] = bytes(color)

    def _paint_body(self, image: bytearray, width: int, height: int, globals_: dict[str, tuple[float, ...]]) -> None:
        texture_w, texture_h, rows = self.images["Body"]
        rest_globals = self.globals_for({}, {})
        skinned = []
        for index, point in enumerate(self.polygon):
            accum_x = 0.0
            accum_y = 0.0
            for bone, weight in self.weights[index].items():
                if weight == 0.0:
                    continue
                current = globals_[bone]
                local_x, local_y = invert_point(rest_globals[bone], point[0], point[1])
                world_x, world_y = apply(current, local_x, local_y)
                accum_x += weight * world_x
                accum_y += weight * world_y
            skinned.append((accum_x, accum_y))
        # 静息变换只算一次会更干净，但顶点少，直接算。
        for a, b, c in self.triangles:
            self._paint_triangle(image, width, height, rows, texture_w, texture_h, skinned[a], skinned[b], skinned[c], self.body_uv[a], self.body_uv[b], self.body_uv[c])

    def _paint_triangle(self, image, width, height, rows, texture_w, texture_h, a, b, c, uva, uvb, uvc) -> None:
        min_x = max(0, int(min(a[0], b[0], c[0])) - 1)
        max_x = min(width - 1, int(max(a[0], b[0], c[0])) + 1)
        min_y = max(0, int(min(a[1], b[1], c[1])) - 1)
        max_y = min(height - 1, int(max(a[1], b[1], c[1])) + 1)
        denom = ((b[1] - c[1]) * (a[0] - c[0])) + ((c[0] - b[0]) * (a[1] - c[1]))
        if abs(denom) < 1e-6:
            return
        for y in range(min_y, max_y + 1):
            for x in range(min_x, max_x + 1):
                px = x + 0.5
                py = y + 0.5
                wa = (((b[1] - c[1]) * (px - c[0])) + ((c[0] - b[0]) * (py - c[1]))) / denom
                wb = (((c[1] - a[1]) * (px - c[0])) + ((a[0] - c[0]) * (py - c[1]))) / denom
                wc = 1.0 - wa - wb
                if wa < -1e-4 or wb < -1e-4 or wc < -1e-4:
                    continue
                uv_x = (wa * uva[0]) + (wb * uvb[0]) + (wc * uvc[0])
                uv_y = (wa * uva[1]) + (wb * uvb[1]) + (wc * uvc[1])
                texel_x = math.floor(uv_x)
                texel_y = math.floor(uv_y)
                if not (0 <= texel_x < texture_w and 0 <= texel_y < texture_h):
                    continue
                color = pixel(rows, texel_x, texel_y)
                if color[3] <= 16:
                    continue
                offset = ((y * width) + x) * 4
                image[offset : offset + 4] = bytes(color)


def pose_at(frame: int) -> tuple[dict[str, float], dict[str, float], str]:
    time = frame / FPS
    swim_time = time % SWIM_LENGTH
    rotations = {
        "Dorsal": lerp_keys(SWIM_DORSAL, swim_time),
        "Pectoral": lerp_keys(SWIM_PECTORAL, swim_time),
    }
    label = "swim"
    for start in BITE_STARTS:
        local = (frame - start) / FPS
        if 0 <= local <= BITE_LENGTH:
            rotations["Jaw"] = lerp_keys(BITE_JAW, local)
            # 咬合片段只写颌。胸鳍和背鳍停在切入时的游动角度。
            frozen = (start / FPS) % SWIM_LENGTH
            rotations["Dorsal"] = lerp_keys(SWIM_DORSAL, frozen)
            rotations["Pectoral"] = lerp_keys(SWIM_PECTORAL, frozen)
            label = f"bite@{start}"
            break
    scales = {}
    for index, name in enumerate(SINE_BONES):
        phase = (time * SINE_FREQUENCY * math.tau) + (index * SINE_PHASE_STEP)
        if SINE_AMPLITUDE_SCALES[index] != 0.0:
            rotations[name] = SINE_AMPLITUDE * SINE_AMPLITUDE_SCALES[index] * math.sin(phase)
        squeeze = 0.5 + (0.5 * math.sin(phase))
        scales[name] = 1.0 - (SINE_SCALE_X[index] * squeeze)
    return rotations, scales, label


def coverage(image: bytearray, width: int, height: int) -> bytearray:
    mask = bytearray(width * height)
    for index in range(width * height):
        if image[(index * 4) + 3] > 16 and image[index * 4 : index * 4 + 3] != bytes(MAGENTA[:3]):
            mask[index] = 1
        elif image[index * 4 : index * 4 + 3] != bytes(MAGENTA[:3]) and image[(index * 4) + 3] > 16:
            mask[index] = 1
    # 洋红只作空背景。不透明且不是洋红才算鱼。上面的判断重复了，保留 alpha。
    for index in range(width * height):
        offset = index * 4
        opaque = image[offset + 3] > 16 and image[offset : offset + 3] != bytes(MAGENTA[:3])
        mask[index] = 1 if opaque else 0
    return mask


def dilate(mask: bytearray, width: int, height: int, radius: int) -> bytearray:
    grown = bytearray(mask)
    for y in range(height):
        for x in range(width):
            if not mask[(y * width) + x]:
                continue
            for dy in range(-radius, radius + 1):
                for dx in range(-radius, radius + 1):
                    if max(abs(dx), abs(dy)) > radius:
                        continue
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < width and 0 <= ny < height:
                        grown[(ny * width) + nx] = 1
    return grown


def hole_pixels(image: bytearray, silhouette: bytearray, width: int, height: int) -> list[tuple[int, int]]:
    """轮廓内、紧挨着上下或左右都有鱼的洋红像素。鳍甩开后的大块空位不算。"""
    fish = bytearray(width * height)
    for index in range(width * height):
        if image[index * 4 : index * 4 + 3] != bytes(MAGENTA[:3]):
            fish[index] = 1
    holes = []
    for y in range(height):
        for x in range(width):
            index = (y * width) + x
            if not silhouette[index] or fish[index]:
                continue

            def hits(dx: int, dy: int) -> bool:
                for step in range(1, 2):
                    nx, ny = x + (dx * step), y + (dy * step)
                    if not (0 <= nx < width and 0 <= ny < height):
                        return False
                    if fish[(ny * width) + nx]:
                        return True
                return False

            if (hits(0, -1) and hits(0, 1)) or (hits(-1, 0) and hits(1, 0)):
                holes.append((x, y))
    return holes


def summarize(holes: list[tuple[int, int]]) -> str:
    if not holes:
        return "0"
    xs = [x for x, _ in holes]
    ys = [y for _, y in holes]
    front = sum(1 for x, _ in holes if x >= 70)
    mid = sum(1 for x, _ in holes if 45 <= x < 70)
    tail = sum(1 for x, _ in holes if x < 45)
    return f"{len(holes)} front={front} mid={mid} tail={tail} bbox=[{min(xs)},{min(ys)}]-[{max(xs)},{max(ys)}]"


def main() -> None:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT
    rig = Rig(root)
    width, height = 140, 90
    rest_image = rig.paint({}, {}, width, height)
    silhouette = dilate(coverage(rest_image, width, height), width, height, DILATE)
    worst = 0
    worst_frame = -1
    worst_holes: list[tuple[int, int]] = []
    reported = []
    bite_peaks = {start: 0 for start in BITE_STARTS}
    for frame in range(FRAME_COUNT):
        rotations, scales, label = pose_at(frame)
        image = rig.paint(rotations, scales, width, height)
        holes = hole_pixels(image, silhouette, width, height)
        count = len(holes)
        if count > worst:
            worst = count
            worst_frame = frame
            worst_holes = holes
        if count > HOLE_LIMIT:
            reported.append((frame, label, count, holes))
        for start in BITE_STARTS:
            local = frame - start
            if 0 <= local <= int(BITE_LENGTH * FPS):
                bite_peaks[start] = max(bite_peaks[start], count)
    print(f"frames {FRAME_COUNT}  max {worst} at frame {worst_frame}  {summarize(worst_holes)}")
    print("bite peaks " + " ".join(f"{start}:{bite_peaks[start]}" for start in BITE_STARTS))
    shown = 0
    for frame, label, count, holes in reported:
        if shown >= 12:
            break
        print(f"  frame {frame} {label} {summarize(holes)}")
        shown += 1
    tail_extremes = []
    for squeeze, name in ((0.0, "tail-open"), (1.0, "tail-flat")):
        scales = {}
        for index, bone in enumerate(SINE_BONES):
            scales[bone] = 1.0 - (SINE_SCALE_X[index] * squeeze)
        image = rig.paint({}, scales, width, height)
        holes = hole_pixels(image, silhouette, width, height)
        tail_extremes.append((name, len(holes)))
        print(f"  {name} {summarize(holes)}")
    if worst > HOLE_LIMIT or any(count > HOLE_LIMIT for _, count in tail_extremes):
        raise SystemExit(f"洞超过 {HOLE_LIMIT}px。")
    print(f"hole audit max {worst}px, under {HOLE_LIMIT}")


if __name__ == "__main__":
    main()
