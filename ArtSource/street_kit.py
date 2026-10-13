# Shared street kit for the exterior maps (Shackleton Ave, Business District, Suburbs): mesh builder, building
# faces, windows, doors, lamps, bulbs and props. All exteriors share one texture/material library
# (street_textures.py -> Assets/Hitman/Art/Shackleton/Textures, materials M_Shk_*).
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler
import numpy as np
import street_textures as ST

PROJECT = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test"
TEX = PROJECT + "/Assets/Hitman/Art/Shackleton/Textures"
rng = np.random.default_rng(11)


def reset(seed):
    """Empty the Blender scene and reseed, before building a map."""
    global rng
    rng = np.random.default_rng(seed)
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    MATS.clear()


def export(fbx_path, blend_path):
    os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=False, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print("OBJECTS", len(bpy.data.objects), "MATERIALS", len(MATS))


MATS = {}


def mat(name):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new("M_Shk_" + name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    img = bpy.data.images.load(os.path.join(TEX, "T_Shk_" + name + ".png"), check_existing=True)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if name in ST.RGBA: nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.85
    MATS[name] = m
    return m


# ---------------------------------------------------------------- mesh builder (one object, many parts/materials)

class Builder:
    def __init__(self, name, origin=(0, 0, 0)):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new()
        self.mats = []
        self.o = Vector(origin)

    def _mi(self, m):
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)

    def _uv(self, faces, lo, hi, m, tile):
        fitted = m in ST.FITTED
        span = [max(hi[i] - lo[i], 1e-4) for i in range(3)]
        for f in faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                p = l.vert.co + self.o
                if ax == 2: ia, ib = 0, 1
                elif ax == 0: ia, ib = 1, 2
                else: ia, ib = 0, 2
                if fitted:
                    u = (p[ia] - lo[ia]) / span[ia]; v = (p[ib] - lo[ib]) / span[ib]
                    if (ax == 0 and n[0] < 0) or (ax == 1 and n[1] > 0): u = 1 - u
                    l[self.uv].uv = (u, v)
                else:
                    l[self.uv].uv = (p[ia] / tile, p[ib] / tile)

    def box(self, x0, x1, y0, y1, z0, z1, m, tile=1.0, rot=None, pivot=None):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        vs = r["verts"]
        c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
        for v in vs:
            v.co = Vector((v.co.x * (x1 - x0), v.co.y * (y1 - y0), v.co.z * (z1 - z0))) + c - self.o
        faces = list({f for v in vs for f in v.link_faces})
        self.bm.normal_update()
        self._uv(faces, (x0, y0, z0), (x1, y1, z1), m, tile)
        idx = self._mi(m)
        for f in faces: f.material_index = idx
        if rot is not None:
            pv = Vector(pivot if pivot is not None else c) - self.o
            bmesh.ops.rotate(self.bm, verts=vs, cent=pv, matrix=Euler(rot).to_matrix())
        return self

    def cyl(self, cx, cy, z0, r, h, m, seg=8, r2=None, rot=None):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
        vs = res["verts"]
        for v in vs: v.co += Vector((cx, cy, z0 + h / 2)) - self.o
        faces = list({f for v in vs for f in v.link_faces})
        self.bm.normal_update()
        self._uv(faces, (cx - r, cy - r, z0), (cx + r, cy + r, z0 + h), m, 0.5)
        idx = self._mi(m)
        for f in faces: f.material_index = idx
        if rot is not None:
            bmesh.ops.rotate(self.bm, verts=vs, cent=Vector((cx, cy, z0)) - self.o, matrix=Euler(rot).to_matrix())
        return self

    def blob(self, cx, cy, z0, sx, sy, sz, m, lumpy=0.3):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=0.5)
        vs = res["verts"]
        for v in vs:
            d = 1 + lumpy * (rng.random() - 0.5) * 2
            v.co = Vector((v.co.x * sx * d + cx, v.co.y * sy * d + cy, max(v.co.z, -0.35) * sz * d + sz * 0.35 + z0)) - self.o
        faces = list({f for v in vs for f in v.link_faces})
        self.bm.normal_update()
        self._uv(faces, (cx - sx, cy - sy, z0), (cx + sx, cy + sy, z0 + sz), m, 0.6)
        idx = self._mi(m)
        for f in faces: f.material_index = idx
        return self

    def mesh(self, verts, faces, m, tile=1.0):
        """Free-form part: verts in world space, faces as index lists (any winding; normals are fixed up)."""
        vs = [self.bm.verts.new(Vector(v) - self.o) for v in verts]
        fs = [self.bm.faces.new([vs[i] for i in f]) for f in faces]
        bmesh.ops.recalc_face_normals(self.bm, faces=fs)
        self.bm.normal_update()
        lo = [min(v[i] for v in verts) for i in range(3)]
        hi = [max(v[i] for v in verts) for i in range(3)]
        self._uv(fs, lo, hi, m, tile)
        idx = self._mi(m)
        for f in fs: f.material_index = idx
        return self

    def done(self):
        if not self.bm.verts:
            self.bm.free(); return None
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me); self.bm.free()
        for m in self.mats: me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        ob.location = self.o
        bpy.context.collection.objects.link(ob)
        return ob


def single(name, *parts):
    """Convenience: one object made of a few boxes: parts = (x0,x1,y0,y1,z0,z1,mat[,tile])."""
    b = Builder(name, ((parts[0][0] + parts[0][1]) / 2, (parts[0][2] + parts[0][3]) / 2, 0))
    for p in parts: b.box(*p)
    return b.done()


# ---------------------------------------------------------------- faces of a building

class Face:
    """One side of a building: axis 'x' faces (normal along X) or 'y'; s = outward sign; a = along-face coordinate."""
    def __init__(self, axis, fixed, s, a0, a1):
        self.axis, self.fixed, self.s, self.a0, self.a1 = axis, fixed, s, a0, a1

    def box(self, B, a0, a1, z0, z1, d0, d1, m, tile=1.0):
        p0, p1 = sorted((self.fixed + self.s * d0, self.fixed + self.s * d1))
        if self.axis == "x": B.box(p0, p1, a0, a1, z0, z1, m, tile)
        else: B.box(a0, a1, p0, p1, z0, z1, m, tile)


def pick_face(faces, street):
    """faces = [east, west, north, south]; street = +1 / -1 (east / west) or "N" / "S"."""
    return {1: faces[0], -1: faces[1], "N": faces[2], "S": faces[3]}[street]


GROUND = 3.4
FLOOR = 3.0
WINDOWS = ["WinDark"] * 5 + ["WinCurtain"] * 3 + ["WinLit"] + ["Boarded"]


CLEAN_WINDOWS = ["WinPunch"] * 4 + ["WinPunchLit"]


