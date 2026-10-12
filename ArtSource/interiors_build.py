# Builds the interiors from the layouts in the design doc and exports one FBX each:
#   SchruteLobby (Schrute Towers ground floor), SchruteFloor5 (the paper company), KevinHouse (ground floor),
#   KevinHouseUp (upstairs), CornerBar (Business District bar).
# Run inside Blender (live via the MCP bridge, or `blender -b --python interiors_build.py`).
#
# Each interior is built around its own origin (it's placed under its Interior in the game). Blender X = Unity X,
# Blender Y = Unity Z. The camera looks in from the south-west, so south and west walls are cut low.
# Names the game uses: *_Door (the way out), Elev_* (elevator doors), *_Stairs (stairs between floors),
# LightSrc_Bulb_* (ceiling lights -> baked lights), Kit_Stove / House_Window* (for later kill set-ups).
import sys, math, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit
for m in (apt_textures, street_textures, street_kit): importlib.reload(m)
from street_kit import *

OUT = PROJECT + "/Assets/Hitman/Art/Interiors"
T = 0.2         # wall thickness
LOW = 0.6       # cut-away height for walls facing the camera


def wall(name, axis, fixed, a0, a1, h, m, openings=(), low=False, glass=None):
    """A wall along X (axis "x", at Y = fixed) or Y. openings = (start, end, bottom, top); window openings get
    a glass pane (glass material) and a frame."""
    B = Builder(name, ((a0 + a1) / 2, fixed, 0) if axis == "x" else (fixed, (a0 + a1) / 2, 0))
    top = LOW if low else h
    cur = a0
    for (o0, o1, ob, ot) in sorted(openings):
        if o0 > cur: seg(B, axis, fixed, cur, o0, 0, top, m)
        if ob > 0: seg(B, axis, fixed, o0, o1, 0, min(ob, top), m)
        if ot < top: seg(B, axis, fixed, o0, o1, ot, top, m)
        if glass and ob > 0 and ot <= top:
            seg(B, axis, fixed, o0, o1, ob, ot, glass, 0.02)
            for a in (o0, o1):
                seg(B, axis, fixed, a - 0.05, a + 0.05, ob, ot, "Steel", T + 0.04)
        cur = o1
    if cur < a1: seg(B, axis, fixed, cur, a1, 0, top, m)
    return B.done()


def seg(B, axis, fixed, a0, a1, z0, z1, m, t=T):
    if z1 - z0 < 0.001 or a1 - a0 < 0.001: return
    if axis == "x": B.box(a0, a1, fixed - t / 2, fixed + t / 2, z0, z1, m)
    else: B.box(fixed - t / 2, fixed + t / 2, a0, a1, z0, z1, m)


def floor(name, x0, x1, y0, y1, m, tile=1.0):
    return single(name, (x0, x1, y0, y1, -0.05, 0.0, m, tile))


def chair(name, cx, cy, face, seat="FabricBlue", frame="Steel", office=False):
    """face = (dx, dy) the way the sitter looks; back is behind them."""
    B = Builder(name, (cx, cy, 0))
    B.box(cx - 0.23, cx + 0.23, cy - 0.23, cy + 0.23, 0.44, 0.52, seat)
    bx, by = cx - face[0] * 0.22, cy - face[1] * 0.22
    if face[1] != 0: B.box(cx - 0.22, cx + 0.22, by - 0.04, by + 0.04, 0.52, 1.0, seat)
    else: B.box(bx - 0.04, bx + 0.04, cy - 0.22, cy + 0.22, 0.52, 1.0, seat)
    if office:
        B.cyl(cx, cy, 0.08, 0.04, 0.36, frame, seg=6)
        B.box(cx - 0.28, cx + 0.28, cy - 0.03, cy + 0.03, 0.0, 0.08, frame)
        B.box(cx - 0.03, cx + 0.03, cy - 0.28, cy + 0.28, 0.0, 0.08, frame)
    else:
        for dx in (-0.19, 0.19):
            for dy in (-0.19, 0.19):
                B.box(cx + dx - 0.025, cx + dx + 0.025, cy + dy - 0.025, cy + dy + 0.025, 0, 0.44, frame)
    return B.done()


def table(name, x0, x1, y0, y1, h=0.74, top="Laminate", legs="Steel"):
    B = Builder(name, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, h - 0.04, h, top)
    for x in (x0 + 0.06, x1 - 0.06):
        for y in (y0 + 0.06, y1 - 0.06):
            B.box(x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0, h - 0.04, legs)
    return B.done()


def computer(name, cx, cy, face):
    """CRT monitor + keyboard on a desk top (z 0.74); face = direction the screen looks."""
    B = Builder(name, (cx, cy, 0.74))
    B.box(cx - 0.21, cx + 0.21, cy - 0.21, cy + 0.21, 0.74, 1.12, "BeigePlastic")
    sx, sy = cx + face[0] * 0.215, cy + face[1] * 0.215
    if face[1] != 0: B.box(cx - 0.16, cx + 0.16, sy - 0.005, sy + 0.005, 0.8, 1.06, "Monitor")
    else: B.box(sx - 0.005, sx + 0.005, cy - 0.16, cy + 0.16, 0.8, 1.06, "Monitor")
    kx, ky = cx + face[0] * 0.45, cy + face[1] * 0.45
    B.box(kx - 0.22, kx + 0.22, ky - 0.08, ky + 0.08, 0.74, 0.77, "BeigePlastic") if face[1] != 0 else \
        B.box(kx - 0.08, kx + 0.08, ky - 0.22, ky + 0.22, 0.74, 0.77, "BeigePlastic")
    return B.done()


def plant(name, x, y, h=1.4):
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.25, 0.5, "Concrete", seg=8, r2=0.2)
    for k in range(3):
        B.blob(x + rng.uniform(-0.15, 0.15), y + rng.uniform(-0.15, 0.15), 0.45 + k * 0.25, 0.6, 0.6, 0.55, "LeavesBright", 0.3)
    return B.done()


