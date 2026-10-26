using UnityEngine;

/// <summary>
/// Attach to any retail department model to register it as a heatmap influence point.
///
/// Two modes (toggle "Use Manual Values" in the Inspector):
///
///   Auto (default) — priorityLevel (1–6) drives both intensity and radius.
///     Higher level = more customers = brighter, slightly larger zone.
///     Multiple objects may share the same level.
///
///   Manual — developer sets exact radius and intensity values directly.
///     priorityLevel is still used to decide which department gets a shader
///     slot when more than 6 are present at the same time.
/// </summary>
[AddComponentMenu("Heinemann/RetailPointLinker")]
public class RetailPointLinker : MonoBehaviour
{
    [Tooltip("HeatmapController in the scene. Auto-found on Enable if left empty.")]
    public HeatmapController heatmap;

    [Header("Priority")]
    [Tooltip("Controls slot priority when > 6 departments are active simultaneously. " +
             "Also drives intensity/radius in Auto mode. Higher = more customers.")]
    [Min(1)] public int priorityLevel = 3;

    [Header("Manual Override")]
    [Tooltip("If enabled, the values below are used instead of the auto-calculated ones.")]
    public bool useManualValues = false;

    [Tooltip("Zone radius in world units. Active only when Use Manual Values is enabled.")]
    public float manualRadius = 1.0f;

    [Tooltip("Heat intensity (0 = invisible, 1 = maximum). Active only when Use Manual Values is enabled.")]
    [Range(0f, 1f)] public float manualIntensity = 0.5f;

    void OnEnable()
    {
        if (heatmap == null)
            heatmap = FindObjectOfType<HeatmapController>();

        heatmap?.Register(this);
    }

    void OnDisable()
    {
        heatmap?.Unregister(this);
    }
}
