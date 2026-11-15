"""Minimal UnityFS reader: decompress a bundle and pull TextAssets out by name.

usage: python -I unbundle.py <bundle.unity3d> <out_dir> [name1 name2 ...]
With no names it only writes the raw decompressed data file(s).
"""
import lzma
import os
import struct
import sys


def lz4_block(src: bytes, out_size: int) -> bytes:
    dst = bytearray()
    i = 0
    n = len(src)
    while i < n:
        token = src[i]
        i += 1
        lit = token >> 4
        if lit == 15:
            while True:
                b = src[i]
                i += 1
                lit += b
                if b != 255:
                    break
        dst += src[i:i + lit]
        i += lit
        if i >= n:
            break
        offset = src[i] | (src[i + 1] << 8)
        i += 2
        mlen = token & 15
        if mlen == 15:
            while True:
                b = src[i]
                i += 1
                mlen += b
                if b != 255:
                    break
        mlen += 4
        start = len(dst) - offset
        for k in range(mlen):
            dst.append(dst[start + k])
    assert len(dst) == out_size, (len(dst), out_size)
    return bytes(dst)


def decompress(data: bytes, out_size: int, kind: int) -> bytes:
    if kind == 0:
        return data
    if kind == 1:
        props = data[0]
        lc = props % 9
        rest = props // 9
        lp = rest % 5
        pb = rest // 5
        dict_size = struct.unpack('<I', data[1:5])[0]
        dec = lzma.LZMADecompressor(lzma.FORMAT_RAW, filters=[{
            'id': lzma.FILTER_LZMA1, 'dict_size': dict_size, 'lc': lc, 'lp': lp, 'pb': pb}])
        return dec.decompress(data[5:], out_size)
    if kind in (2, 3):
        return lz4_block(data, out_size)
    raise ValueError('compression %d' % kind)


def cstr(buf: bytes, pos: int):
    end = buf.index(b'\0', pos)
    return buf[pos:end].decode('utf-8', 'replace'), end + 1


def read_bundle(path: str):
    with open(path, 'rb') as f:
        raw = f.read()
    sig, pos = cstr(raw, 0)
    assert sig == 'UnityFS', sig
    version = struct.unpack('>I', raw[pos:pos + 4])[0]
    pos += 4
    _, pos = cstr(raw, pos)
    _, pos = cstr(raw, pos)
    size, csize, usize, flags = struct.unpack('>qIII', raw[pos:pos + 20])
    pos += 20
    if version >= 7:
        pos = (pos + 15) & ~15
    if flags & 0x80:
        info_raw = raw[len(raw) - csize:]
    else:
        info_raw = raw[pos:pos + csize]
        pos += csize
    info = decompress(info_raw, usize, flags & 0x3F)
    p = 16
    block_count = struct.unpack('>i', info[p:p + 4])[0]
    p += 4
    blocks = []
    for _ in range(block_count):
        u, c, fl = struct.unpack('>IIH', info[p:p + 10])
        p += 10
        blocks.append((u, c, fl))
    node_count = struct.unpack('>i', info[p:p + 4])[0]
    p += 4
    nodes = []
    for _ in range(node_count):
        off, sz, fl = struct.unpack('>qqI', info[p:p + 20])
        p += 20
        name, p = cstr(info, p)
        nodes.append((off, sz, name))
    if flags & 0x200:
        pos = (pos + 15) & ~15
    out = bytearray()
    for u, c, fl in blocks:
        out += decompress(raw[pos:pos + c], u, fl & 0x3F)
        pos += c
    return bytes(out), nodes


def find_text_asset(data: bytes, name: str):
    """TextAsset layout: int32 len + name + pad4, then int32 len + script bytes."""
    key = struct.pack('<I', len(name)) + name.encode()
    best = None
    start = 0
    while True:
        i = data.find(key, start)
        if i < 0:
            break
        p = i + len(key)
        p = (p + 3) & ~3
        ln = struct.unpack('<I', data[p:p + 4])[0]
        body = data[p + 4:p + 4 + ln]
        if 0 < ln < len(data) and body[:1] in (b'[', b'{') and (best is None or ln > len(best)):
            best = body
        start = i + 1
    return best


def main():
    bundle, out_dir = sys.argv[1], sys.argv[2]
    names = sys.argv[3:]
    os.makedirs(out_dir, exist_ok=True)
    data, nodes = read_bundle(bundle)
    print('decompressed', len(data), 'bytes; nodes:', [(n, s) for _, s, n in nodes])
    if not names:
        for off, sz, name in nodes:
            with open(os.path.join(out_dir, os.path.basename(name)), 'wb') as f:
                f.write(data[off:off + sz])
        return
    for name in names:
        body = find_text_asset(data, name)
        if body is None:
            print('NOT FOUND', name)
            continue
        with open(os.path.join(out_dir, name + '.json'), 'wb') as f:
            f.write(body)
        print('wrote', name, len(body))


if __name__ == '__main__':
    main()
