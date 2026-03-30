using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class Trapezoid : MaskableGraphic
{
    [Header("Trapezoid Settings")]
    [Range(0f, 1f)]
    public float bottomWidthRatio = 0.85f;
    
    [Header("Fill Settings")]
    [Range(0f, 1f)]
    public float fillAmount = 1f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        if (fillAmount <= 0f)
        {
            vh.Clear();
            return;
        }

        Rect r = rectTransform.rect;
        vh.Clear();
        
        Color32 color32 = color;
        
        float width = r.width;
        float yMin = r.yMin;
        float yMax = r.yMax;
        
        float targetBottomWidth = width * bottomWidthRatio;
        float inset = (width - targetBottomWidth) / 2f;
        
        float currentWidth = width * fillAmount;
        float centerX = r.center.x;
        float left = centerX - (currentWidth / 2f);
        float right = centerX + (currentWidth / 2f);
        
        float currentInset = inset * fillAmount;
        
        Vector2 vTL = new Vector2(left, yMax);
        Vector2 vTR = new Vector2(right, yMax);
        Vector2 vBR = new Vector2(right - currentInset, yMin);
        Vector2 vBL = new Vector2(left + currentInset, yMin);
        
        vh.AddVert(vBL, color32, new Vector2(0, 0));
        vh.AddVert(vTL, color32, new Vector2(0, 1));
        vh.AddVert(vTR, color32, new Vector2(1, 1));
        vh.AddVert(vBR, color32, new Vector2(1, 0));
        
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
    
    public void SetFillAmount(float amount)
    {
        fillAmount = Mathf.Clamp01(amount);
        SetVerticesDirty();
    }
}