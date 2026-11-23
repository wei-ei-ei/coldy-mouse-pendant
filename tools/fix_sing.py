# Fill the left lens, make the lower belly symmetric, and animate the
# singing mouth plus a few rising notes.
import os
import cv2
import numpy as np
from PIL import Image

GIF = r"C:\Users\asus\Desktop\coldy\assets\coldy-form4.gif"
PACK = os.path.join(os.environ["TEMP"], "coldy-front-pack")
FRAMES = 16


STILL = r"C:\Users\asus\.cursor\projects\c-Users-asus-Desktop-coldy\assets\image-33894178-0b69-435b-8b03-c39eace6cab6.png"
# Mouth center on the cropped still: the black dot under the glasses.
HEAD_CX = 348.0
MOUTH_Y = 318.0


def build_master():
    im = cv2.imread(STILL)
    h, w = im.shape[:2]
    mask = np.full((h, w), cv2.GC_PR_BGD, np.uint8)
    mask[30:870, 90:w - 70] = cv2.GC_PR_FGD
    mask[890:, :] = cv2.GC_BGD
    mask[:, :40] = cv2.GC_BGD
    mask[:, w - 30:] = cv2.GC_BGD
    mask[:15, :] = cv2.GC_BGD
    bgd = np.zeros((1, 65), np.float64)
    fgd = np.zeros((1, 65), np.float64)
    cv2.grabCut(im, mask, None, bgd, fgd, 4, cv2.GC_INIT_WITH_MASK)
    alpha = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    bgr = im.copy()
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    bright = ((hsv[:, :, 2] > 220) & (hsv[:, :, 1] < 45) & (alpha > 128)).astype(np.uint8) * 255
    count, labels, stats, _ = cv2.connectedComponentsWithStats(bright, 8)
    btn = np.zeros(alpha.shape, np.uint8)
    for c in range(1, count):
        x, y, bw, bh, area = [int(v) for v in stats[c]]
        if area > 2000 and 420 < y < 580 and 280 < x < 520:
            cv2.ellipse(btn, (x + bw // 2, y + bh // 2), (bw // 2 + 22, bh // 2 + 22), 0, 0, 360, 255, -1)
    src = np.roll(bgr, 130, axis=1).astype(np.float32)
    soft = cv2.GaussianBlur(btn, (0, 0), 2).astype(np.float32) / 255.0
    bgr = (src * soft[:, :, None] + bgr.astype(np.float32) * (1.0 - soft[:, :, None])).astype(np.uint8)
    # The ice under the belly is not part of the body.
    alpha[856:] = 0
    ys, xs = np.where(alpha > 128)
    y0, y1 = max(0, int(ys.min()) - 6), min(h, int(ys.max()) + 6)
    x0, x1 = max(0, int(xs.min()) - 6), min(w, int(xs.max()) + 6)
    bgr = bgr[y0:y1, x0:x1].copy()
    alpha = alpha[y0:y1, x0:x1].copy()
    # Lift the original mouth dot so the animated oval is the only mouth.
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    dark = ((alpha > 128) & (hsv[:, :, 2] < 80)).astype(np.uint8) * 255
    count, labels, stats, cent = cv2.connectedComponentsWithStats(dark, 8)
    fur = bgr[max(0, int(MOUTH_Y) - 28), int(HEAD_CX)].astype(np.uint8)
    for c in range(1, count):
        cx, cy = cent[c]
        area = int(stats[c, cv2.CC_STAT_AREA])
        if area < 40 or area > 800:
            continue
        if abs(cx - HEAD_CX) > 24 or abs(cy - MOUTH_Y) > 24:
            continue
        bgr[labels == c] = fur
    return bgr, alpha


def enclosed(alpha):
    h, w = alpha.shape
    inv = np.where(alpha > 128, 0, 255).astype(np.uint8)
    flood = inv.copy()
    ff = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(flood, ff, (2, 2), 0)
    return flood > 0


def body_run(cols, head_cx):
    runs = []
    start = prev = int(cols[0])
    for x in cols[1:]:
        x = int(x)
        if x > prev + 12:
            runs.append((start, prev))
            start = x
        prev = x
    runs.append((start, prev))
    holding = [run for run in runs if run[0] - 8 <= head_cx <= run[1] + 8]
    if holding:
        return max(holding, key=lambda run: run[1] - run[0])
    return max(runs, key=lambda run: run[1] - run[0])


def crown_of(alpha):
    h, w = alpha.shape
    opaque = alpha > 128
    # The spoon bowl is a wide blob on the right. The crown is the first
    # row where the head itself is the widest run.
    for y in range(h):
        xs = np.where(opaque[y])[0]
        if xs.size < 80:
            continue
        breaks = np.where(np.diff(xs) > 18)[0]
        runs = []
        start = 0
        for b in list(breaks) + [len(xs) - 1]:
            run = xs[start:int(b) + 1]
            if run.size:
                runs.append(run)
            start = int(b) + 1
        run = max(runs, key=lambda item: int(item[-1] - item[0]))
        if int(run[-1] - run[0]) < 150:
            continue
        return float(np.median(run)), float(y)
    return w / 2.0, 8.0


def round_belly(bgr, alpha):
    # Cut the flat bottom into the same kind of curve as form 2.
    # Only existing fur is kept, so no second-colored base is added.
    h, w = alpha.shape
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s_img, v_img = hsv[:, :, 1], hsv[:, :, 2]
    spans = []
    for y in range(560, h):
        cols = np.where((alpha[y] > 180) & (s_img[y] < 48) & (v_img[y] > 125))[0]
        if cols.size < 40:
            continue
        spans.append((y, int(cols[0]), int(cols[-1])))
    if len(spans) < 8:
        return bgr, alpha
    y_top = spans[0][0]
    belly = [row for row in spans if row[0] < y_top + 90]
    center = float(np.median([(left + right) / 2.0 for _, left, right in belly]))
    half0 = float(np.median([(right - left) / 2.0 for _, left, right in belly]))
    y0 = belly[-1][0]
    y_tip = spans[-1][0] - 6
    ry = max(30.0, float(y_tip - y0))
    print("round center", round(center, 1), "half", round(half0, 1), "from", y0, "tip", y_tip)
    for y in range(y0, h):
        dy = (y - y0) / ry
        if dy >= 1.0:
            alpha[y] = 0
            continue
        half = half0 * np.sqrt(max(0.0, 1.0 - dy * dy))
        x0 = int(max(0, np.round(center - half)))
        x1 = int(min(w - 1, np.round(center + half)))
        outside = np.ones(w, bool)
        outside[x0:x1 + 1] = False
        # Scarf stays. Pale fur outside the curve goes, including the floor crumbs.
        drop = outside & (alpha[y] > 128) & (s_img[y] < 55)
        alpha[y, drop] = 0
    # Floor crumbs along the cut are dark, the fur is not.
    for y in range(max(0, y_tip - 36), h):
        dark = (alpha[y] > 128) & (v_img[y] < 108)
        alpha[y, dark] = 0
    return bgr, alpha


def fill_lens(bgr, alpha):
    h, w = alpha.shape
    holes = enclosed(alpha)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(holes.astype(np.uint8), 8)
    # The left lens is the wide transparent gap in the upper face.
    best = None
    for c in range(1, n):
        area = int(stats[c, cv2.CC_STAT_AREA])
        if area < 400:
            continue
        top = int(stats[c, cv2.CC_STAT_TOP])
        if top > h * 0.45:
            continue
        cx = stats[c, cv2.CC_STAT_LEFT] + stats[c, cv2.CC_STAT_WIDTH] / 2.0
        if cx > w * 0.55:
            continue
        if best is None or area > best[0]:
            best = (area, c)
    if best is None:
        return
    ys, xs = np.where(labels == best[1])
    bgr[ys, xs] = (16, 14, 18)
    alpha[ys, xs] = 255


def white_at(alpha, s_img, v_img, y, x):
    return alpha[y, x] > 200 and s_img[y, x] < 36 and v_img[y, x] > 145


def fix_belly(bgr, alpha):
    h, w = alpha.shape
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s_img, v_img = hsv[:, :, 1], hsv[:, :, 2]
    # The face center is the midpoint of the two lenses. The mouth hangs
    # just under them, in the middle of the lower head.
    head_cx = 318.0
    mouth_y = 252
    # The wide part of the belly is already even. Below it the cut is flat
    # and the left corner is missing. Continue both sides as one arc.
    samples = []
    for y in range(448, 478):
        cols = np.where((alpha[y] > 200) & (s_img[y] < 40) & (v_img[y] > 150))[0]
        left = cols[(cols > 70) & (cols < 250)]
        right = cols[cols > 360]
        if left.size < 15 or right.size < 15:
            continue
        samples.append((int(left[0]), int(right[-1])))
    left0 = int(np.median([p[0] for p in samples]))
    right0 = int(np.median([p[1] for p in samples]))
    center = (left0 + right0) / 2.0
    right_edge = {}
    for y in range(448, h):
        cols = np.where((alpha[y] > 200) & (s_img[y] < 40) & (v_img[y] > 150) & (np.arange(w) > center))[0]
        if cols.size >= 8:
            right_edge[y] = int(cols[-1])
    join_y = 448
    for y in sorted(right_edge):
        if right_edge[y] > right0 - 55:
            join_y = y
    join_half = max(40.0, right_edge[join_y] - center)
    # The flat bottom is the real fur. A painted cap stays a different color
    # from the lit belly, so the outline is left as it is.
    print("belly center", round(center, 1), "join", join_y, "half", round(float(join_half), 1))
    return bgr, alpha, head_cx, mouth_y


def draw_mouth(bgr, alpha, cx, cy, pulse):
    h, w = bgr.shape[:2]
    rx = 8.0 + 12.0 * pulse
    ry = 10.0 + 14.0 * pulse
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    yy, xx = np.mgrid[0:h, 0:w]
    oval = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1.0
    # The microphone and the wires stay in front of the mouth.
    face = (hsv[:, :, 2] > 75) & (hsv[:, :, 1] < 60)
    paint = oval & (face | (alpha < 128))
    bgr[paint] = (12, 10, 12)
    alpha[paint] = 255


def draw_note(bgr, alpha, x, y, fade, scale):
    if fade <= 0.04 or scale <= 0.05:
        return
    color = (245, 245, 248)
    edge = (20, 20, 24)
    cx, cy = int(round(x)), int(round(y))
    rx = max(2, int(round(14 * scale)))
    ry = max(2, int(round(9 * scale)))
    stem = max(6, int(round(36 * scale)))
    drawn = np.zeros(alpha.shape, np.uint8)
    cv2.ellipse(drawn, (cx, cy), (rx, ry), -22, 0, 360, 255, -1)
    stem_x = cx + int(round(10 * scale))
    thick = max(1, int(round(3 * scale)))
    cv2.line(drawn, (stem_x, cy - 2), (stem_x, cy - stem), 255, thick)
    cv2.line(drawn, (stem_x, cy - stem), (stem_x + int(round(14 * scale)), cy - int(round(stem * 0.72))), 255, thick)
    # The stem must stay below the spoon, or the rope hangs from a note.
    if cy - stem < 120:
        return
    open_px = alpha < 200
    paint = (drawn > 0) & open_px
    rim = (cv2.dilate(drawn, np.ones((3, 3), np.uint8)) > 0) & (drawn == 0) & open_px
    bgr[rim] = edge
    bgr[paint] = color
    alpha[rim | paint] = np.clip(fade * 255, 0, 255).astype(np.uint8)


def notes_for(frame, mic_x, mic_y, bgr, alpha):
    # Born at the microphone, on the right, then rising.
    for i in range(3):
        life = (frame / float(FRAMES) + i / 3.0) % 1.0
        if life < 0.04:
            fade = life / 0.04
        elif life < 0.28:
            fade = 0.35 + 0.65 * ((life - 0.04) / 0.24)
        elif life > 0.78:
            fade = max(0.0, (1.0 - life) / 0.22)
        else:
            fade = 1.0
        x = mic_x + life * 48
        y = mic_y - life * 190
        scale = 0.28 + 0.85 * life
        draw_note(bgr, alpha, x, y, float(fade), float(scale))


def bob_body(bgr, alpha, amp, split):
    # The head stays put. The belly eases down as the note opens.
    h, w = bgr.shape[:2]
    yy = np.arange(h, dtype=np.float32)
    t = np.clip((yy - split) / 220.0, 0.0, 1.0)
    t = t * t * (3.0 - 2.0 * t)
    map_y = np.clip(yy - amp * t, 0, h - 1).astype(np.float32)
    map_x = np.tile(np.arange(w, dtype=np.float32), (h, 1))
    map_y = np.tile(map_y[:, None], (1, w))
    out_b = cv2.remap(bgr, map_x, map_y, cv2.INTER_NEAREST, borderMode=cv2.BORDER_CONSTANT)
    out_a = cv2.remap(alpha, map_x, map_y, cv2.INTER_NEAREST, borderMode=cv2.BORDER_CONSTANT)
    return out_b, out_a


def main():
    bgr, alpha = build_master()
    fill_lens(bgr, alpha)
    bgr, alpha = round_belly(bgr, alpha)
    head_cx, mouth_y = HEAD_CX, MOUTH_Y
    # Right side of the microphone, where a note can leave the sprite.
    # Just outside the microphone, on the body's right.
    mic_x, mic_y = 575.0, 370.0
    print("head", round(head_cx, 1), "mouth_y", mouth_y, "shape", bgr.shape)
    cx, cy = crown_of(alpha)
    h, w = alpha.shape
    pad_x, pad_y = 130, 90
    canvas_w = w + pad_x
    canvas_h = h + pad_y + 40
    crown = (pad_x * 0.42 + cx, pad_y + cy)
    shift_x = crown[0] - cx
    shift_y = crown[1] - cy
    if os.path.isdir(PACK):
        for name in os.listdir(PACK):
            if name.endswith(".png"):
                os.remove(os.path.join(PACK, name))
    else:
        os.makedirs(PACK)
    for i in range(FRAMES):
        pulse = 0.5 - 0.5 * np.cos(2 * np.pi * i / FRAMES)
        # Open mouth, belly settles. Closed mouth, belly lifts. No side sway.
        amp = 36.0 * (pulse * 2.0 - 1.0)
        bb, ba = bob_body(bgr, alpha, amp, 500)
        rgba = cv2.cvtColor(bb, cv2.COLOR_BGR2BGRA)
        rgba[:, :, 3] = ba
        matrix = np.array([[1.0, 0.0, shift_x], [0.0, 1.0, shift_y]], np.float32)
        warped = cv2.warpAffine(rgba, matrix, (int(canvas_w), int(canvas_h)), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
        wbgr = warped[:, :, :3].copy()
        wa = warped[:, :, 3].copy()
        draw_mouth(wbgr, wa, head_cx + shift_x, mouth_y + shift_y, float(pulse))
        notes_for(i, mic_x + shift_x, mic_y + shift_y, wbgr, wa)
        wa = np.where(wa > 128, 255, 0).astype(np.uint8)
        # Drop the loose ice chips under the belly. Notes sit higher.
        count, labels, stats, _ = cv2.connectedComponentsWithStats((wa > 128).astype(np.uint8), 8)
        for c in range(1, count):
            if int(stats[c, cv2.CC_STAT_AREA]) < 4000 and int(stats[c, cv2.CC_STAT_TOP]) > wa.shape[0] * 0.72:
                wa[labels == c] = 0
        out = cv2.cvtColor(wbgr, cv2.COLOR_BGR2BGRA)
        out[:, :, 3] = wa
        cv2.imwrite(os.path.join(PACK, "f%02d.png" % i), out)
    # Previews of a small mouth and a large mouth.
    bg = np.full((int(canvas_h), int(canvas_w), 3), 30, np.uint8)
    for name, idx in (("open", 8), ("shut", 0)):
        im = cv2.imread(os.path.join(PACK, "f%02d.png" % idx), cv2.IMREAD_UNCHANGED)
        al = (im[:, :, 3].astype(np.float32) / 255.0)[:, :, None]
        prev = (im[:, :, :3].astype(np.float32) * al + bg * (1 - al)).astype(np.uint8)
        cv2.imwrite(os.path.join(os.environ["TEMP"], "coldy-now", name + ".jpg"), prev)
    print("wrote", FRAMES, "canvas", int(canvas_w), int(canvas_h))


if __name__ == "__main__":
    main()