def sofa(name, x0, x1, y0, y1, face, m="FabricBlue"):
    """face = side the sitter looks towards; back and arms on the other sides."""
    B = Builder(name, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, 0.42, m)
    t = 0.2
    if face == (0, -1): B.box(x0, x1, y1 - t, y1, 0.42, 0.85, m)
    elif face == (0, 1): B.box(x0, x1, y0, y0 + t, 0.42, 0.85, m)
    elif face == (-1, 0): B.box(x1 - t, x1, y0, y1, 0.42, 0.85, m)
    else: B.box(x0, x0 + t, y0, y1, 0.42, 0.85, m)
    return B.done()


def ceiling_light(name, x, y, z, w=1.2, d=0.3):
    """Marks where a ceiling light goes (the game bakes a light there). There's no ceiling in the cutaway view,
    so the fitting itself isn't drawn: just a tiny marker at the light's height."""
    B = Builder(name, (x, y, z))
    B.box(x - 0.01, x + 0.01, y - 0.01, y + 0.01, z - 0.02, z, "LampGlass")
    return B.done()


def stairs(name, x0, x1, y0, y1, rise=3.0, steps=14, m="MarbleDark", rail="Brass"):
    """A straight flight rising towards +Y, with side rails."""
    B = Builder(name, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    run = (y1 - y0) / steps
    for i in range(steps):
        B.box(x0, x1, y0 + i * run, y0 + (i + 1) * run, 0, (i + 1) * rise / steps, m)
    ang = math.atan2(rise, y1 - y0)
    L = math.hypot(rise, y1 - y0)
    for x in (x0 + 0.04, x1 - 0.04):
        B.box(x - 0.03, x + 0.03, (y0 + y1) / 2 - L / 2, (y0 + y1) / 2 + L / 2, rise / 2 + 0.95, rise / 2 + 1.0, rail,
              rot=(ang, 0, 0), pivot=(x, (y0 + y1) / 2, rise / 2))
    return B.done()


def elevator(name, cx, wall_y, label_sign=None):
    """Elevator doors set into a wall at Y = wall_y (facing -Y), with a brass frame and call button."""
    B = Builder(name, (cx, wall_y, 0))
    B.box(cx - 0.75, cx + 0.75, wall_y - 0.12, wall_y - 0.08, 0, 2.3, "ElevatorDoor")
    B.box(cx - 0.9, cx - 0.75, wall_y - 0.16, wall_y - 0.06, 0, 2.45, "Brass")
    B.box(cx + 0.75, cx + 0.9, wall_y - 0.16, wall_y - 0.06, 0, 2.45, "Brass")
    B.box(cx - 0.9, cx + 0.9, wall_y - 0.16, wall_y - 0.06, 2.3, 2.45, "Brass")
    B.box(cx + 1.0, cx + 1.12, wall_y - 0.14, wall_y - 0.1, 1.1, 1.3, "Brass")
    B.box(cx - 0.3, cx + 0.3, wall_y - 0.13, wall_y - 0.09, 2.55, 2.75, "Monitor")
    return B.done()


def elevator_side(name, x_face, cy, s):
    """Elevator doors on a face at X = x_face, facing s (+1 = east, -1 = west), centred at Y = cy."""
    B = Builder(name, (x_face, cy, 0))
    def bx(y0, y1, d0, d1, z0, z1, m):
        a, b = sorted((x_face + s * d0, x_face + s * d1))
        B.box(a, b, y0, y1, z0, z1, m)
    bx(cy - 0.75, cy + 0.75, 0.08, 0.12, 0, 2.3, "ElevatorDoor")
    bx(cy - 0.9, cy - 0.75, 0.06, 0.16, 0, 2.45, "Brass")
    bx(cy + 0.75, cy + 0.9, 0.06, 0.16, 0, 2.45, "Brass")
    bx(cy - 0.9, cy + 0.9, 0.06, 0.16, 2.3, 2.45, "Brass")
    bx(cy + 1.0, cy + 1.12, 0.1, 0.14, 1.1, 1.3, "Brass")                    # call button
    bx(cy - 0.3, cy + 0.3, 0.09, 0.13, 2.55, 2.75, "Monitor")
    return B.done()


def elevator_core(prefix, x0, x1, y0, y1, h, stone):
    """The elevator shaft: a solid core rising through the floor, with an elevator on its west and east sides."""
    single(prefix + "_ElevatorShaft", (x0, x1, y0, y1, 0, h, stone))
    single(prefix + "_ElevatorShaftBase", (x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.08, 0, 0.15, "MarbleDark"))
    cy = (y0 + y1) / 2
    elevator_side("Elev_DoorW", x0, cy, -1)
    elevator_side("Elev_DoorE", x1, cy, +1)


def toilet(name, x, y, face):
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.2, 0.42, "TileWhite", seg=10, r2=0.22)
    tx, ty = x - face[0] * 0.3, y - face[1] * 0.3
    B.box(tx - 0.22, tx + 0.22, ty - 0.1, ty + 0.1, 0.42, 0.8, "TileWhite") if face[1] != 0 else \
        B.box(tx - 0.1, tx + 0.1, ty - 0.22, ty + 0.22, 0.42, 0.8, "TileWhite")
    return B.done()


def basin(name, x0, x1, wall_y, mirror=True):
    """Counter with basins against a wall at Y = wall_y (facing -Y), mirror above."""
    B = Builder(name, ((x0 + x1) / 2, wall_y, 0))
    B.box(x0, x1, wall_y - 0.55, wall_y - 0.1, 0.8, 0.88, "MarbleDark")
    B.box(x0 + 0.1, x1 - 0.1, wall_y - 0.5, wall_y - 0.15, 0, 0.8, "WoodDarkPolish")
    if mirror: B.box(x0, x1, wall_y - 0.12, wall_y - 0.1, 1.2, 1.9, "GlassOffice")
    return B.done()


# ================================================================ Schrute Towers lobby (26 x 20)