def window(B, F, c, sill, w=1.0, h=1.6, kind=None, pool=None):
    pool = pool or WINDOWS
    kind = kind or pool[rng.integers(len(pool))]
    F.box(B, c - w / 2 - 0.1, c + w / 2 + 0.1, sill - 0.08, sill + h + 0.12, 0, 0.03, "Curb")        # surround
    F.box(B, c - w / 2, c + w / 2, sill, sill + h, 0, 0.045, kind)                                 # glass/boards
    F.box(B, c - w / 2 - 0.12, c + w / 2 + 0.12, sill - 0.12, sill, 0, 0.14, "Curb")             # sill
    F.box(B, c - w / 2 - 0.1, c + w / 2 + 0.1, sill + h + 0.12, sill + h + 0.28, 0, 0.07, "Curb")  # lintel
    if pool is WINDOWS and rng.random() < 0.18 and kind != "Boarded":                              # AC unit
        F.box(B, c - 0.32, c + 0.32, sill - 0.5, sill - 0.12, 0, 0.5, "Steel")


def wall_bulb(name, F, c, z):
    """A caged bulb on a bracket, sticking out of a wall: one of the night light sources."""
    B = Builder(name, (F.fixed, c, z) if F.axis == "x" else (c, F.fixed, z))
    F.box(B, c - 0.03, c + 0.03, z + 0.05, z + 0.11, 0, 0.3, "Steel")
    F.box(B, c - 0.1, c + 0.1, z - 0.12, z + 0.08, 0.22, 0.42, "Steel")
    F.box(B, c - 0.07, c + 0.07, z - 0.16, z + 0.04, 0.25, 0.39, "LampGlass")
    return B.done()


