#!/usr/bin/env python3
"""把 swallower_full.png 切成换装槽位。只依赖 Python 标准库。

原图像素头在右侧，和样例静息朝向 +X 一致，切片时不翻转。
关节处各留约 2 像素重叠。张嘴会露出的地方用邻近色填上，不留透明洞。
臀鳍单独成 parts/anal_fin.png，枢轴在肚皮上的鳍根，运行时挂到 SpineTail。
臀鳍的源像素只允许出现在这一张图里。尾不再吃掉它的前缘。
身体贴图在臀鳍那一块改成肚皮色，不把鳍的像素再画一遍。
下颌盖住的像素留在头上，填成口腔色。胸鳍在下颌下多盖 1 像素。
胸鳍仍是自己的部件。背鳍、胸鳍、尾鳍都绕自己的根，不把另一段的鳍带在身上。

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
    "Anal": "anal",
    "Pectoral": "pectoral",
    "Jaw": "jaw",
    "Head": "head",
    "Body": "body",
}

# 部件挂到哪根已有的骨头上。臀鳍跟着尾段，不另开骨头。
PART_BONES = {
    "Head": "Head",
    "Jaw": "Jaw",
    "Body": "SpineFront",
    "Dorsal": "Dorsal",
    "Pectoral": "Pectoral",
    "Anal": "SpineTail",
    "Tail": "Tail",
}

PART_FILES = {
    "Anal": "parts/anal_fin.png",
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


def is_anal_fin(x: int, y: int, rows: list[bytearray], width: int, height: int) -> bool:
    """臀鳍本体。前缘在 x=43 附近，不能让尾的下缘规则把它抢走。"""
    if not (0 <= x < width and 0 <= y < height) or not opaque(rows, x, y):
        return False
    # 垂下的那一叶，含原先被标成尾的 x=43、44。
    if 41 <= y <= 47 and 43 <= x <= 52:
        return True
    # 叶根上方的前缘。y<=40 且 x>=47 是肚皮，不是鳍。
    if 39 <= y <= 40 and 43 <= x <= 46:
        return True
    if y == 48 and 43 <= x <= 46:
        return True
    return False


def primary(x: int, y: int, rows: list[bytearray], width: int, height: int) -> str | None:
    if not (0 <= x < width and 0 <= y < height) or not opaque(rows, x, y):
        return None
    # 臀鳍先于尾。尾的下缘只到 x<=42，前缘留给臀鳍。
    if is_anal_fin(x, y, rows, width, height):
        return "Anal"
    # 叉形尾鳍在左侧，尾柄大约在 x=40。
    if x <= 40 or (x <= 44 and y <= 28) or (x <= 42 and y >= 40):
        return "Tail"
    if y <= 19 and 50 <= x <= 84:
        return "Dorsal"
    # 下缘剩下的两叶是胸鳍。腹鳍尖不要收进臀鳍。
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
                        if not (0 <= nx < width and 0 <= ny < height) or not opaque(rows, nx, ny):
                            continue
                        # 臀鳍源像素不扩进别的部件。
                        if name != "Anal" and is_anal_fin(nx, ny, rows, width, height):
                            continue
                        grown[name][ny][nx] = True
    # 下颌和胸鳍之间补一行，小角度张开时不露出 1 像素缝。
    for y in range(1, height):
        for x in range(78, width):
            if grown["Pectoral"][y][x] and opaque(rows, x, y - 1) and not grown["Pectoral"][y - 1][x]:
                if not is_anal_fin(x, y - 1, rows, width, height):
                    grown["Pectoral"][y - 1][x] = True
    # 下颌张开会离开的像素留在头上，张嘴才不会露出空背景。
    for y in range(height):
        for x in range(width):
            if primary(x, y, rows, width, height) == "Jaw":
                grown["Head"][y][x] = True
    return grown


MOUTH = (24, 44, 52, 255)
# 肚皮主色。臀鳍从身体贴图里抹掉后用它补上，重叠处不留洞。
BELLY = (168, 116, 58, 255)


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
                kind = primary(x, y, rows, width, height)
                # 头上被下颌盖住的地方填口腔色。下颌一转，这里还在。
                if name == "Head" and kind == "Jaw":
                    color = MOUTH
                # 身体蒙皮会扩进臀鳍。这里改成肚皮色，避免尾一摆出现第二片臀鳍。
                if name == "Body" and kind == "Anal" and y >= 38 and 42 <= x <= 55:
                    color = BELLY
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


def audit_anal(painted: dict[str, tuple], rows: list[bytearray], width: int, height: int) -> int:
    """每颗臀鳍源像素必须原色出现在且只出现在 anal 部件里。"""
    sources: dict[tuple[int, int], tuple[int, int, int, int]] = {}
    for y in range(height):
        for x in range(width):
            if is_anal_fin(x, y, rows, width, height):
                sources[(x, y)] = pixel(rows, x, y)
    found: dict[tuple[int, int], list[str]] = {xy: [] for xy in sources}
    for name, (min_x, min_y, _crop_w, _crop_h, image) in painted.items():
        for ly, row in enumerate(image):
            for lx in range(len(row) // 4):
                color = pixel_row(row, lx)
                if color[3] <= 16:
                    continue
                xy = (min_x + lx, min_y + ly)
                if xy in sources and color == sources[xy]:
                    found[xy].append(name)
    bad = [(xy, names) for xy, names in found.items() if names != ["Anal"]]
    if bad:
        sample = ", ".join(f"{xy}→{names}" for xy, names in bad[:8])
        raise SystemExit(f"臀鳍源像素没有只落在 anal 上：{len(bad)} 颗。例如 {sample}")
    print(f"anal audit {len(sources)} source pixels, each only in anal")
    return len(sources)


def pixel_row(row: bytearray, x: int) -> tuple[int, int, int, int]:
    offset = x * 4
    return tuple(row[offset : offset + 4])  # type: ignore[return-value]


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
        # 臀鳍根在肚皮上，和 SpineTail 同一列。
        "Anal": [48, 40],
        "Tail": [40, 33],
    }
    painted = {
        name: paint_part(name, rows, masks[name], width, height)
        for name in RULES
    }
    audit_anal(painted, rows, width, height)
    for name, (min_x, min_y, crop_w, crop_h, image) in painted.items():
        file_name = PART_FILES.get(name, f"parts/{name.lower()}.png")
        write_png(ROOT / file_name, crop_w, crop_h, [bytes(row) for row in image])
        pivot = pivots[name]
        parts[name] = {
            "file": file_name,
            "bone": PART_BONES[name],
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
        "note": "头在图像右侧。切片不翻转。背肌没有单独脊线，Ridge 槽位留空。臀鳍源像素只在 anal_fin.png。尾的下缘停在 x<=42。身体重叠处填肚皮色。胸鳍在下颌下多 1 像素。",
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
