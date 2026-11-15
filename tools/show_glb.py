"""Renders a clean picture of a .glb, seen from the front, a little from the side and above.

    blender --background --factory-startup --python show_glb.py -- <in.glb> <out.png>
"""
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
source, out = os.path.abspath(args[0]), os.path.abspath(args[1])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
scene = bpy.context.scene

low = Vector((1e9, 1e9, 1e9))
high = Vector((-1e9, -1e9, -1e9))
for obj in scene.objects:
    if obj.type != 'MESH':
        continue
    for corner in obj.bound_box:
        p = obj.matrix_world @ Vector(corner)
        low = Vector((min(low.x, p.x), min(low.y, p.y), min(low.z, p.z)))
        high = Vector((max(high.x, p.x), max(high.y, p.y), max(high.z, p.z)))

centre = (low + high) * 0.5
reach = max((high - low).length, 0.1)
camera_data = bpy.data.cameras.new('cam')
camera_data.type = 'ORTHO'
camera_data.ortho_scale = reach * 1.15
camera = bpy.data.objects.new('cam', camera_data)
scene.collection.objects.link(camera)
# A model imported from glTF faces -Y in Blender, so the camera stands on that side.
direction = Vector((0.6, -1.0, 0.5)).normalized()
camera.location = centre + direction * 5.0
camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
scene.camera = camera

scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 900
scene.render.resolution_y = 760
scene.render.filepath = out
shading = scene.display.shading
shading.light = 'STUDIO'
shading.color_type = 'TEXTURE'
shading.show_shadows = True
shading.show_cavity = True
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new('w')
scene.world.color = (0.86, 0.88, 0.92)
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.render.render(write_still=True)
print('rendered', out)
