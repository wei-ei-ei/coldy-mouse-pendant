# Remove the white sticker fringe around the 2D COLDY mascot.
#
# The mascot comes from a white-background frame. A plain "white = transparent"
# cut leaves the anti-aliased pale ring just outside the black ink outline,
# which shows up as a white halo on a dark desktop. Here we peel that ring off
# iteratively: only pale pixels that sit within a few pixels of transparency
# are removed, so the white fill INSIDE the outline stays intact.
import os
import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CAND = os.path.join(ROOT, "assets", "candidates")
SRC = os.path.join(CAND, "coldy-2d-fullbody.png")
BAK = os.path.join(CAND, "coldy-2d-fullbody-before.png")


def main():
    im = cv2.imread(SRC, cv2.IMREAD_UNCHANGED)
    if im is None or im.shape[2] < 4:
        raise SystemExit("expected an RGBA cutout: " + SRC)
    if not os.path.exists(BAK):
        cv2.imwrite(BAK, im)
    bgr = im[:, :, :3]
    alpha = im[:, :, 3].copy()
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    pale = (hsv[:, :, 2] > 155) & (hsv[:, :, 1] < 72)
    kernel = np.ones((5, 5), np.uint8)
    for step in range(4):
        solid = alpha > 128
        outside = (~solid).astype(np.uint8)
        near_transparent = cv2.dilate(outside, kernel) > 0
        kill = solid & near_transparent & pale
        print("pass", step, "removed", int(kill.sum()), "pale rim pixels")
        if not kill.any():
            break
        alpha[kill] = 0

    out = np.dstack([bgr, alpha])
    cv2.imwrite(SRC, out)
    print("saved", SRC, "alpha", int((alpha > 128).sum()), "backup", BAK)


if __name__ == "__main__":
    main()