def lobby():
    reset(51)
    W, D, H = 13.0, 10.0, 6.0
    floor("Lob_Floor", -W, W, -D, D, "Marble", 1.0)
    single("NoCol_LobBand", (-W, W, -0.4, 0.4, 0.0, 0.004, "MarbleDark"), )
    wall("Lob_WallN", "x", D, -W, W, H, "Limestone")
    elevator_core("Lob", -2.0, 2.0, D - 3.6, D - 0.1, H, "LimestoneDark")    # the shaft against the back wall, elevators both sides
    single("Lob_SignSchrute", (-1.9, 1.9, D - 3.68, D - 3.62, 3.4, 4.1, "SignSchrute"))
    wall("Lob_WallE", "y", W, -D, D, H, "Limestone", [(y, y + 2.4, 0.4, 5.6) for y in (-9.0, -5.5, -2.0, 1.5, 5.0)], glass="GlassOffice")
    wall("Lob_WallS", "x", -D, -W, W, H, "Limestone", [(-0.9, 0.9, 0, 2.5)], low=True)
    wall("Lob_WallW", "y", -W, -D, D, H, "Limestone", low=True)
    for side in (-1, 1):                                                       # stairs at both ends, up to a gallery
        xa, xb = sorted((side * 10.6, side * 12.6))
        stairs(f"Lob_Stairs{'E' if side > 0 else 'W'}", xa, xb, 2.4, 9.6, rise=3.6, steps=16)
    for (x, y) in [(-8.0, -4.0), (8.0, -4.0), (-8.0, 4.0), (8.0, 4.0)]:
        B = Builder(f"Lob_Column{x:.0f}{y:.0f}", (x, y, 0))
        B.box(x - 0.35, x + 0.35, y - 0.35, y + 0.35, 0, H, "MarbleDark").box(x - 0.45, x + 0.45, y - 0.45, y + 0.45, 0, 0.3, "Limestone")
        B.done()
    # reception desk in front of the elevator core
    B = Builder("Lob_Reception", (0, 3.7, 0))
    B.box(-2.2, 2.2, 3.4, 4.0, 0, 1.05, "MarbleDark").box(-2.3, 2.3, 3.3, 4.1, 1.05, 1.12, "Laminate")
    B.done()
    chair("Lob_ReceptionChair", 0.0, 4.6, (0, -1), office=True)
    computer("Lob_ReceptionPC", 0.6, 3.85, (0, 1))
    # seating areas either side
    for side in (-1, 1):
        cx = side * 5.6
        sofa(f"Lob_Sofa{side}A", cx - 1.2, cx + 1.2, 0.0, 0.8, (0, -1), "FabricCream")
        sofa(f"Lob_Sofa{side}B", cx - 1.2, cx + 1.2, -2.8, -2.0, (0, 1), "FabricCream")
        table(f"Lob_CoffeeTable{side}", cx - 0.7, cx + 0.7, -1.6, -0.6, h=0.42, top="MarbleDark", legs="Brass")
        single(f"NoCol_LobRug{side}", (cx - 1.8, cx + 1.8, -3.2, 1.2, 0.0, 0.01, "FabricBlue"))
        plant(f"Lob_Plant{side}A", side * 11.8, -8.8)
        plant(f"Lob_Plant{side}B", side * 3.2, -1.4, h=1.0)
        plant(f"Lob_Plant{side}C", side * 4.0, D - 0.8)
    # security check at the entrance: two walk-through gates and a guard desk
    for x in (-1.6, 1.6):
        B = Builder(f"Lob_Gate{x:.0f}", (x, -7.0, 0))
        B.box(x - 0.55, x - 0.45, -7.2, -6.8, 0, 2.2, "Steel").box(x + 0.45, x + 0.55, -7.2, -6.8, 0, 2.2, "Steel")
        B.box(x - 0.55, x + 0.55, -7.2, -6.8, 2.1, 2.25, "Steel")
        B.done()
    B = Builder("Lob_SecurityDesk", (4.4, -7.0, 0))
    B.box(3.4, 5.4, -7.4, -6.7, 0, 1.0, "DeskGrey").box(3.35, 5.45, -7.45, -6.65, 1.0, 1.05, "Laminate")
    B.done()
    computer("Lob_SecurityPC", 4.0, -7.05, (0, -1))
    chair("Lob_GuardChair", 4.6, -6.2, (0, -1), office=True)
    for x in (-3.8, -2.4, 2.4):                                                 # rope posts guiding the queue
        single(f"Lob_Post{x:.0f}", (x - 0.06, x + 0.06, -6.6, -6.48, 0, 0.95, "Brass"))
    door = Builder("Lob_Door", (0, -D, 0))
    door.box(-0.9, 0.9, -D - 0.05, -D + 0.05, 0, 2.5, "GlassOffice").box(-0.02, 0.02, -D - 0.06, -D + 0.06, 0, 2.5, "Steel")
    door.done()
    for i, (x, y) in enumerate([(-6.5, -6), (0, -6), (6.5, -6), (-6.5, 0.5), (0, 0.5), (6.5, 0.5), (-6.5, 7), (6.5, 7)]):
        ceiling_light(f"LightSrc_Bulb_Lobby{i}", x, y, 5.2, 1.6, 0.4)
    export(OUT + "/SchruteLobby.fbx", HERE + "/SchruteLobby.blend")


# ================================================================ Schrute Towers 5th floor: the paper company (22 x 18)

DESKS = [(-3.5, 3.6), (-3.5, 0.4), (-3.5, -2.8), (3.0, 3.6), (3.0, 0.4), (3.0, -2.8)]   # desk-pair centres


