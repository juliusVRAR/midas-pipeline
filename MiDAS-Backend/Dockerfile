FROM pytorch/pytorch:2.7.0-cuda12.6-cudnn9-runtime

# System deps (git only needed if you choose Option B later)
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    ffmpeg \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Install SAM3
COPY external/sam3/sam3 /opt/sam3/sam3
COPY external/sam3/pyproject.toml /opt/sam3

RUN pip install --no-cache-dir -e "/opt/sam3"
RUN pip install --no-cache-dir -e "/opt/sam3[notebooks]"

# Python deps for your server
COPY requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r /app/requirements.txt

# Copy FastAPI server
COPY /app /app/app

#   -e SAM3_CHECKPOINT_PATH=/models/sam3_checkpoint.pt
ENV SAM3_CHECKPOINT_PATH="/models/sam3.pt"

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]