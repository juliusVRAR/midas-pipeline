"""
Coordinate transformation and pose computation utilities.
"""

import numpy as np
from typing import List, Optional, Tuple, TYPE_CHECKING

if TYPE_CHECKING:
    from app.schemas import Intrinsics, ObjectTransform


def quaternion_to_rotation_matrix(q: List[float]) -> np.ndarray:
    """
    Convert quaternion [qx, qy, qz, qw] to 3x3 rotation matrix.
    """
    qx, qy, qz, qw = q

    xx = qx * qx
    yy = qy * qy
    zz = qz * qz
    xy = qx * qy
    xz = qx * qz
    yz = qy * qz
    wx = qw * qx
    wy = qw * qy
    wz = qw * qz

    return np.array([
        [1 - 2*(yy+zz),     2*(xy-wz),     2*(xz+wy)],
        [    2*(xy+wz), 1 - 2*(xx+zz),     2*(yz-wx)],
        [    2*(xz-wy),     2*(yz+wx), 1 - 2*(xx+yy)]
    ], dtype=np.float32)


def pose_to_matrix(
    position: List[float],
    rotation: List[float],
    scale: List[float] = None
) -> np.ndarray:
    """
    Create 4x4 transformation matrix from position, rotation (quaternion), and optional scale.
    """
    R = quaternion_to_rotation_matrix(rotation)

    if scale is not None:
        S = np.diag(scale)
        R = R @ S

    T = np.eye(4, dtype=np.float32)
    T[:3, :3] = R
    T[:3, 3] = position

    return T


def calc_sensor_crop_region(intrinsics: "Intrinsics") -> Tuple[float, float, float, float]:
    """
    Replicate Unity's CalcSensorCropRegion() from PassthroughCameraAccess.
    Returns (x, y, width, height) in sensor pixel coordinates.
    """
    sensor_w = intrinsics.sensor_width
    sensor_h = intrinsics.sensor_height
    current_w = intrinsics.width
    current_h = intrinsics.height

    scale_x = current_w / sensor_w
    scale_y = current_h / sensor_h

    max_scale = max(scale_x, scale_y)
    scale_x /= max_scale
    scale_y /= max_scale

    crop_x = sensor_w * (1 - scale_x) * 0.5
    crop_y = sensor_h * (1 - scale_y) * 0.5
    crop_w = sensor_w * scale_x
    crop_h = sensor_h * scale_y

    return crop_x, crop_y, crop_w, crop_h


def world_to_viewport(
    world_pos: np.ndarray,
    camera_pose: List[float],
    intrinsics: "Intrinsics"
) -> np.ndarray:
    """
    Replicate Unity's WorldToViewportPoint from PassthroughCameraAccess.

    Args:
        world_pos: (N, 3) array of world positions
        camera_pose: [px, py, pz, qx, qy, qz, qw]
        intrinsics: Camera intrinsics

    Returns:
        (N, 2) array of viewport coordinates (0-1 range)
    """
    cam_pos = np.array(camera_pose[:3], dtype=np.float32)
    cam_rot = camera_pose[3:7]

    R_cam = quaternion_to_rotation_matrix(cam_rot)
    R_cam_inv = R_cam.T

    local = (world_pos - cam_pos) @ R_cam_inv.T

    fx, fy = intrinsics.fx, intrinsics.fy
    cx, cy = intrinsics.cx, intrinsics.cy

    sensor_x = local[:, 0] / local[:, 2] * fx + cx
    sensor_y = local[:, 1] / local[:, 2] * fy + cy

    crop_x, crop_y, crop_w, crop_h = calc_sensor_crop_region(intrinsics)

    viewport_x = (sensor_x - crop_x) / crop_w
    viewport_y = (sensor_y - crop_y) / crop_h

    return np.stack([viewport_x, viewport_y], axis=-1)


def viewport_to_pixels(
    viewport_coords: list,
    width: int,
    height: int
) -> np.ndarray:
    """Convert viewport coordinates (0-1) to pixel coordinates."""
    pixels = []
    for coord in viewport_coords:
        x = coord[0] * width
        y = (1 - coord[1]) * height  # Flip Y axis
        pixels.append([x, y])
    return np.array(pixels, dtype=np.int32)


