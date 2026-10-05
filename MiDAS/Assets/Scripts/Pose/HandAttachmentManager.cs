using UnityEngine;
using System.Collections.Generic;
using Meta.XR.Acoustics;
using System;

public class HandAttachmentManager : MonoBehaviour
{
    [Header("Hand Reference")]
    [SerializeField] private Transform leftHandAnchor;

    [Header("Spawn Size")]
    [Tooltip("Target size in meters applied to the longest mesh bound axis at spawn")]
    [SerializeField] private float defaultSpawnSizeMeters = 0.05f; 

    [Header("Object Container")]
    [Tooltip("Container where task objects will be instantiated")]
    [SerializeField] private GameObject objectContainerPrefab;

    [Header("Arc Settings")]
    [SerializeField] private float arcRadius = 0.12f;
    [SerializeField] private float arcAngle = 120f;
    [SerializeField] private float arcTiltX = 45f;
    [SerializeField] private float heightAbovePalm = 0.05f;
    [SerializeField] private bool faceOutward = true;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private List<HandAttachable> _attachableObjects = new List<HandAttachable>();
    private Dictionary<int, TransformData> _objectIdToTransform = new Dictionary<int, TransformData>();
    private TaskScene _currentScene;

    public TaskScene CurrentScene => _currentScene;
    public int ObjectCount => _attachableObjects.Count;


    /// <summary>
    /// Load a task scene - destroys old objects and instantiates new ones
    /// </summary>
    public void LoadTaskScene(TaskScene taskScene)
    {
        if (taskScene == null)
        {
            Debug.LogWarning("[HandAttachmentManager] Cannot load null TaskScene!");
            return;
        }

        if (leftHandAnchor == null)
        {
            Debug.LogError("[HandAttachmentManager] Left Hand Anchor not assigned!");
            return;
        }

        // Clear existing objects
        ClearAllObjects();

        _currentScene = taskScene;

        // Instantiate new objects from object definitions
        InstantiateTaskObjects(taskScene);

        // Initialize positions
        InitializeObjectPositions();

        Debug.Log($"[HandAttachmentManager] Loaded task: '{taskScene.TaskName}' with {_attachableObjects.Count} objects");
    }

    /// <summary>
    /// Clear and destroy all current objects
    /// </summary>
    public void ClearAllObjects()
    {
        // Detach all first
        foreach (var attachable in _attachableObjects)
        {
            if (attachable != null)
            {
                if (!attachable.IsAttachedToHand)
                {
                    PoseObject poseObject = attachable.GetComponentInChildren<PoseObject>();
                    _objectIdToTransform.Add(poseObject.ObjectId, new TransformData(poseObject.transform));
                }
                Destroy(attachable.gameObject);
            }
        }
        _attachableObjects.Clear();

        _currentScene = null;
        Debug.Log("[HandAttachmentManager] Cleared all objects");
    }

    private void InstantiateTaskObjects(TaskScene taskScene)
    {
        foreach (var objectDefinition in taskScene.ObjectDefinitions)
        {
            if (objectDefinition == null)
            {
                Debug.LogWarning($"[HandAttachmentManager] Null ObjectDefinition in task '{taskScene.TaskName}'");
                continue;
            }

            if (objectDefinition.Prefab == null)
            {
                Debug.LogWarning($"[HandAttachmentManager] ObjectDefinition '{objectDefinition.ObjectName}' has null prefab");
                continue;
            }

            // Instantiate container
            GameObject container = Instantiate(objectContainerPrefab, transform);
            container.name = objectDefinition.ObjectName;

            // Configure PoseObject with id, name, and prefab
            PoseObject poseObject = container.GetComponentInChildren<PoseObject>();
            if (poseObject != null)
            {
                poseObject.Configure(objectDefinition);
            }
            else
            {
                Debug.LogWarning($"[HandAttachmentManager] Container missing PoseObject component for '{objectDefinition.ObjectName}'");
            }

            // Get or add HandAttachable
            HandAttachable attachable = container.GetComponent<HandAttachable>();
            if (attachable == null)
            {
                Debug.LogWarning($"[HandAttachmentManager] Container missing HandAttachable for '{objectDefinition.ObjectName}' - adding one");
                attachable = container.AddComponent<HandAttachable>();
            }
            attachable.SetDefaultSize(defaultSpawnSizeMeters);
            _attachableObjects.Add(attachable);
        }
    }

