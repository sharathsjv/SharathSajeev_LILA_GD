# Builds the player's apartment from the layout in the design doc and exports it to the Unity project.
# Run inside Blender (live via the MCP bridge, or `blender -b --python apt_build.py`).
#
# Axes: Blender X = Unity X (east), Blender Y = Unity Z (north). The game camera looks west at the TV wall,
# so each room's east and south walls are the low cut-away ones.
#
# Every object is named after its room so the game can show one room at a time (see RoomVisibility.cs):
#   Liv_ living room, Kit_ kitchen, Bed_ bedroom, Bath_ bathroom,
#   Lead_<A>_<B> = floor of room A just past the doorway from room B, Trim_<A>_<B> = door frame between A and B.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Euler
import numpy as np

HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import importlib, apt_textures
importlib.reload(apt_textures)

PROJECT = os.path.dirname(HERE)
OUT = PROJECT + "/Assets/Hitman/Art/Apartment"
TEX = OUT + "/Textures"
T = 0.15          # wall thickness (partitions are two half-thickness slabs, one per room)
H = 2.7           # wall height
LOW = 0.5         # cut-away height for walls between the camera and the room
rng = np.random.default_rng(7)

# Room outlines (x0, x1, y0, y1)
LIV = (-3.0, 3.0, 0.0, 4.6)
KIT = (-0.2, 3.0, 4.6, 8.4)
BATH = (-1.8, -0.2, -2.4, 0.0)
BED = (-0.2, 3.0, -3.0, 0.0)
# Doorways
D_KIT = (1.3, 2.2)          # in y = 4.6
D_BATH = (-1.1, -0.3)       # in y = 0 (0.8 m: narrower blocks the NavMesh agent)
D_BED = (2.0, 2.8)          # in y = 0
D_ENTRANCE = (1.6, 2.6)     # in x = 3
# Wall finishes: (lower material, height of the lower band, upper material)
FINISH = {
    "Liv": ("DarkWood", 0.9, "Wallpaper"),
    "Kit": ("KitchenTile", 1.2, "PaintCream"),
    "Bed": ("DarkWood", 0.12, "WallpaperBlue"),
    "Bath": ("BathTile", 1.4, "Plaster"),
}

# ---------------------------------------------------------------- scene / materials

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for i in list(bpy.data.images):
    if i.name.startswith("T_Apt_"): bpy.data.images.remove(i)

MATS = {}


def mat(name):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new("M_Apt_" + name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    img = bpy.data.images.load(os.path.join(TEX, "T_Apt_" + name + ".png"), check_existing=True)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    MATS[name] = m
    return m


# ---------------------------------------------------------------- mesh helpers

def uv_project(bm, loc, tile, fitted):
    uv = bm.loops.layers.uv.verify()
    if fitted:
        vs = np.array([v.co[:] for v in bm.verts])
        lo, hi = vs.min(0), vs.max(0)
        span = np.maximum(hi - lo, 1e-4)
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co if fitted else (l.vert.co + loc)
            if ax == 2: a, b, ia, ib = p.x, p.y, 0, 1
            elif ax == 0: a, b, ia, ib = p.y, p.z, 1, 2
            else: a, b, ia, ib = p.x, p.z, 0, 2
            if fitted:
                u = (a - lo[ia]) / span[ia]; v = (b - lo[ib]) / span[ib]
                if (ax == 0 and n[0] < 0) or (ax == 1 and n[1] > 0): u = 1 - u
                l[uv].uv = (u, v)
            else:
                l[uv].uv = (a / tile, b / tile)


def finish(name, bm, loc, material, tile=1.0, rot=(0, 0, 0)):
    if rot != (0, 0, 0):
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler(rot).to_matrix())
    bm.normal_update()
    uv_project(bm, Vector(loc), tile, material in apt_textures.FITTED)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    me.materials.append(mat(material))
    bpy.context.collection.objects.link(ob)
    return ob


def bm_box(sx, sy, sz):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz + sz / 2))
    return bm


def box(name, x0, x1, y0, y1, z0, z1, material, tile=1.0):
    return finish(name, bm_box(x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, z0), material, tile)


