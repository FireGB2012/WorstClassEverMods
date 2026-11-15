"""Puts the self-test's pictures on overview sheets, six to a sheet, each with its file name.

    python tools/selftest_sheets.py <folder with the pictures>

Writes sheet_1.jpg, sheet_2.jpg ... into the same folder.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

folder = sys.argv[1]
files = sorted(f for f in os.listdir(folder)
               if f.lower().endswith(('.jpg', '.png')) and not f.startswith('sheet_'))
tile_w, tile_h, label_h, columns, rows = 640, 360, 30, 2, 3
try:
    font = ImageFont.truetype('arialbd.ttf', 20)
except OSError:
    font = ImageFont.load_default()

per_sheet = columns * rows
made = 0
for start in range(0, len(files), per_sheet):
    batch = files[start:start + per_sheet]
    sheet = Image.new('RGB', (columns * tile_w, rows * (tile_h + label_h)), (30, 32, 38))
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(batch):
        x, y = (i % columns) * tile_w, (i // columns) * (tile_h + label_h)
        try:
            picture = Image.open(os.path.join(folder, name)).convert('RGB')
            picture.thumbnail((tile_w - 4, tile_h - 4))
            sheet.paste(picture, (x + (tile_w - picture.width) // 2, y + label_h + (tile_h - picture.height) // 2))
        except Exception as problem:  # a picture that is still being written, or broken
            draw.text((x + 8, y + label_h + 8), 'could not open: %s' % problem, fill=(255, 120, 120), font=font)
        draw.text((x + 8, y + 4), os.path.splitext(name)[0], fill=(240, 240, 240), font=font)
    made += 1
    sheet.save(os.path.join(folder, 'sheet_%d.jpg' % made), quality=88)
print('made %d sheet(s) from %d picture(s)' % (made, len(files)))
