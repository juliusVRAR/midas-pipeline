"""
WebSocket helper utilities.
"""

import json
from fastapi import WebSocket, WebSocketDisconnect
from fastapi.websockets import WebSocketState


async def send_json(ws: WebSocket, payload: dict) -> bool:
    """
    Send JSON message via WebSocket.
    
    Returns:
        False if connection is closed, True otherwise.
    """
    try:
        if hasattr(ws, "application_state") and ws.application_state != WebSocketState.CONNECTED:
            return False
        await ws.send_text(json.dumps(payload))
        return True
    except (WebSocketDisconnect, RuntimeError):
        return False