def obox(name, cx, cy, z0, sx, sy, sz, material, rz=0.0, rx=0.0, ry=0.0, tile=1.0):
    return finish(name, bm_box(sx, sy, sz), (cx, cy, z0), material, tile, (rx, ry, rz))


def cyl(name, cx, cy, z0, r, h, material, seg=8, r2=None, rx=0.0, ry=0.0, rz=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, h / 2))
    return finish(name, bm, (cx, cy, z0), material, 0.5, (rx, ry, rz))


def blob(name, cx, cy, z0, sx, sy, sz, material, lumpy=0.25, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=0.5)
    for v in bm.verts:
        d = 1 + lumpy * (rng.random() - 0.5) * 2
        v.co = Vector((v.co.x * sx * d, v.co.y * sy * d, max(v.co.z, -0.35) * sz * d + sz * 0.35))
    return finish(name, bm, (cx, cy, z0), material, 0.6, (0, 0, rng.uniform(0, math.pi)))


# ---------------------------------------------------------------- walls

_wall_n = {}


def wall(room, axis, fixed, side, a0, a1, low=False, openings=()):
    """One room's face of a wall line. side +1/-1 = the room is on the +/- side of the line (half slab on that
    side); 0 = outside wall (full thickness). openings = (start, end, bottom, top). Low walls are cut away."""
    lower, split, upper = FINISH[room]
    top = LOW if low else H
    bands = [(0.0, min(split, top), lower), (min(split, top), top, upper)]
    if side > 0: d0, d1 = fixed, fixed + T / 2
    elif side < 0: d0, d1 = fixed - T / 2, fixed
    else: d0, d1 = fixed - T / 2, fixed + T / 2
    for (z0, z1, m) in bands:
        if z1 - z0 < 0.001: continue
        cur = a0
        pieces = []
        for (o0, o1, ob, ot) in sorted(openings):
            if o0 > cur: pieces.append((cur, o0, z0, z1))
            if ob > z0: pieces.append((o0, o1, z0, min(ob, z1)))
            if ot < z1: pieces.append((o0, o1, max(ot, z0), z1))
            cur = o1
        if cur < a1: pieces.append((cur, a1, z0, z1))
        for (s0, s1, p0, p1) in pieces:
            if p1 - p0 < 0.001 or s1 - s0 < 0.001: continue
            k = _wall_n[room] = _wall_n.get(room, 0) + 1
            if axis == "x": box(f"{room}_Wall{k}", s0, s1, d0, d1, p0, p1, m)
            else: box(f"{room}_Wall{k}", d0, d1, s0, s1, p0, p1, m)


def window_in(name, axis, fixed, a0, a1, sill, top):
    f = 0.06
    if axis == "x":
        mk = lambda n, x0, x1, z0, z1, d, m: box(n, x0, x1, fixed - d, fixed + d, z0, z1, m)
    else:
        mk = lambda n, y0, y1, z0, z1, d, m: box(n, fixed - d, fixed + d, y0, y1, z0, z1, m)
    mk(name + "Glass", a0, a1, sill, top, 0.01, "Window")
    fr = "Cabinet"
    mk(name + "FrameL", a0, a0 + f, sill, top, T / 2 + 0.02, fr)
    mk(name + "FrameR", a1 - f, a1, sill, top, T / 2 + 0.02, fr)
    mk(name + "FrameB", a0, a1, sill, sill + f, T / 2 + 0.04, fr)
    mk(name + "FrameT", a0, a1, top - f, top, T / 2 + 0.02, fr)
    mid = (a0 + a1) / 2
    mk(name + "BarV", mid - 0.025, mid + 0.025, sill, top, 0.03, fr)
    mk(name + "BarH", a0, a1, (sill + top) / 2 - 0.025, (sill + top) / 2 + 0.025, 0.03, fr)


def door_trim(name, axis, fixed, a0, a1, top=2.1):
    f = 0.07; e = T / 2 + 0.02
    if axis == "x":
        box(name + "L", a0 - f, a0, fixed - e, fixed + e, 0, top, "DarkWood")
        box(name + "R", a1, a1 + f, fixed - e, fixed + e, 0, top, "DarkWood")
        box(name + "T", a0 - f, a1 + f, fixed - e, fixed + e, top, top + f, "DarkWood")
    else:
        box(name + "L", fixed - e, fixed + e, a0 - f, a0, 0, top, "DarkWood")
        box(name + "R", fixed - e, fixed + e, a1, a1 + f, 0, top, "DarkWood")
        box(name + "T", fixed - e, fixed + e, a0 - f, a1 + f, top, top + f, "DarkWood")


