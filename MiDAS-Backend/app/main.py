"""FastAPI application entry point."""

import os
from contextlib import asynccontextmanager
from pathlib import Path

import torch
from fastapi import FastAPI

from app.routers import pose
from app.routers import sam3  # <-- new import


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
OUT_DIR = "./assets"
DATA_PATH = Path(OUT_DIR).resolve()
os.makedirs(OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Lifespan (replaces deprecated on_event)
# ---------------------------------------------------------------------------
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Startup and shutdown logic."""
    # Startup
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"[INIT] device={device}")

    sam3.init_sam3(device)

    yield  # Application runs here

    # Shutdown (cleanup if needed)
    print("[SHUTDOWN] Cleaning up...")


# ---------------------------------------------------------------------------
# App Factory
# ---------------------------------------------------------------------------
app = FastAPI(
    title="Segmentation API",
    lifespan=lifespan,
)

# Include routers
app.include_router(pose.router)
app.include_router(sam3.router)


# ---------------------------------------------------------------------------
# Health Check
# ---------------------------------------------------------------------------
@app.get("/health")
def health():
    """Return application health status."""
    return {
        "ok": True,
        "cuda_available": torch.cuda.is_available(),
        **sam3.get_sam3_status(),
    }