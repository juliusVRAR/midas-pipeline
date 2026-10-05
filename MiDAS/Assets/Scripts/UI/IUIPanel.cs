// IUIPanel.cs
using UnityEngine;

public interface IUIPanel
{
    UIMode Mode { get; }
    bool IsActive { get; }
    
    void Show();
    void Hide();
    void OnModeEnter();
    void OnModeExit();
}