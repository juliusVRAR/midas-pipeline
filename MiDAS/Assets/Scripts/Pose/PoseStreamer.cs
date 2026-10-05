using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using static PoseController;
using static Sam3RequestBuilder;

public class PoseStreamer : MonoBehaviour
{
    [Header("Network")]
    public ServerUtils serverUtils;
    private string wsUrl;
    
    [Header("Objects")]
    [SerializeField] private ObjectSceneController objectSceneController;

    [Header("Camera")]
    public PassthroughCameraCapture cameraCapture;
    public PoseObjectCoordinator poseCoordinator;

    [Header("Streaming Mode")]
    [Tooltip("Enable to stream full 6DoF poses for BOP dataset")]
    public bool streamBOPFormat = true;
    public int sendFps = 2;
    public TMP_Text fpsText;

    [Header("Websocket")]
    private string sessionId;
    private bool isStreaming = false;

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private float _nextSendTime;
    private int _frameId;

    // Thread-safe queue for messages received from server
    private readonly ConcurrentQueue<string> _receivedMessages = new ConcurrentQueue<string>();
    private bool _isConnected = false;
    
    private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
    private TaskCompletionSource<bool> _completionReceived;
    

    public event Action<string> OnServerText;

    public bool IsConnected => _isConnected && _ws?.State == WebSocketState.Open;
    public bool IsStreaming => isStreaming && IsConnected;

    void Awake()
    {
        SetSendFps(sendFps);
    }

    void Update()
    {
        // Process received messages on main thread
        while (_receivedMessages.TryDequeue(out string msg))
        {
            Debug.Log($"[WS] Server message: {msg}");

            try
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(msg);

                if (data != null &&
                    data.TryGetValue("type", out object type) &&
                    type?.ToString() == "complete")
                {
                    _completionReceived?.TrySetResult(true);
                }

                if (data != null &&
                    data.TryGetValue("type", out object errorType) &&
                    errorType?.ToString() == "error")
                {
                    _completionReceived?.TrySetException(
                        new Exception($"Backend error: {msg}")
                    );
                }
            }
            catch
            {
            }

            OnServerText?.Invoke(msg);
        }

        // Handle frame streaming
        if (_ws == null) return;
        if (cameraCapture == null) return;
        if (!IsConnected || !isStreaming) return;

