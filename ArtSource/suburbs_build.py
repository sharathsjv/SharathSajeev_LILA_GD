# Builds the Suburbs in the clean style of its reference (GTA III-era suburbs): tidy two-storey houses with
# hipped tile roofs and garages, mown lawns, hedges, pools, pines and round trees, rolling green hills all round,
# and clean light roads. Smaller plots than the reference. Footprints come from suburbs_layout.json.
# Axes: Blender X = Unity X, Blender Y = Unity Z (north). Naming as in shackleton_build.py.
import sys, json, math, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit
for m in (apt_textures, street_textures, street_kit): importlib.reload(m)
from street_kit import *

reset(31)
LAYOUT = {it["name"]: it for it in json.load(open(HERE + "/suburbs_layout.json"))["items"]}
LAMP_SPACING = 10.0
JUNCTION_Y = 13.5            # where the side street meets the main road's sidewalk

# ---------------------------------------------------------------- ground

single("Gnd_Lawns", (-21.5, 21.5, -29, 29, -0.08, -0.01, "GrassLush", 1.5))
hills("Gnd_Hills", (-21.5, 21.5, -29, 29), (-72, 72, -84, 84), 18.0)
single("Gnd_MainRoad", (-21.5, 21.5, 17, 21, -0.05, 0.0, "AsphaltClean", 2.0))
single("Gnd_SideRoad", (-1.5, 1.5, -28, 17, -0.05, 0.0, "AsphaltClean", 2.0))
single("Gnd_MainSidewalkW", (-21.5, -1.5, 13.5, 17, -0.05, 0.004, "ConcreteClean", 2.0))   # split where the
single("Gnd_MainSidewalkE", (1.5, 21.5, 13.5, 17, -0.05, 0.004, "ConcreteClean", 2.0))     # side street joins
single("Gnd_ShopFrontage", (-21.5, 21.5, 21, 21.5, -0.05, 0.004, "ConcreteClean", 2.0))
for s in (-1, 1):
    xa, xb = sorted((s * 1.5, s * 4.5))
    single("Gnd_Sidewalk" + ("E" if s > 0 else "W"), (xa, xb, -28, 13.5, -0.05, 0.004, "ConcreteClean", 2.0))

curbs = Builder("NoCol_Curbs")
curbs.box(-21.5, -1.5, 16.9, 17.1, 0, 0.07, "CurbClean")
curbs.box(1.5, 21.5, 16.9, 17.1, 0, 0.07, "CurbClean")
for s in (-1, 1):
    curbs.box(s * 1.5 - 0.1, s * 1.5 + 0.1, -28, 13.5, 0, 0.07, "CurbClean")
curbs.done()
paint = Builder("NoCol_RoadPaint")
k = -21.5
while k < 21:
    paint.box(k, k + 1.5, 18.94, 19.06, 0, 0.004, "LineYellow")
    k += 3.0
for y in (17.3, 20.7):
    paint.box(-21.5, 21.5, y - 0.05, y + 0.05, 0, 0.004, "LineWhite")
paint.done()

# ---------------------------------------------------------------- houses (front doors face the side street)

houses = [("House1", "House 1", +1, "StuccoCream", "RoofBrown"), ("House2", "House 2", -1, "StuccoWhite", "RoofRed"),
          ("House3", "House 3", +1, "StuccoPeach", "RoofBrown"), ("House4", "House 4 (Kevin)", -1, "StuccoCream", "RoofBrown"),
          ("House5", "House 5", +1, "StuccoWhite", "RoofBrown"), ("House6", "House 6", -1, "StuccoPeach", "RoofRed")]
kevin_door = LAYOUT["Door (House 4 (Kevin))"]["pos"][2]
for key, src_name, street, walls, roof in houses:
    src = LAYOUT[src_name]
    y0, y1 = src["min"][2], src["max"][2]
    door = kevin_door if key == "House4" else (y0 + y1) / 2 + 0.8
    _, gar = house2(key, src, street, walls, roof, door=door)
    face_x = src["max"][0] if street > 0 else src["min"][0]
    door_obj(key, face_x, street, door, kind="DoorGreen")
    fx = -4.65 * street                                  # the garden's edge along the sidewalk
    # paths: front path to the door, driveway to the garage
    path = Builder(f"NoCol_Path{key}", (fx, door, 0))
    pa, pb = sorted((fx, face_x))
    path.box(pa, pb, door - 0.5, door + 0.5, 0.0, 0.006, "ConcreteClean")
    path.box(pa, pb, gar[0], gar[1], 0.0, 0.006, "ConcreteClean")
    path.done()
    # low front hedge with gaps for the path and the driveway, taller hedges down the plot sides
    cuts = sorted([(door - 0.6, door + 0.6), (gar[0] - 0.1, gar[1] + 0.1)])
    a = y0 - 1.2
    for (c0, c1) in cuts + [(y1 + 1.2, y1 + 1.2)]:
        if c0 - a > 0.4:
            xa, xb = sorted((fx, fx - 0.5 * street))
            hedge(f"Prop_FrontHedge{key}_{a:.0f}", xa, xb, a, c0, h=0.6)
        a = c1
    for k2, hy in enumerate((y0 - 1.6, y1 + 1.6)):
        hx_a, hx_b = sorted((fx, -street * 18.5))
        hedge(f"Prop_Hedge{key}{k2}", hx_a, hx_b, hy - 0.4, hy + 0.4, h=1.3)
    mailbox(f"Prop_Mailbox{key}", fx + street * 0.3, door - 1.0)

