# Cut the hand-drawn COLDY wordmark out of the Xiaohongshu event poster.
# The source is a phone screenshot; the logo sits on a pale blue ice texture.
# rembg gives the shape, then colour rules remove any ice that survived and
# keep the white snowman / highlights enclosed by the black ink outline.
import os
import cv2
import numpy as np
from PIL import Image
from rembg import remove, new_session

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.environ["USERPROFILE"], ".workbuddy-ai", "clipboard-images",
                   "clipboard-2026-11-12T17-38-00-934Z-9b83e219.jpg")
OUT = os.path.join(ROOT, "assets", "coldy-logo.png")
PREVIEW = os.path.join(os.environ.get("TEMP", ROOT), "coldy-logo-preview.png")
CROP = (165, 330, 770, 725)  # x0, y0, x1, y1 in the 884x1920 screenshot


def main():
    img = cv2.imread(SRC)
    if img is None:
        raise SystemExit("cannot read " + SRC)
    crop = img[CROP[1]:CROP[3], CROP[0]:CROP[2]].copy()
    cut = remove(crop, session=new_session("u2net"))
    alpha = cut[:, :, 3]
    subject = (alpha > 90).astype(np.uint8)

    # Ice leftovers: pale blue, low/medium saturation, connected to the border.
    hsv = cv2.cvtColor(crop, cv2.COLOR_BGR2HSV)
    pale_ice = ((hsv[:, :, 1] < 105) & (hsv[:, :, 2] > 145)).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(pale_ice, 8)
    h, w = subject.shape
    for c in range(1, n):
        x, y, ww, hh, area = stats[c]
        if x == 0 or y == 0 or x + ww >= w or y + hh >= h:
            subject[lab == c] = 0

    # Keep only the connected figure; fill the white areas inside the ink lines.
    n, lab, stats, _ = cv2.connectedComponentsWithStats(subject, 8)
    if n > 1:
        main_id = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        subject = (lab == main_id).astype(np.uint8)
    inv = (1 - subject).astype(np.uint8)
    n2, lab2, stats2, _ = cv2.connectedComponentsWithStats(inv, 8)
    for c in range(1, n2):
        x, y, ww, hh, area = stats2[c]
        if area < 40000 and x > 0 and y > 0 and x + ww < w and y + hh < h:
            subject[lab2 == c] = 1

    alpha = (subject * 255).astype(np.uint8)
    alpha = cv2.GaussianBlur(alpha, (3, 3), 0)
    rgba = np.dstack([cv2.cvtColor(crop, cv2.COLOR_BGR2BGRA)[:, :, :3], alpha])
    ys, xs = np.where(alpha > 40)
    rgba = rgba[max(0, ys.min() - 8):min(h, ys.max() + 9),
                max(0, xs.min() - 8):min(w, xs.max() + 9)]
    cv2.imwrite(OUT, rgba)
    print("logo", OUT, rgba.shape, "opaque", int((rgba[:, :, 3] > 128).sum()))

    comp = rgba[:, :, :3].astype(np.float32)
    a = rgba[:, :, 3:4].astype(np.float32) / 255.0
    bg = np.array([24, 22, 30], np.float32)
    cv2.imwrite(PREVIEW, (comp * a + bg * (1 - a)).astype(np.uint8))
    print("preview", PREVIEW)


if __name__ == "__main__":
    main()
