# Turn the cleaned front still into a gently swaying form-4 sequence.
import os
import cv2
import numpy as np

SRC = os.path.join(os.environ["TEMP"], "coldy-rebuild", "ref_master.png")
PACK = os.path.join(os.environ["TEMP"], "coldy-front-pack")
FRAMES = 16
SWAY = 4.5


def crown_of(alpha):
    h, w = alpha.shape
    opaque = alpha > 128
    for y in range(h):
        xs = np.where(opaque[y])[0]
        if xs.size < 40 or xs[-1] - xs[0] < 90:
            continue
        band = np.where(opaque[y:y + 6].any(axis=0))[0]
        # The spoon sits on the upper right. Keep the wide head.
        gaps = np.where(np.diff(band) > 12)[0]
        if gaps.size:
            band = band[:gaps[0] + 1]
        return float(np.median(band)), float(y)
    return w / 2.0, 8.0


def main():
    im = cv2.imread(SRC, cv2.IMREAD_UNCHANGED)
    alpha = im[:, :, 3]
    ys, xs = np.where(alpha > 128)
    y0, y1 = int(ys.min()), int(ys.max())
    x0, x1 = int(xs.min()), int(xs.max())
    crop = im[y0:y1 + 1, x0:x1 + 1]
    target_h = 540
    scale = target_h / float(crop.shape[0])
    resized = cv2.resize(
        crop,
        (max(1, int(round(crop.shape[1] * scale))), target_h),
        interpolation=cv2.INTER_AREA,
    )
    resized[:, :, 3] = np.where(resized[:, :, 3] > 140, 255, 0).astype(np.uint8)
    cx, cy = crown_of(resized[:, :, 3])
    h, w = resized.shape[:2]
    pad_x, pad_y = 80, 28
    canvas_w = w + pad_x * 2
    canvas_h = h + pad_y * 2
    crown = (pad_x + cx, pad_y + cy)
    if os.path.isdir(PACK):
        for name in os.listdir(PACK):
            os.remove(os.path.join(PACK, name))
    else:
        os.makedirs(PACK)
    print("canvas", canvas_w, canvas_h, "crown", round(crown[0], 1), round(crown[1], 1))
    for i in range(FRAMES):
        angle = SWAY * np.sin(2 * np.pi * i / FRAMES)
        matrix = cv2.getRotationMatrix2D((cx, cy), angle, 1.0)
        matrix[0, 2] += crown[0] - cx
        matrix[1, 2] += crown[1] - cy
        canvas = cv2.warpAffine(
            resized,
            matrix,
            (canvas_w, canvas_h),
            flags=cv2.INTER_LINEAR,
            borderMode=cv2.BORDER_CONSTANT,
            borderValue=(0, 0, 0, 0),
        )
        canvas[:, :, 3] = np.where(canvas[:, :, 3] > 128, 255, 0).astype(np.uint8)
        cv2.imwrite(os.path.join(PACK, "f%02d.png" % i), canvas)
    print("crown", round(cx, 1), round(cy, 1), "frames", FRAMES)


if __name__ == "__main__":
    main()