        if (Time.time >= _nextSendTime)
        {
            _nextSendTime = Time.time + (1f / Mathf.Max(1f, sendFps));
            if (streamBOPFormat)
                SendFrameWithPose();
            else
                SendFrame();
        }
    }

    void OnDestroy()
    {
        CloseWebSocketImmediate();
    }

    void OnApplicationQuit()
    {
        CloseWebSocketImmediate();
    }

    /// <summary>
    /// Starts WebSocket connection with timeout.
    /// Returns true if connection succeeds, false on timeout/error.
    /// </summary>
    public async Task<bool> StartWebSocket(float timeout = 10f)
    {
        if (isStreaming && IsConnected)
        {
            Debug.Log("[WS] Already connected and streaming");
            return true;
        }

        // Clean up any existing connection
        CloseWebSocketImmediate();

        // Reset state
        _isConnected = false;
        isStreaming = false;
        sessionId = Guid.NewGuid().ToString("N");
        _frameId = 0;

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        wsUrl = serverUtils.GetWebSocketAddress() + "/pose/video";
        Debug.Log($"[WS] Attempting to connect to: {wsUrl} with session ID: {sessionId}");

        Debug.Log($"[WS] Connecting to: {wsUrl}");

        try
        {
            // Connect with timeout
            using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout)))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token))
            {
                await _ws.ConnectAsync(new Uri(wsUrl), linkedCts.Token);
            }

            if (_ws.State != WebSocketState.Open)
            {
                Debug.LogError($"[WS] Connection failed. State: {_ws.State}");
                return false;
            }

            _isConnected = true;
            Debug.Log("[WS] Connected!");

            // Start receiving messages in background
            _ = ReceiveLoopAsync(_cts.Token);

            // Send start message
            var startMessage = new StartMessage
            {
                type = "start",
                session_id = sessionId,
                intrinsics = cameraCapture.GetIntrinsics(),
                fps = sendFps,
                bop_format = streamBOPFormat
            };

            string json = JsonConvert.SerializeObject(startMessage);
            await SendTextUnsafeAsync(json);

            isStreaming = true;
            Debug.Log($"[WS] Session started: {sessionId}");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.LogError($"[WS] Connection timeout after {timeout}s");
            CloseWebSocketImmediate();
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WS] Connection failed: {e.Message}");
            CloseWebSocketImmediate();
            return false;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[WS] Server initiated close");
                    _isConnected = false;
                    isStreaming = false;
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _receivedMessages.Enqueue(msg);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when closing
        }
        catch (Exception e)
        {
            Debug.LogError($"[WS] Receive error: {e.Message}");
            _isConnected = false;
            isStreaming = false;
        }
    }

    private async Task SendTextUnsafeAsync(string text)
    {
        if (_ws?.State != WebSocketState.Open) return;

        var bytes = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            _cts?.Token ?? CancellationToken.None
        );
    }

    private async Task SendBinaryUnsafeAsync(byte[] data)
    {
        if (_ws?.State != WebSocketState.Open) return;

        await _ws.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            true,
            _cts?.Token ?? CancellationToken.None
        );
    }

    public async void CloseWebSocket()
    {
        await StopStreamAndUploadModelsAsync();
    }
    
    public async Task StopStreamAndUploadModelsAsync()
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            CloseWebSocketImmediate();
            return;
        }

        // Stop Update() from creating new frame sends.
        isStreaming = false;

        try
        {
            // Waits for any already-sending frame, then sends model data.
            await UploadAllModelsAsync();

            _completionReceived = new TaskCompletionSource<bool>();

            await _sendGate.WaitAsync();
            try
            {
                await SendTextUnsafeAsync("{\"type\":\"stop\"}");
            }
            finally
            {
                _sendGate.Release();
            }

            using CancellationTokenSource timeout =
                new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await WaitWithTimeoutAsync(_completionReceived.Task, TimeSpan.FromSeconds(60));

            Debug.Log("[WS] Backend finalized BOP session successfully.");
        }
        catch (OperationCanceledException)
        {
            Debug.LogError("[WS] Timed out while waiting for backend finalization.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WS] Session finalization failed: {e}");
        }
        finally
        {
            await CloseWebSocketGracefullyAsync();
            CloseWebSocketImmediate();
        }
    }
    
    private static async Task<T> WaitWithTimeoutAsync<T>(
        Task<T> task,
        TimeSpan timeout)
    {
        Task timeoutTask = Task.Delay(timeout);

        Task completedTask = await Task.WhenAny(task, timeoutTask);

        if (completedTask != task)
        {
            throw new TimeoutException(
                $"Timed out after {timeout.TotalSeconds:F0} seconds."
            );
        }

        // Returns the result and propagates any exception from the original task.
        return await task;
    }

    private async Task CloseWebSocketGracefullyAsync()
    {
        if (_ws?.State != WebSocketState.Open)
            return;

        try
        {
            using CancellationTokenSource timeout =
                new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await _ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Capture complete",
                timeout.Token
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WS] Graceful close failed: {e.Message}");
        }
    }

    private void CloseWebSocketImmediate()
    {
        isStreaming = false;
        _isConnected = false;

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch { }

        try
        {
            _ws?.Dispose();
            _ws = null;
        }
        catch { }
    }
    
    private async Task UploadAllModelsAsync()
    {
        if (objectSceneController == null)
            throw new InvalidOperationException("[WS] ObjectSceneController is not assigned.");

        foreach (ObjectDefinition definition in objectSceneController.GetAllObjects())
        {
            if (definition == null)
                continue;

            if (definition.GlbBytes == null || definition.GlbBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[WS] '{definition.ObjectName}' has no original GLB bytes."
                );
            }

            PoseObject poseObject = poseCoordinator.GetPoseObject(definition.ObjectId);
            if (poseObject == null || poseObject.ObjectTransform == null)
            {
                throw new InvalidOperationException(
                    $"[WS] No active PoseObject for object id={definition.ObjectId}."
                );
            }

            Vector3 scale = poseObject.ObjectTransform.lossyScale;

            if (!IsUniformScale(scale))
            {
                throw new InvalidOperationException(
                    $"[WS] '{definition.ObjectName}' has non-uniform scale {scale}. " +
                    "BOP exports require a rigid, uniformly scaled object."
                );
            }

            ModelBeginMessage metadata = new ModelBeginMessage
            {
                obj_id = definition.ObjectId,
                obj_name = definition.ObjectName,
                byte_length = definition.GlbBytes.Length,
                glb_to_unity_scale = new[]
                {
                    scale.x,
                    scale.y,
                    scale.z
                }
            };

            // Keep header and GLB contiguous: no frame can be sent between them.
            await _sendGate.WaitAsync();

            try
            {
                await SendTextUnsafeAsync(JsonConvert.SerializeObject(metadata));
                await SendBinaryUnsafeAsync(definition.GlbBytes);
                await SendTextUnsafeAsync(JsonConvert.SerializeObject(
                    new ModelUploadedMessage { obj_id = definition.ObjectId }
                ));
            }
            finally
            {
                _sendGate.Release();
            }

            Debug.Log(
                $"[WS] Uploaded GLB for {definition.ObjectName}: " +
                $"{definition.GlbBytes.Length} bytes, scale={scale}"
            );
        }
    }

    private static bool IsUniformScale(Vector3 scale, float tolerance = 0.0001f)
    {
        return Mathf.Abs(scale.x - scale.y) <= tolerance &&
               Mathf.Abs(scale.y - scale.z) <= tolerance;
    }
    public void SetStreamingMode(bool bopFormat)
    {
        streamBOPFormat = bopFormat;
    }

    public void SetSendFps(float fps)
    {
        sendFps = (int)fps;
        fpsText.text = $"{sendFps} fps";
    }

    private async void SendFrameWithPose()
    {
        if (!IsConnected || _ws == null) return;

        var packet = cameraCapture.CaptureJpegWithPose();
        var objectTransforms = GetObjectTransformMessages();

        if (packet.imageBytes == null || packet.imageBytes.Length == 0) return;

        var frameMessage = new FrameMessageWithPose
        {
            frame_id = _frameId++,
            timestamp = Time.time,
            camera_pose = packet.cameraPose,
            objects = objectTransforms
        };

        string headerJson = JsonConvert.SerializeObject(frameMessage);
        byte[] headerBytes = Encoding.UTF8.GetBytes(headerJson);

        byte[] lenBytes = BitConverter.GetBytes(headerBytes.Length);
        byte[] payload = new byte[4 + headerBytes.Length + packet.imageBytes.Length];

        Buffer.BlockCopy(lenBytes, 0, payload, 0, 4);
        Buffer.BlockCopy(headerBytes, 0, payload, 4, headerBytes.Length);
        Buffer.BlockCopy(packet.imageBytes, 0, payload, 4 + headerBytes.Length, packet.imageBytes.Length);

        try
        {
            await SendBinaryUnsafeAsync(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Send failed: " + e.Message);
        }
    }

    private async void SendFrame()
    {
        if (!IsConnected || _ws == null) return;

        var worldVertices = poseCoordinator.GetAllPoses();
        PosePacket packet = cameraCapture.CaptureJpegAndPose(worldVertices);
        byte[] jpeg = packet.imageBytes;
        List<List<Vector2>> viewportVertices = packet.vertices;
        float[][][] vertices = new float[viewportVertices.Count][][];
        for (int i = 0; i < vertices.Length; i++)
        {
            List<Vector2> objVertices = viewportVertices[i];
            vertices[i] = new float[8][];
            for (int j = 0; j < objVertices.Count; j++)
                vertices[i][j] = new float[] { objVertices[j].x, objVertices[j].y };
        }

        if (packet == null || packet.imageBytes.Length == 0) return;

        var frameMessage = new FrameMessage
        {
            poses = vertices
        };

        string headerJson = JsonConvert.SerializeObject(frameMessage);
        byte[] headerBytes = Encoding.UTF8.GetBytes(headerJson);

        byte[] lenBytes = BitConverter.GetBytes(headerBytes.Length);
        byte[] payload = new byte[4 + headerBytes.Length + jpeg.Length];

        Buffer.BlockCopy(lenBytes, 0, payload, 0, 4);
        Buffer.BlockCopy(headerBytes, 0, payload, 4, headerBytes.Length);
        Buffer.BlockCopy(jpeg, 0, payload, 4 + headerBytes.Length, jpeg.Length);

        try
        {
            await SendBinaryUnsafeAsync(payload);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Send failed: " + e.Message);
        }
    }

    private List<ObjectTransformMessage> GetObjectTransformMessages()
    {
        var messages = new List<ObjectTransformMessage>();
        var objectTransforms = poseCoordinator.GetAllObjectTransforms();
        var poseVertices = poseCoordinator.GetAllPoses();

        for (int i = 0; i < objectTransforms.Count; i++)
        {
            var (objId, objName, objTransform) = objectTransforms[i];

            float[][] bboxWorld = null;
            if (i < poseVertices.Length && poseVertices[i] != null)
            {
                Vector3[] worldVerts = poseVertices[i];
                bboxWorld = new float[worldVerts.Length][];
                for (int j = 0; j < worldVerts.Length; j++)
                {
                    bboxWorld[j] = new float[] {
                        worldVerts[j].x,
                        worldVerts[j].y,
                        worldVerts[j].z
                    };
                }
            }

            messages.Add(new ObjectTransformMessage
            {
                obj_id = objId,
                obj_name = objName,
                position = new float[] {
                    objTransform.position.x,
                    objTransform.position.y,
                    objTransform.position.z
                },
                rotation = new float[] {
                    objTransform.rotation.x,
                    objTransform.rotation.y,
                    objTransform.rotation.z,
                    objTransform.rotation.w
                },
                scale = new float[] {
                    objTransform.lossyScale.x,
                    objTransform.lossyScale.y,
                    objTransform.lossyScale.z
                },
                bbox_world = bboxWorld
            });
        }

        return messages;
    }

    // ============ Serializable Classes ============

    [Serializable]
    public class FrameMessage
    {
        public float[][][] poses;
    }

    [Serializable]
    public class FrameMessageWithPose
    {
        public int frame_id;
        public float timestamp;
        public float[] camera_pose;
        public List<ObjectTransformMessage> objects;
    }

    [Serializable]
    public class ObjectTransformMessage
    {
        public int obj_id;
        public string obj_name;
        public float[] position;
        public float[] rotation;
        public float[] scale;
        public float[][] bbox_world;
    }

    [Serializable]
    public class StartMessage
    {
        public string type = "start";
        public string session_id;
        public Intrinsics intrinsics;
        public int fps;
        public bool bop_format;
    }
    
    [Serializable]
    public class ModelBeginMessage
    {
        public string type = "model_begin";
        public int obj_id;
        public string obj_name;
        public string format = "model/gltf-binary";
        public int byte_length;
        public float[] glb_to_unity_scale;
    }

    [Serializable]
    public class ModelUploadedMessage
    {
        public string type = "model_uploaded";
        public int obj_id;
    }
}