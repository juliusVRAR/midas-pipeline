using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction.Gizmo;

public class TaskSceneController : MonoBehaviour
{
    [Header("Task Scenes")]
    [SerializeField] private List<TaskScene> taskScenes = new List<TaskScene>();

    [Header("References")]
    [SerializeField] private HandAttachmentManager attachmentManager;

    [Header("Settings")]
    [SerializeField] private bool loadFirstSceneOnStart = true;

    private int _currentSceneIndex = -1;

    public int CurrentSceneIndex => _currentSceneIndex;
    public TaskScene CurrentScene => _currentSceneIndex >= 0 && _currentSceneIndex < taskScenes.Count 
        ? taskScenes[_currentSceneIndex] 
        : null;
    public string CurrentSceneName => CurrentScene != null ? CurrentScene.TaskName : "None";
    public int TotalScenes => taskScenes.Count;

    // Events
    public event System.Action<int, TaskScene> OnSceneLoaded;
    public event System.Action OnSceneReset;

    private void Start()
    {
        ValidateReferences();

        if (loadFirstSceneOnStart && taskScenes.Count > 0)
        {
            LoadScene(0);
        }
    }

    private void ValidateReferences()
    {
        if (attachmentManager == null)
        {
            attachmentManager = FindFirstObjectByType<HandAttachmentManager>();
            if (attachmentManager == null)
            {
                Debug.LogError("[TaskSceneController] HandAttachmentManager not found!");
            }
        }

        if (taskScenes.Count == 0)
        {
            Debug.LogWarning("[TaskSceneController] No task scenes assigned!");
        }
    }

    /// <summary>
    /// Load a scene by index
    /// </summary>
    public void LoadScene(int index)
    {
        if (index == _currentSceneIndex)
        {
            Debug.Log($"[TaskSceneController] Scene {index} is already loaded.");
            return;
        }
        
        if (index < 0 || index >= taskScenes.Count)
        {
            Debug.LogWarning($"[TaskSceneController] Invalid scene index: {index}");
            return;
        }

        if (taskScenes[index] == null)
        {
            Debug.LogWarning($"[TaskSceneController] Scene at index {index} is null!");
            return;
        }

        _currentSceneIndex = index;
        TaskScene scene = taskScenes[index];

        attachmentManager.LoadTaskScene(scene);

        OnSceneLoaded?.Invoke(index, scene);
        Debug.Log($"[TaskSceneController] Loaded scene {index}: '{scene.TaskName}'");
    }

    /// <summary>
    /// Load Scene 1 (index 0)
    /// </summary>
    public void LoadScene1()
    {
        LoadScene(0);
    }

    /// <summary>
    /// Load Scene 2 (index 1)
    /// </summary>
    public void LoadScene2()
    {
        LoadScene(1);
    }

    /// <summary>
    /// Load Scene 3 (index 2)
    /// </summary>
    public void LoadScene3()
    {
        LoadScene(2);
    }

    /// <summary>
    /// Reset current scene - reattach all objects to hand
    /// </summary>
    public void ResetCurrentScene()
    {
        if (_currentSceneIndex < 0)
        {
            Debug.LogWarning("[TaskSceneController] No scene currently loaded!");
            return;
        }

        attachmentManager.ReattachAll();

        OnSceneReset?.Invoke();
        Debug.Log($"[TaskSceneController] Reset scene: '{CurrentSceneName}'");
    }

    /// <summary>
    /// Reload current scene (destroy and reinstantiate objects)
    /// </summary>
    public void ReloadCurrentScene()
    {
        if (_currentSceneIndex >= 0)
        {
            LoadScene(_currentSceneIndex);
        }
    }

    /// <summary>
    /// Load next scene (wraps around)
    /// </summary>
    public void LoadNextScene()
    {
        if (taskScenes.Count == 0) return;

        int nextIndex = (_currentSceneIndex + 1) % taskScenes.Count;
        LoadScene(nextIndex);
    }

    /// <summary>
    /// Load previous scene (wraps around)
    /// </summary>
    public void LoadPreviousScene()
    {
        if (taskScenes.Count == 0) return;

        int prevIndex = _currentSceneIndex <= 0 ? taskScenes.Count - 1 : _currentSceneIndex - 1;
        LoadScene(prevIndex);
    }

    /// <summary>
    /// Clear current scene
    /// </summary>
    public void ClearScene()
    {
        attachmentManager.ClearAllObjects();
        _currentSceneIndex = -1;
    }

    /// <summary>
    /// Add a task scene at runtime
    /// </summary>
    public void AddTaskScene(TaskScene scene)
    {
        if (scene != null && !taskScenes.Contains(scene))
        {
            taskScenes.Add(scene);
        }
    }

    /// <summary>
    /// Get task scene by index
    /// </summary>
    public TaskScene GetTaskScene(int index)
    {
        if (index >= 0 && index < taskScenes.Count)
        {
            return taskScenes[index];
        }
        return null;
    }
}