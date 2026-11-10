#!/usr/bin/env python3
"""Build a readability-focused Phase 4C.1 board proof from the archived scene."""

from __future__ import annotations

import hashlib
import json
import math
import platform
import resource
import shutil
import subprocess
import time
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = ROOT.parents[1]
PHASE4C_ROOT = PROJECT_ROOT / "Artifacts/Phase4CProceduralArt"
SOURCE_BLEND = PHASE4C_ROOT / "generated/models/PulseFactory_Phase4C.blend"
LEVEL_PATH = PROJECT_ROOT / "Assets/Resources/Levels/shared-control.json"
CATALOG_PATH = PROJECT_ROOT / "Assets/Resources/Levels/catalog.json"
RENDERS = ROOT / "rendered"
FRAMES = RENDERS / "animation_frames"
EXPORTS = ROOT / "exports"
REPORT = ROOT / "validation/refinement-report.json"
MANIFEST = ROOT / "asset-manifest.json"

WIDTH, HEIGHT = 1080, 1920
ANIMATION_SIZE = (640, 1138)
ANIMATION_FRAMES = tuple(range(1, 36, 2))
ANIMATION_END = 36
FPS = 12
FIRST_COMMAND = "PulseA"
CRATE_SCALE = 0.72
BASE_DECK_Z = 0.34
MODULE_LENGTH = 0.75


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def level_position(node: dict) -> Vector:
    return Vector(((node["x"] - 0.5) * 3.3066666667, (node["y"] - 0.5) * 5.0, 0))


def routes_for(level: dict, switches: dict[str, bool]) -> dict[str, str]:
    edges = {edge["id"]: edge for edge in level["edges"]}
    routes = {}
    for node in level["nodes"]:
        if node["type"] != "Diverter":
            continue
        branch = switches[node["group"].lower()] ^ bool(node.get("inverted", False))
        edge = edges[node["trueEdge"] if branch else node["falseEdge"]]
        routes[node["id"]] = edge["to"]
    return routes


def route_angle(start: Vector, target: Vector) -> float:
    direction = target - start
    return math.atan2(direction.y, direction.x) - math.pi / 2


def interpolate_angle(start: float, end: float) -> float:
    return start + (end - start + math.pi) % (2 * math.pi) - math.pi


def point_camera(camera: bpy.types.Object, position: Vector, target: Vector, scale: float) -> None:
    camera.location = position
    camera.rotation_euler = (target - position).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = scale


def render_png(scene: bpy.types.Scene, camera: bpy.types.Object, name: str,
               position: Vector, target: Vector, scale: float,
               dimensions: tuple[int, int], samples: int) -> float:
    scene.camera = camera
    point_camera(camera, position, target, scale)
    scene.render.resolution_x, scene.render.resolution_y = dimensions
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.filepath = str(RENDERS / name)
    started = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    return round(time.perf_counter() - started, 3)


def make_material(name: str, color: tuple[float, float, float, float],
                  emission_strength: float = 0.0) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = color
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = 0.12
    shader.inputs["Roughness"].default_value = 0.36
    emission = shader.inputs.get("Emission Color")
    if emission is not None:
        emission.default_value = color
        shader.inputs["Emission Strength"].default_value = emission_strength
    return material


def add_box(name: str, location: Vector, dimensions: tuple[float, float, float],
            material: bpy.types.Material, collection: bpy.types.Collection) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new("Soft symbol edges", "BEVEL")
    bevel.width = min(dimensions) * 0.10
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.data.materials.append(material)
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def add_route_indicator(name: str, deck: bpy.types.Object, material: bpy.types.Material,
                        collection: bpy.types.Collection) -> bpy.types.Object:
    rotation = deck.rotation_quaternion.copy()
    local_offset = Vector((0, 0, 0.232))
    center = deck.location + rotation @ local_offset
    marker = add_box(name, center, (0.43, 0.052, 0.012), material, collection)
    marker.rotation_mode = "QUATERNION"
    marker.rotation_quaternion = rotation
    return marker


def pulse_indicator(name: str, location: Vector, material: bpy.types.Material,
                    collection: bpy.types.Collection) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.48,
        minor_radius=0.018,
        major_segments=48,
        minor_segments=8,
        location=location,
    )
    ring = bpy.context.object
    ring.name = name
    ring.data.materials.append(material)
    for old in list(ring.users_collection):
        old.objects.unlink(ring)
    collection.objects.link(ring)
    ring.scale = (0.04, 0.04, 1)
    for frame, value in (
        (1, 0.04), (8, 0.04), (12, 0.62), (16, 1.0),
        (20, 0.72), (24, 0.24), (28, 0.04), (ANIMATION_END, 0.04),
    ):
        ring.scale = (value, value, 1)
        ring.keyframe_insert(data_path="scale", frame=frame)
    return ring


