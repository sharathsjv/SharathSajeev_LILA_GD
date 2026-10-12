# Builds the Business District in the clean style of its reference (GTA III-era commercial district): pale
# limestone art-deco towers with stone piers and punched windows, glass towers behind, clean light streets with
# green verges and street trees, and a lot of taxis.
# Footprints, doors and gameplay spots come from business_layout.json (exported from the game scene).
# Axes: Blender X = Unity X, Blender Y = Unity Z (north). Naming as in shackleton_build.py.
import sys, json, math, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit
for m in (apt_textures, street_textures, street_kit): importlib.reload(m)
from street_kit import *

reset(21)
LAYOUT = {it["name"]: it for it in json.load(open(HERE + "/business_layout.json"))["items"]}
door_z = lambda n: LAYOUT[n]["pos"][2]
LAMP_SPACING = 10.0
CROSS = (1.0, 6.0)           # the east-west road crossing the avenue (Z span)
SPUR = (-15.0, -10.0)        # the side road off the east sidewalk
KIOSK = (-17.6, -14.4)       # the subway entrance on the west sidewalk

# ---------------------------------------------------------------- ground: clean paving, light asphalt, green verges

single("Gnd_Plaza", (-26.5, 26.5, -27, 27, -0.08, -0.01, "ConcreteClean", 2.0))
single("Gnd_Road", (-3, 3, -27, 27, -0.05, 0.0, "AsphaltClean", 2.0))
single("Gnd_CrossRoad", (-26.5, 26.5, CROSS[0], CROSS[1], -0.05, 0.0, "AsphaltClean", 2.0))
single("Gnd_SpurRoad", (3, 26.5, SPUR[0], SPUR[1], -0.05, 0.0, "AsphaltClean", 2.0))
def gaps(s):
    """Stretches of the avenue's curb, minus the road crossings."""
    out = [(-27, CROSS[0])] if s < 0 else [(-27, SPUR[0]), (SPUR[1], CROSS[0])]
    return out + [(CROSS[1], 27)]


for s in (-1, 1):                                     # sidewalks stop at each road crossing them
    side = "E" if s > 0 else "W"
    xa, xb = sorted((s * 3, s * 6.5))
    for k, (ya, yb) in enumerate(gaps(s)):
        single(f"Gnd_Sidewalk{side}{k}", (xa, xb, ya, yb, -0.05, 0.004, "ConcreteClean", 2.0))
verge = Builder("Gnd_Verges")
curbs = Builder("NoCol_Curbs")
for s in (-1, 1):
    for (ya, yb) in gaps(s):
        curbs.box(s * 3 - 0.1, s * 3 + 0.1, ya, yb, 0, 0.07, "CurbClean")
        # grass strips with breaks every 6 m to walk through
        y = ya + 0.5
        while y < yb - 1.0:
            y2 = min(y + 4.5, yb - 0.5)
            xa, xb = sorted((s * 3.15, s * 3.95))
            verge.box(xa, xb, y, y2, -0.05, 0.03, "GrassLush", 1.5)
            y = y2 + 1.5
verge.done()
curbs.done()

paint = Builder("NoCol_RoadPaint")
for (ya, yb) in ((-27, CROSS[0] - 1.5), (CROSS[1] + 1.5, 27)):
    for dx in (-0.12, 0.12):
        paint.box(dx - 0.05, dx + 0.05, ya, yb, 0, 0.004, "LineYellow")                      # double yellow
    for s in (-1, 1):
        xa, xb = sorted((s * 2.6, s * 2.72))
        paint.box(xa, xb, ya, yb, 0, 0.004, "LineWhite")                                        # edge lines
for k in range(6):                                                                               # zebra crossings
    x = -2.5 + k * 1.0
    paint.box(x - 0.25, x + 0.25, CROSS[0] - 1.3, CROSS[0] - 0.3, 0, 0.004, "LineWhite")
    paint.box(x - 0.25, x + 0.25, CROSS[1] + 0.3, CROSS[1] + 1.3, 0, 0.004, "LineWhite")
for x0 in (-26, 3.5):
    k = x0
    while k < x0 + 22:
        paint.box(k, k + 1.5, 3.44, 3.56, 0, 0.004, "LineWhite")
        k += 3.0
paint.done()

