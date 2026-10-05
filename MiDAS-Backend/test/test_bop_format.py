"""Checks for BOP model metadata and pose conversion."""

import unittest
import json
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from tempfile import TemporaryDirectory

import numpy as np

# This pure helper can be tested without importing app.utils' OpenCV/FastAPI helpers.
module_path = Path(__file__).resolve().parents[1] / "app" / "utils" / "bop_format.py"
spec = spec_from_file_location("bop_format", module_path)
bop_format = module_from_spec(spec)
spec.loader.exec_module(bop_format)
bop_object_pose = bop_format.bop_object_pose
model_info_from_vertices = bop_format.model_info_from_vertices
scene_dir = bop_format.scene_dir
validate_dataset_layout = bop_format.validate_dataset_layout


class BopFormatTests(unittest.TestCase):
    def test_one_capture_uses_one_training_scene(self):
        self.assertEqual(scene_dir(Path("capture")), Path("capture/train/000000"))

    def test_model_info_uses_millimetre_bounds_and_exact_diameter(self):
        vertices = np.array([[-1, -2, -3], [1, 2, 3], [0, 0, 0]], dtype=float)
        info = model_info_from_vertices(vertices)
        self.assertEqual(info["min_x"], -1)
        self.assertEqual(info["size_y"], 4)
        self.assertAlmostEqual(info["diameter"], np.sqrt(56))

    def test_centered_right_handed_model_preserves_camera_projection(self):
        old_rotation = np.diag([1.0, -1.0, 1.0])
        old_translation = np.array([0.0, 0.0, 2.0])
        model_center = np.array([0.0, 1.0, 0.0])
        old_vertex_m = np.array([1.0, 2.0, 3.0])

        rotation, translation_mm = bop_object_pose(
            old_rotation, old_translation, model_center
        )
        rotation = np.asarray(rotation).reshape(3, 3)
        centered_vertex_m = np.array([1.0, -2.0, 3.0]) - model_center

        self.assertAlmostEqual(np.linalg.det(rotation), 1.0)
        np.testing.assert_allclose(
            rotation @ centered_vertex_m + np.asarray(translation_mm) / 1000.0,
            old_rotation @ old_vertex_m + old_translation,
        )

    def test_dataset_layout_requires_aligned_scene_files(self):
        with TemporaryDirectory() as temporary:
            dataset = Path(temporary) / "capture"
            scene = scene_dir(dataset)
            (dataset / "models").mkdir(parents=True)
            for folder in ("rgb", "annotated_rgb", "mask_visib"):
                (scene / folder).mkdir(parents=True)

            (dataset / "camera.json").write_text("{}", encoding="utf-8")
            (dataset / "models" / "obj_000001.ply").write_text("ply")
            (dataset / "models" / "models_info.json").write_text(
                json.dumps({"1": {"diameter": 1}}), encoding="utf-8"
            )
            for name, content in {
                "scene_camera.json": {"0": {}},
                "scene_gt.json": {"0": [{"obj_id": 1}]},
                "scene_gt_info.json": {"0": [{}]},
            }.items():
                (scene / name).write_text(json.dumps(content), encoding="utf-8")
            (scene / "rgb" / "000000.png").write_bytes(b"frame")
            (scene / "annotated_rgb" / "000000.jpg").write_bytes(b"annotated")
            mask = scene / "mask_visib" / "000000_000000.png"
            mask.write_bytes(b"mask")

            validate_dataset_layout(dataset, 1)
            mask.unlink()
            with self.assertRaisesRegex(RuntimeError, "Missing BOP visible mask"):
                validate_dataset_layout(dataset, 1)


if __name__ == "__main__":
    unittest.main()
