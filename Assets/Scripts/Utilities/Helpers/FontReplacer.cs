using TMPro;
using UnityEngine;

public class FontReplacer : MonoBehaviour
{
    [Header("Font mới để thay thế")]
    public TMP_FontAsset newFont;

    [ContextMenu("Replace All TMP Fonts In Children")]
    public void ReplaceFonts()
    {
        if (newFont == null)
        {
            Debug.LogWarning("Chưa gán font mới!");
            return;
        }

        var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        int count = 0;
        foreach (var tmp in tmps)
        {
            tmp.font = newFont;
            count++;
        }
    }
}
