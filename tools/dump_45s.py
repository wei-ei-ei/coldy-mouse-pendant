"""Dump full frames 44.6-45.6 from the official clip: check the framing (is
the whole figure in frame?) and find the cleanest frontal upright moment."""
import os
import cv2
import numpy as np

ROOT = r"C:\Users\asus\Desktop\coldy"
OUT = os.path.join(os.environ.get("TEMP", ROOT), "coldy_work")
os.makedirs(OUT, exist_ok=True)

cap = cv2.VideoCapture(os.path.join(ROOT, "assets", "candidates",
                                    "coldy-cogezwang-1440x2560.mp4"))
tiles = []
for t in np.arange(44.6, 45.62, 0.1):
    cap.set(cv2.CAP_PROP_POS_MSEC, float(t) * 1000.0)
    ok, f = cap.read()
    if not ok:
        continue
    h, w = f.shape[:2]
    c = f[int(h * 0.10):int(h * 0.98), int(w * 0.12):int(w * 0.88)]
    c = cv2.resize(c, (190, round(c.shape[0] * 190 / c.shape[1])))
    cv2.putText(c, "%.1f" % t, (4, 18), cv2.FONT_HERSHEY_SIMPLEX, 0.5,
                (0, 255, 255), 2)
    tiles.append(c)
cap.release()
cols = 6
rows = (len(tiles) + cols - 1) // cols
th = max(x.shape[0] for x in tiles)
grid = np.zeros((rows * th, cols * 190, 3), np.uint8)
for i, x in enumerate(tiles):
    y, cx = (i // cols) * th, (i % cols) * 190
    grid[y:y + x.shape[0], cx:cx + 190] = x
cv2.imwrite(os.path.join(OUT, "frames_45s.png"), grid)
print("saved frames_45s", len(tiles))
