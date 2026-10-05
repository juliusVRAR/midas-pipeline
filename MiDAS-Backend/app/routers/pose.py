"""Pose estimation router - WebSocket frame capture and REST endpoints."""

import asyncio
import json
import logging
import shutil
import struct
import math
from pathlib import Path
from typing import List, Optional

from PIL import Image
import cv2 as cv
import numpy as np
from fastapi import APIRouter, File, Form, HTTPException, UploadFile, WebSocket, WebSocketDisconnect
from fastapi.responses import FileResponse
from pydantic import ValidationError

from app.routers.sam3 import segment_boxes_in_image
from app.schemas import (
    FrameHeaderLightweight,
    FrameHeaderLegacy,
    Intrinsics,
    StartMessage,
    StopMessage,
    ModelBeginMessage,
    ModelCompleteMessage,
    BOPFrameData,
    SessionData,
    ObjectPoseData,
    ModelAssetData,
)

from app.utils import (
    compute_object_pose_in_camera,
    transform_vertices_to_camera,
    world_to_viewport,
    viewport_to_pixels,
    decode_jpeg,
    cv_to_png_base64,
    save_image,
    get_color,
    draw_bbox_3d,
    draw_object_poses,
    draw_label,
    encode_video_from_frames,
    send_json,
)
from app.utils.transforms import _projected_vertices_to_xyxy
from app.utils.writer import _write_bop_mask, _write_empty_bop_mask
from app.utils.bop_format import (
    MODEL_AXIS_FLIP, model_info_from_vertices, scene_dir, validate_dataset_layout,
)

router = APIRouter(prefix="/pose")
logger = logging.getLogger("uvicorn.error")

SAVE_DIR = Path("/data/frames")
SAVE_DIR.mkdir(parents=True, exist_ok=True)


# ============== REST Endpoints ==============

@router.post("/snap")
async def get_poses(
    image: UploadFile = File(...),
    payload: str = Form(...),
):
    data = await image.read()
    arr = np.frombuffer(data, np.uint8)
    img = cv.imdecode(arr, cv.IMREAD_COLOR)

    vertices = np.asarray(
        json.loads(payload) * np.asarray([1280, 960]),
        dtype=np.int32
    )
    vertices[:, :, 1] = 960 - vertices[:, :, 1]
    annotated_img = draw_object_poses(img, vertices)

    return {
        "ok": True,
        "img_png": cv_to_png_base64(annotated_img)
    }


@router.get("/video/{session_id}")
def get_video(session_id: str):
    path = SAVE_DIR / session_id / "extras" / "video.mp4"
    if not path.exists():
        path = SAVE_DIR / session_id / "video.mp4"  # Earlier captures.
    if not path.exists():
        raise HTTPException(status_code=404, detail=f"Video not found: {path}")
    return FileResponse(path, media_type="video/mp4", filename=f"{session_id}.mp4")


@router.get("/bop/{session_id}")
def get_bop_dataset(session_id: str):
    """Download the BOP dataset as a zip file."""
    session_dir = SAVE_DIR / session_id
    if not session_dir.exists():
        raise HTTPException(status_code=404, detail=f"Session not found: {session_id}")
    if not (session_dir / "dataset_info.json").exists():
        raise HTTPException(status_code=404, detail=f"BOP export not ready: {session_id}")

    zip_path = SAVE_DIR / f"{session_id}_bop.zip"
    shutil.make_archive(str(zip_path.with_suffix('')), 'zip', SAVE_DIR, session_id)

    return FileResponse(zip_path, media_type="application/zip", filename=f"{session_id}_bop.zip")


# ============== WebSocket Endpoint ==============