def compute_object_pose_in_camera(
    obj_transform: "ObjectTransform",
    camera_pose: List[float]
) -> Tuple[np.ndarray, np.ndarray]:
    """
    Compute object rotation matrix and translation in camera space (OpenCV convention).

    Returns:
        R: 3x3 rotation matrix (cam <- obj)
        t: 3D translation vector in camera space [tx, ty, tz]
    """
    cam_pos = camera_pose[:3]
    cam_rot = camera_pose[3:7]

    cam_world_matrix = pose_to_matrix(cam_pos, cam_rot)
    world_to_cam = np.linalg.inv(cam_world_matrix)

    unity_to_opencv = np.diag([1, -1, 1, 1]).astype(np.float32)
    world_to_cam_cv = unity_to_opencv @ world_to_cam

    obj_world_matrix = pose_to_matrix(
        obj_transform.position,
        obj_transform.rotation
    )

    obj_in_cam = world_to_cam_cv @ obj_world_matrix

    R = obj_in_cam[:3, :3]
    t = obj_in_cam[:3, 3]

    return R, t


def transform_vertices_to_camera(
    vertices_world: np.ndarray,
    camera_pose: List[float]
) -> np.ndarray:
    """
    Transform 3D vertices from world space to camera space (OpenCV convention).

    Args:
        vertices_world: (N, 3) array of world positions
        camera_pose: [px, py, pz, qx, qy, qz, qw]

    Returns:
        (N, 3) array of camera-space positions
    """
    cam_pos = camera_pose[:3]
    cam_rot = camera_pose[3:7]

    cam_world_matrix = pose_to_matrix(cam_pos, cam_rot)
    world_to_cam = np.linalg.inv(cam_world_matrix)

    unity_to_opencv = np.diag([1, -1, 1, 1]).astype(np.float32)
    world_to_cam_cv = unity_to_opencv @ world_to_cam

    ones = np.ones((vertices_world.shape[0], 1), dtype=np.float32)
    vertices_homo = np.hstack([vertices_world, ones])

    vertices_cam = (world_to_cam_cv @ vertices_homo.T).T

    return vertices_cam[:, :3]

BOP_MASK_PADDING_PX = 4

def _projected_vertices_to_xyxy(
    bbox_2d,
    image_width: int,
    image_height: int,
    padding: int = BOP_MASK_PADDING_PX,
) -> Optional[list[int]]:
    """
    Convert viewport-space projected 3D-box vertices to a clipped XYXY SAM3 box.

    Returns None for missing, degenerate, or fully off-image boxes.
    """
    if not bbox_2d:
        return None

    vertices = np.asarray(bbox_2d, dtype=np.float32)
    if vertices.ndim != 2 or vertices.shape[0] == 0 or vertices.shape[1] < 2:
        return None

    pixels = np.asarray(
        viewport_to_pixels(vertices.tolist(), image_width, image_height),
        dtype=np.float32,
    )

    if pixels.ndim != 2 or pixels.shape[0] == 0 or pixels.shape[1] < 2:
        return None

    raw_x1 = float(np.min(pixels[:, 0]))
    raw_y1 = float(np.min(pixels[:, 1]))
    raw_x2 = float(np.max(pixels[:, 0]))
    raw_y2 = float(np.max(pixels[:, 1]))

    # Entirely outside before clipping.
    if (
        raw_x2 < 0
        or raw_y2 < 0
        or raw_x1 >= image_width
        or raw_y1 >= image_height
    ):
        return None

    x1 = max(0, int(np.floor(raw_x1)) - padding)
    y1 = max(0, int(np.floor(raw_y1)) - padding)
    x2 = min(image_width - 1, int(np.ceil(raw_x2)) + padding)
    y2 = min(image_height - 1, int(np.ceil(raw_y2)) + padding)

    if x2 <= x1 or y2 <= y1:
        return None

    return [x1, y1, x2, y2]