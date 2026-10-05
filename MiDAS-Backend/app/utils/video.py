"""
Video encoding utilities.
"""

import subprocess
from pathlib import Path


def encode_video_from_frames(
    frames_dir: Path,
    fps: int,
    pattern: str = "%06d.jpg",
    output_name: str = "video.mp4",
    output_dir: Path | None = None,
):
    """
    Encode frames to MP4 video using ffmpeg.
    
    Args:
        frames_dir: Directory containing frame images
        fps: Frames per second
        pattern: Frame filename pattern (printf style)
        output_name: Output video filename
        output_dir: Directory for the output video; defaults to frames_dir
    """
    out_dir = output_dir or frames_dir
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / output_name
    cmd = [
        "ffmpeg",
        "-y",
        "-loglevel", "error",
        "-framerate", str(fps),
        "-i", str(frames_dir / pattern),
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        str(out_path),
    ]
    subprocess.run(cmd, check=True)
