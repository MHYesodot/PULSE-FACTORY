#!/usr/bin/env python3
"""Validate Phase 4C.1 topology evidence, rendering, animation, and exports."""

import hashlib
import json
import math
import struct
import time
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = ROOT.parents[1]
LEVEL_FILE = PROJECT_ROOT / "Assets/Resources/Levels/shared-control.json"
CATALOG_FILE = PROJECT_ROOT / "Assets/Resources/Levels/catalog.json"
REPORT_FILE = ROOT / "validation/refinement-report.json"
VALIDATION_FILE = ROOT / "validation/validation-report.json"


def fail(message):
    raise RuntimeError("Phase 4C.1 validation failed: " + message)


def png_info(path):
    with path.open("rb") as file:
        header = file.read(26)
    if len(header) != 26 or header[:8] != b"\x89PNG\r\n\x1a\n":
        fail(f"Invalid PNG: {path}")
    width, height = struct.unpack(">II", header[16:24])
    return width, height, header[25]


def routes_for(level, switches):
    edges = {edge["id"]: edge for edge in level["edges"]}
    routes = {}
    for node in level["nodes"]:
        if node["type"] != "Diverter":
            continue
        branch = switches[node["group"].lower()] ^ bool(node.get("inverted", False))
        route = edges[node["trueEdge"] if branch else node["falseEdge"]]
        routes[node["id"]] = route["to"]
    return routes


def image_diff(path_a, path_b, threshold=0.025):
    image_a = bpy.data.images.load(str(path_a), check_existing=False)
    image_b = bpy.data.images.load(str(path_b), check_existing=False)
    values_a, values_b = image_a.pixels[:], image_b.pixels[:]
    if len(values_a) != len(values_b):
        fail("Comparison renders have different pixel counts.")
    changed = sum(
        max(abs(values_a[index + channel] - values_b[index + channel]) for channel in range(3)) > threshold
        for index in range(0, len(values_a), image_a.channels)
    )
    bpy.data.images.remove(image_a)
    bpy.data.images.remove(image_b)
    return changed


