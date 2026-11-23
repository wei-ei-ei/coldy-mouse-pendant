# Close the holes in the left lens and the mouth, and shape the belly
# like the wide rounded body in the singing frame.
import os
import cv2
import numpy as np

PACK = os.path.join(os.environ["TEMP"], "coldy-front-pack")


def enclosed(alpha):
    h, w = alpha.shape
    inv = np.where(alpha > 128, 0, 255).astype(np.uint8)
    flood = inv.copy()
    ff = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(flood, ff, (0, 0), 0)
    return flood > 0


def fix(path):
    im = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    bgr = im[:, :, :3].copy()
    alpha = im[:, :, 3].copy()
    h, w = alpha.shape
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s_img = hsv[:, :, 1]
    v_img = hsv[:, :, 2]
    opaque = alpha > 128
    bright = opaque & (v_img > 150) & (s_img < 48)
    dark = opaque & (v_img < 75)
    sat = opaque & (s_img > 42)

    face_rows = np.where(bright.any(axis=1))[0]
    if face_rows.size == 0:
        return
    center_band = slice(w // 5, 4 * w // 5)
    scarf_rows = np.where(sat[:, center_band].any(axis=1))[0]
    scarf_top = int(np.percentile(scarf_rows, 10)) if scarf_rows.size else int(face_rows[0] + 70)
    scarf_bot = int(np.percentile(scarf_rows, 50)) if scarf_rows.size else scarf_top + 24

    ink = np.array([22, 20, 18], np.uint8)
    if dark.any():
        ink = np.median(bgr[dark], axis=0).astype(np.uint8)
    holes = enclosed(alpha)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(holes.astype(np.uint8), 8)
    for c in range(1, n):
        area = stats[c, cv2.CC_STAT_AREA]
        if area < 80 or area > 8000:
            continue
        cy = stats[c, cv2.CC_STAT_TOP] + stats[c, cv2.CC_STAT_HEIGHT] / 2.0
        cx = stats[c, cv2.CC_STAT_LEFT] + stats[c, cv2.CC_STAT_WIDTH] / 2.0
        ys, xs = np.where(labels == c)
        split = face_rows[0] + 88
        # Left lens: the big gap inside the left glass.
        if cy < split and cx < w * 0.48 and area > 200:
            bgr[ys, xs] = ink
            alpha[ys, xs] = 255
            continue
        # Mouth: the gap just under the glasses, left of the microphone.
        if split - 4 <= cy <= split + 36 and w * 0.40 < cx < w * 0.56:
            bgr[ys, xs] = (14, 12, 12)
            alpha[ys, xs] = 255
            rx = max(6, int((xs.max() - xs.min()) / 2) + 1)
            ry = max(7, int((ys.max() - ys.min()) / 2) + 1)
            cv2.ellipse(bgr, (int(round(cx)), int(round(cy))), (rx, ry), 0, 0, 360, (14, 12, 12), -1)
            cv2.ellipse(alpha, (int(round(cx)), int(round(cy))), (rx, ry), 0, 0, 360, 255, -1)

    # The unfinished floor is a pale band that gets brighter toward the cut.
    center_cols = slice(w // 3, 2 * w // 3)
    row_v = np.full(h, -1.0)
    for y in range(scarf_bot + 16, h):
        mask = opaque[y, center_cols]
        if mask.sum() > 12:
            row_v[y] = np.median(v_img[y, center_cols][mask])
    cut = None
    for y in range(scarf_bot + 28, h):
        if row_v[y] < 0 or row_v[y - 8] < 0:
            continue
        if row_v[y] > row_v[y - 8] + 6 and row_v[y] > 135:
            cut = y
            break
    if cut is not None:
        pale = (np.arange(h)[:, None] >= cut) & (s_img < 40) & (v_img > 110)
        alpha[pale] = 0
        opaque[pale] = False

    # Belly: wide and rounded, broader across the bottom than a circle.
    belly = opaque & (v_img > 100) & (s_img < 40) & (np.arange(h)[:, None] > scarf_bot - 4)
    by, bx = np.where(belly)
    if bx.size > 60:
        cx = float(np.median(bx))
        left_reach = cx - float(np.percentile(bx, 3))
        right_reach = float(np.percentile(bx, 97)) - cx
        half_w = max(left_reach, right_reach) * 1.04
        cur_bot = float(by.max())
        # Broad bottom, like the frame: the belly stays wide and only
        # rounds at the corners, instead of closing into a circle.
        bottom = min(h - 10, cur_bot + half_w * 0.42)
        top = float(scarf_bot - 2)
        half_h = max(20.0, (bottom - top) / 2.0)
        cy = (top + bottom) / 2.0
        yy, xx = np.mgrid[0:h, 0:w]
        dx = np.abs(xx - cx) / max(half_w, 1.0)
        dy = np.abs(yy - cy) / max(half_h, 1.0)
        inside = (dx ** 4.2 + dy ** 4.2) <= 1.0
        inside &= yy >= scarf_bot - 6
        fur = (alpha > 128) & (v_img > 100) & (s_img < 40) & (yy > scarf_bot + 4)
        last = np.max(np.where(fur, yy, -1), axis=0)
        good = last[last > scarf_bot]
        edge_y = int(np.median(good)) if good.size else int(cur_bot)
        fill = inside & (alpha < 128)
        y0 = max(scarf_bot + 10, edge_y - 40)
        y1 = max(y0 + 8, edge_y - 2)
        band = bgr[y0:y1]
        fill_rows = np.where(fill.any(axis=1))[0]
        if fill_rows.size and band.shape[0] > 4:
            stretched = cv2.resize(band, (w, int(fill_rows.max() - edge_y + 2)), interpolation=cv2.INTER_CUBIC)
            for y in fill_rows:
                dist = max(0, int(y) - edge_y)
                shade = 0.99 - 0.06 * min(1.0, dist / 80.0)
                cols = np.where(fill[y])[0]
                row = stretched[min(stretched.shape[0] - 1, dist), cols].astype(np.float32)
                if dist < 10:
                    edge = bgr[min(h - 1, edge_y), cols].astype(np.float32)
                    mix = dist / 10.0
                    row = edge * (1.0 - mix) + row * mix
                bgr[y, cols] = np.clip(row * shade, 0, 255)
                alpha[y, cols] = 255
        lower = inside & (yy > edge_y - 8) & (s_img < 36) & (alpha > 128)
        if lower.any():
            blur = cv2.blur(bgr, (1, 15))
            mixed = bgr.astype(np.float32) * 0.4 + blur.astype(np.float32) * 0.6
            bgr[lower] = np.clip(mixed, 0, 255)[lower]
        # The dark bite on the lower right is shadow from the cut, not the outline.
        bite = inside & (yy > scarf_bot + 16) & (v_img < 80) & (s_img < 35) & (alpha > 128)
        if bite.any() and fill.any():
            donor = bgr[min(h - 1, edge_y)].astype(np.float32)
            bys, bxs = np.where(bite)
            bgr[bys, bxs] = np.clip(donor[bxs] * 0.94, 0, 255)
        outside = (~inside) & (yy > scarf_bot + 8) & (v_img > 100) & (s_img < 40) & (alpha > 128)
        alpha[outside] = 0

    out = cv2.cvtColor(bgr, cv2.COLOR_BGR2BGRA)
    out[:, :, 3] = alpha
    cv2.imwrite(path, out)


def main():
    names = sorted(n for n in os.listdir(PACK) if n.endswith(".png"))
    for name in names:
        fix(os.path.join(PACK, name))
    print("fixed", len(names))


if __name__ == "__main__":
    main()
