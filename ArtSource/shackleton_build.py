# Builds Shackleton Ave (destitute street under an elevated subway) and exports it to the Unity project.
# Run inside Blender (live via the MCP bridge, or `blender -b --python shackleton_build.py`).
#
# Footprints, doors and gameplay spots come from shackleton_layout.json (exported from the game scene), so the
# street keeps working with the existing routines. Axes: Blender X = Unity X, Blender Y = Unity Z (north).
#
# Names the game relies on:
#   Bldg_*     buildings (made un-walkable; tapping one offers "Cross road")
#   Door_*     doors you can enter (Usable is added in Unity by name)
#   Station_Stairs / Station_Sign  the subway entrance;  TaxiSign  the taxi stand;  Payphone_*  payphones
#   NoCol_* / Graf_*  decoration with no collider (curbs, paint, litter, puddles, graffiti)
#   LightSrc_<Kind>_*  night light sources (Lamp / Bulb / Neon<Sign>): the game puts a baked light at each one
#   Rail_*     the elevated line (overhead parts are ignored by taps)
import sys, json, math, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit, shop_textures
for m in (apt_textures, street_textures, street_kit, shop_textures): importlib.reload(m)
from street_kit import *

reset(11)
LAYOUT = {it["name"]: it for it in json.load(open(HERE + "/shackleton_layout.json"))["items"]}

# ---------------------------------------------------------------- ground

single("Gnd_Lot", (-23.5, 23.5, -28, 28, -0.08, -0.02, "Dirt"))
single("Gnd_Road", (-4, 4, -28, 28, -0.05, 0.0, "Asphalt", 2.0))
single("Gnd_CrossRoad", (-23.5, 23.5, -9, -4, -0.05, 0.0, "Asphalt", 2.0))
for s in (-1, 1):
    side = "E" if s > 0 else "W"
    xa, xb = sorted((s * 4, s * 7.5))
    single(f"Gnd_Sidewalk{side}N", (xa, xb, -4, 28, -0.05, 0.004, "Concrete", 2.0))
    single(f"Gnd_Sidewalk{side}S", (xa, xb, -28, -9, -0.05, 0.004, "Concrete", 2.0))
    xa, xb = sorted((s * 7.5, s * 23.5))
    single(f"Gnd_Sidewalk{side}CrossN", (xa, xb, -4, -3, -0.05, 0.004, "Concrete", 2.0))
    single(f"Gnd_Sidewalk{side}CrossS", (xa, xb, -11, -9, -0.05, 0.004, "Concrete", 2.0))

curbs = Builder("NoCol_Curbs")
for s in (-1, 1):
    for (ya, yb) in ((-28, -9), (-4, 28)):
        curbs.box(s * 4 - 0.1, s * 4 + 0.1, ya, yb, 0, 0.07, "Curb")
    xa, xb = sorted((s * 4, s * 23.5))
    curbs.box(xa, xb, -4.1, -3.9, 0, 0.07, "Curb")
    curbs.box(xa, xb, -9.1, -8.9, 0, 0.07, "Curb")
curbs.done()

paint = Builder("NoCol_RoadPaint")
for (ya, yb) in ((-28, -10.5), (-2.5, 28)):
    for dx in (-0.12, 0.12):
        paint.box(dx - 0.05, dx + 0.05, ya, yb, 0, 0.004, "LineYellow")
for k in range(8):                                                   # crosswalks either side of the junction
    x = -3.5 + k * 1.0
    paint.box(x - 0.25, x + 0.25, -10.4, -9.4, 0, 0.004, "LineWhite")
    paint.box(x - 0.25, x + 0.25, -3.6, -2.6, 0, 0.004, "LineWhite")
for s in (-1, 1):
    for k in range(3):
        y = -8.5 + k * 1.0 + 0.5
        xa, xb = sorted((s * 4.2, s * 7.3))
        paint.box(xa, xb, y - 0.25, y + 0.25, 0, 0.004, "LineWhite")
paint.done()

decals = Builder("NoCol_Decals")
for k in range(14):
    x = rng.choice([rng.uniform(-7.3, -4.3), rng.uniform(4.3, 7.3)])
    y = rng.uniform(-27, 27)
    w = rng.uniform(0.8, 1.6)
    decals.box(x - w / 2, x + w / 2, y - w / 2, y + w / 2, 0.005, 0.008, "Litter")
for k in range(7):
    x = rng.uniform(-3.6, 3.6)
    y = rng.uniform(-27, 27)
    w = rng.uniform(1.0, 2.2)
    decals.box(x - w / 2, x + w / 2, y - w / 2, y + w / 2, 0.002, 0.005, "Puddle")
