"""Turns a model made in Meshy (a .glb file) into a .cpm model plus its picture for the mod.

Run with Blender in the background, always with absolute paths:
    blender --background --factory-startup --python tools/convert_meshy.py -- <in.glb> <models_dir> <name>
        [--size 0.3] [--tris 6000] [--origin 0.5,0.5,0.5] [--offset 0,0,0] [--rot 0,0,180] [--picture 512] [--cut -0.06]

    name      the model's name in the mod, e.g. hat_cap. Writes <name>.cpm and <name>.png.
    --size    the longest side of the finished model, in metres.
    --tris    the model is thinned out to about this many triangles.
    --origin  where the model's (0, 0, 0) goes inside its own box, as fractions:
              0.5,0.5,0.5 is the middle, 0.5,0.5,0 is the middle of the bottom.
    --offset  moves the model after that, in metres (x right, y forward, z up).
    --rot     turns the model first, in degrees around x, y and z. Meshy models usually
              face the camera, so the default turns them to face forward (+Y).
    --picture the size the picture is shrunk to.
    --cut     leaves out every face wholly below this height, in metres after the moves above
              (the afro's ball of curls ends at the mouth instead of going round the chin).

The mod's axes are the same as in make_nerf_models.py: +Y forward, +Z up, in metres.
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector


def option(args, name, default):
    return args[args.index(name) + 1] if name in args else default


def numbers(text):
    return [float(part) for part in text.split(',')]


def base_picture(material):
    """The picture plugged into a material's base colour, or None."""
    if material is None or not material.use_nodes:
        return None
    for node in material.node_tree.nodes:
        if node.type != 'BSDF_PRINCIPLED':
            continue
        for link in node.inputs['Base Color'].links:
            if link.from_node.type == 'TEX_IMAGE' and link.from_node.image is not None:
                return link.from_node.image
    for node in material.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image is not None:
            return node.image
    return None


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    source, out_dir, name = os.path.abspath(args[0]), os.path.abspath(args[1]), args[2]
    size = float(option(args, '--size', '0.3'))
    target_tris = int(option(args, '--tris', '6000'))
    origin = numbers(option(args, '--origin', '0.5,0.5,0.5'))
    offset = Vector(numbers(option(args, '--offset', '0,0,0')))
    turn = Euler([math.radians(v) for v in numbers(option(args, '--rot', '0,0,180'))], 'XYZ').to_matrix().to_4x4()
    picture_size = int(option(args, '--picture', '512'))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise SystemExit('no mesh in ' + source)

    # One object, with every part where it really is.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh = obj.data

    mesh.calc_loop_triangles()
    before = len(mesh.loop_triangles)
    if before > target_tris:
        modifier = obj.modifiers.new('thin', 'DECIMATE')
        modifier.ratio = target_tris / float(before)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        mesh = obj.data

    # Turn, then shrink or grow to the wanted size, then put (0, 0, 0) where it was asked for.
    mesh.transform(turn)
    low = Vector((min(v.co.x for v in mesh.vertices), min(v.co.y for v in mesh.vertices), min(v.co.z for v in mesh.vertices)))
    high = Vector((max(v.co.x for v in mesh.vertices), max(v.co.y for v in mesh.vertices), max(v.co.z for v in mesh.vertices)))
    longest = max(high.x - low.x, high.y - low.y, high.z - low.z, 1e-6)
    scale = size / longest
    anchor = Vector((low.x + (high.x - low.x) * origin[0], low.y + (high.y - low.y) * origin[1], low.z + (high.z - low.z) * origin[2]))
    mesh.transform(Matrix.Translation(offset) @ Matrix.Scale(scale, 4) @ Matrix.Translation(-anchor))
    mesh.update()

    # Everything wholly below this height (in metres, after the moves above) is left out.
    cut = option(args, '--cut', '')
    if cut:
        import bmesh
        level = float(cut)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        gone = [face for face in bm.faces if all(vert.co.z < level for vert in face.verts)]
        bmesh.ops.delete(bm, geom=gone, context='FACES')
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
    mesh.calc_loop_triangles()

    # Pictures: one per material that has one, shrunk and saved next to the model.
    os.makedirs(out_dir, exist_ok=True)
    bpy.context.scene.view_settings.view_transform = 'Standard'
    picture_files = {}
    for index, material in enumerate(mesh.materials):
        image = base_picture(material)
        if image is None:
            continue
        file_name = name + ('' if index == 0 else '_%d' % index) + '.png'
        copy = image.copy()
        if max(copy.size[0], copy.size[1]) > picture_size:
            copy.scale(picture_size, picture_size)
        copy.file_format = 'PNG'
        copy.filepath_raw = os.path.join(out_dir, file_name)
        copy.save()
        picture_files[index] = file_name

    uv_layer = mesh.uv_layers.active
    normals = mesh.corner_normals
    groups = {}
    for tri in mesh.loop_triangles:
        verts, index, tris = groups.setdefault(tri.material_index, ([], {}, []))
        corner = []
        for loop_index in tri.loops:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            normal = normals[loop_index].vector
            uv = uv_layer.data[loop_index].uv if uv_layer is not None else (0.0, 0.0)
            # Blender (x, y, z) with z up  ->  Unity (x, z, y) with y up.
            key = (round(co.x, 5), round(co.z, 5), round(co.y, 5), round(normal.x, 3), round(normal.z, 3), round(normal.y, 3),
                   round(uv[0], 5), round(uv[1], 5))
            if key not in index:
                index[key] = len(verts)
                verts.append(key)
            corner.append(index[key])
        # The axis swap mirrors the model, so the winding is reversed to keep faces outward.
        tris.append((corner[0], corner[2], corner[1]))

    lines = ['# classpack model: %s (from %s)' % (name, os.path.basename(source))]
    base = 0
    total = 0
    for material_index, (verts, _, tris) in sorted(groups.items()):
        color = (255, 255, 255)
        if material_index not in picture_files and material_index < len(mesh.materials) and mesh.materials[material_index] is not None:
            c = mesh.materials[material_index].diffuse_color
            color = (int(c[0] * 255), int(c[1] * 255), int(c[2] * 255))
        lines.append('m %d %d %d' % color)
        if material_index in picture_files:
            lines.append('x ' + picture_files[material_index])
        for v in verts:
            lines.append('v %.5f %.5f %.5f %.3f %.3f %.3f %.5f %.5f' % v)
        for a, b, c in tris:
            lines.append('t %d %d %d' % (a + base, b + base, c + base))
        base += len(verts)
        total += len(tris)
    with open(os.path.join(out_dir, name + '.cpm'), 'w', encoding='ascii', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('wrote %s: %d vertices, %d triangles (from %d), pictures: %s' % (name, base, total, before, ', '.join(picture_files.values()) or 'none'))

    preview = option(args, '--preview', '')
    if preview:
        render_preview(obj, os.path.abspath(preview))


def render_preview(obj, path):
    """A picture of the finished model seen from the front (+Y side), a little from the right and above.

    The red bar on the ground starts at (0, 0, 0) and points forward (+Y), the blue bar
    points up, so the model's facing and its origin can be checked before it goes in the game.
    """
    scene = bpy.context.scene
    corners = [Vector(c) for c in obj.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    reach = max((high - low).length, 0.1)

    def bar(name, size, at, color):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
        marker = bpy.context.active_object
        marker.name = name
        marker.scale = size
        material = bpy.data.materials.new(name)
        material.diffuse_color = color
        marker.data.materials.append(material)

    thick = reach * 0.012
    bar('forward', (thick, reach * 0.6, thick), (0.0, reach * 0.3, 0.0), (1.0, 0.1, 0.1, 1.0))
    bar('up', (thick, thick, reach * 0.35), (0.0, 0.0, reach * 0.175), (0.1, 0.3, 1.0, 1.0))

    centre = (low + high) * 0.5
    camera_data = bpy.data.cameras.new('cam')
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = reach * 1.5
    camera = bpy.data.objects.new('cam', camera_data)
    scene.collection.objects.link(camera)
    direction = Vector((0.55, 1.0, 0.5)).normalized()
    camera.location = centre + direction * 5.0
    camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.render.filepath = path
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.world = bpy.data.worlds.new('w')
    scene.world.color = (0.82, 0.85, 0.9)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)


main()
