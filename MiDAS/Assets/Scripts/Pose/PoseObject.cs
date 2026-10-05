using TMPro;
using UnityEngine;

public class PoseObject : MonoBehaviour
{
    [Header("Identification")]
    [SerializeField] private int objectId;
    [SerializeField] private string objectName = "Unnamed Object";
    [SerializeField] private TMP_Text label;

    [Header("Prefab & Bounding Box")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private GameObject boundingBoxCube;

    private GameObject _instantiatedObject;
    private MeshRenderer _objectMeshRenderer;
    private MeshFilter _cubeMeshFilter;
    private MeshFilter _objectMeshFilter;
    private bool _isInitialized;

    public int ObjectId => objectId;
    public string ObjectName => objectName;
    public bool IsInitialized => _isInitialized;
    public Transform ObjectTransform => GetObjectTransform();

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        PoseObjectCoordinator.RequestRegistration(this);
    }

    private void OnDisable()
    {
        PoseObjectCoordinator.RequestUnregistration(this);
    }

    public void Initialize()
    {
        if (_isInitialized) return;

        if (objectPrefab == null)
        {
            Debug.LogError($"[PoseObject:{objectName}] Object Prefab not assigned!");
            return;
        }
        
        MeshRenderer prefabMeshRenderer = objectPrefab.GetComponent<MeshRenderer>();
        MeshFilter prefabMeshFilter = objectPrefab.GetComponent<MeshFilter>();
        if (prefabMeshRenderer == null)        {
            Debug.LogError($"[PoseObject:{objectName}] Object Prefab '{objectPrefab.name}' is missing a MeshRenderer component!");
        } else {
            Debug.Log($"[PoseObject:{objectName}] Prefab mesh renderer bounds: {prefabMeshRenderer.bounds.size}");
            Debug.Log($"[PoseObject:{objectName}] Prefab mesh filter bounds: {prefabMeshFilter.sharedMesh.bounds.size}");
        }

        _instantiatedObject = Instantiate(objectPrefab, transform);
        _instantiatedObject.SetActive(true);
        _instantiatedObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _objectMeshRenderer = _instantiatedObject.GetComponent<MeshRenderer>();
        _objectMeshFilter = _instantiatedObject.GetComponent<MeshFilter>();

        Debug.Log($"[PoseObject:{objectName}] Instantiated object '{_instantiatedObject.name}' with MeshFilter bounds: {_objectMeshFilter.sharedMesh.bounds.size}, with MeshRenderer bounds: {_objectMeshRenderer.bounds.size}, and scale: {_instantiatedObject.transform.localScale}");

         // Scale the object to a reasonable size (e.g., longest axis = 0.2 meters)

        if (boundingBoxCube == null)
        {
            Debug.LogError($"[PoseObject:{objectName}] Bounding Box Cube not assigned!");
            return;
        }

        MeshFilter objectMeshFilter = _instantiatedObject.GetComponent<MeshFilter>();
        if (objectMeshFilter != null && objectMeshFilter.sharedMesh != null)
        {
            boundingBoxCube.transform.localScale = objectMeshFilter.sharedMesh.bounds.size * objectMeshFilter.transform.localScale.x;
        } else
        {
            Debug.LogWarning($"[PoseObject:{objectName}] Object MeshFilter or sharedMesh not found. Bounding box will not be scaled.");
        }

        _cubeMeshFilter = boundingBoxCube.GetComponent<MeshFilter>();
        _objectMeshFilter = _instantiatedObject.GetComponent<MeshFilter>();
        _isInitialized = true;
    }

    /// <summary>
    /// Configure using an ObjectDefinition ScriptableObject
    /// </summary>
    public void Configure(ObjectDefinition objectDefinition)
    {
        if (objectDefinition == null)
        {
            Debug.LogError("[PoseObject] Cannot configure with null ObjectDefinition!");
            return;
        }

        Configure(objectDefinition.ObjectId, objectDefinition.ObjectName, objectDefinition.Prefab);
    }

    /// <summary>
    /// Configure with individual parameters
    /// </summary>
    public void Configure(int id, string name, GameObject prefab)
    {
        objectId = id;
        objectName = name;
        objectPrefab = prefab;
        label.text = objectName;

        // Re-initialize if already initialized
        if (_isInitialized)
        {
            CleanupInstantiatedObject();
            _isInitialized = false;
        }
        Initialize();
    }

    private void CleanupInstantiatedObject()
    {
        if (_instantiatedObject != null)
        {
            Destroy(_instantiatedObject);
            _instantiatedObject = null;
        }
    }

    public void SetObjectPrefab(GameObject newPrefab)
    {
        objectPrefab = newPrefab;
        Initialize();
    }

    public void SetObjectName(string name)
    {
        objectName = name;
    }

    public void SetObjectId(int id)
    {
        objectId = id;
    }

    public void ToggleObjectVisibility()
    {
        if (_objectMeshRenderer != null)
            _objectMeshRenderer.enabled = !_objectMeshRenderer.enabled;
    }

    public void SetObjectVisibility(bool visible)
    {
        if (_objectMeshRenderer != null)
            _objectMeshRenderer.enabled = visible;
    }

    public Vector3[] GetBoundingBoxVertices()
    {
        if (_objectMeshFilter == null)
        {
            Debug.LogWarning($"[PoseObject:{objectName}] Object MeshFilter not available.");
            return new Vector3[8];
        }

        Mesh mesh = _objectMeshFilter.sharedMesh;
        Bounds bounds = mesh.bounds;

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        // 8 corners in LOCAL space
        Vector3[] localCorners = new Vector3[8]
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x,  extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z)
        };

        // Convert to WORLD space
        Vector3[] worldCorners = new Vector3[8];
        Transform cubeTransform = _objectMeshFilter.transform;
        for (int i = 0; i < 8; i++)
            worldCorners[i] = cubeTransform.TransformPoint(localCorners[i]);

        return worldCorners;
    }

    public Transform GetObjectTransform()
    {
        return _instantiatedObject != null ? _instantiatedObject.transform : null;
    }

    public Bounds GetWorldMeshBounds()
    {
        if (_objectMeshRenderer != null)
            return _objectMeshRenderer.bounds;

        Debug.LogWarning($"[PoseObject:{objectName}] MeshRenderer not available for bounds calculation.");
        return new Bounds();
    }

    private void OnDestroy()
    {
        CleanupInstantiatedObject();
    }
}