    private void InitializeObjectPositions()
    {
        if (_attachableObjects.Count == 0) return;

        Vector3[] positions = CalculateArcPositions();
        Vector3[] rotations = CalculateArcRotations();

        for (int i = 0; i < _attachableObjects.Count; i++)
        {
            if (_attachableObjects[i] != null)
            {
                _attachableObjects[i].Initialize(
                    leftHandAnchor,
                    positions[i],
                    rotations[i]
                );
                Debug.Log($"[HandAttachmentManager] Initialized '{_attachableObjects[i].name}' at position {positions[i]} with rotation {rotations[i]}");
            }

            PoseObject poseObject = _attachableObjects[i].GetComponentInChildren<PoseObject>();
            if (poseObject != null && _objectIdToTransform.TryGetValue(poseObject.ObjectId, out TransformData previousTransform))
            {
                // Apply previous position and rotation if available
                previousTransform.ApplyTo(poseObject.transform);
                _attachableObjects[i].DetachFromHand(); 
                _objectIdToTransform.Remove(poseObject.ObjectId);
                Debug.Log($"[HandAttachmentManager] Restored previous transform for '{poseObject.name}'");
            }
        }
    }

    /// <summary>
    /// Adds a single ObjectDefinition to the scene and refreshes the arc layout.
    /// Called by ObjectSceneController.RegisterObject().
    /// </summary>
    public void AddObject(ObjectDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[HandAttachmentManager] Cannot add null ObjectDefinition.");
            return;
        }

        if (objectContainerPrefab == null || leftHandAnchor == null)
        {
            Debug.LogError("[HandAttachmentManager] objectContainerPrefab or leftHandAnchor not assigned.");
            return;
        }

        // Instantiate container
        GameObject container = Instantiate(objectContainerPrefab, transform);
        container.name = definition.ObjectName;

        // Configure PoseObject
        PoseObject poseObject = container.GetComponentInChildren<PoseObject>();
        if (poseObject != null)
            poseObject.Configure(definition);
        else
            Debug.LogWarning($"[HandAttachmentManager] Container missing PoseObject for '{definition.ObjectName}'.");

        // Get or add HandAttachable
        HandAttachable attachable = container.GetComponent<HandAttachable>()
                                    ?? container.AddComponent<HandAttachable>();
        attachable.SetDefaultSize(defaultSpawnSizeMeters);
        _attachableObjects.Add(attachable);

        // Recalculate arc — update offsets for existing objects, initialize new one
        Vector3[] positions = CalculateArcPositions();
        Vector3[] rotations = CalculateArcRotations();

        for (int i = 0; i < _attachableObjects.Count - 1; i++)
            _attachableObjects[i]?.SetOffsets(positions[i], rotations[i]);

        int newIdx = _attachableObjects.Count - 1;
        attachable.Initialize(leftHandAnchor, positions[newIdx], rotations[newIdx]);

