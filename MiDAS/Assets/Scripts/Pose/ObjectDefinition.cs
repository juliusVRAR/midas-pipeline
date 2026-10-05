using UnityEngine;

[CreateAssetMenu(fileName = "New Object Definition", menuName = "User Study/Object Definition")]
public class ObjectDefinition : ScriptableObject
{
    [Header("Identification")]
    [SerializeField] private int objectId;

    [Header("Prefab")]
    [Tooltip("Prefab must have MeshFilter and MeshRenderer components")]
    [SerializeField] private GameObject prefab;

    private byte[] _glbBytes;
    
    public int ObjectId => objectId;
    public string ObjectName => $"Object #{objectId}";
    public GameObject Prefab => prefab;
    public byte[] GlbBytes => _glbBytes;

    /// <summary>
    /// Used at runtime to populate a ScriptableObject.CreateInstance() with GLB data.
    /// </summary>
    public void Initialize(int id, GameObject glbRoot, byte[] glbBytes)
    {
        objectId = id;
        prefab = glbRoot;
        _glbBytes = glbBytes;
    }

    private void OnValidate()
    {
        if (prefab != null)
        {
            if (prefab.GetComponent<MeshFilter>() == null || prefab.GetComponent<MeshRenderer>() == null)
            {
                Debug.LogWarning($"[ObjectDefinition] '{ObjectName}': Prefab '{prefab.name}' is missing MeshFilter or MeshRenderer!");
            }
        }
    }
}