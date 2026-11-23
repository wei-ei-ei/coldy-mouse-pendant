# Front-facing singing frames. The generated clip sways, and the
# flat cut at the bottom of the round belly is filled back in.
import os
import cv2
import numpy as np

VIDEO = r"C:\Users\asus\.cursor\projects\c-Users-asus-Desktop-coldy\assets\coldy-front.mp4"
PACK = os.path.join(os.environ["TEMP"], "coldy-front-pack")
CANVAS_W = 460
CANVAS_H = 420
CROWN_X = 210
CROWN_Y = 52
TARGET_H = 300


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
        return w / 2.0, 8.0
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
    xs = []
    ys = []
    bottom = min(h - 1, head_y + 5)
    for y in range(head_y, bottom + 1):
        cols = np.where(opaque[y])[0]
        if cols.size == 0:
            continue
        use = cols[cols < spoon_l] if np.any(cols < spoon_l) else cols
        xs.append(use)
        ys.append(np.full(use.shape, y))
    if not xs:
        return w / 2.0, float(head_y)
    return float(np.concatenate(xs).mean()), float(np.concatenate(ys).mean())


def finish_body(frame):
    b, g, r = frame[:, :, 0], frame[:, :, 1], frame[:, :, 2]
    mx = np.maximum(np.maximum(b, g), r)
    alpha = np.where(mx > 26, 255, 0).astype(np.uint8)
    h, w = alpha.shape
    ys, xs = np.where(alpha > 0)
    if xs.size < 100:
        return frame, alpha
    top, bot = int(ys.min()), int(ys.max())
    # The unfinished bottom is a pale flat band. Lift it off.
    cut = bot
    for y in range(bot, top + (bot - top) // 2, -1):
        cols = np.where(alpha[y] > 0)[0]
        if cols.size < 30:
            continue
        row = frame[y, cols]
        if row.std() < 18 and row.mean() > 145:
            cut = y
            alpha[y, cols] = 0
        else:
            break
    return frame, alpha
    # Round belly from the fur that is still there.
    band_y = max(top, cut - 70)
    cols = np.where(alpha[band_y] > 0)[0]
    if cols.size < 20:
        return frame, alpha
    left = int(np.percentile(cols, 6))
    right = int(np.percentile(cols, 94))
    cx = (left + right) / 2.0
    rad = (right - left) / 2.0
    depth = int(rad * 0.28)
    fur_row = frame[max(0, band_y - 6)]
    for y in range(band_y, min(h - 1, band_y + depth)):
        dy = y - (band_y - 6)
        half = rad * rad - (dy * rad / max(1, depth)) ** 2
        if half <= 0:
            continue
        half = half ** 0.5
        x0 = max(0, int(cx - half))
        x1 = min(w - 1, int(cx + half))
        shade = 0.78 + 0.22 * (1.0 - dy / max(1, depth))
        for x in range(x0, x1 + 1):
            if alpha[y, x] > 0:
                continue
            src = min(w - 1, max(0, x))
            color = fur_row[src].astype(np.float32) * shade
            frame[y, x] = np.clip(color, 0, 255)
            alpha[y, x] = 255
    # Fill the bite on the lower right if it falls inside the same circle.
    body_top = top + int((cut - top) * 0.58)
    yy, xx = np.mgrid[0:h, 0:w]
    inside = (xx - cx) ** 2 + (yy - (band_y - 6)) ** 2 <= rad ** 2
    hole = inside & (yy >= body_top) & (yy <= band_y + depth) & (alpha == 0)
    hys, hxs = np.where(hole)
    for i in range(hys.size):
        y = int(hys[i])
        x = int(hxs[i])
        src = min(w - 1, max(0, x))
        frame[y, x] = fur_row[src]
        alpha[y, x] = 255
    return frame, alpha


def main():
    if os.path.isdir(PACK):
        for name in os.listdir(PACK):
            os.remove(os.path.join(PACK, name))
    else:
        os.makedirs(PACK)
    cap = cv2.VideoCapture(VIDEO)
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    step = 6
    saved = 0
    for index in range(0, total, step):
        cap.set(cv2.CAP_PROP_POS_FRAMES, index)
        ok, frame = cap.read()
        if not ok:
            break
        frame, alpha = finish_body(frame)
        ys, xs = np.where(alpha > 128)
        if xs.size < 100:
            continue
        y0, y1 = int(ys.min()), int(ys.max())
        x0, x1 = int(xs.min()), int(xs.max())
        sprite = cv2.cvtColor(frame[y0:y1 + 1, x0:x1 + 1], cv2.COLOR_BGR2BGRA)
        sprite[:, :, 3] = alpha[y0:y1 + 1, x0:x1 + 1]
        scale = TARGET_H / float(sprite.shape[0])
        resized = cv2.resize(sprite, (max(1, int(round(sprite.shape[1] * scale))), TARGET_H), interpolation=cv2.INTER_AREA)
        resized[:, :, 3] = np.where(resized[:, :, 3] > 128, 255, 0).astype(np.uint8)
        cx, cy = crown_of(resized[:, :, 3])
        canvas = np.zeros((CANVAS_H, CANVAS_W, 4), np.uint8)
        left = int(round(CROWN_X - cx))
        top = int(round(CROWN_Y - cy))
        sh, sw = resized.shape[:2]
        x_dst0 = max(0, left)
        y_dst0 = max(0, top)
        x_src0 = max(0, -left)
        y_src0 = max(0, -top)
        x_dst1 = min(CANVAS_W, left + sw)
        y_dst1 = min(CANVAS_H, top + sh)
        x_src1 = x_src0 + (x_dst1 - x_dst0)
        y_src1 = y_src0 + (y_dst1 - y_dst0)
        if x_dst1 > x_dst0 and y_dst1 > y_dst0:
            canvas[y_dst0:y_dst1, x_dst0:x_dst1] = resized[y_src0:y_src1, x_src0:x_src1]
        cv2.imwrite(os.path.join(PACK, "f%02d.png" % saved), canvas)
        saved += 1
        print("frame", index, "crown", round(cx, 1), round(cy, 1))
    cap.release()
    print("packed", saved)


if __name__ == "__main__":
    main()