@router.websocket("/video")
async def ws_frames(ws: WebSocket):
    await ws.accept()
    client = ws.client

    session: Optional[SessionData] = None
    session_dir: Optional[Path] = None
    frame_counter = 0
    finalized = False  # Guard against double finalization
    pending_model: Optional[ModelBeginMessage] = None
    stopped_cleanly = False

    await send_json(ws, {"type": "hello", "msg": "connected"})

    try:
        while True:
            msg = await ws.receive()

            if "text" in msg and msg["text"] is not None:
                result = await _handle_text_message(
                    ws,
                    msg["text"],
                    session,
                    session_dir,
                )

                if result is None:
                    continue

                action, data = result

                if action == "start":
                    session = data["session"]
                    session_dir = data["session_dir"]
                    frame_counter = 0
                    pending_model = None

                elif action == "model_begin":
                    if pending_model is not None:
                        await send_json(ws, {
                            "type": "error",
                            "where": "model_begin",
                            "msg": (
                                "A model upload is already pending. "
                                "Send its binary GLB payload first."
                            ),
                        })
                        continue

                    pending_model = data["model"]

                elif action == "model_complete":
                    # The binary GLB was already validated and stored.
                    pass

                elif action == "stop":
                    if session is None or session_dir is None:
                        await send_json(ws, {
                            "type": "error",
                            "where": "stop",
                            "msg": "No active session exists."
                        })
                        continue

                    if pending_model is not None:
                        await send_json(ws, {
                            "type": "error",
                            "where": "stop",
                            "msg": (
                                f"Cannot stop: model obj_id={pending_model.obj_id} "
                                "is still waiting for its binary GLB payload."
                            ),
                        })
                        continue

                    try:
                        await _finalize_session(session, session_dir)

                        finalized = True
                        stopped_cleanly = True

                        await send_json(ws, {
                            "type": "complete",
                            "sessionId": session.session_id,
                            "frames_captured": len(session.frames),
                            "models_received": len(session.models),
                        })
                    except Exception as exc:
                        logger.error(
                            "[WS] Finalization failed for %s: %s",
                            session.session_id,
                            exc,
                            exc_info=True,
                        )

                        await send_json(ws, {
                            "type": "error",
                            "where": "finalize",
                            "msg": str(exc),
                        })

                    break

                continue

            if "bytes" in msg and msg["bytes"] is not None:
                if session is None or session_dir is None:
                    await send_json(ws, {
                        "type": "error",
                        "where": "bytes",
                        "msg": "Session not started. Send 'start' first."
                    })
                    continue

                # A binary message immediately after model_begin is a GLB.
                if pending_model is not None:
                    saved = await _save_model_glb(
                        ws=ws,
                        payload=msg["bytes"],
                        session=session,
                        session_dir=session_dir,
                        model_msg=pending_model,
                    )

                    if saved:
                        pending_model = None

                    continue

                # Otherwise it is a normal frame packet.
                result = await _handle_binary_message(
                    ws,
                    msg["bytes"],
                    session,
                    session_dir,
                    frame_counter,
                )

                if result is not None:
                    frame_counter = result

                continue

            if msg.get("type") == "websocket.disconnect":
                logger.info(
                    "[WS] Client initiated disconnect: %s:%s",
                    client.host,
                    client.port,
                )
                break

            await send_json(ws, {
                "type": "error",
                "msg": "Unsupported message type.",
            })

    except WebSocketDisconnect:
        logger.info(f"[WS] disconnected: {client.host}:{client.port}")
    except Exception as e:
        logger.error(f"[WS] error: {e}", exc_info=True)
    finally:
        if session and session_dir and not stopped_cleanly:
            logger.warning(
                "[WS] Session %s ended without a successful stop/finalization. "
                "Dataset remains incomplete.",
                session.session_id,
            )

        try:
            await ws.close()
        except RuntimeError:
            pass


# ============== Private Handlers ==============

