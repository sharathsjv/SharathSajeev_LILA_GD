# Extra textures for the shops added on 2026-10-11 (Shop n Save supermarket, barber, the mall). Registers them in
# street_textures' library (so street_kit materials find them) and saves only these, leaving the existing images
# untouched. Runs inside Blender (numpy only).
import numpy as np
import street_textures as ST
import apt_textures as A
from apt_textures import N, fbm, vnoise, col, grime

sign = ST.sign


def goods(rng):
    """Shelf face packed with boxes and bottles in bright packaging."""
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.30, 0.31, 0.33])
    for row in range(4):
        y0, y1 = row * 64 + 6, row * 64 + 58
        x = 2
        while x < N - 4:
            w = int(rng.integers(10, 26))
            h = int(rng.integers(26, 52))
            c = np.array(rng.choice([[0.85, 0.15, 0.12], [0.95, 0.75, 0.15], [0.15, 0.45, 0.80], [0.20, 0.65, 0.30],
                                     [0.95, 0.95, 0.92], [0.55, 0.20, 0.55], [0.95, 0.50, 0.10]]))
            m = (Y > y1 - h) & (Y < y1) & (X >= x) & (X < x + w)
            img[m] = c * (0.85 + 0.2 * rng.random())
            img[m & (np.abs(Y - (y1 - h * 0.55)) < 4)] = [0.95, 0.95, 0.92]          # label band
            x += w + int(rng.integers(1, 4))
        img[(Y >= y1) & (Y < y1 + 6)] = [0.42, 0.43, 0.45]                          # the shelf lip
    return img


def produce(rng):
    """Heaped vegetables and fruit seen from above."""
    img = np.ones((N, N, 3)) * col([0.25, 0.18, 0.10])
    Y, X = np.mgrid[0:N, 0:N]
    for k in range(260):
        cx, cy = rng.integers(0, N, 2)
        r = rng.integers(8, 18)
        c = np.array(rng.choice([[0.20, 0.55, 0.15], [0.80, 0.15, 0.10], [0.95, 0.60, 0.10], [0.85, 0.80, 0.20],
                                 [0.45, 0.30, 0.15], [0.35, 0.65, 0.25]]))
        d = (X - cx) ** 2 + (Y - cy) ** 2
        m = d < r * r
        img[m] = c * (0.75 + 0.35 * (1 - d[m] / (r * r)))[:, None]
    return img


