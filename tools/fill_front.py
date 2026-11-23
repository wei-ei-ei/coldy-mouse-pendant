# Front view: keep the rembg head, glasses and wires, and put the
# round belly back from the recording where the cutout dropped it.
import os
import cv2
import numpy as np

RAW = os.path.join(os.environ["TEMP"], "coldy-front-raw.jpg")
CUT = os.path.join(os.environ["TEMP"], "coldy-front.png")
OUT = os.path.join(os.environ["TEMP"], "coldy-front-full.png")


def main():
    bgr = cv2.imread(RAW)
    cut = cv2.imread(CUT, cv2.IMREAD_UNCHANGED)
    if cut.shape[:2] != bgr.shape[:2]:
        cut = cv2.resize(cut, (bgr.shape[1], bgr.shape[0]))
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s = hsv[:, :, 1]
    v = hsv[:, :, 2]
    alpha = cut[:, :, 3].copy()

    fur = ((v > 150) & (s < 50)).astype(np.uint8)
    fur[:700, :] = 0
    n, labels, stats, _ = cv2.connectedComponentsWithStats(fur, 8)
    belly = None
    best_area = 0
    for c in range(1, n):
        area = stats[c, cv2.CC_STAT_AREA]
        y = stats[c, cv2.CC_STAT_TOP]
        if y > 800 and area > best_area:
            best_area = area
            belly = c
    if belly is not None:
        alpha[labels == belly] = 255
    # Scarf and wires sit between the head cutout and the belly.
    color = ((s > 42) & (v > 40)).astype(np.uint8) * 255
    near = cv2.dilate((alpha > 128).astype(np.uint8) * 255, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15)))
    alpha = np.maximum(alpha, cv2.bitwise_and(color, near))

    # Drop only the small stage light above the head. Keep the belly even if
    # the scarf has not joined it to the head yet.
    keep = (alpha > 128).astype(np.uint8)
    n2, labels2, stats2, _ = cv2.connectedComponentsWithStats(keep, 8)
    for c in range(1, n2):
        if stats2[c, cv2.CC_STAT_AREA] < 4000 and stats2[c, cv2.CC_STAT_TOP] < 280:
            alpha[labels2 == c] = 0

    ys, xs = np.where(alpha > 128)
    y1 = int(ys.max())
    # Belly outline below the scarf, so the missing bottom can be a circle.
    edges = []
    for y in range(980, y1 + 1, 4):
        cols = np.where((alpha[y] > 128) & (v[y] > 140) & (s[y] < 70))[0]
        if cols.size < 20:
            continue
        edges.append((float(cols[0]), float(y)))
        edges.append((float(cols[-1]), float(y)))
    print("edges", len(edges), "belly", best_area, "alpha bottom", y1)
    cx, cy, radius = fit_circle(np.array(edges, np.float64))
    print("circle", round(cx, 1), round(cy, 1), round(radius, 1), "bottom", y1)

    need = int(np.ceil(cy + radius)) + 8
    if need > bgr.shape[0]:
        extra = need - bgr.shape[0]
        bgr = np.vstack([bgr, np.zeros((extra, bgr.shape[1], 3), np.uint8)])
        alpha = np.vstack([alpha, np.zeros((extra, alpha.shape[1]), np.uint8)])
        s = np.vstack([s, np.zeros((extra, s.shape[1]), np.uint8)])
        v = np.vstack([v, np.zeros((extra, v.shape[1]), np.uint8)])
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w]
    inside = (xx - cx) ** 2 + (yy - cy) ** 2 <= (radius - 2) ** 2
    grow = inside & (yy >= y1 - 2) & (alpha == 0)
    fur_vis = (alpha > 128) & (s < 50) & (v > 150)
    filled = bgr.copy()
    grow_ys, grow_xs = np.where(grow)
    for i in range(grow_ys.size):
        y = int(grow_ys[i])
        x = int(grow_xs[i])
        dx = x - cx
        dy = y - cy
        length = (dx * dx + dy * dy) ** 0.5
        if length < 1:
            continue
        sx = float(x)
        sy = float(y)
        found = None
        for _ in range(60):
            sx -= dx / length * 4.0
            sy -= dy / length * 4.0
            ix = int(round(sx))
            iy = int(round(sy))
            if ix < 1 or iy < 1 or ix >= w - 1 or iy >= h - 1:
                break
            if fur_vis[iy, ix]:
                found = (ix, iy)
                break
        if found is None:
            continue
        filled[y, x] = filled[found[1], found[0]]
        alpha[y, x] = 255

    rgba = cv2.cvtColor(filled, cv2.COLOR_BGR2BGRA)
    rgba[:, :, 3] = alpha
    cv2.imwrite(OUT, rgba)
    ys, xs = np.where(alpha > 128)
    x0 = max(0, int(xs.min()) - 30)
    y0 = max(0, int(ys.min()) - 30)
    x1 = min(w - 1, int(xs.max()) + 30)
    y1b = min(h - 1, int(ys.max()) + 30)
    crop = rgba[y0:y1b + 1, x0:x1 + 1]
    studio = np.full((crop.shape[0] + 100, crop.shape[1] + 100, 3), (16, 14, 20), np.uint8)
    rgb = crop[:, :, :3]
    a = crop[:, :, 3:4].astype(np.float32) / 255.0
    studio[50:50 + rgb.shape[0], 50:50 + rgb.shape[1]] = (
        rgb * a + studio[50:50 + rgb.shape[0], 50:50 + rgb.shape[1]] * (1.0 - a)
    ).astype(np.uint8)
    still = os.path.join(os.environ["TEMP"], "coldy-front-still.png")
    cv2.imwrite(still, studio)
    print("still", studio.shape, "grow", int(grow_ys.size))


def fit_circle(pts):
    x = pts[:, 0]
    y = pts[:, 1]
    a = np.column_stack([2 * x, 2 * y, np.ones(len(pts))])
    b = x * x + y * y
    cx, cy, c = np.linalg.lstsq(a, b, rcond=None)[0]
    radius = np.sqrt(max(1.0, c + cx * cx + cy * cy))
    return float(cx), float(cy), float(radius)


if __name__ == "__main__":
    main()