async def _handle_text_message(
    ws: WebSocket,
    raw: str,
    session: Optional[SessionData],
    session_dir: Optional[Path],
) -> Optional[tuple]:
    """Handle text (JSON) control messages."""
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as e:
        await send_json(ws, {"type": "error", "where": "text", "msg": f"Invalid JSON: {e}"})
        return None

    mtype = data.get("type")

    if mtype == "start":
        try:
            start_msg = StartMessage.model_validate(data)
        except ValidationError as e:
            await send_json(ws, {
                "type": "error", "where": "start", "msg": f"Validation error: {e.errors()}"
            })
            return None

        new_session_dir = SAVE_DIR / start_msg.session_id
        if new_session_dir.exists() and any(new_session_dir.iterdir()):
            await send_json(ws, {
                "type": "error",
                "where": "start",
                "msg": "Session ID already exists; choose a new ID for this capture.",
            })
            return None
        new_session_dir.mkdir(parents=True, exist_ok=True)

        frames_dir = scene_dir(new_session_dir) if start_msg.bop_format else new_session_dir
        (frames_dir / "annotated_rgb").mkdir(parents=True, exist_ok=True)
        if start_msg.bop_format:
            (frames_dir / "rgb").mkdir(exist_ok=True)
            (frames_dir / "mask_visib").mkdir(exist_ok=True)
            (new_session_dir / "models").mkdir(exist_ok=True)
            (new_session_dir / "extras" / "source_glb").mkdir(parents=True, exist_ok=True)

        new_session = SessionData(
            session_id=start_msg.session_id,
            intrinsics=start_msg.intrinsics,
            fps=start_msg.fps,
            bop_format=start_msg.bop_format
        )

        if start_msg.bop_format:
            intrinsics = start_msg.intrinsics
            with open(new_session_dir / "camera.json", "w", encoding="utf-8") as f:
                json.dump({
                    "fx": intrinsics.fx, "fy": intrinsics.fy,
                    "cx": intrinsics.cx, "cy": intrinsics.cy,
                    "width": intrinsics.width, "height": intrinsics.height,
                }, f, indent=2)

        
        logger.info(
            f"[WS] Session started: {start_msg.session_id} "
            f"(BOP format: {start_msg.bop_format}, FPS: {start_msg.fps}) "
            f"from {ws.client.host}:{ws.client.port}"
        )
        await send_json(ws, {"type": "started", "sessionId": start_msg.session_id})

        return ("start", {"session": new_session, "session_dir": new_session_dir})

    elif mtype == "stop":
        try:
            StopMessage.model_validate(data)
        except ValidationError as e:
            await send_json(ws, {
                "type": "error", "where": "stop", "msg": f"Validation error: {e.errors()}"
            })
            return None

        if session and session_dir:
            logger.info(f"[WS] Session stopping: {session.session_id}")
            

        return ("stop", {})

    elif mtype == "model_begin":
        if session is None or session_dir is None:
            await send_json(ws, {
                "type": "error",
                "where": "model_begin",
                "msg": "Session not started. Send 'start' first."
            })
            return None

        try:
            model_msg = ModelBeginMessage.model_validate(data)
        except ValidationError as e:
            await send_json(ws, {
                "type": "error",
                "where": "model_begin",
                "msg": f"Validation error: {e.errors()}"
            })
            return None

        if model_msg.obj_id in session.models:
            await send_json(ws, {
                "type": "error",
                "where": "model_begin",
                "msg": f"Model for obj_id={model_msg.obj_id} was already uploaded."
            })
            return None

        return ("model_begin", {"model": model_msg})

    elif mtype == "model_complete":
        if session is None:
            await send_json(ws, {
                "type": "error",
                "where": "model_complete",
                "msg": "Session not started."
            })
            return None

        try:
            complete_msg = ModelCompleteMessage.model_validate(data)
        except ValidationError as e:
            await send_json(ws, {
                "type": "error",
                "where": "model_complete",
                "msg": f"Validation error: {e.errors()}"
            })
            return None

        if complete_msg.obj_id not in session.models:
            await send_json(ws, {
                "type": "error",
                "where": "model_complete",
                "msg": (
                    f"No binary GLB has been received for "
                    f"obj_id={complete_msg.obj_id}."
                )
            })
            return None

        await send_json(ws, {
            "type": "model_received",
            "obj_id": complete_msg.obj_id,
        })

        return ("model_complete", {"obj_id": complete_msg.obj_id})

    else:
        await send_json(ws, {"type": "error", "where": "text", "msg": f"Unknown type: {mtype}"})
        return None


