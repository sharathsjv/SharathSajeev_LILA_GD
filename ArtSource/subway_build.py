# The subway ride screen: a cross-section of a subway car in its tunnel, seen side-on (the doc's "Transition screen
# when taking subway" diagram: doors at both ends, a bench of yellow seats in the middle with a partition and pole at
# its end, a grab bar overhead). Stainless steel, yellow seats and a blue speckled floor, after the doc's references.
# Exports Assets/Hitman/Art/Props/SubwayCar.fbx with two objects: SubwayCar (sways) and SubwayTunnel (stays put).
# Blender axes: X along the car, Y away from the camera (the cut is at Y = 0), Z up.
import sys, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import numpy as np
import apt_textures, street_textures, street_kit, shop_textures
for m in (apt_textures, street_textures, street_kit): importlib.reload(m)
importlib.reload(shop_textures)
import street_textures as ST
from apt_textures import N, fbm, vnoise, col
from street_kit import *


# ---------------------------------------------------------------- textures (T_Shk_Sub*)

def stainless(rng):
    """Brushed stainless panels with vertical seams and rivet lines."""
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.70, 0.73, 0.76])
    brush = vnoise(N, 64, 2, rng)
    img *= (0.90 + 0.12 * brush + 0.04 * fbm(rng, 8, 3))[..., None]
    img[(X % 128) < 2] *= 0.62
    img[((X % 128) == 6) & ((Y % 16) < 3)] *= 0.75
    return np.clip(img, 0, 1)


def seat(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.97, 0.78, 0.14])
    img *= (0.92 + 0.10 * (1 - Y / N))[..., None]                               # moulded, lighter at the top
    img *= (0.96 + 0.05 * fbm(rng, 6, 2))[..., None]
    return np.clip(img, 0, 1)


def floor_blue(rng):
    img = np.ones((N, N, 3)) * col([0.10, 0.14, 0.40])
    img *= (0.92 + 0.12 * fbm(rng, 8, 3))[..., None]
    s = rng.random((N, N))
    img[s > 0.985] = [0.80, 0.82, 0.86]
    img[(s > 0.965) & (s <= 0.985)] = [0.30, 0.38, 0.70]
    img[(s < 0.012)] = [0.05, 0.06, 0.14]
    return img


def ceiling(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.86, 0.87, 0.86])
    img[(X % 128) < 2] *= 0.8
    return img * (0.97 + 0.04 * fbm(rng, 8, 2))[..., None]


def tunnel(rng):
    Y, X = np.mgrid[0:N, 0:N]
    img = np.ones((N, N, 3)) * col([0.13, 0.13, 0.14])
    img *= (0.75 + 0.45 * fbm(rng, 6, 4))[..., None]
    img[(X % 128) < 3] *= 0.6                                                    # ribs
    drip = vnoise(N, 4, 2, rng)
    img *= (0.85 + 0.2 * drip)[..., None]
    return np.clip(img, 0, 1)


def ad(rng, text, bg, fg):
    img = ST.sign(rng, text, bg, fg)
    return img


NEW = {
    "SubSteel": stainless,
    "SubSeat": seat,
    "SubFloor": floor_blue,
    "SubCeiling": ceiling,
    "SubLight": lambda r: np.ones((N, N, 3)) * col([1.0, 0.98, 0.92]),
    "SubSection": lambda r: np.ones((N, N, 3)) * col([0.07, 0.07, 0.08]) * (0.95 + 0.1 * fbm(r, 8, 2))[..., None],
    "SubUnder": lambda r: A_metal(r),
    "SubTunnel": tunnel,
    "SubRubber": lambda r: np.ones((N, N, 3)) * col([0.12, 0.12, 0.13]),
    "SubChrome": lambda r: ST.chrome(r),
    "SubAd1": lambda r: ad(r, "SCHRUTE FARMS", [0.18, 0.42, 0.16], [0.98, 0.88, 0.30]),
    "SubAd2": lambda r: ad(r, "MOP KING", [0.10, 0.30, 0.70], [0.98, 0.98, 0.95]),
    "SubAd3": lambda r: ad(r, "SHOP N SAVE", [0.70, 0.08, 0.08], [0.98, 0.85, 0.20]),
    "SubMap": lambda r: ad(r, "LINE 7", [0.92, 0.92, 0.90], [0.10, 0.10, 0.12]),
}


def A_metal(r):
    return apt_textures.metal(r, [0.20, 0.20, 0.21], 0.8)