def building(key, src, floors, facade, street, shop=None, fire_escape=False, door=None, roof=(), clean=False):
    """src = layout item; street = +1 if the street is on the +X side of the building, else -1."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    top = GROUND + FLOOR * (floors - 1) + 0.9
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, top, facade)
    B.box(x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, top - 0.9, top - 0.62, "Curb")          # cornice
    B.box(x0 - 0.06, x1 + 0.06, y0 - 0.06, y1 + 0.06, top - 0.04, top + 0.06, "Curb")         # coping
    B.box(x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.08, GROUND - 0.2, GROUND - 0.02, "Curb")    # string course
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    street_face = pick_face(faces, street)
    graf = Builder("Graf_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for F in faces:
        L = F.a1 - F.a0
        n = max(1, int((L - 1.2) // 2.3))
        cols = [F.a0 + (L / n) * (i + 0.5) for i in range(n)]
        is_street = F is street_face
        for f in range(1, floors):
            for c in cols:
                window(B, F, c, GROUND + (f - 1) * FLOOR + 0.9, pool=CLEAN_WINDOWS if clean else None)
        # ground floor
        if is_street and shop:
            gap = (door - 0.8, door + 0.8) if door is not None else (1e9, 1e9)
            a, b = F.a0 + 0.5, F.a1 - 0.5
            segs = [(a, min(b, gap[0])), (max(a, gap[1]), b)] if door is not None else [(a, b)]
            for (s0, s1) in segs:
                if s1 - s0 < 0.6: continue
                F.box(B, s0, s1, 0.0, 0.35, 0, 0.06, "Curb")                                    # stall riser
                F.box(B, s0, s1, 0.35, 2.55, 0, 0.04, shop.get("glass", "Shopfront"))
                F.box(B, s0 - 0.05, s1 + 0.05, 2.55, 2.65, 0, 0.08, "Steel")
            neon = Builder(f"LightSrc_Neon{shop['sign'][4:]}_{key}", ((x0 + x1) / 2, (y0 + y1) / 2, 0))
            F.box(neon, F.a0 + 0.4, F.a1 - 0.4, 2.7, 3.15, 0, 0.12, shop["sign"])                # neon sign
            neon.done()
            for k, c in enumerate((F.a0 + 0.25, F.a1 - 0.25)):
                wall_bulb(f"LightSrc_Bulb_{key}{k}", F, c, 3.35)
            if shop.get("awning"):
                F.box(B, F.a0 + 0.4, F.a1 - 0.4, 2.62, 2.68, 0.1, 1.3, shop["awning"])
                F.box(B, F.a0 + 0.4, F.a1 - 0.4, 2.35, 2.68, 1.25, 1.3, shop["awning"])
        else:
            for c in cols:
                if door is not None and is_street and abs(c - door) < 1.3: continue
                window(B, F, c, 0.95, kind=None if clean else ("Boarded" if rng.random() < 0.35 else None),
                       pool=CLEAN_WINDOWS if clean else None)
        if is_street:
            F.box(B, F.a0 + 0.15, F.a0 + 0.27, 0, top - 0.7, 0, 0.12, "Steel")                    # drainpipe
            if not shop and door is None:
                wall_bulb(f"LightSrc_Bulb_{key}", F, (F.a0 + F.a1) / 2, 3.0)
        # graffiti on the lower wall
        for k in range(0 if clean else (2 if is_street else 1)):
            c = F.a0 + rng.uniform(1.0, max(1.1, L - 1.0))
            if door is not None and is_street and abs(c - door) < 1.6: continue
            w = rng.uniform(1.4, 2.4)
            F.box(graf, c - w / 2, c + w / 2, 0.4, 0.4 + w * 0.75, 0.005, 0.012, f"Graffiti{rng.integers(1, 6)}")
    if fire_escape:
        F = street_face
        c = (F.a0 + F.a1) / 2
        for f in range(1, floors):
            z = GROUND + (f - 1) * FLOOR
            F.box(B, c - 1.6, c + 1.6, z - 0.06, z, 0.05, 1.05, "Steel")                            # platform
            F.box(B, c - 1.6, c + 1.6, z + 0.95, z + 1.0, 1.0, 1.05, "Steel")                       # top rail
            for k in range(5):
                a = c - 1.6 + k * 0.8
                F.box(B, a - 0.025, a + 0.025, z, z + 1.0, 1.0, 1.05, "Steel")                      # posts
            for a in (c - 1.6, c + 1.55):
                F.box(B, a, a + 0.05, z + 0.95, z + 1.0, 0.05, 1.05, "Steel")
            if f < floors - 1:                                                                      # stair to next
                length = math.hypot(2.4, FLOOR)
                ang = math.atan2(FLOOR, 2.4)
                d = F.fixed + F.s * 0.55
                ctr = (d, c, z + FLOOR / 2) if F.axis == "x" else (c, d, z + FLOOR / 2)
                if F.axis == "x":
                    B.box(d - 0.2, d + 0.2, c - length / 2, c + length / 2, z + FLOOR / 2 - 0.03, z + FLOOR / 2 + 0.03,
                          "Steel", rot=(ang, 0, 0), pivot=ctr)
        # drop ladder down to just above head height
        z = GROUND
        F.box(B, c + 1.0, c + 1.4, 2.3, z, 0.9, 0.95, "Steel")
    for (rx, ry, kind) in roof:
        if kind == "tank":
            for dx in (-0.7, 0.7):
                for dy in (-0.7, 0.7):
                    B.box(rx + dx - 0.06, rx + dx + 0.06, ry + dy - 0.06, ry + dy + 0.06, top, top + 1.6, "Wood")
            B.cyl(rx, ry, top + 1.6, 1.1, 2.0, "Wood", seg=10)
            B.cyl(rx, ry, top + 3.6, 1.15, 0.6, "Steel", seg=10, r2=0.1)
        elif kind == "chimney":
            B.box(rx - 0.35, rx + 0.35, ry - 0.35, ry + 0.35, top, top + 1.4, "BrickRed")
        elif kind == "ac":
            B.box(rx - 0.6, rx + 0.6, ry - 0.45, ry + 0.45, top, top + 0.8, "Steel")
    graf.done()
    return B.done(), top


def door_obj(key, x_face, s, y, kind="DoorGreen", axis="x"):
    """A door on a building face. axis "x": the face is at X = x_face, centred at Y = y; axis "y": the face is at
    Y = x_face, centred at X = y. s = the outward direction (+1/-1) along that axis."""
    F = Face(axis, x_face, s, y - 1, y + 1)
    B = Builder("Door_" + key, (x_face, y, 0) if axis == "x" else (y, x_face, 0))
    F.box(B, y - 0.55, y + 0.55, 0, 2.25, 0, 0.07, kind)
    F.box(B, y - 0.68, y - 0.55, 0, 2.38, 0, 0.12, "Wood")
    F.box(B, y + 0.55, y + 0.68, 0, 2.38, 0, 0.12, "Wood")
    F.box(B, y - 0.68, y + 0.68, 2.25, 2.38, 0, 0.12, "Wood")
    F.box(B, y - 0.75, y + 0.75, 0, 0.06, 0, 0.45, "Curb")                                          # step
    ob = B.done()
    wall_bulb(f"LightSrc_Bulb_Door{key}", F, y, 2.75)
    return ob



def lamp(name, x, y, arm, lean, broken=False):
    """Street lamp leaning by 'lean' radians; arm = (dx, dy) direction the head reaches out over the road."""
    B = Builder(name, (x, y, 0))
    tilt = (arm[1] * -lean + rng.uniform(-0.3, 0.3) * lean, arm[0] * lean, 0)
    B.cyl(x, y, 0, 0.09, 0.4, "Steel", seg=6)
    B.cyl(x, y, 0, 0.06, 5.0, "Steel", seg=6, rot=tilt)
    B.done()
    if broken: return
    top = Matrix.Rotation(tilt[1], 3, "Y") @ Matrix.Rotation(tilt[0], 3, "X") @ Vector((0, 0, 5.0))
    hx, hy, hz = x + top.x, y + top.y, top.z
    ex, ey = hx + arm[0] * 1.1, hy + arm[1] * 1.1
    head = Builder("LightSrc_Lamp_" + name[5:], (ex, ey, hz))
    head.box(min(hx, ex) - 0.04, max(hx, ex) + 0.04, min(hy, ey) - 0.04, max(hy, ey) + 0.04, hz - 0.08, hz, "Steel")
    head.box(ex - 0.2, ex + 0.2, ey - 0.2, ey + 0.2, hz - 0.18, hz, "Steel")
    head.box(ex - 0.16, ex + 0.16, ey - 0.16, ey + 0.16, hz - 0.22, hz - 0.18, "LampGlass")
    head.done()



def dumpster(name, x, y):
    s = 1 if x < 0 else -1
    B = Builder(name, (x, y, 0))
    B.box(x - 0.5, x + 0.5, y - 0.9, y + 0.9, 0.15, 1.25, "Dumpster")
    B.box(x - 0.55, x + 0.55, y - 0.95, y + 0.95, 1.25, 1.32, "Steel", rot=(0.0, s * 0.12, 0), pivot=(x - s * 0.5, y, 1.25))
    for dy in (-0.75, 0.75):
        B.cyl(x, y + dy, 0, 0.12, 0.15, "Steel", seg=6)
    return B.done()



# ---------------------------------------------------------------- business district pieces

def office(key, src, floors, street, base="Granite", lobby_sign=None, door=None, top_height=None):
    """Office tower: stone base, ribbon windows on every floor (some lit at night), a glass lobby on the street."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    fh = 3.4
    top = top_height or (4.5 + fh * (floors - 1) + 1.2)
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, top, base)
    B.box(x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, top - 0.2, top + 0.1, "Steel")
    B.box(x0 + 1.5, x1 - 1.5, y0 + 1.5, y1 - 1.5, top, top + 2.2, "Concrete")                    # plant room
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    street_face = pick_face(faces, street)
    lit = Builder("LightSrc_Windows_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for F in faces:
        for f in range(1, floors):
            z = 4.5 + (f - 1) * fh
            glass = "GlassOfficeLit" if rng.random() < 0.3 else "GlassOffice"
            F.box(lit if glass == "GlassOfficeLit" else B, F.a0 + 0.35, F.a1 - 0.35, z + 0.6, z + 2.7, 0, 0.04, glass)
            F.box(B, F.a0, F.a1, z + 0.45, z + 0.6, 0, 0.12, "GraniteLight")                       # spandrel ledge
        if F is street_face:
            a, b = F.a0 + 0.5, F.a1 - 0.5
            F.box(B, a, b, 0.0, 3.8, 0, 0.05, "GlassOffice")                                        # lobby glazing
            F.box(B, F.a0, F.a1, 3.8, 4.5, 0, 0.6, "GraniteLight")                                 # canopy band
            if lobby_sign:
                c = door if door is not None else (F.a0 + F.a1) / 2
                neon = Builder(f"LightSrc_Neon{lobby_sign[4:]}_{key}", ((x0 + x1) / 2, (y0 + y1) / 2, 0))
                F.box(neon, c - 1.6, c + 1.6, 3.85, 4.4, 0.6, 0.66, lobby_sign)
                neon.done()
            for k, c in enumerate((F.a0 + 1.0, F.a1 - 1.0)):
                wall_bulb(f"LightSrc_Bulb_{key}{k}", F, c, 3.3)
        else:
            F.box(B, F.a0 + 0.35, F.a1 - 0.35, 1.0, 3.6, 0, 0.04, "GlassOffice")
    lit.done()
    return B.done(), top


def tree(name, x, y, h=4.5, r=1.6, autumn=False, grate=True):
    B = Builder(name, (x, y, 0))
    if grate: B.box(x - 0.6, x + 0.6, y - 0.6, y + 0.6, 0, 0.03, "Steel")
    B.cyl(x, y, 0, 0.14, h * 0.55, "Bark", seg=6)
    leaves = "LeavesAutumn" if autumn else "Leaves"
    for k in range(4):
        B.blob(x + rng.uniform(-0.5, 0.5), y + rng.uniform(-0.5, 0.5), h * 0.45 + rng.uniform(0, 0.8),
               r * rng.uniform(1.1, 1.5), r * rng.uniform(1.1, 1.5), r * rng.uniform(0.9, 1.2), leaves, lumpy=0.35)
    return B.done()


def bench(name, x, y, along_y=True):
    B = Builder(name, (x, y, 0))
    w, d = (0.5, 1.6) if along_y else (1.6, 0.5)
    B.box(x - w / 2, x + w / 2, y - d / 2, y + d / 2, 0.42, 0.48, "Wood")
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        B.box(x + dx * (w / 2 - 0.05) - 0.03, x + dx * (w / 2 - 0.05) + 0.03, y + dy * (d / 2 - 0.08) - 0.03,
              y + dy * (d / 2 - 0.08) + 0.03, 0, 0.42, "Steel")
    return B.done()


def traffic_light(name, x, y, face):
    """face = (dx, dy) direction the lights point."""
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.07, 3.2, "Steel", seg=6)
    hx, hy = x + face[0] * 0.15, y + face[1] * 0.15
    B.box(hx - 0.16, hx + 0.16, hy - 0.16, hy + 0.16, 2.3, 3.2, "Steel")
    for k, m in enumerate(("CarLights", "LampGlass", "Leaves")):
        z = 3.0 - k * 0.28
        B.box(hx + face[0] * 0.17 - 0.07, hx + face[0] * 0.17 + 0.07, hy + face[1] * 0.17 - 0.07, hy + face[1] * 0.17 + 0.07,
              z - 0.07, z + 0.07, m)
    return B.done()


