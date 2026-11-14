"""Builds the mod's 3D models (nerf guns and trick props) and writes them as .cpm files.

Run with Blender in the background:
    blender --background --python tools/make_nerf_models.py -- <models_out_dir> <previews_out_dir>

Each gun points along +Y, with +Z up and its thickness along X, in metres.
(0, 0, 0) is where the trigger hand sits. The trick props follow the same axes;
each builder says where its own (0, 0, 0) is.

.cpm is the mod's own tiny text format, already converted to Unity's axes:
    m r g b              start a colour group (0-255)
    v x y z nx ny nz     a vertex with its normal
    t a b c              a triangle, by vertex number across the whole file
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ORANGE = (255, 128, 16)
TIP = (255, 84, 8)
BLUE = (28, 104, 232)
YELLOW = (250, 208, 36)
GREEN = (44, 176, 72)
RED = (226, 44, 40)
WHITE = (244, 244, 244)
GREY = (128, 134, 146)
DARK = (38, 40, 48)
LENS = (70, 170, 255)
PAPER = (246, 246, 240)
PAPER_FOLD = (226, 226, 214)
INK = (90, 130, 220)
PINK = (236, 90, 130)
PINK_DARK = (198, 58, 100)
WATER = (56, 136, 235)
WATER_LIGHT = (150, 205, 255)
CREAM = (244, 238, 200)
CREAM_LIGHT = (253, 250, 228)
SILVER = (196, 200, 208)

SMOOTH_ANGLE = math.radians(42.0)


class Model:
    """A list of (bmesh, colour) parts. Everything is modelled in place, no object transforms."""

    def __init__(self, name):
        self.name = name
        self.parts = []

    def add(self, bm, color):
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        self.parts.append((bm, color))

    # -- shapes -------------------------------------------------------------

    def profile(self, points, half_x, color, bevel=0.0, x=0.0):
        """Extrude a side-view outline (list of (y, z)) to a slab 2*half_x thick."""
        bm = bmesh.new()
        left = [bm.verts.new((x - half_x, y, z)) for y, z in points]
        right = [bm.verts.new((x + half_x, y, z)) for y, z in points]
        bm.faces.new(left)
        bm.faces.new(list(reversed(right)))
        n = len(points)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((left[j], left[i], right[i], right[j]))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        if bevel > 0.0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
        self.add(bm, color)

    def box(self, center, size, color, bevel=0.0):
        """center and size are (x, y, z)."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1.0)))
        if bevel > 0.0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
        self.add(bm, color)

    def tube(self, start, end, r1, color, r2=None, segments=28):
        """A cylinder or cone from start to end (both (x, y, z))."""
        a, b = Vector(start), Vector(end)
        axis = b - a
        rotation = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=r1, radius2=r1 if r2 is None else r2, depth=axis.length,
                              matrix=Matrix.Translation((a + b) * 0.5) @ rotation)
        self.add(bm, color)

    def ball(self, center, scale, color):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=1.0,
                                  matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1.0)))
        self.add(bm, color)

    def sheet(self, points, color, thickness=0.004):
        """A flat piece through the given (x, y, z) corners, just thick enough to show from both sides."""
        corners = [Vector(p) for p in points]
        normal = (corners[1] - corners[0]).cross(corners[2] - corners[0]).normalized() * (thickness * 0.5)
        bm = bmesh.new()
        top = [bm.verts.new(c + normal) for c in corners]
        bottom = [bm.verts.new(c - normal) for c in corners]
        bm.faces.new(top)
        bm.faces.new(list(reversed(bottom)))
        n = len(corners)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((top[i], bottom[i], bottom[j], top[j]))
        self.add(bm, color)

    def slab(self, points, z0, z1, color, bevel=0.0):
        """Extrude a top-view outline (list of (x, y)) from height z0 up to z1."""
        bm = bmesh.new()
        low = [bm.verts.new((x, y, z0)) for x, y in points]
        high = [bm.verts.new((x, y, z1)) for x, y in points]
        bm.faces.new(list(reversed(low)))
        bm.faces.new(high)
        n = len(points)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((low[i], low[j], high[j], high[i]))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        if bevel > 0.0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
        self.add(bm, color)

    # -- shared details -----------------------------------------------------

    def trigger_and_guard(self, y):
        """Trigger with a guard loop, the trigger sitting at the given y."""
        self.box((0.0, y + 0.018, -0.040), (0.018, 0.090, 0.008), DARK, 0.002)
        self.box((0.0, y + 0.060, -0.012), (0.018, 0.008, 0.062), DARK, 0.002)
        self.profile([(y - 0.004, 0.012), (y + 0.010, 0.012), (y + 0.014, -0.024), (y + 0.004, -0.028)], 0.006, YELLOW, 0.0015)

    def grip(self, y, color, slant=0.022, length=0.128):
        self.profile([(y - 0.036, 0.024), (y + 0.034, 0.024), (y + 0.034 - slant, -length + 0.024),
                      (y - 0.036 - slant, -length + 0.024)], 0.021, color, 0.007)
        for i in range(3):
            z = -0.022 - i * 0.026
            lean = slant * (0.024 - z) / length
            self.box((0.0, y + 0.036 - lean, z), (0.044, 0.006, 0.010), DARK, 0.002)


