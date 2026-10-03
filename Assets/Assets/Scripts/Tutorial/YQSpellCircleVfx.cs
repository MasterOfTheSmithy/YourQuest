using UnityEngine;

// note: Combat supplies progress and lifetime; this presentation component cannot release spells or own another timer.
public sealed class YQSpellCircleVfx : MonoBehaviour
{
    private const int Segments = 64;
    private LineRenderer[] _rings;
    private Color _color;
    public int CircleCount => _rings != null ? _rings.Length : 0;

    public void Initialize(int circle, Color color, Material sharedMaterial)
    {
        _color = color;
        _rings = new LineRenderer[YQSpellCircleRules.ClampCircle(circle)];
        for (int ringIndex = 0; ringIndex < _rings.Length; ringIndex++)
        {
            // note: Concentric rings use the existing approved procedural glow and remain legible in either player perspective.
            GameObject ring = new GameObject("Circle_" + (ringIndex + 1));
            ring.transform.SetParent(transform, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = Segments + 1;
            line.widthMultiplier = 0.025f;
            line.sharedMaterial = sharedMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            float radius = 0.23f + ringIndex * 0.075f;
            for (int point = 0; point <= Segments; point++)
            {
                float angle = point / (float)Segments * Mathf.PI * 2f;
                line.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            _rings[ringIndex] = line;
        }
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (_rings == null) return;
        progress = Mathf.Clamp01(progress);
        for (int index = 0; index < _rings.Length; index++)
        {
            // note: Every circle remains countable; each fills in order and counter-rotates as the authoritative cast advances.
            float filled = Mathf.Clamp01(progress * _rings.Length - index);
            LineRenderer ring = _rings[index];
            Color tint = new Color(_color.r, _color.g, _color.b, Mathf.Lerp(0.18f, 0.95f, filled));
            ring.startColor = ring.endColor = tint;
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, progress * 120f * (index % 2 == 0 ? 1f : -1f));
            ring.widthMultiplier = Mathf.Lerp(0.018f, 0.036f, filled);
        }
    }
}