        Debug.Log($"[HandAttachmentManager] Added '{definition.ObjectName}' (id={definition.ObjectId}). Total: {_attachableObjects.Count}");
    }

    /// <summary>
    /// Removes a single object by id, destroys its container, and refreshes the arc layout.
    /// Called by ObjectSceneController.DeleteObject().
    /// </summary>
    public void RemoveObject(int id)
    {
        int idx = _attachableObjects.FindIndex(a =>
        {
            if (a == null) return false;
            PoseObject p = a.GetComponentInChildren<PoseObject>();
            return p != null && p.ObjectId == id;
        });

        if (idx < 0)
        {
            Debug.LogWarning($"[HandAttachmentManager] Object id={id} not found for removal.");
            return;
        }

        Destroy(_attachableObjects[idx].gameObject);
        _attachableObjects.RemoveAt(idx);

        RefreshLayout();
        Debug.Log($"[HandAttachmentManager] Removed object id={id}. Remaining: {_attachableObjects.Count}");
    }

    private Vector3[] CalculateArcPositions()
    {
        int count = _attachableObjects.Count;
        Vector3[] positions = new Vector3[count];

        float startAngle = -arcAngle / 2f;
        float angleStep = count > 1 ? arcAngle / (count - 1) : 0f;

        Quaternion tiltRotation = Quaternion.Euler(arcTiltX, 0f, 0f);

        for (int i = 0; i < count; i++)
        {
            float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;

            Vector3 basePosition = new Vector3(
                Mathf.Sin(angle) * arcRadius,
                heightAbovePalm,
                Mathf.Cos(angle) * arcRadius
            );

            positions[i] = tiltRotation * basePosition;
        }

        return positions;
    }

    private Vector3[] CalculateArcRotations()
    {
        int count = _attachableObjects.Count;
        Vector3[] rotations = new Vector3[count];

        float startAngle = -arcAngle / 2f;
        float angleStep = count > 1 ? arcAngle / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            if (faceOutward)
            {
                float yRotation = startAngle + angleStep * i;
                rotations[i] = new Vector3(-45f, yRotation, 0f);
            }
            else
            {
                rotations[i] = new Vector3(-90f, 0f, 0f);
            }
        }

        return rotations;
    }

    public void ToggleAttachedObjectsVisibility(bool isVisible)
    {
        foreach (var attachable in _attachableObjects)
        {
            if (attachable != null && attachable.IsAttachedToHand)
            {
                attachable.gameObject.SetActive(isVisible);
            }
        }
    }

    /// <summary>
    /// Recalculate and apply positions
    /// </summary>
    [ContextMenu("Refresh Layout")]
    public void RefreshLayout()
    {
        if (_attachableObjects.Count == 0) return;

        Vector3[] positions = CalculateArcPositions();
        Vector3[] rotations = CalculateArcRotations();

        for (int i = 0; i < _attachableObjects.Count; i++)
        {
            if (_attachableObjects[i] != null)
            {
                _attachableObjects[i].SetOffsets(positions[i], rotations[i]);
            }
        }
    }

    /// <summary>
    /// Reattach all detached objects to hand
    /// </summary>
    [ContextMenu("Reattach All")]
    public void ReattachAll()
    {
        int reattachedCount = 0;
        foreach (var obj in _attachableObjects)
        {
            if (obj != null)
            {
                PoseObject poseObject = obj.GetComponentInChildren<PoseObject>();
                if (poseObject != null)
                {
                    _objectIdToTransform.Remove(poseObject.ObjectId); 
                }
                obj.AttachToHand();
                reattachedCount++;
            }
        }
        Debug.Log($"[HandAttachmentManager] Reattached {reattachedCount} objects");
    }

    /// <summary>
    /// Reload current scene (destroy and reinstantiate)
    /// </summary>
    public void ReloadCurrentScene()
    {
        if (_currentScene != null)
        {
            LoadTaskScene(_currentScene);
        }
    }

    /// <summary>
    /// Get list of all current attachable objects
    /// </summary>
    public IReadOnlyList<HandAttachable> GetAttachableObjects()
    {
        return _attachableObjects.AsReadOnly();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || leftHandAnchor == null) return;

        Gizmos.color = Color.cyan;

        int segments = 20;
        float startAngle = -arcAngle / 2f;
        Quaternion tiltRotation = Quaternion.Euler(arcTiltX, 0f, 0f);

        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (startAngle + (arcAngle / segments) * i) * Mathf.Deg2Rad;
            Vector3 localPoint = new Vector3(
                Mathf.Sin(angle) * arcRadius,
                heightAbovePalm,
                Mathf.Cos(angle) * arcRadius
            );

            localPoint = tiltRotation * localPoint;

            Vector3 worldPoint = leftHandAnchor.TransformPoint(localPoint);

            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, worldPoint);
            }

            prevPoint = worldPoint;
        }

        // Draw object positions
        if (_attachableObjects.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Vector3[] positions = CalculateArcPositions();
            foreach (var pos in positions)
            {
                Vector3 worldPos = leftHandAnchor.TransformPoint(pos);
                Gizmos.DrawWireSphere(worldPos, 0.02f);
            }
        }
    }

    [Serializable]
    public struct TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;

        public TransformData(Transform t)
        {
            position = t.position;
            rotation = t.rotation;
            localScale = t.localScale;
        }

        // Optional: Apply back to a transform
        public void ApplyTo(Transform t)
        {
            t.position = position;
            t.rotation = rotation;
            t.localScale = localScale;
        }
    }
}