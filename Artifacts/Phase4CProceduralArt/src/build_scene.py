#!/usr/bin/env python3
"""Rebuild the Pulse Factory Phase 4C procedural mechanical-art proof."""

from __future__ import annotations

import json
import hashlib
import math
import resource
import sys
import time
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector


ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = ROOT.parents[1]
LEVEL_PATH = PROJECT_ROOT / "Assets/Resources/Levels/shared-control.json"
CATALOG_PATH = PROJECT_ROOT / "Assets/Resources/Levels/catalog.json"
PALETTE_PATH = ROOT / "materials/palette.json"
MODELS_DIR = ROOT / "generated/models"
RENDERS_DIR = ROOT / "rendered"
EXPORTS_DIR = ROOT / "exports"
REPORT_PATH = ROOT / "validation/build-report.json"
MANIFEST_PATH = ROOT / "asset-manifest.json"

UNIT_LENGTH = 0.75
BELT_WIDTH = 0.48
PACKAGE_APPROACH_OFFSET = 0.90
BASE_DECK_Z = 0.34
BRIDGE_EDGE_ID = "J1B"
BRIDGE_LIFT = 0.58
BOARD_WIDTH = 3.9
BOARD_LENGTH = 5.85
BOARD_RENDER = (1200, 1800)
PROOF_RENDER = (640, 640)
PROOF_FRAMES = (1, 16, 32, 48)


def hex_color(value: str) -> tuple[float, float, float, float]:
    raw = value.lstrip("#")
    return tuple(int(raw[index : index + 2], 16) / 255.0 for index in (0, 2, 4)) + (1.0,)


def read_json(path: Path) -> dict:
    with path.open(encoding="utf-8") as file:
        return json.load(file)


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def make_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> bpy.types.Object:
    for existing in list(obj.users_collection):
        existing.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def make_materials(palette_data: dict) -> dict[str, bpy.types.Material]:
    result = {}
    for name, spec in palette_data["palette"].items():
        material = bpy.data.materials.new("MAT_" + name)
        material.diffuse_color = hex_color(spec["hex"])
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = material.diffuse_color
        shader.inputs["Metallic"].default_value = spec.get("metallic", 0.0)
        shader.inputs["Roughness"].default_value = spec.get("roughness", 0.5)
        emission = shader.inputs.get("Emission Color")
        if emission is not None and spec.get("emission_strength", 0) > 0:
            emission.default_value = material.diffuse_color
            shader.inputs["Emission Strength"].default_value = spec["emission_strength"]
        result[name] = material
    return result


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> bpy.types.Object:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    return obj


