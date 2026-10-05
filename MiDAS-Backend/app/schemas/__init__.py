"""Schema re-exports."""

from .camera import Intrinsics, Extrinsics
from .websocket import (
    StartMessage,
    StopMessage,
    ModelBeginMessage,
    ModelCompleteMessage,
)
from .prompts import PointPrompt, BoxPrompt, RequestMeta
from .bop import (
    ObjectTransform,
    ObjectPoseData,
    FrameHeaderLightweight,
    FrameHeaderBOP,
    FrameHeaderLegacy,
)
from .session import (
    SessionData,
    BOPFrameData,
    ModelAssetData,
)

__all__ = [
    # camera
    "Intrinsics",
    "Extrinsics",
    # websocket
    "StartMessage",
    "StopMessage",
    "ModelBeginMessage",
    "ModelCompleteMessage",
    # prompts (SAM3)
    "PointPrompt",
    "BoxPrompt",
    "RequestMeta",
    # bop
    "ObjectTransform",
    "ObjectPoseData",
    "FrameHeaderLightweight",
    "FrameHeaderBOP",
    "FrameHeaderLegacy",
    # session
    "BOPFrameData",
    "SessionData",
    "ModelAssetData",
]