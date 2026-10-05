"""Paths and geometry shared by the BOP-scenewise exporter."""

import json
from pathlib import Path

import numpy as np


SCENE_ID = "000000"
MODEL_AXIS_FLIP = np.diag([1.0, -1.0, 1.0])


def scene_dir(dataset_dir: Path) -> Path:
    """A capture is one self-contained training dataset with one scene."""
    return dataset_dir / "train" / SCENE_ID


def model_info_from_vertices(
    vertices_mm: np.ndarray, diameter_vertices_mm: np.ndarray | None = None
) -> dict[str, float]:
    """Return BOP model bounds and exact vertex-pair diameter, in millimetres."""
    vertices = np.asarray(vertices_mm, dtype=np.float64)
    if vertices.ndim != 2 or vertices.shape[1] != 3 or len(vertices) == 0:
        raise ValueError("Model vertices must have shape (N, 3) with N > 0")

    diameter_vertices = np.asarray(
        diameter_vertices_mm if diameter_vertices_mm is not None else vertices,
        dtype=np.float64,
    )
    bounds_min = vertices.min(axis=0)
    size = vertices.max(axis=0) - bounds_min

    max_squared_distance = 0.0
    for start in range(0, len(diameter_vertices), 256):
        chunk = diameter_vertices[start:start + 256]
        differences = chunk[:, None, :] - diameter_vertices[None, :, :]
        max_squared_distance = max(
            max_squared_distance,
            float(np.max(np.einsum("ijk,ijk->ij", differences, differences))),
        )

    return {
        "min_x": float(bounds_min[0]),
        "min_y": float(bounds_min[1]),
        "min_z": float(bounds_min[2]),
        "size_x": float(size[0]),
        "size_y": float(size[1]),
        "size_z": float(size[2]),
        "diameter": float(np.sqrt(max_squared_distance)),
    }


def bop_object_pose(
    rotation: np.ndarray, translation_m: np.ndarray, center_m: np.ndarray
) -> tuple[list[float], list[float]]:
    """Convert the existing Unity-local pose for a centered, right-handed PLY."""
    rotation_bop = np.asarray(rotation, dtype=np.float64) @ MODEL_AXIS_FLIP
    translation_bop = (
        np.asarray(translation_m, dtype=np.float64)
        + rotation_bop @ np.asarray(center_m, dtype=np.float64)
    ) * 1000.0
    return rotation_bop.reshape(-1).tolist(), translation_bop.tolist()


def validate_dataset_layout(dataset_dir: Path, frame_count: int) -> None:
    """Reject an incomplete scene before making its ZIP available."""
    scene = scene_dir(dataset_dir)
    required = [
        dataset_dir / "camera.json",
        dataset_dir / "models" / "models_info.json",
        scene / "scene_camera.json",
        scene / "scene_gt.json",
        scene / "scene_gt_info.json",
    ]
    for path in required:
        if not path.is_file():
            raise RuntimeError(f"Missing BOP file: {path}")

    scene_camera = json.loads((scene / "scene_camera.json").read_text(encoding="utf-8"))
    scene_gt = json.loads((scene / "scene_gt.json").read_text(encoding="utf-8"))
    scene_gt_info = json.loads((scene / "scene_gt_info.json").read_text(encoding="utf-8"))
    models_info = json.loads(
        (dataset_dir / "models" / "models_info.json").read_text(encoding="utf-8")
    )
    expected_ids = {str(frame_id) for frame_id in range(frame_count)}
    if any(set(data) != expected_ids for data in (scene_camera, scene_gt, scene_gt_info)):
        raise RuntimeError("BOP scene JSON files do not contain the same image IDs")

    for obj_id in models_info:
        model_path = dataset_dir / "models" / f"obj_{int(obj_id):06d}.ply"
        if not model_path.is_file():
            raise RuntimeError(f"Missing BOP model: {model_path}")

    for frame_id in range(frame_count):
        rgb = scene / "rgb" / f"{frame_id:06d}.png"
        annotated = scene / "annotated_rgb" / f"{frame_id:06d}.jpg"
        for path in (rgb, annotated):
            if not path.is_file():
                raise RuntimeError(f"Missing BOP frame: {path}")

        annotations = scene_gt[str(frame_id)]
        info = scene_gt_info[str(frame_id)]
        if len(annotations) != len(info):
            raise RuntimeError(f"BOP GT and mask metadata differ for frame {frame_id}")
        for gt_id, annotation in enumerate(annotations):
            if str(annotation["obj_id"]) not in models_info:
                raise RuntimeError(f"Missing model info for obj_id={annotation['obj_id']}")
            mask = scene / "mask_visib" / f"{frame_id:06d}_{gt_id:06d}.png"
            if not mask.is_file():
                raise RuntimeError(f"Missing BOP visible mask: {mask}")
