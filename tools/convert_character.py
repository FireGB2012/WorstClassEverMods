"""Turns a rigged, animated Meshy character (.glb) into a .cpc character for the mod.

    blender --background --factory-startup --python tools/convert_character.py -- <in.glb> <models_dir> <name>
        [--picture 1024] [--fps 15] [--preview <png>]

The character is turned to face forward (+Y), put with its feet on (0, 0, 0) and scaled to be
exactly 1 m tall; the mod sizes it in the game. Writes <name>.cpc and <name>.png.

.cpc is a text format, already in Unity's axes:
    x file.png                         the picture wrapped around the body
    n <bones>                          how many bones
    r px py pz qx qy qz qw             one line per bone: where it is at rest (whole-body space)
    v x y z nx ny nz u v b0 w0 b1 w1 b2 w2 b3 w3    a vertex, its normal, picture point, 4 bones and weights
    t a b c                            a triangle
    a <clip> <frames> <fps>            a move: idle, walk or run
    p px py pz qx qy qz qw             <frames> x <bones> lines: every bone's place in every frame

Bones are written in whole-body space, not parent-relative, so the mod can keep all bones side
by side under one root and needs no skeleton tree. Sideways travel of the hips is taken out of
every move, so the character walks on the spot and the game moves it.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector


def option(args, name, default):
    return args[args.index(name) + 1] if name in args else default


def base_picture(material):
    if material is None or not material.use_nodes:
        return None
    for node in material.node_tree.nodes:
        if node.type == 'BSDF_PRINCIPLED':
            for link in node.inputs['Base Color'].links:
                if link.from_node.type == 'TEX_IMAGE' and link.from_node.image is not None:
                    return link.from_node.image
    for node in material.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image is not None:
            return node.image
    return None


def unity_place(matrix):
    """Position and rotation of a Blender matrix, written in Unity's axes."""
    p = matrix.to_translation()
    q = matrix.to_quaternion()
    # Blender (x, y, z) with z up -> Unity (x, z, y) with y up. The swap mirrors space, which
    # flips the turn direction of a rotation.
    return (p.x, p.z, p.y, -q.x, -q.z, -q.y, q.w)