def freezer(rng):
    """Upright drinks fridge: glass doors, rows of coloured bottles and cans, lit from inside."""
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.80, 0.88, 0.92])
    for row in range(5):
        y1 = row * 50 + 46
        x = 6
        while x < N - 8:
            w = int(rng.integers(8, 14))
            h = int(rng.integers(22, 40))
            c = rng.choice([[0.85, 0.10, 0.10], [0.10, 0.35, 0.75], [0.15, 0.60, 0.20], [0.95, 0.80, 0.20], [0.30, 0.15, 0.08]])
            img[(Y > y1 - h) & (Y < y1) & (X >= x) & (X < x + w)] = c
            x += w + int(rng.integers(2, 5))
        img[(Y >= y1) & (Y < y1 + 4)] = [0.6, 0.62, 0.64]
    img[(np.abs(X - N // 2) < 4) | (X < 5) | (X > N - 6) | (Y < 5) | (Y > N - 6)] = [0.55, 0.56, 0.58]   # door frames
    refl = np.abs((X + Y * 0.6) % 180 - 60) < 10
    img[refl] = img[refl] * 0.8 + 0.2
    return img


def poster(rng, text, bg, fg, stripe):
    """Big advertising hoarding: bold text over a colour block, with a stripe."""
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col(bg)
    img[(Y > N * 0.66) & (Y < N * 0.78)] = stripe
    m = ST.text_mask(text, N, N // 2, 0.1)
    top = img[N // 6: N // 6 + N // 2]
    top[m] = fg
    img[(X < 6) | (X > N - 7) | (Y < 6) | (Y > N - 7)] = [0.85, 0.85, 0.82]
    return img * (0.92 + 0.1 * vnoise(N, 4, 8, rng))[..., None]


def barber_pole(rng):
    Y, X = np.mgrid[0:N, 0:N]
    k = ((X + Y) // 32) % 3
    img = np.ones((N, N, 3))
    img[k == 0] = [0.80, 0.10, 0.10]
    img[k == 1] = [0.95, 0.95, 0.95]
    img[k == 2] = [0.10, 0.20, 0.60]
    return img


def vinyl(rng, a, b, t=32):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.where((((X // t) + (Y // t)) % 2 == 0)[..., None], col(a), col(b)) * np.ones((N, N, 3))
    return img * (0.92 + 0.1 * fbm(rng, 16, 3))[..., None]


def shelf_grey(rng):
    return A.metal(rng, [0.38, 0.39, 0.41], 0.95)


NEW = {
    "SignBarber": lambda r: sign(r, "BARBER", [0.45, 0.06, 0.06], [0.95, 0.95, 0.92]),
    "SignShopNSave": lambda r: sign(r, "SHOP N SAVE", [0.70, 0.08, 0.08], [0.98, 0.85, 0.20]),
    "SignSuits": lambda r: sign(r, "SUITS", [0.08, 0.10, 0.22], [0.90, 0.78, 0.40]),
    "SignClosed": lambda r: sign(r, "CLOSED", [0.30, 0.30, 0.30], [0.80, 0.78, 0.72]),
    "SignFoodCourt": lambda r: sign(r, "FOOD COURT", [0.90, 0.55, 0.10], [0.15, 0.08, 0.05]),
    "SignNews": lambda r: sign(r, "NEWS", [0.08, 0.08, 0.10], [0.95, 0.95, 0.92]),
    "SignPest": lambda r: sign(r, "PEST CONTROL", [0.20, 0.20, 0.10], [0.95, 0.85, 0.20]),
    "SignPhone": lambda r: sign(r, "PHONE", [0.10, 0.22, 0.58], [0.95, 0.95, 0.95]),
    "PosterCola": lambda r: poster(r, "COLA", [0.75, 0.08, 0.08], [0.98, 0.98, 0.95], [0.98, 0.98, 0.95]),
    "PosterSale": lambda r: poster(r, "SALE", [0.98, 0.85, 0.15], [0.80, 0.10, 0.10], [0.10, 0.10, 0.10]),
    "PosterFly": lambda r: poster(r, "FLY NOW", [0.15, 0.40, 0.75], [0.98, 0.98, 0.95], [0.98, 0.80, 0.20]),
    "FlagRed": lambda r: ST.fabric_clean(r, [0.75, 0.12, 0.10]),
    "FlagBlue": lambda r: ST.fabric_clean(r, [0.12, 0.25, 0.65]),
    "FlagYellow": lambda r: ST.fabric_clean(r, [0.92, 0.75, 0.15]),
    "Goods": goods, "Produce": produce, "Freezer": freezer, "BarberPole": barber_pole, "ShelfGrey": shelf_grey,
    "FloorMall": lambda r: ST.tiles_white(r, 64, (0.84, 0.80, 0.72)),
    "FloorShop": lambda r: vinyl(r, [0.78, 0.78, 0.76], [0.62, 0.64, 0.66]),
    "FloorBarber": lambda r: vinyl(r, [0.90, 0.90, 0.88], [0.12, 0.12, 0.13]),
    "LeatherBlack": lambda r: ST.fabric_clean(r, [0.10, 0.10, 0.11]),
    "BasketRed": lambda r: ST.fabric_clean(r, [0.80, 0.10, 0.10]),
}
FITTED_NEW = {"SignPhone", "SignBarber", "SignShopNSave", "SignSuits", "SignClosed", "SignFoodCourt", "SignNews", "SignPest",
              "PosterCola", "PosterSale", "PosterFly", "Freezer"}

ST.TEXTURES.update(NEW)
ST.FITTED.update(FITTED_NEW)


def make_new(folder):
    for i, (name, fn) in enumerate(NEW.items()):
        ST.save(name, fn(np.random.default_rng(3000 + i)), folder)
    return len(NEW)