def refine_conveyors() -> None:
    processed: set[int] = set()
    for obj in bpy.data.objects:
        name = obj.name
        if not (name.startswith("ASSET_CONVEYOR_") or name.startswith("BOARD_EDGE_")):
            continue
        is_rail = "_Rail_" in name and "RailCap" not in name
        is_cap = "RailCap" in name
        if not (is_rail or is_cap):
            continue
        mesh_id = obj.data.as_pointer()
        if mesh_id not in processed:
            factor = 0.72 if is_rail else 0.74
            for vertex in obj.data.vertices:
                vertex.co.z *= factor
            processed.add(mesh_id)
        obj.location.z -= 0.04 if is_rail else 0.058


def refine_arm_mesh() -> None:
    arm = bpy.data.objects.get("ASSET_DIVERTER_RoutingArm")
    if arm is None or arm.type != "MESH":
        raise RuntimeError("Archived scene is missing the reusable diverter routing arm.")
    for vertex in arm.data.vertices:
        vertex.co.y = 0.25 + (vertex.co.y + 0.07) * 0.90
    arm.data.update()


def place_packages_at_logical_nodes(level: dict, collection: bpy.types.Collection) -> dict:
    source_to_node = {edge["from"]: edge["to"] for edge in level["edges"]}
    records = {}
    for spawn in level["spawns"]:
        if spawn["beat"] != 1:
            continue
        node_id = source_to_node[spawn["source"]]
        node = next(item for item in level["nodes"] if item["id"] == node_id)
        target = level_position(node)
        base = Vector((target.x, target.y, BASE_DECK_Z))
        prefix = f"BOARD_PACKAGE_{spawn['id']}_"
        objects = [obj for obj in bpy.data.objects if obj.name.startswith(prefix)]
        body = next((obj for obj in objects if obj.name.endswith("_CrateBody")), None)
        if body is None:
            raise RuntimeError(f"Package mesh is missing for {spawn['id']}.")
        old_base = body.location - Vector((0, 0, 0.25))
        for obj in objects:
            if obj.name.endswith("_IdentitySwatch"):
                obj.hide_render = True
                obj.hide_viewport = True
                continue
            relative = obj.location - old_base
            obj.location = base + relative * CRATE_SCALE
            obj.scale = obj.scale * CRATE_SCALE
        plate_top = base.z + (0.465 + 0.0125) * CRATE_SCALE
        color = "cargo_red" if spawn["kind"] == "red" else "cargo_blue"
        symbol_material = bpy.data.materials.get(f"MAT_{color}")
        if symbol_material is None:
            raise RuntimeError(f"Missing package material MAT_{color}.")
        if spawn["kind"] == "red":
            symbol = add_box(
                f"BOARD_PACKAGE_{spawn['id']}_Symbol_Diamond",
                Vector((base.x, base.y, plate_top + 0.012)),
                (0.12, 0.12, 0.018),
                symbol_material,
                collection,
            )
            symbol.rotation_euler.z = math.pi / 4
        else:
            bpy.ops.mesh.primitive_cylinder_add(
                vertices=32, radius=0.062, depth=0.018,
                location=(base.x, base.y, plate_top + 0.012),
            )
            symbol = bpy.context.object
            symbol.name = f"BOARD_PACKAGE_{spawn['id']}_Symbol_Circle"
            symbol.data.materials.append(symbol_material)
            for old in list(symbol.users_collection):
                old.objects.unlink(symbol)
            collection.objects.link(symbol)
        records[spawn["id"]] = {
            "kind": spawn["kind"],
            "logicalNode": node_id,
            "visualCenter": [round(base.x, 5), round(base.y, 5)],
            "meshScale": CRATE_SCALE,
        }
    return records


