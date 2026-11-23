# Form 3 ("k歌之王", asset name coldy-form4.gif) sings.
#
# The base sprite comes straight from the official clip at 58.0s (see
# build_form4_hd.py): the snowman sings frontal and upright, the mouth dot
# centred under the glasses and clearly visible, the mic held at the mouth's
# right with the thick rainbow tube leads curling down into the body socket,
# and the scarf shows its green-white AND red-white knit sections with the
# blue-white tail hanging left. Everything is native to the frame - no
# cut-and-paste surgery, no repositioning, no pose correction.
#
# This script only animates:
#   - one continuous deformation field (cv2.remap): the head nods, the body
#     sways, the leads arm wobbles; every destination pixel is sampled exactly
#     once so nothing ghosts,
#   - the mouth: a feathered dark oval drawn over the baked-in mouth dot,
#     growing with the beat,
#   - direction light, two or three rising notes,
#   - fur texture and edge care (bright halo / dark stage fringe repainted
#     with the fur just inside, the cool grade warmed to white on the low-
#     saturation fur, a 1-2px soft grey contour edge so the white figure reads
#     on light desktops),
#   - 2x supersampling: frames are deformed on a doubled canvas and averaged
#     back down so the GIF's 1-bit silhouette steps at half the size.
import os
import math
import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "assets", "candidates", "coldy-form4-fullbody-hd.png")
OUT = os.path.join(ROOT, "assets", "coldy-form4.gif")
PREVIEW = os.path.join(os.environ.get("TEMP", ROOT), "coldy-form4-singing-sheet.png")
KEYFRAMES = os.path.join(os.environ.get("TEMP", ROOT), "coldy-form4-singing-keys.png")

FRAMES = 40
TARGET_H = 345
PLACE = (98, 29)
CANVAS_W, CANVAS_H = 460, 420
PIVOT = (221.0, 215.0)       # the bust's centroid: the sway rotation pivot
NECK_TOP, NECK_BOTTOM = 160.0, 230.0
ARM_CENTER = (315.0, 220.0)  # the rainbow tube leads curling at the right
ARM_SIGMA = (45.0, 62.0)

# the baked-in mouth dot sits centred under the glasses bridge; the singing
# mouth oval grows from the dot's own size so it never jumps
MOUTH = (191.0, 163.0)
MOUTH_HW = (12.5, 16.0)      # half width, closed -> open
MOUTH_HH = (11.0, 15.0)      # half height, closed -> open

SPOON_BOX = (250, 25, 330, 120)   # the metal spoon keeps its own dark edge
NOTE_COLORS = [(140, 225, 255), (255, 205, 120), (235, 245, 255)]  # BGR


