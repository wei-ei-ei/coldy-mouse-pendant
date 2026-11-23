# Rebuild coldy-form4.gif from the singing shot of COLDY WORLD's "Co歌之王".
#
# The old form 4 came from a low-resolution generated clip. Its cutout kept
# the dark stage as opaque pixels (the black slab on the right) and stretched
# one narrow band of fur down the belly, which is why the lower body looked
# like a double exposure. Here the figure comes from the original recording:
# the belly is one closed, rounded silhouette with real fur texture, and the
# colours are taken straight from the frame instead of the premultiplied
# cutout (rembg marks white fur as ~50% transparent, which darkens it).
#
# rembg is not stable frame to frame on the stage lights, so one clean frame
# is used as the base and the loop is a gentle sing-along sway, like form 2/3.
#
# Run with Python 3.13 (cv2, numpy, Pillow, rembg):
#   python tools\repair_form4.py --preview-only
#   python tools\repair_form4.py
import argparse
import math
import os
import cv2
import numpy as np
from PIL import Image
from rembg import remove, new_session

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_VIDEO = os.path.join(ROOT, "屏幕录制 2026-11-12 032453.mp4")
DEFAULT_OUT = os.path.join(ROOT, "assets", "coldy-form4.gif")
TEMP = os.environ.get("TEMP", ROOT)

SOURCE_INDEX = 20          # full-body front view, cleanest cutout
CROP = (45, 0, 950, 1600)  # x0, y0, x1, y1 - remove the app chrome
CANVAS_W, CANVAS_H = 460, 420
SCALE = 345.0 / 1323.0     # same on-screen height as the old form 4
PLACE_X, PLACE_Y = 91, -32 # puts the crown back at about (210, 52)
CROWN = (210.0, 52.0)      # sway around the point where the rope meets the head
FRAMES = 24
FRAME_MS = 100
TRANS = 215                # transparent palette index, same as form 3
MAGENTA = [255, 0, 255]


def largest(mask):
    n, lab, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    if n <= 1:
        return np.zeros(mask.shape, bool)
    idx = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    return lab == idx


def fill_holes(mask, max_area=6000):
    inv = (~mask).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(inv, 8)
    out = mask.copy()
    for i in range(1, n):
        x, y, w, h, area = stats[i]
        if area < max_area and x > 0 and y > 0 and x + w < mask.shape[1] and y + h < mask.shape[0]:
            out[lab == i] = True
    return out


def ellipse(k):
    return cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))


def smooth_bounds(body):
    """Robust left/right body edge per row, used as an outline envelope."""
    h = body.shape[0]
    left = np.full(h, np.nan)
    right = np.full(h, np.nan)
    for y in range(h):
        xs = np.where(body[y])[0]
        if xs.size < 8:
            continue
        left[y] = np.percentile(xs, 1)
        right[y] = np.percentile(xs, 99)
    for arr in (left, right):
        bad = np.isnan(arr)
        good = np.where(~bad)[0]
        if good.size == 0:
            arr[:] = 0
        else:
            arr[bad] = np.interp(np.where(bad)[0], good, arr[good])
    left = cv2.GaussianBlur(left.astype(np.float32), (1, 81), 0).ravel()
    right = cv2.GaussianBlur(right.astype(np.float32), (1, 81), 0).ravel()
    return left, right


def clean(subject, bgr):
    """Keep a closed fur silhouette; trim the off-screen cable and stage specks."""
    h, w = subject.shape
    yy = np.arange(h)[:, None]
    xx = np.arange(w)[None, :]

    # A wide opening removes the thin rainbow leads and leaves the torso.
    body = cv2.morphologyEx(subject.astype(np.uint8), cv2.MORPH_OPEN, ellipse(121)) > 0
    body = largest(body)
    body = cv2.dilate(body.astype(np.uint8), ellipse(61)) > 0
    left, right = smooth_bounds(body)

    # Head: leave room for the spoon. Chest: keep a short length of the leads.
    # Belly: hug the fur so no stage band survives at the left edge.
    margin = np.where(np.arange(h) < 450, 200.0,
                      np.where(np.arange(h) < 1000, 135.0, 26.0)).astype(np.float32)
    margin = cv2.GaussianBlur(margin[:, None], (1, 101), 0).ravel()
    envelope = (xx >= (left - 24)[:, None]) & (xx <= (right + margin)[:, None])
    subject = largest(subject & envelope)
    subject = fill_holes(subject)

    # Soft edge only; the belly itself is fully opaque, never translucent.
    edge = cv2.GaussianBlur(subject.astype(np.float32) * 255, (3, 3), 0)
    hard = np.where(subject, 255, 0).astype(np.uint8)
    rim = subject ^ (edge > 160)
    hard[rim] = np.clip(edge[rim], 0, 255).astype(np.uint8)
    return hard


