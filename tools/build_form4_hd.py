# Build form 4 from the official COLDY WORLD "Co歌之王" clip.
#
# The screen recording used by repair_form4.py is a phone capture of the post.
# Here we use the 1440x2560 source video instead. At 41.2s the snowman is
# frontal, sings with the mouth dot visible left of the mic ball, holds the mic
# with the thick rainbow tube leads curling down into the body socket, and the
# scarf shows its green-white AND red-white knit sections. The whole figure
# (head to base) is in frame, so the sprite carries the real singing pose and
# every scarf colour without any cut-and-paste surgery.
#
# Run with Python 3.13 (cv2, numpy, Pillow, rembg):
#   python tools\build_form4_hd.py
import os
import sys
import cv2
import numpy as np
from rembg import remove, new_session

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CAND = os.path.join(ROOT, "assets", "candidates")
VIDEO = os.path.join(CAND, "coldy-cogezwang-1440x2560.mp4")
OUT_SPRITE = os.path.join(CAND, "coldy-form4-fullbody-hd.png")

SECOND = 58.0
FPS = 60
# the 58.0s frame: the frontal upright medium shot - the mouth dot centred
# under the glasses, the mic held at the mouth's right with the thick rainbow
# tube leads curling into the body socket, the green-white and red-white knit
# collar sections and the blue-white tail all visible. No pose correction
# needed: everything the animation needs is native to this frame.
UPRIGHT_ANGLE = 0.0


def largest(mask):
    n, lab, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    if n <= 1:
        return np.zeros(mask.shape, bool)
    return lab == (1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA])))


def fill_holes(mask, max_area=20000):
    inv = (~mask).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(inv, 8)
    out = mask.copy()
    for i in range(1, n):
        x, y, w, h, area = stats[i]
        if area < max_area and x > 0 and y > 0 and x + w < mask.shape[1] and y + h < mask.shape[0]:
            out[lab == i] = True
    return out


def repaint_stage_floor(rgb, opaque, bottom):
    """The stage floor leaves a blue reflection that starts well above the very
    last rows. Detect the blue cast directly and repaint it with fur from the
    same column, so the sprite has no floor attached."""
    b, g, r = cv2.split(rgb)
    cast = b.astype(np.int16) - r.astype(np.int16)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_BGR2HSV)
    band = np.zeros(rgb.shape[:2], bool)
    band[max(0, bottom - 38):bottom + 1, :] = True
    cool = (cast > 12) & (hsv[:, :, 1] > 16)
    target = opaque & band & cool
    out = rgb.copy()
    for x in range(rgb.shape[1]):
        ys = np.where(target[:, x])[0]
        if ys.size == 0:
            continue
        good = np.where(opaque[:, x] & ~target[:, x])[0]
        if good.size == 0:
            continue
        for c in range(3):
            out[ys, x, c] = np.interp(ys, good, rgb[good, x, c]).astype(np.uint8)
    return out


def recover_body_from_frame(rgba, frame):
    """The close-up framing makes the segmenter grab only the face and drop the
    body below it. The frame still shows every body pixel, so recover them:
    grow through bright low-saturation fur/knit pixels connected to the
    segmenter's mask - one connected-component pass, so isolated bright stage
    lights can never leak in and the dark stage stays blocked."""
    hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)
    # the 58s framing fills the frame bottom with the body, so no floor can
    # leak in; the dark stage is far below the V gate and the warm bokeh is
    # far above the S gate, and both are isolated from the figure anyway
    fur_like = ((hsv[:, :, 2] > 90) & (hsv[:, :, 1] < 180)).astype(np.uint8)
    mask = rgba[:, :, 3] > 127
    seed = mask & (fur_like > 0)
    n, lab = cv2.connectedComponents(fur_like, 8)
    seed_labels = np.unique(lab[seed > 0])
    seed_labels = seed_labels[seed_labels > 0]
    keep = np.isin(lab, seed_labels)
    # the knit's dark crevices sit below the V gate: grow the recovered region
    # a few pixels so they get covered by the surrounding knit colours
    keep = cv2.dilate(keep.astype(np.uint8), np.ones((9, 9), np.uint8)) > 0
    # the frame's top carries the stage spotlights (bright, fur-like, and
    # adjacent to the head in places) - the recovery zone starts below them
    keep[:450, :] = False
    add = keep & ~mask
    out = rgba.copy()
    if add.sum():
        out[add, :3] = frame[add]
        out[:, :, 3][add] = 255

    # second pass: the knit's own shadowed bands are wide and dark, far below
    # any colour gate - any transparent pixel whose column has solid figure
    # pixels both above and below it (within a bounded window) is an interior
    # hole, and gets the vertical interpolation between them. The knit's
    # vertical ribs survive because donors come from the same column; the
    # per-column fills are then smoothed horizontally so they merge into the
    # soft shadow bands the video actually shows.
    hh, ww = out.shape[:2]
    solidf = out[:, :, 3] > 127
    window = 90
    filled = np.zeros((hh, ww), bool)
    for x in range(ww):
        col = np.where(~solidf[:, x])[0]
        if col.size == 0:
            continue
        runs = np.split(col, np.where(np.diff(col) > 1)[0] + 1)
        for run in runs:
            top = run[0] - 1
            bot = run[-1] + 1
            if top < 0 or bot >= hh:
                continue
            if not solidf[top, x] or not solidf[bot, x]:
                continue
            if run[0] - top > window or bot - run[-1] > window:
                continue
            wgt = (run - top) / float(bot - top)
            val = (out[top, x, :3].astype(np.float32) * (1 - wgt[:, None])
                   + out[bot, x, :3].astype(np.float32) * wgt[:, None])
            out[run, x, :3] = np.clip(val, 0, 255).astype(np.uint8)
            out[:, :, 3][run, x] = 255
            filled[run, x] = True
    if filled.sum():
        band = cv2.GaussianBlur(out[:, :, :3], (41, 1), 0)
        soft = cv2.GaussianBlur(filled.astype(np.float32), (0, 0), sigmaX=3.0)[:, :, None]
        out[:, :, :3] = (out[:, :, :3].astype(np.float32) * (1 - soft)
                         + band.astype(np.float32) * soft).astype(np.uint8)
    return out