def add_destination_symbols(level: dict, collection: bpy.types.Collection) -> None:
    for node in level["nodes"]:
        if node["type"] != "Destination":
            continue
        pad = bpy.data.objects.get(f"BOARD_NODE_{node['id']}_ReceivingPad")
        if pad is None:
            raise RuntimeError(f"Destination pad is missing for {node['id']}.")
        center = pad.location.copy()
        center.x += 0.18
        center.y += 0.12
        center.z += pad.dimensions.z * 0.5 + 0.018
        material_name = "MAT_cargo_red" if node["accepts"] == "red" else "MAT_cargo_blue"
        material = bpy.data.materials.get(material_name)
        if material is None:
            raise RuntimeError(f"Destination material is missing: {material_name}.")
        if node["accepts"] == "red":
            symbol = add_box(
                f"BOARD_DESTINATION_{node['id']}_Symbol_Diamond",
                center, (0.15, 0.15, 0.02), material, collection,
            )
            symbol.rotation_euler.z = math.pi / 4
        else:
            bpy.ops.mesh.primitive_cylinder_add(
                vertices=32, radius=0.078, depth=0.02, location=center,
            )
            symbol = bpy.context.object
            symbol.name = f"BOARD_DESTINATION_{node['id']}_Symbol_Circle"
            symbol.data.materials.append(material)
            for old in list(symbol.users_collection):
                old.objects.unlink(symbol)
            collection.objects.link(symbol)


