import os
from typing import List, Optional, Sequence, Tuple
import numpy as np
import torch
from app.schemas import RequestMeta
from PIL import Image
from sam3.model.sam3_image import Sam3Image
from sam3.model_builder import build_sam3_image_model
from sam3.model.sam3_image_processor import Sam3Processor

def load_sam3(device: str):
    ckpt = os.getenv("SAM3_CHECKPOINT_PATH", "").strip()
    model = build_sam3_image_model(checkpoint_path=ckpt, enable_inst_interactivity=True)
    predictor = Sam3Processor(model)
    return model, predictor


def _to_numpy(value) -> np.ndarray:
    """Convert tensor-like SAM3 output to NumPy without dropping dimensions."""
    if torch.is_tensor(value):
        return value.detach().cpu().numpy()
    return np.asarray(value)


def _normalize_batched_masks(masks, expected_count: int) -> np.ndarray:
    """
    Normalize SAM3 masks to shape (N, H, W).

    Expected common shapes:
    - (N, 1, H, W)
    - (N, H, W)
    - (1, H, W) for one prompt
    - (H, W) for one prompt
    """
    array = _to_numpy(masks)

    if array.ndim == 4:
        # Expected: N, masks_per_box, H, W.
        if array.shape[1] != 1:
            raise ValueError(
                f"Expected one mask per box, got mask shape {array.shape}"
            )
        array = array[:, 0, :, :]

    elif array.ndim == 3:
        # Expected: N, H, W.
        pass

    elif array.ndim == 2:
        # Valid only for a single prompt.
        if expected_count != 1:
            raise ValueError(
                f"Expected {expected_count} masks, got unbatched shape {array.shape}"
            )
        array = array[np.newaxis, :, :]

    else:
        raise ValueError(f"Unexpected SAM3 mask shape: {array.shape}")

    if array.shape[0] != expected_count:
        raise ValueError(
            f"SAM3 returned {array.shape[0]} masks for {expected_count} input boxes"
        )

    return array.astype(bool, copy=False)



@torch.inference_mode()
def predict_mask(
    model: Sam3Image,
    processor: Sam3Processor,
    image: Image.Image,
    metadata: RequestMeta,
) -> np.ndarray:
    """Return exactly one HxW boolean mask for the point-prompt API."""
    inference_state = processor.set_image(image)
    point_prompt = metadata.point_prompt

    masks, _, _ = model.predict_inst(
        inference_state,
        point_coords=point_prompt.points,
        point_labels=point_prompt.labels,
        box=None,
        multimask_output=False,
    )

    normalized = _normalize_batched_masks(masks, expected_count=1)
    return normalized[0]



@torch.inference_mode()
def predict_masks_from_boxes(
    model: Sam3Image,
    processor: Sam3Processor,
    image: Image.Image,
    boxes_xyxy: Sequence[Sequence[float]],
) -> Tuple[List[np.ndarray], Optional[np.ndarray]]:
    """
    Segment all boxes in one image embedding/prediction call.

    Returns:
        - masks: list of N HxW boolean masks, in input-box order
        - scores: optional SAM3 score array
    """
    if not boxes_xyxy:
        return [], None

    input_boxes = np.asarray(boxes_xyxy, dtype=np.float32)

    if input_boxes.ndim != 2 or input_boxes.shape[1] != 4:
        raise ValueError(
            f"boxes_xyxy must have shape (N, 4), got {input_boxes.shape}"
        )

    inference_state = processor.set_image(image)

    masks, scores, _ = model.predict_inst(
        inference_state,
        point_coords=None,
        point_labels=None,
        box=input_boxes,
        multimask_output=False,
    )

    normalized_masks = _normalize_batched_masks(
        masks,
        expected_count=len(input_boxes),
    )

    normalized_scores = None
    if scores is not None:
        normalized_scores = _to_numpy(scores).reshape(-1)

    return [mask for mask in normalized_masks], normalized_scores


def bbox_from_mask(mask_bool: np.ndarray) -> Optional[List[int]]:
    """Return [x_min, y_min, x_max, y_max] or None for an empty mask."""
    ys, xs = np.where(mask_bool)
    if len(xs) == 0 or len(ys) == 0:
        return None

    return [
        int(xs.min()),
        int(ys.min()),
        int(xs.max()),
        int(ys.max()),
    ]


def pick_best_mask(masks: np.ndarray, scores: np.ndarray):
    """masks: (N,H,W), scores: (N,)"""
    best = int(np.argmax(scores))
    return masks[best], float(scores[best])