DOOR = lambda d: (d[0], d[1], 0, 2.1)

# ---- living room: TV wall (west) and desk wall (north) full; south and east cut away
wall("Liv", "y", -3, 0, 0, 4.6, openings=[(3.5, 4.3, 0.95, 2.05)])
wall("Liv", "x", 4.6, 0, -3 - T / 2, -0.2 - T / 2)
wall("Liv", "x", 4.6, -1, -0.2 - T / 2, 3, openings=[DOOR(D_KIT)])
wall("Liv", "x", 0, +1, -3, 3, low=True, openings=[DOOR(D_BATH), DOOR(D_BED)])
wall("Liv", "y", 3, 0, 0, 4.6, low=True, openings=[DOOR(D_ENTRANCE)])
window_in("Liv_Window", "y", -3, 3.5, 4.3, 0.95, 2.05)
# ---- kitchen: west and north full; south and east cut away
wall("Kit", "y", -0.2, 0, 4.6, 8.4 + T / 2, openings=[(5.6, 6.4, 1.15, 2.05)])
wall("Kit", "x", 8.4, 0, -0.2 - T / 2, 3 + T / 2, openings=[(1.0, 2.0, 1.15, 2.05)])
wall("Kit", "x", 4.6, +1, -0.2, 3, low=True, openings=[DOOR(D_KIT)])
wall("Kit", "y", 3, 0, 4.6, 8.4 + T / 2, low=True)
window_in("Kit_WindowN", "x", 8.4, 1.0, 2.0, 1.15, 2.05)
window_in("Kit_WindowW", "y", -0.2, 5.6, 6.4, 1.15, 2.05)
# ---- bathroom: north and west full; south and east cut away
wall("Bath", "x", 0, -1, -1.8, -0.2, openings=[DOOR(D_BATH)])
wall("Bath", "y", -1.8, 0, -2.4 - T / 2, 0)
wall("Bath", "x", -2.4, 0, -1.8 - T / 2, -0.2, low=True)
wall("Bath", "y", -0.2, -1, -2.4, 0, low=True)
# ---- bedroom: north and west full; south and east cut away
wall("Bed", "x", 0, -1, -0.2, 3, openings=[DOOR(D_BED)])
wall("Bed", "y", -0.2, +1, -2.4, 0)
wall("Bed", "y", -0.2, 0, -3 - T / 2, -2.4)
wall("Bed", "x", -3, 0, -0.2 - T / 2, 3 + T / 2, low=True)
wall("Bed", "y", 3, 0, -3, 0, low=True)

door_trim("Trim_Liv_Kit_", "x", 4.6, *D_KIT)
door_trim("Trim_Liv_Bath_", "x", 0, *D_BATH)
door_trim("Trim_Liv_Bed_", "x", 0, *D_BED)

# ---- floors, plus the strips of each room you can see from the next one through a doorway
box("Liv_Floor", -3, 3, 0, 4.6, -0.05, 0, "WoodFloor")
box("Kit_Floor", -0.2, 3, 4.6, 8.4, -0.05, 0, "Linoleum")
box("Bath_Floor", -1.8, -0.2, -2.4, 0, -0.05, 0, "BathTile", tile=0.5)
box("Bed_Floor", -0.2, 3, -3, 0, -0.05, 0, "Carpet")
LEAD = 1.0
box("Lead_Kit_Liv", D_KIT[0] - 0.2, D_KIT[1] + 0.2, 4.6, 4.6 + LEAD, -0.05, 0.002, "Linoleum")
box("Lead_Liv_Kit", D_KIT[0] - 0.2, D_KIT[1] + 0.2, 4.6 - LEAD, 4.6, -0.05, 0.002, "WoodFloor")
box("Lead_Bath_Liv", D_BATH[0] - 0.2, D_BATH[1] + 0.15, -LEAD, 0, -0.05, 0.002, "BathTile", tile=0.5)
box("Lead_Liv_Bath", D_BATH[0] - 0.2, D_BATH[1] + 0.15, 0, LEAD, -0.05, 0.002, "WoodFloor")
box("Lead_Bed_Liv", D_BED[0] - 0.2, D_BED[1] + 0.12, -LEAD, 0, -0.05, 0.002, "Carpet")
box("Lead_Liv_Bed", D_BED[0] - 0.2, D_BED[1] + 0.12, 0, LEAD, -0.05, 0.002, "WoodFloor")

