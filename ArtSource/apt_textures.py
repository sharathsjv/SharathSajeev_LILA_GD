# Procedural PS1-noir textures for the apartment (runs inside Blender; numpy only).
import bpy, os
import numpy as np

N = 256


def vnoise(n, cx, cy, rng):
    g = rng.random((cy, cx))
    xs = np.arange(n) * cx / n
    ys = np.arange(n) * cy / n
    x0 = np.floor(xs).astype(int); fx = xs - x0; x1 = (x0 + 1) % cx
    y0 = np.floor(ys).astype(int); fy = ys - y0; y1 = (y0 + 1) % cy
    sx = fx * fx * (3 - 2 * fx); sy = fy * fy * (3 - 2 * fy)
    a = g[np.ix_(y0, x0)]; b = g[np.ix_(y0, x1)]; c = g[np.ix_(y1, x0)]; d = g[np.ix_(y1, x1)]
    top = a + (b - a) * sx[None, :]
    bot = c + (d - c) * sx[None, :]
    return top + (bot - top) * sy[:, None]


def fbm(rng, c, octaves=4, cy=None, n=N):
    cy = cy or c
    tot = np.zeros((n, n)); amp = 1.0; norm = 0.0
    for o in range(octaves):
        tot += amp * vnoise(n, min(n, c * 2 ** o), min(n, cy * 2 ** o), rng)
        norm += amp; amp *= 0.5
    return tot / norm


def col(c):
    return np.array(c, dtype=float)[None, None, :]


def grime(img, rng, strength=0.45, c=4):
    g = fbm(rng, c, 4)
    return img * (1 - strength + strength * 1.4 * g)[..., None]


def speckle(img, rng, amount=0.01, dark=0.4):
    m = rng.random(img.shape[:2]) < amount
    img[m] *= dark
    return img


# ---------------------------------------------------------------- surfaces

