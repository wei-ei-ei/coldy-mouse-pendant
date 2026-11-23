# Generates the Glacier cursor SVG design sources in assets/cursors/svg/.
# The geometry mirrors CursorFactory in src/Program.cs (32-unit grid, u = 1);
# the C# code remains the canonical runtime renderer. Keep the constants in
# sync when editing either side.
#
# Run from the repo root:  python tools/make_cursor_svg.py [outputDir]

import math
import os
import sys

INK = "#1F2838"
ICE_HI = "#F8FEFF"
ICE_MID = "#D8F3FF"
ICE_LO = "#9EDCF5"
RED = "#E2574B"
CUFF_ICE = "#C5ECFF"

DEFS = (
    '<defs><linearGradient id="ice" x1="0" y1="0" x2="0.42" y2="0.91">'
    f'<stop offset="0" stop-color="{ICE_HI}"/>'
    f'<stop offset="0.45" stop-color="{ICE_MID}"/>'
    f'<stop offset="1" stop-color="{ICE_LO}"/>'
    "</linearGradient>{clip}</defs>"
)

ARROW = "M4 2.6 L4 23 L9.4 18.4 L12.6 26.2 L16.8 24.4 L13.6 16.9 L20.6 16.9 Z"
UP = "M16 2.8 L23.2 11.6 L18.7 11.6 L18.7 27.6 L13.3 27.6 L13.3 11.6 L8.8 11.6 Z"
HAND = (
    "M10.7 13.8 L10.7 5.1 A2.3 2.3 0 0 1 15.3 5.1 L15.3 11.9 "
    "A2.2 2.2 0 0 1 19.7 11.9 L19.7 12.6 A2.1 2.1 0 0 1 23.9 12.5 "
    "L23.9 13.4 A2 2 0 0 1 27.9 13.4 L27.9 19.6 "
    "C27.9 24.3 26.2 28.4 22.3 28.4 L14.2 28.4 "
    "C11 28.4 9.2 26.6 7.9 23.9 C6.9 21.8 5.2 19.3 5.2 17.4 "
    "A2.3 2.3 0 0 1 9.26 15.92 Z"
)
RESIZE = (
    "M3.4 16 L9.8 11.2 L9.8 14.5 L22.2 14.5 L22.2 11.2 L28.6 16 "
    "L22.2 20.8 L22.2 17.5 L9.8 17.5 L9.8 20.8 Z"
)
MOVE_PTS = [
    (0, -12.6), (-4.8, -6.2), (-1.5, -6.2), (-1.5, -1.5),
    (-6.2, -1.5), (-6.2, -4.8), (-12.6, 0), (-6.2, 4.8),
    (-6.2, 1.5), (-1.5, 1.5), (-1.5, 6.2), (-4.8, 6.2),
    (0, 12.6), (4.8, 6.2), (1.5, 6.2), (1.5, 1.5),
    (6.2, 1.5), (6.2, 4.8), (12.6, 0), (6.2, -4.8),
    (6.2, -1.5), (1.5, -1.5), (1.5, -6.2), (4.8, -6.2),
]
MOVE = "M" + " L".join(f"{16 + x:g} {16 + y:g}" for x, y in MOVE_PTS) + " Z"


def flake(cx, cy, r):
    segs = []
    for k in range(6):
        a = math.radians(k * 60 - 90)
        dx, dy = math.cos(a), math.sin(a)
        segs.append((cx, cy, cx + dx * r, cy + dy * r))
        bx, by = cx + dx * r * 0.60, cy + dy * r * 0.60
        for s in (-1, 1):
            ba = a + s * math.radians(48)
            segs.append((bx, by, bx + math.cos(ba) * r * 0.38, by + math.sin(ba) * r * 0.38))
    return " ".join(f"M{p[0]:.2f} {p[1]:.2f} L{p[2]:.2f} {p[3]:.2f}" for p in segs)


def filled(d, shadow=False, detail="", clip=""):
    out = []
    if shadow:
        out.append(
            f'<path d="{d}" transform="translate(1,1.5)" fill="{INK}" fill-opacity="0.16" '
            f'stroke="{INK}" stroke-opacity="0.16" stroke-width="4.7" stroke-linejoin="round"/>'
        )
    out.append(f'<path d="{d}" fill="none" stroke="#FFFFFF" stroke-width="4.7" stroke-linejoin="round"/>')
    out.append(f'<path d="{d}" fill="url(#ice)"/>')
    if detail:
        out.append(f'<g clip-path="url(#body)">{detail}</g>')
    out.append(f'<path d="{d}" fill="none" stroke="{INK}" stroke-width="1.7" stroke-linejoin="round"/>')
    return "".join(out), clip


def stroked(d, core, color=INK):
    return (
        f'<path d="{d}" fill="none" stroke="#FFFFFF" stroke-width="{core + 3.2:.1f}" '
        'stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{core:.1f}" '
        'stroke-linecap="round" stroke-linejoin="round"/>'
    )