decals.done()

# ---------------------------------------------------------------- elevated subway over the middle of the road

COLS = [-25.5, -18.5, -11.5, -1.0, 6.0, 13.0, 20.0, 27.0]
DECK = 6.0
rail = Builder("Rail_Columns")
for y in COLS:
    rail.box(-0.4, 0.4, y - 0.4, y + 0.4, 0, 0.45, "Concrete")
    rail.box(-0.22, 0.22, y - 0.22, y + 0.22, 0.45, DECK - 0.4, "Steel")
    rail.box(-0.32, 0.32, y - 0.32, y + 0.32, DECK - 0.75, DECK - 0.4, "Steel")
rail.done()
deck = Builder("Rail_Deck", (0, 0, DECK))
for y in COLS:
    deck.box(-3.3, 3.3, y - 0.22, y + 0.22, DECK - 0.4, DECK, "Steel")                          # bent
    for s in (-1, 1):
        L = 2.2
        deck.box(s * 1.1 - 0.06, s * 1.1 + 0.06, y - 0.06, y + 0.06, DECK - 1.9, DECK - 1.9 + L, "Steel",
                 rot=(0, s * 0.85, 0), pivot=(s * 0.2, y, DECK - 1.9))                          # knee braces
for x in (-1.35, 1.35):
    deck.box(x - 0.15, x + 0.15, -28, 28, DECK, DECK + 0.75, "Steel")                            # girders
    deck.box(x - 0.3, x + 0.3, -28, 28, DECK - 0.03, DECK + 0.03, "Steel")
for k in range(int(56 / 0.6)):
    y = -28 + 0.3 + k * 0.6
    deck.box(-1.7, 1.7, y - 0.11, y + 0.11, DECK + 0.75, DECK + 0.87, "Wood")                    # ties
for x in (-0.82, -0.68, 0.68, 0.82):
    deck.box(x - 0.03, x + 0.03, -28, 28, DECK + 0.87, DECK + 1.0, "Steel")                      # rails
for x in (-1.7, 1.7):
    deck.box(x - 0.04, x + 0.04, -28, 28, DECK + 0.87, DECK + 1.6, "Steel")                      # walkway rail
deck.done()

trash = Builder("Rail_TrashPiles")
for y in COLS[1::2]:
    for k in range(3):
        trash.blob(rng.uniform(-0.5, 0.5), y + rng.uniform(0.4, 1.0), 0, 0.55, 0.5, 0.55, "Wood" if k == 2 else "Steel")
trash.done()

# station: stairs up from the east sidewalk, a landing, a bridge and a platform beside the tracks
STAIR_X = (4.3, 5.7)
RUN0, STEPS, RISE = 0.2, 30, DECK / 30
RUN = 8.0 / STEPS
st = Builder("Station_Stairs", (5.0, 4.0, 0))
for i in range(STEPS):
    st.box(STAIR_X[0], STAIR_X[1], RUN0 + i * RUN, RUN0 + (i + 1) * RUN, (i + 1) * RISE - 0.12, (i + 1) * RISE, "Concrete")
ang = math.atan2(DECK, 8.0)
L = math.hypot(DECK, 8.0)
for x in STAIR_X:
    st.box(x - 0.06, x + 0.06, 4.2 - L / 2, 4.2 + L / 2, DECK / 2 - 0.25, DECK / 2 + 0.25, "Steel",
           rot=(ang, 0, 0), pivot=(x, 4.2, DECK / 2))                                            # stringers
    st.box(x - 0.03, x + 0.03, 4.2 - L / 2, 4.2 + L / 2, DECK / 2 + 0.9, DECK / 2 + 0.95, "Steel",
           rot=(ang, 0, 0), pivot=(x, 4.2, DECK / 2))                                            # handrails
for k, y in enumerate((2.6, 5.4)):
    st.box(4.9, 5.1, y - 0.1, y + 0.1, 0, RISE * ((y - RUN0) / RUN) - 0.1, "Steel")               # props
st.box(4.2, 5.8, 8.2, 10.2, DECK - 0.15, DECK, "Steel")                                           # landing
st.box(4.9, 5.1, 9.1, 9.3, 0, DECK - 0.15, "Steel")
st.done()
plat = Builder("Rail_Platform", (2.4, 8.5, DECK))
plat.box(3.2, 4.2, 8.6, 9.8, DECK - 0.15, DECK, "Steel")                                          # bridge
plat.box(1.75, 3.2, 1.5, 15.5, DECK - 0.15, DECK, "Concrete")                                    # platform
plat.box(3.15, 3.2, 1.5, 8.6, DECK, DECK + 1.0, "Steel")
plat.box(3.15, 3.2, 9.8, 15.5, DECK, DECK + 1.0, "Steel")
for y in (2.5, 6.0, 11.0, 14.5):
    plat.box(3.0, 3.12, y - 0.06, y + 0.06, DECK, DECK + 2.6, "Steel")
