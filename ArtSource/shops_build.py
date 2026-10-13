# Builds the shops added to the design doc on 2026-10-11 and exports one FBX each:
#   Supermarket (Shop n Save, Shackleton Ave), Barber (Shackleton Ave), Mall (Business District).
# Same conventions as interiors_build.py (which it borrows walls / furniture from): built around the origin,
# Blender X = Unity X, Blender Y = Unity Z, camera from the south-west so south and west walls are cut low.
# Names the game uses: *_Door (the way out), Shop_Shelf* / Shop_Freezer / Shop_VegBin* (take things),
# Shop_Till (pay), Barber_Chair* (haircut), Mall_SuitCounter (buy the suit), Mall_CoffeeCounter, Mall_FoodKiosk,
# Mall_BarCounter, Payphone_* (phones), LightSrc_* (lights).
import sys, math, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit, interiors_build, shop_textures
importlib.reload(interiors_build)           # (reloads the shared texture library itself)
importlib.reload(shop_textures)             # so the shop textures are registered after that
from street_kit import *
from interiors_build import wall, seg, floor, chair, table, plant, sofa, ceiling_light, basin, T, LOW

OUT = PROJECT + "/Assets/Hitman/Art/Interiors"


def shelf(name, x0, x1, y0, y1, h=1.8, face="Goods", frame="ShelfGrey"):
    """Gondola shelving: grey frame, goods on both long faces."""
    B = Builder(name, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, 0.12, frame)
    long_x = (x1 - x0) >= (y1 - y0)
    if long_x:
        B.box(x0, x1, y0 + 0.04, y1 - 0.04, 0.12, h, face, tile=1.7)
        B.box(x0 - 0.03, x0 + 0.03, y0, y1, 0, h + 0.05, frame).box(x1 - 0.03, x1 + 0.03, y0, y1, 0, h + 0.05, frame)
    else:
        B.box(x0 + 0.04, x1 - 0.04, y0, y1, 0.12, h, face, tile=1.7)
        B.box(x0, x1, y0 - 0.03, y0 + 0.03, 0, h + 0.05, frame).box(x0, x1, y1 - 0.03, y1 + 0.03, 0, h + 0.05, frame)
    B.box(x0, x1, y0, y1, h, h + 0.04, frame)
    return B.done()


def payphone(name, x, y, face):
    """Wall payphone box; face = direction the handset faces (the side you stand on)."""
    B = Builder(name, (x, y, 0))
    if face[1] != 0:
        B.box(x - 0.2, x + 0.2, y - 0.12, y + 0.12, 0.9, 1.75, "Payphone")
        B.box(x - 0.28, x + 0.28, y - 0.18, y + 0.18, 1.75, 1.85, "Steel")
    else:
        B.box(x - 0.12, x + 0.12, y - 0.2, y + 0.2, 0.9, 1.75, "Payphone")
        B.box(x - 0.18, x + 0.18, y - 0.28, y + 0.28, 1.75, 1.85, "Steel")
    return B.done()


def round_table(name, x, y, r=0.45, h=0.74, top="Laminate"):
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.25, 0.04, "Steel", seg=8).cyl(x, y, 0.04, 0.04, h - 0.04, "Steel", seg=6)
    B.cyl(x, y, h - 0.04, r, 0.04, top, seg=12)
    return B.done()


def stool(name, x, y, h=0.72):
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.04, h, "Chrome", seg=6).cyl(x, y, h, 0.2, 0.06, "FabricRed", seg=10).cyl(x, y, 0.0, 0.19, 0.03, "Chrome", seg=8)
    return B.done()


# ================================================================ Shop n Save supermarket (12 x 14)