async def _handle_binary_message(
    ws: WebSocket,
    payload: bytes,
    session: SessionData,
    session_dir: Path,
    frame_counter: int
) -> Optional[int]:
    """Handle binary frame messages. Returns updated frame counter."""
    if len(payload) < 4:
        await send_json(ws, {"type": "error", "where": "bytes", "msg": "Payload too short"})
        return None

    header_len = struct.unpack("<I", payload[:4])[0]
    if len(payload) < 4 + header_len:
        await send_json(ws, {"type": "error", "where": "bytes", "msg": "Bad header length"})
        return None

    header_bytes = payload[4:4 + header_len]
    jpeg_bytes = payload[4 + header_len:]

    try:
        header_data = json.loads(header_bytes.decode("utf-8"))
    except Exception as e:
        await send_json(ws, {"type": "error", "where": "bytes", "msg": f"Invalid header JSON: {e}"})
        return None
    
    # Process based on format
    if session.bop_format:
        return await _process_frame(
            ws, header_data, jpeg_bytes, session, session_dir, frame_counter
        )
    else:
        return await _process_frame_without_bop(
            ws, header_data, jpeg_bytes, session_dir, frame_counter
        )


def _validate_session_models(session: SessionData) -> None:
    """Ensure every object referenced by frame GT has a received GLB model."""

    referenced_object_ids = {
        obj.obj_id
        for frame in session.frames
        for obj in frame.objects
    }

    uploaded_object_ids = set(session.models.keys())

    missing_model_ids = referenced_object_ids - uploaded_object_ids
    if missing_model_ids:
        missing_text = ", ".join(str(obj_id) for obj_id in sorted(missing_model_ids))
        raise RuntimeError(
            f"Missing GLB model uploads for recorded object ids: {missing_text}"
        )

