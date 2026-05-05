using UnityEngine;

/// <summary>
/// Attach to a GameObject with a Renderer (works with textured materials).
/// When enabled, adds an emissive glow on top of the existing texture.
/// When disabled, clears the emission.
/// 
/// Uses emission instead of base color because the Handle has a baked
/// texture — base-color tinting would multiply with the texture and
/// produce muddy results. Emission adds on top, so the glow stays clean.
/// 
/// Uses Renderer.material (per-instance copy), so the source asset is
/// NOT modified.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class HandleHighlight : MonoBehaviour
{
    [Header("Highlight")]
    [Tooltip("Base color of the emissive glow.")]
    public Color highlightColor = new Color(1f, 0.5f, 0f); // orange

    [Tooltip("Emission intensity. Higher = more obvious glow.")]
    [Range(0f, 5f)]
    public float intensity = 2f;

    private Renderer rend;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        if (rend == null) return;
        var mat = rend.material;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor(EmissionColorID, highlightColor * intensity);
    }

    void OnDisable()
    {
        if (rend == null) return;
        var mat = rend.material;
        mat.SetColor(EmissionColorID, Color.black);
        mat.DisableKeyword("_EMISSION");
    }
}