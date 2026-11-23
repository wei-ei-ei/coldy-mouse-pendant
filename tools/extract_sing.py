# Cut the singing snowman out of the screen recording and
# pin the crown so the rope still meets the head.
import os
import cv2
import numpy as np
from PIL import Image
from rembg import remove, new_session

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VIDEO = os.path.join(ROOT, "屏幕录制 2026-11-12 032453.mp4")
PACK = os.path.join(os.environ["TEMP"], "coldy-sing-pack")
CANVAS_W = 460
CANVAS_H = 420
CROWN_X = 210
CROWN_Y = 52
TARGET_H = 320
MAGENTA = (255, 0, 255)


def crown_of(alpha):
    h, w = alpha.shape
    opaque = alpha > 128
    top = -1
    spoon_l = 0
    spoon_r = -1
    for y in range(h):
        xs = np.where(opaque[y])[0]
        if xs.size == 0:
            continue
        top = y
        spoon_l = int(xs[0])
        spoon_r = int(xs[-1])
        break
    if top < 0:
        return CANVAS_W // 2, 8
    head_y = top
    for y in range(top, h):
        row = opaque[y]
        x = 0
        found = False
        while x < w:
            while x < w and not row[x]:
                x += 1
            if x >= w:
                break
            left = x
            while x < w and row[x]:
                x += 1
            right = x - 1
            if right < spoon_l - 2:
                head_y = y
                found = True
                break
            elif left <= spoon_r + 2:
                if left < spoon_l:
                    spoon_l = left
                if right > spoon_r:
                    spoon_r = right
        if found:
            break
    ys = []
    xs = []
    bottom = min(h - 1, head_y + 5)
    for y in range(head_y, bottom + 1):
        cols = np.where(opaque[y])[0]
        # the head run is the one left of the spoon
        if cols.size == 0:
            continue
        use = cols[cols < spoon_l] if np.any(cols < spoon_l) else cols
        ys.append(np.full(use.shape, y))
        xs.append(use)
    if not xs:
        return w // 2, head_y
    xs = np.concatenate(xs)
    ys = np.concatenate(ys)
    return float(xs.mean()), float(ys.mean())


def main():
    session = new_session("u2net")
    cap = cv2.VideoCapture(VIDEO)
    if os.path.isdir(PACK):
        for name in os.listdir(PACK):
            os.remove(os.path.join(PACK, name))
    else:
        os.makedirs(PACK)
    frames = []
    # Full-body singing shot only. Earlier and later frames are face close-ups.
    for index in range(70, 89):
        cap.set(cv2.CAP_PROP_POS_FRAMES, index)
        ok, frame = cap.read()
        if not ok:
            break
        crop = frame[50:1450, 10:920]
        cut = remove(crop, session=session)
        alpha = cut[:, :, 3]
        ys, xs = np.where(alpha > 128)
        if xs.size < 500:
            print("skip", index)
            continue
        y0, y1 = int(ys.min()), int(ys.max())
        x0, x1 = int(xs.min()), int(xs.max())
        pad = 8
        y0 = max(0, y0 - pad)
        x0 = max(0, x0 - pad)
        y1 = min(cut.shape[0] - 1, y1 + pad)
        x1 = min(cut.shape[1] - 1, x1 + pad)
        sprite = cut[y0:y1 + 1, x0:x1 + 1]
        scale = TARGET_H / float(sprite.shape[0])
        resized = cv2.resize(sprite, (max(1, int(round(sprite.shape[1] * scale))), TARGET_H), interpolation=cv2.INTER_AREA)
        hard = resized.copy()
        hard[:, :, 3] = np.where(resized[:, :, 3] > 128, 255, 0).astype(np.uint8)
        cx, cy = crown_of(hard[:, :, 3])
        canvas = np.zeros((CANVAS_H, CANVAS_W, 4), np.uint8)
        left = int(round(CROWN_X - cx))
        top = int(round(CROWN_Y - cy))
        sh, sw = hard.shape[:2]
        x_dst0 = max(0, left)
        y_dst0 = max(0, top)
        x_src0 = max(0, -left)
        y_src0 = max(0, -top)
        x_dst1 = min(CANVAS_W, left + sw)
        y_dst1 = min(CANVAS_H, top + sh)
        x_src1 = x_src0 + (x_dst1 - x_dst0)
        y_src1 = y_src0 + (y_dst1 - y_dst0)
        if x_dst1 > x_dst0 and y_dst1 > y_dst0:
            canvas[y_dst0:y_dst1, x_dst0:x_dst1] = hard[y_src0:y_src1, x_src0:x_src1]
        cv2.imwrite(os.path.join(PACK, "f%02d.png" % len(frames)), canvas)
        frames.append(index)
        print("frame", index, "crown", round(cx, 1), round(cy, 1), "placed", left, top)
    cap.release()
    print("packed", len(frames), PACK)


if __name__ == "__main__":
    main()
