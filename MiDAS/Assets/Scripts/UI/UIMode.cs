// UIMode.cs
using UnityEngine;

[CreateAssetMenu(fileName = "UIMode", menuName = "UI/Mode")]
public class UIMode : ScriptableObject
{
    public string modeName;
    public Sprite icon;
    public int priority; // For ordering in UI
}