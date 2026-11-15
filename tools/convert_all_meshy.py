"""Converts every Meshy model in meshy\\toy into the mod's models, one Blender run each.

    python tools\\convert_all_meshy.py [name ...]

With no names it converts all of them. Each line of MODELS says how one model sits in the mod:
    size    longest side in metres
    origin  where the model's (0, 0, 0) goes inside its own box, as fractions (x, y, z)
    offset  a move after that, in metres (x right, y forward, z up)
    rot     a turn before all that, in degrees around x, y and z

Meshy models face the camera, which is -Y once Blender has loaded them. The mod wants +Y as
forward, so most models are turned 180 degrees. Things worn on the back keep their facing,
because their outer side should look backwards. Long things that Meshy laid along X (guns,
glue, laser) are turned a quarter instead, so their tip points forward.

Head items are built around (0, 0, 0) = the middle of a head 0.11 m in radius; the game
scales them up to its own big heads. A picture of every result, with a red bar pointing
forward and a blue bar pointing up from (0, 0, 0), goes to dist\\previews\\meshy.
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BLENDER = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
FRONT = '0,0,180'
BACK = '0,0,0'
TIP = '0,0,-90'

MODELS = {
    # name: (size, origin, offset, rot)
    'hat_cap': (0.30, '0.5,0.38,0', '0,0,0.04', FRONT),
    'hat_beanie': (0.27, '0.5,0.5,0', '0,0,0.02', FRONT),
    'hat_top': (0.34, '0.5,0.5,0', '0,0,0.045', FRONT),
    'hat_crown': (0.26, '0.5,0.5,0', '0,0,0.05', FRONT),
    'hat_cowboy': (0.46, '0.5,0.5,0', '0,0,0.045', FRONT),
    'hat_party': (0.27, '0.5,0.5,0', '0,0,0.06', FRONT),
    'hat_cone': (0.34, '0.5,0.5,0', '0,0,0.045', FRONT),
    'hat_chef': (0.32, '0.5,0.5,0', '0,0,0.04', FRONT),
    'hat_hairnet': (0.29, '0.5,0.5,0', '0,0,0.0', FRONT),
    'hat_janitor': (0.30, '0.5,0.4,0', '0,0,0.04', FRONT),
    'hat_wig': (0.36, '0.5,0.5,0.45', '0,0,0', FRONT),
    'face_shades': (0.25, '0.5,1,0.5', '0,0.127,0.012', FRONT),
    'face_moustache': (0.14, '0.5,0.5,0.5', '0,0.125,-0.038', FRONT),
    'back_pack': (0.24, '0.5,1,0.6', '0,0,0', BACK),
    'back_cape': (0.50, '0.5,1,0.80', '0,0,0', BACK),
    'back_wings': (0.55, '0.5,1,0.5', '0,0,0', BACK),
    'body_belly': (0.30, '0.5,0.3,0.5', '0,0,0', FRONT),
    'body_apron': (0.40, '0.5,0,0.62', '0,0.02,0', FRONT),
    'hand_ladle': (0.32, '0.5,0.5,0.12', '0,0,0', FRONT),
    'hand_mop': (0.85, '0.5,0.5,0.5', '0,0,0', FRONT),
    'prop_pot': (0.60, '0.5,0.5,0', '0,0,0', FRONT),
    'shot_pizza': (0.28, '0.5,0.5,0.5', '0,0,0', FRONT),
    'shot_potato': (0.17, '0.5,0.5,0.5', '0,0,0', FRONT),
    'airplane': (0.30, '0.5,0.5,0.5', '0,0,0', FRONT),
    'balloon': (0.16, '0.5,0.5,0.5', '0,0,0', FRONT),
    'glue': (0.16, '0.5,0.5,0.5', '0,0,0', TIP),
    'laser': (0.17, '0.5,0.5,0.5', '0,0,0', TIP),
    'cushion': (0.26, '0.5,0.5,0', '0,0,0', FRONT),
    'note': (0.11, '0.5,0.5,0.5', '0,0,0', FRONT),
    'puddle': (0.50, '0.5,0.5,0', '0,0,0', FRONT),
    'splat': (0.65, '0.5,0.5,0', '0,0,0', FRONT),
    'pistol': (0.31, '0.5,0.35,0.4', '0,0,0', TIP),
    'shotgun': (0.89, '0.5,0.45,0.4', '0,0,0', TIP),
    'sniper': (1.10, '0.5,0.42,0.35', '0,0,0', TIP),
    'rpg': (1.10, '0.5,0.4,0.65', '0,0,0', TIP),
    # Pool hall props: real sizes in metres, standing on (0, 0, 0), not turned, so Meshy's
    # front stays the front (the game sees it as -Z). The board's (0, 0, 0) is the floor under
    # the tip of its plank; its ladder is on the -Z side. The ladder's is the pool's edge at
    # deck height, the duck's its waterline.
    'pool_board': (3.21, '0,0.643,0', '0,0,0', BACK),
    'pool_chair': (1.9, '0.5,0.5,0', '0,0,0', BACK),
    'pool_ladder': (1.7, '0.5,0.3,0.55', '0,0,0', BACK),
    'pool_ring': (0.8, '0.5,0.5,0.5', '0,0,0', BACK),
    'pool_bench': (1.9, '0.5,0.5,0', '0,0,0', BACK),
    'pool_duck': (1.3, '0.5,0.5,0.14', '0,0,0', BACK),
}


# White things came out of Meshy a little grey. Their pictures are brightened by this much.
BRIGHTER = {'back_wings': 1.3, 'note': 1.3, 'hat_chef': 1.2}


def brighten(path, gain):
    from PIL import Image
    picture = Image.open(path).convert('RGBA')
    red, green, blue, alpha = picture.split()
    lift = lambda value: min(255, int(value * gain))
    Image.merge('RGBA', (red.point(lift), green.point(lift), blue.point(lift), alpha)).save(path)


def main():
    wanted = sys.argv[1:] or sorted(MODELS)
    source_dir = os.path.join(ROOT, 'meshy', 'toy')
    out_dir = os.path.join(ROOT, 'package', 'models')
    preview_dir = os.path.join(ROOT, 'dist', 'previews', 'meshy')
    script = os.path.join(ROOT, 'tools', 'convert_meshy.py')
    failed = []
    for name in wanted:
        size, origin, offset, rot = MODELS[name]
        source = os.path.join(source_dir, name + '.glb')
        if not os.path.exists(source):
            failed.append(name + ' (no .glb)')
            continue
        result = subprocess.run(
            [BLENDER, '--background', '--factory-startup', '--python', script, '--', source, out_dir, name,
             '--size', str(size), '--origin', origin, '--offset', offset, '--rot', rot, '--tris', '6000',
             '--picture', '512', '--preview', os.path.join(preview_dir, name + '.png')],
            capture_output=True, text=True)
        line = [l for l in result.stdout.splitlines() if l.startswith('wrote ')]
        if line:
            print(line[0])
            if name in BRIGHTER:
                brighten(os.path.join(out_dir, name + '.png'), BRIGHTER[name])
        else:
            failed.append(name)
            print('FAILED %s: %s' % (name, (result.stdout + result.stderr)[-400:]))
    print('converted %d, failed %d %s' % (len(wanted) - len(failed), len(failed), failed))


main()