def pistol():
    m = Model('pistol')
    m.profile([(-0.115, 0.020), (0.105, 0.020), (0.135, 0.040), (0.135, 0.085), (0.110, 0.100),
               (-0.090, 0.100), (-0.120, 0.078)], 0.024, ORANGE, 0.006)
    m.box((0.0, 0.005, 0.108), (0.024, 0.170, 0.016), YELLOW, 0.004)
    m.box((0.0, -0.082, 0.122), (0.030, 0.018, 0.014), DARK, 0.003)
    m.box((0.0, 0.098, 0.121), (0.010, 0.016, 0.012), DARK, 0.003)
    m.tube((0.0, 0.130, 0.062), (0.0, 0.176, 0.062), 0.020, DARK)
    m.tube((0.0, 0.170, 0.062), (0.0, 0.192, 0.062), 0.025, TIP)
    m.tube((0.0, 0.186, 0.062), (0.0, 0.194, 0.062), 0.013, DARK)
    m.grip(-0.066, BLUE)
    m.trigger_and_guard(-0.012)
    for side in (-1.0, 1.0):
        m.box((side * 0.0245, 0.010, 0.062), (0.004, 0.150, 0.013), WHITE, 0.0015)
        m.box((side * 0.0245, -0.050, 0.040), (0.004, 0.050, 0.008), BLUE, 0.0015)
        m.tube((side * 0.022, -0.085, 0.060), (side * 0.0275, -0.085, 0.060), 0.007, DARK, segments=14)
        m.tube((side * 0.022, 0.105, 0.045), (side * 0.0275, 0.105, 0.045), 0.006, DARK, segments=14)
    return m


def shotgun():
    m = Model('shotgun')
    m.profile([(-0.160, 0.000), (0.120, 0.000), (0.140, 0.020), (0.140, 0.085), (0.110, 0.100),
               (-0.140, 0.100), (-0.170, 0.070)], 0.030, BLUE, 0.007)
    m.box((0.0, -0.010, 0.108), (0.026, 0.220, 0.014), YELLOW, 0.004)
    # Barrel on top, magazine tube under it, with the pump riding the tube.
    m.tube((0.0, 0.135, 0.070), (0.0, 0.440, 0.070), 0.022, GREY)
    m.tube((0.0, 0.430, 0.070), (0.0, 0.458, 0.070), 0.027, TIP)
    m.tube((0.0, 0.452, 0.070), (0.0, 0.460, 0.070), 0.014, DARK)
    m.tube((0.0, 0.135, 0.026), (0.0, 0.400, 0.026), 0.017, DARK)
    m.box((0.0, 0.270, 0.024), (0.064, 0.150, 0.056), ORANGE, 0.012)
    for i in range(4):
        m.box((0.0, 0.222 + i * 0.032, 0.024), (0.068, 0.008, 0.044), DARK, 0.002)
    m.box((0.0, 0.415, 0.048), (0.030, 0.014, 0.070), DARK, 0.003)
    m.profile([(-0.165, 0.088), (-0.165, 0.020), (-0.400, -0.045), (-0.425, -0.040), (-0.425, 0.075),
               (-0.400, 0.088)], 0.024, ORANGE, 0.008)
    m.box((0.0, -0.432, 0.018), (0.054, 0.016, 0.126), DARK, 0.005)
    m.grip(-0.112, DARK)
    m.trigger_and_guard(-0.056)
    # Spare shells clipped to the right side.
    for i in range(3):
        y = -0.090 + i * 0.050
        m.tube((0.034, y, 0.052), (0.034, y + 0.034, 0.052), 0.0095, RED, segments=16)
        m.tube((0.034, y + 0.034, 0.052), (0.034, y + 0.042, 0.052), 0.0095, YELLOW, segments=16)
    m.box((0.031, -0.025, 0.040), (0.006, 0.150, 0.008), DARK, 0.002)
    m.box((-0.0305, -0.020, 0.052), (0.004, 0.200, 0.014), WHITE, 0.0015)
    return m