def make_base():
    im = cv2.imread(SRC, cv2.IMREAD_UNCHANGED)
    if im is None:
        raise SystemExit("cannot read " + SRC)
    a = im[:, :, 3]
    ys, xs = np.where(a > 128)
    crop = im[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    s = TARGET_H / crop.shape[0]
    dw = round(crop.shape[1] * s)
    sized = cv2.resize(crop, (dw, TARGET_H), interpolation=cv2.INTER_AREA)
    base = np.zeros((CANVAS_H, CANVAS_W, 4), np.uint8)
    px, py = PLACE
    base[py:py + TARGET_H, px:px + dw] = sized
    solid = base[:, :, 3] > 127

    hsv = cv2.cvtColor(base[:, :, :3], cv2.COLOR_BGR2HSV)
    fur = (base[:, :, 3] > 127) & (hsv[:, :, 1] < 62) & (hsv[:, :, 2] > 100)
    # The 58s shot is graded cool: the bust fur reads pale blue-grey and melts
    # straight into light desktops. Push the low-saturation fur to the warm
    # white of the other shots - lift the value, pull the saturation way down -
    # everywhere except the collar band, whose green and red knit must keep
    # their colours.
    rows_m = np.arange(CANVAS_H)
    collar_band = (rows_m >= 172) & (rows_m <= 242)
    warm_mask = fur & ~collar_band[:, None]
    if warm_mask.sum():
        hch, sch, vch = cv2.split(hsv)
        s2 = (sch.astype(np.float32) * 0.35).astype(np.uint8)
        v2 = np.clip(vch.astype(np.float32) * 1.12 + 14, 0, 255).astype(np.uint8)
        warm = cv2.cvtColor(cv2.merge([hch, s2, v2]), cv2.COLOR_HSV2BGR)
        rgb = base[:, :, :3].astype(np.float32)
        rgb[warm_mask] = warm[warm_mask]
        keep = fur & collar_band[:, None]
        rgb[keep] = np.clip(rgb[keep] * 1.04 + 4, 0, 255)
        base[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)

    # Piled-snow texture: a couple of octaves of smooth noise become small
    # luminance bumps, everywhere inside the fur and a touch on the scarf.
    # Keep the bumps off the outer 3px so the silhouette edge stays clean.
    fuzz = snow_field(CANVAS_H, CANVAS_W)
    solid0 = (base[:, :, 3] > 127)
    dist0 = cv2.distanceTransform(solid0.astype(np.uint8), cv2.DIST_L2, 3)
    lump = (fur | ((base[:, :, 3] > 127) & (hsv[:, :, 1] < 90))) & (dist0 > 3)
    delta = (fuzz * 22.0)
    delta = np.clip(delta, -26, 26)
    delta[dist0 <= 3] = 0
    rgb = base[:, :, :3].astype(np.float32)
    rgb[lump] = np.clip(rgb[lump] + delta[lump][:, None], 0, 255)
    base[:, :, :3] = rgb.astype(np.uint8)

    # The cutout's matte runs a pixel or two past the visible fur around the
    # silhouette, dragging a thin band of dark stage background along the
    # outline, and the video's rim light adds a pale halo on top. Repaint every
    # boundary pixel that is markedly darker - or brighter - than the fur just
    # inside with that interior colour; the fill keeps the local hue, so the
    # scarf and the rainbow tubes keep their own colours. The metal spoon needs
    # its dark edge for contrast and is left alone.
    solid = base[:, :, 3] > 127
    hsvb = cv2.cvtColor(base[:, :, :3], cv2.COLOR_BGR2HSV)
    dist = cv2.distanceTransform(solid.astype(np.uint8), cv2.DIST_L2, 3)
    interior = (dist >= 4.0).astype(np.float32)
    masked = base[:, :, :3].astype(np.float32) * interior[:, :, None]
    # both terms must be blurred: dividing the raw masked colour by the blurred
    # weight yields black at the rim (0 / weight)
    fill = cv2.GaussianBlur(masked, (0, 0), sigmaX=3.0) / \
        np.maximum(cv2.GaussianBlur(interior, (0, 0), sigmaX=3.0), 1e-3)[:, :, None]
    vfill = cv2.GaussianBlur(hsvb[:, :, 2].astype(np.float32) * interior, (0, 0), sigmaX=3.0) / \
        np.maximum(cv2.GaussianBlur(interior, (0, 0), sigmaX=3.0), 1e-3)
    rim = solid & (dist < 4.5) & (hsvb[:, :, 2].astype(np.float32) < vfill - 20.0)
    rim[SPOON_BOX[1]:SPOON_BOX[3], SPOON_BOX[0]:SPOON_BOX[2]] = False
    base[:, :, :3][rim] = np.clip(fill[rim], 0, 255).astype(np.uint8)
    halo = solid & (dist < 3.0) & (hsvb[:, :, 2].astype(np.float32) > vfill + 30.0)
    base[:, :, :3][halo] = np.clip(fill[halo], 0, 255).astype(np.uint8)

    # A whisper of edge shading: with the black ring gone the white silhouette
    # melts straight into light desktops and the bust reads as missing. The
    # boundary gets a 1-2px soft grey edge - far lighter than the black ring
    # that was removed, just enough to read the contour on bright backgrounds
    # while staying next to invisible on dark ones. Fur pixels only.
    fur_edge = solid & (dist < 2.0) & (hsvb[:, :, 1] < 70) & (hsvb[:, :, 2] > 140)
    base[:, :, :3][fur_edge] = np.clip(
        base[:, :, :3][fur_edge].astype(np.float32) * 0.82 + 8, 0, 255).astype(np.uint8)

    # The lower silhouette carries small bumps from the source cutout, and the
    # per-frame resampling makes them shimmer like waves while the body sways.
    # Smooth the alpha of the lower outline: the 50% contour keeps its place,
    # the bumps flatten out, and the scarf fringe is excluded.
    alpha = base[:, :, 3].astype(np.float32)
    smooth = cv2.GaussianBlur(alpha, (0, 0), sigmaX=7.0)
    zone = np.zeros((CANVAS_H, CANVAS_W), np.float32)
    zone[280:, 178:] = 1.0
    zone = cv2.GaussianBlur(zone, (0, 0), sigmaX=14.0)
    base[:, :, 3] = (alpha * (1 - zone) + smooth * zone).astype(np.uint8)

    return base


def snow_field(h, w):
    rng = np.random.default_rng(7)
    n1 = rng.random((h // 9 + 2, w // 9 + 2)).astype(np.float32)
    n1 = cv2.resize(n1, (w, h), interpolation=cv2.INTER_LINEAR)
    n2 = rng.random((h // 3 + 2, w // 3 + 2)).astype(np.float32)
    n2 = cv2.resize(n2, (w, h), interpolation=cv2.INTER_LINEAR)
    n1 -= n1.mean()
    n2 -= n2.mean()
    n1 = cv2.GaussianBlur(n1, (0, 0), sigmaX=2.4)
    n2 = cv2.GaussianBlur(n2, (0, 0), sigmaX=1.1)
    return 0.62 * n1 + 0.38 * n2


def deform_map(t, grid, k=1.0):
    yy, xx = grid
    phase = 2.0 * math.pi * t
    beat = math.sin(phase * 2.0 + 0.3)
    head_ang = math.radians(3.2 * beat)
    body_ang = math.radians(1.8 * beat)
    neck_top, neck_bottom = NECK_TOP * k, NECK_BOTTOM * k
    u = np.clip((yy - neck_top) / (neck_bottom - neck_top), 0.0, 1.0)
    w_head = 1.0 - u * u * (3.0 - 2.0 * u)
    ang = body_ang * (1.0 - w_head) + head_ang * w_head

    cx, cy = PIVOT[0] * k, PIVOT[1] * k
    dx, dy = xx - cx, yy - cy
    ca, sa = np.cos(-ang), np.sin(-ang)
    sx = cx + dx * ca - dy * sa
    sy = cy + dx * sa + dy * ca

    breath = 1.0 + 0.010 * math.sin(phase * 2.0 + 0.9)
    sx = cx + (sx - cx) / breath
    sy = cy + (sy - cy) / breath

    bob = 3.4 * math.sin(phase * 2.0 + 0.8)
    sy = sy + bob * w_head

    ax, ay = ARM_CENTER[0] * k, ARM_CENTER[1] * k
    sxg, syg = ARM_SIGMA[0] * k, ARM_SIGMA[1] * k
    w_arm = np.exp(-((xx - ax) ** 2) / (2.0 * sxg ** 2)
                   - ((yy - ay) ** 2) / (2.0 * syg ** 2))
    sx = sx + w_arm * (3.2 * math.sin(phase * 2.0 + 1.0))
    sy = sy + w_arm * (1.7 * math.sin(phase * 2.0 + 0.2))
    return sx, sy


def extend_rgb(img):
    """Carry the figure's colours a few pixels out into the transparent zone.
    When the 2x frame is downscaled, a canvas edge pixel averages sub-pixels
    from inside and outside the silhouette; with the outside kept at black the
    edge would come out dark again, so the border needs fur-coloured padding."""
    solid = (img[:, :, 3] > 127).astype(np.float32)
    masked = img[:, :, :3].astype(np.float32) * solid[:, :, None]
    wsum = cv2.GaussianBlur(solid, (0, 0), sigmaX=3.0)[:, :, None]
    fill = cv2.GaussianBlur(masked, (0, 0), sigmaX=3.0) / np.maximum(wsum, 1e-3)
    out = img.copy()
    grow = cv2.dilate(solid.astype(np.uint8), np.ones((7, 7), np.uint8)) > 0
    ext = grow & (img[:, :, 3] <= 127)
    out[:, :, :3][ext] = np.clip(fill[ext], 0, 255).astype(np.uint8)
    return out


def shade(frame, phase):
    h, w = frame.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    gx = (xx / w - 0.5) * (0.075 + 0.045 * math.sin(phase))
    gy = (yy / h - 0.5) * 0.035
    light = (1.0 + gx - gy)[:, :, None]
    frame[:, :, :3] = np.clip(frame[:, :, :3].astype(np.float32) * light, 0, 255).astype(np.uint8)
    return frame


def mouth_sprite(open_amt, k=1.0):
    """The singing mouth, drawn over the baked-in mouth dot: at rest it is the
    dot's own size, with the beat it grows into a wide feathered oval. No
    outline ring - the reference mouth is a soft hole in the fur."""
    hw = (MOUTH_HW[0] + (MOUTH_HW[1] - MOUTH_HW[0]) * open_amt) * k
    hh = (MOUTH_HH[0] + (MOUTH_HH[1] - MOUTH_HH[0]) * open_amt) * k
    cx, cy = int(round(MOUTH[0] * k)), int(round(MOUTH[1] * k))
    layer = np.zeros((int(CANVAS_H * k), int(CANVAS_W * k), 4), np.uint8)
    cv2.ellipse(layer, (cx, cy), (max(1, int(round(hw))), max(1, int(round(hh)))),
                0, 0, 360, (28, 20, 24, 255), -1)
    if open_amt > 0.45:
        cv2.ellipse(layer, (cx, cy + int(round(hh * 0.30))),
                    (max(1, int(round(hw * 0.55))), max(1, int(round(hh * 0.40)))),
                    0, 0, 360, (52, 36, 42, 255), -1)
    layer[:, :, 3] = cv2.GaussianBlur(layer[:, :, 3], (0, 0), sigmaX=1.1 * k)
    return layer


def note(layer, x, y, size, color):
    cv2.circle(layer, (x, y - size // 2), size + 6, (color[0], color[1], color[2], 32), -1)
    cv2.ellipse(layer, (x, y), (size, max(3, int(size * 0.72))), -22, 0, 360,
                (color[0], color[1], color[2], 255), -1)
    cv2.line(layer, (x + size - 1, y - 2), (x + size - 1, y - size * 2),
             (color[0], color[1], color[2], 255), 2)
    pts = np.array([[x + size - 1, y - size * 2],
                    [x + size + size // 2 + 2, y - int(size * 1.55)],
                    [x + size + 1, y - int(size * 1.05)]], np.int32)
    cv2.fillPoly(layer, [pts], (color[0], color[1], color[2], 255))


def add_notes(frame, t):
    for k in range(3):
        phase = (t * 2.0 + k / 3.0) % 1.0
        if phase > 0.72:
            continue
        life = phase / 0.72
        x = int(338 + k * 18 + 9 * math.sin(phase * 7.0 + k))
        y = int(198 - life * 96)
        opacity = math.sin(math.pi * life) ** 0.6
        layer = np.zeros_like(frame)
        note(layer, x, y, 10 + (k % 2) * 2, NOTE_COLORS[k])
        a = (layer[:, :, 3:4].astype(np.float32) / 255.0) * opacity
        frame[:, :, :3] = (frame[:, :, :3].astype(np.float32) * (1.0 - a)
                           + layer[:, :, :3].astype(np.float32) * a).astype(np.uint8)
        frame[:, :, 3] = np.maximum(frame[:, :, 3], (a[:, :, 0] * 255).astype(np.uint8))
    return frame


def over(bg, fg):
    a = fg[:, :, 3:4].astype(np.float32) / 255.0
    bg[:, :, :3] = (bg[:, :, :3].astype(np.float32) * (1 - a)
                    + fg[:, :, :3].astype(np.float32) * a).astype(np.uint8)
    bg[:, :, 3] = np.maximum(bg[:, :, 3], fg[:, :, 3])
    return bg


def main():
    base = make_base()
    # 2x supersampling: every frame is deformed on a doubled canvas and
    # averaged back down, so the GIF's 1-bit silhouette steps at half the size.
    k = 2.0
    w2, h2 = int(CANVAS_W * k), int(CANVAS_H * k)
    base2 = extend_rgb(cv2.resize(base, (w2, h2), interpolation=cv2.INTER_LINEAR))
    grid2 = np.mgrid[0:h2, 0:w2].astype(np.float32)
    frames = []
    for i in range(FRAMES):
        t = i / float(FRAMES)
        sx, sy = deform_map(t, grid2, k)
        body = cv2.remap(base2, sx, sy, cv2.INTER_LINEAR,
                         borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
        body = shade(body, 4.0 * math.pi * t)

        open_amt = (0.5 + 0.5 * math.sin(2.0 * math.pi * t * 2.0 + 0.8)) ** 1.8
        body = over(body, mouth_sprite(open_amt, k))

        body = cv2.resize(body, (CANVAS_W, CANVAS_H), interpolation=cv2.INTER_AREA)
        body = add_notes(body, t)
        mask = body[:, :, 3] > 127
        body[:, :, 3] = np.where(mask, 255, 0).astype(np.uint8)
        body[~mask] = 0
        frames.append(body)

    from repair_form4 import pack_gif, composite  # noqa: E402
    pack_gif(frames, OUT)
    cv2.imwrite(PREVIEW, composite(frames))
    keys = [frames[i] for i in (0, 5, 10, 15, 20, 25, 30, 35)]
    cv2.imwrite(KEYFRAMES, composite(keys, cols=8, tile=250))
    print("gif", OUT, os.path.getsize(OUT), "bytes", FRAMES, "frames")
    print("preview", PREVIEW)
    print("keyframes", KEYFRAMES)


if __name__ == "__main__":
    main()
