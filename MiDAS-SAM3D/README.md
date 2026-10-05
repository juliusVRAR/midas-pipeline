# SAM3D FastAPI Server — Workstation Deployment Guide

A containerized 3-D segmentation inference service built on SAM3D, served through FastAPI and Uvicorn on a dedicated GPU workstation.



---

## Table of Contents

- [SAM3D FastAPI Server — Workstation Deployment Guide](#sam3d-fastapi-server--workstation-deployment-guide)
  - [Table of Contents](#table-of-contents)
  - [Architecture Overview](#architecture-overview)
  - [Hardware Requirements](#hardware-requirements)
  - [Software Prerequisites](#software-prerequisites)
  - [Repository Structure](#repository-structure)
  - [Prepare the Checkpoints](#prepare-the-checkpoints)
  - [Build the Docker Image](#build-the-docker-image)
    - [Optional registry workflow](#optional-registry-workflow)
  - [Run with Docker Compose](#run-with-docker-compose)
  - [Access the Server](#access-the-server)
    - [Local access](#local-access)
    - [Trusted network access](#trusted-network-access)
    - [Remote access through SSH](#remote-access-through-ssh)
    - [Security notice](#security-notice)
  - [Automatic Startup](#automatic-startup)
  - [Updating the Server](#updating-the-server)
  - [API Reference](#api-reference)
    - [`GET /health`](#get-health)
    - [`POST /infer/glb`](#post-inferglb)
  - [Configuration Reference](#configuration-reference)
  - [Troubleshooting](#troubleshooting)

---

## Architecture Overview

```text
Client application
       │
       │ HTTP
       ▼
GPU workstation :8001
       │
       │ Docker port mapping
       ▼
Docker container :8000
       │
       ▼
FastAPI / Uvicorn
       │
       ▼
SAM3D inference pipeline
```

The workstation:

1. Stores the SAM3D checkpoints on persistent local storage.
2. Builds or pulls the Docker image.
3. Runs the container with NVIDIA GPU access.
4. Exposes the FastAPI service on port `8001`.
5. Automatically restarts the service after failures or workstation reboots.

---

## Hardware Requirements

Recommended workstation configuration:

| Component | Recommendation |
|---|---|
| GPU | One CUDA-capable NVIDIA GPU supported by SAM3D |
| GPU memory | Use a GPU with enough VRAM for the selected model and input resolution |
| CPU | Modern multi-core processor; 16 or more cores recommended for demanding workloads |
| System memory | 64 GB minimum; 128 GB recommended for workloads comparable to the former cluster deployment |
| Storage | SSD with enough capacity for the Docker image, checkpoints, temporary files, and generated GLB models |
| Network | Gigabit Ethernet or faster for remote clients and large uploads |

Actual requirements depend on the SAM3D model, image resolution, request concurrency, and inference settings.

---

## Software Prerequisites

Install the following software on the workstation:

| Requirement | Purpose |
|---|---|
| Linux | Recommended host operating system |
| Git | Source repository management |
| Docker Engine | Container execution |
| Docker Compose | Service configuration and lifecycle management |
| NVIDIA driver | Host GPU support |
| NVIDIA Container Toolkit | Makes the GPU available inside Docker |
| SAM3D checkpoints | Model configuration and weights used for inference |

Verify Docker and Docker Compose:

```bash
docker --version
docker compose version
```

Verify the NVIDIA driver:

```bash
nvidia-smi
```

Verify that Docker can access the GPU:

```bash
docker run --rm --gpus all \
  nvidia/cuda:12.1.1-base-ubuntu22.04 \
  nvidia-smi
```

The exact CUDA image tag can be changed. Its CUDA version must be compatible with the installed NVIDIA driver and the application image.

If the test fails, install or repair the NVIDIA driver and NVIDIA Container Toolkit before continuing.

---

## Repository Structure

```text
.
├── Dockerfile          # Two-stage CUDA build
├── compose.yaml        # Workstation service definition
├── .env                # Local deployment configuration; do not commit secrets
├── scripts/
│   └── build_push.sh   # Optional registry build and publication script
├── server.py           # FastAPI application
├── inference.py        # Inference wrapper
├── environment.yml     # Conda environment
├── requirements.txt    # Pip dependencies
└── pyproject.toml      # Package definition
```

---

## Prepare the Checkpoints

Create a persistent directory for the SAM3D configuration and model weights:

```bash
sudo mkdir -p /srv/sam3d/checkpoints
sudo chown -R "$(id -u):$(id -g)" /srv/sam3d
```

Copy or extract the checkpoint package into that directory:

```bash
unzip /path/to/sam3d_checkpoint.zip -d /srv/sam3d/checkpoints
```

Alternatively, copy an already extracted directory:

```bash
cp -a /path/to/sam3d_checkpoint/. /srv/sam3d/checkpoints/
```

Confirm that the pipeline configuration exists:

```bash
test -f /srv/sam3d/checkpoints/pipeline.yaml \
  && echo "SAM3D configuration found" \
  || echo "SAM3D configuration missing"
```

The default deployment expects:

```text
/srv/sam3d/checkpoints/pipeline.yaml
```

If the configuration has another filename or location, set `SAM3D_CONFIG` accordingly in the Compose configuration.

The checkpoint directory is mounted read-only into the container by default:

```text
/srv/sam3d/checkpoints → /checkpoints
```

---

## Build the Docker Image

Build the image directly on the workstation from the repository root:

```bash
docker build -t sam3d:latest .
```

Confirm that the image exists:

```bash
docker image inspect sam3d:latest
```

The Dockerfile uses a two-stage build:

- **Builder stage:** Installs Miniforge, creates the SAM3D Conda environment, and installs package dependencies.
- **Runtime stage:** Copies the prepared environment and application code into a CUDA runtime image.

### Optional registry workflow

A registry is not required when the image is built directly on the workstation.

If the image is built elsewhere, publish it to any OCI-compatible registry reachable from the workstation:

```bash
export IMAGE="registry.example.com/your-project/sam3d:latest"

docker login registry.example.com
docker build -t "${IMAGE}" .
docker push "${IMAGE}"
```

Pull it on the workstation:

```bash
docker pull "${IMAGE}"
```

The image can also be transferred without a registry:

```bash
docker save sam3d:latest | gzip > sam3d-image.tar.gz
```

Copy the archive to the workstation and import it:

```bash
gunzip -c sam3d-image.tar.gz | docker load
```

---

## Run with Docker Compose

Create `compose.yaml` in the repository root:

```yaml
services:
  sam3d:
    image: ${SAM3D_IMAGE:-sam3d:latest}
    build:
      context: .
      dockerfile: Dockerfile
    container_name: sam3d
    restart: unless-stopped

    gpus: all

    ports:
      - "${BIND_ADDRESS:-127.0.0.1}:${HOST_PORT:-8001}:8000"

    environment:
      SAM3D_CONFIG: ${SAM3D_CONFIG:-/checkpoints/pipeline.yaml}
      LIDRA_SKIP_INIT: ${LIDRA_SKIP_INIT:-1}
      SPARSE_ATTN_BACKEND: ${SPARSE_ATTN_BACKEND:-sdpa}
      ATTN_BACKEND: ${ATTN_BACKEND:-sdpa}

    volumes:
      - "${CHECKPOINT_PATH:-/srv/sam3d/checkpoints}:/checkpoints:ro"

    shm_size: "16gb"
```

Create a `.env` file for local configuration:

```dotenv
SAM3D_IMAGE=sam3d:latest

BIND_ADDRESS=127.0.0.1
HOST_PORT=8001

CHECKPOINT_PATH=/srv/sam3d/checkpoints
SAM3D_CONFIG=/checkpoints/pipeline.yaml

LIDRA_SKIP_INIT=1
SPARSE_ATTN_BACKEND=sdpa
ATTN_BACKEND=sdpa
```

> **Note:** `LIDRA_SKIP_INIT` is retained from the original deployment configuration. Verify that this spelling matches the environment variable expected by SAM3D. If the project expects a differently named variable, update it in both `.env` and `compose.yaml`.

Start the service and build the image if necessary:

```bash
docker compose up -d --build
```

Check the service status:

```bash
docker compose ps
```

Follow the application logs:

```bash
docker compose logs -f sam3d
```

Stop and remove the container:

```bash
docker compose down
```

Restart the service:

```bash
docker compose restart sam3d
```

---

## Access the Server

### Local access

With the default `BIND_ADDRESS=127.0.0.1`, the server is accessible only from the workstation:

```text
http://localhost:8001
```

Check the health endpoint:

```bash
curl http://localhost:8001/health
```

Open the generated FastAPI documentation:

```text
http://localhost:8001/docs
```

### Trusted network access

To allow clients on a trusted network to connect directly, update `.env`:

```dotenv
BIND_ADDRESS=0.0.0.0
HOST_PORT=8001
```

Recreate the container:

```bash
docker compose up -d
```

The server is then reachable through the workstation hostname or IP address:

```text
http://workstation.example.com:8001
```

If a host firewall is enabled, allow access only from the required network. For example, with UFW:

```bash
sudo ufw allow from 192.0.2.0/24 to any port 8001 proto tcp
```

Replace the example subnet with the trusted client network.

### Remote access through SSH

The recommended remote-access method is to keep the server bound to `127.0.0.1` and use a single SSH tunnel:

```bash
ssh -N \
  -L 8001:127.0.0.1:8001 \
  user@workstation.example.com
```

The server is then available on the client machine at:

```text
http://localhost:8001
```

Unlike the former cluster deployment, no login node, reverse tunnel, or second SSH hop is needed.

### Security notice

Do not expose the FastAPI development endpoint directly to the public internet unless authentication, TLS, request-size limits, rate limits, and appropriate network controls have been added.

For production access, use one of these approaches:

1. Bind to `127.0.0.1` and use SSH forwarding.
2. Bind to a trusted private network.
3. Place the service behind an authenticated HTTPS reverse proxy.

---

## Automatic Startup

The Compose configuration uses:

```yaml
restart: unless-stopped
```

This restarts the container after application failures, Docker restarts, and workstation reboots, unless it was stopped manually.

Enable Docker at system startup:

```bash
sudo systemctl enable --now docker
```

Verify the restart policy:

```bash
docker inspect \
  --format '{{.HostConfig.RestartPolicy.Name}}' \
  sam3d
```

The expected result is:

```text
unless-stopped
```

---

## Updating the Server

Pull the latest source changes:

```bash
git pull
```

Rebuild and recreate the service:

```bash
docker compose up -d --build
```

Follow the startup logs:

```bash
docker compose logs -f sam3d
```

After confirming that the new image works, remove unused image layers:

```bash
docker image prune
```

For stable deployments, prefer an immutable version or commit tag instead of `latest`:

```dotenv
SAM3D_IMAGE=sam3d:1.0.0
```

This simplifies release tracking and rollback.

---

## API Reference

### `GET /health`

Returns the current server status and whether the model is loaded.

**Example request:**

```bash
curl http://localhost:8001/health
```

**Example response:**

```json
{
  "ok": true,
  "model_loaded": true
}
```

---

### `POST /infer/glb`

Runs 3-D inference on an image and segmentation mask and returns a binary GLB model.

**Request:** `multipart/form-data`

| Field | Type | Description |
|---|---|---|
| `image` | File | Input RGB image in PNG or JPEG format |
| `mask` | File | Segmentation mask image |

**Response:**

| Property | Value |
|---|---|
| Content type | `model/gltf-binary` |
| Body | Binary GLB file |
| Suggested filename | `result.glb` |

**Example:**

```bash
curl -X POST http://localhost:8001/infer/glb \
  -F "image=@input.png" \
  -F "mask=@mask.png" \
  --output result.glb
```

For a remote workstation, replace `localhost` with the workstation hostname or IP address unless an SSH tunnel is being used.

**Error responses:**

| Status code | Possible cause |
|---|---|
| `500` | The inference pipeline failed |
| `500` | GLB export failed |
| `500` | The configured model file was not found during startup |

Consult the container logs for the underlying exception:

```bash
docker compose logs --tail=200 sam3d
```

---

## Configuration Reference

| Variable or setting | Default | Description |
|---|---|---|
| `SAM3D_IMAGE` | `sam3d:latest` | Docker image to run |
| `BIND_ADDRESS` | `127.0.0.1` | Workstation interface on which the service is exposed |
| `HOST_PORT` | `8001` | Port exposed by the workstation |
| Container port | `8000` | FastAPI/Uvicorn port inside the container |
| `CHECKPOINT_PATH` | `/srv/sam3d/checkpoints` | Checkpoint directory on the workstation |
| `SAM3D_CONFIG` | `/checkpoints/pipeline.yaml` | Hydra pipeline configuration inside the container |
| `LIDRA_SKIP_INIT` | `1` | Skips optional initialization steps; verify the variable name against SAM3D |
| `SPARSE_ATTN_BACKEND` | `sdpa` | Sparse attention backend |
| `ATTN_BACKEND` | `sdpa` | Attention backend |
| `shm_size` | `16gb` | Shared memory allocated to the container |
| Restart policy | `unless-stopped` | Restarts the service automatically |

The checkpoint mount is:

```text
/srv/sam3d/checkpoints → /checkpoints
```

The host path can be changed through `CHECKPOINT_PATH` without modifying `compose.yaml`.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `SAM3D_CONFIG not found` | Confirm that `pipeline.yaml` exists below `CHECKPOINT_PATH` and that `SAM3D_CONFIG` uses the correct in-container path |
| Model does not load | Inspect `docker compose logs sam3d` and verify that all checkpoint files are present and readable |
| Docker cannot access the GPU | Run the CUDA test container and verify the NVIDIA driver and NVIDIA Container Toolkit |
| CUDA or driver mismatch | Confirm that the host driver supports the CUDA runtime used by the image |
| Container fails to start | Run `docker compose ps` and inspect `docker compose logs --tail=200 sam3d` |
| Port `8001` is already in use | Set another `HOST_PORT` in `.env`, then recreate the container |
| Server works locally but not remotely | Check `BIND_ADDRESS`, firewall rules, routing, and the workstation hostname or IP address |
| SSH tunnel does not connect | Verify direct SSH access to the workstation and confirm that the backend is listening on workstation port `8001` |
| Health endpoint reports `model_loaded: false` | Check the configuration path, checkpoint files, GPU availability, and startup logs |
| Inference returns HTTP `500` | Inspect the server logs for model, memory, input-format, or GLB-export errors |
| GPU runs out of memory | Reduce input size or concurrency, use a smaller model, or install a GPU with more VRAM |
| System memory is exhausted | Reduce parallel requests or increase workstation RAM and swap capacity |
| Container is not restored after reboot | Enable the Docker service and confirm the `unless-stopped` restart policy |
| Image rebuild uses stale layers | Run `docker compose build --no-cache sam3d`, then recreate the service |
| Checkpoints are accidentally modified | Keep the `/checkpoints` volume mounted read-only with the `:ro` suffix |