def brick(rng):
    Y, X = np.mgrid[0:N, 0:N]
    rh, bw, mor = 16, 64, 2
    row = Y // rh
    off = (row % 2) * (bw // 2)
    bx = ((X + off) // bw) % (N // bw)
    idx = row * (N // bw) + bx
    count = (N // rh) * (N // bw)
    base = np.array([0.46, 0.21, 0.14])
    tint = rng.uniform(0.65, 1.2, (count, 1)) * base[None, :]
    tint[:, 1] *= rng.uniform(0.85, 1.25, count)
    img = tint[idx]
    det = fbm(rng, 32, 3)
    img = img * (0.75 + 0.5 * det)[..., None]
    mortar = ((Y % rh) < mor) | (((X + off) % bw) < mor)
    img[mortar] = (col([0.42, 0.40, 0.36]) * (0.7 + 0.5 * det[..., None]))[mortar]
    img = grime(img, rng, 0.55, 3)
    return speckle(img, rng, 0.015, 0.5)


def plaster(rng):
    img = np.ones((N, N, 3)) * col([0.58, 0.56, 0.49])
    img *= (0.85 + 0.25 * fbm(rng, 16, 4))[..., None]
    stain = fbm(rng, 3, 3)
    ring = np.clip(1 - np.abs(stain - 0.55) * 14, 0, 1)
    img *= (1 - 0.35 * np.clip(stain - 0.5, 0, 1) * 2)[..., None]
    img -= ring[..., None] * col([0.05, 0.08, 0.12])
    return grime(img, rng, 0.35, 2)


def wood_floor(rng):
    Y, X = np.mgrid[0:N, 0:N]
    ph = 32
    row = Y // ph
    shift = rng.integers(0, N, N // ph)
    seg = ((X + shift[row]) // 128) % 2
    pid = row * 2 + seg
    tint = rng.uniform(0.7, 1.15, (N // ph) * 2)
    grain = vnoise(N, 4, 128, rng) * 0.6 + fbm(rng, 8, 3, cy=64) * 0.4
    img = col([0.34, 0.22, 0.13]) * (tint[pid] * (0.7 + 0.5 * grain))[..., None]
    gap = ((Y % ph) == 0) | (((X + shift[row]) % 128) == 0)
    img[gap] *= 0.35
    img = grime(img, rng, 0.5, 3)
    return speckle(img, rng, 0.02, 0.55)


def linoleum(rng):
    Y, X = np.mgrid[0:N, 0:N]
    chk = ((X // 64) + (Y // 64)) % 2
    img = np.where(chk[..., None] == 0, col([0.72, 0.69, 0.57]), col([0.22, 0.30, 0.27]))
    img = img * (0.85 + 0.25 * fbm(rng, 24, 3))[..., None]
    yell = fbm(rng, 3, 3)
    img = img * (1 - 0.3 * yell[..., None]) + col([0.08, 0.06, 0.0]) * yell[..., None]
    return speckle(grime(img, rng, 0.4, 4), rng, 0.01, 0.5)


def bath_tile(rng):
    Y, X = np.mgrid[0:N, 0:N]
    t = 32
    tid = (Y // t) * (N // t) + (X // t)
    tint = rng.uniform(0.9, 1.05, (N // t) ** 2)
    img = col([0.80, 0.82, 0.80]) * tint[tid][..., None]
    grout = ((X % t) < 2) | ((Y % t) < 2)
    img[grout] = [0.28, 0.27, 0.24]
    st = fbm(rng, 4, 4)
    img = img * (1 - 0.45 * np.clip(st - 0.45, 0, 1)[..., None] * 2) + col([0.06, 0.05, 0.0]) * st[..., None]
    return speckle(img, rng, 0.01, 0.6)


def formica(rng):
    img = np.ones((N, N, 3)) * col([0.55, 0.52, 0.45])
    img *= (0.9 + 0.2 * fbm(rng, 64, 2))[..., None]
    img = speckle(img, rng, 0.08, 0.7)
    return grime(img, rng, 0.35, 3)


def fabric(rng, base):
    Y, X = np.mgrid[0:N, 0:N]
    weave = 0.92 + 0.08 * np.sign(np.sin(X * np.pi / 2) * np.sin(Y * np.pi / 2))
    img = col(base) * (weave * (0.8 + 0.35 * fbm(rng, 16, 3)))[..., None]
    st = fbm(rng, 3, 3)
    img *= (1 - 0.4 * np.clip(st - 0.5, 0, 1) * 2)[..., None]
    return grime(img, rng, 0.3, 2)


def sheets(rng):
    r = fbm(rng, 6, 4)
    wrinkle = 1 - np.abs(r - 0.5) * 2
    img = col([0.70, 0.70, 0.66]) * (0.7 + 0.35 * wrinkle)[..., None]
    st = fbm(rng, 3, 3)
    img = img * (1 - 0.3 * np.clip(st - 0.5, 0, 1)[..., None] * 2) + col([0.07, 0.05, 0.0]) * st[..., None] * 0.6
    return img


def dark_wood(rng):
    grain = vnoise(N, 3, 96, rng) * 0.6 + fbm(rng, 6, 3, cy=48) * 0.4
    img = col([0.22, 0.13, 0.07]) * (0.7 + 0.6 * grain)[..., None]
    return speckle(grime(img, rng, 0.35, 3), rng, 0.01, 0.6)


def paint(rng, base, under=(0.30, 0.26, 0.20), chip=0.66):
    img = np.ones((N, N, 3)) * col(base)
    img *= (0.85 + 0.25 * fbm(rng, 16, 3))[..., None]
    chips = fbm(rng, 12, 4) > chip
    img[chips] = (col(under) * (0.8 + 0.4 * fbm(rng, 32, 2))[..., None])[chips]
    return grime(img, rng, 0.45, 3)


def metal(rng, base, rust=0.7):
    img = np.ones((N, N, 3)) * col(base)
    img *= (0.88 + 0.2 * fbm(rng, 24, 3))[..., None]
    r = fbm(rng, 10, 4)
    m = np.clip((r - rust) * 6, 0, 1)
    img = img * (1 - m[..., None]) + col([0.27, 0.20, 0.13]) * (0.6 + 0.5 * fbm(rng, 48, 2))[..., None] * m[..., None]
    return grime(img, rng, 0.3, 3)


def screen(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.10, 0.16, 0.22])
    cx = (X - N / 2) / (N / 2); cy = (Y - N / 2) / (N / 2)
    glow = np.clip(1 - (cx ** 2 + cy ** 2) * 0.8, 0, 1)
    img = img * (0.4 + 0.9 * glow)[..., None]
    img[(Y % 4) < 1] *= 0.55
    return img


def cardboard(rng):
    Y, X = np.mgrid[0:N, 0:N]
    corr = 0.93 + 0.07 * np.sin(X * np.pi / 3)
    img = col([0.58, 0.45, 0.29]) * (corr * (0.85 + 0.25 * fbm(rng, 16, 3)))[..., None]
    tape = (np.abs(Y - N // 2) < 14)
    img[tape] = img[tape] * 0.85 + col([0.15, 0.13, 0.08])
    st = fbm(rng, 4, 3)
    img *= (1 - 0.5 * np.clip(st - 0.55, 0, 1) * 2)[..., None]
    return img


def trash(rng):
    r = fbm(rng, 8, 4)
    sheen = np.clip(1 - np.abs(r - 0.5) * 6, 0, 1)
    return col([0.05, 0.055, 0.07]) + col([0.25, 0.27, 0.32]) * sheen[..., None] * 0.6


def ceramic(rng):
    img = np.ones((N, N, 3)) * col([0.80, 0.80, 0.76])
    img *= (0.92 + 0.12 * fbm(rng, 16, 3))[..., None]
    st = fbm(rng, 4, 4)
    m = np.clip((st - 0.55) * 4, 0, 1)
    img = img * (1 - 0.35 * m[..., None]) + col([0.08, 0.06, 0.0]) * m[..., None]
    return img


def glass(rng, base):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col(base)
    hl = (np.abs(X - N * 0.3) < N * 0.06)
    img[hl] = img[hl] * 1.6 + 0.08
    return img * (0.85 + 0.2 * fbm(rng, 8, 2))[..., None]


def cloth_pile(rng):
    pal = np.array([[0.22, 0.27, 0.38], [0.30, 0.31, 0.33], [0.55, 0.53, 0.46], [0.15, 0.15, 0.16], [0.33, 0.31, 0.19]])
    q = fbm(rng, 5, 3)
    idx = np.clip((q - q.min()) / (q.max() - q.min() + 1e-6) * len(pal), 0, len(pal) - 1).astype(int)
    img = pal[idx]
    r = fbm(rng, 12, 3)
    return img * (0.65 + 0.5 * (1 - np.abs(r - 0.5) * 2))[..., None]


def books(rng):
    Y, X = np.mgrid[0:N, 0:N]
    w = 16
    bid = X // w
    pal = rng.uniform(0.1, 0.6, (N // w, 3)) * np.array([1.0, 0.75, 0.6])
    img = pal[bid] * (0.85 + 0.2 * fbm(rng, 16, 2))[..., None]
    img[(X % w) < 1] *= 0.4
    bands = (np.abs((Y % 128) - 30) < 4) | (np.abs((Y % 128) - 96) < 3)
    img[bands] = img[bands] * 0.6 + 0.25
    return img


def paper(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.78, 0.76, 0.68])
    lines = ((Y % 12) == 0) & (X > 20) & (X < N - 20) & (rng.random((N, N)) > 0.15)
    img[lines] = [0.25, 0.25, 0.3]
    return grime(img, rng, 0.25, 3)


# ---------------------------------------------------------------- fitted (one per face) images

def poster_noir(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.09, 0.09, 0.12])
    moon = (X - 170) ** 2 + (Y - 70) ** 2 < 42 ** 2
    img[moon] = [0.85, 0.82, 0.68]
    fig = ((np.abs(X - 100) < 18) & (Y > 110) & (Y < 230)) | ((X - 100) ** 2 + (Y - 95) ** 2 < 16 ** 2)
    hat = (np.abs(X - 100) < 30) & (np.abs(Y - 82) < 4)
    img[fig | hat] = [0.02, 0.02, 0.02]
    title = (Y > 20) & (Y < 44) & (X > 24) & (X < 120)
    img[title] = [0.65, 0.10, 0.08]
    for r in range(3):
        bars = (np.abs(Y - (236 + r * 7)) < 2) & (X > 30) & (X < 30 + rng.integers(80, 200))
        img[bars] = [0.7, 0.68, 0.6]
    return grime(img, rng, 0.35, 3)


def poster_jazz(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.70, 0.52, 0.18])
    sax = ((X - 128) ** 2 / 50 ** 2 + (Y - 140) ** 2 / 80 ** 2) < 1
    img[sax] = [0.05, 0.04, 0.03]
    img[(Y > 18) & (Y < 50) & (X > 30) & (X < 226)] = [0.08, 0.06, 0.05]
    img[(Y > 228) & (Y < 240) & (X > 60) & (X < 196)] = [0.45, 0.10, 0.06]
    return grime(img, rng, 0.4, 3)


def window(rng):
    Y, X = np.mgrid[0:N, 0:N]
    t = Y / N
    img = col([0.62, 0.68, 0.76]) * (1 - 0.35 * t)[..., None]
    city = (np.sin(X * 0.09) > 0.2) & (Y > 120 + 40 * np.sin(X * 0.031))
    img[city] *= 0.55
    streak = vnoise(N, 64, 2, rng)
    img *= (0.85 + 0.2 * streak)[..., None]
    return grime(img, rng, 0.3, 3)


def door(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = paint(rng, [0.16, 0.24, 0.20], under=(0.35, 0.33, 0.30))
    for (x0, x1, y0, y1) in [(30, 226, 24, 110), (30, 226, 140, 232)]:
        edge = ((np.abs(X - x0) < 3) | (np.abs(X - x1) < 3)) & (Y > y0) & (Y < y1) | \
               ((np.abs(Y - y0) < 3) | (np.abs(Y - y1) < 3)) & (X > x0) & (X < x1)
        img[edge] *= 0.45
    return img


def rug(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.34, 0.09, 0.09])
    border = (np.minimum(np.minimum(X, N - 1 - X), np.minimum(Y, N - 1 - Y)) < 18)
    img[border] = [0.55, 0.42, 0.20]
    dia = ((np.abs(X - 128) + np.abs(Y - 128)) % 48) < 6
    img[dia & ~border] = [0.62, 0.50, 0.28]
    wear = fbm(rng, 4, 3)
    img = img * (1 - 0.4 * np.clip(wear - 0.5, 0, 1)[..., None] * 2) + col([0.12, 0.1, 0.08]) * wear[..., None] * 0.5
    return img


def mirror(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.45, 0.52, 0.58])
    img *= (0.8 + 0.3 * (1 - Y / N))[..., None]
    spots = fbm(rng, 8, 3) > 0.62
    img[spots] *= 0.6
    return img


TEXTURES = {
    "Brick": brick, "Plaster": plaster, "WoodFloor": wood_floor, "Linoleum": linoleum, "BathTile": bath_tile,
    "Formica": formica, "Couch": lambda r: fabric(r, [0.30, 0.29, 0.19]), "Fabric": lambda r: fabric(r, [0.55, 0.47, 0.32]),
    "Sheets": sheets, "DarkWood": dark_wood, "Cabinet": lambda r: paint(r, [0.52, 0.60, 0.50]),
    "MetalWhite": lambda r: metal(r, [0.76, 0.75, 0.70]), "MetalDark": lambda r: metal(r, [0.17, 0.17, 0.18], 0.7),
    "Plastic": lambda r: metal(r, [0.45, 0.44, 0.41], 0.9), "Screen": screen, "Cardboard": cardboard, "Trash": trash,
    "Ceramic": ceramic, "GlassGreen": lambda r: glass(r, [0.10, 0.28, 0.12]), "GlassBrown": lambda r: glass(r, [0.30, 0.16, 0.06]),
    "Cloth": cloth_pile, "Books": books, "Paper": paper,
    "PosterNoir": poster_noir, "PosterJazz": poster_jazz, "Window": window, "Door": door, "Rug": rug, "Mirror": mirror,
}
# ---------------------------------------------------------------- room wall finishes

def wallpaper(rng):
    """Faded striped damask, yellowed and water-stained, torn away in places to show the plaster."""
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.38, 0.43, 0.33])
    img[(X % 64) < 24] *= 1.12
    motif = (((X % 64) - 44) ** 2 / 36.0 + ((Y % 48) - 24) ** 2 / 100.0) < 1
    img[motif] = img[motif] * 0.7 + col([0.12, 0.11, 0.05])[0, 0]
    img *= (0.88 + 0.2 * fbm(rng, 16, 3))[..., None]
    st = fbm(rng, 3, 4)
    img = img * (1 - 0.35 * np.clip(st - 0.45, 0, 1)[..., None] * 2) + col([0.10, 0.07, 0.0]) * st[..., None] * 0.6
    torn = fbm(rng, 2, 5)
    torn = (torn - torn.min()) / (torn.max() - torn.min())
    hole = torn > 0.8
    rim = (torn > 0.76) & ~hole
    img[hole] = (plaster(np.random.default_rng(99)) * 0.85)[hole]
    img[rim] = img[rim] * 0.55 + col([0.25, 0.22, 0.15])[0, 0]
    return img


def paint_cream(rng):
    img = paint(rng, [0.66, 0.61, 0.47], under=(0.52, 0.50, 0.44), chip=0.73)
    grease = fbm(rng, 2, 4)
    return img * (1 - 0.3 * grease[..., None]) + col([0.06, 0.04, 0.0]) * grease[..., None]


def kitchen_tile(rng):
    Y, X = np.mgrid[0:N, 0:N]
    t = 32
    tid = (Y // t) * (N // t) + (X // t)
    tint = rng.uniform(0.88, 1.05, (N // t) ** 2)
    img = col([0.76, 0.74, 0.64]) * tint[tid][..., None]
    img[((Y // t) % 4) == 2] *= col([0.55, 0.75, 0.62])[0, 0]
    img[((X % t) < 2) | ((Y % t) < 2)] = [0.25, 0.23, 0.19]
    gr = fbm(rng, 3, 4)
    return img * (1 - 0.35 * gr[..., None]) + col([0.08, 0.05, 0.0]) * gr[..., None]


def carpet(rng):
    img = np.ones((N, N, 3)) * col([0.33, 0.27, 0.20])
    img *= (0.8 + 0.3 * fbm(rng, 64, 2))[..., None]
    st = fbm(rng, 4, 3)
    img *= (1 - 0.45 * np.clip(st - 0.5, 0, 1) * 2)[..., None]
    return speckle(img, rng, 0.03, 0.6)


TEXTURES.update({
    "Wallpaper": wallpaper, "PaintCream": paint_cream, "KitchenTile": kitchen_tile, "Carpet": carpet,
    "PaintBlue": lambda r: paint(r, [0.30, 0.38, 0.43], under=(0.50, 0.49, 0.44), chip=0.74),
})


def wallpaper_blue(rng):
    """Small regular motif that reads as wallpaper when tiled: pinstripes and little diamonds, evenly faded."""
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.36, 0.43, 0.50])
    img[(X % 32) < 2] *= 0.86
    gx, gy = (X % 32) - 16, (Y % 32) - 16
    sx, sy = ((X + 16) % 32) - 16, ((Y + 16) % 32) - 16
    dia = (np.abs(gx) + np.abs(gy)) < 5
    dot = (sx ** 2 + sy ** 2) < 4
    img[dia] = img[dia] * 0.72 + col([0.17, 0.16, 0.12])[0, 0]
    img[dot] *= 0.75
    img *= (0.9 + 0.14 * fbm(rng, 32, 3))[..., None]
    img *= (0.9 + 0.12 * fbm(rng, 8, 2))[..., None]   # gentle, evenly spread fading (no big blotches)
    return img


TEXTURES["WallpaperBlue"] = wallpaper_blue


FITTED = {"PosterNoir", "PosterJazz", "Window", "Door", "Rug", "Mirror", "Screen"}


def save(name, arr, folder):
    arr = np.clip(arr, 0, 1)
    h, w = arr.shape[:2]
    full = "T_Apt_" + name
    img = bpy.data.images.get(full)
    if img: bpy.data.images.remove(img)
    img = bpy.data.images.new(full, w, h, alpha=False)
    rgba = np.dstack([arr, np.ones((h, w))])[::-1]
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(folder, full + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def make_all(folder):
    os.makedirs(folder, exist_ok=True)
    out = {}
    for i, (name, fn) in enumerate(TEXTURES.items()):
        out[name] = save(name, fn(np.random.default_rng(1000 + i)), folder)
    return out