def ice_stroked(d, core):
    return (
        f'<path d="{d}" fill="none" stroke="#FFFFFF" stroke-width="{core + 4.8:.1f}" '
        'stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="{d}" fill="none" stroke="{INK}" stroke-width="{core + 2:.1f}" '
        'stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="{d}" fill="none" stroke="url(#ice)" stroke-width="{core:.1f}" '
        'stroke-linecap="round" stroke-linejoin="round"/>'
    )


def dot(cx, cy, r, halo=True):
    out = ""
    if halo:
        out += f'<circle cx="{cx}" cy="{cy}" r="{r + 2.1:.1f}" fill="#FFFFFF"/>'
    out += f'<circle cx="{cx}" cy="{cy}" r="{r + 0.9:.1f}" fill="{INK}"/>'
    out += f'<circle cx="{cx}" cy="{cy}" r="{r:.1f}" fill="url(#ice)"/>'
    return out


def arrow_group():
    body, _ = filled(ARROW, shadow=True)
    return f'<g transform="translate(0.4,0.4) scale(0.66)">{body}</g>'


def build():
    hand_detail = (
        f'<path d="M15.3 12.4 L15.3 16.4 M19.7 13 L19.7 16.4 M23.9 13.8 L23.9 16.6" '
        f'fill="none" stroke="{INK}" stroke-width="1" stroke-linecap="round"/>'
        f'<rect x="4" y="23.9" width="26" height="1.8" fill="{CUFF_ICE}"/>'
        f'<rect x="4" y="25.7" width="26" height="0.9" fill="#FFFFFF"/>'
        f'<rect x="4" y="26.6" width="26" height="3" fill="{ICE_LO}"/>'
    )
    hand_clip = f'<clipPath id="body"><path d="{HAND}"/></clipPath>'
    ibeam = (
        "M16 7.4 L16 24.6 "
        "M10.6 5.8 C13 5.8 14.6 7.4 16 7.4 C17.4 7.4 19 5.8 21.4 5.8 "
        "M10.6 26.2 C13 26.2 14.6 24.6 16 24.6 C17.4 24.6 19 26.2 21.4 26.2"
    )
    cross = "M16 3.4 L16 11.4 M16 20.6 L16 28.6 M3.4 16 L11.4 16 M20.6 16 L28.6 16"
    help_disc, _ = filled("M23 16.4 A6.6 6.6 0 1 1 22.99 16.4 Z")
    files = {}
    files["arrow"], _ = filled(ARROW, shadow=True)
    files["up"], _ = filled(UP, shadow=True)
    files["hand"], _ = filled(HAND, shadow=True, detail=hand_detail, clip=hand_clip)
    files["ibeam"] = stroked(ibeam, 2.3) + dot(16, 16, 1.5)
    files["cross"] = stroked(cross, 2.2) + dot(16, 16, 1.4)
    files["wait"] = ice_stroked(flake(16, 16, 12.2), 2.1)
    files["no"] = (
        f'<circle cx="16" cy="16" r="10.6" fill="none" stroke="#FFFFFF" stroke-width="5.9"/>'
        f'<circle cx="16" cy="16" r="10.6" fill="none" stroke="{INK}" stroke-width="2.7"/>'
        + stroked("M8.8 8.8 L23.2 23.2", 2.7, RED)
    )
    for name, angle in (("we", 0), ("ns", 90), ("nwse", 45), ("nesw", 135)):
        body, _ = filled(RESIZE)
        files[name] = f'<g transform="rotate({angle} 16 16)">{body}</g>' if angle else body
    files["move"] = filled(MOVE)[0] + dot(16, 16, 1.1, halo=False)
    files["busy"] = arrow_group() + ice_stroked(flake(21.8, 21.8, 6.5), 1.7)
    files["help"] = (
        arrow_group() + help_disc
        + '<text x="23" y="23.4" text-anchor="middle" dominant-baseline="central" '
        'font-family="Segoe UI, Arial, sans-serif" font-weight="700" font-size="9.2" fill="#FFFFFF">?</text>'
    )
    return files


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join("assets", "cursors", "svg")
    os.makedirs(out, exist_ok=True)
    for name, body in build().items():
        clip = f'<clipPath id="body"><path d="{HAND}"/></clipPath>' if name == "hand" else ""
        svg = (
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">\n'
            f"<!-- COLDY Glacier cursors · {name} · design source, mirrors CursorFactory in src/Program.cs -->\n"
            + DEFS.format(clip=clip) + body + "\n</svg>\n"
        )
        with open(os.path.join(out, name + ".svg"), "w", encoding="utf-8") as f:
            f.write(svg)
    print("Wrote", os.path.abspath(out))


if __name__ == "__main__":
    main()