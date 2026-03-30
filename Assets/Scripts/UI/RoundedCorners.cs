using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RoundCorners : MonoBehaviour, IMeshModifier
{
    [Header("Corner Settings")]
    public Vector4 cornerRadius = new Vector4(20, 20, 20, 20);
    [Range(4, 32)]
    public int cornerSegments = 8;

    private Graphic _graphic;

    private void OnEnable()
    {
        _graphic = GetComponent<Graphic>();
        if (_graphic != null)
            _graphic.SetVerticesDirty();
    }

    private void OnDisable()
    {
        if (_graphic != null)
            _graphic.SetVerticesDirty();
    }

    public void ModifyMesh(Mesh mesh)
    {
        using (var vh = new VertexHelper(mesh))
        {
            ModifyMesh(vh);
            vh.FillMesh(mesh);
        }
    }

    public void ModifyMesh(VertexHelper vh)
    {
        if (!enabled) return;
        
        Rect r = ((RectTransform)transform).rect;
        vh.Clear();

        float width = r.width;
        float height = r.height;

        Color32 color = _graphic != null ? _graphic.color : Color.white;
        
        float tl = Mathf.Min(cornerRadius.x, width / 2f, height / 2f);
        float tr = Mathf.Min(cornerRadius.y, width / 2f, height / 2f);
        float br = Mathf.Min(cornerRadius.z, width / 2f, height / 2f);
        float bl = Mathf.Min(cornerRadius.w, width / 2f, height / 2f);
        
        Vector2 center = r.center;
        vh.AddVert(center, color, Vector2.zero); // Index 0
        
        Vector2 tlCenter = new Vector2(r.xMin + tl, r.yMax - tl);
        Vector2 trCenter = new Vector2(r.xMax - tr, r.yMax - tr);
        Vector2 brCenter = new Vector2(r.xMax - br, r.yMin + br);
        Vector2 blCenter = new Vector2(r.xMin + bl, r.yMin + bl);
        
        AddArc(vh, trCenter, tr, 0, 90, color);
        AddArc(vh, tlCenter, tl, 90, 180, color);
        AddArc(vh, blCenter, bl, 180, 270, color);
        AddArc(vh, brCenter, br, 270, 360, color);
        
        int count = vh.currentVertCount;
        for (int i = 1; i < count; i++)
        {
            int next = (i == count - 1) ? 1 : i + 1;
            vh.AddTriangle(0, i, next);
        }
    }

    private void AddArc(VertexHelper vh, Vector2 center, float radius, float startAngle, float endAngle, Color32 color)
    {
        for (int i = 0; i <= cornerSegments; i++)
        {
            float t = (float)i / cornerSegments;
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(pos, color, Vector2.zero);
        }
    }

    private void AddRect(VertexHelper vh, Rect r, Color32 color)
    {
        vh.AddVert(new Vector2(r.xMin, r.yMin), color, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMin), color, Vector2.zero);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_graphic == null) _graphic = GetComponent<Graphic>();
        if (_graphic != null) _graphic.SetVerticesDirty();
    }
#endif
}
