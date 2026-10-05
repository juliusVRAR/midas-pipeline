"""SAM3 prompt schemas for segmentation attachment."""

from typing import List, Optional
from pydantic import BaseModel
from .camera import Intrinsics, Extrinsics


class PointPrompt(BaseModel):
    """Point-based prompt for SAM3."""
    image_id: Optional[int] = 1
    points: List[List[float]]  # list of [x, y] in image pixel coordinates
    labels: List[int]          # 1 = foreground, 0 = background


class BoxPrompt(BaseModel):
    """Box-based prompt for SAM3."""
    image_id: int
    box: List[float]           # [x0, y0, x1, y1] in image pixel coordinates
    multimask_output: bool = True


class RequestMeta(BaseModel):
    """Complete request metadata for SAM3 segmentation."""
    intrinsics: Intrinsics
    extrinsics: Extrinsics

    point_prompt: Optional[PointPrompt] = None
    text_prompt: Optional[str] = None
    world_point: List[float]

    # Optional metric anchor (from Quest depth ray)
    anchor_u: Optional[int] = None
    anchor_v: Optional[int] = None
    anchor_depth_m: Optional[float] = None

    # Speed/quality tradeoff
    sample_stride: int = 2