def repaint_stage_lights(rgb, subject):
    """Stage lamps show through the crown and the bottom edge. Repaint those
    warm pixels with the fur colour from the same column instead of leaving
    orange blotches or punching holes in the silhouette."""
    h, w = rgb.shape[:2]
    yy = np.arange(h)[:, None]
    hsv = cv2.cvtColor(rgb, cv2.COLOR_BGR2HSV)
    warm = (hsv[:, :, 0] < 40) & (hsv[:, :, 1] > 40) & (hsv[:, :, 2] > 80)
    warm &= subject & ((yy < 400) | (yy > 1400))
    clean = subject & ~warm
    out = rgb.copy()
    for x in range(w):
        ys = np.where(warm[:, x])[0]
        if ys.size == 0:
            continue
        good = np.where(clean[:, x])[0]
        if good.size == 0:
            continue
        for c in range(3):
            out[ys, x, c] = np.interp(ys, good, rgb[good, x, c]).astype(np.uint8)
    return out


def brighten(rgb, opaque):
    """The recording is dim; lift only the pale fur, not the glasses or mic."""
    hsv = cv2.cvtColor(rgb, cv2.COLOR_BGR2HSV)
    fur = opaque & (hsv[:, :, 1] < 58) & (hsv[:, :, 2] > 105)
    out = rgb.astype(np.float32)
    out[fur] = np.clip(out[fur] * 1.09 + 5, 0, 255)
    return out.astype(np.uint8)


def build_base(frame, session, threshold):
    x0, y0, x1, y1 = CROP
    crop = frame[y0:y1, x0:x1].copy()
    cut = remove(crop, session=session)
    alpha = cut[:, :, 3]
    subject = largest(alpha > threshold)
    subject = cv2.morphologyEx(subject.astype(np.uint8), cv2.MORPH_CLOSE, ellipse(9)) > 0
    subject = fill_holes(largest(subject))
    hard = clean(subject, crop)

    opaque = hard > 127
    sprite = np.dstack([brighten(repaint_stage_lights(crop, opaque), opaque), hard])
    sized = cv2.resize(sprite, (round(sprite.shape[1] * SCALE), round(sprite.shape[0] * SCALE)),
                       interpolation=cv2.INTER_AREA)
    sized[:, :, 3] = np.where(sized[:, :, 3] > 127, 255, 0).astype(np.uint8)
    canvas = np.zeros((CANVAS_H, CANVAS_W, 4), np.uint8)
    sx0, sy0 = max(0, -PLACE_X), max(0, -PLACE_Y)
    dx0, dy0 = max(0, PLACE_X), max(0, PLACE_Y)
    dx1 = min(CANVAS_W, PLACE_X + sized.shape[1])
    dy1 = min(CANVAS_H, PLACE_Y + sized.shape[0])
    if dx1 > dx0 and dy1 > dy0:
        canvas[dy0:dy1, dx0:dx1] = sized[sy0:sy0 + dy1 - dy0, sx0:sx0 + dx1 - dx0]
    return canvas