def floor5():
    reset(52)
    W, D, H = 11.0, 9.0, 3.0
    N = 11.0                                                                  # north edge: the elevator lobby is 5.5 m deep
    floor("Off_Floor", -7.5, W, -5.5, 5.5, "CarpetOffice", 1.0)
    floor("Off_FloorRestrooms", -W, -7.5, -1.5, 5.5, "TileWhite", 0.5)
    floor("Off_FloorBreak", -W, -7.5, -D, -1.5, "TileKitchen", 0.6)
    floor("Off_FloorLeft", -W, -7.5, 5.5, N, "CarpetOffice", 1.0)
    floor("Off_FloorRooms", -7.5, W, -D, -5.5, "CarpetOffice", 1.0)
    wall("Off_WallN", "x", N, -W, W, H, "PaintOffice")
    floor("Off_FloorLift", -7.5, W, 5.5, N, "Marble", 1.0)                   # the elevator lobby outside the office
    elevator_core("Off", -2.0, 2.0, N - 3.6, N - 0.1, H, "LimestoneDark")
    # the office's front: glass wall with a frosted band, a wide entrance and the company sign above it
    B = Builder("Off_Entrance", (0, 5.5, 0))                                 # wood lower half, framed glass above
    for (a0, a1) in ((-7.5, -0.9), (0.9, W)):
        B.box(a0, a1, 5.42, 5.58, 0, 1.05, "WoodDarkPolish")
        B.box(a0, a1, 5.44, 5.56, 1.05, H, "GlassPartition")
        B.box(a0, a1, 5.4, 5.6, 1.0, 1.1, "Steel")
        B.box(a0, a1, 5.4, 5.6, H - 0.1, H, "Steel")
        x = a0
        while x <= a1 + 0.01:
            B.box(x - 0.04, x + 0.04, 5.41, 5.59, 1.05, H, "Steel")
            x += 1.5
    B.box(-0.9, 0.9, 5.44, 5.56, 2.4, H, "GlassPartition")
    B.done()
    B = Builder("Off_EntranceFrame", (0, 5.5, 0))
    for x in (-0.95, 0.95):
        B.box(x - 0.06, x + 0.06, 5.42, 5.58, 0, 2.3, "Steel")
    B.box(-1.0, 1.0, 5.42, 5.58, 2.3, 2.4, "Steel")
    B.done()
    single("Off_SignPaper", (-1.6, 1.6, 5.46, 5.54, 2.45, 2.9, "SignPaper"))
    single("Off_ReceptionDesk", (2.0, 4.0, 4.3, 4.9, 0, 1.0, "WoodDarkPolish"))
    chair("Off_ReceptionChair", 3.0, 3.8, (0, 1), office=True)
    wall("Off_WallE", "y", W, -D, N, H, "PaintOffice", [(y, y + 2.6, 0.5, 2.7) for y in (-8.5, -5.6, -2.7, 0.2, 3.1, 6.0, 8.2)], glass="GlassOffice")
    wall("Off_WallS", "x", -D, -W, W, H, "PaintOffice", low=True)
    wall("Off_WallW", "y", -W, -D, N, H, "PaintOffice", low=True)
    for side in (-1, 1):                                                       # stair cores in the top corners
        xa, xb = sorted((side * 8.9, side * 10.8))
        stairs(f"Off_Stairs{'E' if side > 0 else 'W'}", xa, xb, N - 3.0, N - 0.2, rise=2.9, steps=12, m="Concrete", rail="Steel")
    # left column: utilities, ladies, mens (doors onto the office); break room below
    wall("Off_WallRooms", "y", -7.5, -D, 5.5, H, "PaintOffice", [(4.0, 4.9, 0, 2.1), (2.0, 2.9, 0, 2.1), (0.0, 0.9, 0, 2.1), (-3.6, -2.4, 0, 2.1)])
    for y, nm in ((3.5, "Util"), (1.5, "Ladies"), (-1.5, "Mens")):
        wall(f"Off_Wall{nm}", "x", y, -W, -7.5, H, "TileWhite" if nm != "Util" else "PaintOffice")
    wall("Off_WallUtilTop", "x", 5.5, -W, -7.5, H, "PaintOffice")
    single("Off_SignWomen", (-7.38, -7.36, 2.1, 2.8, 2.2, 2.5, "SignWomen"))
    single("Off_SignMen", (-7.38, -7.36, 0.1, 0.8, 2.2, 2.5, "SignMen"))
    for k, x in enumerate((-10.4, -9.5)):                                       # utilities: shelving on the back wall
        single(f"Off_UtilShelf{k}", (x - 0.42, x + 0.42, 5.0, 5.42, 0, 2.0, "Steel"))
    single("Off_Boiler", (-10.95, -10.35, 3.58, 4.25, 0, 1.8, "MetalWhite"))   # boiler in the far corner
    B = Builder("Off_MopBucket", (-9.75, 3.85, 0))                              # cleaning tools (opening 2)
    B.cyl(-9.75, 3.85, 0, 0.2, 0.35, "DoorGreen", seg=10, r2=0.23)
    B.box(-9.5, -9.45, 3.8, 3.9, 0, 1.4, "Wood", rot=(0, 0.18, 0), pivot=(-9.47, 3.85, 0))
    B.box(-9.85, -9.35, 3.7, 4.0, 0.3, 0.42, "Linen")
    B.done()
    single("Off_WetFloorSign", (-8.6, -8.4, 5.05, 5.35, 0, 0.6, "SignTaxi"))
    for k, x in enumerate((-10.3, -9.1)):                                       # ladies: stalls + basin
        single(f"Off_LadiesStall{k}", (x - 0.6, x - 0.55, 1.6, 3.4, 0, 2.0, "DeskGrey"))
        toilet(f"Off_LadiesToilet{k}", x, 3.1, (0, -1))
    basin("Off_LadiesBasin", -10.8, -9.6, 1.6 + 0.65, mirror=False)
    for k, x in enumerate((-10.4, -9.6)):                                       # mens: urinals + basin
        single(f"Off_Urinal{k}", (x - 0.2, x + 0.2, 0.5 - 0.05, 0.5 + 0.12, 0.45, 1.2, "TileWhite"))
    toilet("Off_MensToilet", -8.3, 1.1, (0, -1))
    basin("Off_MensBasin", -10.8, -9.4, -1.4 + 0.65, mirror=False)
    # break room
    table("Off_BreakTable", -10.0, -8.6, -6.4, -5.2)
    for k, (x, y, f) in enumerate([(-9.3, -6.9, (0, 1)), (-9.3, -4.7, (0, -1))]):
        chair(f"Off_BreakChair{k}", x, y, f, seat="FabricRed")
    B = Builder("Off_BreakCounter", (-10.6, -3.0, 0))
    B.box(-10.95, -10.35, -4.2, -2.0, 0, 0.9, "Cabinet").box(-10.97, -10.33, -4.2, -2.0, 0.9, 0.94, "Laminate")
    B.box(-10.9, -10.45, -3.6, -3.1, 0.94, 1.2, "MetalWhite")
    B.done()
    B = Builder("Off_CoffeeMachine", (-10.67, -2.4, 0.94))                    # the office coffee machine (opening 3)
    B.box(-10.88, -10.45, -2.65, -2.15, 0.94, 1.32, "MetalDark").box(-10.86, -10.5, -2.6, -2.2, 1.32, 1.36, "Steel")
    B.cyl(-10.6, -2.4, 0.95, 0.07, 0.14, "GlassBrown", seg=8)
    B.done()
    single("Off_BreakFridge", (-10.95, -10.3, -8.8, -8.1, 0, 1.8, "MetalWhite"))
    single("Off_Vending", (-8.5, -7.7, -8.8, -8.2, 0, 1.9, "CarRed"))
    # meeting rooms and cabins along the bottom, behind glass
    wall("Off_GlassFront", "x", -5.5, -7.5, W, H, "GlassPartition", [(-5.4, -4.5, 0, 2.2), (-0.4, 0.5, 0, 2.2), (3.6, 4.5, 0, 2.2), (8.0, 8.9, 0, 2.2)])
    for x in (-2.75, 2.0, 6.5):
        wall(f"Off_GlassSplit{x:.0f}", "y", x, -D, -5.5, H, "GlassPartition")
    for k, (x0, x1) in enumerate(((-7.0, -3.3), (-2.3, 1.6))):                 # meeting rooms
        table(f"Off_MeetingTable{k}", x0 + 0.6, x1 - 0.6, -7.8, -6.6, top="WoodDarkPolish")
        for j in range(3):
            x = x0 + 0.9 + j * ((x1 - x0 - 1.8) / 2)
            chair(f"Off_MeetChair{k}{j}a", x, -8.3, (0, 1), office=True)
            chair(f"Off_MeetChair{k}{j}b", x, -6.1, (0, -1), office=True)
    for k, (x0, x1) in enumerate(((2.0, 6.5), (6.5, 11.0))):                   # cabins
        cx = (x0 + x1) / 2
        table(f"Off_CabinDesk{k}", cx - 1.0, cx + 1.0, -8.2, -7.4, top="WoodDarkPolish", legs="WoodDarkPolish")
        computer(f"Off_CabinPC{k}", cx + 0.4, -7.95, (0, 1))
        chair(f"Off_CabinChair{k}", cx, -8.6, (0, 1), office=True)
        chair(f"Off_CabinGuest{k}", cx - 0.5, -6.8, (0, -1))
        plant(f"Off_CabinPlant{k}", x1 - 0.5, -8.5)
        single(f"Off_CabinFile{k}", (x0 + 0.25, x0 + 0.75, -8.8, -8.3, 0, 1.3, "DeskGrey"))
    # the open office: desk pairs facing each other
    for k, (cx, cy) in enumerate(DESKS):
        table(f"Off_Desk{k}", cx - 0.8, cx + 0.8, cy - 0.8, cy + 0.8, top="Laminate", legs="DeskGrey")
        single(f"Off_DeskScreen{k}", (cx - 0.8, cx + 0.8, cy - 0.02, cy + 0.02, 0.74, 1.15, "FabricBlue"))
        for s, f in ((1, (0, -1)), (-1, (0, 1))):
            computer(f"Off_PC{k}{'N' if s > 0 else 'S'}", cx - 0.2, cy + s * 0.45, f)
            chair(f"Off_Chair{k}{'N' if s > 0 else 'S'}", cx, cy + s * 1.25, f, office=True)
            for p in range(2):
                single(f"NoCol_Paper{k}{s}{p}", (cx + 0.25 + p * 0.06, cx + 0.55 + p * 0.06, cy + s * 0.3 - 0.15, cy + s * 0.3 + 0.15,
                                                0.74, 0.745 + p * 0.003, "Paper"))
    for (x, y) in [(-6.8, 9.8), (6.8, 9.8), (-6.8, 6.2), (6.8, 6.2), (9.8, 5.0), (-0.3, -4.6), (9.8, -4.6)]:
        plant(f"Off_Plant{x:.0f}{y:.0f}", x, y)
    single("Off_Copier", (-6.9, -6.0, -4.9, -4.2, 0, 1.1, "BeigePlastic"))
    single("Off_WaterCooler", (6.3, 6.7, 4.6, 5.0, 0, 1.3, "GlassOffice"))
    for k in range(3):
        single(f"Off_Filing{k}", (9.9, 10.6, -2.6 + k * 0.6, -2.1 + k * 0.6, 0, 1.3, "DeskGrey"))
    for i, (x, y) in enumerate([(-3.5, 5.0), (3.0, 5.0), (-3.5, 1.9), (3.0, 1.9), (-3.5, -1.3), (3.0, -1.3), (-5.0, -7.2), (0.0, -7.2), (4.2, -7.2), (8.7, -7.2), (-9.3, -5.8), (-9.3, 2.5), (-5.0, 7.5), (5.0, 7.5), (-5.0, 9.8), (5.0, 9.8)]):
        ceiling_light(f"LightSrc_Bulb_Office{i}", x, y, 2.85, 1.2, 0.3)
    export(OUT + "/SchruteFloor5.fbx", HERE + "/SchruteFloor5.blend")


