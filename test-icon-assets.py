#!/usr/bin/env python3
"""Check application icon alpha bounds and Windows-compatible DIB frames."""
import pathlib
import struct
import unittest


class IconAssetTests(unittest.TestCase):
    def test_remote_fills_height_at_every_resolution(self):
        data = (pathlib.Path(__file__).parent / 'assets/app-icon.ico').read_bytes()
        self.assertEqual(struct.unpack_from('<HHH', data), (0, 1, 7))
        for i, size in enumerate((16, 24, 32, 48, 64, 128, 256)):
            _, _, _, _, planes, depth, length, offset = struct.unpack_from('<BBBBHHII', data, 6 + i * 16)
            frame = data[offset:offset + length]
            self.assertEqual(struct.unpack_from('<IiiHH', frame), (40, size, size * 2, 1, 32))
            self.assertEqual((planes, depth), (1, 32))
            rows = [y for y in range(size) if any(frame[40 + (y * size + x) * 4 + 3] for x in range(size))]
            self.assertGreaterEqual(len(rows), size - 1, f'{size}px drawing is too small')
            self.assertLessEqual(min(rows), 1)
            self.assertGreaterEqual(max(rows), size - 2)
            for y in (0, size - 1):
                for x in (0, size - 1):
                    self.assertEqual(frame[40 + (y * size + x) * 4 + 3], 0, f'{size}px corner must be transparent')


if __name__ == '__main__':
    unittest.main()