FITTED_NEW = {"SubAd1", "SubAd2", "SubAd3", "SubMap"}
ST.TEXTURES.update(NEW)
ST.FITTED.update(FITTED_NEW)


def make_textures():
    for i, (name, fn) in enumerate(NEW.items()):
        ST.save(name, fn(np.random.default_rng(5000 + i)), TEX)


# ---------------------------------------------------------------- the car

L = 4.85            # half length of the car shown
D = 1.6             # depth of the back half (cut at Y = 0 to the back wall)
H = 2.35            # floor to ceiling
DOOR = (3.0, 4.4)   # door opening, |X|
BENCH = 1.8         # bench half length (6 seats)


def wall_with_holes(B, y0, y1, x0, x1, z0, z1, holes, m="SubSteel"):
    """A wall slab in X/Z with rectangular openings (x0, x1, z0, z1): built as columns between the holes."""
    xs = sorted({x0, x1, *[h[0] for h in holes], *[h[1] for h in holes]})
    for a, b in zip(xs, xs[1:]):
        mid = (a + b) / 2
        cuts = sorted([h for h in holes if h[0] <= mid <= h[1]], key=lambda h: h[2])
        z = z0
        for h in cuts:
            if h[2] > z: B.box(a, b, y0, y1, z, h[2], m)
            z = max(z, h[3])
        if z < z1: B.box(a, b, y0, y1, z, z1, m)