def main():
    cap = cv2.VideoCapture(VIDEO)
    if not cap.isOpened():
        raise SystemExit("cannot open " + VIDEO)
    cap.set(cv2.CAP_PROP_POS_FRAMES, round(SECOND * FPS))
    ok, frame = cap.read()
    cap.release()
    if not ok:
        raise SystemExit("cannot read %ss of the clip" % SECOND)

    cut = remove(frame, session=new_session("isnet-general-use"))
    alpha = cut[:, :, 3]
    subject = largest(alpha > 40)
    subject = cv2.morphologyEx(subject.astype(np.uint8), cv2.MORPH_CLOSE,
                               cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (11, 11))) > 0
    subject = fill_holes(largest(subject))
    opaque = subject

    ys, xs = np.where(opaque)
    x0, x1 = int(np.percentile(xs, 0.1)), int(np.percentile(xs, 99.9))
    y0, y1 = int(np.percentile(ys, 0.1)), int(np.percentile(ys, 99.9))
    hard = np.where(opaque, 255, 0).astype(np.uint8)
    colors = repaint_stage_floor(frame, opaque, y1)
    rgba = np.dstack([colors, hard])
    # recover the dropped body on the FULL frame first, then crop: cropping
    # before the recovery loses everything the segmenter dropped below its own
    # bounding box
    rgba = recover_body_from_frame(rgba, frame)
    ys, xs = np.where(rgba[:, :, 3] > 127)
    x0, x1 = int(np.percentile(xs, 0.1)), int(np.percentile(xs, 99.9))
    y0, y1 = int(np.percentile(ys, 0.1)), int(np.percentile(ys, 99.9))
    sprite = rgba[y0:y1 + 1, x0:x1 + 1]
    # Peel off the warm rim light from the stage backlight and the last bright
    # floor line, so the figure has no pale fringe on a dark desktop.
    sprite[:, :, 3] = cv2.erode(sprite[:, :, 3], np.ones((3, 3), np.uint8), iterations=2)
    sprite[-10:, :, 3] = 0

    # stand the figure upright
    h, w = sprite.shape[:2]
    M = cv2.getRotationMatrix2D((w / 2.0, h / 2.0), UPRIGHT_ANGLE, 1.0)
    cosv, sinv = abs(M[0, 0]), abs(M[0, 1])
    nw, nh = int(h * sinv + w * cosv), int(h * cosv + w * sinv)
    M[0, 2] += nw / 2.0 - w / 2.0
    M[1, 2] += nh / 2.0 - h / 2.0
    sprite = cv2.warpAffine(sprite, M, (nw, nh), flags=cv2.INTER_LINEAR,
                            borderValue=(0, 0, 0, 0))
    ys2, xs2 = np.where(sprite[:, :, 3] > 10)
    sprite = sprite[ys2.min():ys2.max() + 1, xs2.min():xs2.max() + 1]
    cv2.imwrite(OUT_SPRITE, sprite)
    print("full-body sprite", OUT_SPRITE, sprite.shape)


if __name__ == "__main__":
    main()