plat.box(1.7, 3.4, 1.5, 15.5, DECK + 2.6, DECK + 2.7, "Steel")                                   # canopy
plat.box(3.38, 3.42, 6.5, 10.5, DECK + 2.1, DECK + 2.6, "SignSubway")
plat.done()
single("Station_Sign", (5.92, 6.02, -0.3, -0.2, 0, 2.4, "Steel"), (5.87, 5.92, -0.95, 0.45, 2.35, 2.75, "SignSubway"))

# ---------------------------------------------------------------- buildings (footprints from the game)

# 2026-10-11 doc map: the east side is now Supermarket (Shop n Save) / Barber / Cafe, and home is Apartment 2 (west).
home_door = LAYOUT["Door (Home)"]["pos"][2]
barber_door = LAYOUT["Door (Barber Shop)"]["pos"][2]
super_door = LAYOUT["Door (Supermarket)"]["pos"][2]
bar_door = LAYOUT["Door (Bar)"]["pos"][2]
cafe_door = LAYOUT["Door (Cafe)"]["pos"][2]
building("BarberShop", LAYOUT["Barber Shop"], 3, "BrickRed", -1, shop={"sign": "SignBarber", "glass": "Shopfront"},
         door=barber_door, roof=[(15.5, 5.5, "chimney"), (12.0, 9.5, "ac")])
building("Apartment2", LAYOUT["Apartment 2"], 4, "BrickBrown", +1, fire_escape=True, door=home_door,
         roof=[(-14.0, 8.5, "tank"), (-10.0, 12.5, "chimney")])
building("Restaurant", LAYOUT["Restaurant"], 2, "Stucco", +1, shop={"sign": "SignDiner", "awning": "AwningRed"},
         roof=[(-13.0, 19.0, "ac")])
building("Supermarket", LAYOUT["Supermarket"], 2, "BrickRed", -1, shop={"sign": "SignShopNSave", "awning": "AwningRed"},
         door=super_door)
building("Bar", LAYOUT["Bar"], 2, "BrickBrown", +1, shop={"sign": "SignBar", "glass": "WinDark"}, door=bar_door)
building("Cafe", LAYOUT["Cafe"], 2, "Stucco", -1, shop={"sign": "SignCafe", "awning": "Awning"}, door=cafe_door)
building("GenericW", LAYOUT["Generic Building W"], 3, "Stucco", +1, shop={"sign": "SignLiquor", "glass": "Shutter"},
         roof=[(-15.0, -20.0, "ac"), (-11.0, -14.0, "chimney")])
building("GenericE", LAYOUT["Generic Building E"], 3, "BrickBrown", -1, roof=[(14.0, -16.0, "tank")])

door_obj("Home", -7.5, +1, home_door)
door_obj("Barber", 7.5, -1, barber_door)
door_obj("Supermarket", 7.5, -1, super_door)
door_obj("Bar", -7.5, +1, bar_door, kind="Boarded")
door_obj("Cafe", 7.5, -1, cafe_door)

# ---------------------------------------------------------------- street furniture

LAMP_SPACING = 8.0          # metres between lamp posts (change and rebuild)
JUNCTION = (-9.0, -4.0)     # the cross street's span along the avenue; lamps start at its corners



lamps = []
for s_ in (-1, 1):                       # both sidewalks of the avenue, arms over the road
    k = 0
    y = JUNCTION[1] + 0.5
    while y <= 27.5:
        lamps.append((s_ * 4.45, y, (-s_, 0)))
        y += LAMP_SPACING
    y = JUNCTION[0] - 0.5
    while y >= -27.5:
        lamps.append((s_ * 4.45, y, (-s_, 0)))
        y -= LAMP_SPACING
    x = 4.45 + LAMP_SPACING              # the cross street, outwards from the junction
    while x <= 22:
        lamps.append((s_ * x, JUNCTION[1] + 0.45, (0, -1)))
        lamps.append((s_ * x, JUNCTION[0] - 0.45, (0, 1)))
        x += LAMP_SPACING
STAIRS = (4.0, -0.6, 6.0, 10.6)          # keep clear of the station stairs
n = 0
for (x, y, arm) in lamps:
    if STAIRS[0] < x < STAIRS[2] and STAIRS[1] < y < STAIRS[3]: continue
    n += 1
    broken = n in (4, 11)                # a couple of dead, leaning ones
    lamp(f"Prop_Lamp{n:02d}", x, y, arm, 0.16 if broken else rng.uniform(0.0, 0.06), broken)