def main():
    started = time.perf_counter()
    report = json.loads(REPORT_FILE.read_text(encoding="utf-8"))
    level = json.loads(LEVEL_FILE.read_text(encoding="utf-8"))
    catalog = json.loads(CATALOG_FILE.read_text(encoding="utf-8"))
    catalog_entry = next(item for item in catalog["levels"] if item["levelId"] == level["id"])
    checks = []

    if report["catalogCommand"] != catalog_entry["solution"][0]:
        fail("Pulse state was not derived from the authoritative catalog.")
    if report["levelSourceSha256"] != hashlib.sha256(LEVEL_FILE.read_bytes()).hexdigest():
        fail("Level JSON changed after the render was generated.")
    initial = dict(level["initialSwitches"])
    after = dict(initial)
    group = report["catalogCommand"].removeprefix("Pulse").lower()
    after[group] = not after[group]
    if report["routesBeforePulse"] != routes_for(level, initial):
        fail("Before-pulse route selection does not match the Level 2 graph.")
    if report["routesAfterPulse"] != routes_for(level, after):
        fail("After-pulse route selection does not match the Level 2 graph.")
    checks.append("Unmodified Level 2 source and graph-derived switch routes")

    expected_counts = {"S1J1": 3, "S2J2": 3, "J1R": 3, "J1B": 4, "J2R": 4, "J2B": 3}
    for edge_id, expected in expected_counts.items():
        decks = [
            obj for obj in bpy.context.scene.objects
            if obj.name.startswith(f"BOARD_EDGE_{edge_id}_") and obj.name.endswith("_Deck")
        ]
        if len(decks) != expected or report["edgeModuleCounts"].get(edge_id) != expected:
            fail(f"Physical route {edge_id} is incomplete.")
        for deck in decks:
            index = int(deck.name.split("_")[3])
            if bpy.data.objects.get(f"ROUTE_{edge_id}_{index}_Base") is None:
                fail(f"Missing inactive path indicator for {edge_id} segment {index}.")
            if bpy.data.objects.get(f"ROUTE_{edge_id}_{index}_Active") is None:
                fail(f"Missing active path indicator for {edge_id} segment {index}.")
    bridge_deck = bpy.data.objects.get("BOARD_EDGE_J1B_1_Deck")
    lower_crossing_deck = bpy.data.objects.get("BOARD_EDGE_J2R_1_Deck")
    if bridge_deck is None or lower_crossing_deck is None:
        fail("The grade-separated crossing has no measurable route decks.")
    if bridge_deck.location.z - lower_crossing_deck.location.z < 0.45:
        fail("The J1B bridge is not visibly raised above the crossing route.")
    checks.append("All six authoritative connections have complete modules and active/inactive indicators")

    node_positions = {
        node["id"]: ((node["x"] - 0.5) * 3.3066666667, (node["y"] - 0.5) * 5.0)
        for node in level["nodes"]
    }
    for package_id, record in report["packageVisuals"].items():
        body = bpy.data.objects.get(f"BOARD_PACKAGE_{package_id}_CrateBody")
        if body is None:
            fail(f"Missing package visual {package_id}.")
        expected = node_positions[record["logicalNode"]]
        if math.dist(body.location[:2], expected) > 0.002:
            fail(f"{package_id} is not visually centered at its logical node.")
        if not any(obj.name.startswith(f"BOARD_PACKAGE_{package_id}_Symbol_")
                   for obj in bpy.context.scene.objects):
            fail(f"{package_id} has no independent shape symbol.")
    checks.append("Package meshes coincide with their logical nodes and use independent symbols")

    pivot_angles = {}
    for node_id in ("J1", "J2"):
        pivot = bpy.data.objects.get(f"BOARD_NODE_{node_id}_ArmPivot")
        if pivot is None or pivot.animation_data is None:
            fail(f"Missing switch animation for {node_id}.")
        before_angle = pivot.rotation_euler.z
        bpy.context.scene.frame_set(8)
        before_angle = pivot.rotation_euler.z
        bpy.context.scene.frame_set(20)
        after_angle = pivot.rotation_euler.z
        if abs((after_angle - before_angle + math.pi) % (2 * math.pi) - math.pi) < 0.25:
            fail(f"{node_id} routing arm does not visibly change state.")
        pivot_angles[node_id] = (before_angle, after_angle)
        if bpy.data.objects.get(f"PULSE_{node_id}_SynchronizedHalo") is None:
            fail(f"Missing synchronized pulse activation effect for {node_id}.")
    for edge_id in ("J1R", "J1B", "J2R", "J2B"):
        marker = bpy.data.objects.get(f"ROUTE_{edge_id}_0_Active")
        status = report["edgeStatus"][edge_id]
        bpy.context.scene.frame_set(8)
        before_scale = marker.scale.z
        bpy.context.scene.frame_set(20)
        after_scale = marker.scale.z
        if abs(before_scale - (1.0 if status["initial"] else 0.06)) > 0.02:
            fail(f"{edge_id} route indicator does not match the pre-pulse route state.")
        if abs(after_scale - (1.0 if status["afterPulse"] else 0.06)) > 0.02:
            fail(f"{edge_id} route indicator does not match the post-pulse route state.")
    angle_changes = [
        abs((after - before + math.pi) % (2 * math.pi) - math.pi)
        for before, after in pivot_angles.values()
    ]
    if max(angle_changes) - min(angle_changes) > 0.05:
        fail("The two switch activations do not transition in sync.")
    bpy.context.scene.frame_set(20)
    checks.append("Both diverters, route indicators, and pulse effects transition synchronously")

    comparison_paths = [
        ROOT / "rendered/comparison_A_baseline.png",
        ROOT / "rendered/comparison_B_near_top_down.png",
        ROOT / "rendered/comparison_C_refined_three_quarter.png",
    ]
    for path in comparison_paths:
        if png_info(path)[:2] != (1080, 1920):
            fail(f"Comparison render is not 1080x1920: {path.name}.")
    mobile = ROOT / "rendered/mobile_preview_1080x1920.png"
    small_mobile = ROOT / "rendered/mobile_preview_360x640.png"
    if png_info(mobile)[:2] != (1080, 1920) or png_info(small_mobile)[:2] != (360, 640):
        fail("Mobile preview dimensions are incorrect.")
    if image_diff(mobile, ROOT / "rendered/comparison_B_near_top_down.png", threshold=0.0) != 0:
        fail("Chosen mobile preview does not match the recommended camera render.")
    if image_diff(comparison_paths[0], comparison_paths[1]) < 1000:
        fail("Baseline and near-top-down comparison renders are unexpectedly identical.")
    if image_diff(comparison_paths[1], comparison_paths[2]) < 1000:
        fail("Near-top-down and elevated comparison renders are unexpectedly identical.")
    checks.append("Three 9:16 comparisons and 1080x1920 / 360x640 mobile previews")

    animation = ROOT / "rendered/synchronized_pulse_transition.mp4"
    if not animation.is_file() or animation.stat().st_size < 10000:
        fail("Synchronized animation proof is missing or implausibly small.")
    expected_frames = list(range(1, 36, 2))
    if len(list((ROOT / "rendered/animation_frames").glob("diverter_*.png"))) != len(expected_frames):
        fail("Animation proof does not contain all rendered evidence frames.")
    if report["animation"]["frames"] != expected_frames:
        fail("Animation timeline frame manifest changed unexpectedly.")
    frame_start = ROOT / "rendered/animation_frames/diverter_0001.png"
    frame_pulse = ROOT / "rendered/animation_frames/diverter_0015.png"
    frame_end = ROOT / "rendered/animation_frames/diverter_0035.png"
    for path in (frame_start, frame_pulse, frame_end):
        if png_info(path)[:2] != (640, 1138):
            fail(f"Unexpected animation frame dimensions: {path.name}.")
    pulse_diff = image_diff(frame_start, frame_pulse)
    settle_diff = image_diff(frame_pulse, frame_end)
    if pulse_diff < 1000 or settle_diff < 1000:
        fail("The pulse proof does not visibly show both activation and settled state.")
    checks.append("Synchronized pulse animation frames and encoded H.264 proof")

    for filename in (
        "conveyor_module.glb", "diverter_module.glb", "cargo_red.glb", "cargo_blue.glb",
        "conveyor_module.fbx", "diverter_module.fbx", "cargo_red.fbx", "cargo_blue.fbx",
    ):
        path = ROOT / "exports" / filename
        if not path.is_file() or path.stat().st_size < 1000:
            fail(f"Missing export: {filename}.")
        before_import = set(bpy.data.objects)
        if filename.endswith(".glb"):
            bpy.ops.import_scene.gltf(filepath=str(path))
        else:
            bpy.ops.import_scene.fbx(filepath=str(path))
        imported = [obj for obj in bpy.data.objects if obj not in before_import]
        if not any(obj.type == "MESH" for obj in imported):
            fail(f"Export did not round-trip as mesh geometry: {filename}.")
        if filename.startswith("diverter_module"):
            arm = next((obj for obj in imported if "RoutingArm" in obj.name), None)
            if arm is None or arm.parent is None:
                fail(f"Diverter pivot hierarchy was not preserved: {filename}.")
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
    checks.append("Refined GLB and FBX exports round-trip with the diverter pivot hierarchy")

    manifest = json.loads((ROOT / "asset-manifest.json").read_text(encoding="utf-8"))
    for relative, expected_hash in manifest["exports"].items():
        actual = hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()
        if actual != expected_hash:
            fail(f"Asset checksum mismatch: {relative}.")
    checks.append("Refined asset manifest checksums")

    validation = {
        "status": "passed",
        "checks": checks,
        "checkCount": len(checks),
        "levelGraphSha256": report["levelSourceSha256"],
        "comparisonRenderDimensions": [1080, 1920],
        "mobilePreviewDimensions": [360, 640],
        "validatedExportCount": 8,
        "animationFrameCount": len(expected_frames),
        "validationSeconds": round(time.perf_counter() - started, 3),
        "blenderVersion": bpy.app.version_string,
    }
    VALIDATION_FILE.write_text(json.dumps(validation, indent=2) + "\n", encoding="utf-8")
    print("PHASE4C1_VALIDATION=" + json.dumps(validation, separators=(",", ":")))


if __name__ == "__main__":
    main()
