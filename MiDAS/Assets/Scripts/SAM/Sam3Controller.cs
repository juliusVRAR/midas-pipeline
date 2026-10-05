using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using static PassthroughCameraCapture;
using static Sam3RequestBuilder;

public class Sam3Controller : MonoBehaviour
{
    [Header("Helper")]
    [SerializeField] private ServerUtils serverUtils;
    [SerializeField] private PassthroughCameraCapture cameraCapture;
    [SerializeField] private Sam3RequestBuilder requestBuilder;
    [SerializeField] private Sam3ReponseHandler responseHandler;


    public void GetSegmentation()
    {
        if (!cameraCapture.CaptureImage(out ImageCaptureProperties captureProperties))
        {
            return;
        }

        var packet = requestBuilder.BuildMeta(captureProperties);


        StartCoroutine(serverUtils.PostImageMultipart("sam3/segment", captureProperties.imageBytes, JsonConvert.SerializeObject(packet),
            onSuccess: (response) =>
            {
                responseHandler.HandleInitialSegmentation(response, packet, captureProperties);
                Debug.Log("[Sam3Controller] Initial segmentation completed successfully.");
            },
            onError: (err) =>
            {
                Debug.LogError($"[Sam3Controller] Error occurred while getting segmentation: {err}");
            }
        ));
    }

    public void RefineSegmentation(RequestMeta metadata, byte[] imageBytes)
    {
        StartCoroutine(serverUtils.PostImageMultipart("sam3/segment", imageBytes, JsonConvert.SerializeObject(metadata),
            onSuccess: (resp) =>
            {
                responseHandler.HandleSegmentation(resp);
                Debug.Log("[Sam3Controller] Segmentation refined successfully.");
            },
            onError: (err) =>
            {
                Debug.LogError($"[Sam3Controller] Error occurred while refining segmentation: {err}");
            }
        ));
    }

    public void Create3DModel(byte[] imageBytes, byte[] maskBytes)
    {
        StartCoroutine(serverUtils.PostTwoFilesMultipart(
            "infer/glb",
            imageBytes,
            maskBytes,
            onSuccess: (glbBytes) =>
            {
                responseHandler.LoadGlbModel(glbBytes);
                Debug.Log("[Sam3Controller] 3D model created successfully.");
            },
            onError: (err) =>
            {
                Debug.LogError($"[Sam3Controller] Error creating 3D model: {err}");
            }
        ));
    }
}