decals = Builder("NoCol_Decals")                       # a few newspapers blowing about, nothing worse
for (x, y) in [(-5.8, -10.0), (5.6, 18.0), (-4.2, 22.5)]:
    decals.box(x - 0.4, x + 0.4, y - 0.4, y + 0.4, 0.005, 0.008, "Litter")
decals.done()

# ---------------------------------------------------------------- buildings

def deco_block(B, lit, x0, x1, y0, y1, z0, floors, stone, fh=3.4, skip=None):
    """One block of an art-deco tower: stone with dark piers and punched windows on every side."""
    top = z0 + fh * floors
    B.box(x0, x1, y0, y1, z0, top, stone)
    for F in [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]:
        L = F.a1 - F.a0
        bays = max(2, int(L // 1.7))
        w = L / bays
        for k in range(bays + 1):
            a = F.a0 + k * w
            F.box(B, a - 0.16, a + 0.16, z0, top, 0, 0.22, "LimestoneDark")
        for f in range(floors):
            z = z0 + f * fh
            for k in range(bays):
                c = F.a0 + (k + 0.5) * w
                kind = "WinPunchLit" if rng.random() < 0.25 else "WinPunch"
                F.box(lit if kind == "WinPunchLit" else B, c - w / 2 + 0.25, c + w / 2 - 0.25, z + 0.7, z + 2.8, 0, 0.04, kind)
    return top


def ledge_planting(name, x0, x1, y0, y1, z):
    """Hedges and little trees along the edge of a setback (the reference's terraces)."""
    P = Builder(name, ((x0 + x1) / 2, (y0 + y1) / 2, z))
    for (a0, a1, b0, b1) in ((x0, x1, y0, y0 + 0.6), (x0, x1, y1 - 0.6, y1), (x0, x0 + 0.6, y0, y1), (x1 - 0.6, x1, y0, y1)):
        P.box(a0, a1, b0, b1, z, z + 0.5, "LimestoneDark").box(a0 + 0.05, a1 - 0.05, b0 + 0.05, b1 - 0.05, z + 0.5, z + 1.1, "Hedge")
    for k in range(6):
        t = (k + 0.5) / 6
        for (x, y) in ((x0 + 0.3 + t * (x1 - x0 - 0.6), y0 + 0.3), (x0 + 0.3 + t * (x1 - x0 - 0.6), y1 - 0.3)):
            P.blob(x, y, z + 1.0, 0.6, 0.6, 0.9, "LeavesBright", lumpy=0.3)
    return P.done()


def schrute_tower(key, src, street, door, sign):
    """Schrute Towers, after its reference: a limestone art-deco tower that steps back as it rises, with planted
    terraces on the setbacks, and a gilded, flag-hung entrance at street level."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    base = 6.0
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    lit = Builder("LightSrc_Windows_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    # the podium (a tall lobby storey), then three blocks, each stepping back, with a crown
    B.box(x0, x1, y0, y1, 0, base, "Limestone")
    B.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, base - 0.5, base, "LimestoneDark")
    z = deco_block(B, lit, x0, x1, y0, y1, base, 8, "Limestone")
    ledge_planting("NoCol_Terrace1_" + key, x0 + 0.1, x1 - 0.1, y0 + 0.1, y1 - 0.1, z)
    z = deco_block(B, lit, x0 + 1.5, x1 - 1.5, y0 + 1.5, y1 - 1.5, z, 6, "Limestone")
    ledge_planting("NoCol_Terrace2_" + key, x0 + 1.6, x1 - 1.6, y0 + 1.6, y1 - 1.6, z)
    z = deco_block(B, lit, x0 + 3.2, x1 - 3.2, y0 + 3.2, y1 - 3.2, z, 5, "Limestone")
    for k, inset in enumerate((4.2, 5.0, 5.7)):                                             # stepped crown
        B.box(x0 + inset, x1 - inset, y0 + inset, y1 - inset, z + k * 2.6, z + (k + 1) * 2.6, "Limestone" if k < 2 else "LimestoneDark")
        B.box(x0 + inset - 0.1, x1 - inset + 0.1, y0 + inset - 0.1, y1 - inset + 0.1, z + (k + 1) * 2.6 - 0.25, z + (k + 1) * 2.6, "Brass")
    B.cyl((x0 + x1) / 2, (y0 + y1) / 2, z + 7.8, 0.18, 6.0, "Brass", seg=6)                # gilded mast
    top = z
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    F = pick_face(faces, street)
    c = door if door is not None else (F.a0 + F.a1) / 2
    gold = Builder("Deco_Gold_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    L = F.a1 - F.a0
    bays = max(2, int(L // 1.7))
    w = L / bays
    F.box(B, F.a0 + 0.6, F.a1 - 0.6, 0.0, 4.6, 0, 0.05, "GlassOffice")                        # lobby glazing
    for k in range(bays + 1):
        a = F.a0 + k * w
        if abs(a - c) < 1.3: continue                                                         # (the doorway stays clear)
        F.box(B, a - 0.24, a + 0.24, 0, base - 0.5, 0, 0.32, "Limestone")                     # lobby piers
        F.box(gold, a - 0.26, a + 0.26, 0, 0.35, 0, 0.36, "Brass")                            # bronze pier feet
    F.box(gold, F.a0 + 0.3, F.a1 - 0.3, 4.6, 4.85, 0, 0.42, "Brass")                         # gilded band over the lobby
    for k in range(bays):                                                                     # bronze transoms in the lobby glass
        a = F.a0 + (k + 0.5) * w
        F.box(gold, a - w / 2 + 0.3, a + w / 2 - 0.3, 3.3, 3.42, 0.04, 0.1, "Brass")
    # the entrance portal: a deep bronze frame, a glass canopy, the name above
    F.box(gold, c - 1.35, c - 1.1, 0, 3.6, 0, 0.5, "Brass")
    F.box(gold, c + 1.1, c + 1.35, 0, 3.6, 0, 0.5, "Brass")
    F.box(gold, c - 1.35, c + 1.35, 3.4, 3.65, 0, 0.5, "Brass")
    F.box(gold, c - 1.7, c + 1.7, 3.65, 3.8, 0, 1.4, "Brass")                                 # canopy
    gold.done()
    if sign:
        neon = Builder(f"LightSrc_Neon{sign[4:]}_{key}", ((x0 + x1) / 2, (y0 + y1) / 2, 0))
        F.box(neon, c - 1.8, c + 1.8, 4.95, 5.45, 0.3, 0.36, sign)
        neon.done()
    for k, cc in enumerate((c - 1.9, c + 1.9)):                                              # bronze lanterns by the door
        wall_bulb(f"LightSrc_Bulb_{key}{k}", F, cc, 2.8)
    # flags on poles out over the sidewalk, as in the reference
    fl = Builder("NoCol_Flags_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for k, (cc, cloth) in enumerate(((c - 3.2, "FlagBlue"), (c + 3.2, "FlagRed"))):
        for j in range(8):                                                                    # pole angled up and out
            d0, d1 = 0.1 + j * 0.22, 0.32 + j * 0.22
            F.box(fl, cc - 0.04, cc + 0.04, 5.2 + j * 0.13, 5.32 + j * 0.13, d0, d1, "Brass")
        F.box(fl, cc - 0.02, cc + 0.02, 4.0, 6.1, 0.9, 1.75, cloth)                          # the flag, hanging
    fl.done()
    # stone planters between the piers either side of the door (kept against the wall: the sidewalk stays clear)
    pl = Builder("Prop_Planters_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for (a0, a1) in ((F.a0 + 0.8, c - 2.4), (c + 2.4, F.a1 - 0.8)):
        if a1 - a0 < 1.0: continue
        F.box(pl, a0, a1, 0, 0.55, 0.32, 0.85, "LimestoneDark")
        F.box(pl, a0 + 0.06, a1 - 0.06, 0.55, 1.0, 0.36, 0.8, "Hedge")
    pl.done()
    for F2 in faces:
        if F2 is F: continue
        for k in range(max(2, int((F2.a1 - F2.a0) // 1.7))):
            ww = (F2.a1 - F2.a0) / max(2, int((F2.a1 - F2.a0) // 1.7))
            cc = F2.a0 + (k + 0.5) * ww
            F2.box(B, cc - ww / 2 + 0.3, cc + ww / 2 - 0.3, 1.2, 4.4, 0, 0.04, "WinPunch")
    lit.done()
    return B.done(), top


def dept_store(key, src, street, door, sign):
    """The mall, after its reference (a suburban department store): a big windowless box in cream stucco with
    horizontal grooves, a tall pylon by the entrance, a colonnaded canopy over the doors, big navy lettering
    and a planted strip with young trees along the front."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    H = 11.0
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, H, "StuccoCream")
    B.box(x0 - 0.25, x1 + 0.25, y0 - 0.25, y1 + 0.25, H - 0.7, H, "Limestone")              # cornice
    B.box(x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, H - 1.1, H - 0.95, "Limestone")
    for zz in (6.6, 7.2):                                                                       # the long grooves
        B.box(x0 - 0.06, x1 + 0.06, y0 - 0.06, y1 + 0.06, zz, zz + 0.12, "LimestoneDark")
    B.box(x0 + 2.0, x1 - 2.0, y0 + 2.0, y1 - 2.0, H, H + 1.6, "Concrete")                     # roof plant
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    F = pick_face(faces, street)
    c = door if door is not None else (F.a0 + F.a1) / 2
    # the pylon beside the entrance, taller than the store
    pa = min(F.a1 - 1.2, c + 4.6)
    F.box(B, pa - 0.9, pa + 0.9, 0, H + 4.0, 0, 0.9, "Limestone")
    F.box(B, pa - 1.0, pa + 1.0, H + 3.6, H + 4.0, 0, 1.0, "LimestoneDark")
    for zz in range(4):
        F.box(B, pa - 0.08, pa + 0.08, 1.0 + zz * 3.4, 3.4 + zz * 3.4, 0.9, 0.96, "LimestoneDark")
    # entrance: glass doors, a deep canopy on columns
    F.box(B, c - 1.8, c + 1.8, 0, 3.2, 0, 0.05, "GlassOffice")
    F.box(B, c - 1.9, c + 1.9, 3.2, 3.4, 0, 0.12, "Steel")
    F.box(B, c - 3.6, c + 3.6, 3.6, 4.1, 0, 1.45, "Limestone")                               # canopy slab
    F.box(B, c - 3.65, c + 3.65, 4.1, 4.3, 0, 1.5, "LimestoneDark")
    col = Builder("Prop_Colonnade_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for cc in (c - 3.3, c - 1.6, c + 1.6, c + 3.3):
        if F.axis == "x": col.cyl(F.fixed + F.s * 1.2, cc, 0, 0.2, 3.6, "Limestone", seg=10)
        else: col.cyl(cc, F.fixed + F.s * 1.2, 0, 0.2, 3.6, "Limestone", seg=10)
    col.done()
    # the name, big, up on the wall
    neon = Builder(f"LightSrc_Neon{sign[4:]}_{key}", ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    F.box(neon, c - 3.6, c + 3.6, 7.6, 10.0, 0.06, 0.12, sign)
    neon.done()
    for k, cc in enumerate((c - 2.6, c + 2.6)):
        wall_bulb(f"LightSrc_Bulb_{key}{k}", F, cc, 3.0)
    # a planted strip against the wall either side of the entrance, with young trees
    pl = Builder("Prop_Planters_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for (a0, a1) in ((F.a0 + 0.4, c - 4.0), (c + 4.0, pa - 1.2), (pa + 1.2, F.a1 - 0.4)):
        if a1 - a0 < 1.0: continue
        F.box(pl, a0, a1, 0, 0.18, 0, 0.75, "LimestoneDark")
        F.box(pl, a0 + 0.05, a1 - 0.05, 0.18, 0.5, 0.05, 0.7, "Hedge")
    pl.done()
    for k, aa in enumerate(t for t in (F.a0 + 1.6, c - 5.5, c + 6.8, F.a1 - 1.6) if F.a0 + 1 < t < F.a1 - 1 and abs(t - pa) > 1.6):
        tx, ty = (F.fixed + F.s * 0.4, aa) if F.axis == "x" else (aa, F.fixed + F.s * 0.4)
        T = Builder(f"Prop_StoreTree{k}_{key}", (tx, ty, 0))
        T.cyl(tx, ty, 0.5, 0.06, 2.2, "Bark", seg=5)
        T.blob(tx, ty, 2.4, 0.55, 0.55, 0.9, "LeavesBright", lumpy=0.3)
        T.done()
    return B.done(), H

schrute_tower("Tower1", LAYOUT["Tower 1"], +1, door_z("Door (Tower 1)"), "SignSchrute")             # Schrute Towers
deco_tower("Tower3", LAYOUT["Tower 3"], 12, -1)
office("Tower2", LAYOUT["Tower 2"], 9, -1, base="LimestoneDark")      # the glass tower, further back
dept_store("Mall", LAYOUT["Mall"], -1, door_z("Door (Mall)"), "SignMall")
building("Apartments", LAYOUT["Apartments"], 7, "BrickClean", +1, door=13.0, clean=True,
         roof=[(-14.0, 20.0, "tank"), (-10.0, 15.0, "ac")])
building("CoffeeShop", LAYOUT["Coffee Shop"], 1, "StuccoCream", +1, shop={"sign": "SignCoffee", "awning": "Awning"},
         door=door_z("Door (Coffee Shop)"), clean=True)
building("CornerBar", LAYOUT["Corner Bar"], 2, "BrickClean", +1, shop={"sign": "SignBar", "glass": "WinPunch"},
         door=door_z("Door (Corner Bar)"), clean=True)

door_obj("Tower1", -6.5, +1, door_z("Door (Tower 1)"), kind="GlassOffice")
door_obj("Mall", 6.5, -1, door_z("Door (Mall)"), kind="GlassOffice")
door_obj("CoffeeShop", -6.5, +1, door_z("Door (Coffee Shop)"))
door_obj("CornerBar", -6.5, +1, door_z("Door (Corner Bar)"))
door_obj("Apartments", -6.5, +1, 13.0)            # Oscar goes home through here (not enterable by the player)

skyline("Gnd_Skyline", (-26.5, 26.5, -27, 27))

# ---------------------------------------------------------------- street furniture

subway_kiosk("Station_Stairs", -4.75, -16.0, along_y=True)
taxi_sign(6.1, -3.0)

n = 0
for s_ in (-1, 1):                                    # lamps in the verges, from the junction outwards
    for start, step in ((CROSS[1] + 0.8, LAMP_SPACING), (CROSS[0] - 0.8, -LAMP_SPACING)):
        y = start
        while -26.5 <= y <= 26.5:
            if not (s_ > 0 and SPUR[0] - 0.5 < y < SPUR[1] + 0.5):
                n += 1
                lamp(f"Prop_Lamp{n:02d}", s_ * 3.55, y, (-s_, 0), 0.0)
            y += step
k = 0
for s_ in (-1, 1):                                    # street trees in the verges, between the lamps
    for y in (-23.0, -18.0, -4.0, 11.5, 16.5, 21.5):
        if s_ > 0 and SPUR[0] - 1 < y < SPUR[1] + 1: continue
        k += 1
        round_tree(f"Prop_Tree{k:02d}", s_ * 3.55, y, h=5.5, r=1.25)
for i, (x, y) in enumerate([(-5.9, -3.0), (5.9, 14.0), (-5.9, 19.5), (5.9, -21.0)]):
    bench(f"Prop_Bench{i}", x, y)
for i, (x, y, f) in enumerate([(-3.3, 0.7, (0, -1)), (3.3, 0.7, (0, -1)), (-3.3, 6.3, (0, 1)), (3.3, 6.3, (0, 1))]):
    traffic_light(f"Prop_TrafficLight{i}", x, y, f)
for i, (x, y) in enumerate([(3.4, -20.0), (-3.4, 9.0)]):
    B = Builder(f"Prop_Hydrant{i}", (x, y, 0))
    B.cyl(x, y, 0, 0.13, 0.55, "CarRed", seg=8).cyl(x, y, 0.55, 0.15, 0.15, "Chrome", seg=8, r2=0.08)
    B.done()
for i, y in enumerate((-1.8, -1.2)):
    B = Builder(f"Prop_Newsbox{i}", (6.1, y, 0))
    B.box(5.85, 6.35, y - 0.22, y + 0.22, 0.3, 1.05, "Payphone" if i else "CarTeal")
    B.box(5.9, 6.3, y - 0.18, y + 0.18, 0, 0.3, "Steel")
    B.done()
pp = Builder("Payphone_1", (-6.2, 3.0, 0))
pp.box(-6.45, -6.05, 2.7, 3.3, 0.6, 2.0, "Payphone").box(-6.45, -6.3, 2.65, 3.35, 0, 2.2, "Steel").done()

export(PROJECT + "/Assets/Hitman/Art/Business/BusinessDistrict.fbx", HERE + "/BusinessDistrict.blend")
