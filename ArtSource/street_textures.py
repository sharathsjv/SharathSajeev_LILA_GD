# Textures for Shackleton Ave (destitute, grimy, graffiti, rusted elevated subway). Runs inside Blender (numpy only).
import os
import numpy as np
import apt_textures as A
from apt_textures import N, fbm, vnoise, col, grime, speckle

# ---------------------------------------------------------------- tiny 5x7 pixel font for signs and tags

FONT = {
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "B": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "C": ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "F": ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
    "G": ["01111", "10000", "10000", "10011", "10001", "10001", "01111"],
    "H": ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    "I": ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    "J": ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
    "K": ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    "L": ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "N": ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
    "O": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "P": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "Q": ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
    "R": ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    "T": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    "U": ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
    "V": ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
    "W": ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
    "X": ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
    "Y": ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
    "Z": ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
    "0": ["01110", "10011", "10101", "10101", "11001", "10001", "01110"],
    "1": ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
    "2": ["01110", "10001", "00001", "00110", "01000", "10000", "11111"],
    "4": ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    "7": ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
    " ": ["00000"] * 7, "-": ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    "'": ["00100", "00100", "01000", "00000", "00000", "00000", "00000"],
}


def text_mask(text, w, h, margin=0.08, slant=0.0):
    """Boolean mask (h, w) with the text drawn as big pixels, fitted inside the image."""
    cols = len(text) * 6 - 1
    s = max(1, int(min(w * (1 - 2 * margin) / cols, h * (1 - 2 * margin) / 7)))
    tw, th = cols * s, 7 * s
    ox, oy = (w - tw) // 2, (h - th) // 2
    m = np.zeros((h, w), bool)
    for i, ch in enumerate(text.upper()):
        g = FONT.get(ch, FONT[" "])
        for r in range(7):
            for c in range(5):
                if g[r][c] == "1":
                    x0 = ox + (i * 6 + c) * s + int((3.5 - r) * s * slant)
                    y0 = oy + r * s
                    m[max(0, y0):y0 + s, max(0, x0):x0 + s] = True
    return m


def dilate(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                out |= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


# ---------------------------------------------------------------- surfaces

def brick_dirty(rng, base=(0.40, 0.18, 0.12)):
    img = A.brick(rng) * (np.array(base) / np.array([0.46, 0.21, 0.14]))[None, None, :]
    Y, X = np.mgrid[0:N, 0:N]
    soot = vnoise(N, 24, 2, rng)                           # vertical soot/rain streaks
    img *= (0.75 + 0.35 * soot)[..., None]
    return grime(img, rng, 0.5, 2)


def stucco(rng):
    img = np.ones((N, N, 3)) * col([0.50, 0.49, 0.45])
    img *= (0.85 + 0.25 * fbm(rng, 48, 3))[..., None]
    img *= (0.75 + 0.35 * vnoise(N, 20, 2, rng))[..., None]
    cracks = np.abs(fbm(rng, 6, 4) - 0.5) < 0.012
    img[cracks] *= 0.5
    return grime(img, rng, 0.45, 2)


def asphalt(rng):
    img = np.ones((N, N, 3)) * col([0.17, 0.17, 0.18])
    img *= (0.8 + 0.4 * fbm(rng, 96, 2))[..., None]
    img = speckle(img, rng, 0.08, 1.5)
    patch = fbm(rng, 3, 3) > 0.62
    img[patch] *= 0.75
    cracks = np.abs(fbm(rng, 5, 5) - 0.5) < 0.01
    img[cracks] *= 0.35
    oil = np.clip((fbm(rng, 4, 3) - 0.6) * 4, 0, 1)
    img *= (1 - 0.4 * oil)[..., None]
    return img


def concrete(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.46, 0.45, 0.42])
    img *= (0.85 + 0.25 * fbm(rng, 32, 3))[..., None]
    joints = ((X % 128) < 2) | ((Y % 128) < 2)
    img[joints] *= 0.45
    cracks = np.abs(fbm(rng, 4, 4) - 0.5) < 0.004
    img[cracks] *= 0.6
    gum = rng.random((N, N)) < 0.0015
    img[dilate(gum, 1)] *= 0.55
    return grime(img, rng, 0.5, 3)


def curb(rng):
    img = np.ones((N, N, 3)) * col([0.52, 0.51, 0.48])
    img *= (0.8 + 0.3 * fbm(rng, 24, 3))[..., None]
    return grime(img, rng, 0.5, 4)


def dirt(rng):
    img = np.ones((N, N, 3)) * col([0.25, 0.22, 0.17])
    img *= (0.7 + 0.5 * fbm(rng, 24, 4))[..., None]
    return speckle(img, rng, 0.05, 1.4)


