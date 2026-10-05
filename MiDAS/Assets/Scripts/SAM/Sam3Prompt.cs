using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Sam3RequestBuilder;

public class Sam3Prompt : MonoBehaviour, IPointerClickHandler
{
    [Header("Marker Prefab")]
    [SerializeField] private RectTransform positivePointPrefab; 
    [SerializeField] private RectTransform negativePointPrefab; 

    [Header("Image Viewer")]
    [SerializeField] private RawImage image; 
    [SerializeField] private RectTransform promptContainer; 
    [SerializeField] private Sam3ImageViewer imageViewer;

    [Header("SAM3")]
    [SerializeField] private PointPrompt3D pointPrompt3D;
    [SerializeField] private Sam3Controller controller;
    
    private bool isPositivePrefab = true;
    private PointPromptList promptList = new();

    void OnDisable()
    {
        ClosePrompt();
    }


    public void InitiatePromptList(PointPrompt pointPrompt)
    {
        promptList = ToPointPromptList(pointPrompt);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!imageViewer.gameObject.activeSelf) return;

        if (!TryGetPixelCoord(eventData, image, out Vector2 pixel))
            return;
    }

    private bool TryGetPixelCoord(PointerEventData eventData, RawImage img, out Vector2 pixel)
    {
        pixel = default;

        RectTransform imgRectTransform = img.rectTransform;

        // Convert screen point to local point in the RawImage rect
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                imgRectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        Rect rect = imgRectTransform.rect;

        // Convert local coords to normalized (0..1)
        float u = (local.x - rect.xMin) / rect.width;
        float v = (local.y - rect.yMin) / rect.height;

        // Ensure inside
        if (u < 0 || u > 1 || v < 0 || v > 1) return false;

        // Convert to pixel coords in the underlying texture
        float x = u * img.texture.width;
        float y = (1f - v) * img.texture.height; // flip Y

        // Create marker as child of image
        RectTransform selectedPrefab = isPositivePrefab ? positivePointPrefab : negativePointPrefab;
        RectTransform marker = Instantiate(selectedPrefab, promptContainer);
        marker.anchoredPosition = local;

        pixel = new Vector2(x, y);
        int label = isPositivePrefab ? 1 : 0;

        // Add point to prompt
        promptList.AddPrompt(marker.gameObject, pixel, label);
        
        return true;
    }

    public void RefineSegmentation()
    {
        byte[] imageBytes = imageViewer.ImageBytes;

        PointPrompt newPointPrompt = ToPointPrompt(promptList);
        RequestMeta metadata = imageViewer.Metadata;
        metadata.point_prompt = newPointPrompt;

        controller.RefineSegmentation(metadata, imageBytes);

    }

    public void Create3DModel()
    {
        byte[] imageBytes = imageViewer.ImageBytes;
        byte[] maskBytes  = imageViewer.MaskBytes;

        if (maskBytes == null)
        {
            Debug.LogError("[Sam3Prompt] Mask bytes are null. Cannot create 3D model.");
            return;
        }

        controller.Create3DModel(imageBytes, maskBytes);
    }

    public void TogglePointPrefab(bool toggleValue)
    {
        isPositivePrefab = toggleValue;
    }

    public void ResetPrompt()
    {
        foreach(GameObject obj in promptList.pointObjects)
        {
            Destroy(obj);
        }

        promptList = new();
    }

    public void ClosePrompt()
    {
        ResetPrompt();
        imageViewer.gameObject.SetActive(false);
        pointPrompt3D.Reset3DPoint();
    }

    public PointPrompt ToPointPrompt(PointPromptList promptList)
    {
        List<Vector2> points = promptList.points;
        float[][] _points = new float[points.Count][];

        int[] _labels = promptList.labels.ToArray();

        for (int i = 0; i < points.Count; i++)
            _points[i] = new float[] { points[i].x, points[i].y };
        
        return new PointPrompt
        {
            points = _points,
            labels = _labels
        };
    }

    public PointPromptList ToPointPromptList(PointPrompt prompt)
    {
        float[][] _points = prompt.points;
        int[] _labels = prompt.labels;

        List<Vector2> points = new();
        List<int> labels = new();
        List<GameObject> pointObjects = new();
        
        for (int i = 0; i < _points.GetLength(0); i++)
        {
            Vector2 point = new(_points[i][0], _points[i][1]);
            int label = _labels[i];

            RectTransform selectedPrefab = label == 1 ? positivePointPrefab : negativePointPrefab;
            RectTransform marker = Instantiate(selectedPrefab, promptContainer);
            marker.anchoredPosition = new(point.x - (image.texture.width / 2), (image.texture.height / 2) - point.y);

            points.Add(point);
            labels.Add(label);
            pointObjects.Add(marker.gameObject);
        }
        
        return new PointPromptList
        {
            points = points,
            labels = labels,
            pointObjects = pointObjects
        };
    }

    [Serializable] public class PointPromptList
    {
        public List<Vector2> points;
        public List<int> labels;
        public List<GameObject> pointObjects;

        public PointPromptList()
        {
            points = new();
            labels = new();
            pointObjects = new();
        }

        public void AddPrompt(GameObject obj, Vector2 point, int label)
        {
            points.Add(point);
            labels.Add(label);
            pointObjects.Add(obj);
        }
    }
}
