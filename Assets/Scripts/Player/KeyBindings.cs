using UnityEngine;
using UnityEngine.InputSystem;

public static class KeyBindings
{
    const string PrefsKey = "Mindscape.KeyBindings";

    static InputActionAsset _asset;

    public static InputActionAsset Asset
    {
        get
        {
            if (_asset == null)
            {
                _asset = Resources.Load<InputActionAsset>("GameControls");
                if (_asset == null)
                    Debug.LogError("GameControls.inputactions not found under a Resources folder.");
                else
                    LoadOverrides();
            }
            return _asset;
        }
    }

    public static InputActionMap Gameplay => Asset != null ? Asset.FindActionMap("Gameplay") : null;

    public static void LoadOverrides()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
            _asset.LoadBindingOverridesFromJson(json);
    }

    public static void SaveOverrides()
    {
        if (_asset == null) return;
        PlayerPrefs.SetString(PrefsKey, _asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetToDefaults()
    {
        if (_asset == null) return;
        _asset.RemoveAllBindingOverrides();
        SaveOverrides();
    }
}