def subway_kiosk(name, x, y, along_y=True, sign="SignSubway"):
    """Street entrance to an underground station: railings round a stair well, a sign on a post."""
    B = Builder(name, (x, y, 0))
    w, d = (1.5, 3.0) if along_y else (3.0, 1.5)
    B.box(x - w / 2, x + w / 2, y - d / 2, y + d / 2, -0.02, 0.01, "Steel")
    for i in range(6):                                                   # steps going down (seen through the well)
        t = i / 6.0
        if along_y: B.box(x - w / 2 + 0.1, x + w / 2 - 0.1, y - d / 2 + t * d, y - d / 2 + (t + 1 / 6) * d, -0.02 - i * 0.05, 0.0 - i * 0.05, "Concrete")
        else: B.box(x - w / 2 + t * w, x - w / 2 + (t + 1 / 6) * w, y - d / 2 + 0.1, y + d / 2 - 0.1, -0.02 - i * 0.05, 0.0 - i * 0.05, "Concrete")
    for sx in (-1, 1):
        if along_y:
            B.box(x + sx * w / 2 - 0.04, x + sx * w / 2 + 0.04, y - d / 2, y + d / 2, 0.95, 1.02, "Steel")
            for k in range(5): B.box(x + sx * w / 2 - 0.03, x + sx * w / 2 + 0.03, y - d / 2 + k * d / 4 - 0.03, y - d / 2 + k * d / 4 + 0.03, 0, 1.0, "Steel")
        else:
            B.box(x - w / 2, x + w / 2, y + sx * d / 2 - 0.04, y + sx * d / 2 + 0.04, 0.95, 1.02, "Steel")
            for k in range(5): B.box(x - w / 2 + k * w / 4 - 0.03, x - w / 2 + k * w / 4 + 0.03, y + sx * d / 2 - 0.03, y + sx * d / 2 + 0.03, 0, 1.0, "Steel")
    if along_y: B.box(x - w / 2, x + w / 2, y + d / 2 - 0.04, y + d / 2 + 0.04, 0, 1.02, "Steel")
    else: B.box(x + w / 2 - 0.04, x + w / 2 + 0.04, y - d / 2, y + d / 2, 0, 1.02, "Steel")
    B.done()
    s = Builder("Station_Sign", (x, y, 0))
    px, py = (x + w / 2 + 0.2, y - d / 2) if along_y else (x - w / 2, y + d / 2 + 0.2)
    s.cyl(px, py, 0, 0.05, 2.6, "Steel", seg=6)
    if along_y: s.box(px - 0.05, px + 0.05, py - 0.6, py + 0.6, 2.3, 2.7, sign)
    else: s.box(px - 0.6, px + 0.6, py - 0.05, py + 0.05, 2.3, 2.7, sign)
    return s.done()


def taxi_sign(x, y):
    t = Builder("TaxiSign", (x, y, 0))
    t.cyl(x, y, 0, 0.05, 2.6, "Steel", seg=6)
    t.box(x - 0.04, x + 0.04, y - 0.35, y + 0.35, 2.2, 2.6, "SignTaxi")
    return t.done()


# ---------------------------------------------------------------- suburbs pieces