# back gardens: pools for a couple of houses, trees everywhere
pool("Prop_PoolHouse1", -19.0, -16.0, 7.5, 11.0)
pool("Prop_PoolHouse6", 16.0, 19.0, -14.0, -10.5)
k = 0
for (x, y) in [(-17.5, -0.5), (18.0, -1.0), (-18.0, -12.0), (17.5, 10.0), (-20.0, 4.0), (20.0, 4.5), (-20.5, -6.5),
               (20.0, -7.0), (-7.0, -23.5), (8.0, -23.0), (-15.5, -24.5), (15.0, -24.0), (19.5, 25.5), (-19.5, 25.0),
               (-12.0, -26.5), (12.0, -26.5)]:
    k += 1
    if k % 2: pine(f"Prop_Tree{k:02d}", x, y, h=rng.uniform(6.5, 9.0))
    else: round_tree(f"Prop_Tree{k:02d}", x, y, h=rng.uniform(5.0, 6.5), r=rng.uniform(1.5, 2.0),
                     leaves="LeavesYellow" if k % 6 == 0 else "LeavesBright")
for (x, y) in [(-3.9, 4.0), (3.9, -7.0), (-3.9, -19.0), (3.9, 7.0), (-3.9, -7.5), (3.9, -19.5)]:
    k += 1
    round_tree(f"Prop_Tree{k:02d}", x, y, h=4.8, r=1.2)
B = Builder("Prop_BackFences", (0, 0, 0))
for s in (-1, 1):
    picket_fence(B, s * 20.5, s * 20.5, -28, 21)
B.done()

# ---------------------------------------------------------------- the shops on the main road (fronts face south)

building("Groceries", LAYOUT["Shops (Groceries)"], 1, "BrickClean", "S", shop={"sign": "SignMarket", "awning": "Awning"},
         door=LAYOUT["Door (Shops (Groceries))"]["pos"][0], clean=True)
building("Shop2", LAYOUT["Shop 2"], 1, "StuccoCream", "S", shop={"sign": "SignLaundry", "awning": "AwningRed"}, clean=True)
building("Shop3", LAYOUT["Shop 3"], 1, "StuccoWhite", "S", shop={"sign": "SignHardware", "awning": "Awning"}, clean=True)
door_obj("Groceries", 21.5, -1, LAYOUT["Door (Shops (Groceries))"]["pos"][0], axis="y")

# ---------------------------------------------------------------- street furniture

subway_kiosk("Station_Stairs", 3.5, 15.2, along_y=False)
taxi_sign(4.2, -21.0)

n = 0
y = JUNCTION_Y - 0.6                                    # side street, from the junction south
while y >= -27.5:
    for s_ in (-1, 1):
        n += 1
        lamp(f"Prop_Lamp{n:02d}", s_ * 1.85, y, (-s_, 0), 0.0)
    y -= LAMP_SPACING
for s_ in (-1, 1):                                      # main road, from the junction outwards
    x = 6.0
    while x <= 21:
        n += 1
        lamp(f"Prop_Lamp{n:02d}", s_ * x, 16.65, (0, 1), 0.0)
        x += LAMP_SPACING
for i, (x, y) in enumerate([(-1.85, -10.0), (12.0, 16.2)]):
    Bh = Builder(f"Prop_Hydrant{i}", (x, y, 0))
    Bh.cyl(x, y, 0, 0.13, 0.55, "CarRed", seg=8).cyl(x, y, 0.55, 0.15, 0.15, "Chrome", seg=8, r2=0.08)
    Bh.done()
pp = Builder("Payphone_1", (-8.0, 14.0, 0))
pp.box(-8.3, -7.7, 13.75, 14.15, 0.6, 2.0, "Payphone").box(-8.35, -7.65, 13.75, 13.9, 0, 2.2, "Steel").done()
bench("Prop_Bench0", -12.0, 14.0, along_y=False)

export(PROJECT + "/Assets/Hitman/Art/Suburbs/Suburbs.fbx", HERE + "/Suburbs.blend")