def clip_name(action_name, index):
    lower = action_name.lower()
    if 'idle' in lower:
        return 'idle'
    if 'walk' in lower:
        return 'walk'
    if 'run' in lower:
        return 'run'
    return ('idle', 'walk', 'run')[index] if index < 3 else 'clip%d' % index


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    source, out_dir, name = os.path.abspath(args[0]), os.path.abspath(args[1]), args[2]
    picture_size = int(option(args, '--picture', '1024'))
    fps = float(option(args, '--fps', '15'))
    preview = option(args, '--preview', '')

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)
    scene = bpy.context.scene
    armature = next(o for o in scene.objects if o.type == 'ARMATURE')
    meshes = [o for o in scene.objects if o.type == 'MESH']
    bones = list(armature.data.bones)
    bone_index = {bone.name: i for i, bone in enumerate(bones)}
    actions = list(bpy.data.actions)
    print('bones %d, meshes %d, actions %s' % (len(bones), len(meshes), [(a.name, tuple(a.frame_range)) for a in actions]))

    # Rest pose first: that is the pose the vertices are stored in.
    if armature.animation_data is not None:
        armature.animation_data.action = None
        armature.animation_data.use_nla = False
    armature.data.pose_position = 'REST'
    bpy.context.view_layer.update()

    points = []
    for obj in meshes:
        points.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
    low_z = min(p.z for p in points)
    high_z = max(p.z for p in points)
    mid_x = (min(p.x for p in points) + max(p.x for p in points)) * 0.5
    mid_y = (min(p.y for p in points) + max(p.y for p in points)) * 0.5
    # Face forward, feet on the ground in the middle, 1 m tall.
    fit = (Matrix.Scale(1.0 / max(high_z - low_z, 1e-6), 4) @ Matrix.Rotation(math.pi, 4, 'Z')
           @ Matrix.Translation((-mid_x, -mid_y, -low_z)))

    rest = [fit @ armature.matrix_world @ bone.matrix_local for bone in bones]

    os.makedirs(out_dir, exist_ok=True)
    lines = ['# classpack character: %s (from %s)' % (name, os.path.basename(source))]

    picture = None
    for obj in meshes:
        for material in obj.data.materials:
            picture = picture or base_picture(material)
    if picture is not None:
        copy = picture.copy()
        if max(copy.size[0], copy.size[1]) > picture_size:
            copy.scale(picture_size, picture_size)
        copy.file_format = 'PNG'
        copy.filepath_raw = os.path.join(out_dir, name + '.png')
        copy.save()
        lines.append('x ' + name + '.png')

    lines.append('n %d' % len(bones))
    for matrix in rest:
        lines.append('r %.5f %.5f %.5f %.5f %.5f %.5f %.5f' % unity_place(matrix))

    verts, index, tris = [], {}, []
    for obj in meshes:
        mesh = obj.data
        mesh.calc_loop_triangles()
        to_fit = fit @ obj.matrix_world
        turn = to_fit.to_3x3().normalized()
        uv_layer = mesh.uv_layers.active
        normals = mesh.corner_normals
        groups = {g.index: bone_index.get(g.name, -1) for g in obj.vertex_groups}
        skin = []
        for v in mesh.vertices:
            weights = sorted(((g.weight, groups.get(g.group, -1)) for g in v.groups if groups.get(g.group, -1) >= 0), reverse=True)[:4]
            total = sum(w for w, _ in weights) or 1.0
            weights = [(w / total, b) for w, b in weights] + [(0.0, 0)] * (4 - len(weights))
            skin.append(tuple(x for w, b in weights for x in (b, round(w, 4))))
        for tri in mesh.loop_triangles:
            corner = []
            for loop_index in tri.loops:
                vertex = mesh.loops[loop_index].vertex_index
                co = to_fit @ mesh.vertices[vertex].co
                normal = (turn @ normals[loop_index].vector).normalized()
                uv = uv_layer.data[loop_index].uv if uv_layer is not None else (0.0, 0.0)
                key = (round(co.x, 5), round(co.z, 5), round(co.y, 5), round(normal.x, 3), round(normal.z, 3), round(normal.y, 3),
                       round(uv[0], 5), round(uv[1], 5)) + skin[vertex]
                if key not in index:
                    index[key] = len(verts)
                    verts.append(key)
                corner.append(index[key])
            tris.append((corner[0], corner[2], corner[1]))
    for v in verts:
        lines.append('v %.5f %.5f %.5f %.3f %.3f %.3f %.5f %.5f %d %.4f %d %.4f %d %.4f %d %.4f' % v)
    for a, b, c in tris:
        lines.append('t %d %d %d' % (a, b, c))

    # The moves, sampled a few times a second.
    armature.data.pose_position = 'POSE'
    roots = [i for i, bone in enumerate(bones) if bone.parent is None]
    step = max(1.0, scene.render.fps / fps)
    written = []
    for action_number, action in enumerate(actions):
        if armature.animation_data is None:
            armature.animation_data_create()
        armature.animation_data.action = action
        slots = getattr(action, 'slots', None)
        if slots:
            armature.animation_data.action_slot = slots[0]
        start, end = action.frame_range
        frames = []
        frame = start
        while frame <= end + 1e-6:
            scene.frame_set(int(frame), subframe=frame - int(frame))
            frames.append([fit @ armature.matrix_world @ armature.pose.bones[bone.name].matrix for bone in bones])
            frame += step
        if len(frames) < 2:
            continue
        # Keep the hips over the same spot: the game moves the character, not the move.
        anchor = frames[0][roots[0]].to_translation() if roots else Vector()
        clip = clip_name(action.name, action_number)
        lines.append('a %s %d %.3f' % (clip, len(frames), scene.render.fps / step))
        for pose in frames:
            drift = (pose[roots[0]].to_translation() - anchor) if roots else Vector()
            shift = Matrix.Translation((-drift.x, -drift.y, 0.0))
            for matrix in pose:
                lines.append('p %.5f %.5f %.5f %.5f %.5f %.5f %.5f' % unity_place(shift @ matrix))
        written.append('%s %d frames' % (clip, len(frames)))

    with open(os.path.join(out_dir, name + '.cpc'), 'w', encoding='ascii', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('wrote %s: %d bones, %d vertices, %d triangles, moves: %s' % (name, len(bones), len(verts), len(tris), ', '.join(written)))

    if preview:
        # A picture of the walk, mid-stride, to check that the skin follows the bones.
        scene.frame_set(int((start + end) * 0.5))
        low = Vector((1e9, 1e9, 1e9))
        high = Vector((-1e9, -1e9, -1e9))
        for obj in meshes:
            for corner in obj.bound_box:
                p = obj.matrix_world @ Vector(corner)
                low = Vector((min(low.x, p.x), min(low.y, p.y), min(low.z, p.z)))
                high = Vector((max(high.x, p.x), max(high.y, p.y), max(high.z, p.z)))
        centre = (low + high) * 0.5
        reach = max((high - low).length, 0.1)
        camera_data = bpy.data.cameras.new('cam')
        camera_data.type = 'ORTHO'
        camera_data.ortho_scale = reach * 1.1
        camera = bpy.data.objects.new('cam', camera_data)
        scene.collection.objects.link(camera)
        direction = Vector((0.6, -1.0, 0.35)).normalized()
        camera.location = centre + direction * 6.0
        camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
        scene.camera = camera
        scene.render.engine = 'BLENDER_WORKBENCH'
        scene.render.resolution_x = 700
        scene.render.resolution_y = 800
        scene.render.filepath = os.path.abspath(preview)
        scene.display.shading.light = 'STUDIO'
        scene.display.shading.color_type = 'TEXTURE'
        scene.view_settings.view_transform = 'Standard'
        scene.world = bpy.data.worlds.new('w')
        scene.world.color = (0.86, 0.88, 0.92)
        bpy.ops.render.render(write_still=True)


main()
