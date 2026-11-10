#!/usr/bin/env python3
"""Independent structural and file validation for the Phase 4C build."""

import json
import hashlib
import math
import struct
import time
import zlib
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = ROOT.parents[1]
LEVEL_FILE = PROJECT_ROOT / "Assets/Resources/Levels/shared-control.json"
CATALOG_FILE = PROJECT_ROOT / "Assets/Resources/Levels/catalog.json"
REPORT_FILE = ROOT / "validation/validation-report.json"


def fail(message):
    raise RuntimeError("Phase 4C validation failed: " + message)


def png_info(path):
    with path.open("rb") as file:
        header = file.read(26)
    if len(header) != 26 or header[:8] != b"\x89PNG\r\n\x1a\n":
        fail(f"Invalid PNG: {path}")
    width, height = struct.unpack(">II", header[16:24])
    color_type = header[25]
    return {"width": width, "height": height, "colorType": color_type}


def glb_json(path):
    with path.open("rb") as file:
        header = file.read(12)
        if len(header) != 12:
            fail(f"Truncated GLB header: {path}")
        magic, version, total_length = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2 or total_length != path.stat().st_size:
            fail(f"Invalid glTF 2.0 container: {path}")
        chunk_length, chunk_type = struct.unpack("<II", file.read(8))
        if chunk_type != 0x4E4F534A:
            fail(f"GLB JSON chunk missing: {path}")
        return json.loads(file.read(chunk_length).decode("utf-8"))


