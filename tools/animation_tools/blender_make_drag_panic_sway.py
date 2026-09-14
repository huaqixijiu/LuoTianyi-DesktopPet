from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


FRAME_COUNT = 40
FPS = 24
RENDER_WIDTH = 960
RENDER_HEIGHT = 1040


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Author and render a full-body drag panic sway action.")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--preview-frames", action="store_true")
    raw_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(raw_args)


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area_light(name: str, location: tuple[float, float, float], energy: float) -> None:
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = 4.0
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    point_at(light, Vector((0.0, 0.0, 0.48)))


def setup_render() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.resolution_x = RENDER_WIDTH
    scene.render.resolution_y = RENDER_HEIGHT
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.fps = FPS
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.world.color = (0.8, 0.8, 0.8)

    for obj in list(scene.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)

    camera_data = bpy.data.cameras.new("PetCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.18
    camera = bpy.data.objects.new("PetCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.0, -3.0, 0.50)
    point_at(camera, Vector((0.0, 0.02, 0.50)))
    scene.camera = camera

    add_area_light("Key", (-2.5, -4.0, 4.5), 180.0)
    add_area_light("Fill", (2.5, -2.5, 2.0), 80.0)


def armature() -> bpy.types.Object:
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def mesh() -> bpy.types.Object:
    return next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")


def reset_pose(rig: bpy.types.Object) -> None:
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)
        for constraint in bone.constraints:
            if constraint.type == "IK":
                constraint.influence = 0.0


def set_rotation(rig: bpy.types.Object, name: str, xyz_degrees: tuple[float, float, float]) -> None:
    bone = rig.pose.bones.get(name)
    if bone is None:
        raise KeyError(f"Required bone not found: {name}")
    bone.rotation_mode = "XYZ"
    bone.rotation_euler = tuple(math.radians(value) for value in xyz_degrees)
    bone.keyframe_insert(data_path="rotation_euler", group=name)


def set_location(rig: bpy.types.Object, name: str, xyz: tuple[float, float, float]) -> None:
    bone = rig.pose.bones.get(name)
    if bone is None:
        raise KeyError(f"Required bone not found: {name}")
    bone.location = xyz
    bone.keyframe_insert(data_path="location", group=name)


def set_morph(model: bpy.types.Object, name: str, value: float) -> None:
    if model.data.shape_keys is None:
        raise RuntimeError("Model has no shape keys")
    key = model.data.shape_keys.key_blocks.get(name)
    if key is None:
        raise KeyError(f"Required morph not found: {name}")
    key.value = value
    key.keyframe_insert(data_path="value", group="Face")


def author_action() -> None:
    rig = armature()
    model = mesh()
    reset_pose(rig)
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = FRAME_COUNT

    action = bpy.data.actions.new("Drag_Panic_Sway")
    rig.animation_data_create()
    rig.animation_data.action = action

    for frame in range(1, FRAME_COUNT + 1):
        phase = 2.0 * math.pi * (frame - 1) / FRAME_COUNT
        front_back = math.sin(2.0 * phase)  # true front-back swing basis
        breathe = 0.5 - 0.5 * math.cos(2.0 * phase)
        body_wobble = math.sin(phase)
        left_swing = front_back
        right_swing = -front_back
        scene.frame_set(frame)

        # keep feet visible and stable in canvas, while feeling pulled back and forth
        set_location(rig, "センター", (0.008 * front_back, 0.0, 0.028 + 0.013 * breathe))
        set_rotation(rig, "センター", (0.004 * front_back, 0.0, 0.0))
        set_rotation(rig, "下半身", (-2.2 * body_wobble, 0.0, 0.0))
        set_rotation(rig, "上半身", (1.4 * left_swing, 0.0, 0.0))
        set_rotation(rig, "上半身2", (1.2 * right_swing, 0.0, 0.0))
        set_rotation(rig, "頭", (2.1 * body_wobble, 0.0, 0.0))

        # pure front-back foot swing (front and back, no circular twist)
        set_rotation(rig, "左足", (35.0 * left_swing, 0.0, 0.0))
        set_rotation(rig, "左ひざ", (-36.0 - 16.0 * left_swing, 0.0, 0.0))
        set_rotation(rig, "左足首", (12.0 + 12.0 * abs(left_swing), 0.0, 0.0))
        set_rotation(rig, "右足", (35.0 * right_swing, 0.0, 0.0))
        set_rotation(rig, "右ひざ", (-36.0 - 16.0 * right_swing, 0.0, 0.0))
        set_rotation(rig, "右足首", (12.0 + 12.0 * abs(right_swing), 0.0, 0.0))

        # hands and hair jitter to sell panic
        hand_kick = math.sin(phase * 2.5)
        set_rotation(rig, "左腕", (6.0 * front_back, 0.0, 0.0))
        set_rotation(rig, "左ひじ", (18.0 * abs(hand_kick), 0.0, 0.0))
        set_rotation(rig, "右腕", (6.0 * front_back, 0.0, 0.0))
        set_rotation(rig, "右ひじ", (18.0 * abs(hand_kick), 0.0, 0.0))

        set_rotation(rig, "左发_01_01", (0.0, 0.0, 0.0))
        set_rotation(rig, "右发_01_01", (0.0, 0.0, 0.0))
        set_rotation(rig, "左八字辫_01_01", (0.0, 0.0, 2.0 * front_back))
        set_rotation(rig, "右八字辫_01_01", (0.0, 0.0, -2.0 * front_back))

        # closed eyes panic look
        frown_drive = 0.35 + 0.35 * abs(math.sin(phase * 2.2))
        set_morph(model, "困る", 0.62 + 0.20 * math.sin(phase * 2.2))
        set_morph(model, "怒り", frown_drive)
        set_morph(model, "下", 0.14 + 0.10 * abs(front_back))
        set_morph(model, "笑い", 0.0)
        set_morph(model, "にやり", 0.0)
        set_morph(model, "まばたき", 0.95)

    for fcurve in action.fcurves:
        for keyframe in fcurve.keyframe_points:
            keyframe.interpolation = "LINEAR"


def render_frames(output: Path, preview_only: bool) -> None:
    output.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    frames = (1, 8, 16, 24, 32, 40) if preview_only else range(1, FRAME_COUNT + 1)
    for frame in frames:
        scene.frame_set(frame)
        scene.render.filepath = str(output / f"drag-panic-sway-{frame:03d}.png")
        bpy.ops.render.render(write_still=True)
        print(f"Rendered frame {frame}")


def main() -> int:
    args = parse_args()
    setup_render()
    author_action()
    blend = args.blend.resolve()
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    render_frames(args.output.resolve(), args.preview_frames)
    print(f"Created drag panic sway action: {FRAME_COUNT} frames at {FPS} fps")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
