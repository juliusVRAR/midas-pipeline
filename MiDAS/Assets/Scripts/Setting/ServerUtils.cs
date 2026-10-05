using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ServerUtils : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string _ipAddress = "192.168.2.147"; // IP address only
    [SerializeField] private int _port = 8000;
    [SerializeField] private int _sam3DPort = 8001;
    [SerializeField] private int timeoutSeconds = 30;

    public IEnumerator PostImageMultipart(
        string endpoint,
        byte[] imageBytes,
        string payloadJson,
        Action<string> onSuccess,
        Action<string> onError)
    {
        string url = $"{GetHttpAddress()}/{endpoint}";
        
        // Build multipart form
        WWWForm form = new();
        form.AddBinaryData("image", imageBytes, "frame.jpg", "image/jpeg");
        form.AddField("payload", payloadJson, Encoding.UTF8);

        using UnityWebRequest req = UnityWebRequest.Post(url, form);
        req.timeout = timeoutSeconds;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"HTTP error: {req.error}\nResponse: {req.downloadHandler.text}");
            yield break;
        }

        onSuccess?.Invoke(req.downloadHandler.text);
    }

    public IEnumerator PostTwoFilesMultipart(
        string endpoint,
        byte[] imageBytes,
        byte[] maskBytes,
        Action<byte[]> onSuccess,
        Action<string> onError)
    {
        string url = $"{GetSam3DHttpAddress()}/{endpoint}";

        List<IMultipartFormSection> form = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("image", imageBytes, "image.png", "image/png"),
            new MultipartFormFileSection("mask",  maskBytes,  "mask.png",  "image/png")
        };

        using UnityWebRequest req = UnityWebRequest.Post(url, form);
        req.downloadHandler = new DownloadHandlerBuffer();

        Debug.Log($"[ServerUtils] Sending 3D model creation request to {url} with image size {imageBytes.Length} bytes and mask size {maskBytes.Length} bytes.");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.data); // raw GLB bytes
        else
            onError?.Invoke(req.error);
    }

    public string GetHttpAddress()
    {
        return $"http://{_ipAddress}:{_port}";
    }

    public string GetSam3DHttpAddress()
    {
        return $"http://{_ipAddress}:{_sam3DPort}";
    }

    public string GetWebSocketAddress()
    {
        return $"ws://{_ipAddress}:{_port}";
    }

    public string GetIpAddress()
    {
        return _ipAddress;
    }

    public void SetIpAddress(string ipAddress)
    {
        _ipAddress = ipAddress?.Trim() ?? string.Empty;
    }

    public int GetPort()
    {
        return _port;
    }

    public void SetPort(int port)
    {
        _port = port;
    }
}