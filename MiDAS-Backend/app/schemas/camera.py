"""Camera-related schemas."""

from typing import List
from pydantic import BaseModel


class Intrinsics(BaseModel):
    """Camera intrinsic parameters."""
    width: int
    height: int
    sensor_width: float
    sensor_height: float
    fx: float
    fy: float
    cx: float
    cy: float

    def to_camera_matrix(self) -> List[List[float]]:
        """Convert to 3x3 camera matrix K for BOP format."""
        return [
            [self.fx, 0.0, self.cx],
            [0.0, self.fy, self.cy],
            [0.0, 0.0, 1.0]
        ]


class Extrinsics(BaseModel):
    """Camera extrinsic parameters (camera -> world transform)."""
    T_world_cam: List[List[float]]  # 4x4 matrix