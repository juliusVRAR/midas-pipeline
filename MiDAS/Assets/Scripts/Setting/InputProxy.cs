using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputProxy : MonoBehaviour
{
    public static InputProxy Instance { get; private set; }

    public TMP_Text _inputText;

    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        // Clean up reference when destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void UpdateText(string inputText)
    {
        _inputText.text = inputText;
    }

    public void ToggleProxy(bool value)
    {
        gameObject.SetActive(value);
    }
}