def main():
    started = time.perf_counter()
    level = json.loads(LEVEL_FILE.read_text(encoding="utf-8"))
    catalog = json.loads(CATALOG_FILE.read_text(encoding="utf-8"))
    catalog_entry = next(item for item in catalog["levels"] if item["levelId"] == level["id"])
    build = json.loads((ROOT / "validation/build-report.json").read_text(encoding="utf-8"))
    checks = []

    if len(level["nodes"]) != 6 or len(level["edges"]) != 6:
        fail("Level 2 no longer has the expected six nodes and six edges.")
    if build["sourceLevelFile"] != "Assets/Resources/Levels/shared-control.json":
        fail("Build report does not name the authoritative Level 2 file.")
    if build["firstCatalogCommand"] != catalog_entry["solution"][0]:
        fail("Board state was not derived from the actual level catalog.")
    if build["moduleLengthMeters"] != 0.75:
        fail("Conveyor repeat length changed unexpectedly.")
    checks.append("Level 2 source and catalog state")

    for edge in level["edges"]:
        edge_id = edge["id"]
        start = next(node for node in level["nodes"] if node["id"] == edge["from"])
        end = next(node for node in level["nodes"] if node["id"] == edge["to"])
        dx = (end["x"] - start["x"]) * 3.3066666667
        dy = (end["y"] - start["y"]) * 5.0
        expected_modules = round(math.hypot(dx, dy) / 0.75)
        if build["moduleCountsByEdge"].get(edge_id) != expected_modules:
            fail(f"Repeat count mismatch on edge {edge_id}.")
        object_names = {
            obj.name for obj in bpy.context.scene.objects
            if obj.name.startswith(f"BOARD_EDGE_{edge_id}_")
        }
        rendered_indices = set()
        for name in object_names:
            parts = name.split("_")
            if len(parts) < 5:
                fail(f"Malformed board conveyor object name: {name}")
            rendered_indices.add(parts[3])
        if len(rendered_indices) != expected_modules:
            fail(f"Expected {expected_modules} fixed modules on edge {edge_id}, found {len(rendered_indices)}.")
    checks.append("Fixed-size conveyor modules cover each authoritative edge")

    prototype_deck = bpy.data.objects.get("ASSET_CONVEYOR_Deck")
    if prototype_deck is None or abs(prototype_deck.dimensions.x - 0.75) > 0.001:
        fail("Conveyor prototype does not have the fixed 0.75 m module length.")
    checks.append("Conveyor prototype dimensions")

    for node_id in ("J1", "J2"):
        pivot = bpy.data.objects.get(f"BOARD_NODE_{node_id}_ArmPivot")
        arm = bpy.data.objects.get(f"BOARD_NODE_{node_id}_RoutingArm")
        if pivot is None or arm is None or arm.parent != pivot:
            fail(f"Diverter {node_id} has no separate arm parented to its pivot.")
        if abs(pivot.location.z - 0.45) > 0.001:
            fail(f"Diverter {node_id} pivot height changed.")
        if pivot.animation_data is None or pivot.animation_data.action is None:
            fail(f"Diverter {node_id} has no transformation-driven animation.")
    checks.append("Separate diverter arms and animated pivots")

    if "ASSET_DIVERTER_GroupA" not in bpy.data.objects:
        fail("Group A identification is missing from the mechanical prototype.")
    if "ASSET_CARGO_RED_TopPlate" not in bpy.data.objects:
        fail("Cargo neutral identification plate is missing.")
    if any("Wheel" in obj.name for obj in bpy.data.objects if obj.name.startswith("ASSET_CARGO_")):
        fail("Cargo prototype unexpectedly contains wheels.")
    malformed_meshes = [
        obj.name
        for obj in bpy.data.objects
        if obj.type == "MESH" and (len(obj.data.vertices) == 0 or len(obj.data.polygons) == 0)
    ]
    if malformed_meshes:
        fail(f"Found empty mesh geometry: {malformed_meshes}")
    checks.append("Group badge, neutral cargo plate, and wheel-free cargo")
    checks.append("Generated mesh geometry contains vertices and faces")

    for package_id, kind, node_id in (
        ("red-1", "red", "J1"),
        ("blue-1", "blue", "J2"),
    ):
        swatch = bpy.data.objects.get(f"BOARD_PACKAGE_{package_id}_IdentitySwatch")
        body = bpy.data.objects.get(f"BOARD_PACKAGE_{package_id}_CrateBody")
        if swatch is None or body is None:
            fail(f"Missing visible package body or identity swatch for {package_id}.")
        if not body.data.materials or body.data.materials[0].name != f"MAT_cargo_{kind}":
            fail(f"Incorrect cargo material assignment for {package_id}.")
        node = next(item for item in level["nodes"] if item["id"] == node_id)
        node_position = (
            (node["x"] - 0.5) * 3.3066666667,
            (node["y"] - 0.5) * 5.0,
        )
        visual_distance = math.dist(swatch.location[:2], node_position)
        if abs(visual_distance - build["packageVisualOffsetFromNodeMeters"]) > 0.002:
            fail(f"Unexpected visual staging offset for {package_id}.")
    checks.append("Visible red/blue package identity at input-side approach positions")

    expected_glbs = {
        "conveyor_module.glb": ("ASSET_CONVEYOR_Deck",),
        "diverter_module.glb": ("ASSET_DIVERTER_RoutingArm", "ASSET_DIVERTER_ArmPivot"),
        "cargo_red.glb": ("ASSET_CARGO_RED_CrateBody", "ASSET_CARGO_RED_TopPlate"),
        "cargo_blue.glb": ("ASSET_CARGO_BLUE_CrateBody", "ASSET_CARGO_BLUE_TopPlate"),
    }
    for filename, required_nodes in expected_glbs.items():
        path = ROOT / "exports" / filename
        if not path.is_file() or path.stat().st_size < 1000:
            fail(f"Missing or implausibly small GLB: {filename}")
        names = {node.get("name") for node in glb_json(path).get("nodes", [])}
        missing = [name for name in required_nodes if name not in names]
        if missing:
            fail(f"{filename} is missing nodes: {missing}")
        before_import = set(bpy.data.objects)
        try:
            bpy.ops.import_scene.gltf(filepath=str(path))
        except Exception as error:
            fail(f"Blender could not round-trip import {filename}: {error}")
        imported = [obj for obj in bpy.data.objects if obj not in before_import]
        imported_meshes = [obj for obj in imported if obj.type == "MESH"]
        if not imported_meshes:
            fail(f"Blender imported no mesh objects from {filename}.")
        if filename == "diverter_module.glb":
            arm = next((obj for obj in imported if obj.name.startswith("ASSET_DIVERTER_RoutingArm")), None)
            if arm is None or arm.parent is None:
                fail("Diverter GLB round-trip lost the animated arm hierarchy.")
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
    checks.append("glTF 2.0 export containers, required nodes, and Blender round-trip imports")

    expected_fbxs = {
        "conveyor_module.fbx": ("ASSET_CONVEYOR_Deck",),
        "diverter_module.fbx": ("ASSET_DIVERTER_RoutingArm", "ASSET_DIVERTER_ArmPivot"),
        "cargo_red.fbx": ("ASSET_CARGO_RED_CrateBody", "ASSET_CARGO_RED_TopPlate"),
        "cargo_blue.fbx": ("ASSET_CARGO_BLUE_CrateBody", "ASSET_CARGO_BLUE_TopPlate"),
    }
    for filename, required_names in expected_fbxs.items():
        path = ROOT / "exports" / filename
        if not path.is_file() or path.stat().st_size < 1000:
            fail(f"Missing or implausibly small FBX: {filename}")
        before_import = set(bpy.data.objects)
        try:
            bpy.ops.import_scene.fbx(filepath=str(path))
        except Exception as error:
            fail(f"Blender could not round-trip import {filename}: {error}")
        imported = [obj for obj in bpy.data.objects if obj not in before_import]
        imported_meshes = [obj for obj in imported if obj.type == "MESH"]
        imported_names = {obj.name.rsplit(".", 1)[0] for obj in imported}
        missing = [name for name in required_names if name not in imported_names]
        if not imported_meshes or missing:
            fail(f"FBX round-trip is incomplete for {filename}; missing: {missing}")
        if filename == "conveyor_module.fbx":
            deck = next(obj for obj in imported if obj.name.startswith("ASSET_CONVEYOR_Deck"))
            if abs(deck.dimensions.x - 0.75) > 0.002:
                fail("FBX round-trip changed the conveyor module length.")
        if filename == "diverter_module.fbx":
            arm = next((obj for obj in imported if obj.name.startswith("ASSET_DIVERTER_RoutingArm")), None)
            if arm is None or arm.parent is None:
                fail("FBX round-trip lost the diverter arm pivot hierarchy.")
        if not any(obj.data.materials for obj in imported_meshes):
            fail(f"FBX round-trip lost all materials from {filename}.")
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
    checks.append("FBX exports preserve geometry, materials, dimensions, and diverter pivot hierarchy")

    manifest_path = ROOT / "asset-manifest.json"
    if not manifest_path.is_file():
        fail("Asset manifest is missing.")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if len(manifest.get("assets", [])) != 4 or manifest["provenance"]["thirdPartyGeometryOrTextures"]:
        fail("Asset manifest does not describe the four original procedural components.")
    for relative_path, expected_hash in manifest["exports"].items():
        if hashlib.sha256((ROOT / relative_path).read_bytes()).hexdigest() != expected_hash:
            fail(f"Asset manifest checksum mismatch: {relative_path}.")
    checks.append("Asset manifest provenance and export checksums")

    board_path = ROOT / "rendered/level2_shared_control.png"
    board = png_info(board_path)
    if (board["width"], board["height"]) != (1200, 1800) or board["colorType"] not in (2, 6):
        fail("Level 2 board render has unexpected dimensions or PNG color type.")
    checks.append("Portrait board render dimensions")

    alpha_reports = {}
    for name in ("asset_conveyor_rgba.png", "asset_diverter_rgba.png",
                 "asset_cargo_red_rgba.png", "asset_cargo_blue_rgba.png"):
        path = ROOT / "rendered" / name
        info = png_info(path)
        if (info["width"], info["height"]) != (768, 768) or info["colorType"] != 6:
            fail(f"Isolated preview is not a 768x768 RGBA PNG: {name}")
        image = bpy.data.images.load(str(path), check_existing=False)
        if image.channels != 4:
            fail(f"Blender did not read an alpha channel in {name}")
        values = image.pixels[:]
        alpha = values[3::4]
        alpha_min, alpha_max = min(alpha), max(alpha)
        bpy.data.images.remove(image)
        if alpha_min >= 0.99 or alpha_max < 0.99:
            fail(f"Isolated preview does not contain both transparent and opaque pixels: {name}")
        alpha_reports[name] = [round(alpha_min, 4), round(alpha_max, 4)]
    checks.append("Genuine RGBA transparency with transparent and opaque pixels")

    animation_files = list((ROOT / "rendered").glob("diverter_transition*.mp4"))
    if len(animation_files) != 1 or animation_files[0].stat().st_size < 10000:
        fail("Expected one rendered diverter transition MP4.")
    before = png_info(ROOT / "rendered/diverter_state_before.png")
    after = png_info(ROOT / "rendered/diverter_state_after.png")
    if (before["width"], before["height"]) != (640, 640) or (after["width"], after["height"]) != (640, 640):
        fail("Diverter state stills have unexpected dimensions.")
    before_image = bpy.data.images.load(str(ROOT / "rendered/diverter_state_before.png"), check_existing=False)
    after_image = bpy.data.images.load(str(ROOT / "rendered/diverter_state_after.png"), check_existing=False)
    before_pixels, after_pixels = before_image.pixels[:], after_image.pixels[:]
    changed_pixels = sum(
        max(abs(before_pixels[index + channel] - after_pixels[index + channel]) for channel in range(3)) > 0.03
        for index in range(0, len(before_pixels), 4)
    )
    bpy.data.images.remove(before_image)
    bpy.data.images.remove(after_image)
    if changed_pixels < 1000:
        fail("Diverter states are not visibly distinct in the rendered stills.")
    checks.append("Diverter state stills and rendered animation proof")

    report = {
        "status": "passed",
        "checks": checks,
        "checkCount": len(checks),
        "alphaMinMax": alpha_reports,
        "stateDifferencePixels": changed_pixels,
        "animationFile": str(animation_files[0].relative_to(ROOT)),
        "validationSeconds": round(time.perf_counter() - started, 3),
        "blenderVersion": bpy.app.version_string,
    }
    REPORT_FILE.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("PHASE4C_VALIDATION=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
