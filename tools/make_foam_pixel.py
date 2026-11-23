# Pixel Coldy with the salon foam curls from the Xiaohongshu stills.
from PIL import Image, ImageDraw

W, H = 64, 84
im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
px = im.load()

OUT = (55, 68, 82, 255)
BODY = (244, 247, 251, 255)
BODY_S = (214, 222, 232, 255)
BODY_H = (255, 255, 255, 255)
HAIR = (236, 242, 248, 255)
HAIR_S = (198, 210, 222, 255)
HAIR_H = (255, 255, 255, 255)
SOCKET = (42, 49, 62, 255)
WINK = (150, 214, 236, 255)
GLOW = (90, 214, 255, 255)
GLOW_C = (210, 248, 255, 255)
HEART = (255, 255, 255, 255)
MOUTH = (46, 54, 66, 255)
SPOON = (186, 196, 206, 255)
SPOON_D = (120, 132, 146, 255)
SPOON_H = (244, 247, 250, 255)
CREAM = (232, 214, 184, 255)
GREEN = (63, 122, 78, 255)
ORANGE = (214, 112, 52, 255)
BLUE = (46, 104, 168, 255)
BEIGE = (214, 196, 168, 255)
CABLES = [
    (214, 58, 58, 255),
    (232, 186, 48, 255),
    (58, 168, 84, 255),
    (48, 112, 210, 255),
    (36, 42, 52, 255),
]


def put(x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[x, y] = c


def disc(cx, cy, r, c, outline=None):
    r2 = r * r
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = (x - cx) ** 2 + (y - cy) ** 2
            if d <= r2:
                put(x, y, c)
            elif outline and d <= (r + 0.85) ** 2:
                put(x, y, outline)


def blob(cx, cy, rx, ry, c, shade=None):
    for y in range(int(cy - ry - 1), int(cy + ry + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            nx = (x - cx) / float(rx)
            ny = (y - cy) / float(ry)
            if nx * nx + ny * ny <= 1:
                col = c
                if shade and ny > 0.25:
                    col = shade
                put(x, y, col)


# Body
blob(31, 62, 16, 15, BODY, BODY_S)
for y in range(52, 74):
    for x in range(18, 46):
        nx = (x - 31) / 16.0
        ny = (y - 62) / 15.0
        if nx * nx + ny * ny <= 1 and x > 36 and y > 64:
            put(x, y, BODY_S)
        if nx * nx + ny * ny <= 0.55 and x < 28 and y < 60:
            put(x, y, BODY_H)

# Foam curls. Drawn back to front so the face stays clear.
curls = [
    (16, 30, 7, 6), (22, 22, 7, 6), (31, 18, 8, 6),
    (40, 22, 7, 6), (46, 30, 7, 6), (12, 36, 6, 5),
    (50, 36, 6, 5), (20, 34, 6, 5), (42, 34, 6, 5),
    (31, 28, 7, 5), (26, 24, 5, 4), (36, 24, 5, 4),
]
for cx, cy, rx, ry in curls:
    blob(cx, cy, rx, ry, HAIR, HAIR_S)
    put(cx - 2, cy - 2, HAIR_H)
    put(cx - 1, cy - 2, HAIR_H)

# Face opening
blob(31, 38, 11, 9, BODY, BODY_S)

# Eyes
disc(24, 37, 3.2, SOCKET)
for x in range(22, 27):
    put(x, 37, WINK)
put(23, 36, (90, 110, 130, 255))

disc(38, 37, 3.6, GLOW)
disc(38, 37, 2.1, GLOW_C)
# Heart highlight
put(38, 36, HEART)
put(37, 36, HEART)
put(39, 36, HEART)
put(38, 37, HEART)

# Smile
for x in range(29, 34):
    put(x, 42, MOUTH)
put(28, 41, MOUTH)
put(34, 41, MOUTH)

# Spoon: bowl above the curls, handle dropping into the hair.
disc(48, 7, 3.4, SPOON, SPOON_D)
put(47, 6, SPOON_H)
put(49, 6, SPOON_H)
put(48, 8, SPOON_D)
for y in range(10, 18):
    put(45, y, SPOON_D)
    put(46, y, SPOON)

# Scarf neck, green / cream / orange like the salon still.
for y in range(45, 53):
    stripe = [GREEN, CREAM, ORANGE, CREAM, GREEN, CREAM, ORANGE, CREAM][y - 45]
    for x in range(17, 48):
        if px[x, y][3]:
            put(x, y, stripe)

# Hanging tail on the viewer's left
for y in range(50, 76):
    stripe = BLUE if ((y // 3) % 2 == 0) else BEIGE
    width = 5 if y < 70 else 4
    for x in range(14, 14 + width):
        put(x, y, stripe)
    put(13, y, OUT)

# Cable bundle fanning out of the right side.
tips = [(48, 54), (51, 56), (54, 58), (52, 61), (48, 63)]
for col, (x1, y1) in zip(CABLES, tips):
    x0, y0 = 43, 52
    for t in range(8):
        x = x0 + (x1 - x0) * t // 7
        y = y0 + (y1 - y0) * t // 7
        put(x, y, col)
        put(x, y + 1, col)
        put(x + 1, y, col)
    put(x1 + 1, y1, (70, 76, 86, 255))
    put(x1 + 1, y1 + 1, (70, 76, 86, 255))

# Crisp outline on the silhouette
src = im.copy().load()
for y in range(H):
    for x in range(W):
        if src[x, y][3] == 0:
            continue
        edge = False
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            xx, yy = x + dx, y + dy
            if not (0 <= xx < W and 0 <= yy < H) or src[xx, yy][3] == 0:
                edge = True
        if edge and src[x, y][:3] not in (GLOW[:3], GLOW_C[:3], HEART[:3], WINK[:3]):
            # Keep the glow soft. Outline only the white foam and body.
            if src[x, y][0] > 180 and src[x, y][2] > 160:
                put(x, y, OUT)

preview = im.resize((W * 6, H * 6), Image.NEAREST)
im.save(r"C:\Users\asus\Desktop\coldy\assets\coldy-form-foam.png")
preview.save(r"C:\Users\asus\Desktop\coldy\assets\coldy-form-foam-preview.png")
print(im.size)
