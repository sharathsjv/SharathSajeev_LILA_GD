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

deco_tower("Tower1", LAYOUT["Tower 1"], 10, +1, door=door_z("Door (Tower 1)"), sign="SignSchrute")   # Schrute Towers
deco_tower("Tower3", LAYOUT["Tower 3"], 12, -1)
office("Tower2", LAYOUT["Tower 2"], 9, -1, base="LimestoneDark")      # the glass tower, further back
office("Mall", LAYOUT["Mall"], 3, -1, base="Limestone", lobby_sign="SignMall", door=door_z("Door (Mall)"))
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
