using System;
using Meta.WitAi.Json;
using UnityEngine;
using static PassthroughCameraCapture;
using static Sam3Response;
using static Sam3RequestBuilder;
using GLTFast;

public class Sam3ReponseHandler : MonoBehaviour
{

    [Header("3D Model")]
    [SerializeField] private ObjectSceneController objectSceneController;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Segmentation Display")]
    public Sam3ImageViewer imageViewer;
    public Sam3Prompt sam3Prompt;


    private GameObject _currentModel;

    public void HandleInitialSegmentation(string maskPng, RequestMeta metadata, ImageCaptureProperties captureProperties)
    {
        imageViewer.gameObject.SetActive(true);
        imageViewer.SetImage(captureProperties.imageTexture);
        imageViewer.Metadata = metadata;
        imageViewer.ImageBytes = captureProperties.imageBytes;
        sam3Prompt.InitiatePromptList(metadata.point_prompt);
        HandleSegmentation(maskPng);
    }

    public void HandleSegmentation(string maskPng)
    {
        SegmentResponse response;
        try
        {
            response = JsonConvert.DeserializeObject<SegmentResponse>(maskPng);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Sam3ResponseHandler] Error parsing segmentation response: {e.Message}\nRaw: {maskPng}");
            return;
        }

        Texture2D maskTexture = DecodeBase64PngToTexture(response.mask_png);
        imageViewer.SetMask(maskTexture);
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

    // ── 3D Model ─────────────────────────────────────────────────────────────

    public async void LoadGlbModel(byte[] glbBytes)
    {
        if (glbBytes == null || glbBytes.Length == 0)
        {
            Debug.LogError("[Sam3ResponseHandler] Received empty GLB bytes.");
            return;
        }

        if (loadingIndicator != null) loadingIndicator.SetActive(true);

        // Instantiate GLB root — ObjectSceneController will take ownership
        GameObject glbRoot = new GameObject("Sam3D_Import");
        var gltfImport = new GltfImport();

        try
        {
            bool success = await gltfImport.Load(glbBytes);

            if (success)
            {
                await gltfImport.InstantiateMainSceneAsync(glbRoot.transform);
                objectSceneController.RegisterObject(glbRoot, glbBytes);
                Debug.Log("[Sam3ResponseHandler] GLB loaded and registered to ObjectSceneController.");
            }
            else
            {
                Debug.LogError("[Sam3ResponseHandler] GLTFast failed to parse GLB.");
                Destroy(glbRoot);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Sam3ResponseHandler] Exception loading GLB: {e}");
            Destroy(glbRoot);
        }
        finally
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }
    }

}
