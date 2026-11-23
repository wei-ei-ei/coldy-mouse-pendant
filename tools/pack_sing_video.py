# The approved singing look is assets/coldy-form4-pose.png.
# Motion is drawn on that picture. Do not rebuild the body from the video.
import os
import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
POSE = os.path.join(ROOT, "assets", "coldy-form4-pose.png")
PACK = os.path.join(os.environ["TEMP"], "coldy-front-pack")
FRAMES = 96
# Upright pose: glasses seated, microphone whole, mouth a circle above the mic.
MOUTH_X, MOUTH_Y = 382, 414
# Ice colors, BGR. Blue, frost, lilac, scarf blue, silver, mint.
COLORS = (
    (240, 186, 110),
    (232, 220, 190),
    (220, 168, 188),
    (210, 150, 78),
    (226, 222, 214),
    (186, 214, 168),
)


def key_magenta(im):
    g = im[:, :, 1].astype(np.int16)
    r = im[:, :, 2].astype(np.int16)
    b = im[:, :, 0].astype(np.int16)
    bg = (g < 110) & (r > 150) & (b > 150) & (np.abs(r - b) < 70)
    alpha = np.where(bg, 0, 255).astype(np.uint8)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    alpha = cv2.morphologyEx(alpha, cv2.MORPH_CLOSE, kernel)
    alpha = bridge(alpha)
    count, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 128).astype(np.uint8), 8)
    if count <= 1:
        return alpha
    keep = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    alpha = np.where(labels == keep, 255, 0).astype(np.uint8)
    h = alpha.shape[0]
    for y in range(h - 1, int(h * 0.7), -1):
        if int((alpha[y] > 128).sum()) < 40:
            alpha[y] = 0
        else:
            break
    return alpha


def bridge(alpha):
    # One-pixel gaps in the spoon make the rope grab the spoon instead of the head.
    gap = (alpha[:, 1:-1] == 0) & (alpha[:, :-2] > 128) & (alpha[:, 2:] > 128)
    alpha = alpha.copy()
    alpha[:, 1:-1][gap] = 255
    return alpha


def spans_of(flag):
    spans = []
    start = None
    for i, on in enumerate(flag):
        if on and start is None:
            start = i
        if (not on) and start is not None:
            spans.append((start, i - 1))
            start = None
    if start is not None:
        spans.append((start, len(flag) - 1))
    return spans


def _inward_color(bgr, y, src):
    x = max(0, src - 6)
    return bgr[y, x]


def _is_fur(color):
    mx = int(color.max())
    mn = int(color.min())
    return mx > 185 and (mx - mn) < 45


def extend_right(bgr, alpha, extra):
    # The clip shears the right side. Fur is continued into a rounded cap.
    # Wires are trimmed onto a curve, so neither side stays a straight cut.
    h, w = alpha.shape
    out_b = np.zeros((h, w + extra, 3), np.uint8)
    out_a = np.zeros((h, w + extra), np.uint8)
    out_b[:, :w] = bgr
    out_a[:, :w] = alpha
    right = np.full(h, -1, np.int32)
    for y in range(h):
        cols = np.where(alpha[y] > 128)[0]
        if cols.size:
            right[y] = int(cols[-1])
    clipped = right >= (w - 14)
    for y0, y1 in spans_of(clipped):
        height = y1 - y0 + 1
        if height < 10:
            continue
        samples = [_inward_color(bgr, y, int(right[y])) for y in range(y0, y1 + 1, 2) if right[y] >= 0]
        if not samples:
            continue
        fur = _is_fur(np.median(np.array(samples), axis=0))
        amp = min(extra, max(14, int(round(height * 0.30))))
        for y in range(y0, y1 + 1):
            t = (y - y0) / float(max(1, y1 - y0))
            depth = int(round(amp * np.sin(np.pi * t) ** 0.8))
            src = int(right[y])
            if src < 0 or depth <= 0:
                continue
            if fur:
                sample = _inward_color(bgr, y, src)
                if not _is_fur(sample):
                    continue
                end = src + 1 + depth
                out_b[y, src + 1:end] = sample
                out_a[y, src + 1:end] = 255
            else:
                cut = src - depth
                if cut < src:
                    out_a[y, cut + 1:src + 1] = 0
    return out_b, out_a