def animate(base, count, crown=CROWN):
    frames = []
    for i in range(count):
        t = 2.0 * math.pi * i / count
        angle = 3.2 * math.sin(t)
        sx = 1.0 - 0.008 * math.sin(t + 0.6)
        sy = 1.0 + 0.012 * math.sin(t + 0.6)
        dy = 1.6 * math.sin(2.0 * t)
        m = cv2.getRotationMatrix2D(crown, angle, 1.0)
        m[0, 0] *= sx
        m[0, 1] *= sx
        m[1, 0] *= sy
        m[1, 1] *= sy
        m[1, 2] += dy
        warped = cv2.warpAffine(base, m, (CANVAS_W, CANVAS_H), flags=cv2.INTER_LINEAR,
                                borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
        warped[:, :, 3] = np.where(warped[:, :, 3] > 127, 255, 0).astype(np.uint8)
        frames.append(warped)
    return frames


def composite(frames, cols=6, tile=240):
    tiles = []
    for f in frames:
        rgb = f[:, :, :3].astype(np.float32)
        a = f[:, :, 3:4].astype(np.float32) / 255.0
        bg = np.array([20, 18, 26], np.float32)
        t = (rgb * a + bg * (1 - a)).astype(np.uint8)
        tiles.append(cv2.resize(t, (tile, round(tile * f.shape[0] / f.shape[1]))))
    th = max(t.shape[0] for t in tiles)
    rows = (len(tiles) + cols - 1) // cols
    sheet = np.full((th * rows, tile * cols, 3), (20, 18, 26), np.uint8)
    for i, t in enumerate(tiles):
        y, x = (i // cols) * th, (i % cols) * tile
        sheet[y:y + t.shape[0], x:x + t.shape[1]] = t
        cv2.putText(sheet, "%02d" % i, (x + 8, y + 22),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
    return sheet


def pack_gif(frames, path):
    rgb_frames = []
    masks = []
    for f in frames:
        mask = f[:, :, 3] > 127
        rgb = cv2.cvtColor(f[:, :, :3], cv2.COLOR_BGR2RGB)
        rgb[~mask] = (0, 0, 0)
        rgb_frames.append(Image.fromarray(rgb))
        masks.append(mask)
    sample = np.concatenate([np.array(im)[::2, ::2] for im in rgb_frames], axis=0).reshape(-1, 3)
    # Force a grey ramp into the palette so the soft edge shadow of the singing
    # form is not quantized back up to white (the body is mostly white, which
    # would otherwise dominate a median-cut palette). The sample must become a
    # real RGB image: fromarray on a plain (N, 3) array builds an L-mode
    # grayscale image, and the median cut then yields an all-grey palette that
    # drains every colour out of the GIF.
    ramp = np.array([[g, g, g] for g in range(20, 246, 4)], dtype=np.uint8)
    sample = np.concatenate([sample, ramp], axis=0)
    pal = Image.fromarray(sample.reshape(-1, 1, 3)).quantize(colors=255, method=Image.MEDIANCUT)
    colors = list(pal.getpalette()[:255 * 3])
    colors = colors[:TRANS * 3] + MAGENTA + colors[TRANS * 3:]
    pal_img = Image.new("P", (1, 1))
    pal_img.putpalette(colors)

    pages = []
    for im, mask in zip(rgb_frames, masks):
        # Error diffusion keeps the smooth scarf / collar / lead gradients from
        # posterizing into flat patches; the fur texture hides the grain.
        idx = np.array(im.quantize(palette=pal_img, dither=Image.Dither.FLOYDSTEINBERG))
        idx[(idx == TRANS) & mask] = TRANS + 1  # never punch a hole in the figure
        idx[~mask] = TRANS
        page = Image.fromarray(idx.astype(np.uint8), "P")
        page.putpalette(colors)
        pages.append(page)
    pages[0].save(path, save_all=True, append_images=pages[1:], duration=FRAME_MS,
                  loop=0, disposal=2, transparency=TRANS, optimize=False)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--video", default=DEFAULT_VIDEO)
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--preview", default=os.path.join(TEMP, "coldy-form4-rebuild.png"))
    ap.add_argument("--source-index", type=int, default=SOURCE_INDEX)
    ap.add_argument("--frames", type=int, default=FRAMES)
    ap.add_argument("--threshold", type=int, default=40)
    ap.add_argument("--preview-only", action="store_true")
    args = ap.parse_args()

    cap = cv2.VideoCapture(args.video)
    if not cap.isOpened():
        raise SystemExit("cannot open " + args.video)
    cap.set(cv2.CAP_PROP_POS_FRAMES, args.source_index)
    ok, frame = cap.read()
    cap.release()
    if not ok:
        raise SystemExit("cannot read frame " + str(args.source_index))

    session = new_session("u2net")
    base = build_base(frame, session, args.threshold)
    cv2.imwrite(os.path.join(TEMP, "coldy-form4-base.png"), base)
    print("base", int((base[:, :, 3] > 127).sum()), "opaque pixels")
    frames = animate(base, args.frames)

    cv2.imwrite(args.preview, composite(frames))
    print("preview", args.preview)
    gif = os.path.join(TEMP, "coldy-form4-preview.gif") if args.preview_only else args.out
    pack_gif(frames, gif)
    print("gif", gif, os.path.getsize(gif), "bytes")


if __name__ == "__main__":
    main()
