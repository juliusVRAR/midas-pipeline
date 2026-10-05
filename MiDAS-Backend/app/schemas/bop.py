"""BOP (Benchmark for 6D Object Pose Estimation) format schemas."""

from typing import List, Optional
from pydantic import BaseModel, Field, field_validator
from app.utils.bop_format import bop_object_pose


class ObjectTransform(BaseModel):
    """Object transform in world space (lightweight input format)."""
    obj_id: int
    obj_name: str
    position: List[float]       # [x, y, z]
    rotation: List[float]       # [qx, qy, qz, qw]
    scale: List[float]          # [sx, sy, sz]
    bbox_world: Optional[List[List[float]]] = None  # World-space vertices


class ObjectPoseData(BaseModel):
    """Object pose in camera space (BOP output format)."""
    obj_id: int
    obj_name: str
    R: List[List[float]] = Field(..., min_length=3, max_length=3)
    t: List[float] = Field(..., min_length=3, max_length=3)
    bbox_2d: Optional[List[List[float]]] = None
    bbox_3d_cam: Optional[List[List[float]]] = None

    @field_validator('R')
    @classmethod
    def validate_rotation_matrix(cls, v):
        if len(v) != 3 or any(len(row) != 3 for row in v):
            raise ValueError("Rotation matrix must be 3x3")
        return v

    @field_validator('t')
    @classmethod
    def validate_translation(cls, v):
        if len(v) != 3:
            raise ValueError("Translation must have 3 components")
        return v

    def to_bop_gt(self, model_center_m: List[float]) -> dict:
        """Convert to a right-handed, centered BOP scene_gt.json entry."""
        R_flat, t_mm = bop_object_pose(self.R, self.t, model_center_m)
        return {
            "cam_R_m2c": R_flat,
            "cam_t_m2c": t_mm,
            "obj_id": self.obj_id
        }


class FrameHeaderLightweight(BaseModel):
    """Lightweight frame header (world transforms, computed on backend)."""
    frame_id: int
    timestamp: float
    camera_pose: List[float]    # [px, py, pz, qx, qy, qz, qw]
    objects: List[ObjectTransform]


class FrameHeaderBOP(BaseModel):
    """Full BOP frame header (precomputed poses)."""
    frame_id: int = Field(ge=0)
    timestamp: float
    camera_pose: List[float] = Field(..., min_length=7, max_length=7)
    objects: List[ObjectPoseData]

    @field_validator('camera_pose')
    @classmethod
    def validate_camera_pose(cls, v):
        if len(v) != 7:
            raise ValueError("Camera pose must have 7 components [px,py,pz,qx,qy,qz,qw]")
        return v


class FrameHeaderLegacy(BaseModel):
    """Legacy frame header (vertices only)."""
    poses: List[List[List[float]]]