# ---- entrance door (the way out)
door_trim("Liv_TrimEntrance", "y", 3, *D_ENTRANCE)
obox("Liv_EntranceDoor", 3.0, 2.1, 0, 0.06, 1.0, 2.08, "Door")
cyl("Liv_EntranceDoorKnob", 2.94, 2.45, 1.0, 0.03, 0.06, "MetalDark", ry=math.pi / 2)

# ---------------------------------------------------------------- kitchen

obox("Kit_Fridge", 0.27, 7.95, 0, 0.66, 0.68, 1.78, "MetalWhite")
box("Kit_FridgeSeam", 0.6, 0.615, 7.62, 8.28, 1.22, 1.24, "MetalDark")
box("Kit_FridgeHandle", 0.6, 0.64, 7.66, 7.7, 0.7, 1.15, "MetalDark")
box("Kit_FridgeHandle2", 0.6, 0.64, 7.66, 7.7, 1.35, 1.6, "MetalDark")

box("Kit_CounterN", 0.62, 1.72, 7.73, 8.325, 0, 0.86, "Cabinet")
box("Kit_CounterNTop", 0.6, 1.74, 7.7, 8.325, 0.86, 0.9, "Formica")
box("Kit_Sink", 0.85, 1.35, 7.82, 8.22, 0.9, 0.905, "MetalDark")
cyl("Kit_SinkTap", 1.1, 8.25, 0.9, 0.015, 0.22, "MetalWhite", seg=6)
obox("Kit_SinkSpout", 1.1, 8.17, 1.1, 0.03, 0.17, 0.03, "MetalWhite")
box("Kit_CabinetUpper", 0.62, 0.98, 8.0, 8.325, 1.5, 2.2, "Cabinet")

obox("Kit_Stove", 2.04, 8.02, 0, 0.6, 0.6, 0.9, "MetalWhite")
box("Kit_StoveTop", 1.76, 2.32, 7.74, 8.3, 0.9, 0.915, "MetalDark")
for i, (bx, by) in enumerate([(1.9, 7.88), (2.18, 7.88), (1.9, 8.16), (2.18, 8.16)]):
    cyl(f"Kit_StoveBurner{i}", bx, by, 0.915, 0.08, 0.015, "MetalDark", seg=10)
box("Kit_StoveOvenDoor", 1.8, 2.28, 7.715, 7.73, 0.12, 0.7, "Screen")
for i in range(4):
    cyl(f"Kit_StoveKnob{i}", 1.84 + i * 0.13, 7.715, 0.78, 0.02, 0.03, "Plastic", seg=6, rx=math.pi / 2)

box("Kit_CounterE", 2.4, 2.925, 5.9, 7.7, 0, 0.86, "Cabinet")
box("Kit_CounterETop", 2.38, 2.925, 5.88, 7.7, 0.86, 0.9, "Formica")

box("Kit_DiningTable", 0.5, 1.3, 6.3, 6.9, 0.70, 0.74, "DarkWood")
for i, (lx, ly) in enumerate([(0.54, 6.34), (1.26, 6.34), (0.54, 6.86), (1.26, 6.86)]):
    box(f"Kit_DiningTableLeg{i}", lx - 0.025, lx + 0.025, ly - 0.025, ly + 0.025, 0, 0.7, "DarkWood")


