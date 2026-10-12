# A street phone booth like the one in the doc's Shackleton reference (a payphone on a post with a hood and side
# wings, NYC style). Exports Assets/Hitman/Art/Props/PhoneBooth.fbx at the origin, the phone facing +Y (Unity +Z).
import sys, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit, shop_textures
for m in (apt_textures, street_textures, street_kit): importlib.reload(m)
importlib.reload(shop_textures)
from street_kit import *


def phone_booth(name="PhoneBooth"):
    reset(71)
    B = Builder(name, (0, 0, 0))
    B.box(-0.28, 0.28, -0.22, 0.22, 0, 0.05, "Concrete")                       # base plate
    B.box(-0.07, 0.07, -0.07, 0.07, 0.05, 0.98, "Steel")                       # post
    B.box(-0.31, 0.31, -0.2, 0.2, 0.95, 2.05, "Steel")                         # housing
    B.box(-0.24, 0.24, 0.2, 0.225, 1.05, 1.95, "Payphone")                      # the phone panel (fitted)
    for x in (-0.31, 0.31):                                                    # side wings
        B.box(x - 0.015, x + 0.015, 0.2, 0.55, 0.95, 2.05, "Steel")
    B.box(-0.36, 0.36, -0.22, 0.6, 2.05, 2.11, "Steel")                        # hood
    B.box(-0.3, 0.3, -0.02, 0.02, 2.11, 2.41, "SignPhone")                     # sign on top
    B.box(0.12, 0.2, 0.225, 0.29, 1.35, 1.7, "MetalDark")                      # handset
    B.box(0.15, 0.17, 0.23, 0.27, 1.0, 1.35, "MetalDark")                      # cord
    B.done()
    export(PROJECT + "/Assets/Hitman/Art/Props/PhoneBooth.fbx", HERE + "/PhoneBooth.blend")


if __name__ == "__main__":
    phone_booth()
