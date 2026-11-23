# Rebuilds the pixel Text (I-beam) cursor in the pack's pixel-art idiom:
# 1px ink outline + white fill + chunky stepped shoulders, centered and
# horizontally symmetric, with NO baked drop-shadow. The author's original
# Text.ani sits left-of-center and carries gray shadow pixels; a plain
# straight-bar redraw reads as a vector glyph, so this version keeps the
# blocky pixel shoulders that show real pixel grains when nearest-scaled.
# The other 16 cursors remain the author's (线框xiankuang) originals.
#
# Pure stdlib; writes a single-frame 32bpp .ani. Run from the repo root:
#   python tools/fix_pixel_ibeam.py [outFile]
import struct
import sys
from pathlib import Path

# B = ink outline, W = white fill, . = transparent. 8 wide x 14 tall (slim),
# centered in the 16x16 grid (symmetric about x=7.5). The top/bottom bars are
# one pixel narrower than the serif body so the corners form pixel staircases,
# and the shoulders step in to a 2px stem -- these steps read as "pixel art"
# once nearest-scaled, instead of a smooth vector glyph.
ROWS = [
    "................",
    ".....BBBBBB.....",
    "....BWWWWWWB....",
    "....BWWWWWWB....",
    "....BBBWWBBB....",
    "......BWWB......",
    "......BWWB......",
    "......BWWB......",
    "......BWWB......",
    "......BWWB......",
    "......BWWB......",
    "....BBBWWBBB....",
    "....BWWWWWWB....",
    "....BWWWWWWB....",
    ".....BBBBBB.....",
    "................",
]
W = H = 16
HOTX = HOTY = 8  # insertion point at the visual center
INK = (31, 40, 56)


def frame_bytes():
    stride = W * 4
    mstride = ((W + 31) // 32) * 4
    xor = bytearray(stride * H)
    andm = bytearray(mstride * H)
    for y in range(H):
        sy = H - 1 - y
        for x in range(W):
            ch = ROWS[y][x]
            if ch == "W":
                r = g = b = a = 255
            elif ch == "B":
                r, g, b = INK
                a = 255
            else:
                r = g = b = a = 0
            o = sy * stride + x * 4
            xor[o:o + 4] = bytes((b, g, r, a))
            if a < 128:
                andm[sy * mstride + (x >> 3)] |= 0x80 >> (x & 7)
    img = bytearray(40)
    struct.pack_into("<i", img, 0, 40)
    struct.pack_into("<i", img, 4, W)
    struct.pack_into("<i", img, 8, H * 2)
    struct.pack_into("<H", img, 12, 1)
    struct.pack_into("<H", img, 14, 32)
    struct.pack_into("<I", img, 20, len(xor) + len(andm))
    img += xor + andm
    entry = bytearray(22)
    struct.pack_into("<HHH", entry, 0, 0, 2, 1)
    entry[6] = W
    entry[7] = H
    struct.pack_into("<HH", entry, 10, HOTX, HOTY)
    struct.pack_into("<I", entry, 14, len(img))
    struct.pack_into("<I", entry, 18, 22)
    return bytes(entry + img)


def chunk(cid, data):
    return cid + struct.pack("<I", len(data)) + data + (b"\x00" if len(data) & 1 else b"")


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("assets/cursors/pixel/Text.ani")
    anih = struct.pack("<9I", 36, 1, 1, W, H, 32, 1, 5, 1)
    fram = b"fram" + chunk(b"icon", frame_bytes())
    body = b"ACON" + chunk(b"anih", anih) + chunk(b"LIST", fram)
    ani = b"RIFF" + struct.pack("<I", len(body)) + body
    out.write_bytes(ani)
    print("wrote centered chunky pixel I-beam ->", out)


if __name__ == "__main__":
    main()