def supermarket():
    reset(61)
    W, D, H = 6.0, 7.0, 3.2
    floor("Shop_Floor", -W, W, -D, D, "FloorShop", 2.0)
    wall("Shop_WallN", "x", D, -W, W, H, "PaintOffice")
    wall("Shop_WallE", "y", W, -D, D, H, "PaintOffice")
    wall("Shop_WallS", "x", -D, -W, W, H, "PaintOffice", [(0.9, 2.5, 0, 2.3)], low=True)
    wall("Shop_WallW", "y", -W, -D, D, H, "PaintOffice", low=True)
    single("Shop_SignInside", (-2.0, 2.0, D - 0.12, D - 0.08, 2.3, 2.9, "SignShopNSave"))
    door = Builder("Shop_Door", (1.7, -D, 0))
    door.box(0.9, 2.5, -D - 0.05, -D + 0.05, 0, 2.3, "GlassOffice").box(1.68, 1.72, -D - 0.06, -D + 0.06, 0, 2.3, "Steel")
    door.done()
    # vegetables along the back wall and a produce table in front of them
    for k, (x0, x1) in enumerate(((-3.6, 1.2), (1.2, 5.9))):
        B = Builder(f"Shop_VegBin{k}", ((x0 + x1) / 2, 6.1, 0))
        B.box(x0, x1, 5.3, 6.9, 0, 0.7, "WoodOak").box(x0 + 0.05, x1 - 0.05, 5.35, 6.85, 0.7, 0.8, "Produce")
        B.box(x0, x1, 6.6, 6.9, 0.7, 1.3, "WoodOak")
        B.done()
    B = Builder("Shop_VegTable", (-0.6, 3.8, 0))
    B.box(-3.0, 1.8, 3.45, 4.15, 0, 0.75, "WoodOak").box(-2.95, 1.75, 3.5, 4.1, 0.75, 0.85, "Produce")
    B.done()
    # tall shelving on the west wall (pest control at the bottom end), four aisles, one on the east wall
    shelf("Shop_ShelfWestTop", -W + 0.05, -W + 0.65, 1.4, 6.8, h=2.2)
    shelf("Shop_ShelfPest", -W + 0.05, -W + 0.65, -3.6, 1.0, h=2.0)
    single("Shop_SignPest", (-W + 0.67, -W + 0.69, -2.8, 0.2, 2.05, 2.4, "SignPest"))
    for k, x in enumerate((-3.2, -1.0, 1.2)):
        shelf(f"Shop_Shelf{k}", x - 0.35, x + 0.35, -3.4, 2.2)
    shelf("Shop_ShelfEast", W - 0.65, W - 0.05, -2.8, 4.6, h=2.0)
    # drinks freezer at the foot of the first aisle
    B = Builder("Shop_Freezer", (-3.2, -4.1, 0))
    B.box(-3.75, -2.65, -4.5, -3.75, 0, 1.95, "MetalWhite").box(-3.7, -2.7, -4.52, -4.48, 0.1, 1.9, "Freezer")
    B.done()
    # the till: an L-shaped counter in the bottom right corner, register on top
    B = Builder("Shop_Till", (4.2, -4.6, 0))
    B.box(2.9, 5.2, -4.9, -4.3, 0, 0.92, "Cabinet").box(2.85, 5.25, -4.95, -4.25, 0.92, 0.96, "Laminate")
    B.box(4.6, 5.2, -4.3, -3.2, 0, 0.92, "Cabinet").box(4.55, 5.25, -4.3, -3.15, 0.92, 0.96, "Laminate")
    B.box(3.3, 3.75, -4.75, -4.45, 0.96, 1.22, "BeigePlastic").box(3.35, 3.7, -4.46, -4.44, 1.0, 1.18, "Monitor")
    B.box(4.7, 5.15, -3.9, -3.3, 0.96, 1.5, "Steel")                                  # cigarettes rack
    B.done()
    # a stack of red baskets by the door
    B = Builder("Shop_Baskets", (0.1, -6.2, 0))
    for k in range(5):
        B.box(-0.2, 0.4, -6.45, -5.95, k * 0.12, k * 0.12 + 0.1, "BasketRed")
    B.done()
    single("NoCol_ShopMat", (0.8, 2.6, -6.95, -6.1, 0.0, 0.01, "FabricBlue"))
    for i, (x, y) in enumerate([(-3.5, 5.0), (1.5, 5.0), (-2.1, 0.0), (0.1, 0.0), (3.5, 0.0), (-2.1, -4.0), (3.0, -5.0), (-4.8, -1.0)]):
        ceiling_light(f"LightSrc_Bulb_Shop{i}", x, y, 3.0, 1.2, 0.3)
    export(OUT + "/Supermarket.fbx", HERE + "/Supermarket.blend")


# ================================================================ Barber (9 x 7)