def sniper():
    m = Model('sniper')
    m.profile([(-0.200, 0.000), (0.160, 0.000), (0.180, 0.015), (0.180, 0.075), (0.150, 0.090),
               (-0.180, 0.090), (-0.210, 0.065)], 0.026, GREEN, 0.006)
    m.tube((0.0, 0.175, 0.050), (0.0, 0.640, 0.050), 0.014, GREY)
    m.tube((0.0, 0.175, 0.050), (0.0, 0.360, 0.050), 0.025, YELLOW)
    for i in range(4):
        m.tube((0.0, 0.200 + i * 0.040, 0.050), (0.0, 0.214 + i * 0.040, 0.050), 0.0265, DARK)
    m.tube((0.0, 0.610, 0.050), (0.0, 0.680, 0.050), 0.024, TIP)
    m.tube((0.0, 0.674, 0.050), (0.0, 0.682, 0.050), 0.012, DARK)
    # Scope with a wide front bell, a narrower eyepiece and blue glass.
    m.tube((0.0, -0.120, 0.150), (0.0, 0.140, 0.150), 0.022, DARK)
    m.tube((0.0, 0.140, 0.150), (0.0, 0.200, 0.150), 0.022, DARK, r2=0.034)
    m.tube((0.0, 0.200, 0.150), (0.0, 0.212, 0.150), 0.034, DARK)
    m.tube((0.0, 0.208, 0.150), (0.0, 0.214, 0.150), 0.029, LENS)
    m.tube((0.0, -0.165, 0.150), (0.0, -0.120, 0.150), 0.029, DARK, r2=0.022)
    m.tube((0.0, -0.171, 0.150), (0.0, -0.164, 0.150), 0.024, LENS)
    m.tube((0.0, 0.010, 0.168), (0.0, 0.010, 0.192), 0.013, YELLOW, segments=16)
    m.tube((0.022, 0.010, 0.150), (0.044, 0.010, 0.150), 0.012, YELLOW, segments=16)
    for y in (-0.065, 0.085):
        m.box((0.0, y, 0.108), (0.022, 0.026, 0.042), DARK, 0.003)
    m.profile([(-0.200, 0.082), (-0.200, 0.030), (-0.455, -0.030), (-0.480, -0.030), (-0.480, 0.082)],
              0.022, GREEN, 0.007)
    m.box((0.0, -0.330, 0.092), (0.038, 0.160, 0.022), YELLOW, 0.006)
    m.box((0.0, -0.488, 0.026), (0.052, 0.016, 0.120), DARK, 0.005)
    m.grip(-0.132, DARK)
    m.trigger_and_guard(-0.076)
    m.box((0.0, 0.040, -0.048), (0.034, 0.062, 0.100), ORANGE, 0.006)
    # Bipod.
    for side in (-1.0, 1.0):
        m.tube((side * 0.010, 0.400, 0.036), (side * 0.070, 0.440, -0.130), 0.0065, DARK, segments=12)
        m.ball((side * 0.070, 0.440, -0.132), (0.012, 0.016, 0.008), DARK)
    m.box((0.0, 0.400, 0.036), (0.040, 0.030, 0.022), DARK, 0.004)
    m.box((-0.0265, -0.010, 0.048), (0.004, 0.280, 0.014), WHITE, 0.0015)
    m.box((0.0265, -0.010, 0.048), (0.004, 0.280, 0.014), WHITE, 0.0015)
    return m


