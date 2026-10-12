# Better 70s cars for the street traffic (2026-10-11): chamfered, tapered bodies with real wheel arches, a sloped
# glasshouse with a roof and pillars, chrome bumpers and grille, round headlights, tail lights, plates, door seams,
# mirrors and hubcaps. Same conventions as street_kit.vehicle: at the origin, facing +Y (Unity +Z), wheels on z = 0.
import math
from street_kit import Builder


def ring(B, stations, m, tile=1.0, chamfer=0.07):
    """A body made of cross-sections along Y. stations = [(y, half_width, z_bottom, z_top), ...] from back to front.
    Each section is a chamfered rectangle; neighbouring sections are joined and the two ends are capped."""
    verts, faces = [], []
    for (y, hw, z0, z1) in stations:
        c = min(chamfer, hw * 0.3, (z1 - z0) * 0.3)
        verts += [(-hw, y, z0 + c), (-hw + c, y, z0), (hw - c, y, z0), (hw, y, z0 + c),
                  (hw, y, z1 - c), (hw - c, y, z1), (-hw + c, y, z1), (-hw, y, z1 - c)]
    n = 8
    for s in range(len(stations) - 1):
        a, b = s * n, (s + 1) * n
        for k in range(n):
            faces.append([a + k, a + (k + 1) % n, b + (k + 1) % n, b + k])
    faces.append(list(range(n))[::-1])
    last = (len(stations) - 1) * n
    faces.append([last + k for k in range(n)])
    B.mesh(verts, faces, m, tile)


def glasshouse(B, y_back, y_front, hw_low, hw_top, z_low, z_top, slope_back, slope_front, glass, paint):
    """Cabin: glass all round with sloped windscreen / back window, a painted roof and pillars."""
    yb_top, yf_top = y_back + slope_back, y_front - slope_front
    verts = [(-hw_low, y_back, z_low), (hw_low, y_back, z_low), (hw_low, y_front, z_low), (-hw_low, y_front, z_low),
             (-hw_top, yb_top, z_top), (hw_top, yb_top, z_top), (hw_top, yf_top, z_top), (-hw_top, yf_top, z_top)]
    faces = [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [4, 5, 6, 7]]
    B.mesh(verts, faces, glass)
    # the roof, a little proud of the glass
    B.box(-hw_top - 0.02, hw_top + 0.02, yb_top - 0.03, yf_top + 0.03, z_top - 0.01, z_top + 0.05, paint)
    # the middle (B) pillar on both sides
    ymid = (yb_top + yf_top) / 2
    for sx in (-1, 1):
        x = sx * ((hw_low + hw_top) / 2)
        B.box(x - 0.04, x + 0.04, ymid - 0.05, ymid + 0.05, z_low, z_top, paint)


def wheel(B, x, y, r, side):
    """Tyre with a chrome hubcap facing outwards (side = +1 right, -1 left)."""
    B.cyl(x, y, r, r, 0.22, "Tire", seg=14, rot=(0, math.pi / 2, 0))
    hx = x + side * 0.115
    B.cyl(hx, y, r, r * 0.55, 0.02, "Chrome", seg=12, rot=(0, math.pi / 2, 0))