def chair(name, cx, cy, face, clothes=False):
    """Seat centred at (cx, cy); face = (dx, dy) the way the sitter faces; the back is behind them."""
    s = 0.42
    box(name, cx - s / 2, cx + s / 2, cy - s / 2, cy + s / 2, 0.42, 0.46, "DarkWood")
    for i, (dx, dy) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)]):
        lx, ly = cx + dx * (s / 2 - 0.03), cy + dy * (s / 2 - 0.03)
        box(f"{name}Leg{i}", lx - 0.02, lx + 0.02, ly - 0.02, ly + 0.02, 0, 0.42, "DarkWood")
    if face[1] != 0:
        by = cy - face[1] * (s / 2 - 0.02)
        box(f"{name}Back", cx - s / 2, cx + s / 2, by - 0.02, by + 0.02, 0.46, 0.95, "DarkWood")
    else:
        bx = cx - face[0] * (s / 2 - 0.02)
        box(f"{name}Back", bx - 0.02, bx + 0.02, cy - s / 2, cy + s / 2, 0.46, 0.95, "DarkWood")
    if clothes:
        blob(f"{name}Clothes", cx, cy, 0.46, 0.4, 0.38, 0.14, "Cloth")


chair("Kit_DiningChair", 0.9, 5.97, (0, 1))
obox("Kit_CerealBox", 0.7, 6.6, 0.74, 0.07, 0.19, 0.27, "Cardboard", rz=0.3)
cyl("Kit_CerealBowl", 0.95, 6.45, 0.74, 0.07, 0.05, "Ceramic", seg=10, r2=0.05)
cyl("Kit_DiningBottle", 1.15, 6.75, 0.74, 0.035, 0.24, "GlassGreen", seg=8)
for i in range(3):
    cyl(f"Kit_Plate{i}", 1.5, 8.0, 0.9 + i * 0.02, 0.11, 0.018, "Ceramic", seg=10)
for i, (bx, by, m) in enumerate([(2.55, 6.1, "GlassBrown"), (2.65, 6.25, "GlassGreen"), (2.75, 6.05, "GlassBrown"), (0.68, 7.95, "GlassGreen")]):
    cyl(f"Kit_CounterBottle{i}", bx, by, 0.9, 0.035, 0.26, m, seg=8)
blob("Kit_Trash", 0.22, 7.2, 0, 0.5, 0.45, 0.55, "Trash")
for i in range(3):
    obox(f"Kit_PizzaBox{i}", 0.25, 5.15, i * 0.045, 0.4, 0.4, 0.045, "Cardboard", rz=rng.uniform(-0.3, 0.3))

# ---------------------------------------------------------------- living room

box("Liv_TVStand", -2.925, -2.45, 1.45, 3.25, 0, 0.55, "DarkWood")
obox("Liv_CRT", -2.7, 2.35, 0.55, 0.48, 0.56, 0.46, "Plastic")
box("Liv_CRTScreen", -2.46, -2.445, 2.14, 2.56, 0.6, 0.95, "Screen")
obox("Liv_VCR", -2.68, 2.35, 0.3, 0.4, 0.42, 0.08, "MetalDark")
cyl("Liv_Antenna1", -2.75, 2.25, 1.01, 0.006, 0.4, "MetalDark", seg=4, rx=0.45)
cyl("Liv_Antenna2", -2.75, 2.45, 1.01, 0.006, 0.4, "MetalDark", seg=4, rx=-0.45)

box("Liv_Couch", 0.1, 0.95, 1.5, 3.2, 0, 0.42, "Couch")
box("Liv_CouchBack", 0.75, 0.97, 1.5, 3.2, 0.42, 0.88, "Couch")
box("Liv_CouchArmS", 0.1, 0.95, 1.5, 1.68, 0.42, 0.62, "Couch")
box("Liv_CouchArmN", 0.1, 0.95, 3.02, 3.2, 0.42, 0.62, "Couch")
obox("Liv_CouchCushion1", 0.42, 1.97, 0.42, 0.62, 0.62, 0.12, "Couch", rz=0.08)
obox("Liv_CouchCushion2", 0.42, 2.66, 0.42, 0.62, 0.66, 0.12, "Couch", rz=-0.12)
blob("Liv_CouchClothes", 0.45, 2.9, 0.54, 0.45, 0.35, 0.16, "Cloth")

