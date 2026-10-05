"""Session data storage schemas."""

from typing import Dict, List, Optional
import numpy as np
from pydantic import BaseModel, Field

from app.utils.transforms import pose_to_matrix
from app.utils.bop_format import MODEL_AXIS_FLIP
from .camera import Intrinsics
from .bop import ObjectPoseData


class ModelAssetData(BaseModel):
    """A GLB model received for one BOP object."""
    obj_id: int
    obj_name: str
    glb_path: str
    glb_to_unity_scale: List[float]


class BOPFrameData(BaseModel):
    """Stored data for a single frame."""
    frame_id: int
    timestamp: float
    camera_pose: List[float]
    objects: List[ObjectPoseData]
    image_path: Optional[str] = None


class SessionData(BaseModel):
    """Complete session data."""
    session_id: str
    intrinsics: Intrinsics
    fps: int
    frames: List[BOPFrameData] = Field(default_factory=list)
    models: Dict[int, ModelAssetData] = Field(default_factory=dict)
    bop_format: bool = False

    def add_frame(self, frame: BOPFrameData):
        self.frames.append(frame)

    def add_model(self, model: ModelAssetData):
        self.models[model.obj_id] = model

    def to_bop_scene_gt(self, model_centers_m: Dict[int, List[float]]) -> dict:
        scene_gt = {}

        for frame in self.frames:
            frame_key = str(frame.frame_id)
            scene_gt[frame_key] = [
                obj.to_bop_gt(model_centers_m[obj.obj_id])
                for obj in frame.objects
            ]

        return scene_gt

    def to_bop_scene_camera(self) -> dict:
        scene_camera = {}
        cam_K = self.intrinsics.to_camera_matrix()
        for frame in self.frames:
            frame_key = str(frame.frame_id)

            cam_pos = frame.camera_pose[:3]
            cam_rot = frame.camera_pose[3:7]

            cam_world_matrix = pose_to_matrix(cam_pos, cam_rot)
            world_to_cam = np.linalg.inv(cam_world_matrix)

            unity_to_opencv = np.diag([1.0, -1.0, 1.0, 1.0])
            world_to_cam_cv = unity_to_opencv @ world_to_cam

            R_w2c = world_to_cam_cv[:3, :3] @ MODEL_AXIS_FLIP
            t_w2c = world_to_cam_cv[:3, 3]

            scene_camera[frame_key] = {
                "cam_K": [float(val) for row in cam_K for val in row],
                "cam_R_w2c": [float(val) for row in R_w2c for val in row],
                "cam_t_w2c": [float(v * 1000) for v in t_w2c],
                "depth_scale": 1.0,
            }

        return scene_camera

    def to_object_info(self) -> dict:
        obj_map = {}

        for obj_id, model in self.models.items():
            obj_map[obj_id] = model.obj_name

        return obj_map
