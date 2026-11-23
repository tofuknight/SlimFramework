#!/usr/bin/env python3
"""把 swallower_full.png 切成换装槽位。只依赖 Python 标准库。

原图像素头在右侧，和样例静息朝向 +X 一致，切片时不翻转。
关节处各留约 2 像素重叠。张嘴会露出的地方用邻近色填上，不留透明洞。

用法（在仓库根目录）：

    python3 samples/CutoutSkeletonDemo/Art/Swallower/slice_swallower.py

会重写 parts/ 和 slices.json。样例运行时只读这些结果，不调用本脚本。
"""

from __future__ import annotations

import json
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent
FULL = ROOT / "swallower_full.png"
PARTS = ROOT / "parts"

# 头在 +X。SpineFront 是骨架原点，对应这颗像素。
ANCHOR = (80, 32)

# (x0, y0, x1, y1) 含边界。重叠写在规则里，不写在这张表的互斥上。
# 背肌这一条没有单独的脊线，槽位留空。
RULES = {
    "Tail": "caudal",
    "Dorsal": "dorsal",
    "Pectoral": "pectoral",
    "Jaw": "jaw",
    "Head": "head",
    "Body": "body",
}


def read_png(path: Path) -> tuple[int, int, list[bytearray]]:
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"{path} 不是 PNG。")
    index = 8
    width = height = 0
    idat = b""
    while index < len(data):
        length = struct.unpack(">I", data[index : index + 4])[0]
        kind = data[index + 4 : index + 8]
        chunk = data[index + 8 : index + 8 + length]
        if kind == b"IHDR":
            width, height, bit, color, *_ = struct.unpack(">IIBBBBB", chunk)
            if (bit, color) != (8, 6):
                raise SystemExit(f"只接受 8 位 RGBA，得到 bit={bit} color={color}。")
        elif kind == b"IDAT":
            idat += chunk
        elif kind == b"IEND":
            break
        index += 12 + length
    raw = zlib.decompress(idat)
    rows: list[bytearray] = []
    cursor = 0
    stride = width * 4
    previous = bytearray(stride)
    for _ in range(height):
        filter_type = raw[cursor]
        cursor += 1
        row = bytearray(raw[cursor : cursor + stride])
        cursor += stride
        for x in range(stride):
            left = row[x - 4] if x >= 4 else 0
            up = previous[x]
            upper_left = previous[x - 4] if x >= 4 else 0
            if filter_type == 0:
                value = row[x]
            elif filter_type == 1:
                value = (row[x] + left) & 255
            elif filter_type == 2:
                value = (row[x] + up) & 255
            elif filter_type == 3:
                value = (row[x] + ((left + up) // 2)) & 255
            elif filter_type == 4:
                estimate = left + up - upper_left
                choices = (
                    (abs(estimate - left), left),
                    (abs(estimate - up), up),
                    (abs(estimate - upper_left), upper_left),
                )
                value = (row[x] + min(choices)[1]) & 255
            else:
                raise SystemExit(f"不认识的 PNG 滤波器 {filter_type}。")
            row[x] = value
        rows.append(row)
        previous = row
    return width, height, rows


def write_png(path: Path, width: int, height: int, rows: list[bytes]) -> None:
    raw = b"".join(b"\x00" + row for row in rows)

    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    path.write_bytes(png)


def pixel(rows: list[bytearray], x: int, y: int) -> tuple[int, int, int, int]:
    offset = x * 4
    return tuple(rows[y][offset : offset + 4])  # type: ignore[return-value]


def opaque(rows: list[bytearray], x: int, y: int) -> bool:
    return pixel(rows, x, y)[3] > 16


def primary(x: int, y: int, rows: list[bytearray], width: int, height: int) -> str | None:
    if not (0 <= x < width and 0 <= y < height) or not opaque(rows, x, y):
        return None
    # 叉形尾鳍在左侧，尾柄大约在 x=40。
    if x <= 40 or (x <= 44 and (y <= 28 or y >= 40)):
        return "Tail"
    if y <= 19 and 50 <= x <= 84:
        return "Dorsal"
    if y >= 41 and 40 <= x <= 90:
        return "Pectoral"
    if x >= 84 and 32 <= y <= 40:
        return "Jaw"
    if x >= 84:
        return "Head"
    return "Body"


def build_masks(width: int, height: int, rows: list[bytearray]) -> dict[str, list[list[bool]]]:
    masks = {name: [[False] * width for _ in range(height)] for name in RULES}
    for y in range(height):
        for x in range(width):
            name = primary(x, y, rows, width, height)
            if name is not None:
                masks[name][y][x] = True

    # 每个部位向外扩 2 像素，重叠区抄原图像素。
    grown = {name: [row[:] for row in mask] for name, mask in masks.items()}
    for name, mask in masks.items():
        for y in range(height):
            for x in range(width):
                if not mask[y][x]:
                    continue
                for dy in range(-2, 3):
                    for dx in range(-2, 3):
                        if max(abs(dx), abs(dy)) > 2:
                            continue
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < width and 0 <= ny < height and opaque(rows, nx, ny):
                            grown[name][ny][nx] = True
    return grown


MOUTH = (24, 44, 52, 255)


def nearest_color(rows: list[bytearray], mask: list[list[bool]], x: int, y: int, width: int, height: int):
    for radius in range(1, 8):
        for dy in range(-radius, radius + 1):
            for dx in range(-radius, radius + 1):
                if max(abs(dx), abs(dy)) != radius:
                    continue
                nx, ny = x + dx, y + dy
                if 0 <= nx < width and 0 <= ny < height and mask[ny][nx]:
                    return pixel(rows, nx, ny)
    return MOUTH


def paint_part(name: str, rows: list[bytearray], mask: list[list[bool]], width: int, height: int):
    pixels = [(x, y) for y in range(height) for x in range(width) if mask[y][x]]
    if not pixels:
        raise SystemExit(f"{name} 没有像素。")
    min_x = min(x for x, _ in pixels)
    min_y = min(y for _, y in pixels)
    max_x = max(x for x, _ in pixels)
    max_y = max(y for _, y in pixels)
    # 奇数尺寸让居中后的像素中心落在整数上。
    if (max_x - min_x + 1) % 2 == 0:
        if min_x > 0:
            min_x -= 1
        else:
            max_x += 1
    if (max_y - min_y + 1) % 2 == 0:
        if min_y > 0:
            min_y -= 1
        else:
            max_y += 1
    crop_w = max_x - min_x + 1
    crop_h = max_y - min_y + 1
    out = [bytearray(crop_w * 4) for _ in range(crop_h)]
    for y in range(min_y, max_y + 1):
        for x in range(min_x, max_x + 1):
            if not (0 <= x < width and 0 <= y < height):
                continue
            local_x = x - min_x
            local_y = y - min_y
            offset = local_x * 4
            if mask[y][x]:
                color = pixel(rows, x, y)
                # 头的下沿在闭嘴时被下颌盖住。这里填口腔色，张开才不会穿帮。
                if name == "Head" and y >= 32 and primary(x, y, rows, width, height) == "Jaw":
                    color = MOUTH
                out[local_y][offset : offset + 4] = bytes(color)
    return min_x, min_y, crop_w, crop_h, out


def body_polygon(mask: list[list[bool]], width: int, height: int) -> list[list[int]]:
    columns = []
    for x in range(width):
        ys = [y for y in range(height) if mask[y][x]]
        if ys:
            columns.append((x, min(ys), max(ys)))
    if len(columns) < 4:
        raise SystemExit("身体轮廓太短。")
    step = 5
    sampled = columns[::step]
    if sampled[-1] != columns[-1]:
        sampled.append(columns[-1])
    top = [[x, top_y] for x, top_y, _ in sampled]
    bottom = [[x, bottom_y] for x, _, bottom_y in reversed(sampled)]
    polygon = top + bottom
    # 去掉过近的重复点。
    unique = [polygon[0]]
    for point in polygon[1:]:
        if abs(point[0] - unique[-1][0]) + abs(point[1] - unique[-1][1]) >= 2:
            unique.append(point)
    return unique


def main() -> None:
    width, height, rows = read_png(FULL)
    masks = build_masks(width, height, rows)
    PARTS.mkdir(exist_ok=True)
    parts = {}
    pivots = {
        "Head": [84, 30],
        "Jaw": [86, 32],
        "Body": list(ANCHOR),
        "Dorsal": [66, 20],
        "Pectoral": [64, 40],
        "Tail": [40, 33],
    }
    for name in RULES:
        min_x, min_y, crop_w, crop_h, image = paint_part(name, rows, masks[name], width, height)
        file_name = f"parts/{name.lower()}.png"
        write_png(PARTS / f"{name.lower()}.png", crop_w, crop_h, [bytes(row) for row in image])
        pivot = pivots[name]
        parts[name] = {
            "file": file_name,
            "crop": [min_x, min_y, crop_w, crop_h],
            "pivot": pivot,
            "pivotInTexture": [pivot[0] - min_x, pivot[1] - min_y],
        }
        print(f"{name:10} crop={parts[name]['crop']} pivot={pivot} tex={parts[name]['pivotInTexture']}")

    bones = [
        ["SpineFront", None, list(ANCHOR)],
        ["Head", "SpineFront", pivots["Head"]],
        ["Jaw", "Head", pivots["Jaw"]],
        ["Ridge", "SpineFront", [68, 20]],
        ["Dorsal", "SpineFront", pivots["Dorsal"]],
        ["Pectoral", "SpineFront", pivots["Pectoral"]],
        ["SpineMid", "SpineFront", [62, 32]],
        ["SpineTail", "SpineMid", [48, 33]],
        ["Tail", "SpineTail", pivots["Tail"]],
    ]
    document = {
        "source": "swallower_full.png",
        "size": [width, height],
        "facing": "+X",
        "anchor": {"bone": "SpineFront", "image": list(ANCHOR)},
        "note": "头在图像右侧。切片不翻转。背肌没有单独脊线，Ridge 槽位留空。",
        "parts": parts,
        "bones": [{"name": name, "parent": parent, "image": image} for name, parent, image in bones],
        "bodyPolygon": body_polygon(masks["Body"], width, height),
    }
    (ROOT / "slices.json").write_text(
        json.dumps(document, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8")
    print(f"polygon {len(document['bodyPolygon'])} points")


if __name__ == "__main__":
    main()
