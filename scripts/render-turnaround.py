"""Render a character turnaround (front, three-quarter, side, back) from Ibrahim's .blend files.

These renders are the references for AI-generated cutscenes, so the videos match the in-game models.
Uses Ibrahim's own "Preview studio" camera and lights, the Idle pose and Cycles on the CPU.

Needs Blender 4.5 as a Python module (Python 3.11):
    python3.11 -m venv bpyenv && bpyenv/bin/pip install "bpy>=4.5,<4.6"
Render one view per run (bpy sometimes crashes; just rerun the view):
    bpyenv/bin/python scripts/render-turnaround.py characters/dr-nyxen/dr-nyxen.blend out/dr-nyxen front
Views: front, three-quarter, side, back.
Do not run it from a folder containing files named like Python modules (inspect.py, bisect.py, ...):
bpy then aborts with "InitGoogleLogging() twice".
"""
import bpy, sys, math, os
blend, out, only = sys.argv[-3], sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene
# Hide Ibrahim's reference-image collections; keep the character and his preview studio.
for c in bpy.data.collections:
    if any(k in c.name.lower() for k in ("reference", "turnaround", "source")):
        c.hide_render = True
        for o in c.all_objects: o.hide_render = True
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
cam = next(o for o in bpy.data.objects if o.type == 'CAMERA' and not o.hide_render)
sc.camera = cam
# Idle pose, first frame.
idle = bpy.data.actions.get("Idle")
if idle:
    arm.animation_data_create(); arm.animation_data.action = idle
    sc.frame_set(int(idle.frame_range[0]))
r = sc.render
r.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 48
sc.cycles.use_denoising = True
r.film_transparent = True
r.resolution_x = 900; r.resolution_y = 1200; r.resolution_percentage = 100
r.image_settings.file_format = 'PNG'; r.image_settings.color_mode = 'RGBA'
base = arm.rotation_euler.z
os.makedirs(out, exist_ok=True)
for name, deg in [v for v in (("front", 0), ("three-quarter", 35), ("side", 90), ("back", 180)) if v[0] == only]:
    arm.rotation_euler.z = base + math.radians(deg)
    r.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("wrote", r.filepath, flush=True)
sys.stdout.flush(); os._exit(0)