def steel(rng):
    """Riveted, rust-streaked painted steel for the elevated subway."""
    Y, X = np.mgrid[0:N, 0:N]
    img = A.metal(rng, [0.24, 0.29, 0.27], rust=0.58)
    rivets = (((X % 32) - 16) ** 2 + ((Y % 64) - 8) ** 2) < 5
    img[rivets] *= 1.4
    streak = vnoise(N, 32, 3, rng)
    img *= (0.75 + 0.35 * streak)[..., None]
    return img


def window_dark(rng, lit=False, curtain=None):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * (col([0.55, 0.42, 0.22]) if lit else col([0.12, 0.15, 0.19]))
    img *= (0.75 + 0.4 * (1 - Y / N))[..., None]
    refl = (np.abs((X + Y * 0.6) % 180 - 60) < 14)
    img[refl] = img[refl] * 1.4 + 0.04
    if curtain is not None:
        c = (X < N * 0.3) | (X > N * 0.7)
        img[c] = (col(curtain) * (0.7 + 0.3 * np.sin(X * 0.4))[..., None])[c]
    img[(np.abs(X - N // 2) < 4) | (np.abs(Y - N // 2) < 4)] = [0.18, 0.16, 0.13]   # window bars
    return grime(img, rng, 0.3, 3)


def boarded(rng):
    Y, X = np.mgrid[0:N, 0:N]
    planks = (Y // 42) % 3
    img = col([0.42, 0.33, 0.22]) * (np.array([1.0, 0.85, 0.92])[planks] * (0.75 + 0.4 * vnoise(N, 4, 64, rng)))[..., None]
    img[(Y % 42) < 2] *= 0.4
    img = graffiti_on(img, rng, "NO", (0.85, 0.85, 0.8))
    return grime(img, rng, 0.4, 3)


def shopfront(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.10, 0.12, 0.14])
    shelves = ((Y % 64) < 6) & (Y > 60)
    img[shelves] = [0.30, 0.26, 0.20]
    goods = (rng.random((N // 8, N // 8)) > 0.5).repeat(8, 0).repeat(8, 1) & ((Y % 64) > 30) & (Y > 60)
    img[goods] = img[goods] * 0.5 + rng.uniform(0.1, 0.35, 3)
    refl = (np.abs((X + Y * 0.5) % 220 - 80) < 18)
    img[refl] = img[refl] * 1.3 + 0.06
    return grime(img, rng, 0.35, 3)


def shutter(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = col([0.45, 0.45, 0.44]) * (0.8 + 0.2 * np.sin(Y * np.pi / 4))[..., None] * np.ones((N, N, 3))
    img = A.metal(rng, [0.45, 0.45, 0.44], rust=0.66) * (0.85 + 0.15 * np.sin(Y * np.pi / 4))[..., None]
    img = graffiti_on(img, rng, "ZOD", (0.25, 0.55, 0.85))
    return img


def graffiti_on(img, rng, word, color, slant=0.25):
    m = text_mask(word, N, N, 0.12, slant)
    m = dilate(m, 3)
    out = dilate(m, 3) & ~m
    img = img.copy()
    img[out] = [0.05, 0.05, 0.05]
    fill = col(color)[0, 0] * (0.8 + 0.3 * fbm(rng, 8, 2)[m][:, None])
    img[m] = fill
    return img


def graffiti(rng, word, color):
    """RGBA tag for walls: fat outlined letters with drips; transparent background."""
    m = dilate(text_mask(word, N, N, 0.08, 0.3), 4)
    outline = dilate(m, 4) & ~m
    drips = np.zeros_like(m)
    xs = rng.choice(np.where(m.any(0))[0], 6)
    for x in xs:
        ys = np.where(m[:, x])[0]
        if len(ys): drips[ys.max():min(N, ys.max() + rng.integers(10, 40)), max(0, x - 1):x + 2] = True
    rgba = np.zeros((N, N, 4))
    shade = (0.75 + 0.35 * fbm(rng, 8, 2))[..., None]
    rgba[..., :3] = col(color) * shade
    rgba[outline, :3] = [0.04, 0.04, 0.05]
    rgba[..., 3] = (m | outline | drips).astype(float)
    hole = fbm(rng, 16, 3) > 0.7                           # weathered/peeled paint
    rgba[hole & m, 3] = 0
    return rgba


def sign(rng, text, bg, fg):
    Y, X = np.mgrid[0:N // 4, 0:N]
    h = N // 4
    img = np.ones((h, N, 3)) * col(bg)[0]
    m = text_mask(text, N, h, 0.1)
    img[m] = fg
    img[(Y < 4) | (Y > h - 5) | (X < 4) | (X > N - 5)] = [0.08, 0.07, 0.06]
    img *= (0.8 + 0.3 * vnoise(N, 16, 4, rng)[:h])[..., None]
    missing = (rng.random((h, N)) < 0.002)
    img[dilate(missing, 3) & m] = bg
    return img


def payphone(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = A.metal(rng, [0.55, 0.56, 0.58], rust=0.75)
    img[(Y < 50)] = [0.10, 0.22, 0.55]
    top = img[:50]
    top[text_mask("PHONE", N, 50, 0.15)] = [0.9, 0.9, 0.9]
    img[(Y > 80) & (Y < 150) & (X > 70) & (X < 186)] = [0.12, 0.12, 0.12]
    for r in range(4):
        for c in range(3):
            img[(np.abs(Y - (170 + r * 18)) < 6) & (np.abs(X - (100 + c * 28)) < 9)] = [0.7, 0.7, 0.68]
    return grime(img, rng, 0.35, 3)


def dumpster(rng):
    img = A.metal(rng, [0.16, 0.30, 0.20], rust=0.6)
    return graffiti_on(img, rng, "RAT", (0.85, 0.75, 0.2), 0.1)


def lamp_glass(rng):
    return np.ones((N, N, 3)) * col([0.95, 0.85, 0.6])


def chainlink(rng):
    Y, X = np.mgrid[0:N, 0:N]
    rgba = np.zeros((N, N, 4))
    wire = (np.abs(((X + Y) % 32) - 16) < 2) | (np.abs(((X - Y) % 32) - 16) < 2)
    rgba[..., :3] = col([0.45, 0.46, 0.45]) * (0.8 + 0.3 * fbm(rng, 16, 2))[..., None]
    rgba[..., 3] = wire.astype(float)
    return rgba


def line_paint(rng, color):
    img = np.ones((N, N, 3)) * col(color)
    worn = fbm(rng, 16, 4) > 0.55
    img[worn] = img[worn] * 0.35 + col([0.17, 0.17, 0.18])[0, 0] * 0.65
    return img


def litter(rng):
    rgba = np.zeros((N, N, 4))
    for _ in range(40):
        x, y = rng.integers(0, N, 2)
        w, h = rng.integers(6, 22, 2)
        c = rng.choice([[0.75, 0.73, 0.65], [0.55, 0.45, 0.3], [0.2, 0.2, 0.22], [0.6, 0.15, 0.1], [0.3, 0.4, 0.55]])
        rgba[y:y + h, x:x + w, :3] = c
        rgba[y:y + h, x:x + w, 3] = 1
    return rgba


def puddle(rng):
    rgba = np.zeros((N, N, 4))
    m = A.fbm(rng, 3, 4)
    Y, X = np.mgrid[0:N, 0:N]
    fall = 1 - np.clip(((X - N / 2) ** 2 + (Y - N / 2) ** 2) / (N / 2) ** 2, 0, 1)
    a = (m * fall) > 0.3
    rgba[..., :3] = col([0.10, 0.12, 0.15]) + 0.12 * (1 - Y / N)[..., None]
    rgba[..., 3] = a.astype(float)
    return rgba


TEXTURES = {
    "BrickRed": lambda r: brick_dirty(r), "BrickBrown": lambda r: brick_dirty(r, (0.30, 0.20, 0.14)),
    "Stucco": stucco, "Asphalt": asphalt, "Concrete": concrete, "Curb": curb, "Dirt": dirt, "Steel": steel,
    "WinDark": lambda r: window_dark(r), "WinLit": lambda r: window_dark(r, lit=True, curtain=[0.45, 0.2, 0.15]),
    "WinCurtain": lambda r: window_dark(r, curtain=[0.55, 0.52, 0.4]), "Boarded": boarded,
    "Shopfront": shopfront, "Shutter": shutter, "Payphone": payphone, "Dumpster": dumpster, "LampGlass": lamp_glass,
    "LineWhite": lambda r: line_paint(r, [0.75, 0.74, 0.7]), "LineYellow": lambda r: line_paint(r, [0.75, 0.6, 0.2]),
    "SignBar": lambda r: sign(r, "BAR", [0.12, 0.05, 0.05], [0.85, 0.2, 0.15]),
    "SignCafe": lambda r: sign(r, "CAFE", [0.75, 0.7, 0.55], [0.15, 0.25, 0.2]),
    "SignGroceries": lambda r: sign(r, "GROCERIES", [0.2, 0.35, 0.25], [0.85, 0.8, 0.6]),
    "SignDiner": lambda r: sign(r, "DINER", [0.8, 0.75, 0.6], [0.6, 0.12, 0.1]),
    "SignLiquor": lambda r: sign(r, "LIQUOR", [0.08, 0.08, 0.1], [0.9, 0.75, 0.3]),
    "SignPawn": lambda r: sign(r, "PAWN", [0.85, 0.7, 0.2], [0.1, 0.1, 0.1]),
    "SignSubway": lambda r: sign(r, "SUBWAY", [0.1, 0.25, 0.15], [0.9, 0.9, 0.85]),
}
RGBA = {
    "Graffiti1": lambda r: graffiti(r, "ZOD", [0.2, 0.6, 0.9]), "Graffiti2": lambda r: graffiti(r, "KRS", [0.9, 0.75, 0.2]),
    "Graffiti3": lambda r: graffiti(r, "SKAM", [0.85, 0.3, 0.6]), "Graffiti4": lambda r: graffiti(r, "BOO", [0.4, 0.8, 0.35]),
    "Graffiti5": lambda r: graffiti(r, "NOIR", [0.9, 0.9, 0.9]), "Fence": chainlink, "Litter": litter, "Puddle": puddle,
}
# Images that are stretched once over each face instead of tiled at 1 m.
FITTED = {"WinDark", "WinLit", "WinCurtain", "Boarded", "Shopfront", "Shutter", "Payphone", "SignBar", "SignCafe",
          "SignGroceries", "SignDiner", "SignLiquor", "SignPawn", "SignSubway", "Graffiti1", "Graffiti2", "Graffiti3",
          "Graffiti4", "Graffiti5", "Litter", "Puddle"}



def awning(rng, a=(0.20, 0.32, 0.24), b=(0.62, 0.58, 0.46)):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.where(((X // 32) % 2 == 0)[..., None], col(a), col(b)) * np.ones((N, N, 3))
    img *= (0.8 + 0.3 * fbm(rng, 12, 3))[..., None]
    return grime(img, rng, 0.45, 3)


TEXTURES.update({
    "Wood": A.dark_wood, "DoorGreen": A.door, "Awning": awning,
    "AwningRed": lambda r: awning(r, (0.38, 0.12, 0.10), (0.62, 0.58, 0.46)),
    "SignTaxi": lambda r: sign(r, "TAXI", [0.85, 0.7, 0.15], [0.08, 0.08, 0.08]),
})
FITTED.update({"DoorGreen", "SignTaxi"})



# ---------------------------------------------------------------- business district / suburbs / cars

def granite(rng, base=(0.22, 0.22, 0.24)):
    img = np.ones((N, N, 3)) * col(base)
    img *= (0.8 + 0.35 * fbm(rng, 64, 3))[..., None]
    img = speckle(img, rng, 0.06, 1.6)
    Y, X = np.mgrid[0:N, 0:N]
    img[((Y % 128) < 2) | ((X % 128) < 2)] *= 0.6
    return grime(img, rng, 0.25, 3)


def glass_office(rng, lit=False):
    Y, X = np.mgrid[0:N, 0:N]
    if lit:
        img = np.ones((N, N, 3)) * col([0.55, 0.50, 0.38])
        dark = vnoise(N, 8, 1, rng) > 0.55                         # some offices dark, some lit
        img[dark] = [0.10, 0.13, 0.17]
    else:
        img = np.ones((N, N, 3)) * col([0.20, 0.27, 0.34])
        img *= (0.75 + 0.45 * (1 - Y / N))[..., None]
        refl = (np.abs((X + Y * 0.8) % 200 - 70) < 22)
        img[refl] = img[refl] * 1.35 + 0.05
    img[(X % 64) < 3] = [0.12, 0.13, 0.14]                       # mullions
    return grime(img, rng, 0.15, 3)


def siding(rng, base):
    Y, X = np.mgrid[0:N, 0:N]
    board = 0.82 + 0.18 * ((Y % 32) / 32.0)
    img = col(base) * board[..., None]
    img[(Y % 32) < 2] *= 0.55
    img *= (0.88 + 0.18 * fbm(rng, 16, 3))[..., None]
    peel = fbm(rng, 10, 4) > 0.72
    img[peel] = img[peel] * 0.6 + col([0.25, 0.22, 0.18])[0, 0] * 0.4
    return grime(img, rng, 0.35, 3)


def shingles(rng):
    Y, X = np.mgrid[0:N, 0:N]
    row = Y // 21
    off = (row % 2) * 16
    tid = row * 8 + ((X + off) // 32) % 8
    tint = rng.uniform(0.75, 1.15, tid.max() + 1)
    img = col([0.20, 0.19, 0.20]) * tint[tid][..., None]
    img[(Y % 21) < 2] *= 0.45
    img[((X + off) % 32) < 1] *= 0.55
    moss = fbm(rng, 5, 4) > 0.64
    img[moss] = img[moss] * 0.6 + col([0.12, 0.18, 0.08])[0, 0]
    return grime(img, rng, 0.3, 3)


def grass(rng):
    img = np.ones((N, N, 3)) * col([0.22, 0.33, 0.14])
    img *= (0.7 + 0.5 * fbm(rng, 96, 2))[..., None]
    bald = fbm(rng, 4, 4) > 0.66
    img[bald] = img[bald] * 0.5 + col([0.30, 0.25, 0.15])[0, 0] * 0.5
    img *= (0.85 + 0.25 * fbm(rng, 6, 3))[..., None]
    return speckle(img, rng, 0.04, 1.4)


def leaves(rng, base=(0.16, 0.28, 0.11)):
    img = np.ones((N, N, 3)) * col(base)
    clumps = fbm(rng, 24, 3)
    img *= (0.5 + 0.9 * clumps)[..., None]
    img = speckle(img, rng, 0.08, 1.5)
    return img


def bark(rng):
    grain = vnoise(N, 24, 2, rng) * 0.6 + fbm(rng, 16, 3, cy=4) * 0.4
    return col([0.24, 0.18, 0.13]) * (0.6 + 0.6 * grain)[..., None]


def car_paint(rng, base, checker=False):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col(base)
    img *= (0.85 + 0.2 * fbm(rng, 12, 3))[..., None]
    dent = fbm(rng, 20, 3) > 0.78
    img[dent] *= 0.9
    rust = np.clip((fbm(rng, 8, 4) - 0.8) * 3, 0, 1)
    img = img * (1 - rust[..., None]) + col([0.30, 0.18, 0.10]) * rust[..., None]
    if checker:
        band = (np.abs(Y - N // 2) < 12)
        chk = (((X // 12) + (Y // 12)) % 2 == 0)
        img[band & chk] = [0.05, 0.05, 0.05]
        img[band & ~chk] = [0.85, 0.85, 0.8]
    hl = (np.abs(Y - N * 0.3) < 10)                                   # a soft highlight along the body
    img[hl] = img[hl] * 1.25 + 0.03
    return grime(img, rng, 0.15, 3)


def tire(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.07, 0.07, 0.08])
    r = np.hypot(X - N / 2, Y - N / 2) / (N / 2)
    img[r < 0.55] = [0.45, 0.45, 0.47]
    img[r < 0.15] = [0.2, 0.2, 0.22]
    return img * (0.85 + 0.2 * fbm(rng, 16, 2))[..., None]


def car_lights(rng):
    img = np.ones((N, N, 3)) * col([0.95, 0.9, 0.75])
    img[:, N // 2:] = [0.7, 0.08, 0.06]                          # left half head lamp, right half tail lamp
    return img


TEXTURES.update({
    "Granite": granite, "GraniteLight": lambda r: granite(r, (0.48, 0.46, 0.43)),
    "GlassOffice": glass_office, "GlassOfficeLit": lambda r: glass_office(r, True),
    "SidingWhite": lambda r: siding(r, [0.72, 0.70, 0.64]), "SidingBlue": lambda r: siding(r, [0.42, 0.52, 0.58]),
    "SidingSage": lambda r: siding(r, [0.48, 0.55, 0.44]), "SidingCream": lambda r: siding(r, [0.72, 0.64, 0.48]),
    "Shingles": shingles, "Grass": grass, "Hedge": leaves, "Leaves": lambda r: leaves(r, (0.18, 0.30, 0.12)),
    "LeavesAutumn": lambda r: leaves(r, (0.40, 0.26, 0.10)), "Bark": bark,
    "CarRed": lambda r: car_paint(r, [0.45, 0.10, 0.08]), "CarBlue": lambda r: car_paint(r, [0.14, 0.22, 0.38]),
    "CarGrey": lambda r: car_paint(r, [0.42, 0.43, 0.44]), "CarBlack": lambda r: car_paint(r, [0.08, 0.08, 0.09]),
    "CarWhite": lambda r: car_paint(r, [0.72, 0.71, 0.66]), "CarGreen": lambda r: car_paint(r, [0.16, 0.28, 0.20]),
    "CarTaxi": lambda r: car_paint(r, [0.85, 0.66, 0.12], checker=True), "Tire": tire, "CarLights": car_lights,
    "SignMall": lambda r: sign(r, "MALL", [0.12, 0.12, 0.16], [0.9, 0.85, 0.7]),
    "SignCoffee": lambda r: sign(r, "COFFEE", [0.30, 0.18, 0.10], [0.95, 0.85, 0.6]),
    "SignTower": lambda r: sign(r, "TOWER", [0.10, 0.10, 0.12], [0.85, 0.85, 0.85]),
    "SignHardware": lambda r: sign(r, "HARDWARE", [0.65, 0.15, 0.10], [0.95, 0.9, 0.8]),
    "SignLaundry": lambda r: sign(r, "LAUNDRY", [0.20, 0.40, 0.60], [0.95, 0.95, 0.95]),
    "SignMarket": lambda r: sign(r, "MARKET", [0.20, 0.35, 0.22], [0.9, 0.85, 0.6]),
})
FITTED.update({"SignMall", "SignCoffee", "SignTower", "SignHardware", "SignLaundry", "SignMarket", "Tire", "CarLights"})



# ---------------------------------------------------------------- clean set (Business District, Suburbs)

def clean(img, rng, amount=0.12):
    return img * (1 - amount + amount * 1.6 * fbm(rng, 3, 3))[..., None]


def limestone(rng, base=(0.74, 0.70, 0.60)):
    Y, X = np.mgrid[0:N, 0:N]
    row = Y // 32
    off = (row % 2) * 32
    bid = row * 4 + ((X + off) // 64) % 4
    tint = rng.uniform(0.94, 1.06, bid.max() + 1)
    img = col(base) * tint[bid][..., None] * (0.92 + 0.12 * fbm(rng, 48, 3))[..., None]
    joints = ((Y % 32) < 1) | (((X + off) % 64) < 1)
    img[joints] *= 0.8
    return clean(img, rng, 0.12)


def win_punch(rng, lit=False):
    Y, X = np.mgrid[0:N, 0:N]
    if lit:
        img = np.ones((N, N, 3)) * col([0.80, 0.68, 0.42])
    else:
        img = np.ones((N, N, 3)) * col([0.36, 0.48, 0.60]) * (0.75 + 0.45 * (1 - Y / N))[..., None]
        refl = (np.abs((X + Y * 0.7) % 220 - 80) < 20)
        img[refl] = img[refl] * 1.25 + 0.06
    frame = (X < 14) | (X > N - 15) | (Y < 14) | (Y > N - 15) | (np.abs(X - N // 2) < 5) | (np.abs(Y - N * 0.4) < 5)
    img[frame] = [0.86, 0.85, 0.80]
    return img


def asphalt_clean(rng):
    img = np.ones((N, N, 3)) * col([0.30, 0.30, 0.31])
    img *= (0.9 + 0.15 * fbm(rng, 96, 2))[..., None]
    img = speckle(img, rng, 0.05, 1.25)
    cracks = np.abs(fbm(rng, 4, 4) - 0.5) < 0.003
    img[cracks] *= 0.7
    return clean(img, rng, 0.08)


def concrete_clean(rng, base=(0.70, 0.68, 0.63)):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col(base) * (0.93 + 0.1 * fbm(rng, 32, 3))[..., None]
    img[((X % 128) < 2) | ((Y % 128) < 2)] *= 0.75
    return clean(img, rng, 0.1)


def stucco_clean(rng, base):
    img = np.ones((N, N, 3)) * col(base) * (0.93 + 0.1 * fbm(rng, 48, 3))[..., None]
    return clean(img, rng, 0.08)


def roof_tiles(rng, base=(0.42, 0.25, 0.17)):
    Y, X = np.mgrid[0:N, 0:N]
    row = Y // 16
    off = (row % 2) * 16
    tid = row * 8 + ((X + off) // 32) % 8
    tint = rng.uniform(0.85, 1.12, tid.max() + 1)
    img = col(base) * tint[tid][..., None]
    img[(Y % 16) < 2] *= 0.6
    img[((X + off) % 32) < 1] *= 0.75
    return clean(img, rng, 0.1)


def grass_lush(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.30, 0.50, 0.17])
    img *= (0.82 + 0.3 * fbm(rng, 96, 2))[..., None]
    img *= (0.95 + 0.06 * np.sign(np.sin(X * np.pi / 64)))[..., None]       # faint mowing stripes
    return speckle(img, rng, 0.03, 1.2)


def water(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.22, 0.55, 0.72])
    caustic = np.abs(fbm(rng, 12, 3) - 0.5) < 0.03
    img[caustic] = img[caustic] * 1.3 + 0.1
    return img


def garage_door(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.84, 0.83, 0.78])
    img[(Y % 52) < 3] *= 0.7
    img[(X < 6) | (X > N - 7)] *= 0.75
    return clean(img, rng, 0.08)


def chrome(rng):
    Y, X = np.mgrid[0:N, 0:N]
    t = Y / N
    img = col([0.85, 0.86, 0.88]) * (0.55 + 0.6 * np.abs(np.sin(t * 6.0)))[..., None] * np.ones((N, N, 3))
    return img


TEXTURES.update({
    "Limestone": limestone, "LimestoneDark": lambda r: limestone(r, (0.60, 0.57, 0.50)),
    "WinPunch": win_punch, "WinPunchLit": lambda r: win_punch(r, True),
    "AsphaltClean": asphalt_clean, "ConcreteClean": concrete_clean, "CurbClean": lambda r: concrete_clean(r, (0.78, 0.77, 0.73)),
    "BrickClean": lambda r: clean(A.brick(r) * 1.1, r, 0.05),
    "StuccoCream": lambda r: stucco_clean(r, [0.86, 0.80, 0.64]), "StuccoWhite": lambda r: stucco_clean(r, [0.88, 0.87, 0.82]),
    "StuccoPeach": lambda r: stucco_clean(r, [0.86, 0.70, 0.58]),
    "RoofBrown": roof_tiles, "RoofRed": lambda r: roof_tiles(r, (0.48, 0.20, 0.14)), "GrassLush": grass_lush,
    "LeavesBright": lambda r: leaves(r, (0.24, 0.44, 0.15)), "Pine": lambda r: leaves(r, (0.09, 0.22, 0.11)),
    "LeavesYellow": lambda r: leaves(r, (0.62, 0.52, 0.14)), "Water": water, "GarageDoor": garage_door, "Chrome": chrome,
    "CarTeal": lambda r: car_paint(r, [0.20, 0.42, 0.42]), "CarMint": lambda r: car_paint(r, [0.42, 0.62, 0.50]),
    "CarMaroon": lambda r: car_paint(r, [0.36, 0.10, 0.10]), "CarCream": lambda r: car_paint(r, [0.80, 0.74, 0.58]),
})
FITTED.update({"WinPunch", "WinPunchLit", "GarageDoor"})



# ---------------------------------------------------------------- interiors (Schrute Towers, Kevin's house, the bar)

def marble(rng, base=(0.86, 0.85, 0.82), tile=64):
    Y, X = np.mgrid[0:N, 0:N]
    tid = (Y // tile) * (N // tile) + (X // tile)
    tint = rng.uniform(0.95, 1.04, tid.max() + 1)
    img = col(base) * tint[tid][..., None]
    vein = np.abs(fbm(rng, 6, 5) - 0.5) < 0.02
    img[vein] *= 0.78
    img[((X % tile) < 1) | ((Y % tile) < 1)] *= 0.82
    shine = np.clip(1 - np.abs((X + Y) % 256 - 128) / 40.0, 0, 1)
    return img + 0.08 * shine[..., None]


def carpet_tiles(rng, base=(0.30, 0.34, 0.40)):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col(base) * (0.85 + 0.25 * fbm(rng, 96, 2))[..., None]
    turn = (((X // 128) + (Y // 128)) % 2 == 0)
    img[turn] *= 1.06
    return clean(img, rng, 0.06)


def wood_clean(rng, base=(0.62, 0.48, 0.32)):
    Y, X = np.mgrid[0:N, 0:N]
    ph = 32
    row = Y // ph
    shift = rng.integers(0, N, N // ph)
    seg = ((X + shift[row]) // 128) % 2
    tint = rng.uniform(0.9, 1.08, (N // ph) * 2)
    grain = vnoise(N, 4, 128, rng) * 0.6 + fbm(rng, 8, 3, cy=64) * 0.4
    img = col(base) * (tint[row * 2 + seg] * (0.82 + 0.3 * grain))[..., None]
    img[((Y % ph) == 0) | (((X + shift[row]) % 128) == 0)] *= 0.6
    return img


def tiles_white(rng, t=32, base=(0.88, 0.89, 0.88)):
    Y, X = np.mgrid[0:N, 0:N]
    k = N // t + 1                                               # tiles per row (the last may be partial)
    img = np.ones((N, N, 3)) * col(base) * rng.uniform(0.96, 1.03, k * k)[(Y // t) * k + X // t][..., None]
    img[((X % t) < 2) | ((Y % t) < 2)] = [0.62, 0.62, 0.60]
    return img


def fabric_clean(rng, base):
    Y, X = np.mgrid[0:N, 0:N]
    weave = 0.94 + 0.06 * np.sign(np.sin(X * np.pi / 2) * np.sin(Y * np.pi / 2))
    return col(base) * (weave * (0.88 + 0.18 * fbm(rng, 16, 3)))[..., None]


def monitor(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.10, 0.22, 0.40])
    img[(Y % 6) < 2] *= 0.8
    rows = (Y > 40) & (Y < 200) & ((Y // 16) % 2 == 0) & (X > 30) & (X < 30 + (rng.random(N) * 180)[Y // 16 * 16 % N])
    img[rows] = [0.65, 0.80, 0.95]
    return img


def glass_partition(rng):
    Y, X = np.mgrid[0:N, 0:N]
    rgba = np.zeros((N, N, 4))
    rgba[..., :3] = [0.80, 0.86, 0.88]
    band = (np.abs(Y - N * 0.5) < N * 0.14)                      # frosted band at eye level
    frame = (X < 6) | (X > N - 7) | (Y < 6) | (Y > N - 7)
    rgba[..., 3] = (band | frame).astype(float)
    rgba[frame, :3] = [0.35, 0.36, 0.38]
    return rgba


def elevator_door(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.70, 0.71, 0.73]) * (0.9 + 0.12 * vnoise(N, 2, 64, rng))[..., None]
    img[np.abs(X - N // 2) < 2] = [0.25, 0.25, 0.27]
    img[(X < 8) | (X > N - 9) | (Y > N - 9)] = [0.45, 0.42, 0.30]
    return img


def back_bar(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.16, 0.10, 0.07])
    for r in range(3):
        y0 = 20 + r * 80
        img[(Y > y0 + 60) & (Y < y0 + 66)] = [0.35, 0.25, 0.15]                    # shelves
        x = 6
        while x < N - 10:
            w = int(rng.integers(8, 14)); h = int(rng.integers(30, 55))
            c = rng.choice([[0.15, 0.4, 0.15], [0.45, 0.25, 0.08], [0.6, 0.6, 0.62], [0.5, 0.1, 0.1], [0.75, 0.6, 0.2]])
            img[(Y > y0 + 60 - h) & (Y < y0 + 60) & (X > x) & (X < x + w)] = c
            x += w + int(rng.integers(2, 6))
    return img


TEXTURES.update({
    "Marble": marble, "MarbleDark": lambda r: marble(r, (0.30, 0.30, 0.32), 128),
    "CarpetOffice": carpet_tiles, "PaintOffice": lambda r: stucco_clean(r, [0.84, 0.84, 0.80]),
    "PaintSage": lambda r: stucco_clean(r, [0.64, 0.70, 0.60]), "PaintBlush": lambda r: stucco_clean(r, [0.86, 0.76, 0.70]),
    "WoodOak": wood_clean, "WoodDarkPolish": lambda r: wood_clean(r, (0.28, 0.16, 0.09)),
    "TileWhite": tiles_white, "TileKitchen": lambda r: tiles_white(r, 24, (0.86, 0.84, 0.74)),
    "Laminate": lambda r: stucco_clean(r, [0.78, 0.74, 0.64]), "DeskGrey": lambda r: stucco_clean(r, [0.42, 0.43, 0.45]),
    "FabricBlue": lambda r: fabric_clean(r, [0.20, 0.26, 0.40]), "FabricRed": lambda r: fabric_clean(r, [0.48, 0.10, 0.10]),
    "FabricBrown": lambda r: fabric_clean(r, [0.40, 0.30, 0.22]), "FabricCream": lambda r: fabric_clean(r, [0.78, 0.72, 0.60]),
    "Linen": lambda r: fabric_clean(r, [0.86, 0.85, 0.80]), "BeigePlastic": lambda r: stucco_clean(r, [0.76, 0.72, 0.62]),
    "Monitor": monitor, "ElevatorDoor": elevator_door, "BackBar": back_bar, "Brass": lambda r: stucco_clean(r, [0.72, 0.56, 0.24]),
    "SignSchrute": lambda r: sign(r, "SCHRUTE TOWERS", [0.12, 0.12, 0.14], [0.88, 0.78, 0.45]),
    "SignPaper": lambda r: sign(r, "PAPER COMPANY", [0.85, 0.85, 0.82], [0.12, 0.22, 0.45]),
    "SignMen": lambda r: sign(r, "MEN", [0.20, 0.30, 0.55], [0.95, 0.95, 0.95]),
    "SignWomen": lambda r: sign(r, "WOMEN", [0.55, 0.20, 0.30], [0.95, 0.95, 0.95]),
})
RGBA["GlassPartition"] = glass_partition
FITTED.update({"Monitor", "ElevatorDoor", "BackBar", "SignSchrute", "SignPaper", "SignMen", "SignWomen", "GlassPartition"})



# shared with the apartment's set (furniture and fittings)
TEXTURES.update({
    "Cabinet": lambda r: A.paint(r, [0.80, 0.78, 0.70], under=(0.6, 0.58, 0.52), chip=0.8),
    "Cardboard": A.cardboard, "Paper": A.paper,
    "GlassBrown": lambda r: A.glass(r, [0.30, 0.16, 0.06]), "GlassGreen": lambda r: A.glass(r, [0.10, 0.28, 0.12]),
    "MetalDark": lambda r: A.metal(r, [0.17, 0.17, 0.18], 0.85), "MetalWhite": lambda r: A.metal(r, [0.84, 0.84, 0.80], 0.9),
})


def save(name, arr, folder):
    import bpy
    arr = np.clip(arr, 0, 1)
    h, w = arr.shape[:2]
    full = "T_Shk_" + name
    img = bpy.data.images.get(full)
    if img: bpy.data.images.remove(img)
    alpha = arr.shape[2] == 4
    img = bpy.data.images.new(full, w, h, alpha=alpha)
    rgba = arr if alpha else np.dstack([arr, np.ones((h, w))])
    img.pixels.foreach_set(rgba[::-1].astype(np.float32).ravel())
    img.filepath_raw = os.path.join(folder, full + ".png")
    img.file_format = "PNG"
    img.save()


def make_all(folder):
    os.makedirs(folder, exist_ok=True)
    for i, (name, fn) in enumerate(list(TEXTURES.items()) + list(RGBA.items())):
        save(name, fn(np.random.default_rng(2000 + i)), folder)
    return len(TEXTURES) + len(RGBA)