box("Liv_Rug", -1.95, -0.05, 1.3, 3.4, 0, 0.012, "Rug")
box("Liv_CoffeeTable", -1.3, -0.5, 2.0, 2.8, 0.36, 0.4, "DarkWood")
for i, (lx, ly) in enumerate([(-1.26, 2.04), (-0.54, 2.04), (-1.26, 2.76), (-0.54, 2.76)]):
    box(f"Liv_CoffeeTableLeg{i}", lx - 0.025, lx + 0.025, ly - 0.025, ly + 0.025, 0.012, 0.36, "DarkWood")
for i in range(5):
    cyl(f"Liv_CoffeeBottle{i}", -1.2 + rng.uniform(0, 0.6), 2.1 + rng.uniform(0, 0.6), 0.4, 0.035, 0.25,
        "GlassBrown" if i % 2 else "GlassGreen", seg=8)
for i in range(3):
    cyl(f"Liv_FloorBottle{i}", -0.2 + rng.uniform(-1.4, 0.2), 1.2 + rng.uniform(0, 2.4), 0.035, 0.035, 0.25,
        "GlassBrown", seg=8, rx=math.pi / 2, rz=rng.uniform(0, math.pi))
obox("Liv_CoffeePizza", -0.9, 2.45, 0.4, 0.38, 0.38, 0.04, "Cardboard", rz=0.5)
cyl("Liv_Ashtray", -0.65, 2.15, 0.4, 0.06, 0.025, "GlassBrown", seg=8)

# desk with the laptop (email / notepad) against the north wall, under the west window
box("Liv_Desk", -2.7, -1.5, 3.95, 4.5, 0.72, 0.76, "DarkWood")
box("Liv_DeskSideL", -2.7, -2.66, 3.95, 4.5, 0, 0.72, "DarkWood")
box("Liv_DeskSideR", -1.54, -1.5, 3.95, 4.5, 0, 0.72, "DarkWood")
obox("Liv_Laptop", -2.1, 4.12, 0.76, 0.34, 0.24, 0.02, "MetalDark")
finish("Liv_LaptopLid", bm_box(0.34, 0.015, 0.23), (-2.1, 4.24, 0.77), "MetalDark", 1.0, (-0.25, 0, 0))
finish("Liv_LaptopScreen", bm_box(0.3, 0.004, 0.19), (-2.1, 4.228, 0.79), "Screen", 1.0, (-0.25, 0, 0))
for i in range(4):
    obox(f"Liv_DeskBook{i}", -2.55, 4.3, 0.76 + i * 0.05, 0.2, 0.28, 0.05, "Books", rz=rng.uniform(-0.25, 0.25))
for i in range(4):
    obox(f"Liv_DeskPaper{i}", -1.75 + rng.uniform(-0.1, 0.1), 4.15 + rng.uniform(-0.1, 0.1), 0.76 + i * 0.003,
         0.21, 0.3, 0.003, "Paper", rz=rng.uniform(-0.6, 0.6))
cyl("Liv_DeskMug", -1.65, 4.38, 0.76, 0.04, 0.1, "Ceramic", seg=8)
cyl("Liv_LampBase", -2.62, 4.0, 0.76, 0.06, 0.02, "MetalDark", seg=8)
cyl("Liv_LampPole", -2.62, 4.0, 0.78, 0.01, 0.32, "MetalDark", seg=4)
cyl("Liv_LampShade", -2.62, 4.0, 1.05, 0.12, 0.14, "Fabric", seg=8, r2=0.06)
chair("Liv_DeskChair", -2.1, 3.62, (0, 1))

for i, (bx, by, s) in enumerate([(-2.6, 0.45, 0.55), (-2.55, 0.5, 0.45), (-2.2, 0.35, 0.5)]):
    obox(f"Liv_Boxes{i}", bx, by, 0 if i != 1 else 0.55, s, s, s * 0.9, "Cardboard", rz=rng.uniform(-0.4, 0.4))
blob("Liv_Trash1", 2.6, 4.15, 0, 0.55, 0.5, 0.6, "Trash")
blob("Liv_Trash2", 2.55, 3.6, 0, 0.45, 0.45, 0.5, "Trash")
obox("Liv_PosterNoir", -2.92, 0.9, 1.15, 0.012, 0.6, 0.85, "PosterNoir", rz=0.03)

