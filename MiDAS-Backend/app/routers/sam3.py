import base64
import io
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional
import zipfile
import threading

from fastapi.responses import StreamingResponse
import numpy as np
from fastapi import APIRouter, File, Form, HTTPException, UploadFile
from PIL import Image

from app.models.sam3.sam3_loader import bbox_from_mask, load_sam3, predict_mask, predict_masks_from_boxes
from app.schemas import RequestMeta
from app.utils.drawing import draw_bbox
from app.utils.image import save_image


# ---------------------------------------------------------------------------
# State Management
# ---------------------------------------------------------------------------
@dataclass
class SAM3State:
    """Holds SAM3 model and processor instances."""
    device: str = "cpu"
    processor: Optional[object] = field(default=None)
    model: Optional[object] = field(default=None)

    @property
    def is_loaded(self) -> bool:
        return self.model is not None and self.processor is not None


sam3_state = SAM3State()
sam3_inference_lock = threading.Lock()


def init_sam3(device: str) -> None:
    """Load SAM3 model and processor into module state."""
    sam3_state.device = device
    print("[INIT] Loading SAM3...")
    sam3_state.model, sam3_state.processor = load_sam3(device)
    print("[INIT] SAM3 loaded")


def get_sam3_status() -> dict:
    """Return SAM3 status for health checks."""
    return {
        "sam3_loaded": sam3_state.is_loaded,
        "sam3_device": sam3_state.device,
    }


# ---------------------------------------------------------------------------
# Router
# ---------------------------------------------------------------------------
router = APIRouter(prefix="/sam3", tags=["SAM3"])

SAVE_DIR = Path("/data/sam3_masks")
SAVE_DIR.mkdir(parents=True, exist_ok=True)
img_counter = 0

@router.post("/segment")
async def segment(
    image: UploadFile = File(...),
    payload: str = Form(...),
):
    """
    Segment an image using SAM3 with point/box prompts.

    - **image**: Uploaded image file
    - **payload**: JSON string of RequestMeta
    """
    if not sam3_state.is_loaded:
        raise HTTPException(status_code=503, detail="SAM3 model not loaded")

    # Parse payload
    try:
        metadata = RequestMeta.model_validate_json(payload)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid payload JSON: {e}")

    # Load image
    data = await image.read()
    pil_image = Image.open(io.BytesIO(data)).convert("RGB")

    # Predict mask
    with sam3_inference_lock:
        mask = predict_mask(
            sam3_state.model,
            sam3_state.processor,
            pil_image,
            metadata,
        )

    # Derive bounding box from mask
    mask_bool = mask.astype(bool)
    bbox = bbox_from_mask(mask_bool)
    
    # Build outputs (raw bytes — no redundant encode/decode)
    mask_bytes      = mask_to_png(mask)
    annotated_bytes = draw_bbox(pil_image, mask_bool, bbox)

    # Persist both files
    global img_counter
    (SAVE_DIR / f"annotated_{img_counter:05d}.png").write_bytes(annotated_bytes)
    img_counter += 1
    print(f"[SEGMENT] Saved mask + annotated #{img_counter:05d} → {SAVE_DIR}")


    return {
        "ok": True,
        "mask_png": base64.b64encode(mask_bytes).decode("utf-8"),
    }


@router.get("/download-masks")
async def download_masks():
    """Download all saved mask images as a single ZIP archive."""
    mask_files = sorted(SAVE_DIR.glob("*.png"))

    if not mask_files:
        raise HTTPException(status_code=404, detail="No saved masks found")

    zip_buffer = io.BytesIO()
    with zipfile.ZipFile(zip_buffer, mode="w", compression=zipfile.ZIP_DEFLATED) as zf:
        for mask_path in mask_files:
            zf.write(mask_path, arcname=mask_path.name)

    zip_buffer.seek(0)

    return StreamingResponse(
        zip_buffer,
        media_type="application/zip",
        headers={"Content-Disposition": "attachment; filename=masks.zip"},
    )

# ---------------------------------------------------------------------------
# Services
# ---------------------------------------------------------------------------

def segment_boxes_in_image(
    image: Image.Image,
    boxes_xyxy: list[list[float]],
) -> list[np.ndarray]:
    """
    Internal SAM3 service for BOP export.

    The masks are returned in exactly the same order as boxes_xyxy.
    """
    if not sam3_state.is_loaded:
        raise RuntimeError("SAM3 image model is not loaded")

    if not boxes_xyxy:
        return []

    rgb_image = image.convert("RGB")

    with sam3_inference_lock:
        masks, _scores = predict_masks_from_boxes(
            sam3_state.model,
            sam3_state.processor,
            rgb_image,
            boxes_xyxy,
        )

    return masks

# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------
def mask_to_png(mask: np.ndarray) -> bytes:
    """
    Convert a boolean/binary mask to a base64-encoded RGBA PNG.

    Args:
        mask: HxW array (bool or 0/1)

    Returns:
        Base64-encoded PNG string (white pixels with alpha = mask)
    """
    if mask.dtype != np.uint8:
        mask_u8 = mask.astype(np.uint8) * 255
    else:
        mask_u8 = mask

    h, w = mask_u8.shape
    rgba = np.zeros((h, w, 4), dtype=np.uint8)

    # White RGB where mask is present
    rgba[..., :3] = 255

    # Alpha channel = mask intensity
    rgba[..., 3] = mask_u8

    buf = io.BytesIO()
    Image.fromarray(rgba, mode="RGBA").save(buf, format="PNG")
    return buf.getvalue()