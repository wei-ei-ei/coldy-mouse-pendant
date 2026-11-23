# Build a front-facing snowman. The head, glasses and wires come from
# the cutout. The belly is completed as a circle of the same fur.
import os
import cv2
import numpy as np

RAW = os.path.join(os.environ["TEMP"], "coldy-front-raw.jpg")
CUT = os.path.join(os.environ["TEMP"], "coldy-front.png")
OUT = os.path.join(os.environ["TEMP"], "coldy-front-full.png")


def main():
    bgr = cv2.imread(RAW)
    cut = cv2.imread(CUT, cv2.IMREAD_UNCHANGED)
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    s = hsv[:, :, 1].astype(np.int16)
    v = hsv[:, :, 2].astype(np.int16)
    h, w = v.shape
    cx, cy, radius = 430.0, 1120.0, 310.0
    yy, xx = np.mgrid[0:h, 0:w]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    circle = dist <= radius

    fur = (v > 155) & (s < 40) & circle & (yy > cy - 40)
    alpha = np.zeros((h, w), np.uint8)
    filled = np.zeros_like(bgr)
    # Rays through the visible fur, so the new arc keeps the same color.
    ang = np.arctan2(yy - cy, xx - cx)
    grow = circle & ~fur
    alpha[fur] = 255
    filled[fur] = bgr[fur]
    gys, gxs = np.where(grow)
    fur_ys, fur_xs = np.where(fur)
    if fur_ys.size == 0:
        raise SystemExit("no fur")
    for i in range(gys.size):
        y = int(gys[i])
        x = int(gxs[i])
        dx = x - cx
        dy = y - cy
        length = (dx * dx + dy * dy) ** 0.5 + 1e-6
        sx = float(x)
        sy = float(y)
        found = None
        for _ in range(40):
            sx -= dx / length * 6.0
            sy -= dy / length * 6.0
            ix = int(round(sx))
            iy = int(round(sy))
            if ix < 0 or iy < 0 or ix >= w or iy >= h:
                break
            if fur[iy, ix]:
                found = (ix, iy)
                break
        if found is None:
            ix = int(fur_xs[i % fur_xs.size])
            iy = int(fur_ys[i % fur_ys.size])
            found = (ix, iy)
        shade = 0.72 + 0.28 * (1.0 - min(1.0, length / radius))
        color = bgr[found[1], found[0]].astype(np.float32) * shade
        filled[y, x] = np.clip(color, 0, 255)
        alpha[y, x] = 255

    # Scarf: the striped knit around the neck, including the hanging tail.
    scarf = (s > 28) & (v > 40) & (yy > 680) & (yy < 980) & (np.abs(xx - cx) < radius + 20)
    tail = (s > 28) & (v > 35) & (xx < cx - 40) & (yy >= 900) & (yy < 1450) & (xx > cx - radius - 10)
    knit = scarf | tail
    filled[knit] = bgr[knit]
    alpha[knit] = 255

    # Head, glasses, mouth, microphone and wires stay from the cutout.
    head = cut[:, :, 3] > 140
    # Do not let the cutout's empty lower body erase the circle.
    head = head & (yy < 1020)
    filled[head] = cut[:, :, :3][head]
    alpha[head] = 255

    rgba = cv2.cvtColor(filled, cv2.COLOR_BGR2BGRA)
    rgba[:, :, 3] = alpha
    cv2.imwrite(OUT, rgba)
    ys, xs = np.where(alpha > 128)
    x0 = max(0, int(xs.min()) - 24)
    y0 = max(0, int(ys.min()) - 24)
    x1 = min(w - 1, int(xs.max()) + 24)
    y1 = min(h - 1, int(ys.max()) + 24)
    crop = rgba[y0:y1 + 1, x0:x1 + 1]
    studio = np.full((crop.shape[0] + 80, crop.shape[1] + 80, 3), (14, 12, 18), np.uint8)
    rgb = crop[:, :, :3]
    a = (crop[:, :, 3:4].astype(np.float32) / 255.0)
    studio[40:40 + rgb.shape[0], 40:40 + rgb.shape[1]] = (
        rgb * a + studio[40:40 + rgb.shape[0], 40:40 + rgb.shape[1]] * (1.0 - a)
    ).astype(np.uint8)
    still = os.path.join(os.environ["TEMP"], "coldy-front-still.png")
    cv2.imwrite(still, studio)
    print("still", studio.shape, "fur", int(fur.sum()), "grow", int(gys.size))


if __name__ == "__main__":
    main()
