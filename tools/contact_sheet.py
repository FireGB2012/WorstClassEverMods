"""Puts the model pictures on overview sheets, twelve to a sheet, each with its name."""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

src, out_dir = sys.argv[1], sys.argv[2]
names = {
    'airplane': 'Paper airplane', 'back_cape': 'Cape', 'back_pack': 'Backpack', 'back_wings': 'Wings',
    'balloon': 'Water balloon', 'body_apron': 'Apron', 'body_belly': 'Gym teacher belly', 'cushion': 'Whoopee cushion',
    'face_moustache': 'Moustache', 'face_shades': 'Sunglasses', 'glue': 'Super glue', 'hand_ladle': 'Ladle',
    'hand_mop': 'Mop', 'hat_beanie': 'Beanie', 'hat_cap': 'Cap', 'hat_chef': 'Chef hat', 'hat_cone': 'Traffic cone',
    'hat_cowboy': 'Cowboy hat', 'hat_crown': 'Crown', 'hat_hairnet': 'Hair net', 'hat_janitor': 'Janitor cap',
    'hat_party': 'Party hat', 'hat_top': 'Top hat', 'hat_wig': 'Wig', 'laser': 'Laser pointer', 'note': 'Note',
    'pistol': 'Nerf pistol', 'prop_pot': 'Lunch lady pot', 'puddle': 'Glue puddle', 'rpg': 'Nerf RPG',
    'shot_pizza': 'Pizza slice', 'shot_potato': 'Mashed potatoes', 'shotgun': 'Nerf shotgun', 'sniper': 'Nerf sniper',
    'splat': 'Water splash',
}
files = sorted(f for f in os.listdir(src) if f.lower().endswith('.png'))
tile_w, tile_h, label_h, columns, rows = 450, 380, 44, 4, 3
try:
    font = ImageFont.truetype('arialbd.ttf', 26)
except OSError:
    font = ImageFont.load_default()

per_sheet = columns * rows
for sheet_index in range(0, len(files), per_sheet):
    batch = files[sheet_index:sheet_index + per_sheet]
    sheet = Image.new('RGB', (columns * tile_w, rows * (tile_h + label_h)), (236, 239, 245))
    draw = ImageDraw.Draw(sheet)
    for i, file_name in enumerate(batch):
        x = (i % columns) * tile_w
        y = (i // columns) * (tile_h + label_h)
        picture = Image.open(os.path.join(src, file_name)).convert('RGB').resize((tile_w, tile_h), Image.LANCZOS)
        sheet.paste(picture, (x, y))
        label = names.get(os.path.splitext(file_name)[0], os.path.splitext(file_name)[0])
        box = draw.textbbox((0, 0), label, font=font)
        draw.text((x + (tile_w - (box[2] - box[0])) // 2, y + tile_h + 6), label, fill=(30, 34, 44), font=font)
    path = os.path.join(out_dir, 'toy_models_%d.png' % (sheet_index // per_sheet + 1))
    sheet.save(path)
    print('wrote', path, len(batch))