# ---------------------------------------------------------------- bedroom (3.2 x 3.0)
# Bed against the west wall; wardrobe on the north wall; shelves above the bed; a small table by the south wall.

box("Bed_Frame", -0.125, 1.9, -2.75, -1.35, 0, 0.3, "DarkWood")
box("Bed_Mattress", -0.08, 1.85, -2.7, -1.4, 0.3, 0.5, "Sheets")
box("Bed_Headboard", -0.125, -0.05, -2.75, -1.35, 0.3, 0.95, "DarkWood")
obox("Bed_Pillow", 0.15, -2.05, 0.5, 0.36, 0.62, 0.12, "Sheets", rz=0.15)
blob("Bed_Blanket", 1.05, -2.05, 0.42, 1.25, 1.25, 0.2, "Sheets", lumpy=0.35)
box("Bed_Rug", 0.3, 1.7, -1.33, -0.75, 0, 0.01, "Rug")

box("Bed_SideTable", -0.125, 0.3, -1.25, -0.85, 0, 0.5, "DarkWood")
cyl("Bed_FanBase", 0.08, -1.05, 0.5, 0.08, 0.03, "Plastic", seg=8)
cyl("Bed_FanPole", 0.08, -1.05, 0.53, 0.012, 0.25, "Plastic", seg=4)
cyl("Bed_FanHead", 0.14, -1.05, 0.8, 0.14, 0.06, "Plastic", seg=10, ry=math.pi / 2)
cyl("Bed_Ashtray", 0.22, -0.95, 0.5, 0.05, 0.02, "GlassBrown", seg=8)

# wardrobe: body, two doors with a gap, handles, junk on top
box("Bed_Wardrobe", 0.45, 1.65, -0.62, -0.075, 0, 1.95, "DarkWood")
box("Bed_WardrobeDoorL", 0.48, 1.04, -0.64, -0.62, 0.08, 1.88, "Door")
box("Bed_WardrobeDoorR", 1.06, 1.62, -0.64, -0.62, 0.08, 1.88, "Door")
box("Bed_WardrobeKnobL", 0.98, 1.0, -0.67, -0.64, 0.95, 1.05, "MetalDark")
box("Bed_WardrobeKnobR", 1.1, 1.12, -0.67, -0.64, 0.95, 1.05, "MetalDark")
obox("Bed_WardrobeBox1", 0.75, -0.35, 1.95, 0.45, 0.4, 0.3, "Cardboard", rz=0.1)
obox("Bed_WardrobeBox2", 1.3, -0.33, 1.95, 0.35, 0.35, 0.22, "Cardboard", rz=-0.2)
obox("Bed_Suitcase", 1.32, -0.34, 2.17, 0.4, 0.3, 0.14, "MetalDark", rz=0.15)

# two wall shelves above the bed, loaded
for k, z in enumerate((1.3, 1.7)):
    box(f"Bed_Shelf{k}", -0.125, 0.1, -2.6, -1.5, z, z + 0.03, "DarkWood")
    for b in range(2):
        box(f"Bed_ShelfBracket{k}{b}", -0.125, 0.06, -2.5 + b * 0.9, -2.47 + b * 0.9, z - 0.15, z, "MetalDark")
for i in range(7):
    obox(f"Bed_ShelfBook{i}", -0.02, -2.55 + i * 0.065, 1.33, 0.18, 0.05, 0.2 + rng.uniform(0, 0.06), "Books",
         rx=0, rz=0, ry=0)
obox("Bed_ShelfBookLean", 0.0, -2.05, 1.33, 0.18, 0.05, 0.22, "Books", rx=0.35)
obox("Bed_ShelfBox", 0.0, -1.75, 1.33, 0.2, 0.3, 0.16, "Cardboard", rz=0.1)
cyl("Bed_ShelfBottle1", -0.02, -2.45, 1.73, 0.035, 0.24, "GlassGreen", seg=8)
cyl("Bed_ShelfBottle2", 0.0, -2.33, 1.73, 0.035, 0.22, "GlassBrown", seg=8)
obox("Bed_ShelfRadio", -0.0, -1.95, 1.73, 0.18, 0.32, 0.18, "Plastic", rz=0.05)
cyl("Bed_ShelfMug", 0.0, -1.68, 1.73, 0.04, 0.1, "Ceramic", seg=8)
obox("Bed_PhotoFrame", -0.11, -2.05, 1.95, 0.02, 0.36, 0.28, "PosterJazz", rz=0.02)

