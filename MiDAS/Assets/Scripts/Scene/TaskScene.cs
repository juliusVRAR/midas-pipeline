using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Task Scene", menuName = "User Study/Task Scene")]
public class TaskScene : ScriptableObject
{
    [Header("Task Information")]
    [SerializeField] private string taskName;

    [Header("Objects")]
    [Tooltip("Object definitions containing prefab, id, and name")]
    [SerializeField] private List<ObjectDefinition> objectDefinitions = new List<ObjectDefinition>();

    public string TaskName => taskName;
    public IReadOnlyList<ObjectDefinition> ObjectDefinitions => objectDefinitions;

    private void OnValidate()
    {
        // Check for duplicate IDs
        HashSet<int> seenIds = new HashSet<int>();
        foreach (var objDef in objectDefinitions)
        {
            if (objDef == null) continue;

            if (!seenIds.Add(objDef.ObjectId))
            {
                Debug.LogWarning($"[TaskScene] '{taskName}': Duplicate object ID {objDef.ObjectId} detected!");
            }
        }
    }
}