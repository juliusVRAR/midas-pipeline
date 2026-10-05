"""
Image encoding, decoding, and I/O utilities.
"""

import base64
import io
from pathlib import Path

import cv2 as cv
import numpy as np
from PIL import Image


def decode_jpeg(jpeg_bytes: bytes) -> np.ndarray:
    """Decode JPEG bytes to OpenCV image (BGR)."""
    arr = np.frombuffer(jpeg_bytes, np.uint8)
    img = cv.imdecode(arr, cv.IMREAD_COLOR)
    if img is None:
        raise ValueError("Could not decode JPEG")
    return img


def cv_to_png_base64(img: np.ndarray) -> str:
    """Convert OpenCV image (BGR) to base64-encoded PNG string."""
    img_rgb = cv.cvtColor(img, cv.COLOR_BGR2RGB)
    img_pil = Image.fromarray(img_rgb)
    buf = io.BytesIO()
    img_pil.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")


def save_image(
    img: np.ndarray,
    path: Path,
    format: str = "JPEG",
    quality: int = 85
):
    """
    Save OpenCV image (BGR) to file.
    
    Args:
        img: OpenCV image in BGR format
        path: Output file path
        format: Image format ("JPEG" or "PNG")
        quality: JPEG quality (ignored for PNG)
    """
    img_rgb = cv.cvtColor(img, cv.COLOR_BGR2RGB)
    img_pil = Image.fromarray(img_rgb)
    save_kwargs = {"quality": quality} if format == "JPEG" else {}
    img_pil.save(path, format=format, **save_kwargs)