def _lighten(color, amount):
    return tuple(int(c + (255 - c) * amount) for c in color)


def _curve(p0, p1, p2, p3):
    pts = []
    for step in range(18):
        t = step / 17.0
        u = 1.0 - t
        x = u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0]
        y = u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]
        pts.append((int(round(x)), int(round(y))))
    return np.array(pts, np.int32)


def draw_note(bgr, alpha, x, y, scale, color, ceiling):
    # Eighth note: tilted head with a highlight, stem, and a curved flag.
    cx, cy = int(round(x)), int(round(y))
    rx = max(8, int(round(15 * scale)))
    ry = max(6, int(round(10 * scale)))
    stem = max(22, int(round(46 * scale)))
    if cy - stem < ceiling:
        stem = cy - int(ceiling)
        if stem < 28:
            return
    h, w = alpha.shape
    if cx < 4 or cy < 4 or cx >= w - 4 or cy >= h - 4:
        return
    tilt = -26
    drawn = np.zeros(alpha.shape, np.uint8)
    shine = np.zeros(alpha.shape, np.uint8)
    cv2.ellipse(drawn, (cx, cy), (rx, ry), tilt, 0, 360, 255, -1)
    cv2.ellipse(shine, (cx - rx // 5, cy - ry // 3), (max(3, rx // 3), max(2, ry // 3)), tilt, 0, 360, 255, -1)
    sx = cx + int(round(rx * 0.55))
    top = cy - stem
    thick = max(3, int(round(3.2 * scale)))
    cv2.line(drawn, (sx, cy - max(1, ry // 5)), (sx, top), 255, thick)
    s = float(scale)
    flag = _curve(
        (sx, top),
        (sx + 16 * s, top - 8 * s),
        (sx + 28 * s, top + 10 * s),
        (sx + 8 * s, top + 26 * s),
    )
    cv2.polylines(drawn, [flag], False, 255, thick, cv2.LINE_AA)
    # A second, shorter sweep so the flag reads as cloth, not a stick.
    inner = _curve(
        (sx, top + 8 * s),
        (sx + 12 * s, top + 2 * s),
        (sx + 20 * s, top + 16 * s),
        (sx + 6 * s, top + 28 * s),
    )
    cv2.polylines(drawn, [inner], False, 255, max(2, thick - 1), cv2.LINE_AA)
    open_px = alpha < 200
    paint = (drawn > 0) & open_px
    gloss = (shine > 0) & paint
    rim = (cv2.dilate(drawn, np.ones((3, 3), np.uint8)) > 0) & (drawn == 0) & open_px
    bgr[rim] = (18, 18, 22)
    bgr[paint] = color
    bgr[gloss] = _lighten(color, 0.62)
    alpha[rim | paint] = 255


def circle_mouth(bgr, alpha, openness):
    # Remove whatever mouth the clip drew (circle, crescent, or split lobes)
    # and put back one circle. Its size follows the body's sway.
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    v = hsv[:, :, 2]
    s = hsv[:, :, 1]
    h, w = alpha.shape
    rows = np.where((alpha > 128).any(axis=1))[0]
    if rows.size == 0:
        return
    top = int(rows[0])
    dark_rows = ((alpha[:, 320:490] > 128) & (v[:, 320:490] < 88)).sum(axis=1)
    smooth = np.convolve(dark_rows.astype(np.float32), np.ones(5) / 5.0, mode="same")
    # Glasses, then a light gap, then the mouth. Start under the gap.
    y = top + 260
    limit = min(h - 1, top + 520)
    seen = False
    gap_at = None
    while y < limit:
        if smooth[y] > 18:
            seen = True
        elif seen and smooth[y] < 8:
            gap_at = y
            break
        y += 1
    if gap_at is None:
        gap_at = top + 330
    y0 = gap_at + 2
    y1 = min(h - 1, y0 + 150)
    zone = ((alpha > 128) & (v < 96) & (s < 130)).astype(np.uint8)
    zone[:y0, :] = 0
    zone[y1:, :] = 0
    zone[:, :280] = 0
    zone[:, 530:] = 0
    count, labels, stats, _ = cv2.connectedComponentsWithStats(zone, 8)
    mouth = np.zeros_like(zone)
    for c in range(1, count):
        area = int(stats[c, cv2.CC_STAT_AREA])
        if area < 25:
            continue
        bx, by, bw, bh = [int(stats[c, k]) for k in range(4)]
        pixels = labels == c
        # The microphone climbs into this band. Keep only the part above it.
        if by + bh >= y1 - 2 and bh > 36:
            cut = _cut_above_mic(pixels, by)
            pixels = pixels.copy()
            pixels[cut:] = False
            if int(pixels.sum()) < 25:
                continue
        mouth[pixels] = 255
    if int(mouth.max()) == 0:
        return
    grown = cv2.dilate(mouth, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9)))
    grown[y1 - 4:, :] = 0
    color = _face_color(bgr, alpha, grown)
    bgr[grown > 0] = color
    edge = cv2.dilate(grown, np.ones((5, 5), np.uint8))
    feather = (edge > 0) & (grown == 0) & (alpha > 128)
    blur = cv2.GaussianBlur(bgr, (0, 0), 1.6)
    bgr[feather] = blur[feather]
    my, mx = np.where(mouth > 0)
    cx = int(round(float(mx.mean())))
    cy = int(round(float(my.mean())))
    radius = int(round(11 + 17 * float(openness)))
    disk = np.zeros(alpha.shape, np.uint8)
    cv2.circle(disk, (cx, cy), max(8, radius), 255, -1)
    # Anything dark left in the mouth window, other than the new circle, is a leftover piece.
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    window = np.zeros(alpha.shape, bool)
    window[max(0, int(my.min()) - 10):min(h, int(my.max()) + 16),
            max(0, int(mx.min()) - 14):min(w, int(mx.max()) + 14)] = True
    window[disk > 0] = False
    window[y1 - 6:, :] = False
    crumb = window & (alpha > 128) & (hsv[:, :, 2] < 80) & (hsv[:, :, 1] < 110)
    if crumb.any():
        bgr[crumb] = color
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    face = (alpha > 128) & (hsv[:, :, 1] < 120) & (hsv[:, :, 2] > 100)
    bgr[(disk > 0) & face] = (10, 10, 14)


def _cut_above_mic(pixels, top):
    rows = np.where(pixels.any(axis=1))[0]
    rights = []
    for yy in rows:
        cols = np.where(pixels[yy])[0]
        rights.append((int(yy), int(cols.max())))
    if not rights:
        return top
    base = float(np.median([r for _, r in rights[:max(3, len(rights) // 5)]]))
    prev = base
    for yy, r in rights:
        if yy > top + 16 and r > base + 42 and (r - prev) > 10:
            return yy
        prev = r
    return int(rows[min(len(rows) - 1, int(len(rows) * 0.55))])


def _face_color(bgr, alpha, mask):
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    ring = cv2.dilate(mask, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (23, 23)))
    face = (ring > 0) & (mask == 0) & (alpha > 128) & (hsv[:, :, 1] < 45) & (hsv[:, :, 2] > 175)
    if int(face.sum()) < 20:
        face = (ring > 0) & (mask == 0) & (alpha > 128) & (hsv[:, :, 1] < 80) & (hsv[:, :, 2] > 150)
    if int(face.sum()) < 8:
        return (198, 199, 202)
    med = np.median(bgr[face], axis=0)
    return (int(med[0]), int(med[1]), int(med[2]))


def notes_for(frame, mic_x, mic_y, bgr, alpha, ceiling):
    # Four notes, each a different color and a different curve.
    lanes = (
        (10, 0.25, 1.15, 0.3, 0),
        (34, 1.2, 0.7, 1.8, 2),
        (18, 2.15, 0.5, 3.4, 3),
        (46, 0.7, 0.95, 5.0, 5),
    )
    for i, (x0, drift, rise, phase, color_i) in enumerate(lanes):
        life = (frame / float(FRAMES) + i / 4.0) % 1.0
        if life < 0.08 or life > 0.86:
            continue
        wobble = np.sin(life * np.pi * 2.0 + phase) * (8 + 14 * life)
        x = mic_x + x0 * 0.35 + drift * 36 * life + wobble
        if x < mic_x:
            x = mic_x
        # Leave the mouth and float up beside the head.
        y = mic_y - (8 * life + 240 * rise * life * life)
        scale = 2.0 * (0.5 + 0.74 * life)
        draw_note(bgr, alpha, x, y, scale, COLORS[color_i], ceiling)


def lean_of(alpha):
    ys, xs = np.where(alpha > 128)
    if ys.size < 100:
        return 0.0
    top = int(ys.min())
    head = (ys > top + 80) & (ys < top + 220)
    belly = ys > top + 400
    if not np.any(head) or not np.any(belly):
        return 0.0
    return float(xs[head].mean() - xs[belly].mean())


def round_belly_arc(bgr, alpha):
    # The frame cuts the right belly flat. Rebuild that side as the circle
    # already drawn by the uncut outline, so it bows outward in one arc.
    h, w = alpha.shape
    right = np.full(h, -1, np.int32)
    for y in range(h):
        cols = np.where(alpha[y] > 128)[0]
        if cols.size:
            right[y] = int(cols[-1])
    pts = []
    for y in range(540, min(h, 980)):
        src = int(right[y])
        if src < 8 or src >= w - 4:
            continue
        if y < 660 or y > 830:
            if _is_fur(_inward_color(bgr, y, src)):
                pts.append((float(src), float(y)))
    if len(pts) < 30:
        return bgr, alpha
    pts = np.array(pts, np.float64)
    xs, ys = pts[:, 0], pts[:, 1]
    fit = np.c_[2 * xs, 2 * ys, np.ones(len(pts))]
    cx, cy, const = np.linalg.lstsq(fit, xs * xs + ys * ys, rcond=None)[0]
    radius = float(np.sqrt(max(1.0, const + cx * cx + cy * cy)))
    extra = 32
    out_b = np.zeros((h, w + extra, 3), np.uint8)
    out_a = np.zeros((h, w + extra), np.uint8)
    out_b[:, :w] = bgr
    out_a[:, :w] = alpha
    for y in range(640, 910):
        src = int(right[y])
        if src < 8 or not _is_fur(_inward_color(bgr, y, src)):
            continue
        dy = y - cy
        if abs(dy) >= radius:
            continue
        target = int(round(cx + np.sqrt(radius * radius - dy * dy)))
        target = max(0, min(w + extra - 1, target))
        cut = src >= w - 6
        if not cut and abs(src - target) > 16:
            continue
        if target > src:
            out_b[y, src + 1:target + 1] = _inward_color(bgr, y, src)
            out_a[y, src + 1:target + 1] = 255
        elif target < src:
            out_a[y, target + 1:] = 0
    return out_b, out_a


def clear_mouth(bgr, alpha):
    # Lift the clip's mouth off the cheek. The microphone is left where it is,
    # so it stays in front of the mouth on every frame.
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    v = hsv[:, :, 2]
    h, w = alpha.shape
    yy, xx = np.ogrid[:h, :w]
    disk = (xx - MOUTH_X) ** 2 + (yy - MOUTH_Y) ** 2 <= 52 ** 2
    mic = (xx > 450) & (yy > 448)
    mouth = (disk & (alpha > 128) & (v < 170) & ~mic).astype(np.uint8) * 255
    mouth = cv2.dilate(mouth, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    mouth[mic] = 0
    if int(mouth.max()) == 0:
        return
    bgr[:] = cv2.inpaint(bgr, mouth, 8, cv2.INPAINT_TELEA)


def draw_mouth(bgr, alpha, openness):
    radius = int(round(12 + 20 * float(openness)))
    h, w = alpha.shape
    yy, xx = np.ogrid[:h, :w]
    circ = (xx - MOUTH_X) ** 2 + (yy - MOUTH_Y) ** 2 <= radius ** 2
    mic = (xx > 450) & (yy > 448)
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    face = (alpha > 128) & (hsv[:, :, 1] < 80) & (hsv[:, :, 2] > 130) & ~mic
    bgr[circ & face] = (8, 8, 12)


def rope_pivot(alpha):
    # Same point the pendant uses for the rope: the top of the head, left of the spoon.
    h, w = alpha.shape
    opaque = alpha > 128
    top = -1
    spoon_l = spoon_r = 0
    for y in range(h):
        xs = np.where(opaque[y])[0]
        if xs.size == 0:
            continue
        top = y
        spoon_l = int(xs[0])
        spoon_r = spoon_l
        while spoon_r + 1 < w and opaque[y, spoon_r + 1]:
            spoon_r += 1
        break
    if top < 0:
        return (w / 2.0, 8.0)
    head_y = -1
    for y in range(top, h):
        x = 0
        found = False
        while x < w:
            while x < w and not opaque[y, x]:
                x += 1
            if x >= w:
                break
            left = x
            while x < w and opaque[y, x]:
                x += 1
            right = x - 1
            if right < spoon_l - 2:
                head_y = y
                found = True
                break
            if left <= spoon_r + 2:
                if left < spoon_l:
                    spoon_l = left
                if right > spoon_r:
                    spoon_r = right
        if found:
            break
    if head_y < 0:
        head_y = top
    sx = sy = n = 0
    bottom = min(h - 1, head_y + 5)
    for y in range(head_y, bottom + 1):
        x = 0
        while x < w and not opaque[y, x]:
            x += 1
        while x < w and opaque[y, x]:
            sx += x
            sy += y
            n += 1
            x += 1
    if n == 0:
        return (w / 2.0, float(head_y))
    return (sx / float(n), sy / float(n))


def load_pose():
    im = cv2.imread(POSE, cv2.IMREAD_UNCHANGED)
    if im is None or im.ndim != 3 or im.shape[2] != 4:
        raise RuntimeError("locked pose missing")
    return im[:, :, :3].copy(), np.where(im[:, :, 3] > 128, 255, 0).astype(np.uint8)




def main():
    im, alpha = load_pose()
    # Room on every side so the swing never meets the canvas and gets sliced off.
    pad_left, pad_top, pad_right, pad_bottom = 200, 48, 320, 72
    sample_h, sample_w = im.shape[:2]
    width = sample_w + pad_left + pad_right
    height = sample_h + pad_top + pad_bottom
    if os.path.isdir(PACK):
        for name in os.listdir(PACK):
            if name.endswith(".png"):
                os.remove(os.path.join(PACK, name))
    else:
        os.makedirs(PACK)
    for i in range(FRAMES):
        turn = i / float(FRAMES)
        # The whole body dips and comes back. The mouth opens at the bottom.
        wave = 0.5 - 0.5 * np.cos(turn * np.pi * 2.0)
        openness = wave
        frame_b = im.copy()
        frame_a = alpha.copy()
        draw_mouth(frame_b, frame_a, openness)
        h, w = frame_a.shape
        canvas_b = np.zeros((height, width, 3), np.uint8)
        canvas_a = np.zeros((height, width), np.uint8)
        canvas_b[pad_top:pad_top + h, pad_left:pad_left + w] = frame_b
        canvas_a[pad_top:pad_top + h, pad_left:pad_left + w] = frame_a
        ys, xs = np.where(canvas_a > 128)
        ceiling = int(ys.min()) + 96
        mouth_y = pad_top + MOUTH_Y
        rights = []
        for row_y in range(mouth_y - 8, mouth_y + 18):
            row = np.where(canvas_a[row_y] > 128)[0]
            if row.size:
                rights.append(int(row[-1]))
        mouth_x = int(np.median(rights)) + 14
        notes_for(i, mouth_x, mouth_y, canvas_b, canvas_a, ceiling)
        canvas_a = np.where(canvas_a > 128, 255, 0).astype(np.uint8)
        out = cv2.cvtColor(canvas_b, cv2.COLOR_BGR2BGRA)
        out[:, :, 3] = canvas_a
        cv2.imwrite(os.path.join(PACK, "f%02d.png" % i), out)
        print(i, "open", round(float(openness), 2))
    print("frames", FRAMES, "canvas", width, height)


if __name__ == "__main__":
    main()
