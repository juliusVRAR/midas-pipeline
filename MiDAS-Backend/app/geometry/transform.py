import numpy as np

from app.schemas import Intrinsics


def validate_T_world_cam(T: np.ndarray) -> None:
    if T.shape != (4, 4):
        raise ValueError("T_world_cam must be 4x4")
    
def unity_to_opencv_extrinsics(T: np.ndarray) -> np.ndarray:
    T[1, 1] = -T[1, 1]
    return np.linalg.inv(T)

def unity_to_opencv_intrinsics(intrinsics: Intrinsics) -> np.ndarray:
    return np.asarray([
        intrinsics.fx, 0., intrinsics.cx,
        0., -intrinsics.fy, 1120. - intrinsics.cx,
        0, 0, 1
    ]).reshape(3, 3)


def cam_to_world_points(points_cam: np.ndarray, T_world_cam: np.ndarray) -> np.ndarray:
    """
    points_cam: Nx3 in camera coords
    T_world_cam: 4x4 camera->world
    """
    N = points_cam.shape[0]
    pts_h = np.hstack([points_cam.astype(np.float32), np.ones((N, 1), np.float32)])
    pts_w = (T_world_cam.astype(np.float32) @ pts_h.T).T
    return pts_w[:, :3]
