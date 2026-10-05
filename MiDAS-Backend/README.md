# MiDAS Backend — Workstation Deployment Guide

FastAPI service for SAM 3 image segmentation, pose annotation, frame capture, and BOP dataset export. This guide describes deployment on a GPU workstation with enough compute capacity to host the backend continuously.

The deployment uses Docker directly. 

---

## Table of Contents

- [MiDAS Backend — Workstation Deployment Guide](#midas-backend--workstation-deployment-guide)
  - [Table of Contents](#table-of-contents)
  - [Architecture Overview](#architecture-overview)
  - [Prerequisites](#prerequisites)
  - [Repository Structure](#repository-structure)
  - [Prepare SAM 3](#prepare-sam-3)
  - [Build the Docker Image](#build-the-docker-image)
    - [Optional registry workflow](#optional-registry-workflow)
  - [Configure Persistent Storage](#configure-persistent-storage)
  - [Run the Backend](#run-the-backend)
    - [Stop and start the service](#stop-and-start-the-service)
    - [Remove the container](#remove-the-container)
  - [Run with Docker Compose](#run-with-docker-compose)
  - [Access the Backend](#access-the-backend)
    - [From the workstation](#from-the-workstation)
    - [From another machine](#from-another-machine)
    - [Production exposure](#production-exposure)
  - [API Reference](#api-reference)
  - [Pose Capture WebSocket](#pose-capture-websocket)
    - [Model upload](#model-upload)
    - [Finalization](#finalization)
  - [Session Output](#session-output)
    - [Backups](#backups)
  - [Configuration Reference](#configuration-reference)
  - [Updating the Deployment](#updating-the-deployment)
  - [Troubleshooting](#troubleshooting)

---

## Architecture Overview

The backend runs in a GPU-enabled Docker container on a workstation:

```text
Client application
      │
      │  HTTP / WebSocket
      ▼
GPU workstation :8000
      │
      │  Docker port mapping
      ▼
Container :8000
      │
      ▼
FastAPI / SAM 3 / pose capture
      │
      ├── /models/sam3.pt
      ├── /data/frames
      └── /data/sam3_masks
```

The workstation can be accessed in one of three ways:

- Locally through `http://localhost:8000`.
- From a trusted network through `http://<workstation-hostname>:8000`.
- Through a reverse proxy that provides TLS and authentication.

The SAM 3 checkpoint and generated output are mounted from the workstation so that they persist when the container is replaced or restarted.

---

## Prerequisites

| Requirement | Details |
|---|---|
| Operating system | A Linux workstation is recommended. |
| NVIDIA GPU | A CUDA-capable GPU with enough VRAM for SAM 3 inference. |
| NVIDIA driver | A host driver compatible with the CUDA version used by the image. |
| Docker | Docker Engine with Docker Compose support. |
| NVIDIA Container Toolkit | Required to expose the workstation GPU to Docker. |
| Storage | Enough space for the SAM 3 checkpoint, uploaded models, frames, masks, videos, and BOP exports. |
| SAM 3 source | Cloned into `external/sam3` before building the image. |
| SAM 3 checkpoint | Obtained through the [official SAM 3 repository](https://github.com/facebookresearch/sam3). |
| Network access | Port `8000`, or the configured alternative, must be reachable by intended clients. |

Verify Docker:

```bash
docker --version
docker compose version
```

Verify GPU access from Docker:

```bash
docker run --rm --gpus all nvidia/cuda:12.6.0-base-ubuntu24.04 nvidia-smi
```

If this command fails, install or repair the NVIDIA driver and NVIDIA Container Toolkit before continuing.

---

## Repository Structure

```text
.
├── Dockerfile              # CUDA image and FastAPI entry point
├── requirements.txt        # Backend Python dependencies
├── compose.yaml            # Optional workstation deployment
├── app/
│   ├── main.py             # Startup and health check
│   ├── routers/            # SAM 3 and pose endpoints
│   ├── models/sam3/        # Model loading and inference
│   ├── schemas/            # Request and session models
│   └── utils/              # Image, video, BOP format, and export helpers
├── test/                   # Sample requests and notebooks
└── external/sam3/          # Local SAM 3 checkout, not tracked
```

---

## Prepare SAM 3

From the repository root, clone the SAM 3 source required by the Dockerfile:

```bash
mkdir -p external
git clone https://github.com/facebookresearch/sam3.git external/sam3
```

The Dockerfile copies `external/sam3/sam3` and `external/sam3/pyproject.toml` into the image.

Obtain the SAM 3 checkpoint according to the official project instructions. Store it outside the repository, for example:

```text
/opt/midas/models/sam3.pt
```

Restrict write access to the checkpoint where appropriate. The container only needs read access.

---

## Build the Docker Image

Build the image from the repository root:

```bash
docker build -t midas-backend:latest .
```

Confirm that the image exists:

```bash
docker image inspect midas-backend:latest
```

A container registry is not required when the image is built and run on the same workstation.

### Optional registry workflow

If images are built on another machine, use any OCI-compatible registry:

```bash
export IMAGE="registry.example.com/your-project/midas-backend:latest"

docker login registry.example.com
docker build -t "${IMAGE}" .
docker push "${IMAGE}"
```

On the workstation:

```bash
docker pull "${IMAGE}"
```

Replace the example registry and project with values appropriate for your environment.

---

## Configure Persistent Storage

Create directories for the checkpoint and generated output:

```bash
sudo mkdir -p /opt/midas/models
sudo mkdir -p /opt/midas/data/frames
sudo mkdir -p /opt/midas/data/sam3_masks
```

Copy the checkpoint into place:

```bash
sudo cp /path/to/downloaded/sam3.pt /opt/midas/models/sam3.pt
```

Set ownership to the account that manages the deployment:

```bash
sudo chown -R "$(id -u):$(id -g)" /opt/midas
```

The resulting layout is:

```text
/opt/midas/
├── models/
│   └── sam3.pt
└── data/
    ├── frames/
    └── sam3_masks/
```

The paths under `/opt/midas` are examples. Any readable and writable host paths can be used.

---

## Run the Backend

Start the backend directly with Docker:

```bash
docker run -d \
  --name midas-backend \
  --restart unless-stopped \
  --gpus all \
  -p 8000:8000 \
  -e SAM3_CHECKPOINT_PATH=/models/sam3.pt \
  -v /opt/midas/models/sam3.pt:/models/sam3.pt:ro \
  -v /opt/midas/data/frames:/data/frames \
  -v /opt/midas/data/sam3_masks:/data/sam3_masks \
  midas-backend:latest
```

Check the container status:

```bash
docker ps --filter name=midas-backend
```

Follow the logs:

```bash
docker logs -f midas-backend
```

Check backend health:

```bash
curl http://localhost:8000/health
```

### Stop and start the service

```bash
docker stop midas-backend
docker start midas-backend
```

### Remove the container

Removing the container does not delete data in the mounted host directories:

```bash
docker rm -f midas-backend
```

---

## Run with Docker Compose

A Compose deployment makes configuration and service management easier.

Create `compose.yaml` in the repository root:

```yaml
services:
  backend:
    build:
      context: .
      dockerfile: Dockerfile
    image: midas-backend:latest
    container_name: midas-backend
    restart: unless-stopped
    ports:
      - "8000:8000"
    environment:
      SAM3_CHECKPOINT_PATH: /models/sam3.pt
    volumes:
      - ${MIDAS_MODEL_PATH:-/opt/midas/models/sam3.pt}:/models/sam3.pt:ro
      - ${MIDAS_FRAMES_PATH:-/opt/midas/data/frames}:/data/frames
      - ${MIDAS_MASKS_PATH:-/opt/midas/data/sam3_masks}:/data/sam3_masks
    gpus: all
```

Optionally create a `.env` file to override the host paths:

```dotenv
MIDAS_MODEL_PATH=/opt/midas/models/sam3.pt
MIDAS_FRAMES_PATH=/opt/midas/data/frames
MIDAS_MASKS_PATH=/opt/midas/data/sam3_masks
```

Do not commit `.env` if it contains environment-specific or sensitive values.

Build and start the service:

```bash
docker compose up -d --build
```

Check its status:

```bash
docker compose ps
```

Follow logs:

```bash
docker compose logs -f backend
```

Restart the backend:

```bash
docker compose restart backend
```

Stop the deployment:

```bash
docker compose down
```

Persistent output remains in the mounted workstation directories.

---

## Access the Backend

### From the workstation

Use:

```text
http://localhost:8000
```

Available service URLs include:

- Health check: `http://localhost:8000/health`
- OpenAPI documentation: `http://localhost:8000/docs`
- WebSocket endpoint: `ws://localhost:8000/pose/video`

### From another machine

Use the workstation hostname or IP address:

```text
http://workstation.example.com:8000
```

The WebSocket endpoint becomes:

```text
ws://workstation.example.com:8000/pose/video
```

Ensure that:

1. The workstation firewall permits inbound TCP traffic on port `8000`.
2. The client can resolve or reach the workstation.
3. Network policy permits access between the client and workstation.

For example, on a system using UFW, access can be limited to a trusted subnet:

```bash
sudo ufw allow from 192.0.2.0/24 to any port 8000 proto tcp
```

Replace the example subnet with the actual trusted network.

### Production exposure

The backend does not inherently provide transport encryption or user authentication. If it is exposed beyond a trusted network, place it behind a reverse proxy that provides:

- HTTPS/TLS
- Authentication or access control
- Request-size limits appropriate for image and GLB uploads
- WebSocket forwarding
- Timeouts long enough for BOP finalization

Do not expose port `8000` directly to the public internet without suitable protection.

---

## API Reference

| Route | Purpose |
|---|---|
| `GET /health` | Reports service status, CUDA availability, whether SAM 3 loaded, and its device. |
| `POST /sam3/segment` | Segments an uploaded image with point prompts and returns `mask_png` as base64-encoded RGBA PNG bytes. |
| `GET /sam3/download-masks` | Downloads a ZIP of saved annotated PNGs from `/data/sam3_masks`; returns 404 when none exist. |
| `POST /pose/snap` | Annotates viewport-space pose vertices on an uploaded image; returns `img_png` as base64 PNG. |
| `WS /pose/video` | Captures frame sessions and optional BOP data. |
| `GET /pose/video/{session_id}` | Downloads the session's annotated MP4 if present. |
| `GET /pose/bop/{session_id}` | Downloads the completed, self-contained BOP dataset ZIP. |

`POST /sam3/segment` accepts multipart fields named `image` and `payload`. The payload is a JSON string containing camera `intrinsics`, `extrinsics`, `world_point`, and a `point_prompt` with pixel `points` and labels:

- `1`: foreground
- `0`: background

The sample in `test/segment/test_01.json` shows the complete structure:

```bash
curl -X POST http://localhost:8000/sam3/segment \
  -F "image=@test/segment/test_01.jpg" \
  -F "payload=<test/segment/test_01.json"
```

The returned mask is included in the response body as base64 text.

The server also saves an annotated image under `/data/sam3_masks`. Because that path is mounted to the workstation, the files remain available after the container is replaced.

---

## Pose Capture WebSocket

Connect to:

```text
ws://localhost:8000/pose/video
```

For remote access, replace `localhost` with the workstation hostname. When TLS is provided by a reverse proxy, use `wss://` instead of `ws://`.

The server first sends:

```json
{
  "type": "hello",
  "msg": "connected"
}
```

Start a session with a JSON text message:

```json
{
  "type": "start",
  "session_id": "example-session",
  "fps": 30,
  "bop_format": true,
  "intrinsics": {
    "width": 1280,
    "height": 960,
    "sensor_width": 1280.0,
    "sensor_height": 1280.0,
    "fx": 867.5285,
    "fy": 867.5285,
    "cx": 639.4329,
    "cy": 640.4481
  }
}
```

The server replies with `started`.

Each frame is one binary WebSocket message containing:

1. A 4-byte little-endian unsigned header length.
2. A UTF-8 JSON header of that length.
3. The JPEG bytes.

With `bop_format: true`, the header supplies:

- `frame_id`
- `timestamp`
- `camera_pose` as `[px, py, pz, qx, qy, qz, qw]`
- `objects`

Each object supplies:

- `obj_id`
- `obj_name`
- World-space `position`
- Quaternion `rotation`
- `scale`
- Optional `bbox_world` vertices

With `bop_format: false`, the header instead contains viewport-space `poses`.

### Model upload

For a BOP session, upload a GLB for every object ID referenced by frames.

Send a `model_begin` JSON text message containing:

- `obj_id`
- `obj_name`
- `format: "model/gltf-binary"`
- `byte_length`
- A positive, uniform three-value `glb_to_unity_scale`

Send the raw GLB as the next binary message. Then send:

```json
{
  "type": "model_complete",
  "obj_id": 1
}
```

Wait for `model_received` before continuing.

The backend converts each uploaded mesh into a right-handed ASCII PLY in millimetres, centers its bounding box, and adjusts the exported pose accordingly. A GLB must contain exactly one triangle mesh.

### Finalization

After sending all frames and models, send:

```json
{
  "type": "stop"
}
```

Finalization completes before the server sends `complete`. The response includes the stored BOP-frame and uploaded-model counts.

If a referenced model is missing or export fails, the server sends an `error` response instead.

---

## Session Output

Sessions are written under:

```text
/data/frames/<session_id>/
```

With the example deployment, this maps to:

```text
/opt/midas/data/frames/<session_id>/
```

Each BOP capture is a self-contained [BOP scenewise training dataset](https://github.com/thodan/bop_toolkit/blob/master/docs/bop_datasets_format.md) with scene ID `000000`:

```text
<session_id>/
├── camera.json
├── dataset_info.json
├── models/
│   ├── obj_000001.ply
│   └── models_info.json
├── train/000000/
│   ├── rgb/000000.png
│   ├── annotated_rgb/000000.jpg
│   ├── mask_visib/000000_000000.png
│   ├── scene_camera.json
│   ├── scene_gt.json
│   └── scene_gt_info.json
└── extras/
    ├── source_glb/obj_000001.glb
    ├── model_metadata.json
    ├── object_info.json
    └── video.mp4
```

Visible masks are generated from projected object boxes using SAM 3 during BOP finalization.

Image IDs match across:

- `rgb`
- `annotated_rgb`
- Scene JSON files

Mask filenames combine the image ID with the frame's ground-truth object index.

This is an RGB-only capture:

- No depth files are created.
- No full-silhouette masks are created.
- `bbox_visib` and `px_count_visib` come from the visible mask.
- Unavailable full-silhouette and depth-dependent fields remain `-1`.
- `scene_gt_coco.json` is not generated because full-silhouette masks are unavailable.

The `extras` directory contains capture artifacts outside the standard BOP scene structure.

A non-BOP session saves `annotated_rgb/` and `extras/video.mp4` without BOP metadata.

### Backups

The workstation deployment does not automatically copy output elsewhere. Back up `/opt/midas/data` according to the retention requirements of your environment.

For example:

```bash
rsync -a --delete /opt/midas/data/ /path/to/backup/midas-data/
```

Schedule backups with the workstation's standard backup system or a systemd timer if required.

---

## Configuration Reference

| Setting | Default or example | Meaning |
|---|---|---|
| `SAM3_CHECKPOINT_PATH` | `/models/sam3.pt` | Checkpoint path inside the container. |
| `MIDAS_MODEL_PATH` | `/opt/midas/models/sam3.pt` | Checkpoint path on the workstation. |
| `MIDAS_FRAMES_PATH` | `/opt/midas/data/frames` | Workstation directory mounted at `/data/frames`. |
| `MIDAS_MASKS_PATH` | `/opt/midas/data/sam3_masks` | Workstation directory mounted at `/data/sam3_masks`. |
| Host port | `8000` | TCP port exposed by the workstation. |
| Container port | `8000` | FastAPI listener inside the container. |
| Container image | `midas-backend:latest` | Locally built image name. |
| Restart policy | `unless-stopped` | Restarts the backend after failures and workstation reboots unless it was manually stopped. |

Use an immutable version or commit tag instead of `latest` when reproducible deployments are required.

---

## Updating the Deployment

Pull the latest source changes and rebuild:

```bash
git pull
docker compose build --pull backend
docker compose up -d backend
```

Check startup and model loading:

```bash
docker compose logs -f backend
```

Verify health:

```bash
curl http://localhost:8000/health
```

The checkpoint and output remain on the workstation because they are bind-mounted into the container.

To remove unused image layers after confirming the update:

```bash
docker image prune
```

---

## Troubleshooting

| Problem | Check |
|---|---|
| Docker build cannot find `external/sam3` | Clone the SAM 3 source at the exact path described in [Prepare SAM 3](#prepare-sam-3). |
| SAM 3 fails to load | Check the checkpoint path, file permissions, mount, `/health`, and container logs. |
| GPU is unavailable in the container | Verify `nvidia-smi` on the host and run the CUDA Docker test from Prerequisites. |
| Container exits during startup | Run `docker logs midas-backend` or `docker compose logs backend`. |
| Port `8000` is already in use | Stop the conflicting service or publish another host port, such as `-p 8080:8000`. |
| Backend works locally but not remotely | Check the workstation firewall, hostname, routing, and network access policy. |
| WebSocket fails through a reverse proxy | Enable WebSocket upgrade forwarding and increase proxy timeouts. |
| `/sam3/download-masks` returns 404 | The route only archives annotated PNGs created by segmentation requests. |
| Annotated masks disappear | Confirm that `/data/sam3_masks` is mounted to a persistent workstation directory. |
| Session output disappears | Confirm that `/data/frames` is mounted to a persistent workstation directory. |
| Permission denied when writing output | Ensure the container process can write to the mounted frames and masks directories. |
| BOP finalization reports a missing GLB | Upload one model for every object ID before sending `stop`. |
| BOP model export reports missing libraries | Rebuild the image with the current `requirements.txt`. |
| Video download returns 404 | Check whether FFmpeg created `extras/video.mp4` in the session directory. |
| Workstation runs out of storage | Monitor `/opt/midas/data`, configure retention, and back up or remove old sessions. |
| Workstation runs out of GPU memory | Stop other GPU workloads, reduce concurrent requests, or use a GPU with more VRAM. |