piles = Builder("Prop_TrashPiles")
for (x, y) in [(-4.9, -16.9), (4.9, -24.9), (-4.9, 21.1), (4.9, 13.1), (-7.0, -4.6)]:
    for k in range(4):
        m = ["Steel", "Wood", "Concrete", "Steel"][k]
        piles.blob(x + rng.uniform(-0.3, 0.3), y + rng.uniform(-0.4, 0.4), 0, 0.6, 0.5, 0.55, m)
    piles.box(x - 0.25, x + 0.25, y + 0.5, y + 1.0, 0, 0.4, "Wood")
piles.done()



dumpster("Prop_DumpsterW", -6.95, 14.5)
dumpster("Prop_DumpsterE", 6.95, -12.6)

for i, (x, y) in enumerate([(4.4, -15.6), (-4.4, 10.3)]):
    B = Builder(f"Prop_Hydrant{i}", (x, y, 0))
    B.cyl(x, y, 0, 0.13, 0.55, "DoorGreen", seg=8).cyl(x, y, 0.55, 0.15, 0.15, "Steel", seg=8, r2=0.08)
    B.box(x - 0.22, x + 0.22, y - 0.05, y + 0.05, 0.35, 0.45, "Steel")
    B.done()

pp = Builder("Payphone_1", (-7.2, 4.2, 0))
pp.box(-7.45, -7.05, 3.9, 4.5, 0.6, 2.0, "Payphone").box(-7.45, -7.3, 3.85, 4.55, 0, 2.2, "Steel")
pp.box(-7.5, -6.9, 3.8, 4.6, 2.2, 2.3, "Steel")
pp.done()

for i, y in enumerate((1.3, 1.85)):
    B = Builder(f"Prop_Newsbox{i}", (-7.1, y, 0))
    B.box(-7.35, -6.85, y - 0.22, y + 0.22, 0.3, 1.05, "Dumpster" if i else "Payphone")
    B.box(-7.3, -6.9, y - 0.18, y + 0.18, 0, 0.3, "Steel")
    B.done()

# the newspaper machine outside the Shop n Save (buy the paper here)
B = Builder("Newsbox_Paper", (7.0, 19.9, 0))
B.box(6.75, 7.25, 19.65, 20.15, 0.3, 1.1, "Payphone").box(6.8, 7.2, 19.7, 20.1, 0, 0.3, "Steel")
B.box(6.73, 6.75, 19.68, 20.12, 0.75, 1.05, "SignNews")
B.done()
# a bench by the subway entrance (loitering), and a second payphone on the east side
bench("Bench_Subway", 7.05, -1.9, along_y=True)
pp = Builder("Payphone_2", (7.2, -6.2 + 3.0, 0))
pp.box(7.05, 7.45, -3.5, -2.9, 0.6, 2.0, "Payphone").box(7.3, 7.45, -3.55, -2.85, 0, 2.2, "Steel")
pp.box(6.9, 7.5, -3.6, -2.8, 2.2, 2.3, "Steel")
pp.done()

crates = Builder("Prop_Crates")
for k in range(4):
    y = 15.6 + k * 0.62
    crates.box(6.75, 7.4, y - 0.28, y + 0.28, 0, 0.35, "Wood").box(6.8, 7.35, y - 0.24, y + 0.24, 0.35, 0.5, "Awning")
crates.done()

for i, (x, y, lean) in enumerate([(4.3, -2.3, 0.2), (-4.3, -2.3, 0.0), (4.3, -10.7, -0.25), (-4.3, -10.7, 0.12)]):
    B = Builder(f"Prop_Bollard{i}", (x, y, 0))
    B.cyl(x, y, 0, 0.08, 0.9, "Steel", seg=6, rot=(lean, 0, 0))
    B.done()

taxi = Builder("TaxiSign", (7.1, 9.2, 0))
taxi.cyl(7.1, 9.2, 0, 0.05, 2.6, "Steel", seg=6)
taxi.box(7.06, 7.14, 8.85, 9.55, 2.2, 2.6, "SignTaxi")
taxi.done()

fence = Builder("Prop_Fences")
for s in (-1, 1):
    x = s * 20.5
    fence.box(x - 0.01, x + 0.01, -28, 28, 0, 2.4, "Fence", 1.0)
    for k in range(15):
        y = -28 + k * 4
        fence.box(x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0, 2.5, "Steel")
fence.done()


export(PROJECT + "/Assets/Hitman/Art/Shackleton/ShackletonAve.fbx", HERE + "/ShackletonAve.blend")