def car(name, kind, paint):
    """kind: sedan, hatch, van, taxi."""
    B = Builder(name, (0, 0, 0))
    L = {"sedan": 4.6, "hatch": 3.9, "van": 4.9, "taxi": 4.7}[kind]
    hw = 0.9 if kind != "van" else 0.98
    R = 0.31
    wys = (-(L / 2 - 0.85), L / 2 - 0.9)                           # rear / front axle
    arch = R + 0.1
    z_arch = 2 * R + 0.06                                          # top of the wheel arches
    belt = 0.92 if kind != "van" else 1.0                          # bottom of the windows
    # --- lower body: below the arches only between / beyond the wheels
    cuts = [(-L / 2 + 0.05, wys[0] - arch), (wys[0] + arch, wys[1] - arch), (wys[1] + arch, L / 2 - 0.05)]
    for (y0, y1) in cuts:
        ring(B, [(y0, hw - 0.02, 0.3, z_arch + 0.02), (y1, hw - 0.02, 0.3, z_arch + 0.02)], paint, chamfer=0.05)
    for wy in wys:                                                 # dark wheel wells
        B.box(-hw + 0.2, hw - 0.2, wy - arch, wy + arch, 0.34, z_arch, "Tire")
    # --- upper body: tapered nose and tail, hood a little lower than the beltline
    if kind == "van":
        ring(B, [(-L / 2, hw - 0.03, z_arch, 2.05), (-L / 2 + 0.08, hw, z_arch, 2.12), (L / 2 - 1.15, hw, z_arch, 2.12),
                 (L / 2 - 1.1, hw, z_arch, belt + 0.05)], paint)
        ring(B, [(L / 2 - 1.1, hw, z_arch, belt + 0.05), (L / 2 - 0.12, hw - 0.04, z_arch, belt - 0.02),
                 (L / 2, hw - 0.1, z_arch + 0.06, belt - 0.12)], paint)
        # windscreen and cab side windows
        verts = [(-hw + 0.05, L / 2 - 1.1, belt + 0.05), (hw - 0.05, L / 2 - 1.1, belt + 0.05),
                 (hw - 0.05, L / 2 - 1.16, 1.95), (-hw + 0.05, L / 2 - 1.16, 1.95)]
        B.mesh(verts, [[0, 1, 2, 3]], "WinDark")
        for sx in (-1, 1):
            x = sx * (hw + 0.005)
            B.box(x - 0.01, x + 0.01, L / 2 - 1.95, L / 2 - 1.25, belt + 0.12, 1.75, "WinDark")       # cab window
            B.box(x - 0.01, x + 0.01, -L / 2 + 0.4, L / 2 - 2.2, 1.35, 1.75, "WinDark")              # load windows
            for yy in (-L / 2 + 0.25, L / 2 - 2.05):
                B.box(x - 0.012, x + 0.012, yy - 0.012, yy + 0.012, z_arch, 2.05, "Tire")            # door seams
    else:
        nose_drop = 0.08
        stations = [(-L / 2, hw - 0.06, z_arch, belt - 0.08), (-L / 2 + 0.18, hw, z_arch, belt),
                    (L / 2 - 0.25, hw, z_arch, belt - nose_drop * 0.5), (L / 2, hw - 0.06, z_arch, belt - nose_drop - 0.08)]
        ring(B, stations, paint)
        if kind == "hatch":
            glasshouse(B, -L / 2 + 0.2, L * 0.16, hw - 0.06, hw - 0.2, belt, belt + 0.58, 0.18, 0.5, "WinDark", paint)
        else:
            glasshouse(B, -L * 0.24, L * 0.17, hw - 0.06, hw - 0.2, belt, belt + 0.56, 0.42, 0.5, "WinDark", paint)
        for sx in (-1, 1):                                          # door seams and handles
            x = sx * (hw + 0.004)
            for yy in ((-L * 0.24, L * 0.17) if kind == "hatch" else (-L * 0.22, -L * 0.02, L * 0.17)):
                B.box(x - 0.01, x + 0.01, yy - 0.01, yy + 0.01, z_arch - 0.2, belt - 0.02, "Tire")
            B.box(x - 0.015, x + 0.015, -L * 0.04 - 0.08, -L * 0.04 + 0.08, belt - 0.12, belt - 0.08, "Chrome")
            mx = sx * (hw + 0.07)                                   # mirrors
            B.box(mx - 0.06, mx + 0.06, L * 0.15, L * 0.15 + 0.08, belt + 0.02, belt + 0.12, "Chrome")
    # --- chrome: bumpers, beltline trim, grille, headlights, tail lights, plates
    for sy in (-1, 1):
        y = sy * (L / 2 + 0.02)
        B.box(-hw - 0.03, hw + 0.03, y - 0.09, y + 0.09, 0.34, 0.5, "Chrome")
        B.box(-0.22, 0.22, y + sy * 0.08, y + sy * 0.1, 0.38, 0.5, "LineWhite")                         # number plate
    B.box(-hw - 0.012, hw + 0.012, -L / 2 + 0.3, L / 2 - 0.3, z_arch + 0.06, z_arch + 0.09, "Chrome")    # side trim
    fy = L / 2 - (0.0 if kind != "van" else 0.0)
    gz0, gz1 = 0.52, (belt - 0.18 if kind != "van" else belt - 0.22)
    B.box(-hw + 0.38, hw - 0.38, fy - 0.03, fy + 0.02, gz0, gz1, "Chrome")                              # grille frame
    B.box(-hw + 0.42, hw - 0.42, fy - 0.02, fy + 0.03, gz0 + 0.04, gz1 - 0.04, "Tire")                 # grille
    for sx in (-1, 1):
        lx = sx * (hw - 0.2)
        B.cyl(lx, fy + 0.01, (gz0 + gz1) / 2, 0.11, 0.03, "Chrome", seg=12, rot=(math.pi / 2, 0, 0))   # headlight rim
        B.cyl(lx, fy + 0.03, (gz0 + gz1) / 2, 0.085, 0.02, "LampGlass", seg=12, rot=(math.pi / 2, 0, 0))
        tx = sx * (hw - 0.22)
        B.box(tx - 0.16, tx + 0.16, -L / 2 - 0.02, -L / 2 + 0.01, 0.56, 0.74, "CarLights")             # tail lights
    # --- wheels
    for wy in wys:
        for sx in (-1, 1):
            wx = sx * (hw - 0.09)
            wheel(B, wx, wy, R, sx)
    if kind == "taxi":
        B.box(-0.32, 0.32, -0.25, 0.25, belt + 0.62, belt + 0.86, "SignTaxi")                         # roof sign
        for sx in (-1, 1):                                          # checker stripe
            x = sx * (hw + 0.008)
            B.box(x - 0.01, x + 0.01, -L / 2 + 0.4, L / 2 - 0.4, belt - 0.2, belt - 0.1, "CarTaxi")
    return B.done()
