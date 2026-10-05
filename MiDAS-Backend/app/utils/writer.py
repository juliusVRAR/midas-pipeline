"""
Writer helper utilities.
"""

from pathlib import Path
import numpy as np
import cv2 as cv


def _write_bop_mask(mask: np.ndarray, output_path: Path, height: int, width: int) -> None:
    """Write a single-channel BOP mask_visib PNG using 0 and 255."""
    mask_array = np.asarray(mask)

    if mask_array.shape != (height, width):
        raise ValueError(
            f"Mask shape {mask_array.shape} does not match RGB shape {(height, width)}"
        )

    binary_mask = np.where(mask_array.astype(bool), 255, 0).astype(np.uint8)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    success = cv.imwrite(str(output_path), binary_mask)
    if not success:
        raise IOError(f"Failed to write BOP mask: {output_path}")


def _write_empty_bop_mask(output_path: Path, height: int, width: int) -> None:
    """Write an all-zero fallback mask for skipped or failed segmentation."""
    _write_bop_mask(
        np.zeros((height, width), dtype=bool),
        output_path,
        height,
        width,
    )