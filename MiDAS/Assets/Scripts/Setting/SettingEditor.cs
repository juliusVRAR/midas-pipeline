using TMPro;
using UnityEngine;

public class SettingEditor : MonoBehaviour
{
    [SerializeField] private ServerUtils serverUtils;
    [SerializeField] private TMP_InputField inputField;

    void Start()
    {
        SetText(serverUtils.GetIpAddress());
    }

    public void SetText(string label)
    {
        inputField.text = label;
    }

    public void OnIpAddressChanged()
    {
        serverUtils.SetIpAddress(inputField.text);
    }
}