def _export_scaled_bop_models(
    session: SessionData,
    session_dir: Path,
) -> dict[int, list[float]]:
    """
    Convert each uploaded SAM3 GLB into a BOP PLY model.

    Unity scale is in metres per original GLB unit.
    BOP PLY vertices are stored in millimetres.

    Reflect the Unity-local Y axis and center each model's bounding box.
    The returned centers let scene_gt.json preserve the original camera pose.
    """
    try:
        import trimesh
    except ImportError as exc:
        raise RuntimeError(
            "GLB-to-PLY export requires trimesh and pygltflib. "
            "Install them with: pip install trimesh pygltflib"
        ) from exc

    models_dir = session_dir / "models"
    models_dir.mkdir(parents=True, exist_ok=True)

    model_metadata = {}
    models_info = {}
    model_centers_m = {}

    for obj_id, model in session.models.items():
        glb_path = Path(model.glb_path)

        if not glb_path.exists():
            raise RuntimeError(
                f"GLB file for obj_id={obj_id} is missing: {glb_path}"
            )

        loaded = trimesh.load(
            str(glb_path),
            force="scene",
            process=False,
        )

        if isinstance(loaded, trimesh.Scene):
            if len(loaded.geometry) != 1:
                raise RuntimeError(
                    f"Expected exactly one mesh in {glb_path}, "
                    f"found {len(loaded.geometry)}."
                )

            # Keep raw mesh-local coordinates.
            # Do NOT bake scene/node transforms here because Unity sends the
            # extracted mesh object's pose as the BOP pose reference.
            mesh = next(iter(loaded.geometry.values())).copy()

        elif isinstance(loaded, trimesh.Trimesh):
            mesh = loaded.copy()

        else:
            raise RuntimeError(
                f"Unsupported GLB content for obj_id={obj_id}: {type(loaded)}"
            )

        if mesh.vertices is None or len(mesh.vertices) == 0 or len(mesh.faces) == 0:
            raise RuntimeError(f"GLB for obj_id={obj_id} has no triangle mesh.")

        scale_m = np.asarray(model.glb_to_unity_scale, dtype=np.float64)

        if scale_m.shape != (3,):
            raise RuntimeError(
                f"Invalid scale for obj_id={obj_id}: {scale_m.tolist()}"
            )

        if not np.all(np.isfinite(scale_m)) or np.any(scale_m <= 0):
            raise RuntimeError(
                f"Invalid non-positive/non-finite scale for obj_id={obj_id}: "
                f"{scale_m.tolist()}"
            )

        if not np.allclose(scale_m, scale_m[0], atol=1e-4):
            raise RuntimeError(
                f"Non-uniform scale is not valid for BOP obj_id={obj_id}: "
                f"{scale_m.tolist()}"
            )

        # GLB mesh units -> right-handed metres -> bbox-centered millimetres.
        vertices_m = (
            np.asarray(mesh.vertices, dtype=np.float64) * scale_m[np.newaxis, :]
        ) @ MODEL_AXIS_FLIP
        center_m = (vertices_m.min(axis=0) + vertices_m.max(axis=0)) / 2.0
        model_centers_m[obj_id] = center_m.tolist()
        mesh.vertices = (vertices_m - center_m) * 1000.0
        mesh.invert()  # Reflection changes face winding; restore outward normals.
        _ = mesh.vertex_normals

        ply_path = models_dir / f"obj_{obj_id:06d}.ply"
        mesh.export(
            str(ply_path), file_type="ply", encoding="ascii", vertex_normal=True
        )

        try:
            diameter_vertices = np.asarray(mesh.convex_hull.vertices)
        except Exception:
            diameter_vertices = np.asarray(mesh.vertices)
        models_info[str(obj_id)] = model_info_from_vertices(
            mesh.vertices, diameter_vertices
        )

        model_metadata[str(obj_id)] = {
            "obj_id": obj_id,
            "obj_name": model.obj_name,
            "source_glb": glb_path.name,
            "glb_to_unity_scale": model.glb_to_unity_scale,
            "ply_units": "mm",
            "ply_path": ply_path.name,
            "model_center_m": center_m.tolist(),
        }

        logger.info(
            "Exported BOP PLY: obj_id=%d, path=%s, scale=%s",
            obj_id,
            ply_path,
            model.glb_to_unity_scale,
        )

    with open(models_dir / "models_info.json", "w", encoding="utf-8") as f:
        json.dump(models_info, f, indent=2)
    with open(session_dir / "extras" / "model_metadata.json", "w", encoding="utf-8") as f:
        json.dump(model_metadata, f, indent=2)

    return model_centers_m

async def _process_frame_without_bop(
    ws: WebSocket,
    header_data: dict,
    jpeg_bytes: bytes,
    session_dir: Path,
    frame_counter: int
) -> Optional[int]:
    """Process a frame in legacy format (vertices only)."""
    
    try:
        header = FrameHeaderLegacy.model_validate(header_data)
    except ValidationError as e:
        await send_json(ws, {
            "type": "error",
            "where": "frame_header",
            "msg": f"Validation error: {e.errors()}"
        })
        return None

    try:
        img = decode_jpeg(jpeg_bytes)
    except Exception as e:
        await send_json(ws, {"type": "error", "where": "bytes", "msg": f"JPEG decode failed: {e}"})
        return None

    h, w = img.shape[:2]
    
    # Convert vertices and draw
    all_vertices = []
    for pose_verts in header.poses:
        vertices_px = viewport_to_pixels(pose_verts, w, h)
        all_vertices.append(vertices_px)

    annotated_img = draw_object_poses(img, np.array(all_vertices, dtype=np.int32))

    # Save
    jpg_path = session_dir / "annotated_rgb" / f"{frame_counter:06d}.jpg"
    save_image(annotated_img, jpg_path)

    return frame_counter + 1