def barber():
    reset(62)
    W, D, H = 4.5, 3.5, 3.0
    floor("Barber_Floor", -W, W, -D, D, "FloorBarber", 2.5)
    wall("Barber_WallN", "x", D, -W, W, H, "PaintSage")
    wall("Barber_WallE", "y", W, -D, D, H, "PaintSage")
    wall("Barber_WallS", "x", -D, -W, W, H, "PaintSage", [(-0.5, 0.5, 0, 2.2), (-3.8, -1.2, 0.9, 2.3)], low=True)
    wall("Barber_WallW", "y", -W, -D, D, H, "PaintSage", low=True)
    door = Builder("Barber_Door", (0, -D, 0))
    door.box(-0.5, 0.5, -D - 0.05, -D + 0.05, 0, 2.2, "DoorGreen")
    door.done()
    # mirror wall and the counter under it
    single("Barber_Mirror", (-4.2, 2.4, D - 0.12, D - 0.1, 1.05, 2.1, "GlassOffice"))
    B = Builder("Barber_Counter", (-0.9, 3.2, 0))
    B.box(-4.2, 2.4, 2.9, 3.4, 0, 0.85, "WoodDarkPolish").box(-4.25, 2.45, 2.85, 3.42, 0.85, 0.9, "MarbleDark")
    for x in (-3.6, -2.0, 0.6):
        B.cyl(x, 3.1, 0.9, 0.04, 0.22, "GlassGreen", seg=8)
    B.done()
    # two barber chairs facing the mirror
    for k, x in enumerate((-2.6, 0.4)):
        B = Builder(f"Barber_Chair{k}", (x, 1.9, 0))
        B.cyl(x, 1.9, 0, 0.32, 0.06, "Chrome", seg=10).cyl(x, 1.9, 0.06, 0.08, 0.38, "Chrome", seg=8)
        B.box(x - 0.3, x + 0.3, 1.6, 2.2, 0.44, 0.58, "LeatherBlack")
        B.box(x - 0.3, x + 0.3, 1.52, 1.62, 0.58, 1.25, "LeatherBlack")
        B.box(x - 0.36, x - 0.3, 1.65, 2.2, 0.58, 0.72, "Chrome").box(x + 0.3, x + 0.36, 1.65, 2.2, 0.58, 0.72, "Chrome")
        B.box(x - 0.2, x + 0.2, 2.25, 2.45, 0.1, 0.16, "Chrome")                          # footrest
        B.done()
    # waiting chairs along the east wall, magazines, coat stand, the pole
    for k, y in enumerate((-1.6, -0.8, 0.0)):
        chair(f"Barber_WaitChair{k}", W - 0.4, y, (-1, 0), seat="LeatherBlack", frame="Chrome")
    table("Barber_MagTable", W - 0.7, W - 0.15, 0.5, 1.1, h=0.45, top="WoodDarkPolish", legs="Chrome")
    B = Builder("Barber_Pole", (-4.1, -3.1, 0))
    B.cyl(-4.1, -3.1, 0.6, 0.1, 1.2, "BarberPole", seg=10).cyl(-4.1, -3.1, 1.8, 0.13, 0.12, "Chrome", seg=10)
    B.done()
    single("Barber_Sink", (2.9, 3.6, 2.9, 3.45, 0, 0.9, "MetalWhite"))
    for i, (x, y) in enumerate([(-2.6, 1.5), (0.4, 1.5), (3.2, -1.0)]):
        ceiling_light(f"LightSrc_Bulb_Barber{i}", x, y, 2.85, 0.5, 0.5)
    export(OUT + "/Barber.fbx", HERE + "/Barber.blend")


# ================================================================ The mall (26 x 20)