# ================================================================ Kevin's house: ground floor (11 x 8)

def house_ground():
    reset(53)
    W, D, H = 5.5, 4.0, 2.8
    floor("House_Floor", -W, W, -D, 0.4, "WoodOak", 1.0)
    floor("House_FloorKitchen", -W, W, 0.4, D, "TileKitchen", 0.6)
    wall("House_WallN", "x", D, -W, W, H, "PaintBlush", [(0.6, 1.5, 0, 2.1), (-4.6, -3.4, 1.0, 2.1), (2.2, 3.2, 1.0, 2.1)], glass="WinPunch")
    wall("House_WallE", "y", W, -D, D, H, "PaintBlush")
    wall("House_WallS", "x", -D, -W, W, H, "PaintBlush", [(0.4, 1.4, 0, 2.1)], low=True)
    wall("House_WallW", "y", -W, -D, D, H, "PaintBlush", low=True)
    for k, (x0, x1) in enumerate(((-4.6, -3.4), (2.2, 3.2))):
        single(f"House_Window{k}", (x0, x1, D - 0.04, D + 0.04, 1.0, 2.1, "WinPunch"))
    single("House_BackDoor", (0.6, 1.5, D - 0.05, D + 0.05, 0, 2.1, "DoorGreen"))
    door = Builder("House_Door", (0.9, -D, 0))
    door.box(0.4, 1.4, -D - 0.05, -D + 0.05, 0, 2.1, "DoorGreen")
    door.done()
    # kitchen: cabinets along the west and north walls, fridge, stove, sink; dining table; bar counter
    B = Builder("Kit_Cabinets", (-4.0, 2.5, 0))
    B.box(-5.4, -4.8, 0.6, 3.9, 0, 0.88, "Cabinet").box(-5.42, -4.75, 0.6, 3.92, 0.88, 0.92, "Laminate")
    B.box(-4.8, -1.9, 3.3, 3.9, 0, 0.88, "Cabinet").box(-4.8, -1.9, 3.25, 3.92, 0.88, 0.92, "Laminate")
    B.box(-5.4, -4.95, 0.6, 3.9, 1.5, 2.2, "Cabinet").box(-4.8, -1.9, 3.55, 3.9, 1.6, 2.2, "Cabinet")
    B.box(-5.35, -4.85, 2.0, 2.6, 0.92, 0.93, "MetalDark")
    B.done()
    B = Builder("Kit_Stove", (-3.3, 3.6, 0))
    B.box(-3.7, -3.0, 3.3, 3.9, 0, 0.92, "MetalWhite").box(-3.68, -3.02, 3.32, 3.88, 0.92, 0.94, "MetalDark")
    for (bx, by) in ((-3.5, 3.45), (-3.2, 3.45), (-3.5, 3.75), (-3.2, 3.75)):
        B.cyl(bx, by, 0.94, 0.08, 0.015, "MetalDark", seg=10)
    B.done()
    single("Kit_Fridge", (-1.8, -1.1, 3.25, 3.9, 0, 1.85, "MetalWhite"))
    table("Kit_DiningTable", -3.9, -2.1, 1.3, 2.4, top="WoodOak", legs="WoodOak")
    for k, (x, y, f) in enumerate([(-3.5, 0.95, (0, 1)), (-2.5, 0.95, (0, 1)), (-3.5, 2.75, (0, -1)), (-2.5, 2.75, (0, -1))]):
        chair(f"Kit_DiningChair{k}", x, y, f, seat="WoodOak", frame="WoodOak")
    B = Builder("Kit_BarCounter", (-1.0, 0.6, 0))
    B.box(-1.9, 0.2, 0.3, 0.75, 0, 1.0, "Cabinet").box(-2.0, 0.3, 0.25, 0.85, 1.0, 1.05, "Laminate")
    B.done()
    for k, x in enumerate((-1.5, -0.6)):
        B = Builder(f"Kit_BarStool{k}", (x, -0.05, 0))
        B.cyl(x, -0.05, 0, 0.04, 0.7, "Chrome", seg=6).cyl(x, -0.05, 0.7, 0.2, 0.06, "FabricRed", seg=10)
        B.done()
    # living room: couch facing the TV, side couch, coffee table, side table
    sofa("Liv_Couch", -4.6, -2.0, -0.9, -0.1, (0, -1), "FabricBrown")
    sofa("Liv_SideCouch", -1.6, -0.8, -3.0, -1.0, (-1, 0), "FabricBrown")
    table("Liv_CoffeeTable", -3.9, -2.7, -2.2, -1.4, h=0.42, top="WoodDarkPolish", legs="WoodDarkPolish")
    table("Liv_SideTable", -5.4, -4.9, -2.6, -1.0, h=0.6, top="WoodDarkPolish", legs="WoodDarkPolish")
    B = Builder("Liv_TV", (-3.3, -3.6, 0))
    B.box(-4.1, -2.5, -3.9, -3.4, 0, 0.55, "WoodDarkPolish").box(-3.6, -3.0, -3.85, -3.4, 0.55, 1.05, "BeigePlastic")
    B.box(-3.55, -3.05, -3.39, -3.38, 0.6, 1.0, "Monitor")
    B.done()
    for k in range(4):                                                          # Kevin's empties
        x, y = -3.7 + k * 0.25, -1.8 + rng.uniform(-0.2, 0.2)
        B = Builder(f"Liv_Bottle{k}", (x, y, 0.42))
        B.cyl(x, y, 0.42, 0.035, 0.25, "GlassBrown" if k % 2 else "GlassGreen", seg=8)
        B.done()
    single("NoCol_LivRug", (-4.4, -1.9, -2.8, -0.9, 0.0, 0.01, "FabricRed"))
    stairs("House_Stairs", 3.4, 5.3, -3.6, 1.8, rise=2.8, steps=13, m="WoodOak", rail="WoodDarkPolish")
    wall("House_StairWall", "y", 3.3, -3.6, 1.8, 1.1, "PaintBlush")
    for i, (x, y) in enumerate([(-3.2, 2.0), (-3.0, -1.6), (1.0, -1.0)]):
        ceiling_light(f"LightSrc_Bulb_House{i}", x, y, 2.7, 0.5, 0.5)
    export(OUT + "/KevinHouse.fbx", HERE + "/KevinHouse.blend")