async def _process_frame(
    ws: WebSocket,
    header_data: dict,
    jpeg_bytes: bytes,
    session: SessionData,
    session_dir: Path,
    frame_counter: int
) -> Optional[int]:
    """Process a lightweight frame - compute poses on backend."""
    try:
        header = FrameHeaderLightweight.model_validate(header_data)
    except ValidationError as e:
        await send_json(ws, {
            "type": "error", "where": "frame_header", "msg": f"Validation error: {e.errors()}"
        })
        return None

    try:
        img = decode_jpeg(jpeg_bytes)
    except Exception as e:
        await send_json(ws, {"type": "error", "where": "bytes", "msg": f"JPEG decode failed: {e}"})
        return None

    h, w = img.shape[:2]

    # Compute poses
    object_poses = _compute_frame_poses(header, session.intrinsics)

    # Draw annotations
    annotated_img = img.copy()
    for idx, obj_pose in enumerate(object_poses):
        if obj_pose.bbox_2d:
            vertices_px = viewport_to_pixels(obj_pose.bbox_2d, w, h)
            color = get_color(idx)
            annotated_img = draw_bbox_3d(annotated_img, vertices_px, color)

            if len(vertices_px) > 0:
                sorted_vertices = sorted(vertices_px, key=lambda v: (-v[1], v[0]))
                label_pos = (int(sorted_vertices[0][0]), int(sorted_vertices[0][1]) + 10)
                draw_label(
                    annotated_img,
                    f"{obj_pose.obj_name}",
                    label_pos,
                    color
                )

    # Save images
    frames_dir = scene_dir(session_dir)
    save_image(annotated_img, frames_dir / "annotated_rgb" / f"{frame_counter:06d}.jpg", format="JPEG")
    save_image(img, frames_dir / "rgb" / f"{frame_counter:06d}.png", format="PNG")

    # Store frame data
    frame_data = BOPFrameData(
        frame_id=frame_counter,
        timestamp=header.timestamp,
        camera_pose=header.camera_pose,
        objects=object_poses,
        image_path=str(frames_dir / "rgb" / f"{frame_counter:06d}.png")
    )
    session.add_frame(frame_data)

    logger.debug(f"[WS] Frame {frame_counter}: computed {len(object_poses)} object poses")
    return frame_counter + 1


def _compute_frame_poses(
    header: FrameHeaderLightweight,
    intrinsics: Intrinsics
) -> List[ObjectPoseData]:
    """Compute all BOP-format pose data from lightweight frame."""
    results = []

    for obj in header.objects:
        R, t = compute_object_pose_in_camera(obj, header.camera_pose)

        bbox_2d = None
        bbox_3d_cam = None

        if obj.bbox_world and len(obj.bbox_world) > 0:
            vertices_world = np.array(obj.bbox_world, dtype=np.float32)
            vertices_cam = transform_vertices_to_camera(vertices_world, header.camera_pose)
            bbox_3d_cam = vertices_cam.tolist()
            viewport_coords = world_to_viewport(vertices_world, header.camera_pose, intrinsics)
            bbox_2d = viewport_coords.tolist()

        results.append(ObjectPoseData(
            obj_id=obj.obj_id,
            obj_name=obj.obj_name,
            R=R.tolist(),
            t=t.tolist(),
            bbox_2d=bbox_2d,
            bbox_3d_cam=bbox_3d_cam
        ))

    return results


