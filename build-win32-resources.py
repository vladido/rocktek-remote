#!/usr/bin/env python3
"""Pack the shared ICO and asInvoker manifest into a native Win32 .res file."""
import pathlib
import struct
import sys


def resource(kind, name, data, flags=0x1030):
    header = struct.pack('<IIHHHHIHHII', len(data), 32, 0xffff, kind,
                         0xffff, name, 0, flags, 0, 0, 0)
    return header + data + bytes((-len(data)) % 4)


def build(icon, manifest):
    reserved, kind, count = struct.unpack_from('<HHH', icon)
    if reserved != 0 or kind != 1 or count != 7:
        raise ValueError('Expected the seven-resolution application ICO')
    out = resource(0, 0, b'', 0)
    group = struct.pack('<HHH', 0, 1, count)
    for index in range(count):
        width, height, colors, reserved, planes, depth, size, offset = struct.unpack_from(
            '<BBBBHHII', icon, 6 + index * 16)
        if reserved or size < 1 or offset < 6 + count * 16 or offset + size > len(icon):
            raise ValueError('Invalid ICO directory entry')
        image = icon[offset:offset + size]
        out += resource(3, index + 1, image)
        group += struct.pack('<BBBBHHIH', width, height, colors, 0, planes,
                             depth, size, index + 1)
    out += resource(14, 1, group)
    if b'level="asInvoker"' not in manifest:
        raise ValueError('Expected a non-elevating asInvoker manifest')
    return out + resource(24, 1, manifest)


if __name__ == '__main__':
    icon, manifest, output = map(pathlib.Path, sys.argv[1:])
    output.write_bytes(build(icon.read_bytes(), manifest.read_bytes()))