def bevel_box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    bevel: float = 0.025,
    collection: bpy.types.Collection | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    set_active(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        modifier = obj.modifiers.new("Soft machined edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        modifier.limit_method = "ANGLE"
        set_active(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    assign_material(obj, material)
    if collection is not None:
        move_to_collection(obj, collection)
    return obj


def cylinder(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection | None = None,
    rotation: tuple[float, float, float] = (0, 0, 0),
    vertices: int = 32,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    assign_material(obj, material)
    if collection is not None:
        move_to_collection(obj, collection)
    return obj


def torus(
    name: str,
    location: tuple[float, float, float],
    major_radius: float,
    minor_radius: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=48,
        minor_segments=10,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    assign_material(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_text(
    name: str,
    text: str,
    location: tuple[float, float, float],
    size: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    rotation: tuple[float, float, float] = (0, 0, 0),
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name + "_Curve", "FONT")
    curve.body = text
    curve.size = size
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.extrude = 0.001
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    assign_material(obj, material)
    set_active(obj)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    return obj


def create_scene_collection() -> bpy.types.Collection:
    scene = bpy.context.scene
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(scene.collection.children):
        scene.collection.children.unlink(collection)
        if collection.users == 0:
            bpy.data.collections.remove(collection)
    return make_collection("PF_Phase4C_Scene")


def create_conveyor_prototype(materials: dict, collection: bpy.types.Collection) -> list[bpy.types.Object]:
    made = []
    length = UNIT_LENGTH
    made.append(bevel_box("ASSET_CONVEYOR_Deck", (0, 0, 0.16), (length, 0.62, 0.2),
                          materials["factory_base"], 0.055, collection))
    made.append(bevel_box("ASSET_CONVEYOR_Belt", (0, 0, BASE_DECK_Z), (length - 0.12, BELT_WIDTH, 0.075),
                          materials["dark_rubber"], 0.024, collection))
    for side in (-1, 1):
        y = side * 0.32
        made.append(bevel_box(f"ASSET_CONVEYOR_Rail_{side}", (0, y, 0.41), (length, 0.085, 0.22),
                              materials["painted_steel"], 0.035, collection))
        made.append(bevel_box(f"ASSET_CONVEYOR_RailCap_{side}", (0, y, 0.525), (length - 0.05, 0.045, 0.035),
                              materials["brushed_metal"], 0.012, collection))
    for end in (-1, 1):
        x = end * (length / 2 - 0.075)
        roller = cylinder(f"ASSET_CONVEYOR_Roller_{end}", (x, 0, BASE_DECK_Z - 0.012),
                          0.068, 0.54, materials["brushed_metal"], collection,
                          rotation=(math.pi / 2, 0, 0))
        made.append(roller)
        for side in (-1, 1):
            made.append(cylinder(f"ASSET_CONVEYOR_Axle_{end}_{side}", (x, side * 0.29, BASE_DECK_Z - 0.012),
                                 0.035, 0.025, materials["warm_brass"], collection,
                                 rotation=(math.pi / 2, 0, 0), vertices=20))
    for x in (-0.23, 0.0, 0.23):
        made.append(bevel_box(f"ASSET_CONVEYOR_Tread_{x}", (x, 0, BASE_DECK_Z + 0.041),
                              (0.024, BELT_WIDTH - 0.035, 0.008), materials["brushed_metal"], 0.003, collection))
    for x in (-0.23, 0.23):
        for side in (-1, 1):
            made.append(cylinder(f"ASSET_CONVEYOR_Bolt_{x}_{side}", (x, side * 0.32, 0.475),
                                 0.027, 0.018, materials["warm_brass"], collection, vertices=16))
    return made


def create_diverter_prototype(materials: dict, collection: bpy.types.Collection) -> tuple[list[bpy.types.Object], bpy.types.Object]:
    made = []
    made.append(cylinder("ASSET_DIVERTER_BaseFoot", (0, 0, 0.14), 0.54, 0.22,
                         materials["factory_base"], collection))
    made.append(cylinder("ASSET_DIVERTER_BasePaint", (0, 0, 0.265), 0.49, 0.10,
                         materials["painted_steel"], collection))
    made.append(torus("ASSET_DIVERTER_OuterRing", (0, 0, 0.33), 0.415, 0.025,
                      materials["brushed_metal"], collection))
    made.append(cylinder("ASSET_DIVERTER_RotatingPlate", (0, 0, 0.355), 0.32, 0.08,
                         materials["painted_steel_dark"], collection))
    made.append(torus("ASSET_DIVERTER_PivotRing", (0, 0, 0.405), 0.105, 0.025,
                      materials["warm_brass"], collection))
    made.append(cylinder("ASSET_DIVERTER_PivotPin", (0, 0, 0.43), 0.075, 0.10,
                         materials["brushed_metal"], collection))
    for angle in range(0, 360, 90):
        radians = math.radians(angle + 45)
        loc = (math.cos(radians) * 0.43, math.sin(radians) * 0.43, 0.34)
        made.append(cylinder(f"ASSET_DIVERTER_Fastener_{angle}", loc, 0.035, 0.035,
                             materials["warm_brass"], collection, vertices=16))

    pivot = bpy.data.objects.new("ASSET_DIVERTER_ArmPivot", None)
    collection.objects.link(pivot)
    pivot.empty_display_type = "CIRCLE"
    pivot.empty_display_size = 0.2
    pivot.location = (0, 0, 0.45)

    verts = [
        (-0.12, -0.07, 0.0), (0.12, -0.07, 0.0), (0.14, 0.08, 0.0),
        (0.10, 0.43, 0.0), (0.0, 0.60, 0.0), (-0.10, 0.43, 0.0), (-0.14, 0.08, 0.0),
        (-0.12, -0.07, 0.09), (0.12, -0.07, 0.09), (0.14, 0.08, 0.09),
        (0.10, 0.43, 0.09), (0.0, 0.60, 0.09), (-0.10, 0.43, 0.09), (-0.14, 0.08, 0.09),
    ]
    faces = [
        tuple(range(7)), tuple(range(7, 14)),
        (0, 1, 8, 7), (1, 2, 9, 8), (2, 3, 10, 9), (3, 4, 11, 10),
        (4, 5, 12, 11), (5, 6, 13, 12), (6, 0, 7, 13),
    ]
    mesh = bpy.data.meshes.new("ASSET_DIVERTER_ArmMesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(materials["brushed_metal"])
    arm = bpy.data.objects.new("ASSET_DIVERTER_RoutingArm", mesh)
    collection.objects.link(arm)
    arm.parent = pivot
    arm.location = (0, 0, 0)
    bevel = arm.modifiers.new("Arm edge bevel", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 3

    badge = cylinder("ASSET_DIVERTER_GroupBadge", (0, -0.3, 0.435), 0.105, 0.035,
                     materials["warm_brass"], collection)
    made.append(badge)
    add_text("ASSET_DIVERTER_GroupA", "A", (0, -0.3, 0.458), 0.105,
             materials["factory_base"], collection)
    return made + [arm], pivot


def create_crate(
    prefix: str,
    material: bpy.types.Material,
    materials: dict,
    collection: bpy.types.Collection,
    location: tuple[float, float, float] = (0, 0, 0),
) -> list[bpy.types.Object]:
    x, y, z = location
    made = []
    made.append(bevel_box(prefix + "_CrateBody", (x, y, z + 0.25), (0.48, 0.42, 0.42),
                          material, 0.055, collection))
    made.append(bevel_box(prefix + "_TopPlate", (x, y, z + 0.465), (0.30, 0.27, 0.025),
                          materials["cargo_identifier"], 0.024, collection))
    for side_x in (-1, 1):
        for side_y in (-1, 1):
            made.append(bevel_box(prefix + f"_Corner_{side_x}_{side_y}",
                                  (x + side_x * 0.19, y + side_y * 0.16, z + 0.25),
                                  (0.065, 0.065, 0.38), materials["brushed_metal"], 0.018, collection))
    made.append(bevel_box(prefix + "_LowerBand", (x, y, z + 0.12), (0.492, 0.432, 0.045),
                          materials["warm_brass"], 0.012, collection))
    return made


def duplicate_component(
    source: bpy.types.Object,
    name: str,
    center: Vector,
    rotation,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    obj = source.copy()
    if source.data is not None:
        obj.data = source.data
    collection.objects.link(obj)
    local_location = source.location.copy()
    obj.location = center + rotation @ local_location
    local_rotation = source.rotation_euler.to_quaternion()
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = rotation @ local_rotation
    obj.scale = source.scale.copy()
    obj.name = name
    obj.hide_render = False
    obj.hide_viewport = False
    return obj


def route_z(edge_id: str, t: float, base_z: float) -> float:
    if edge_id != BRIDGE_EDGE_ID:
        return base_z
    if t <= 0.12 or t >= 0.88:
        lift = 0.0
    elif t < 0.42:
        ratio = (t - 0.12) / 0.30
        lift = BRIDGE_LIFT * 0.5 * (1 - math.cos(math.pi * ratio))
    elif t <= 0.58:
        lift = BRIDGE_LIFT
    else:
        ratio = (t - 0.58) / 0.30
        lift = BRIDGE_LIFT * 0.5 * (1 + math.cos(math.pi * ratio))
    return base_z + lift


def route_tangent(edge_id: str, start: Vector, end: Vector, t: float, base_z: float) -> Vector:
    epsilon = 0.001
    before = max(0.0, t - epsilon)
    after = min(1.0, t + epsilon)
    p0 = start.lerp(end, before)
    p1 = start.lerp(end, after)
    p0.z = route_z(edge_id, before, base_z)
    p1.z = route_z(edge_id, after, base_z)
    return (p1 - p0).normalized()


def add_module_inlay(
    name: str,
    center: Vector,
    rotation,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    obj = bevel_box(name, tuple(center + rotation @ Vector((0, 0, BASE_DECK_Z + 0.045))),
                    (0.42, 0.028, 0.008), material, 0.004, collection)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = rotation
    return obj


def add_bridge_support(
    name: str,
    location: Vector,
    top_z: float,
    materials: dict,
    collection: bpy.types.Collection,
) -> None:
    height = max(0.12, top_z - 0.06)
    bevel_box(name, (location.x, location.y, height / 2),
              (0.12, 0.12, height), materials["factory_base"], 0.025, collection)
    bevel_box(name + "_Foot", (location.x, location.y, 0.045),
              (0.22, 0.22, 0.09), materials["brushed_metal"], 0.018, collection)


def create_station(
    node: dict,
    position: Vector,
    materials: dict,
    diverter_parts: list[bpy.types.Object],
    collection: bpy.types.Collection,
) -> tuple[list[bpy.types.Object], bpy.types.Object | None]:
    node_id = node["id"]
    prefix = "BOARD_NODE_" + node_id
    made = []
    pivot = None
    if node["type"] == "Diverter":
        for source in diverter_parts:
            if source.name == "ASSET_DIVERTER_RoutingArm":
                continue
            duplicate_component(source, prefix + "_" + source.name.removeprefix("ASSET_DIVERTER_"),
                                Vector((position.x, position.y, 0)), Quaternion((1, 0, 0, 0)), collection)
        pivot = bpy.data.objects.new(prefix + "_ArmPivot", None)
        collection.objects.link(pivot)
        pivot.empty_display_type = "CIRCLE"
        pivot.empty_display_size = 0.24
        pivot.location = (position.x, position.y, 0.45)
        arm_source = next(obj for obj in diverter_parts if obj.name == "ASSET_DIVERTER_RoutingArm")
        arm = arm_source.copy()
        arm.data = arm_source.data
        collection.objects.link(arm)
        arm.name = prefix + "_RoutingArm"
        arm.parent = pivot
        arm.location = (0, 0, 0)
        arm.rotation_euler = (0, 0, 0)
        arm.hide_render = False
        arm.hide_viewport = False
        made.extend([obj for obj in collection.objects if obj.name.startswith(prefix)])
        return made, pivot

    if node["type"] == "Source":
        made.append(bevel_box(prefix + "_Hopper", (position.x, position.y, 0.39),
                              (0.92, 0.68, 0.46), materials["factory_base"], 0.09, collection))
        made.append(bevel_box(prefix + "_PaintedFront", (position.x, position.y - 0.355, 0.41),
                              (0.68, 0.08, 0.31), materials["painted_steel"], 0.03, collection))
        made.append(bevel_box(prefix + "_FeedSlot", (position.x, position.y, 0.64),
                              (0.55, 0.32, 0.04), materials["dark_rubber"], 0.025, collection))
        add_text(prefix + "_NodeLabel", node_id, (position.x, position.y - 0.20, 0.665),
                 0.16, materials["cargo_identifier"], collection)
    elif node["type"] == "Destination":
        accent = materials["cargo_red"] if node["accepts"] == "red" else materials["cargo_blue"]
        made.append(bevel_box(prefix + "_Receiver", (position.x, position.y, 0.32),
                              (0.92, 0.82, 0.30), materials["factory_base"], 0.075, collection))
        made.append(bevel_box(prefix + "_ReceivingPad", (position.x, position.y, 0.49),
                              (0.68, 0.58, 0.07), accent, 0.045, collection))
        for side_x in (-1, 1):
            made.append(bevel_box(prefix + f"_SideRail_{side_x}",
                                  (position.x + side_x * 0.37, position.y, 0.61),
                                  (0.09, 0.68, 0.20), materials["brushed_metal"], 0.03, collection))
        made.append(bevel_box(prefix + "_BackStop", (position.x, position.y + 0.30, 0.61),
                              (0.82, 0.09, 0.20), materials["painted_steel"], 0.03, collection))
        add_text(prefix + "_NodeLabel", node["id"], (position.x, position.y - 0.21, 0.545),
                 0.20, materials["cargo_identifier"], collection)
    return made, pivot


def make_light(name: str, location: tuple[float, float, float], energy: float,
               size: float, target: Vector) -> bpy.types.Object:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def setup_render(scene: bpy.types.Scene, width: int, height: int, samples: int) -> None:
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.compression = 18
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.resolution_percentage = 100
    scene.render.use_file_extension = True


def point_camera(camera: bpy.types.Object, position: Vector, target: Vector, ortho_scale: float) -> None:
    camera.location = position
    camera.rotation_euler = (target - position).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale


def node_position(node: dict) -> Vector:
    return Vector(((node["x"] - 0.5) * 3.3066666667, (node["y"] - 0.5) * 5.0, 0))


def state_routes(level: dict, switches: dict) -> dict[str, str]:
    edges = {edge["id"]: edge for edge in level["edges"]}
    route = {}
    for node in level["nodes"]:
        if node["type"] != "Diverter":
            continue
        state = bool(switches[node["group"].lower()])
        branch = state ^ bool(node.get("inverted", False))
        selected = node["trueEdge"] if branch else node["falseEdge"]
        route[node["id"]] = edges[selected]["to"]
    return route


def angle_to_target(position: Vector, target: Vector) -> float:
    delta = target - position
    return math.atan2(delta.y, delta.x) - math.pi / 2


def interpolate_angle(start: float, end: float) -> float:
    delta = (end - start + math.pi) % (2 * math.pi) - math.pi
    return start + delta


def export_collection(collection: bpy.types.Collection, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    saved_visibility = {obj.name: (obj.hide_render, obj.hide_viewport) for obj in collection.objects}
    for obj in collection.objects:
        obj.hide_render = False
        obj.hide_viewport = False
        obj.select_set(True)
    if not any(obj.select_get() for obj in bpy.context.scene.objects):
        raise RuntimeError("No visible objects selected for export: " + collection.name)
    bpy.context.view_layer.objects.active = next(obj for obj in collection.objects if obj.select_get())
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
    )
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj.hide_render, obj.hide_viewport = saved_visibility[obj.name]


def export_collection_fbx(collection: bpy.types.Collection, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    saved_visibility = {obj.name: (obj.hide_render, obj.hide_viewport) for obj in collection.objects}
    for obj in collection.objects:
        obj.hide_render = False
        obj.hide_viewport = False
        obj.select_set(True)
    if not any(obj.select_get() for obj in bpy.context.scene.objects):
        raise RuntimeError("No visible objects selected for FBX export: " + collection.name)
    bpy.context.view_layer.objects.active = next(obj for obj in collection.objects if obj.select_get())
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        bake_anim=False,
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj.hide_render, obj.hide_viewport = saved_visibility[obj.name]


def render_isolated_asset(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    asset_collection: bpy.types.Collection,
    prefix: str,
    target: Vector,
    path: Path,
) -> dict:
    visible_names = {obj.name for obj in asset_collection.objects}
    light_and_camera = {obj.name for obj in scene.objects if obj.type in {"LIGHT", "CAMERA"}}
    saved = {obj.name: obj.hide_render for obj in scene.objects}
    for obj in scene.objects:
        obj.hide_render = obj.name not in visible_names | light_and_camera
    print(f"PHASE4C_ISOLATED {prefix} objectCount={len(visible_names)}")
    point_camera(camera, Vector((4.8, -6.5, 6.4)), target, 2.25)
    scene.render.film_transparent = True
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(path)
    start = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    elapsed = time.perf_counter() - start
    image = bpy.data.images.load(str(path), check_existing=False)
    if image.channels != 4:
        raise RuntimeError(f"Expected RGBA render for {path.name}, got {image.channels} channels")
    pixel_values = image.pixels[:]
    alpha_values = pixel_values[3::4]
    alpha_range = (min(alpha_values), max(alpha_values))
    bpy.data.images.remove(image)
    for obj in scene.objects:
        obj.hide_render = saved[obj.name]
    scene.render.film_transparent = False
    return {
        "file": str(path.relative_to(ROOT)),
        "dimensions": [768, 768],
        "channels": 4,
        "alphaMinMax": list(alpha_range),
        "renderSeconds": round(elapsed, 3),
    }


def build() -> None:
    started = time.perf_counter()
    level = read_json(LEVEL_PATH)
    catalog = read_json(CATALOG_PATH)
    palette_data = read_json(PALETTE_PATH)
    entry = next(item for item in catalog["levels"] if item["levelId"] == level["id"])
    first_command = entry["solution"][0]
    switches = dict(level["initialSwitches"])
    if first_command.startswith("Pulse"):
        switch_key = first_command.removeprefix("Pulse").lower()
        switches[switch_key] = not switches[switch_key]
    routes_on = state_routes(level, switches)
    switches_off = dict(level["initialSwitches"])
    routes_off = state_routes(level, switches_off)
    positions = {node["id"]: node_position(node) for node in level["nodes"]}

    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    RENDERS_DIR.mkdir(parents=True, exist_ok=True)
    EXPORTS_DIR.mkdir(parents=True, exist_ok=True)
    (ROOT / "validation").mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.name = "PulseFactory_Level2_SharedControl"
    scene_collection = create_scene_collection()
    scene = bpy.context.scene
    scene.name = "PulseFactory_Level2_SharedControl"

    materials = make_materials(palette_data)
    conveyor_collection = make_collection("PROTO_CONVEYOR_0p75m")
    conveyor_parts = create_conveyor_prototype(materials, conveyor_collection)
    diverter_collection = make_collection("PROTO_DIVERTER")
    diverter_parts, diverter_pivot = create_diverter_prototype(materials, diverter_collection)
    cargo_red_collection = make_collection("PROTO_CARGO_RED")
    cargo_red_parts = create_crate("ASSET_CARGO_RED", materials["cargo_red"], materials, cargo_red_collection)
    cargo_blue_collection = make_collection("PROTO_CARGO_BLUE")
    cargo_blue_parts = create_crate("ASSET_CARGO_BLUE", materials["cargo_blue"], materials, cargo_blue_collection)

    # Prototype objects remain in the file and exports. Hide them from the board render.
    prototype_collections = [conveyor_collection, diverter_collection, cargo_red_collection, cargo_blue_collection]
    for collection in prototype_collections:
        for obj in collection.objects:
            obj.hide_render = True
            obj.hide_viewport = True

    platform = bevel_box("BOARD_PLATFORM", (0, 0, -0.15), (BOARD_WIDTH, BOARD_LENGTH, 0.30),
                         materials["board_ground"], 0.12, scene_collection)
    bevel_box("BOARD_PLATFORM_Inlay", (0, 0, 0.012), (BOARD_WIDTH - 0.16, BOARD_LENGTH - 0.16, 0.035),
              materials["factory_base"], 0.09, scene_collection)
    bevel_box("BOARD_PLATFORM_InnerPlate", (0, 0, 0.042), (BOARD_WIDTH - 0.36, BOARD_LENGTH - 0.36, 0.025),
              materials["board_ground"], 0.08, scene_collection)

    nodes = {node["id"]: node for node in level["nodes"]}
    diverter_pivots: dict[str, bpy.types.Object] = {}
    for node in level["nodes"]:
        _, pivot = create_station(node, positions[node["id"]], materials, diverter_parts, scene_collection)
        if pivot is not None:
            diverter_pivots[node["id"]] = pivot

    edges = {edge["id"]: edge for edge in level["edges"]}
    selected_edge_ids = set()
    for diverter_id, destination in routes_on.items():
        selected_edge_ids.update(edge["id"] for edge in level["edges"]
                                 if edge["from"] == diverter_id and edge["to"] == destination)
    selected_edge_ids.update(edge["id"] for edge in level["edges"]
                             if nodes[edge["from"]]["type"] != "Diverter")

    module_counts = {}
    proof_near_objects = set()
    for edge in level["edges"]:
        start = positions[edge["from"]]
        end = positions[edge["to"]]
        planar_length = (end - start).length
        modules = round(planar_length / UNIT_LENGTH)
        if abs(planar_length - modules * UNIT_LENGTH) > 0.002:
            raise RuntimeError(f"Edge {edge['id']} is not an integer number of fixed modules: {planar_length}")
        module_counts[edge["id"]] = modules
        is_selected = edge["id"] in selected_edge_ids
        for index in range(modules):
            t = (index + 0.5) / modules
            center = start.lerp(end, t)
            center.z = route_z(edge["id"], t, BASE_DECK_Z) - BASE_DECK_Z
            tangent = route_tangent(edge["id"], start, end, t, BASE_DECK_Z)
            rotation = tangent.to_track_quat("X", "Z")
            for source in conveyor_parts:
                obj = duplicate_component(source,
                    f"BOARD_EDGE_{edge['id']}_{index}_{source.name.removeprefix('ASSET_CONVEYOR_')}",
                    center, rotation, scene_collection)
                if min((center - positions["J1"]).length, (center - positions["J2"]).length) < 1.15:
                    proof_near_objects.add(obj.name)
            if is_selected:
                inlay = add_module_inlay(f"BOARD_EDGE_{edge['id']}_{index}_ActiveInlay",
                                         center, rotation, materials["active_mint"], scene_collection)
                if min((center - positions["J1"]).length, (center - positions["J2"]).length) < 1.15:
                    proof_near_objects.add(inlay.name)
            if edge["id"] == BRIDGE_EDGE_ID and index in (1, 2):
                normal = Vector((-tangent.y, tangent.x, 0)).normalized()
                for side in (-1, 1):
                    support_pos = center + normal * side * 0.35
                    add_bridge_support(f"BOARD_BRIDGE_SUPPORT_{index}_{side}", support_pos,
                                       center.z + BASE_DECK_Z - 0.06, materials, scene_collection)

    # Static packages are placed at the diverters after the first catalog command.
    package_records = []
    source_to_node = {edge["from"]: edge["to"] for edge in level["edges"]}
    for spawn in level["spawns"]:
        if spawn["beat"] != 1:
            continue
        node_id = source_to_node[spawn["source"]]
        node_position_world = positions[node_id]
        incoming_direction = (node_position_world - positions[spawn["source"]]).normalized()
        package_position = node_position_world - incoming_direction * PACKAGE_APPROACH_OFFSET
        package_position.z = BASE_DECK_Z - 0.04
        package_kind = spawn["kind"]
        parts = cargo_red_parts if package_kind == "red" else cargo_blue_parts
        for source in parts:
            obj = source.copy()
            obj.data = source.data
            scene_collection.objects.link(obj)
            obj.location = package_position + source.location
            obj.rotation_euler = source.rotation_euler
            obj.hide_render = False
            obj.hide_viewport = False
            obj.name = f"BOARD_PACKAGE_{spawn['id']}_{source.name.removeprefix('ASSET_CARGO_' + package_kind.upper() + '_')}"
        swatch = bevel_box(
            f"BOARD_PACKAGE_{spawn['id']}_IdentitySwatch",
            (package_position.x, package_position.y, package_position.z + 0.493),
            (0.14, 0.14, 0.02),
            materials["cargo_red"] if package_kind == "red" else materials["cargo_blue"],
            0.015,
            scene_collection,
        )
        swatch.hide_render = False
        swatch.hide_viewport = False
        package_records.append({
            "id": spawn["id"],
            "kind": package_kind,
            "node": node_id,
            "visualOffsetFromNodeMeters": PACKAGE_APPROACH_OFFSET,
        })

    for node in level["nodes"]:
        if node["type"] != "Diverter":
            continue
        pivot = diverter_pivots[node["id"]]
        target_off = positions[routes_off[node["id"]]]
        target_on = positions[routes_on[node["id"]]]
        off_angle = angle_to_target(positions[node["id"]], target_off)
        on_angle = interpolate_angle(off_angle, angle_to_target(positions[node["id"]], target_on))
        pivot.rotation_euler.z = off_angle
        pivot.keyframe_insert(data_path="rotation_euler", index=2, frame=1)
        pivot.keyframe_insert(data_path="rotation_euler", index=2, frame=14)
        pivot.rotation_euler.z = on_angle
        pivot.keyframe_insert(data_path="rotation_euler", index=2, frame=32)
        pivot.keyframe_insert(data_path="rotation_euler", index=2, frame=48)
        proof_near_objects.add(pivot.name)
        for obj in scene_collection.objects:
            if obj.name.startswith(f"BOARD_NODE_{node['id']}_"):
                proof_near_objects.add(obj.name)

    world = bpy.data.worlds.new("PF_World") if bpy.data.worlds.get("PF_World") is None else bpy.data.worlds["PF_World"]
    scene.world = world
    world.node_tree.nodes["Background"].inputs["Color"].default_value = hex_color(palette_data["palette"]["background"]["hex"])
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.18
    make_light("KEY_Softbox", (-4.2, -3.4, 8.5), 520, 6.0, Vector((0, 0, 0)))
    make_light("FILL_Softbox", (4.8, 1.5, 6.5), 250, 5.0, Vector((0, 0, 0)))
    make_light("RIM_Softbox", (-2.0, 5.2, 7.0), 380, 4.0, Vector((0, 0, 0)))
    camera_data = bpy.data.cameras.new("CAM_Level2")
    camera = bpy.data.objects.new("CAM_Level2", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    board_target = Vector((0, 0, 0.1))
    board_camera_position = Vector((4.1, -5.3, 17.5))
    point_camera(camera, board_camera_position, board_target, 10.2)

    setup_render(scene, *BOARD_RENDER, samples=28)
    scene.frame_start = 1
    scene.frame_end = PROOF_FRAMES[-1]
    scene.render.filepath = str(RENDERS_DIR / "level2_shared_control.png")
    scene.frame_set(PROOF_FRAMES[-1])
    print("PHASE4C_BOARD_NODES", [obj.name for obj in scene_collection.objects
                                  if obj.name.startswith("BOARD_NODE_") and not obj.name.endswith("_ArmPivot")])
    print("PHASE4C_BOARD_PACKAGES", [obj.name for obj in scene_collection.objects
                                     if obj.name.startswith("BOARD_PACKAGE_")])
    for node_id, pivot in diverter_pivots.items():
        scene.frame_set(1)
        angle_off = pivot.rotation_euler.z
        scene.frame_set(PROOF_FRAMES[-1])
        angle_on = pivot.rotation_euler.z
        print(f"PHASE4C_DIVERTER {node_id} off={angle_off:.4f} on={angle_on:.4f}")
    scene.frame_set(PROOF_FRAMES[-1])
    start_render = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    board_seconds = time.perf_counter() - start_render

    # Render transparent isolated previews from the same prototype geometry.
    asset_previews = {}
    camera_target = Vector((0, 0, 0.3))
    asset_previews["conveyor"] = render_isolated_asset(scene, camera, conveyor_collection,
        "ASSET_CONVEYOR_", camera_target, RENDERS_DIR / "asset_conveyor_rgba.png")
    asset_previews["diverter"] = render_isolated_asset(scene, camera, diverter_collection,
        "ASSET_DIVERTER_", camera_target, RENDERS_DIR / "asset_diverter_rgba.png")
    asset_previews["cargo_red"] = render_isolated_asset(scene, camera, cargo_red_collection,
        "ASSET_CARGO_RED_", camera_target, RENDERS_DIR / "asset_cargo_red_rgba.png")
    asset_previews["cargo_blue"] = render_isolated_asset(scene, camera, cargo_blue_collection,
        "ASSET_CARGO_BLUE_", camera_target, RENDERS_DIR / "asset_cargo_blue_rgba.png")

    # A cropped view of the actual two diverters demonstrates their synchronized arm motion.
    saved_visibility = {obj.name: obj.hide_render for obj in scene.objects}
    for obj in scene.objects:
        keep = obj.type in {"CAMERA", "LIGHT"} or obj.name == "BOARD_PLATFORM" or obj.name in proof_near_objects
        obj.hide_render = not keep
    point_camera(camera, Vector((4.8, -6.6, 8.2)), Vector((0, 0.1, 0.55)), 4.2)
    scene.render.resolution_x, scene.render.resolution_y = PROOF_RENDER
    scene.cycles.samples = 12
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(RENDERS_DIR / "diverter_state_before.png")
    scene.frame_set(1)
    bpy.ops.render.render(write_still=True)
    scene.render.filepath = str(RENDERS_DIR / "diverter_state_after.png")
    scene.frame_set(PROOF_FRAMES[-1])
    bpy.ops.render.render(write_still=True)
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.file_format = "PNG"
    scene.render.fps = 24
    frame_dir = RENDERS_DIR / "animation_frames"
    frame_dir.mkdir(parents=True, exist_ok=True)
    frames = list(range(1, PROOF_FRAMES[-1], 2)) + [PROOF_FRAMES[-1]]
    scene.render.use_persistent_data = True
    animation_started = time.perf_counter()
    for frame in frames:
        scene.frame_set(frame)
        scene.render.filepath = str(frame_dir / f"diverter_{frame:04d}.png")
        bpy.ops.render.render(write_still=True)
    animation_seconds = time.perf_counter() - animation_started
    for obj in scene.objects:
        obj.hide_render = saved_visibility[obj.name]

    # Restore the main Level 2 composition and selected post-Pulse-A state.
    scene.frame_set(PROOF_FRAMES[-1])
    point_camera(camera, board_camera_position, board_target, 10.2)
    setup_render(scene, *BOARD_RENDER, samples=28)
    scene.camera = camera
    scene.render.filepath = str(RENDERS_DIR / "level2_shared_control.png")

    export_collection(conveyor_collection, EXPORTS_DIR / "conveyor_module.glb")
    export_collection(diverter_collection, EXPORTS_DIR / "diverter_module.glb")
    export_collection(cargo_red_collection, EXPORTS_DIR / "cargo_red.glb")
    export_collection(cargo_blue_collection, EXPORTS_DIR / "cargo_blue.glb")
    export_collection_fbx(conveyor_collection, EXPORTS_DIR / "conveyor_module.fbx")
    export_collection_fbx(diverter_collection, EXPORTS_DIR / "diverter_module.fbx")
    export_collection_fbx(cargo_red_collection, EXPORTS_DIR / "cargo_red.fbx")
    export_collection_fbx(cargo_blue_collection, EXPORTS_DIR / "cargo_blue.fbx")

    blend_path = MODELS_DIR / "PulseFactory_Phase4C.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    manifest_assets = [
        {
            "id": "conveyor-module",
            "source": "src/build_scene.py",
            "export": "exports/conveyor_module.glb",
            "unityCandidateExport": "exports/conveyor_module.fbx",
            "dimensionsMeters": {"length": UNIT_LENGTH, "beltWidth": BELT_WIDTH},
            "modular": True,
        },
        {
            "id": "diverter-module",
            "source": "src/build_scene.py",
            "export": "exports/diverter_module.glb",
            "unityCandidateExport": "exports/diverter_module.fbx",
            "pivot": "ASSET_DIVERTER_ArmPivot",
            "animatedPart": "ASSET_DIVERTER_RoutingArm",
            "group": "A",
            "states": ["logical false route", "logical true route"],
        },
        {
            "id": "cargo-crate-red",
            "source": "src/build_scene.py",
            "export": "exports/cargo_red.glb",
            "unityCandidateExport": "exports/cargo_red.fbx",
            "dimensionsMeters": {"width": 0.48, "depth": 0.42, "height": 0.42},
            "identityPlate": "neutral; Unity supplies package symbol",
            "materialVariant": "cargo_red",
        },
        {
            "id": "cargo-crate-blue",
            "source": "src/build_scene.py",
            "export": "exports/cargo_blue.glb",
            "unityCandidateExport": "exports/cargo_blue.fbx",
            "dimensionsMeters": {"width": 0.48, "depth": 0.42, "height": 0.42},
            "identityPlate": "neutral; Unity supplies package symbol",
            "materialVariant": "cargo_blue",
        },
    ]
    manifest = {
        "schemaVersion": 1,
        "provenance": {
            "createdBy": "Pulse Factory procedural Python source using Blender bpy",
            "sourceFiles": ["src/build_scene.py", "materials/palette.json"],
            "thirdPartyGeometryOrTextures": [],
            "projectLicense": "No separate asset license is defined in the repository.",
        },
        "coordinateSystem": {"units": "meters", "boardPlane": "XY", "upAxis": "Z"},
        "assets": manifest_assets,
        "exports": {
            path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in sorted([*EXPORTS_DIR.glob("*.glb"), *EXPORTS_DIR.glob("*.fbx")])
        },
        "scene": str(blend_path.relative_to(ROOT)),
        "boardPreview": "rendered/level2_shared_control.png",
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "levelId": level["id"],
        "sourceLevelFile": str(LEVEL_PATH.relative_to(PROJECT_ROOT)),
        "firstCatalogCommand": first_command,
        "switchesAfterFirstCommand": switches,
        "selectedRoutesAfterFirstCommand": routes_on,
        "packagesAfterFirstCommand": package_records,
        "moduleLengthMeters": UNIT_LENGTH,
        "packageVisualOffsetFromNodeMeters": PACKAGE_APPROACH_OFFSET,
        "moduleCountsByEdge": module_counts,
        "bridgeEdge": BRIDGE_EDGE_ID,
        "bridgeLiftMeters": BRIDGE_LIFT,
        "boardRender": {
            "file": str((RENDERS_DIR / "level2_shared_control.png").relative_to(ROOT)),
            "dimensions": list(BOARD_RENDER),
            "renderSeconds": round(board_seconds, 3),
        },
        "isolatedAssetRenders": asset_previews,
        "animationRenderSeconds": round(animation_seconds, 3),
        "animationFrames": frames,
        "animationFps": 12,
        "animationFrameDirectory": "rendered/animation_frames/",
        "animationFile": "rendered/diverter_transition.mp4 (encoded by build.sh via macOS AVFoundation)",
        "exportFiles": [
            "exports/conveyor_module.glb",
            "exports/diverter_module.glb",
            "exports/cargo_red.glb",
            "exports/cargo_blue.glb",
        ],
        "unityCandidateExportFiles": [
            "exports/conveyor_module.fbx",
            "exports/diverter_module.fbx",
            "exports/cargo_red.fbx",
            "exports/cargo_blue.fbx",
        ],
        "assetManifest": str(MANIFEST_PATH.relative_to(ROOT)),
        "blendFile": str(blend_path.relative_to(ROOT)),
        "wallClockBuildSeconds": round(time.perf_counter() - started, 3),
        "peakResidentMemoryBytes": resource.getrusage(resource.RUSAGE_SELF).ru_maxrss,
        "validation": "geometry and export checks are completed by validate_build.py",
        "unityIntegration": False,
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("PHASE4C_BUILD_REPORT=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    build()