def generate_mask_visib(session: SessionData, session_dir: Path) -> None:
    """
    Generate one mask_visib PNG for every frame-local GT object.

    Invalid boxes and failed frame inference receive all-zero fallback masks.
    """
    frames_dir = scene_dir(session_dir)
    mask_dir = frames_dir / "mask_visib"
    mask_dir.mkdir(parents=True, exist_ok=True)

    for frame in session.frames:
        rgb_path = frames_dir / "rgb" / f"{frame.frame_id:06d}.png"

        try:
            with Image.open(rgb_path) as loaded_image:
                image = loaded_image.convert("RGB")

            width, height = image.size

        except Exception as exc:
            logger.error(
                "Cannot open RGB frame %s for BOP segmentation: %s",
                rgb_path,
                exc,
            )
            continue

        valid_gt_ids: list[int] = []
        valid_boxes: list[list[int]] = []

        for gt_id, obj in enumerate(frame.objects):
            output_path = mask_dir / f"{frame.frame_id:06d}_{gt_id:06d}.png"

            box = _projected_vertices_to_xyxy(
                obj.bbox_2d,
                image_width=width,
                image_height=height,
            )

            if box is None:
                logger.warning(
                    "Frame %06d gt_id %06d: invalid/offscreen projected box; "
                    "writing empty mask",
                    frame.frame_id,
                    gt_id,
                )
                _write_empty_bop_mask(output_path, height, width)
                continue

            valid_gt_ids.append(gt_id)
            valid_boxes.append(box)

        if not valid_boxes:
            continue

        try:
            masks = segment_boxes_in_image(image, valid_boxes)

            if len(masks) != len(valid_gt_ids):
                raise RuntimeError(
                    f"SAM3 returned {len(masks)} masks for {len(valid_gt_ids)} boxes"
                )

            for gt_id, mask in zip(valid_gt_ids, masks):
                output_path = mask_dir / f"{frame.frame_id:06d}_{gt_id:06d}.png"

                if not np.any(mask):
                    logger.warning(
                        "Frame %06d gt_id %06d: SAM3 returned an empty mask",
                        frame.frame_id,
                        gt_id,
                    )

                _write_bop_mask(mask, output_path, height, width)

        except Exception as exc:
            logger.error(
                "SAM3 segmentation failed for frame %06d: %s",
                frame.frame_id,
                exc,
                exc_info=True,
            )

            # Preserve one mask file per scene_gt object.
            for gt_id in valid_gt_ids:
                output_path = mask_dir / f"{frame.frame_id:06d}_{gt_id:06d}.png"
                _write_empty_bop_mask(output_path, height, width)


async def _save_model_glb(
    ws: WebSocket,
    payload: bytes,
    session: SessionData,
    session_dir: Path,
    model_msg: ModelBeginMessage,
) -> bool:
    """Validate and persist one uploaded SAM3 GLB."""

    if len(payload) != model_msg.byte_length:
        await send_json(ws, {
            "type": "error",
            "where": "model_glb",
            "msg": (
                f"obj_id={model_msg.obj_id}: expected {model_msg.byte_length} bytes, "
                f"received {len(payload)} bytes."
            ),
        })
        return False

    # Standard GLB header begins with ASCII bytes: b'glTF'
    if len(payload) < 12 or payload[:4] != b"glTF":
        await send_json(ws, {
            "type": "error",
            "where": "model_glb",
            "msg": f"obj_id={model_msg.obj_id}: payload is not a valid GLB header.",
        })
        return False

    glb_path = session_dir / "extras" / "source_glb" / f"obj_{model_msg.obj_id:06d}.glb"
    temporary_path = glb_path.with_suffix(".glb.part")

    # Atomic write: a final .glb is visible only after the complete payload is written.
    temporary_path.write_bytes(payload)
    temporary_path.replace(glb_path)

    session.add_model(ModelAssetData(
        obj_id=model_msg.obj_id,
        obj_name=model_msg.obj_name,
        glb_path=str(glb_path),
        glb_to_unity_scale=model_msg.glb_to_unity_scale,
    ))

    logger.info(
        "[WS] Received GLB for obj_id=%d, bytes=%d, scale=%s",
        model_msg.obj_id,
        len(payload),
        model_msg.glb_to_unity_scale,
    )

    return True



async def _finalize_session(session: SessionData, session_dir: Path):
    """Finalize capture without blocking the FastAPI event loop."""
    await asyncio.to_thread(_finalize_session_sync, session, session_dir)


