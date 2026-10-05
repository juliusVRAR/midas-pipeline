"""
Image annotation and visualization utilities.
"""

import io

from PIL import Image, ImageDraw
import cv2 as cv
import numpy as np
from typing import List, Optional, Tuple

# 3D bounding box edges (8 vertices)
BBOX_EDGES = [
    (0, 1), (1, 2), (2, 3), (3, 0),  # bottom
    (4, 5), (5, 6), (6, 7), (7, 4),  # top
    (0, 4), (1, 5), (2, 6), (3, 7)   # verticals
]

POSE_COLORS = [
    (0, 255, 0),      # Green
    (0, 0, 255),      # Red
    (255, 0, 0),      # Blue
    (0, 255, 255),    # Yellow
    (255, 0, 255),    # Magenta
    (255, 255, 0),    # Cyan
    (0, 165, 255),    # Orange
    (147, 20, 255),   # Pink
    (0, 128, 0),      # Dark Green
    (128, 0, 128),    # Purple
    (255, 144, 30),   # Dodger Blue
    (0, 215, 255),    # Gold
]

_MASK_OVERLAY_COLOR = (0, 42, 255, 145)   # semi-transparent green
_BBOX_OUTLINE_COLOR = (255, 0, 0)        # red
_BBOX_LINE_WIDTH    = 3

def get_color(index: int) -> Tuple[int, int, int]:
    """Get color from palette, cycling if index exceeds palette size."""
    return POSE_COLORS[index % len(POSE_COLORS)]


def draw_bbox_3d(
    image: np.ndarray,
    vertices: np.ndarray,
    color: Tuple[int, int, int],
    thickness: int = 2
) -> np.ndarray:
    """
    Draw 3D bounding box on image.
    
    Args:
        image: OpenCV image
        vertices: (8, 2) array of pixel coordinates
        color: BGR color tuple
        thickness: Line thickness
    
    Returns:
        Annotated image
    """
    if len(vertices) < 8:
        return image

    overlay = image.copy()

    for i, j in BBOX_EDGES:
        if i < len(vertices) and j < len(vertices):
            p1 = tuple(vertices[i].astype(int))
            p2 = tuple(vertices[j].astype(int))
            cv.line(overlay, p1, p2, color, thickness, lineType=cv.LINE_AA)

    for v in vertices:
        p = tuple(v.astype(int))
        cv.drawMarker(overlay, p, color, cv.MARKER_SQUARE, 10)

    return overlay


def draw_object_poses(
    image: np.ndarray,
    vertices_list: np.ndarray
) -> np.ndarray:
    """
    Draw multiple object poses on image.
    
    Args:
        image: OpenCV image
        vertices_list: (N, 8, 2) array of pixel coordinates for N objects
    
    Returns:
        Annotated image
    """
    overlay = image.copy()
    for idx, obj_vertices in enumerate(vertices_list):
        color = get_color(idx)
        overlay = draw_bbox_3d(overlay, obj_vertices, color)
    return overlay


def draw_label(
    image: np.ndarray,
    text: str,
    position: Tuple[int, int],
    color: Tuple[int, int, int],
    font_scale: float = 0.5,
    thickness: int = 2
) -> np.ndarray:
    """Draw text label on image."""
    cv.putText(
        image, text, position,
        cv.FONT_HERSHEY_SIMPLEX, font_scale, color, thickness
    )
    return image

def draw_bbox(
    source_image: Image.Image,
    mask_bool: np.ndarray,
    bbox: Optional[List[int]],
) -> bytes:
    """
    Composite a semi-transparent mask overlay onto *source_image* and draw
    the bounding box rectangle on top.

    Args:
        source_image: Original RGB PIL image.
        mask_bool:    HxW boolean mask.
        bbox:         [x_min, y_min, x_max, y_max] or None.

    Returns:
        Raw PNG bytes of the annotated RGB image.
    """
    # --- mask overlay (RGBA layer) ---
    mask_u8  = mask_bool.astype(np.uint8) * _MASK_OVERLAY_COLOR[3]
    overlay  = np.zeros((*mask_bool.shape, 4), dtype=np.uint8)
    overlay[mask_bool] = _MASK_OVERLAY_COLOR

    overlay_img = Image.fromarray(overlay, mode="RGBA")
    annotated   = source_image.convert("RGBA")
    annotated   = Image.alpha_composite(annotated, overlay_img).convert("RGB")

    # --- bounding box ---
    if bbox is not None:
        x0, y0, x1, y1 = bbox
        draw = ImageDraw.Draw(annotated)
        draw.rectangle(
            [x0, y0, x1, y1],
            outline=_BBOX_OUTLINE_COLOR,
            width=_BBOX_LINE_WIDTH,
        )

    buf = io.BytesIO()
    annotated.save(buf, format="PNG")
    return buf.getvalue()