# small table + chair by the south wall
box("Bed_Table", 1.95, 2.9, -2.9, -2.35, 0.72, 0.76, "DarkWood")
for i, (lx, ly) in enumerate([(1.98, -2.87), (2.87, -2.87), (1.98, -2.38), (2.87, -2.38)]):
    box(f"Bed_TableLeg{i}", lx - 0.02, lx + 0.02, ly - 0.02, ly + 0.02, 0, 0.72, "DarkWood")
chair("Bed_Chair", 2.42, -2.1, (0, -1), clothes=True)
cyl("Bed_TableLampBase", 2.75, -2.75, 0.76, 0.06, 0.02, "MetalDark", seg=8)
cyl("Bed_TableLampPole", 2.75, -2.75, 0.78, 0.01, 0.3, "MetalDark", seg=4)
cyl("Bed_TableLampShade", 2.75, -2.75, 1.03, 0.11, 0.13, "Fabric", seg=8, r2=0.06)
for i in range(3):
    obox(f"Bed_TablePaper{i}", 2.25 + rng.uniform(-0.1, 0.1), -2.62 + rng.uniform(-0.08, 0.08), 0.76 + i * 0.003,
         0.21, 0.3, 0.003, "Paper", rz=rng.uniform(-0.6, 0.6))
cyl("Bed_TableBottle", 2.05, -2.5, 0.76, 0.035, 0.25, "GlassBrown", seg=8)
obox("Bed_TableBooks", 2.6, -2.5, 0.76, 0.22, 0.3, 0.12, "Books", rz=0.3)

# floor clutter
cyl("Bed_LaundryBasket", 2.75, -0.95, 0, 0.22, 0.45, "Fabric", seg=8, r2=0.25)
blob("Bed_LaundryTop", 2.75, -0.95, 0.4, 0.45, 0.45, 0.18, "Cloth")
blob("Bed_Clothes1", 2.6, -1.6, 0, 0.6, 0.5, 0.2, "Cloth")
blob("Bed_Bag", 0.25, -0.38, 0, 0.38, 0.32, 0.38, "Trash")
for i in range(3):
    obox(f"Bed_Shoes{i}", 1.75 + i * 0.12, -0.85, 0, 0.1, 0.26, 0.1, "MetalDark", rz=rng.uniform(-0.5, 0.5))

# ---------------------------------------------------------------- bathroom (1.6 x 2.4)

box("Bath_ShowerTray", -1.725, -1.0, -2.325, -1.6, 0, 0.08, "Ceramic")
box("Bath_ShowerCurtain", -1.03, -1.0, -2.325, -1.6, 0.3, 2.0, "Fabric")
cyl("Bath_Toilet", -1.45, -1.1, 0, 0.17, 0.4, "Ceramic", seg=10, r2=0.2)
box("Bath_ToiletTank", -1.725, -1.58, -1.3, -0.9, 0.4, 0.75, "Ceramic")
box("Bath_Sink", -1.7, -1.2, -0.525, -0.075, 0.8, 0.88, "Ceramic")
box("Bath_SinkPedestal", -1.5, -1.4, -0.3, -0.2, 0, 0.8, "Ceramic")
box("Bath_Mirror", -1.65, -1.25, -0.09, -0.075, 1.25, 1.85, "Mirror")
blob("Bath_Towel", -0.7, -1.7, 0, 0.45, 0.3, 0.08, "Fabric")
for i in range(3):
    cyl(f"Bath_Bottle{i}", -1.6 + i * 0.07, -0.2, 0.88, 0.025, 0.14, "Plastic", seg=6)

# ---------------------------------------------------------------- export

os.makedirs(OUT, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT + "/Apartment.fbx", use_selection=False, object_types={"MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
bpy.ops.wm.save_as_mainfile(filepath=HERE + "/Apartment.blend")
print("OBJECTS", len(bpy.data.objects), "MATERIALS", len(MATS))