def rpg():
    m = Model('rpg')
    m.tube((0.0, -0.300, 0.0), (0.0, 0.340, 0.0), 0.045, GREY, segments=32)
    m.tube((0.0, 0.300, 0.0), (0.0, 0.362, 0.0), 0.053, DARK, segments=32)
    m.tube((0.0, -0.430, 0.0), (0.0, -0.300, 0.0), 0.072, DARK, r2=0.045, segments=32)
    m.tube((0.0, -0.445, 0.0), (0.0, -0.430, 0.0), 0.076, DARK, segments=32)
    m.tube((0.0, -0.025, 0.0), (0.0, 0.025, 0.0), 0.049, BLUE, segments=32)
    m.tube((0.0, -0.250, 0.0), (0.0, -0.225, 0.0), 0.049, YELLOW, segments=32)
    # Rocket: stem, fat body, pointed nose.
    m.tube((0.0, 0.360, 0.0), (0.0, 0.440, 0.0), 0.030, RED, r2=0.076, segments=32)
    m.tube((0.0, 0.440, 0.0), (0.0, 0.560, 0.0), 0.076, RED, segments=32)
    m.tube((0.0, 0.470, 0.0), (0.0, 0.500, 0.0), 0.079, WHITE, segments=32)
    m.tube((0.0, 0.560, 0.0), (0.0, 0.700, 0.0), 0.076, RED, r2=0.016, segments=32)
    m.ball((0.0, 0.702, 0.0), (0.022, 0.026, 0.022), YELLOW)
    # Top shield and sights.
    m.box((0.0, -0.050, 0.050), (0.062, 0.260, 0.022), ORANGE, 0.008)
    m.box((0.0, 0.110, 0.074), (0.012, 0.020, 0.060), DARK, 0.003)
    m.box((0.0, -0.160, 0.070), (0.026, 0.016, 0.046), DARK, 0.003)
    # Two grips under the tube, joined by a bar.
    m.box((0.0, -0.040, -0.050), (0.030, 0.260, 0.020), DARK, 0.005)
    for y, color in ((0.040, ORANGE), (-0.110, ORANGE)):
        m.profile([(y - 0.030, -0.045), (y + 0.030, -0.045), (y + 0.012, -0.170), (y - 0.048, -0.170)],
                  0.020, color, 0.007)
    m.box((0.0, -0.062, -0.076), (0.018, 0.070, 0.008), DARK, 0.002)
    m.profile([(-0.082, -0.058), (-0.070, -0.058), (-0.066, -0.088), (-0.076, -0.090)], 0.006, YELLOW, 0.0015)
    return m


# ------------------------------------------------------------- trick props


def blob(radius, wobble, lobes, steps=44, phase=0.0):
    """A wobbly round outline, for puddles and splashes."""
    points = []
    for i in range(steps):
        a = 2.0 * math.pi * i / steps
        r = radius * (1.0 + wobble * math.sin(lobes * a + phase)
                      + wobble * 0.5 * math.sin((lobes + 3) * a + 1.7 * phase + 0.6))
        points.append((r * math.cos(a), r * math.sin(a)))
    return points


def airplane():
    """A folded paper dart, nose along +Y. (0, 0, 0) is where the fingers pinch it."""
    m = Model('airplane')
    nose = Vector((0.0, 0.150, 0.0))
    tail = -0.130
    for side in (-1.0, 1.0):
        root = Vector((side * 0.012, tail, 0.004))
        tip = Vector((side * 0.105, tail, 0.024))
        m.sheet([nose, root, tip], PAPER)
        # The fold under each wing: together they make the keel you hold it by.
        m.sheet([nose, root, Vector((side * 0.004, tail, -0.042))], PAPER_FOLD)
        # A ruled line and a stripe on top of the wing, like a page torn from a notebook.
        lift = (root - nose).cross(tip - nose).normalized()
        if lift.z < 0.0:
            lift = -lift
        lift *= 0.003

        def on_wing(u, v, nose=nose, root=root, tip=tip, lift=lift):
            return nose + (root - nose) * u + (tip - nose) * v + lift

        m.sheet([on_wing(0.42, 0.34), on_wing(0.34, 0.66), on_wing(0.26, 0.74), on_wing(0.36, 0.40)], INK, 0.002)
        m.sheet([on_wing(0.10, 0.62), on_wing(0.04, 0.96), on_wing(0.00, 1.00), on_wing(0.06, 0.66)], RED, 0.002)
    return m


def balloon():
    """A filled water balloon, knot towards -Y. (0, 0, 0) is its middle."""
    m = Model('balloon')
    m.ball((0.0, 0.010, 0.0), (0.062, 0.074, 0.060), WATER)
    m.tube((0.0, -0.058, 0.0), (0.0, -0.080, 0.0), 0.017, WATER, r2=0.007, segments=18)
    m.tube((0.0, -0.080, 0.0), (0.0, -0.092, 0.0), 0.007, WATER_LIGHT, r2=0.014, segments=18)
    m.ball((0.024, 0.040, 0.034), (0.013, 0.018, 0.010), WATER_LIGHT)
    return m


