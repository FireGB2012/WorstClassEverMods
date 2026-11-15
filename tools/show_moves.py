"""Renders a few frames of every move in a rigged, animated .glb, seen from the side, as one strip.

    blender --background --factory-startup --python tools/show_moves.py -- <in.glb> <out.png> [frames per move]

For looking at what a Meshy move really looks like before it goes into the game.
"""
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
source, out = os.path.abspath(args[0]), os.path.abspath(args[1])
per_move = int(args[2]) if len(args) > 2 else 4

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
scene = bpy.context.scene
armature = next(o for o in scene.objects if o.type == 'ARMATURE')
for obj in list(scene.objects):
    if obj.type == 'MESH' and not any(m.type == 'ARMATURE' for m in obj.modifiers):
        bpy.data.objects.remove(obj)

world = bpy.data.worlds.new('w')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.93, 0.94, 0.96, 1)
world.node_tree.nodes['Background'].inputs[1].default_value = 1.0
scene.world = world
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
sun.data.energy = 3.0
sun.rotation_euler = (0.9, 0.2, 0.6)
scene.collection.objects.link(sun)

camera_data = bpy.data.cameras.new('cam')
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 2.6
camera = bpy.data.objects.new('cam', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in type(scene.render).bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
scene.render.resolution_x = 420
scene.render.resolution_y = 420
scene.render.film_transparent = False

shots = []
folder = os.path.dirname(out)
for action in bpy.data.actions:
    armature.animation_data.action = action
    slots = getattr(action, 'slots', None)
    if slots:
        armature.animation_data.action_slot = slots[0]
    start, end = action.frame_range
    for i in range(per_move):
        frame = start + (end - start) * i / per_move
        scene.frame_set(int(frame))
        hips = armature.matrix_world @ armature.pose.bones[0].head
        # From the character's right side (it faces -Y), level with the hips.
        camera.location = Vector((hips.x - 6.0, hips.y, hips.z + 0.1))
        camera.rotation_euler = (1.5708, 0.0, -1.5708)
        path = os.path.join(folder, '_move_%s_%d.png' % (action.name, i))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        shots.append((action.name, int(frame), path))

print('SHOTS ' + ';'.join('%s|%d|%s' % s for s in shots))
