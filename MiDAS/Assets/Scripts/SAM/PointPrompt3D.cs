using Meta.XR;
using UnityEngine;
using System.Collections;


public class PointPrompt3D : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Transform raySampleOrigin;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("SAM3 Interactor")]
    [SerializeField] private PassthroughCameraAccess cameraAccess;

    [SerializeField] private Sam3Controller controller;
    [SerializeField] private Sam3RequestBuilder requestBuilder;

    [Header("Prompt")]
    [SerializeField] private Transform cubeTransform;

    private Vector3? _lastHitPoint;
    private bool isPointSet = false;
    private EnvironmentRaycastManager _raycastManager;

    private void Start()
    {
        _raycastManager = gameObject.GetComponentInParent<EnvironmentRaycastManager>();
        StartCoroutine(WaitForCameraFeed());
    }

    void OnEnable()
    {
        SetupLineRendererAndCube();
    }

    void OnDisable()
    {
        isPointSet = false;
    }

    private IEnumerator WaitForCameraFeed()
    {
        while (cameraAccess && !cameraAccess.IsPlaying)
        {
            yield return null;
        }
    }

    void Update()
    {
        if (UIManager.Instance?.CurrentMode.modeName != "SAM")
        {
            return;
        }

        if (!isPointSet)
        {
            UpdateSamplingPoint();
        }

        if (OVRInput.GetUp(OVRInput.Button.One))
        {
            SetPoint();
        }

        if (OVRInput.GetUp(OVRInput.Button.Two))
        {
            controller.GetSegmentation();
        }
    }

    public void Reset3DPoint()
    {
        isPointSet = false;
        lineRenderer.enabled = true;
    }

    private void UpdateSamplingPoint()
    {
        Ray ray = new(raySampleOrigin.position, raySampleOrigin.forward);
        var hitSuccess = _raycastManager.Raycast(ray, out var hit);

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, ray.origin);
        lineRenderer.SetPosition(1, hitSuccess ? hit.point : ray.origin + ray.direction * 5f);
        _lastHitPoint = hitSuccess ? hit.point : null;

        PlaceCubeOnHitpoint();
    }

    private void PlaceCubeOnHitpoint()
    {
        cubeTransform.position = _lastHitPoint.Value;
        Vector3 dir = raySampleOrigin.position - cubeTransform.position;
        cubeTransform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void SetPoint()
    {
        if (_lastHitPoint == null || !cameraAccess || !cameraAccess.IsPlaying)
        {
            Debug.LogWarning("CubePrompt: Invalid sampling point or passthrough feed not ready.");
            return;
        }
        
        isPointSet = true;
        lineRenderer.enabled = false;
        requestBuilder.SetWorldPoint(_lastHitPoint.Value); 
    }

    private void SetupLineRendererAndCube()
    {
        if (!lineRenderer)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineRenderer.endWidth = 0.01f;

        cubeTransform.gameObject.SetActive(true);
    }
}