def _finalize_session_sync(session: SessionData, session_dir: Path):
    """Generate scaled PLY models, masks, BOP metadata, and video."""

    if session.bop_format:
        _validate_session_models(session)
        model_centers_m = _export_scaled_bop_models(session, session_dir)

        generate_mask_visib(session, session_dir)
        _generate_bop_files(session, session_dir, model_centers_m)

    try:
        frames_dir = scene_dir(session_dir) if session.bop_format else session_dir
        encode_video_from_frames(
            frames_dir / "annotated_rgb", session.fps,
            output_dir=session_dir / "extras",
        )
        logger.info("Video encoded: /pose/video/%s", session.session_id)
    except Exception as exc:
        logger.error("Video encoding failed: %s", exc, exc_info=True)

    if session.bop_format:
        validate_dataset_layout(session_dir, len(session.frames))
        with open(session_dir / "dataset_info.json", "w", encoding="utf-8") as f:
            json.dump({
                "name": session.session_id,
                "format": "bop-scenewise",
                "split": "train",
                "scene_ids": [0],
                "modalities": ["rgb"],
                "frame_count": len(session.frames),
            }, f, indent=2)


def _generate_bop_files(
    session: SessionData, session_dir: Path, model_centers_m: dict[int, list[float]]
):
    """Generate BOP-scenewise JSON files for this capture's training scene."""
    frames_dir = scene_dir(session_dir)
    # scene_gt.json
    scene_gt = session.to_bop_scene_gt(model_centers_m)
    with open(frames_dir / "scene_gt.json", "w", encoding="utf-8") as f:
        json.dump(scene_gt, f, indent=2)
    logger.info(f"Generated scene_gt.json with {len(scene_gt)} frames")

    # scene_camera.json
    scene_camera = session.to_bop_scene_camera()
    with open(frames_dir / "scene_camera.json", "w", encoding="utf-8") as f:
        json.dump(scene_camera, f, indent=2)

    # scene_gt_info.json
    scene_gt_info = _generate_scene_gt_info(session, session_dir)
    with open(frames_dir / "scene_gt_info.json", "w", encoding="utf-8") as f:
        json.dump(scene_gt_info, f, indent=2)

    with open(session_dir / "extras" / "object_info.json", "w", encoding="utf-8") as f:
        json.dump(session.to_object_info(), f, indent=2)


def _generate_scene_gt_info(session: SessionData, session_dir: Path) -> dict:
    """Generate visible-object BOP metadata from mask_visib PNG files."""
    scene_gt_info = {}

    for frame in session.frames:
        frame_key = str(frame.frame_id)
        frame_info = []

        for gt_id, _obj in enumerate(frame.objects):
            mask_path = (
                scene_dir(session_dir)
                / "mask_visib"
                / f"{frame.frame_id:06d}_{gt_id:06d}.png"
            )

            obj_info = {
                "bbox_obj": [-1, -1, -1, -1],
                "bbox_visib": [-1, -1, -1, -1],
                "px_count_all": -1,
                "px_count_valid": -1,
                "px_count_visib": 0,
                "visib_fract": -1.0,
            }

            if not mask_path.exists():
                logger.warning("Missing BOP visible mask: %s", mask_path)
                frame_info.append(obj_info)
                continue

            mask = cv.imread(str(mask_path), cv.IMREAD_GRAYSCALE)
            if mask is None:
                logger.warning("Could not read BOP visible mask: %s", mask_path)
                frame_info.append(obj_info)
                continue

            ys, xs = np.where(mask > 0)

            if len(xs) > 0:
                x_min = int(xs.min())
                y_min = int(ys.min())
                x_max = int(xs.max())
                y_max = int(ys.max())

                obj_info["bbox_visib"] = [
                    x_min,
                    y_min,
                    x_max - x_min + 1,
                    y_max - y_min + 1,
                ]
                obj_info["px_count_visib"] = int(len(xs))

            frame_info.append(obj_info)

        scene_gt_info[frame_key] = frame_info

    return scene_gt_info
