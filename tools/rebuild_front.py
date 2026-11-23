# Rebuild the front singing pose so the belly sits upright under the head
# and the cut bottom becomes the wide rounded body from the reference frame.
import os
import cv2
import numpy as np

VIDEO = r"C:\Users\asus\.cursor\projects\c-Users-asus-Desktop-coldy\assets\coldy-front.mp4"
OUT = os.path.join(os.environ["TEMP"], "coldy-rebuild")


def enclosed(alpha):
    h, w = alpha.shape
    inv = np.where(alpha > 128, 0, 255).astype(np.uint8)
    flood = inv.copy()
    ff = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(flood, ff, (2, 2), 0)
    return flood > 0


def character(bgr):
    h, w = bgr.shape[:2]
    mx = bgr.max(axis=2)
    alpha = np.where(mx > 28, 255, 0).astype(np.uint8)
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    # Drop the pale unfinished floor at the bottom of the frame.
    cut = h
    for y in range(h - 1, h // 2, -1):
        cols = np.where(alpha[y] > 0)[0]
        if cols.size < 40:
            continue
        row = bgr[y, cols]
        if row.std() < 16 and row.mean() > 140:
            cut = y
            alpha[y] = 0
        else:
            break
    return alpha, hsv[:, :, 1], hsv[:, :, 2], cut


def fill_face(bgr, alpha, s_img):
    h, w = alpha.shape
    opaque = alpha > 128
    bright = opaque & (s_img < 48)
    rows = np.where(bright.any(axis=1))[0]
    if rows.size == 0:
        return
    head_top = int(rows[0])
    center = slice(w // 5, 4 * w // 5)
    sat_rows = np.where((opaque & (s_img > 48))[:, center].any(axis=1))[0]
    scarf_y = int(np.percentile(sat_rows, 20)) if sat_rows.size else head_top + 160
    holes = enclosed(alpha)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(holes.astype(np.uint8), 8)
    dark = opaque & (cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)[:, :, 2] < 80)
    ink = np.median(bgr[dark], axis=0).astype(np.uint8) if dark.any() else np.array([24, 22, 20], np.uint8)
    face_xs = np.where(bright[head_top:scarf_y].any(axis=0))[0]
    head_cx = float(np.median(face_xs)) if face_xs.size else w / 2.0
    # Close gaps inside the left lens even when they open onto the outline.
    left_dark = dark & (np.arange(w)[None, :] < head_cx - 6)
    left_dark &= np.arange(h)[:, None] < head_top + int((scarf_y - head_top) * 0.48)
    left_dark &= np.arange(h)[:, None] > head_top + 8
    pts = np.column_stack(np.where(left_dark)[::-1])
    if pts.shape[0] > 12:
        hull = cv2.convexHull(pts.astype(np.int32))
        lens = np.zeros((h, w), np.uint8)
        cv2.fillConvexPoly(lens, hull, 255)
        gap = (lens > 0) & (alpha < 128)
        bgr[gap] = ink
        alpha[gap] = 255

    for c in range(1, n):
        area = stats[c, cv2.CC_STAT_AREA]
        if area < 60 or area > 9000:
            continue
        cy = stats[c, cv2.CC_STAT_TOP] + stats[c, cv2.CC_STAT_HEIGHT] / 2.0
        cx = stats[c, cv2.CC_STAT_LEFT] + stats[c, cv2.CC_STAT_WIDTH] / 2.0
        if cy < head_top or cy > scarf_y + 10:
            continue
        ys, xs = np.where(labels == c)
        # Left lens sits above the mouth and left of the face center.
        if cy < head_top + (scarf_y - head_top) * 0.55 and cx < head_cx - 8:
            bgr[ys, xs] = ink
            alpha[ys, xs] = 255
        elif cy >= head_top + (scarf_y - head_top) * 0.42 and abs(cx - head_cx) < w * 0.12:
            bgr[ys, xs] = (16, 14, 14)
            alpha[ys, xs] = 255
            rx = max(7, int((xs.max() - xs.min()) / 2) + 1)
            ry = max(8, int((ys.max() - ys.min()) / 2) + 1)
            cv2.ellipse(bgr, (int(round(cx)), int(round(cy))), (rx, ry), 0, 0, 360, (16, 14, 14), -1)
            cv2.ellipse(alpha, (int(round(cx)), int(round(cy))), (rx, ry), 0, 0, 360, 255, -1)


def body_run(cols, head_cx):
    runs = []
    start = int(cols[0])
    prev = int(cols[0])
    for x in cols[1:]:
        x = int(x)
        if x > prev + 10:
            runs.append((start, prev))
            start = x
        prev = x
    runs.append((start, prev))
    holding = [run for run in runs if run[0] <= head_cx <= run[1]]
    if holding:
        return max(holding, key=lambda run: run[1] - run[0])
    return max(runs, key=lambda run: run[1] - run[0])


def smooth_delta(delta):
    known = np.isfinite(delta)
    if known.sum() < 5:
        return np.zeros_like(delta)
    idx = np.arange(delta.size)
    filled = np.interp(idx, idx[known], delta[known])
    k = 31
    kernel = np.ones(k) / float(k)
    padded = np.pad(filled, k // 2, mode="edge")
    return np.convolve(padded, kernel, mode="valid")


def pad(bgr, alpha, top, bottom, left, right):
    h, w = alpha.shape
    out_b = np.zeros((h + top + bottom, w + left + right, 3), np.uint8)
    out_a = np.zeros((h + top + bottom, w + left + right), np.uint8)
    out_b[top:top + h, left:left + w] = bgr
    out_a[top:top + h, left:left + w] = alpha
    return out_b, out_a


def rebuild(bgr):
    bgr = bgr.copy()
    alpha, s_img, v_img, cut = character(bgr)
    fill_face(bgr, alpha, s_img)
    # Room under the cut bottom. The head, scarf and microphone stay where they are.
    bgr, alpha = pad(bgr, alpha, 8, 150, 30, 30)
    h, w = alpha.shape
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s_img, v_img = hsv[:, :, 1], hsv[:, :, 2]
    opaque = alpha > 128
    fur = opaque & (s_img < 42) & (v_img > 95)
    sat = opaque & (s_img > 48)

    face_rows = np.where(fur.any(axis=1))[0]
    head_top = int(face_rows[0])
    sat_count = sat.sum(axis=1)
    wide = np.where(sat_count > 80)[0]
    wide = wide[wide > head_top + 50]
    scarf_top = int(wide[0]) if wide.size else head_top + 150
    scarf_bot = int(wide[min(len(wide) - 1, 18)]) if wide.size else scarf_top + 36
    face_band = fur[head_top + 10:max(head_top + 11, scarf_top - 10)]
    xs = np.where(face_band.any(axis=0))[0]
    if xs.size:
        gaps = np.where(np.diff(xs) > 15)[0]
        if gaps.size:
            xs = xs[:gaps[0] + 1]
        head_cx = float(np.median(xs))
    else:
        head_cx = w / 2.0

    # The lower belly is already under the head. The right edge and the
    # bottom are cut off, so the outline is finished around that same axis.
    sat_near = cv2.dilate((sat.astype(np.uint8) * 255), np.ones((7, 7), np.uint8)) > 0
    body = fur & ~sat_near
    samples = []
    for y in range(scarf_bot + 30, h):
        cols = np.where(body[y])[0]
        if cols.size < 80:
            continue
        run = body_run(cols, head_cx)
        if run is None:
            continue
        left, right = run
        if right - left < 160 or right < head_cx + 40:
            continue
        samples.append((y, left, right))
    if not samples:
        print("no belly")
        return bgr, alpha
    # Use the full middle of the belly, before the right side gets bitten off.
    good = [item for item in samples if item[2] - head_cx > 90 and head_cx - item[1] > 70]
    use = good if len(good) > 6 else samples
    half_w = float(np.percentile([max(head_cx - left, right - head_cx) for _, left, right in use], 80))
    cut_y = int(np.percentile([y for y, _, _ in use], 92))
    extra = int(half_w * 0.42)
    bottom = min(h - 2, cut_y + extra)
    cy = float(cut_y - half_w * 0.15)
    half_h = max(30.0, bottom - cy)
    donor_y = int(np.median([y for y, _, _ in use]))
    donor = bgr[donor_y]
    print("head", round(head_cx, 1), "half", round(half_w, 1), "cut", cut_y, "bottom", bottom, "donor", donor_y)
    yy, xx = np.mgrid[0:h, 0:w]
    dx = np.abs(xx - head_cx) / max(half_w * 1.04, 1.0)
    dy = np.clip(yy - cy, 0, None) / max(half_h, 1.0)
    inside = (dx ** 3.2 + dy ** 3.2) <= 1.0
    inside &= yy >= scarf_bot + 24
    # Keep the scarf and the wires. Only fur, or empty space, is reshaped.
    sat_now = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)[:, :, 1] > 48
    editable = inside & ~sat_now & ((alpha < 128) | (yy >= cut_y - 18))
    mask = np.zeros((h, w), np.uint8)
    mask[editable] = 255
    mask[:12, :] = 0
    mask[-12:, :] = 0
    mask[:, :12] = 0
    mask[:, -12:] = 0
    if mask.any():
        band = bgr[max(0, donor_y - 36):donor_y + 12]
        src = cv2.resize(band, (w, h), interpolation=cv2.INTER_CUBIC) if band.size else bgr.copy()
        cloned = cv2.seamlessClone(src, bgr, mask, (w // 2, h // 2), cv2.NORMAL_CLONE)
        painted = mask > 0
        bgr[painted] = cloned[painted]
        alpha[painted] = 255
    close_face(bgr, alpha, head_cx)
    return bgr, alpha


def fill_right_edge(bgr, alpha, head_cx, y0, y1):
    h, w = alpha.shape
    s_img = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)[:, :, 1]
    edge = np.full(h, -1.0)
    for y in range(max(0, y0), min(h, y1)):
        cols = np.where((alpha[y] > 128) & (s_img[y] < 40))[0]
        cols = cols[(cols > head_cx + 20) & (cols < head_cx + 210)]
        if cols.size > 10:
            edge[y] = float(cols.max())
    for y in range(max(0, y0), min(h, y1)):
        window = edge[max(y0, y - 18):min(y1, y + 19)]
        window = window[window > 0]
        if window.size == 0 or edge[y] < 0:
            continue
        target = float(np.percentile(window, 75))
        if target <= edge[y] + 3:
            continue
        for x in range(int(edge[y]) + 1, int(target) + 1):
            if x < 0 or x >= w or alpha[y, x] > 128:
                continue
            src = int(np.clip(round(2 * head_cx - x), 0, w - 1))
            if alpha[y, src] > 128 and s_img[y, src] < 42:
                bgr[y, x] = bgr[y, src]
                alpha[y, x] = 255


def close_face(bgr, alpha, head_cx):
    h, w = alpha.shape
    holes = enclosed(alpha)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(holes.astype(np.uint8), 8)
    ys, xs = np.where(alpha > 128)
    if ys.size == 0:
        return
    face_bot = int(ys.min() + (ys.max() - ys.min()) * 0.46)
    dark = (alpha > 128) & (cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)[:, :, 2] < 70)
    ink = np.median(bgr[dark], axis=0).astype(np.uint8) if dark.any() else np.array([22, 20, 18], np.uint8)
    found = []
    for c in range(1, n):
        area = int(stats[c, cv2.CC_STAT_AREA])
        if area < 80 or area > 8000:
            continue
        cy = stats[c, cv2.CC_STAT_TOP] + stats[c, cv2.CC_STAT_HEIGHT] / 2.0
        cx = stats[c, cv2.CC_STAT_LEFT] + stats[c, cv2.CC_STAT_WIDTH] / 2.0
        if cy > face_bot:
            continue
        found.append((area, c, cx, cy))
    lens = None
    for area, c, cx, cy in found:
        if cx < head_cx - 28 and (lens is None or area > lens[0]):
            lens = (area, c, cx, cy)
    if lens is not None:
        ys, xs = np.where(labels == lens[1])
        bgr[ys, xs] = ink
        alpha[ys, xs] = 255
    mouth = None
    for area, c, cx, cy in found:
        if lens is not None and c == lens[1]:
            continue
        if abs(cx - head_cx) < 48 and (lens is None or cy > lens[3]):
            if mouth is None or area > mouth[0]:
                mouth = (area, c, cx, cy)
    if mouth is not None:
        _, c, cx, cy = mouth
        ys, xs = np.where(labels == c)
        bgr[ys, xs] = (14, 12, 12)
        alpha[ys, xs] = 255


def sat_at(s_img, alpha, y, x):
    return alpha[y, x] > 128 and s_img[y, x] > 48


def main():
    os.makedirs(OUT, exist_ok=True)
    cap = cv2.VideoCapture(VIDEO)
    cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
    ok, frame = cap.read()
    cap.release()
    if not ok:
        raise SystemExit("no frame")
    bgr, alpha = rebuild(frame)
    out = cv2.cvtColor(bgr, cv2.COLOR_BGR2BGRA)
    out[:, :, 3] = alpha
    ys, xs = np.where(alpha > 128)
    print("bbox", int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))
    cv2.imwrite(os.path.join(OUT, "master.png"), out)
    bg = np.full(bgr.shape, 36, np.uint8)
    al = (alpha.astype(np.float32) / 255.0)[:, :, None]
    preview = (bgr.astype(np.float32) * al + bg * (1.0 - al)).astype(np.uint8)
    y0, y1 = max(0, int(ys.min()) - 8), min(preview.shape[0], int(ys.max()) + 8)
    x0, x1 = max(0, int(xs.min()) - 8), min(preview.shape[1], int(xs.max()) + 8)
    cv2.imwrite(os.path.join(OUT, "preview.jpg"), preview[y0:y1, x0:x1])
    print("wrote", os.path.join(OUT, "preview.jpg"))


if __name__ == "__main__":
    main()