def mall():
    reset(63)
    W, D, H = 13.0, 10.0, 6.0
    floor("Mall_Floor", -W, W, -D, D, "FloorMall", 2.0)
    wall("Mall_WallN", "x", D, -W, W, H, "Limestone")
    wall("Mall_WallE", "y", W, -D, D, H, "Limestone")
    wall("Mall_WallS", "x", -D, -W, W, H, "Limestone", low=True)
    wall("Mall_WallW", "y", -W, -D, D, H, "Limestone", [(-1.5, 1.5, 0, 3.0)], low=True)
    door = Builder("Mall_Door", (-W, 0, 0))
    door.box(-W - 0.05, -W + 0.05, -1.5, 1.5, 0, 3.0, "GlassOffice").box(-W - 0.06, -W + 0.06, -0.03, 0.03, 0, 3.0, "Steel")
    door.done()

    def front(name, x0, x1, y, open_shop, sign, s=-1, low_front=False):
        """Shop front along X at Y = y facing s (-1 = south / the concourse). Open shops: glass, a door gap and a
        sign above; closed ones: a shutter and CLOSED. low_front for the row nearest the camera."""
        top = 1.2 if low_front else 4.0
        B = Builder(name, ((x0 + x1) / 2, y, 0))
        if open_shop:
            mid = (x0 + x1) / 2
            for (a, b) in ((x0 + 0.3, mid - 0.8), (mid + 0.8, x1 - 0.3)):
                B.box(a, b, y - 0.06, y + 0.06, 0, 0.4, "Steel").box(a, b, y - 0.03, y + 0.03, 0.4, top - 0.1, "GlassOffice")
            B.box(x0 + 0.3, x1 - 0.3, y - 0.08, y + 0.08, top - 0.1, top, "Steel")
        else:
            B.box(x0 + 0.3, x1 - 0.3, y - 0.05, y + 0.05, 0, top, "MetalWhite")          # clean roller shutter
        for x in (x0, x1):
            B.box(x - 0.3, x + 0.3, y - 0.3, y + 0.3, 0, H if not low_front else top + 0.2, "Limestone")   # pillars
        if not low_front:
            B.box(x0, x1, y - 0.15, y + 0.15, top, H, "Limestone")                                   # wall above
        B.done()
        if sign:
            if low_front:     # a standing sign board in front of the low shop front
                single(f"{name}Sign", (x0 + 2.0, x1 - 2.0, y + s * 0.5 - 0.04, y + s * 0.5 + 0.04, 1.3, 2.0, sign),
                       (x0 + 2.1, x0 + 2.2, y + s * 0.5 - 0.04, y + s * 0.5 + 0.04, 0, 1.3, "Steel"),
                       (x1 - 2.2, x1 - 2.1, y + s * 0.5 - 0.04, y + s * 0.5 + 0.04, 0, 1.3, "Steel"))
            else:
                single(f"{name}Sign", (x0 + 1.0, x1 - 1.0, y + s * 0.2 - 0.04, y + s * 0.2 + 0.04, top + 0.3, top + 1.3, sign))

    # top row: suit shop, supermarket, bar;  bottom row: closed, coffee, barber;  east: closed
    front("Mall_FrontSuits", -W, -4.5, 5.0, True, "SignSuits")
    front("Mall_FrontClosedN", -4.5, 4.5, 5.0, True, "SignShopNSave")         # (a Shop n Save now)
    front("Mall_FrontBar", 4.5, W, 5.0, True, "SignBar")
    front("Mall_FrontClosedSW", -W, -4.5, -5.0, False, "SignClosed", s=1, low_front=True)
    front("Mall_FrontCoffee", -4.5, 4.5, -5.0, False, "SignCoffee", s=1, low_front=True)   # (Daily Grind: bankrupt, shuttered)
    front("Mall_FrontClosedSE", 4.5, W, -5.0, True, "SignBarber", s=1, low_front=True)   # (a barber now)
    for x in (-4.5, 4.5):
        wall(f"Mall_SplitN{x:.0f}", "y", x, 5.0, D, H, "Limestone")
        wall(f"Mall_SplitS{x:.0f}", "y", x, -D, -5.0, H, "Limestone", low=True)
    # east shop: shuttered, set well back so the whole of the bar's front opens onto the concourse
    EX = 12.0
    B = Builder("Mall_FrontClosedE", (EX, 0, 0))
    B.box(EX - 0.05, EX + 0.05, -4.7, 4.7, 0, 4.0, "MetalWhite").box(EX - 0.15, EX + 0.15, -5.0, 5.0, 4.0, H, "Limestone")
    B.done()
    single("Mall_FrontClosedESign", (EX - 0.25, EX - 0.17, -2.0, 2.0, 4.3, 5.3, "SignClosed"))

    # suit shop: racks of suits, mirror, counter
    for k, y in enumerate((6.4, 8.2)):
        B = Builder(f"Mall_SuitRack{k}", (-9.5, y, 0))
        B.box(-11.8, -7.0, y - 0.03, y + 0.03, 1.55, 1.6, "Chrome")
        for x in (-11.8, -7.0): B.box(x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0, 1.6, "Chrome")
        for j in range(9):
            x = -11.5 + j * 0.52
            B.box(x - 0.2, x + 0.2, y - 0.07, y + 0.07, 0.55, 1.5, "FabricBlue" if j % 3 else "LeatherBlack")
        B.done()
    B = Builder("Mall_SuitCounter", (-6.0, 8.6, 0))
    B.box(-6.9, -5.1, 8.2, 8.9, 0, 1.0, "WoodDarkPolish").box(-6.95, -5.05, 8.15, 8.95, 1.0, 1.05, "MarbleDark")
    B.done()
    single("Mall_SuitMirror", (-12.9, -12.85, 6.0, 7.4, 0.2, 2.2, "GlassOffice"))
    # the bar in the top right
    B = Builder("Mall_BarCounter", (8.8, 7.2, 0))
    B.box(6.0, 11.8, 6.8, 7.4, 0, 1.05, "WoodDarkPolish").box(5.95, 11.85, 6.75, 7.45, 1.05, 1.1, "WoodDarkPolish")
    B.done()
    single("Mall_BackBar", (6.0, 11.8, 9.75, 9.9, 0.9, 2.6, "BackBar"))
    for k, x in enumerate((6.8, 8.0, 9.2, 10.4)):
        stool(f"Mall_BarStool{k}", x, 6.3)
    # the coffee shop in the bottom middle
    B = Builder("Mall_CoffeeCounter", (0, -8.6, 0))
    B.box(-3.0, 1.5, -8.9, -8.3, 0, 1.0, "Cabinet").box(-3.05, 1.55, -8.95, -8.25, 1.0, 1.04, "WoodOak")
    B.box(-2.6, -1.9, -8.8, -8.4, 1.04, 1.45, "Chrome")                                        # espresso machine
    B.done()
    for k, x in enumerate((-2.5, 0.5, 3.2)):
        round_table(f"Mall_CoffeeTable{k}", x, -6.4, r=0.35)
        chair(f"Mall_CoffeeChair{k}", x, -7.0, (0, 1), seat="FabricBrown", frame="Steel")

    # the Shop n Save, top middle: shelves of goods, fruit & veg, the drinks fridge, a till by the door
    for k, x in enumerate((-3.2, -0.6)):
        B = Builder(f"Mall_MarketShelf{k}", (x, 7.9, 0))
        B.box(x - 0.35, x + 0.35, 6.2, 9.6, 0, 1.8, "ShelfGrey")
        for side in (-1, 1): B.box(x + side * 0.36 - 0.02, x + side * 0.36 + 0.02, 6.25, 9.55, 0.15, 1.75, "Goods")
        B.done()
    B = Builder("Mall_MarketVeg", (1.6, 9.2, 0))
    B.box(0.9, 2.3, 8.8, 9.7, 0, 0.7, "WoodOak").box(0.95, 2.25, 8.85, 9.65, 0.7, 0.75, "Produce")
    B.done()
    single("Mall_MarketFreezer", (3.4, 4.2, 8.6, 9.9, 0, 2.0, "Freezer"))
    B = Builder("Mall_MarketTill", (2.6, 6.1, 0))
    B.box(1.9, 3.4, 5.8, 6.4, 0, 0.95, "MetalWhite").box(1.85, 3.45, 5.75, 6.45, 0.95, 1.0, "Laminate")
    B.box(2.9, 3.2, 6.0, 6.25, 1.0, 1.25, "BeigePlastic")
    B.done()
    # the barber, bottom right: two chairs facing a mirror, a pole outside
    single("Mall_BarberMirror", (5.4, 12.6, -9.92, -9.88, 1.0, 2.2, "GlassOffice"))
    single("Mall_BarberCounter", (5.4, 12.6, -9.9, -9.5, 0, 0.9, "Cabinet"))
    for k, x in enumerate((7.0, 10.5)):
        B = Builder(f"Mall_BarberChair{k}", (x, -8.6, 0))
        B.cyl(x, -8.6, 0, 0.25, 0.4, "Chrome", seg=10)
        B.box(x - 0.32, x + 0.32, -8.9, -8.3, 0.4, 0.62, "LeatherBlack").box(x - 0.32, x + 0.32, -8.3, -8.15, 0.62, 1.4, "LeatherBlack")
        B.box(x - 0.36, x - 0.3, -8.9, -8.3, 0.62, 0.85, "Chrome").box(x + 0.3, x + 0.36, -8.9, -8.3, 0.62, 0.85, "Chrome")
        B.done()
    B = Builder("Mall_BarberPole", (12.4, -4.6, 0))
    B.cyl(12.4, -4.6, 0, 0.12, 2.0, "BarberPole", seg=10)
    B.done()

    # the concourse: fountain, food court, plastic trees, benches, hoardings, flags
    B = Builder("Mall_Fountain", (2.5, 0, 0))
    B.cyl(2.5, 0, 0, 2.0, 0.5, "MarbleDark", seg=16).cyl(2.5, 0, 0.5, 1.85, 0.02, "Water", seg=16)
    B.cyl(2.5, 0, 0.5, 0.25, 0.9, "MarbleDark", seg=10).cyl(2.5, 0, 1.4, 0.8, 0.12, "MarbleDark", seg=12, r2=0.5)
    B.cyl(2.5, 0, 1.52, 0.7, 0.02, "Water", seg=12).cyl(2.5, 0, 1.52, 0.12, 0.6, "MarbleDark", seg=8)
    B.done()
    for k, (x, y) in enumerate([(-8.6, 2.4), (-6.6, 2.4), (-8.6, 0.0), (-6.6, 0.0), (-8.6, -2.4), (-6.6, -2.4)]):
        round_table(f"Mall_FoodTable{k}", x, y, r=0.5)
        chair(f"Mall_FoodChair{k}a", x - 0.75, y, (1, 0), seat="FabricRed", frame="Steel")
        chair(f"Mall_FoodChair{k}b", x + 0.75, y, (-1, 0), seat="FabricRed", frame="Steel")
    B = Builder("Mall_FoodKiosk", (-11.6, 3.6, 0))
    B.box(-12.9, -10.4, 2.9, 4.4, 0, 1.0, "MetalWhite").box(-12.95, -10.35, 2.85, 4.45, 1.0, 1.05, "Laminate")
    for x in (-12.8, -10.5): B.box(x - 0.05, x + 0.05, 2.9, 3.0, 1.05, 2.5, "Steel")
    B.box(-12.9, -10.4, 2.85, 3.05, 2.5, 3.0, "SignFoodCourt")
    B.done()
    for k, (x, y) in enumerate([(-3.2, 3.6), (7.6, 3.6), (-3.2, -3.6), (7.6, -3.6)]):
        B = Builder(f"Mall_Planter{k}", (x, y, 0))
        B.box(x - 0.6, x + 0.6, y - 0.6, y + 0.6, 0, 0.55, "Limestone").box(x - 0.52, x + 0.52, y - 0.52, y + 0.52, 0.55, 0.57, "Dirt")
        B.cyl(x, y, 0.55, 0.07, 1.6, "Bark", seg=6)
        for j in range(4):
            B.blob(x + rng.uniform(-0.4, 0.4), y + rng.uniform(-0.4, 0.4), 1.6 + j * 0.35, 1.2, 1.2, 0.8, "LeavesBright", 0.25)
        B.done()
    bench("Mall_Bench0", 2.5, 3.2, along_y=False)
    bench("Mall_Bench1", 2.5, -3.2, along_y=False)
    bench("Mall_Bench2", 5.6, 0.0, along_y=True)
    for k, (x, poster) in enumerate([(-4.5, "PosterCola"), (4.5, "PosterSale")]):          # hoardings on the pillars
        single(f"Mall_Hoarding{k}", (x - 1.3, x + 1.3, 4.6, 4.66, 1.0, 3.6, poster))
    single("Mall_HoardingE", (EX - 0.2, EX - 0.16, -4.6, -2.6, 1.2, 3.4, "PosterFly"))
    single("Mall_HoardingW", (-12.86, -12.82, 4.8, 7.0, 3.0, 5.4, "PosterSale"))
    flags = Builder("NoCol_MallFlags", (0, 0, 5))
    for y in (-2.6, 2.6):
        flags.box(-11, 11, y - 0.01, y + 0.01, 5.48, 5.5, "Steel")                            # the wire
        for j in range(12):
            x = -10.5 + j * 1.85
            flags.box(x - 0.35, x + 0.35, y - 0.02, y + 0.02, 4.5, 5.48, ["FlagRed", "FlagBlue", "FlagYellow"][j % 3])
    flags.done()
    payphone("Payphone_Mall", -12.75, -3.0, (1, 0))
    for i, (x, y) in enumerate([(-8.0, 0.0), (-3.0, 0.0), (2.5, 0.0), (7.5, 0.0), (-8.7, 7.5), (0.0, 7.5), (8.7, 7.5),
                                (-8.7, -7.5), (0.0, -7.5), (8.7, -7.5), (11.0, 0.0)]):
        ceiling_light(f"LightSrc_Bulb_Mall{i}", x, y, 5.6, 1.6, 0.4)
    export(OUT + "/Mall.fbx", HERE + "/Mall.blend")


if __name__ == "__main__":
    for build in globals().get("BUILD", [supermarket, barber, mall]):
        build()