# ================================================================ Kevin's house: upstairs (11 x 8)

def house_up():
    reset(54)
    W, D, H = 5.5, 4.0, 2.7
    floor("Up_Floor", -W, W, -D, D, "CarpetOffice", 1.0)
    wall("Up_WallN", "x", D, -W, W, H, "PaintSage", [(-3.8, -2.6, 1.0, 2.0), (2.6, 3.6, 1.1, 2.0)], glass="WinPunch")
    for k, (x0, x1) in enumerate(((-3.8, -2.6), (2.6, 3.6))):
        single(f"House_UpWindow{k}", (x0, x1, D - 0.04, D + 0.04, 1.0, 2.0, "WinPunch"))
    wall("Up_WallE", "y", W, -D, D, H, "PaintSage")
    wall("Up_WallS", "x", -D, -W, W, H, "PaintSage", low=True)
    wall("Up_WallW", "y", -W, -D, D, H, "PaintSage", low=True)
    wall("Up_WallBeds", "y", 0.6, -D, D, H, "PaintSage", [(0.4, 1.3, 0, 2.1), (-1.6, -0.7, 0, 2.1)])
    wall("Up_WallBedSplit", "x", 0.0, -W, 0.6, H, "PaintSage")
    wall("Up_WallStorage", "x", 1.6, 1.6, W, H, "PaintSage", [(2.0, 2.9, 0, 2.1)])
    # Kevin's bedroom (north): double bed, bedside table, wardrobe, bottles
    B = Builder("Up_BedKevin", (-3.6, 2.3, 0))
    B.box(-5.3, -3.0, 1.1, 3.4, 0, 0.35, "WoodDarkPolish").box(-5.25, -3.05, 1.15, 3.35, 0.35, 0.55, "Linen")
    B.box(-5.35, -5.25, 1.1, 3.4, 0.35, 1.05, "WoodDarkPolish")
    B.box(-5.2, -4.6, 1.3, 3.2, 0.55, 0.68, "Linen")
    B.blob(-3.8, 2.2, 0.48, 1.5, 1.7, 0.22, "FabricBlue", 0.3)
    B.done()
    single("Up_Nightstand", (-5.3, -4.8, 3.5, 3.9, 0, 0.55, "WoodDarkPolish"))
    single("Up_Wardrobe", (-1.4, 0.4, 3.35, 3.9, 0, 2.0, "WoodDarkPolish"))
    for k in range(3):
        B = Builder(f"Up_Bottle{k}", (-2.6 + k * 0.2, 0.6, 0))
        B.cyl(-2.6 + k * 0.2, 0.6 + rng.uniform(-0.1, 0.1), 0.0, 0.035, 0.25, "GlassBrown", seg=8, rot=(math.pi / 2 if k == 1 else 0, 0, 0))
        B.done()
    # spare bedroom (south): single bed, desk
    B = Builder("Up_BedSpare", (-4.3, -2.0, 0))
    B.box(-5.3, -3.3, -3.7, -2.7, 0, 0.35, "WoodOak").box(-5.25, -3.35, -3.65, -2.75, 0.35, 0.52, "Linen")
    B.done()
    table("Up_Desk", -1.6, -0.2, -0.75, -0.25, top="WoodOak", legs="WoodOak")
    chair("Up_DeskChair", -0.9, -1.1, (0, 1), seat="WoodOak", frame="WoodOak")
    for k in range(3):
        single(f"Up_Box{k}", (-1.3 + k * 0.05, -0.8 + k * 0.05, -3.8, -3.2, k * 0.45, k * 0.45 + 0.45, "Cardboard"))
    # storage with the shower stall Kevin uses in the morning
    for k in range(3):
        single(f"Up_StorageShelf{k}", (1.8 + k * 0.9, 2.5 + k * 0.9, 3.5, 3.9, 0, 1.8, "Steel"))
    B = Builder("Up_Shower", (4.8, 2.6, 0))
    B.box(4.3, 5.4, 1.8, 3.3, 0, 0.08, "TileWhite").box(4.25, 4.3, 1.8, 3.3, 0.08, 2.0, "GlassPartition")
    B.box(5.35, 5.4, 1.8, 3.3, 0, 2.2, "TileWhite")
    B.done()
    stairs("House_UpStairs", 3.4, 5.3, -3.6, 0.6, rise=0.0001, steps=1, m="WoodOak", rail="WoodDarkPolish")
    single("Up_StairWell", (3.4, 5.3, -3.6, 0.6, -0.02, 0.0, "WoodDarkPolish"))
    B = Builder("House_UpRail", (3.3, -1.5, 0))
    B.box(3.25, 3.35, -3.6, 0.6, 0, 1.0, "WoodDarkPolish")
    B.done()
    for i, (x, y) in enumerate([(-3.0, 2.0), (-3.0, -2.0), (2.0, -1.0), (3.6, 2.6)]):
        ceiling_light(f"LightSrc_Bulb_Up{i}", x, y, 2.6, 0.5, 0.5)
    export(OUT + "/KevinHouseUp.fbx", HERE + "/KevinHouseUp.blend")


