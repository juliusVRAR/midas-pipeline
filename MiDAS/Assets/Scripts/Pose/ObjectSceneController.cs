using System.Collections.Generic;
using UnityEngine;

public class ObjectSceneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandAttachmentManager handAttachmentManager;

    private readonly List<ObjectDefinition> _objects = new();
    private int _nextId = 1;

    public IReadOnlyList<ObjectDefinition> Objects => _objects.AsReadOnly();

    /// <summary>
    /// Called by Sam3ResponseHandler after GLB is loaded.
    /// Extracts the first child (which holds MeshFilter + MeshRenderer)
    /// and registers it as the ObjectDefinition prefab.
    /// </summary>
    public void RegisterObject(GameObject glbRoot, byte[] glbBytes)
    {
        if (glbBytes == null || glbBytes.Length == 0)
        {
            Debug.LogError("[ObjectSceneController] Cannot register an object with empty GLB bytes.");
            Destroy(glbRoot);
            return;
        }
        
        MeshRenderer meshRenderer = glbRoot.GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError("[ObjectSceneController] No MeshRenderer found anywhere in GLB hierarchy.");
            Destroy(glbRoot);
            return;
        }

        GameObject meshObject = meshRenderer.gameObject;
        meshObject.transform.SetParent(null, true);
        meshObject.transform.localScale *= 100f;
        Debug.Log($"[ObjectSceneController] Mesh renderer bounds: {meshRenderer.bounds.size}");
        meshObject.SetActive(false); 

        // Root is now empty — destroy it
        Destroy(glbRoot);

        // Create a runtime ScriptableObject instance
        ObjectDefinition definition = ScriptableObject.CreateInstance<ObjectDefinition>();
        definition.Initialize(_nextId++, meshObject, glbBytes);

        _objects.Add(definition);
        handAttachmentManager.AddObject(definition);

        Debug.Log(
            $"[ObjectSceneController] Registered '{definition.ObjectName}' " +
            $"(id={definition.ObjectId}, GLB bytes={glbBytes.Length})."
        );
    }

    /// <summary>
    /// Removes object from scene and destroys its assets.
    /// </summary>
    public void DeleteObject(int id)
    {
        ObjectDefinition def = GetObject(id);
        if (def == null)
        {
            Debug.LogWarning($"[ObjectSceneController] Object id={id} not found for deletion.");
            return;
        }

        handAttachmentManager.RemoveObject(id);

        if (def.Prefab != null)
            Destroy(def.Prefab);

        _objects.Remove(def);
        Destroy(def);

        Debug.Log($"[ObjectSceneController] Deleted object id={id}.");
    }

    /// <summary>
    /// Marks object as saved. Extend to persist GLB bytes to disk.
    /// </summary>
    public void SaveObject(int id)
    {
        ObjectDefinition def = GetObject(id);
        if (def == null)
        {
            Debug.LogWarning($"[ObjectSceneController] Object id={id} not found for saving.");
            return;
        }

        // TODO: Persist to Application.persistentDataPath
        Debug.Log($"[ObjectSceneController] Save requested for '{def.ObjectName}' (id={id}).");
    }


    public ObjectDefinition GetObject(int id) => _objects.Find(o => o.ObjectId == id);
    public IReadOnlyList<ObjectDefinition> GetAllObjects() => _objects.AsReadOnly();
}