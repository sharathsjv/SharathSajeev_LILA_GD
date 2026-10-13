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
hills("Gnd_Hills", (-48, 48, -50, 29), (-90, 90, -100, 84), 18.0)
single("Ext_Lawns", (-50, 50, -52, 29, -0.12, -0.03, "GrassLush", 1.5))      # past the barricades: more of the same
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

# Houses 7 and 8 at the south end (doc map: four rows of two); Kevin lives in 8 now.
LAYOUT["House 7"] = {"min": [-14.5, 0, -27.0], "max": [-5.5, 4, -20.0]}
LAYOUT["House 8 (Kevin)"] = {"min": [5.5, 0, -27.0], "max": [14.5, 4, -20.0]}
houses = [("House1", "House 1", +1, "StuccoCream", "RoofBrown"), ("House2", "House 2", -1, "StuccoWhite", "RoofRed"),
          ("House3", "House 3", +1, "StuccoPeach", "RoofBrown"), ("House4", "House 4 (Kevin)", -1, "StuccoCream", "RoofBrown"),
          ("House5", "House 5", +1, "StuccoWhite", "RoofBrown"), ("House6", "House 6", -1, "StuccoPeach", "RoofRed"),
          ("House7", "House 7", +1, "StuccoCream", "RoofRed"), ("House8", "House 8 (Kevin)", -1, "StuccoCream", "RoofBrown")]
kevin_door = -22.7
for key, src_name, street, walls, roof in houses:
    src = LAYOUT[src_name]
    y0, y1 = src["min"][2], src["max"][2]
    door = kevin_door if key == "House8" else (y0 + y1) / 2 + 0.8
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
               (20.0, -7.0), (-18.5, -18.5), (18.5, -18.5), (-19.0, -25.0), (19.0, -25.0), (19.5, 25.5), (-19.5, 25.0)]:
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
# the upkept brownstones (doc reference), set back so their stoops fit in front
brownstone_row("Upkeep", {"min": [-0.5, 0, 23.3], "max": [18.0, 0, 27.5]}, "S", floors=3, stoop=1.75)
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

# past the barricades: the roads go on, more houses and gardens
for (xa, xb) in ((-48, -21.5), (21.5, 48)):
    single(f"Ext_MainRoad{xa:.0f}", (xa, xb, 17, 21, -0.05, 0.0, "AsphaltClean", 2.0))
    single(f"Ext_MainWalk{xa:.0f}", (xa, xb, 13.5, 17, -0.05, 0.004, "ConcreteClean", 2.0))
    single(f"Ext_MainFront{xa:.0f}", (xa, xb, 21, 21.5, -0.05, 0.004, "ConcreteClean", 2.0))
    ext_row(f"Main{xa:.0f}", "x", xa + 0.5, xb - 0.5, 21.5, "S", depth=6.0, facades=("StuccoCream", "BrickClean", "StuccoWhite"), floors=(1, 2), brownstones=0.5)
    for x in range(int(xa) + 3, int(xb) - 2, 7):
        round_tree(f"Prop_ExtTree{x}", x + 0.5, 11.0, h=5.0, r=1.6)
single("Ext_SideRoad", (-1.5, 1.5, -52, -28, -0.05, 0.0, "AsphaltClean", 2.0))
for s_ in (-1, 1):
    xa, xb = sorted((s_ * 1.5, s_ * 4.5))
    single(f"Ext_SideWalk{s_}", (xa, xb, -52, -28, -0.05, 0.004, "ConcreteClean", 2.0))
    for k3, (y0, y1) in enumerate(((-38.0, -31.0), (-49.0, -42.0))):
        house2(f"Ext{s_}{k3}", {"min": [min(s_ * 5.5, s_ * 14.5), 0, y0], "max": [max(s_ * 5.5, s_ * 14.5), 4, y1]}, -s_,
               ["StuccoCream", "StuccoWhite", "StuccoPeach"][k3 % 3], ["RoofBrown", "RoofRed"][k3 % 2])

# where the player's part of the suburbs ends (doc map)
barricades("Barricade_W", 13.5, 21.0, -12.0, along_y=True)
barricades("Barricade_E", 13.5, 21.0, 12.0, along_y=True)
barricades("Barricade_S", -4.5, 4.5, -27.6)

# wider roads and sidewalks (everything beside them moves out to make room)
warp_scene(zones_x=[(0.0, 4.65, WIDEN)], zones_y=[(17.5, 4.0, WIDEN)])

export(PROJECT + "/Assets/Hitman/Art/Suburbs/Suburbs.fbx", HERE + "/Suburbs.blend")