def glue():
    """A squeeze tube of super glue, nozzle along +Y. (0, 0, 0) is the middle of the tube."""
    m = Model('glue')
    m.tube((0.0, -0.060, 0.0), (0.0, 0.040, 0.0), 0.022, WHITE, segments=26)
    m.box((0.0, -0.070, 0.0), (0.052, 0.024, 0.007), GREY, 0.002)
    m.tube((0.0, 0.040, 0.0), (0.0, 0.058, 0.0), 0.022, WHITE, r2=0.008, segments=26)
    m.tube((0.0, 0.058, 0.0), (0.0, 0.068, 0.0), 0.008, WHITE, segments=18)
    m.tube((0.0, 0.066, 0.0), (0.0, 0.112, 0.0), 0.012, RED, r2=0.004, segments=18)
    m.tube((0.0, -0.034, 0.0), (0.0, 0.020, 0.0), 0.0228, RED, segments=26)
    m.tube((0.0, -0.022, 0.0), (0.0, 0.008, 0.0), 0.0235, YELLOW, segments=26)
    return m


def laser():
    """A pen-sized laser pointer, lens along +Y. (0, 0, 0) is where the thumb rests."""
    m = Model('laser')
    m.tube((0.0, -0.090, 0.0), (0.0, 0.075, 0.0), 0.013, DARK, segments=22)
    m.tube((0.0, 0.075, 0.0), (0.0, 0.094, 0.0), 0.0145, SILVER, segments=22)
    m.tube((0.0, 0.092, 0.0), (0.0, 0.098, 0.0), 0.010, RED, segments=18)
    m.tube((0.0, -0.100, 0.0), (0.0, -0.090, 0.0), 0.0145, SILVER, segments=22)
    m.tube((0.0, -0.016, 0.0), (0.0, -0.002, 0.0), 0.0138, SILVER, segments=22)
    m.box((0.0, 0.030, 0.0140), (0.012, 0.028, 0.006), RED, 0.002)
    m.ball((0.0, -0.112, 0.0), (0.009, 0.012, 0.009), SILVER)
    return m


def cushion():
    """A whoopee cushion lying flat, mouthpiece along +Y. (0, 0, 0) is the floor under its middle."""
    m = Model('cushion')
    m.ball((0.0, 0.0, 0.024), (0.115, 0.115, 0.024), PINK)
    m.ball((0.0, 0.0, 0.022), (0.123, 0.123, 0.009), PINK_DARK)
    m.tube((0.0, 0.104, 0.024), (0.0, 0.168, 0.024), 0.019, PINK, r2=0.012, segments=18)
    m.tube((0.0, 0.162, 0.024), (0.0, 0.176, 0.024), 0.016, PINK_DARK, segments=18)
    m.ball((-0.034, -0.030, 0.043), (0.022, 0.030, 0.006), (250, 150, 178))
    return m


def note():
    """A folded note. (0, 0, 0) is its middle."""
    m = Model('note')
    m.box((0.0, 0.0, 0.0), (0.100, 0.070, 0.009), PAPER, 0.002)
    m.sheet([(-0.050, 0.035, 0.0052), (0.050, 0.035, 0.0052), (0.0, -0.006, 0.0086)], PAPER_FOLD, 0.002)
    for i in range(3):
        m.box((0.0, -0.014 - i * 0.008, 0.0050), (0.062 - i * 0.012, 0.0024, 0.0014), INK)
    m.ball((0.0, 0.004, 0.0088), (0.007, 0.007, 0.0024), RED)
    return m


def puddle():
    """A pool of spilled glue. (0, 0, 0) is the floor under its middle."""
    m = Model('puddle')
    m.slab(blob(0.210, 0.16, 5, phase=0.4), 0.0, 0.010, CREAM, 0.003)
    m.slab(blob(0.125, 0.20, 4, phase=1.3), 0.008, 0.016, CREAM_LIGHT, 0.003)
    for x, y, r in ((0.215, 0.130, 0.036), (-0.200, -0.140, 0.030), (0.050, -0.250, 0.028)):
        m.ball((x, y, 0.004), (r, r, 0.008), CREAM)
    return m


def splat():
    """The wet patch a water balloon leaves. Flat, facing +Z; (0, 0, 0) is its middle."""
    m = Model('splat')
    m.slab(blob(0.300, 0.30, 7, steps=70, phase=0.9), 0.0, 0.008, WATER)
    m.slab(blob(0.170, 0.22, 5, phase=2.0), 0.006, 0.013, WATER_LIGHT)
    for x, y, r in ((0.330, 0.180, 0.030), (-0.340, 0.060, 0.026), (-0.120, -0.360, 0.034), (0.210, -0.310, 0.022)):
        m.ball((x, y, 0.004), (r, r, 0.007), WATER)
    return m