def car():
    B = Builder("SubwayCar", (0, 0, 0))
    # floor and its cut face, roof, end walls (the section through the car body is dark, like the diagram's lines)
    B.box(-L, L, 0, D, -0.06, 0, "SubFloor")
    B.box(-L - 0.1, L + 0.1, -0.03, 0.0, -0.32, 0.0, "SubSection")
    B.box(-L - 0.1, L + 0.1, 0.0, D + 0.1, -0.32, -0.06, "SubUnder")
    B.box(-L, L, 0, D, H, H + 0.06, "SubCeiling")
    B.box(-L - 0.1, L + 0.1, -0.03, D + 0.1, H + 0.06, H + 0.3, "SubSection")
    for s in (-1, 1):
        B.box(s * L, s * (L + 0.1), -0.03, D + 0.1, -0.32, H + 0.3, "SubSection")
        # roof shoulders
        B.box(s * (L + 0.1), s * (L + 0.1) - s * 0.3, -0.03, D + 0.1, H + 0.3, H + 0.38, "SubSection")
    # ceiling light strip and the ad frames along the top of the back wall
    B.box(-L + 0.3, L - 0.3, 0.75, 1.05, H - 0.04, H, "SubLight")
    # back wall: windows over the bench and beside the doors, door openings
    win = [(-1.75, -0.65, 1.02, 1.8), (-0.55, 0.55, 1.02, 1.8), (0.65, 1.75, 1.02, 1.8)]
    side_win = [(-2.75, -2.1, 1.02, 1.8), (2.1, 2.75, 1.02, 1.8)]
    doors = [(-DOOR[1], -DOOR[0], 0.0, 2.05), (DOOR[0], DOOR[1], 0.0, 2.05)]
    wall_with_holes(B, D, D + 0.1, -L, L, 0, H, win + side_win + doors)
    for (a, b, c, d) in win + side_win:                                        # window frames
        B.box(a - 0.04, b + 0.04, D - 0.03, D, c - 0.04, c, "SubRubber")
        B.box(a - 0.04, b + 0.04, D - 0.03, D, d, d + 0.04, "SubRubber")
        B.box(a - 0.04, a, D - 0.03, D, c, d, "SubRubber")
        B.box(b, b + 0.04, D - 0.03, D, c, d, "SubRubber")
    # ads above the windows
    for (a, b), m in zip(((-1.75, -0.65), (-0.55, 0.55), (0.65, 1.75)), ("SubAd1", "SubAd2", "SubAd3")):
        B.box(a, b, D - 0.02, D, 1.88, 1.88 + (b - a) / 4, m)
    B.box(-2.75, -2.1, D - 0.02, D, 1.88, 1.88 + 0.65 / 4, "SubMap")
    # doors: two leaves each, each with a tall window, shut in the opening
    for s in (-1, 1):
        a, b = sorted((s * DOOR[0], s * DOOR[1]))
        mid = (a + b) / 2
        for (x0, x1) in ((a, mid - 0.01), (mid + 0.01, b)):
            wall_with_holes(B, D + 0.02, D + 0.07, x0, x1, 0.0, 2.05, [(x0 + 0.12, x1 - 0.12, 1.0, 1.8)])
            B.box(x0 + 0.1, x1 - 0.1, D + 0.01, D + 0.02, 0.98, 1.0, "SubRubber")
            B.box(x0 + 0.1, x1 - 0.1, D + 0.01, D + 0.02, 1.8, 1.82, "SubRubber")
        B.box(mid - 0.01, mid + 0.01, D + 0.0, D + 0.08, 0.0, 2.05, "SubRubber")            # the gap
        B.box(a - 0.05, b + 0.05, D - 0.04, D, 2.05, 2.12, "SubSteel")                       # header
        B.box(a, b, D - 0.22, D, -0.005, 0.002, "SubSeat")                                    # yellow threshold strip
    # the bench: steel base, six yellow moulded seats
    B.box(-BENCH, BENCH, 1.05, D, 0.0, 0.40, "SubSteel")
    for i in range(6):
        x0 = -BENCH + i * 0.6 + 0.02
        x1 = x0 + 0.56
        B.box(x0, x1, 1.0, D - 0.06, 0.40, 0.47, "SubSeat")
        B.box(x0, x1, D - 0.1, D - 0.02, 0.47, 1.0, "SubSeat", rot=(-0.12, 0, 0), pivot=((x0 + x1) / 2, D - 0.06, 0.47))
    # partition and pole at the left end of the bench (the diagram's orange panel), a pole at the right end too
    B.box(-BENCH - 0.12, -BENCH - 0.06, 0.95, D, 0.05, 1.75, "SubSteel")
    B.cyl(-BENCH - 0.09, 0.92, 0.0, 0.025, H, "SubChrome", seg=10)
    B.cyl(BENCH + 0.09, 0.92, 0.0, 0.025, H, "SubChrome", seg=10)
    # overhead grab bar with angled ends, and hanging straps
    zb, yb = 1.98, 0.62
    bar = 2.6
    B.cyl(-bar, yb, zb, 0.02, 2 * bar, "SubChrome", seg=8, rot=(0, np.pi / 2, 0))
    B.cyl(-bar, yb, zb, 0.02, 0.5, "SubChrome", seg=8, rot=(0, -0.7, 0))
    B.cyl(bar, yb, zb, 0.02, 0.5, "SubChrome", seg=8, rot=(0, 0.7, 0))
    for x in (-1.2, -0.2, 0.9, 2.1):
        B.box(x - 0.02, x + 0.02, yb - 0.015, yb + 0.015, zb - 0.32, zb, "SubSeat")
        B.box(x - 0.07, x + 0.07, yb - 0.015, yb + 0.015, zb - 0.42, zb - 0.32, "SubSeat")
    # undercarriage, bogies and wheels
    B.box(-L + 0.3, L - 0.3, 0.25, D, -0.62, -0.32, "SubUnder")
    for s in (-1, 1):
        B.box(s * 3.4 - 0.9, s * 3.4 + 0.9, 0.15, D, -0.85, -0.62, "SubUnder")
        for x in (s * 3.4 - 0.55, s * 3.4 + 0.55):
            B.cyl(x, 0.12, -0.88, 0.3, 0.12, "SubRubber", seg=14, rot=(np.pi / 2, 0, 0))
    B.done()


def tunnel_set():
    B = Builder("SubwayTunnel", (0, 0, 0))
    B.box(-9, 9, 3.4, 3.6, -2.6, 5.0, "SubTunnel")                              # back wall of the tunnel
    B.box(-9, 9, -2.0, 3.6, -1.6, -1.3, "SubTunnel")                            # trackbed
    for x in np.arange(-9, 9, 0.7):                                             # sleepers
        B.box(x, x + 0.25, -0.2, 2.0, -1.3, -1.22, "SubRubber")
    B.box(-9, 9, 0.0, 0.1, -1.22, -1.12, "SubUnder")                            # rail
    B.box(-9, 9, 1.6, 1.7, -1.22, -1.12, "SubUnder")
    for z in (3.0, 3.15, 3.3):                                                  # cable runs
        B.box(-9, 9, 3.3, 3.4, z, z + 0.05, "SubRubber")
    B.box(-9, 9, 3.2, 3.4, -0.2, 0.0, "SubSection")                              # walkway ledge
    B.done()


def build():
    reset(91)
    make_textures()
    car()
    tunnel_set()
    export(PROJECT + "/Assets/Hitman/Art/Props/SubwayCar.fbx", HERE + "/SubwayCar.blend")


if __name__ == "__main__":
    build()
