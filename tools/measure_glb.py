"""Prints the box of a .glb as Blender loads it, and how it is spread along its longest side."""
import os, sys
import bpy
from mathutils import Vector
args = sys.argv[sys.argv.index('--') + 1:]
for source in args:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(source))
    pts = []
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            pts += [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    size = hi - lo
    print('MEASURE', os.path.basename(source), 'objects', len([o for o in bpy.context.scene.objects if o.type == 'MESH']),
          'lo', tuple(round(c, 3) for c in lo), 'hi', tuple(round(c, 3) for c in hi), 'size', tuple(round(c, 3) for c in size))
    axis = 0 if size.x >= size.y else 1
    other = 1 - axis
    bins = 10
    for b in range(bins):
        a0 = lo[axis] + size[axis] * b / bins
        a1 = lo[axis] + size[axis] * (b + 1) / bins
        inside = [p for p in pts if a0 <= p[axis] <= a1]
        if inside:
            print('MEASURE   %s %.2f..%.2f  z %.3f..%.3f  %s %.3f..%.3f  n=%d' % ('xy'[axis], a0, a1,
                  min(p.z for p in inside), max(p.z for p in inside), 'xy'[other],
                  min(p[other] for p in inside), max(p[other] for p in inside), len(inside)))
