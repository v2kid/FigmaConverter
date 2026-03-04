using UnityEngine;
using UnityEngine.UI;

public class ScaleIcon : MonoBehaviour
{
    public Vector2 size = new Vector2(56, 56);

    [ContextMenu("Scale")]
    public void Scale()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Image image = GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                image.SetNativeSize();
                rectTransform.ScaleIcon(size.x, size.y);
            }
        }
    }
    
}

public static class RectTransformExtensions
{
    public static void ScaleIcon(this RectTransform target, float refWidth, float refHeight)
        {
            float scaleFactorWidth = refWidth / target.sizeDelta.x;
            float scaleFactorHeight = refHeight / target.sizeDelta.y;


            // Use the smaller scale factor to ensure the icon fits within the reference dimensions
            float scaleFactor = Mathf.Min(scaleFactorWidth, scaleFactorHeight);

            target.sizeDelta *= scaleFactor;
        }
}
