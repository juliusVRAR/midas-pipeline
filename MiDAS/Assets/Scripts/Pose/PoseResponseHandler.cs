using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public class PoseResponseHandler : MonoBehaviour
{
    public PoseViewer poseViewer;

    [Serializable]
    public class PoseResponse
    {
        public bool ok;
        public string img_png;
    }
    
    public void HandlePose(string maskPng)
    {
        PoseResponse response;
        try
        {
            response = JsonConvert.DeserializeObject<PoseResponse>(maskPng);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PoseResponseHandler] Error parsing pose response: {e.Message}\nRaw: {maskPng}");
            return;
        }

        Texture2D maskTexture = DecodeBase64PngToTexture(response.img_png);
        poseViewer.SetImage(maskTexture);
    }

    private Texture2D DecodeBase64PngToTexture(string b64)
    {
        if (string.IsNullOrEmpty(b64)) return null;

        byte[] pngBytes;
        try { pngBytes = Convert.FromBase64String(b64); }
        catch { return null; }

        Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, pngBytes))
        {
            return null;
        } 

        return tex;
    }
}
