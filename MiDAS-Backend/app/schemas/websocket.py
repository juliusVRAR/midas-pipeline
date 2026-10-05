"""WebSocket message schemas."""

from typing import Literal
from pydantic import BaseModel, Field, field_validator

from .camera import Intrinsics


class StartMessage(BaseModel):
    """Start session message."""
    type: str = Field(..., pattern="^start$")
    session_id: str
    intrinsics: Intrinsics
    fps: int = Field(ge=1, le=60)
    bop_format: bool = False


class StopMessage(BaseModel):
    """Stop session message."""
    type: str = Field(..., pattern="^stop$")


class ModelBeginMessage(BaseModel):
    """Metadata sent immediately before one binary GLB payload."""
    type: Literal["model_begin"]
    obj_id: int = Field(ge=1)
    obj_name: str = Field(min_length=1, max_length=256)
    format: Literal["model/gltf-binary"] = "model/gltf-binary"
    byte_length: int = Field(gt=0, le=100_000_000)
    glb_to_unity_scale: list[float] = Field(min_length=3, max_length=3)

    @field_validator("glb_to_unity_scale")
    @classmethod
    def validate_scale(cls, value: list[float]) -> list[float]:
        if any(scale <= 0 for scale in value):
            raise ValueError("All model scale components must be positive.")

        tolerance = 1e-4
        if abs(value[0] - value[1]) > tolerance or abs(value[1] - value[2]) > tolerance:
            raise ValueError(
                "Non-uniform scale is not supported for BOP rigid-object exports."
            )

        return value


class ModelCompleteMessage(BaseModel):
    """Confirms that a model transfer is complete."""
    type: Literal["model_complete"]
    obj_id: int = Field(ge=1)