def house(key, src, street, siding, floors=1, door=None, porch=True, chimney=True):
    """Small suburban house: clapboard walls, pitched shingle roof (ridge along the street), porch, windows."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    wall = 3.0 * floors
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, 0.4, "Concrete")                                                     # foundation
    B.box(x0, x1, y0, y1, 0.4, wall, siding)
    # gable roof: two sloped slabs meeting at a ridge running along Y (parallel to the side street)
    cx = (x0 + x1) / 2
    half = (x1 - x0) / 2 + 0.4
    rise = 1.9
    slope = math.atan2(rise, half)
    length = math.hypot(half, rise)
    for s in (-1, 1):
        mx = cx + s * half / 2
        B.box(mx - length / 2, mx + length / 2, y0 - 0.35, y1 + 0.35, wall + rise / 2 - 0.08, wall + rise / 2 + 0.08,
              "Shingles", rot=(0, s * slope, 0), pivot=(mx, (y0 + y1) / 2, wall + rise / 2))
    for y in (y0 + 0.01, y1 - 0.01):                                                               # gable ends
        for k in range(6):
            t = k / 6.0
            hh = rise * (1 - t)
            B.box(cx - half * (1 - t) + 0.4 * (1 - t), cx + half * (1 - t) - 0.4 * (1 - t), y - 0.01, y + 0.01,
                  wall + hh * 0.0 + rise * t * 0.0 + (rise / 6) * k, wall + (rise / 6) * (k + 1), siding)
    if chimney:
        B.box(cx + half * 0.4, cx + half * 0.4 + 0.6, y0 + 1.0, y0 + 1.6, wall, wall + rise + 0.8, "BrickRed")
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    street_face = pick_face(faces, street)
    for F in faces:
        L = F.a1 - F.a0
        n = max(1, int((L - 1.0) // 2.4))
        cols = [F.a0 + (L / n) * (i + 0.5) for i in range(n)]
        for f in range(floors):
            for c in cols:
                if F is street_face and f == 0 and door is not None and abs(c - door) < 1.3: continue
                kind = "WinCurtain" if rng.random() < 0.5 else ("WinLit" if rng.random() < 0.3 else "WinDark")
                window(B, F, c, 1.2 + f * 3.0, w=0.9, h=1.3, kind=kind)
                F.box(B, c - 0.75, c - 0.5, 1.15 + f * 3.0, 2.6 + f * 3.0, 0, 0.05, "DoorGreen")      # shutters
                F.box(B, c + 0.5, c + 0.75, 1.15 + f * 3.0, 2.6 + f * 3.0, 0, 0.05, "DoorGreen")
    if porch and door is not None:
        F = street_face
        F.box(B, door - 1.4, door + 1.4, 0, 0.12, 0, 0.9, "Wood")                                    # deck (step height)
        for c in (door - 1.3, door + 1.3):
            F.box(B, c - 0.06, c + 0.06, 0.12, 2.7, 0.78, 0.9, "SidingWhite")                     # posts
        F.box(B, door - 1.5, door + 1.5, 2.7, 2.85, -0.05, 1.0, "Shingles")                      # porch roof
    return B.done()


def hedge(name, x0, x1, y0, y1, h=0.9):
    return single(name, (x0, x1, y0, y1, 0, h, "Hedge", 0.7))


def picket_fence(B, x0, x1, y0, y1):
    """Adds a white picket run along X or Y to builder B."""
    if abs(x1 - x0) > abs(y1 - y0):
        n = int(abs(x1 - x0) / 0.15)
        for k in range(n): B.box(x0 + k * 0.15, x0 + k * 0.15 + 0.07, y0 - 0.02, y0 + 0.02, 0, 0.85, "SidingWhite")
        B.box(x0, x1, y0 - 0.03, y0 + 0.03, 0.55, 0.62, "SidingWhite")
    else:
        n = int(abs(y1 - y0) / 0.15)
        for k in range(n): B.box(x0 - 0.02, x0 + 0.02, y0 + k * 0.15, y0 + k * 0.15 + 0.07, 0, 0.85, "SidingWhite")
        B.box(x0 - 0.03, x0 + 0.03, y0, y1, 0.55, 0.62, "SidingWhite")


def mailbox(name, x, y):
    B = Builder(name, (x, y, 0))
    B.box(x - 0.04, x + 0.04, y - 0.04, y + 0.04, 0, 1.0, "Wood")
    B.box(x - 0.13, x + 0.13, y - 0.24, y + 0.24, 1.0, 1.25, "Steel")
    return B.done()


# ---------------------------------------------------------------- vehicles (built at the origin, facing +Y)

def vehicle(name, kind, paint):
    """Low-poly car at the origin facing +Y (Unity +Z). kind: sedan, hatch, van, taxi."""
    B = Builder(name, (0, 0, 0))
    L = {"sedan": 4.4, "hatch": 3.8, "van": 4.8, "taxi": 4.5}[kind]
    W = 1.8 if kind != "van" else 1.95
    body_top = 0.95 if kind != "van" else 1.0
    R = 0.31                                                       # wheel radius (wheel centre at z = R)
    wys = (-(L / 2 - 0.85), L / 2 - 0.85)                          # rear / front axle
    arch = R + 0.09                                                # half-length of a wheel arch
    arch_top = 2 * R + 0.07
    B.box(-W / 2, W / 2, -L / 2, L / 2, arch_top, body_top, paint)                               # body above the arches
    cuts = [(-L / 2, wys[0] - arch), (wys[0] + arch, wys[1] - arch), (wys[1] + arch, L / 2)]
    for (y0, y1) in cuts:                                                                         # sills between arches
        B.box(-W / 2, W / 2, y0, y1, 0.3, arch_top, paint)
    for wy in wys:                                                                                # dark wheel wells
        B.box(-W / 2 + 0.24, W / 2 - 0.24, wy - arch, wy + arch, 0.3, arch_top, "Tire")
    B.box(-W / 2 - 0.04, W / 2 + 0.04, -L / 2 - 0.08, -L / 2 + 0.12, 0.32, 0.52, "Chrome")        # chrome bumpers
    B.box(-W / 2 - 0.04, W / 2 + 0.04, L / 2 - 0.12, L / 2 + 0.08, 0.32, 0.52, "Chrome")
    B.box(-W / 2 + 0.35, W / 2 - 0.35, L / 2 - 0.02, L / 2 + 0.03, 0.55, 0.82, "Chrome")           # grille
    B.box(-W / 2 - 0.01, W / 2 + 0.01, -L / 2 + 0.3, L / 2 - 0.3, arch_top + 0.04, arch_top + 0.08, "Chrome")  # side trim
    if kind == "van":
        B.box(-W / 2 + 0.02, W / 2 - 0.02, -L / 2, L / 2 - 1.0, body_top, 2.1, paint)
        B.box(-W / 2 + 0.05, W / 2 - 0.05, L / 2 - 1.0, L / 2 - 0.4, body_top, 1.85, "WinDark", rot=(-0.35, 0, 0), pivot=(0, L / 2 - 0.7, body_top))
    else:
        cab0, cab1 = (-L * 0.28, L * 0.18) if kind != "hatch" else (-L * 0.42, L * 0.15)
        B.box(-W / 2 + 0.1, W / 2 - 0.1, cab0, cab1, body_top, body_top + 0.6, paint)
        B.box(-W / 2 + 0.12, W / 2 - 0.12, cab0 + 0.1, cab1 - 0.1, body_top + 0.05, body_top + 0.55, "WinDark")
        B.box(-W / 2 + 0.08, W / 2 - 0.08, cab0 - 0.02, cab1 + 0.02, body_top + 0.12, body_top + 0.48, "WinDark")
    for wy in wys:                                                 # wheels sit in the arches, a touch proud of the sides
        for sx in (-1, 1):
            wx = W / 2 - 0.2 if sx > 0 else -W / 2 - 0.02
            B.cyl(wx, wy, R, R, 0.22, "Tire", seg=12, rot=(0, math.pi / 2, 0))
    for sx in (-1, 1):                                                                               # head / tail lamps
        B.box(sx * (W / 2 - 0.35) - 0.18, sx * (W / 2 - 0.35) + 0.18, L / 2 - 0.02, L / 2 + 0.02, 0.6, 0.78, "CarLights")
        B.box(sx * (W / 2 - 0.35) - 0.18, sx * (W / 2 - 0.35) + 0.18, -L / 2 - 0.02, -L / 2 + 0.02, 0.6, 0.78, "CarLights")
    if kind == "taxi":
        B.box(-0.35, 0.35, -0.2, 0.2, body_top + 0.6, body_top + 0.85, "SignTaxi")
    return B.done()



# ---------------------------------------------------------------- clean districts (Business District, Suburbs)

def deco_tower(key, src, floors, street, door=None, sign=None, stone="Limestone"):
    """Art-deco office tower: pale stone with vertical piers, punched windows, a tall lobby and stepped crown."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    fh, base = 3.4, 6.0
    top = base + fh * floors
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    lit = Builder("LightSrc_Windows_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, top, stone)
    B.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, base - 0.5, base, "LimestoneDark")              # podium cornice
    for k, inset in enumerate((1.4, 2.8, 4.0)):                                                     # stepped crown
        z0 = top + k * 3.2
        B.box(x0 + inset, x1 - inset, y0 + inset, y1 - inset, z0, z0 + 3.2, stone if k < 2 else "LimestoneDark")
    B.cyl((x0 + x1) / 2, (y0 + y1) / 2, top + 9.6, 0.15, 5.0, "Steel", seg=6)                       # mast
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    street_face = pick_face(faces, street)
    for F in faces:
        L = F.a1 - F.a0
        bays = max(2, int(L // 1.7))
        w = L / bays
        for b in range(bays + 1):                                                                   # piers
            a = F.a0 + b * w
            F.box(B, a - 0.16, a + 0.16, base, top + 0.4, 0, 0.22, "LimestoneDark")
        for f in range(floors):
            z = base + f * fh
            for b in range(bays):
                c = F.a0 + (b + 0.5) * w
                kind = "WinPunchLit" if rng.random() < 0.25 else "WinPunch"
                F.box(lit if kind == "WinPunchLit" else B, c - w / 2 + 0.25, c + w / 2 - 0.25, z + 0.7, z + 2.8, 0, 0.04, kind)
        if F is street_face:
            c = door if door is not None else (F.a0 + F.a1) / 2
            F.box(B, F.a0 + 0.6, F.a1 - 0.6, 0.0, 4.6, 0, 0.05, "GlassOffice")                      # lobby glazing
            for b in range(bays + 1):
                a = F.a0 + b * w
                F.box(B, a - 0.22, a + 0.22, 0, base - 0.5, 0, 0.3, stone)                         # lobby piers
            if sign:
                neon = Builder(f"LightSrc_Neon{sign[4:]}_{key}", ((x0 + x1) / 2, (y0 + y1) / 2, 0))
                F.box(neon, c - 1.8, c + 1.8, 4.8, 5.4, 0.3, 0.36, sign)
                neon.done()
            for k, cc in enumerate((F.a0 + 1.0, F.a1 - 1.0)):
                wall_bulb(f"LightSrc_Bulb_{key}{k}", F, cc, 3.6)
        else:
            for b in range(bays):
                c = F.a0 + (b + 0.5) * w
                F.box(B, c - w / 2 + 0.3, c + w / 2 - 0.3, 1.2, 4.4, 0, 0.04, "WinPunch")
    lit.done()
    return B.done(), top


def hip_roof(B, x0, x1, y0, y1, z, rise, overhang, m):
    """Hipped roof over a rectangle: four slopes up to a ridge along the longer side."""
    o = overhang
    a0, a1, b0, b1 = x0 - o, x1 + o, y0 - o, y1 + o
    if (b1 - b0) >= (a1 - a0):
        hw = (a1 - a0) / 2
        cx = (a0 + a1) / 2
        r0, r1 = (cx, b0 + hw * 0.8, z + rise), (cx, b1 - hw * 0.8, z + rise)
        verts = [(a0, b0, z), (a1, b0, z), (a1, b1, z), (a0, b1, z), r0, r1]
        faces = [[0, 1, 4], [1, 2, 5, 4], [2, 3, 5], [3, 0, 4, 5], [0, 3, 2, 1]]
    else:
        hw = (b1 - b0) / 2
        cy = (b0 + b1) / 2
        r0, r1 = (a0 + hw * 0.8, cy, z + rise), (a1 - hw * 0.8, cy, z + rise)
        verts = [(a0, b0, z), (a1, b0, z), (a1, b1, z), (a0, b1, z), r0, r1]
        faces = [[0, 1, 5, 4], [1, 2, 5], [2, 3, 4, 5], [3, 0, 4], [0, 3, 2, 1]]
    B.mesh(verts, faces, m)


def house2(key, src, street, wall_mat, roof_mat="RoofBrown", floors=2, door=None, garage=True):
    """Tidy two-storey suburban house: smooth walls, white-framed windows, hipped tile roof, integrated garage."""
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    wall = 2.9 * floors + 0.3
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, 0.3, "CurbClean")
    B.box(x0, x1, y0, y1, 0.3, wall, wall_mat)
    B.box(x0 - 0.12, x1 + 0.12, y0 - 0.12, y1 + 0.12, wall - 0.25, wall, "StuccoWhite")              # eaves band
    hip_roof(B, x0, x1, y0, y1, wall, 1.8, 0.5, roof_mat)
    B.box(x0 + 1.5, x0 + 2.1, y0 + 1.2, y0 + 1.8, wall, wall + 2.4, "BrickClean")                   # chimney
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    street_face = pick_face(faces, street)
    gar = None
    if garage:
        F = street_face                                                                             # garage at the end
        gar = (F.a1 - 3.1, F.a1 - 0.5) if door is not None and door - F.a0 < 4.2 else (F.a0 + 0.5, F.a0 + 3.1)  # away from the door
        street_face.box(B, gar[0], gar[1], 0.3, 2.6, 0, 0.05, "GarageDoor")
        street_face.box(B, gar[0] - 0.12, gar[1] + 0.12, 2.6, 2.8, 0, 0.1, "StuccoWhite")
    for F in faces:
        L = F.a1 - F.a0
        n = max(1, int((L - 0.8) // 2.2))
        cols = [F.a0 + (L / n) * (i + 0.5) for i in range(n)]
        for f in range(floors):
            for c in cols:
                if F is street_face and f == 0 and ((door is not None and abs(c - door) < 1.2) or (gar and gar[0] - 0.6 < c < gar[1] + 0.6)):
                    continue
                kind = "WinPunchLit" if rng.random() < 0.2 else "WinPunch"
                window(B, F, c, 1.0 + f * 2.9, w=1.0, h=1.4, kind=kind, pool=CLEAN_WINDOWS)
    if door is not None:                                                                            # entrance canopy
        F = street_face
        F.box(B, door - 0.9, door + 0.9, 2.55, 2.7, 0, 0.9, "StuccoWhite")
        for c in (door - 0.85, door + 0.85):
            F.box(B, c - 0.06, c + 0.06, 0.0, 2.55, 0.78, 0.9, "StuccoWhite")
    return B.done(), gar


def pine(name, x, y, h=7.0):
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.16, h * 0.3, "Bark", seg=6)
    for k in range(4):
        z = h * (0.2 + k * 0.19)
        r = (h * 0.24) * (1 - k * 0.22)
        B.cyl(x, y, z, r, h * 0.32, "Pine", seg=7, r2=0.05)
    return B.done()


def round_tree(name, x, y, h=5.5, r=1.8, leaves="LeavesBright"):
    B = Builder(name, (x, y, 0))
    B.cyl(x, y, 0, 0.16, h * 0.55, "Bark", seg=6)
    for k in range(5):
        B.blob(x + rng.uniform(-0.6, 0.6), y + rng.uniform(-0.6, 0.6), h * 0.42 + rng.uniform(0, 0.9),
               r * rng.uniform(1.0, 1.35), r * rng.uniform(1.0, 1.35), r * rng.uniform(0.9, 1.15), leaves, lumpy=0.25)
    return B.done()


def pool(name, x0, x1, y0, y1):
    B = Builder(name, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0 - 0.5, x1 + 0.5, y0 - 0.5, y1 + 0.5, 0, 0.06, "ConcreteClean")
    B.box(x0, x1, y0, y1, 0.06, 0.07, "Water")
    return B.done()


def hills(name, inner, outer, height, seed=5, step=4.0):
    """Rolling green hills in a ring around the playable area (inner = (x0,x1,y0,y1) kept flat)."""
    r = np.random.default_rng(seed)
    xs = np.arange(outer[0], outer[1] + 0.1, step)
    ys = np.arange(outer[2], outer[3] + 0.1, step)
    g = r.random((len(ys) // 3 + 3, len(xs) // 3 + 3))
    def noise(i, j):
        fi, fj = i / 3.0, j / 3.0
        i0, j0 = int(fi), int(fj)
        ti, tj = fi - i0, fj - j0
        a = g[i0, j0] * (1 - tj) + g[i0, j0 + 1] * tj
        b = g[i0 + 1, j0] * (1 - tj) + g[i0 + 1, j0 + 1] * tj
        return a * (1 - ti) + b * ti
    verts, faces = [], []
    for i, y in enumerate(ys):
        for j, x in enumerate(xs):
            dx = max(inner[0] - x, 0, x - inner[1])
            dy = max(inner[2] - y, 0, y - inner[3])
            d = math.hypot(dx, dy)
            z = -0.15 if d <= 0 else min(height, d * 0.35) * (0.55 + 0.9 * noise(i, j)) - 0.15
            verts.append((x, y, z))
    W = len(xs)
    for i in range(len(ys) - 1):
        for j in range(W - 1):
            faces.append([i * W + j, i * W + j + 1, (i + 1) * W + j + 1, (i + 1) * W + j])
    B = Builder(name, (0, 0, 0))
    B.mesh(verts, faces, "GrassLush", tile=3.0)
    return B.done()


def skyline(name, inner, seed=7):
    """Distant towers beyond the edges of the map."""
    r = np.random.default_rng(seed)
    B = Builder(name, (0, 0, 0))
    for k in range(18):
        side = k % 4
        t = r.uniform(-1, 1)
        if side == 0: cx, cy = inner[1] + r.uniform(8, 22), t * (inner[3] + 10)
        elif side == 1: cx, cy = inner[0] - r.uniform(8, 22), t * (inner[3] + 10)
        elif side == 2: cx, cy = t * (inner[1] + 10), inner[3] + r.uniform(8, 22)
        else: cx, cy = t * (inner[1] + 10), inner[2] - r.uniform(8, 22)
        w, d, h = r.uniform(7, 14), r.uniform(7, 14), r.uniform(25, 70)
        m = ["Limestone", "GlassOffice", "LimestoneDark", "BrickClean"][k % 4]
        B.box(cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, 0, h, m, tile=3.0)
        B.box(cx - w / 2 + 1.5, cx + w / 2 - 1.5, cy - d / 2 + 1.5, cy + d / 2 - 1.5, h, h + r.uniform(3, 9), m, tile=3.0)
    return B.done()


def brownstone_row(key, src, street, floors=4, stoop=1.8, unit=4.6):
    """A row of brownstones (the doc's upkeep-property reference): reddish sandstone, a stoop with iron railings up
    to a raised parlour floor, bay windows on alternate houses, window boxes and a heavy cornice."""
    KEPT = ["WinCurtain", "WinCurtain", "WinPunch", "WinPunchLit", "WinDark"]         # kept up: no boards
    x0, x1 = src["min"][0], src["max"][0]
    y0, y1 = src["min"][2], src["max"][2]
    fh, parlour = 3.1, 1.5
    top = parlour + fh * floors
    B = Builder("Bldg_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    B.box(x0, x1, y0, y1, 0, top, "Brownstone")
    B.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, top - 0.6, top, "WoodDarkPolish")             # cornice
    B.box(x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, top - 0.8, top - 0.6, "Brownstone")
    faces = [Face("x", x1, 1, y0, y1), Face("x", x0, -1, y0, y1), Face("y", y1, 1, x0, x1), Face("y", y0, -1, x0, x1)]
    F = pick_face(faces, street)
    stoops = Builder("Prop_Stoops_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    rails = Builder("NoCol_Railings_" + key, ((x0 + x1) / 2, (y0 + y1) / 2, 0))
    n = max(1, int((F.a1 - F.a0) // unit))
    w = (F.a1 - F.a0) / n
    for u in range(n):
        a0 = F.a0 + u * w
        door = a0 + 1.1
        win = (a0 + 2.2, a0 + w - 0.4)
        F.box(B, a0 - 0.05, a0 + 0.05, 0, top - 0.8, 0, 0.1, "LimestoneDark")                    # party-wall line
        # garden level: windows under the stoop's landing
        F.box(B, win[0] + 0.2, win[1] - 0.2, 0.3, 1.2, 0, 0.04, rng.choice(KEPT))
        # the stoop: steps out to the sidewalk, sandstone cheek walls, iron rails
        steps = 7
        for k in range(steps):
            d1 = stoop - k * (stoop - 0.6) / steps
            F.box(stoops, door - 0.6, door + 0.6, 0, parlour * (k + 1) / steps, 0, d1, "Brownstone")
        for side in (-1, 1):
            F.box(stoops, door + side * 0.62 - 0.1, door + side * 0.62 + 0.1, 0, parlour + 0.3, 0, stoop, "Brownstone")
            for k in range(5):
                d = 0.3 + k * (stoop - 0.4) / 4
                F.box(rails, door + side * 0.62 - 0.02, door + side * 0.62 + 0.02, parlour + 0.3, parlour + 1.2 - k * 0.12, d, d + 0.04, "MetalDark")
            F.box(rails, door + side * 0.62 - 0.03, door + side * 0.62 + 0.03, parlour + 1.15, parlour + 1.2, 0.2, stoop, "MetalDark")
        # front door with a hood
        F.box(B, door - 0.5, door + 0.5, parlour, parlour + 2.4, 0, 0.06, rng.choice(["WoodDarkPolish", "DoorGreen"]))
        F.box(B, door - 0.75, door + 0.75, parlour + 2.45, parlour + 2.75, 0, 0.45, "WoodDarkPolish")
        wall_bulb(f"LightSrc_Bulb_{key}{u}", F, door + 0.75, parlour + 2.0)
        # upper floors: a bay on alternate houses, two tall windows otherwise
        bay = u % 2 == 0
        for f in range(floors):
            z = parlour + f * fh
            if bay:
                F.box(B, win[0], win[1], z + 0.1, z + fh, 0, 0.55, "Brownstone")
                F.box(B, win[0] + 0.2, win[1] - 0.2, z + 0.6, z + 2.5, 0.55, 0.59, rng.choice(KEPT))
            else:
                for c in (win[0] + 0.5, win[1] - 0.5):
                    F.box(B, c - 0.4, c + 0.4, z + 0.6, z + 2.5, 0, 0.04, rng.choice(KEPT))
                    F.box(B, c - 0.5, c + 0.5, z + 2.5, z + 2.7, 0, 0.12, "LimestoneDark")       # lintel
                    if f == 0 and rng.random() < 0.6:                                               # window box
                        F.box(B, c - 0.45, c + 0.45, z + 0.35, z + 0.6, 0, 0.3, "WoodDarkPolish")
                        F.box(B, c - 0.42, c + 0.42, z + 0.6, z + 0.8, 0.02, 0.28, "Hedge")
    stoops.done()
    rails.done()
    return B.done(), top


def barricade(name, x, y, along_y=False, length=1.8):
    """A striped road-closed sawhorse: where the player's part of the city ends."""
    B = Builder(name, (x, y, 0))
    h = length / 2
    def bx(a0, a1, d0, d1, z0, z1, m):
        if along_y: B.box(x + d0, x + d1, y + a0, y + a1, z0, z1, m)
        else: B.box(x + a0, x + a1, y + d0, y + d1, z0, z1, m)
    for side in (-1, 1):                                     # A-frame legs
        a = side * (h - 0.15)
        bx(a - 0.04, a + 0.04, -0.3, -0.24, 0, 1.0, "Steel")
        bx(a - 0.04, a + 0.04, 0.24, 0.3, 0, 1.0, "Steel")
    k, a = 0, -h
    while a < h - 0.01:                                      # the striped boards
        a2 = min(a + 0.3, h)
        for z0 in (0.75, 0.4):
            bx(a, a2, -0.04, 0.04, z0, z0 + 0.22, "CarRed" if k % 2 == 0 else "LineWhite")
        a, k = a2, k + 1
    return B.done()


def barricades(prefix, x0, x1, y, along_y=False):
    """A line of sawhorses across a road from x0 to x1 (or along Y when along_y), at y (or x)."""
    n, a = 0, x0 + 0.9
    while a <= x1 - 0.8:
        n += 1
        if along_y: barricade(f"NoCol_{prefix}{n}", y, a, along_y=True)
        else: barricade(f"NoCol_{prefix}{n}", a, y)
        a += 2.0


def ext_row(prefix, axis, a0, a1, front, street, depth=10.0, facades=("BrickClean",), clean=True, floors=(3, 6),
            brownstones=0.0):
    """Filler past the edge of the playable street (so the city doesn't just stop): a row of buildings along an
    extended road. axis "y": the row runs along Y and street = +1/-1 (road on the +X/-X side, front at X = front);
    axis "x": the row runs along X and street = "N"/"S" (road to the north/south, front at Y = front)."""
    a, k = a0, 0
    while a1 - a > 4.0:
        w = min(a1 - a, rng.uniform(8.0, 12.0))
        if axis == "y":
            xa, xb = sorted((front, front - street * depth))
            src = {"min": [xa, 0, a], "max": [xb, 0, a + w]}
        else:
            ya, yb = sorted((front, front + (-depth if street == "N" else depth)))
            src = {"min": [a, 0, ya], "max": [a + w, 0, yb]}
        k += 1
        key = f"Ext{prefix}{k}"
        if rng.random() < brownstones: brownstone_row(key, src, street, floors=int(rng.integers(3, 5)))
        else: building(key, src, int(rng.integers(floors[0], floors[1] + 1)), str(rng.choice(list(facades))), street, clean=clean)
        a += w + 0.2


WIDEN = 1.4          # how much wider the streets are (road + sidewalks), between the building lines


def warp_offset(v, zones):
    """How far a coordinate moves when the street zones (centre, half-width, factor) are widened: inside a zone it
    stretches about the centre; beyond it everything shifts out by the zone's extra width."""
    off = 0.0
    for c, h, k in zones:
        d = v - c
        if d > h: off += h * (k - 1)
        elif d < -h: off -= h * (k - 1)
        else: off += d * (k - 1)
    return off


def warp_scene(zones_x=(), zones_y=(), rigid=3.0):
    """Widen the streets after building: big things (roads, sidewalks, buildings) are warped vertex by vertex;
    small things (lamps, benches, trees, signs, barricades) move whole so they keep their shape."""
    import bpy
    from mathutils import Vector
    bpy.context.view_layer.update()            # (new objects' world matrices aren't computed until this)
    for ob in bpy.data.objects:
        if ob.type != "MESH": continue
        mw = ob.matrix_world
        pts = [mw @ Vector(c) for c in ob.bound_box]
        xs = [p.x for p in pts]; ys = [p.y for p in pts]
        small_x = (max(xs) - min(xs)) < rigid
        small_y = (max(ys) - min(ys)) < rigid
        cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
        dx_rigid = warp_offset(cx, zones_x) if small_x else 0.0
        dy_rigid = warp_offset(cy, zones_y) if small_y else 0.0
        inv = mw.inverted()
        me = ob.data
        for v in me.vertices:
            w = mw @ v.co
            w.x += dx_rigid if small_x else warp_offset(w.x, zones_x)
            w.y += dy_rigid if small_y else warp_offset(w.y, zones_y)
            v.co = inv @ w
        me.update()
