import unittest

import numpy as np
from PIL import Image

from audit_animation_edges import edge_stats


class AnimationEdgeAuditTests(unittest.TestCase):
    def test_detects_bright_and_dark_opaque_edge_candidates(self):
        data = np.zeros((9, 9, 4), dtype=np.uint8)
        data[3:6, 3:6] = [32, 32, 32, 255]
        data[2, 4] = [255, 255, 255, 255]
        bright_stats = edge_stats(Image.fromarray(data))
        self.assertGreater(bright_stats["opaqueBrightNearDark"], 0)

        data = np.zeros((9, 9, 4), dtype=np.uint8)
        data[3:6, 3:6] = [240, 240, 240, 255]
        data[2, 4] = [16, 16, 16, 255]
        dark_stats = edge_stats(Image.fromarray(data))
        self.assertGreater(dark_stats["opaqueDarkNearBright"], 0)

    def test_reports_partial_edges_and_hidden_rgb_separately(self):
        data = np.zeros((9, 9, 4), dtype=np.uint8)
        data[:, :, :3] = [255, 255, 255]
        data[3:6, 3:6] = [32, 32, 32, 255]
        data[2, 4] = [255, 255, 255, 120]
        stats = edge_stats(Image.fromarray(data))
        self.assertGreater(stats["partialBrightNearDark"], 0)
        self.assertGreater(stats["hiddenRgbPixels"], 0)


if __name__ == "__main__":
    unittest.main()