def build() -> None:
    started = time.perf_counter()
    RENDERS.mkdir(parents=True, exist_ok=True)
    FRAMES.mkdir(parents=True, exist_ok=True)
    for old_frame in FRAMES.glob("diverter_*.png"):
        old_frame.unlink()
    EXPORTS.mkdir(parents=True, exist_ok=True)
    (ROOT / "generated/models").mkdir(parents=True, exist_ok=True)
    (ROOT / "validation").mkdir(parents=True, exist_ok=True)

    level = read_json(LEVEL_PATH)
    catalog = read_json(CATALOG_PATH)
    catalog_entry = next(item for item in catalog["levels"] if item["levelId"] == level["id"])
    first_command = catalog_entry["solution"][0]
    if first_command != FIRST_COMMAND:
        raise RuntimeError(f"Expected catalog-derived first command {FIRST_COMMAND}, got {first_command}.")
    initial_switches = dict(level["initialSwitches"])
    after_switches = dict(initial_switches)
    group = first_command.removeprefix("Pulse").lower()
    after_switches[group] = not after_switches[group]
    initial_routes = routes_for(level, initial_switches)
    after_routes = routes_for(level, after_switches)
    level_hash = hashlib.sha256(LEVEL_PATH.read_bytes()).hexdigest()

    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    scene = bpy.context.scene
    camera = bpy.data.objects.get("CAM_Level2")
    if camera is None:
        raise RuntimeError("Archived Phase 4C scene is missing CAM_Level2.")
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.frame_set(48)
    baseline_camera_position = camera.location.copy()
    baseline_camera_scale = camera.data.ortho_scale
    render_times = {}
    scene.world.color = (0.04, 0.06, 0.07)

    board_collection = bpy.data.collections.get("PF_Phase4C_Scene")
    if board_collection is None:
        raise RuntimeError("Archived board scene collection is missing.")
    refine_conveyors()
    refine_arm_mesh()
    package_records = place_packages_at_logical_nodes(level, board_collection)
    add_destination_symbols(level, board_collection)

    # The archived active markers were static. Replace them with state-driven
    # indicators while retaining the complete original six-edge geometry.
    for obj in list(bpy.data.objects):
        if "_ActiveInlay" in obj.name:
            bpy.data.objects.remove(obj, do_unlink=True)

    indicator_collection = bpy.data.collections.new("PF_Phase4C1_RouteIndicators")
    scene.collection.children.link(indicator_collection)
    active_material = make_material(
        "MAT_Phase4C1_RouteActive", (0.25, 0.95, 0.69, 1), 0.45,
    )
    inactive_material = make_material(
        "MAT_Phase4C1_RouteInactive", (0.19, 0.28, 0.30, 1), 0.0,
    )
    pulse_material = make_material(
        "MAT_Phase4C1_PulseHalo", (0.28, 0.88, 0.70, 1), 0.35,
    )
    node_by_id = {node["id"]: node for node in level["nodes"]}
    edge_status = {}
    indicator_objects = {}
    for edge in level["edges"]:
        edge_id = edge["id"]
        source_node = node_by_id[edge["from"]]
        status_initial = source_node["type"] != "Diverter" or initial_routes[edge["from"]] == edge["to"]
        status_after = source_node["type"] != "Diverter" or after_routes[edge["from"]] == edge["to"]
        edge_status[edge_id] = {"initial": status_initial, "afterPulse": status_after}
        deck_objects = sorted(
            (obj for obj in bpy.data.objects if obj.name.startswith(f"BOARD_EDGE_{edge_id}_")
             and obj.name.endswith("_Deck")),
            key=lambda obj: int(obj.name.split("_")[3]),
        )
        expected_count = int(round(
            (level_position(node_by_id[edge["to"]]) - level_position(node_by_id[edge["from"]])).length
            / MODULE_LENGTH
        ))
        if len(deck_objects) != expected_count:
            raise RuntimeError(f"Edge {edge_id} has {len(deck_objects)} modules, expected {expected_count}.")
        for deck in deck_objects:
            index = int(deck.name.split("_")[3])
            add_route_indicator(
                f"ROUTE_{edge_id}_{index}_Base",
                deck, inactive_material, indicator_collection,
            )
            active = add_route_indicator(
                f"ROUTE_{edge_id}_{index}_Active",
                deck, active_material, indicator_collection,
            )
            initial_active = status_initial
            after_active = status_after
            for frame, value in (
                (1, 1.0 if initial_active else 0.06),
                (8, 1.0 if initial_active else 0.06),
                (13, 1.0 if after_active else 0.06),
                (19, 1.0 if after_active else 0.06),
                (28, 1.0 if after_active else 0.06),
                (ANIMATION_END, 1.0 if after_active else 0.06),
            ):
                active.scale.z = value
                active.keyframe_insert(data_path="scale", index=2, frame=frame)
            indicator_objects.setdefault(edge_id, []).append(active.name)

    target_by_id = {node["id"]: level_position(node) for node in level["nodes"]}
    for node in level["nodes"]:
        if node["type"] != "Diverter":
            continue
        pivot = bpy.data.objects.get(f"BOARD_NODE_{node['id']}_ArmPivot")
        if pivot is None:
            raise RuntimeError(f"Missing animated switch pivot at {node['id']}.")
        start = target_by_id[node["id"]]
        initial_angle = route_angle(start, target_by_id[initial_routes[node["id"]]])
        after_angle = interpolate_angle(
            initial_angle, route_angle(start, target_by_id[after_routes[node["id"]]])
        )
        for frame, angle in (
            (1, initial_angle),
            (8, initial_angle),
            (14, after_angle),
            (20, after_angle),
            (28, after_angle),
            (ANIMATION_END, after_angle),
        ):
            pivot.rotation_euler.z = angle
            pivot.keyframe_insert(data_path="rotation_euler", index=2, frame=frame)
        pulse_indicator(
            f"PULSE_{node['id']}_SynchronizedHalo",
            Vector((start.x, start.y, 0.475)),
            pulse_material,
            indicator_collection,
        )

    scene.frame_start = 1
    scene.frame_end = ANIMATION_END
    scene.frame_set(ANIMATION_END)
    target = Vector((0, 0, 0.1))
    render_times["A_baseline_camera"] = render_png(
        scene, camera, "comparison_A_baseline.png",
        baseline_camera_position, target, baseline_camera_scale,
        (WIDTH, HEIGHT), 20,
    )
    render_times["B_near_top_down"] = render_png(
        scene, camera, "comparison_B_near_top_down.png",
        Vector((0.2, -0.7, 22.0)), target, 10.2,
        (WIDTH, HEIGHT), 20,
    )
    render_times["C_refined_three_quarter"] = render_png(
        scene, camera, "comparison_C_refined_three_quarter.png",
        Vector((2.3, -3.1, 18.5)), target, 11.6,
        (WIDTH, HEIGHT), 20,
    )
    shutil.copy2(RENDERS / "comparison_B_near_top_down.png",
                 RENDERS / "mobile_preview_1080x1920.png")

    animation_position = Vector((0.2, -0.7, 22.0))
    animation_target = Vector((0, 0, 0.1))
    animation_seconds = 0.0
    for frame in ANIMATION_FRAMES:
        scene.frame_set(frame)
        scene.render.filepath = str(FRAMES / f"diverter_{frame:04d}.png")
        scene.render.resolution_x, scene.render.resolution_y = ANIMATION_SIZE
        scene.render.image_settings.color_mode = "RGB"
        scene.cycles.samples = 8
        point_camera(camera, animation_position, animation_target, 10.2)
        render_start = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        animation_seconds += time.perf_counter() - render_start
    scene.frame_set(ANIMATION_END)
    point_camera(camera, animation_position, animation_target, 10.2)

    # Export refined reusable models to the new workspace only.
    for collection_name, filename in (
        ("PROTO_CONVEYOR_0p75m", "conveyor_module"),
        ("PROTO_DIVERTER", "diverter_module"),
        ("PROTO_CARGO_RED", "cargo_red"),
        ("PROTO_CARGO_BLUE", "cargo_blue"),
    ):
        collection = bpy.data.collections.get(collection_name)
        if collection is None:
            raise RuntimeError(f"Missing reusable asset collection: {collection_name}.")
        saved_visibility = {obj.name: obj.hide_viewport for obj in collection.objects}
        bpy.ops.object.select_all(action="DESELECT")
        for obj in collection.objects:
            obj.hide_viewport = False
            obj.select_set(True)
        bpy.context.view_layer.objects.active = next(obj for obj in collection.objects if obj.type == "MESH")
        bpy.ops.export_scene.gltf(
            filepath=str(EXPORTS / f"{filename}.glb"),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_animations=False,
        )
        bpy.ops.object.select_all(action="DESELECT")
        for obj in collection.objects:
            obj.hide_viewport = saved_visibility[obj.name]
            obj.select_set(False)
        for obj in collection.objects:
            obj.hide_viewport = False
            obj.select_set(True)
        bpy.context.view_layer.objects.active = next(obj for obj in collection.objects if obj.type == "MESH")
        bpy.ops.export_scene.fbx(
            filepath=str(EXPORTS / f"{filename}.fbx"),
            use_selection=True,
            bake_anim=False,
            apply_unit_scale=True,
            axis_forward="-Z",
            axis_up="Y",
        )
        bpy.ops.object.select_all(action="DESELECT")
        for obj in collection.objects:
            obj.hide_viewport = saved_visibility[obj.name]

    blend_path = ROOT / "generated/models/PulseFactory_Phase4C1.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    export_hashes = {
        path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted([*EXPORTS.glob("*.glb"), *EXPORTS.glob("*.fbx")])
    }
    manifest = {
        "schemaVersion": 1,
        "derivedFrom": "Artifacts/Phase4CProceduralArt/generated/models/PulseFactory_Phase4C.blend",
        "sourceBuilder": "Artifacts/Phase4CProceduralArt/src/build_scene.py",
        "refinementSource": "src/refine_board.py",
        "levelSourceSha256": level_hash,
        "provenance": "Original procedural geometry refined in Blender Python; no third-party models or textures.",
        "coordinateSystem": {"units": "meters", "boardPlane": "XY", "upAxis": "Z"},
        "exports": export_hashes,
    }
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    report = {
        "environment": {
            "blenderVersion": bpy.app.version_string,
            "macOS": platform.mac_ver()[0],
            "architecture": platform.machine(),
            "memoryBytes": int(subprocess.check_output(
                ["sysctl", "-n", "hw.memsize"], text=True
            ).strip()),
        },
        "levelId": level["id"],
        "levelSource": str(LEVEL_PATH.relative_to(PROJECT_ROOT)),
        "levelSourceSha256": level_hash,
        "catalogCommand": first_command,
        "switchesBeforePulse": initial_switches,
        "switchesAfterPulse": after_switches,
        "routesBeforePulse": initial_routes,
        "routesAfterPulse": after_routes,
        "edgeStatus": edge_status,
        "edgeModuleCounts": {
            edge_id: len(objects) for edge_id, objects in indicator_objects.items()
        },
        "packageVisuals": package_records,
        "boardDimensions": [WIDTH, HEIGHT],
        "comparisonRenders": {
            "A_baseline_camera": "rendered/comparison_A_baseline.png",
            "B_near_top_down": "rendered/comparison_B_near_top_down.png",
            "C_refined_three_quarter": "rendered/comparison_C_refined_three_quarter.png",
        },
        "comparisonBasis": "Same refined scene, materials, switch/package state, and graph; only camera differs.",
        "recommendedCamera": "B_near_top_down",
        "mobilePreview": "rendered/mobile_preview_1080x1920.png",
        "animation": {
            "frames": list(ANIMATION_FRAMES),
            "fps": FPS,
            "durationSeconds": len(ANIMATION_FRAMES) / FPS,
            "frameDimensions": list(ANIMATION_SIZE),
            "renderSeconds": round(animation_seconds, 3),
            "file": "rendered/synchronized_pulse_transition.mp4",
        },
        "renderSeconds": render_times,
        "buildWallSeconds": round(time.perf_counter() - started, 3),
        "peakResidentMemoryBytes": resource.getrusage(resource.RUSAGE_SELF).ru_maxrss,
        "sourceBlendPreserved": True,
        "unityIntegration": False,
    }
    REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("PHASE4C1_REPORT=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    build()
