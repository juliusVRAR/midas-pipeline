using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

public class PoseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PoseResponseHandler responseHandler;
    [SerializeField] private PassthroughCameraCapture cameraCapture;
    [SerializeField] private ServerUtils serverUtils;
    [SerializeField] private ViewerTransformer viewerTransformer;

    [Serializable]
    public class PosePacket
    {
        public byte[] imageBytes;
        public List<List<Vector2>> vertices;
    }

    public void SendPose(Vector3[][] worldVertices)
    {
        PosePacket packet = cameraCapture.CaptureJpegAndPose(worldVertices);

        float[][][] vertices = ConvertToPayloadFormat(packet.vertices);

        StartCoroutine(serverUtils.PostImageMultipart(
            "pose/snap",
            packet.imageBytes,
            JsonConvert.SerializeObject(vertices),
            onSuccess: (resp) =>
            {
                responseHandler.HandlePose(resp);
                viewerTransformer.DefaultTransform();
            },
            onError: (err) =>
            {
                Debug.LogError($"[PoseController] Error sending pose data: {err}");
            }
        ));
    }

    private float[][][] ConvertToPayloadFormat(List<List<Vector2>> viewportVertices)
    {
        float[][][] vertices = new float[viewportVertices.Count][][];
        
        for (int i = 0; i < vertices.Length; i++)
        {
            List<Vector2> objVertices = viewportVertices[i];
            vertices[i] = new float[objVertices.Count][];
            
            for (int j = 0; j < objVertices.Count; j++)
                vertices[i][j] = new float[] { objVertices[j].x, objVertices[j].y };
        }
        
        return vertices;
    }
}