# ================================================================ Corner Bar (9 x 12)

def bar():
    reset(55)
    W, D, H = 4.5, 6.0, 3.0
    floor("Bar_Floor", -W, W, -D, 3.4, "WoodDarkPolish", 1.0)
    floor("Bar_FloorWash", -W, 0.2, 3.4, D, "TileWhite", 0.5)
    floor("Bar_FloorOffice", 0.2, 2.2, 3.4, D, "WoodOak", 1.0)
    floor("Bar_FloorKitchen", 2.2, W, 3.4, D, "TileKitchen", 0.6)
    wall("Bar_WallN", "x", D, -W, W, H, "BrickClean")
    wall("Bar_WallE", "y", W, -D, D, H, "BrickClean", [(-3.0, -2.0, 0, 2.2), (-5.5, -4.0, 1.0, 2.4), (0.0, 1.5, 1.0, 2.4)], glass="WinPunch")
    wall("Bar_WallS", "x", -D, -W, W, H, "BrickClean", low=True)
    wall("Bar_WallW", "y", -W, -D, D, H, "BrickClean", low=True)
    door = Builder("Bar_Door", (W, -2.5, 0))
    door.box(W - 0.05, W + 0.05, -3.0, -2.0, 0, 2.2, "DoorGreen")
    door.done()
    # back rooms along the top: stalls + washroom, office, kitchen
    wall("Bar_WallBack", "x", 3.4, -W, W, H, "WoodDarkPolish", [(-1.4, -0.6, 0, 2.1), (0.8, 1.6, 0, 2.1), (2.9, 3.7, 0, 2.1)])
    wall("Bar_WallWashOffice", "y", 0.2, 3.4, D, H, "PaintOffice")
    wall("Bar_WallOfficeKitchen", "y", 2.2, 3.4, D, H, "PaintOffice")
    for k, y0 in enumerate((3.4, 4.7)):                                          # two stalls on the left
        single(f"Bar_Stall{k}", (-3.3, -3.25, y0, y0 + 1.3, 0, 2.0, "DeskGrey"))
        toilet(f"Bar_StallToilet{k}", -4.0, y0 + 0.65, (1, 0))
    single("Bar_StallSplit", (-W, -3.3, 4.68, 4.72, 0, 2.0, "DeskGrey"))
    for k, x in enumerate((-2.4, -1.7, -1.0)):                                  # urinals on the north wall
        single(f"Bar_Urinal{k}", (x - 0.2, x + 0.2, D - 0.32, D - 0.12, 0.45, 1.2, "TileWhite"))
    basin("Bar_Basin", -0.5, 0.1, D)
    table("Bar_OfficeDesk", 0.4, 1.9, 5.0, 5.7, top="WoodDarkPolish", legs="WoodDarkPolish")
    chair("Bar_OfficeChair", 1.1, 4.6, (0, 1), office=True)
    single("Bar_Safe", (1.5, 2.0, 3.6, 4.1, 0, 0.8, "MetalDark"))
    B = Builder("Bar_KitchenLine", (3.35, 5.5, 0))
    B.box(2.4, 4.4, 5.3, 5.9, 0, 0.9, "MetalWhite").box(2.4, 4.4, 5.3, 5.9, 0.9, 0.95, "MetalDark")
    B.box(4.0, 4.4, 3.6, 4.6, 0, 1.85, "MetalWhite")
    B.done()
    # the bar: counter, brass rail, back bar shelves, stools
    B = Builder("Bar_Counter", (2.4, 1.5, 0))
    B.box(0.5, 4.3, 1.2, 1.8, 0, 1.05, "WoodDarkPolish").box(0.4, 4.4, 1.1, 1.85, 1.05, 1.12, "WoodDarkPolish")
    B.box(0.5, 4.3, 1.05, 1.1, 0.2, 0.25, "Brass")
    B.done()
    single("Bar_BackBar", (0.5, 4.3, 3.15, 3.3, 0.9, 2.6, "BackBar"))
    single("Bar_BackCounter", (0.5, 4.3, 2.8, 3.3, 0, 0.9, "WoodDarkPolish"))
    for k, x in enumerate((1.3, 2.3, 3.3)):
        B = Builder(f"Bar_Stool{k}", (x, 0.6, 0))
        B.cyl(x, 0.6, 0, 0.04, 0.72, "Chrome", seg=6).cyl(x, 0.6, 0.72, 0.21, 0.07, "FabricRed", seg=10).cyl(x, 0.6, 0.0, 0.2, 0.03, "Chrome", seg=8)
        B.done()
    for k, (x, y) in enumerate([(-0.8, 1.4), (-0.8, -0.6)]):                   # small round high tables
        B = Builder(f"Bar_HighTable{k}", (x, y, 0))
        B.cyl(x, y, 0, 0.05, 1.0, "Chrome", seg=6).cyl(x, y, 1.0, 0.35, 0.04, "WoodDarkPolish", seg=10)
        B.done()
    # booths: three along the west wall, two along the south
    for k, yc in enumerate((1.8, -0.9, -3.6)):
        sofa(f"Bar_BoothW{k}a", -W + 0.05, -2.6, yc + 0.55, yc + 1.05, (0, -1), "FabricRed")
        sofa(f"Bar_BoothW{k}b", -W + 0.05, -2.6, yc - 1.05, yc - 0.55, (0, 1), "FabricRed")
        table(f"Bar_BoothTableW{k}", -W + 0.15, -2.9, yc - 0.45, yc + 0.45, top="WoodDarkPolish", legs="Brass")
    for k, xc in enumerate((-0.6, 2.4)):
        sofa(f"Bar_BoothS{k}a", xc - 1.2, xc - 0.7, -D + 0.05, -4.2, (1, 0), "FabricRed")
        sofa(f"Bar_BoothS{k}b", xc + 0.7, xc + 1.2, -D + 0.05, -4.2, (-1, 0), "FabricRed")
        table(f"Bar_BoothTableS{k}", xc - 0.5, xc + 0.5, -D + 0.15, -4.4, top="WoodDarkPolish", legs="Brass")
    single("LightSrc_NeonBar_Bar", (1.6, 3.2, 3.12, 3.14, 2.2, 2.6, "SignBar"))
    for i, (x, y) in enumerate([(2.4, 1.5), (-3.5, 1.8), (-3.5, -0.9), (-3.5, -3.6), (-0.6, -4.9), (2.4, -4.9), (-0.8, 0.4), (-1.6, 4.7), (3.3, 4.7)]):
        ceiling_light(f"LightSrc_Bulb_Bar{i}", x, y, 2.7, 0.35, 0.35)
    export(OUT + "/CornerBar.fbx", HERE + "/CornerBar.blend")


if __name__ == "__main__":
    for build in globals().get("BUILD", [lobby, floor5, house_ground, house_up, bar]):
        build()