# ---------------------------------------------------------------------------


def export(model, path):
    """Triangulate, smooth normals by angle, merge identical vertices, convert to Unity axes."""
    groups = {}
    for bm, color in model.parts:
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bm.normal_update()
        cos_limit = math.cos(SMOOTH_ANGLE)
        verts, index, tris = groups.setdefault(color, ([], {}, []))
        for face in bm.faces:
            corner = []
            for loop in face.loops:
                normal = Vector((0.0, 0.0, 0.0))
                for other in loop.vert.link_faces:
                    if other.normal.dot(face.normal) >= cos_limit:
                        normal += other.normal * other.calc_area()
                if normal.length < 1e-9:
                    normal = face.normal.copy()
                normal.normalize()
                co = loop.vert.co
                # Blender (x, y, z) with z up  ->  Unity (x, z, y) with y up.
                key = (round(co.x, 5), round(co.z, 5), round(co.y, 5),
                       round(normal.x, 3), round(normal.z, 3), round(normal.y, 3))
                if key not in index:
                    index[key] = len(verts)
                    verts.append(key)
                corner.append(index[key])
            # The axis swap mirrors the model, so the winding is reversed to keep faces outward.
            tris.append((corner[0], corner[2], corner[1]))

    lines = ['# classpack model: %s' % model.name]
    base = 0
    total_tris = 0
    for color, (verts, _, tris) in groups.items():
        lines.append('m %d %d %d' % color)
        for v in verts:
            lines.append('v %.5f %.5f %.5f %.3f %.3f %.3f' % v)
        for a, b, c in tris:
            lines.append('t %d %d %d' % (a + base, b + base, c + base))
        base += len(verts)
        total_tris += len(tris)
    with open(path, 'w', encoding='ascii', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    return base, total_tris


def preview(model, path):
    """Renders the model so it can be looked at before it goes into the game."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    materials = {}
    low = Vector((1e9, 1e9, 1e9))
    high = Vector((-1e9, -1e9, -1e9))
    for i, (bm, color) in enumerate(model.parts):
        mesh = bpy.data.meshes.new('%s_%d' % (model.name, i))
        bm.to_mesh(mesh)
        for polygon in mesh.polygons:
            polygon.use_smooth = False
        if color not in materials:
            material = bpy.data.materials.new('c_%d_%d_%d' % color)
            material.diffuse_color = (color[0] / 255.0, color[1] / 255.0, color[2] / 255.0, 1.0)
            materials[color] = material
        mesh.materials.append(materials[color])
        obj = bpy.data.objects.new(mesh.name, mesh)
        scene.collection.objects.link(obj)
        for v in mesh.vertices:
            low = Vector((min(low.x, v.co.x), min(low.y, v.co.y), min(low.z, v.co.z)))
            high = Vector((max(high.x, v.co.x), max(high.y, v.co.y), max(high.z, v.co.z)))

    centre = (low + high) * 0.5
    size = max((high - low).length, 0.1)
    camera_data = bpy.data.cameras.new('cam')
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = size * 1.05
    camera = bpy.data.objects.new('cam', camera_data)
    scene.collection.objects.link(camera)
    direction = Vector((1.0, -0.55, 0.45)).normalized()
    camera.location = centre + direction * 3.0
    camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera

    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 620
    scene.render.film_transparent = False
    scene.render.filepath = path
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_cavity = True
    shading.show_shadows = True
    shading.show_object_outline = True
    scene.world = bpy.data.worlds.new('w')
    scene.world.color = (0.82, 0.85, 0.9)
    bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    # Absolute paths: Blender resolves a relative render path against the drive root.
    out_dir = os.path.abspath(args[0])
    preview_dir = os.path.abspath(args[1]) if len(args) > 1 else None
    os.makedirs(out_dir, exist_ok=True)
    for build in (pistol, shotgun, sniper, rpg, airplane, balloon, glue, laser, cushion, note, puddle, splat):
        model = build()
        if preview_dir:
            os.makedirs(preview_dir, exist_ok=True)
            try:
                preview(build(), os.path.join(preview_dir, model.name + '.png'))
            except Exception as error:  # a failed preview must not stop the export
                print('preview failed for %s: %s' % (model.name, error))
        verts, tris = export(model, os.path.join(out_dir, model.name + '.cpm'))
        print('wrote %s: %d vertices, %d triangles' % (model.name, verts, tris))


main()
