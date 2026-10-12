# Builds the traffic cars (one FBX each, at the origin facing +Z in Unity) for StreetLife.
import sys, importlib
HERE = "/mnt/13e3623f-9e8e-462a-93b0-a94e3f20b9b1/GitStuff/SharathSajeev_LILA_GD_Test/ArtSource"
sys.path.insert(0, HERE)
import apt_textures, street_textures, street_kit, car_kit
for m in (apt_textures, street_textures, street_kit, car_kit): importlib.reload(m)
from street_kit import *
from car_kit import car
import bpy

CARS = [("Car_SedanTeal", "sedan", "CarTeal"), ("Car_SedanMint", "sedan", "CarMint"), ("Car_SedanMaroon", "sedan", "CarMaroon"),
        ("Car_SedanCream", "sedan", "CarCream"), ("Car_SedanBlack", "sedan", "CarBlack"), ("Car_HatchRed", "hatch", "CarRed"),
        ("Car_VanWhite", "van", "CarWhite"), ("Car_Taxi", "taxi", "CarTaxi")]
for name, kind, paint in CARS:
    reset(41)
    car(name, kind, paint)
    os.makedirs(PROJECT + "/Assets/Hitman/Art/Vehicles", exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=f"{PROJECT}/Assets/Hitman/Art/Vehicles/{name}.fbx", use_selection=False, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
print("CARS", len(CARS))
