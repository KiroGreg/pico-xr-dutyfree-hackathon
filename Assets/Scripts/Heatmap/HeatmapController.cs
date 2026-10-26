using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the interactive floor heatmap for the Gebr. Heinemann airport retail demo.
///
/// Departments register themselves via RetailPointLinker.OnEnable → Register().
/// Each linker occupies one of 6 shader slots. If more than 6 linkers are present,
/// the 6 highest-priority ones get slots; the rest are queued and take over a slot
/// when a higher-priority linker unregisters.
///
/// priorityLevel (1–6) on each linker maps to both intensity and radius:
///   higher level  →  brighter glow, slightly larger halo around the department.
/// </summary>
[AddComponentMenu("Heinemann/HeatmapController")]
public class HeatmapController : MonoBehaviour
{
    private const int MAX_POINTS = 6;
    private const int MAX_FLOWS  = 6;

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Target")]
    [Tooltip("Renderer on the floor plane that uses the Heatmap material.")]
    public Renderer targetRenderer;

    [Header("Interpolation")]
    [Range(1f, 12f)] public float lerpSpeed = 3f;

    [Header("Noise")]
    [Range(0f,  0.3f)] public float noiseStrength = 0.05f;
    [Range(0.5f, 10f)] public float noiseScale    = 3.0f;

    [Header("Flow Streams")]
    [Range(0.05f, 2f)] public float flowWidth = 0.3f;
    [Range(0.1f,  5f)] public float flowSpeed = 1.5f;

    // ─── Slot management ──────────────────────────────────────────────────────

    // _slots[i] holds the linker currently occupying shader slot i (null = empty).
    private readonly RetailPointLinker[] _slots = new RetailPointLinker[MAX_POINTS];

    // Linkers that arrived when all slots were full; waited for a free slot.
    private readonly List<RetailPointLinker> _overflow = new List<RetailPointLinker>();

    // ─── Per-slot interpolation state ─────────────────────────────────────────

    private readonly Vector3[] _currentPos       = new Vector3[MAX_POINTS];
    private readonly float[]   _currentRadius    = new float[MAX_POINTS];
    private readonly float[]   _currentIntensity = new float[MAX_POINTS];

    private readonly Vector3[] _targetPos        = new Vector3[MAX_POINTS];
    private readonly float[]   _targetRadius     = new float[MAX_POINTS];
    private readonly float[]   _targetIntensity  = new float[MAX_POINTS];
    private readonly float[]   _baseIntensity    = new float[MAX_POINTS]; // without boost

    // Prevents LateUpdate from clobbering _targetIntensity while a pulse boost is active.
    private readonly bool[]    _boostActive      = new bool[MAX_POINTS];

    // ─── Flow state ───────────────────────────────────────────────────────────

    private readonly int[] _flowStart = new int[MAX_FLOWS];
    private readonly int[] _flowEnd   = new int[MAX_FLOWS];
    private int            _activeFlows;

    // ─── Shader upload buffers (pre-allocated, no GC per frame) ───────────────

    private readonly Vector4[] _posBuffer       = new Vector4[MAX_POINTS];
    private readonly float[]   _intensityBuffer = new float[MAX_POINTS];
    private readonly Vector4[] _flowStartBuf    = new Vector4[MAX_FLOWS];
    private readonly Vector4[] _flowEndBuf      = new Vector4[MAX_FLOWS];

    private MaterialPropertyBlock _mpb;

    // Cached shader property IDs
    private static readonly int ID_Positions     = Shader.PropertyToID("_HeatmapPositions");
    private static readonly int ID_Intensities   = Shader.PropertyToID("_HeatmapIntensities");
    private static readonly int ID_Count         = Shader.PropertyToID("_HeatmapCount");
    private static readonly int ID_FlowStarts    = Shader.PropertyToID("_FlowStarts");
    private static readonly int ID_FlowEnds      = Shader.PropertyToID("_FlowEnds");
    private static readonly int ID_FlowCount     = Shader.PropertyToID("_FlowCount");
    private static readonly int ID_FlowWidth     = Shader.PropertyToID("_FlowWidth");
    private static readonly int ID_FlowSpeed     = Shader.PropertyToID("_FlowSpeed");
    private static readonly int ID_Time          = Shader.PropertyToID("_HeatmapTime");
    private static readonly int ID_NoiseStrength = Shader.PropertyToID("_NoiseStrength");
    private static readonly int ID_NoiseScale    = Shader.PropertyToID("_NoiseScale");

    // Scenario presets (optional manual mode, used when no linkers are present)
    private Dictionary<string, HeatPointConfig[]> _scenarios;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        InitScenarios();
    }

    void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime); // framerate-independent

        for (int i = 0; i < MAX_POINTS; i++)
        {
            // Pull live values from registered linker every frame so Inspector
            // changes (position, radius, intensity) are reflected immediately.
            if (_slots[i] != null)
            {
                _targetPos[i]    = _slots[i].transform.position;
                _targetRadius[i] = GetRadius(_slots[i]);

                // Only update base/target intensity when no boost coroutine is running.
                _baseIntensity[i] = GetBaseIntensity(_slots[i]);
                if (!_boostActive[i])
                    _targetIntensity[i] = _baseIntensity[i];
            }

            _currentPos[i]       = Vector3.Lerp(_currentPos[i],      _targetPos[i],      t);
            _currentRadius[i]    = Mathf.Lerp(_currentRadius[i],     _targetRadius[i],   t);
            _currentIntensity[i] = Mathf.Lerp(_currentIntensity[i],  _targetIntensity[i],t);

            _posBuffer[i]       = new Vector4(_currentPos[i].x, _currentPos[i].y, _currentPos[i].z, _currentRadius[i]);
            _intensityBuffer[i] = _currentIntensity[i];
        }

        for (int f = 0; f < _activeFlows; f++)
        {
            _flowStartBuf[f] = _posBuffer[_flowStart[f]];
            _flowEndBuf[f]   = _posBuffer[_flowEnd[f]];
        }

        targetRenderer.GetPropertyBlock(_mpb);
        _mpb.SetVectorArray(ID_Positions,   _posBuffer);
        _mpb.SetFloatArray(ID_Intensities,  _intensityBuffer);
        _mpb.SetInt(ID_Count,               MAX_POINTS);
        _mpb.SetVectorArray(ID_FlowStarts,  _flowStartBuf);
        _mpb.SetVectorArray(ID_FlowEnds,    _flowEndBuf);
        _mpb.SetInt(ID_FlowCount,           _activeFlows);
        _mpb.SetFloat(ID_FlowWidth,         flowWidth);
        _mpb.SetFloat(ID_FlowSpeed,         flowSpeed);
        _mpb.SetFloat(ID_Time,              Time.time);
        _mpb.SetFloat(ID_NoiseStrength,     noiseStrength);
        _mpb.SetFloat(ID_NoiseScale,        noiseScale);
        targetRenderer.SetPropertyBlock(_mpb);
    }

    // ─── Registration API (called by RetailPointLinker) ───────────────────────

    public void Register(RetailPointLinker linker)
    {
        // Ignore if already registered
        for (int i = 0; i < MAX_POINTS; i++)
            if (_slots[i] == linker) return;

        // Find an empty slot
        int freeSlot = FindEmptySlot();
        if (freeSlot >= 0)
        {
            AssignToSlot(freeSlot, linker);
            return;
        }

        // All slots full: try to evict the lowest-priority occupied slot
        int lowestSlot = FindLowestPrioritySlot();
        if (_slots[lowestSlot].priorityLevel < linker.priorityLevel)
        {
            _overflow.Add(_slots[lowestSlot]); // evicted linker goes to overflow
            AssignToSlot(lowestSlot, linker);
        }
        else
        {
            _overflow.Add(linker); // wait until a slot opens up
        }
    }

    public void Unregister(RetailPointLinker linker)
    {
        for (int i = 0; i < MAX_POINTS; i++)
        {
            if (_slots[i] != linker) continue;

            _slots[i]           = null;
            _targetIntensity[i] = 0f;
            _baseIntensity[i]   = 0f;

            // Promote the highest-priority overflow linker into this slot
            RetailPointLinker next = PopHighestOverflow();
            if (next != null)
                AssignToSlot(i, next);

            RebuildFlows();
            return;
        }

        // Not in a slot — remove from overflow queue
        _overflow.Remove(linker);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Manually moves a specific slot's target position (e.g., for non-linker scripting).</summary>
    public void UpdatePointPosition(int slotIndex, Vector3 newPos)
    {
        if (!IsValidIndex(slotIndex)) return;
        _targetPos[slotIndex] = newPos;
    }

    /// <summary>
    /// Temporarily spikes the intensity of a specific linker's slot, then decays back.
    /// Useful for interaction highlights when a customer picks up a product.
    /// </summary>
    public void TriggerPulseBoost(RetailPointLinker linker, float boostIntensity, float duration)
    {
        int slot = FindSlotOf(linker);
        if (slot < 0) return;
        StartCoroutine(PulseBoostRoutine(slot, boostIntensity, duration));
    }

    /// <summary>Pulse boost by slot index (0–5).</summary>
    public void TriggerPulseBoost(int slotIndex, float boostIntensity, float duration)
    {
        if (!IsValidIndex(slotIndex)) return;
        StartCoroutine(PulseBoostRoutine(slotIndex, boostIntensity, duration));
    }

    /// <summary>
    /// Manual scenario mode — bypasses linkers and sets all 6 slots to fixed positions.
    /// Supported: "NormalFlow", "AsiaFlight", "DelayCrisis".
    /// Call Register() on any linker afterward to return to live mode.
    /// </summary>
    public void SetAirportScenario(string scenarioName)
    {
        if (!_scenarios.TryGetValue(scenarioName, out HeatPointConfig[] configs))
        {
            Debug.LogWarning($"[HeatmapController] Unknown scenario: '{scenarioName}'");
            return;
        }

        for (int i = 0; i < MAX_POINTS; i++)
        {
            _slots[i] = null; // detach linkers so positions are manually driven
            if (i < configs.Length)
            {
                _targetPos[i]       = configs[i].position;
                _targetRadius[i]    = configs[i].radius;
                _targetIntensity[i] = configs[i].intensity;
                _baseIntensity[i]   = configs[i].intensity;

                if (_currentRadius[i] == 0f)
                {
                    _currentPos[i]       = configs[i].position;
                    _currentRadius[i]    = configs[i].radius;
                    _currentIntensity[i] = configs[i].intensity;
                }
            }
            else
            {
                _targetIntensity[i] = 0f;
                _baseIntensity[i]   = 0f;
            }
        }

        RebuildFlows();
    }

    // ─── Value resolution (manual override vs. priority auto) ────────────────

    /// <summary>Returns the effective radius for a linker — manual or auto.</summary>
    private static float GetRadius(RetailPointLinker linker)
    {
        return linker.useManualValues ? linker.manualRadius : PriorityToRadius(linker.priorityLevel);
    }

    /// <summary>Returns the effective base intensity for a linker — manual or auto.</summary>
    private static float GetBaseIntensity(RetailPointLinker linker)
    {
        return linker.useManualValues ? linker.manualIntensity : PriorityToIntensity(linker.priorityLevel);
    }

    // ─── Priority → value mappings (used in Auto mode) ───────────────────────

    /// <summary>Level 1 = moderate glow (0.55), level 6+ asymptotically approaches 1.00.</summary>
    private static float PriorityToIntensity(int level)
    {
        // Logarithmic curve: grows fast up to level ~6, then flattens toward 1.0.
        float t = 1f - Mathf.Exp(-(level - 1) * 0.35f);
        return Mathf.Lerp(0.55f, 1.00f, t);
    }

    /// <summary>Level 1 = 0.90 m radius, higher levels approach 1.60 m asymptotically.</summary>
    private static float PriorityToRadius(int level)
    {
        float t = 1f - Mathf.Exp(-(level - 1) * 0.35f);
        return Mathf.Lerp(0.90f, 1.60f, t);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void AssignToSlot(int slot, RetailPointLinker linker)
    {
        _slots[slot]           = linker;
        _boostActive[slot]     = false;
        _baseIntensity[slot]   = GetBaseIntensity(linker);
        _targetIntensity[slot] = _baseIntensity[slot];
        _targetRadius[slot]    = GetRadius(linker);

        // Snap position so the zone appears immediately at the model (no lerp from zero)
        _currentPos[slot]      = linker.transform.position;
        _targetPos[slot]       = linker.transform.position;

        RebuildFlows();
    }

    private IEnumerator PulseBoostRoutine(int slot, float boostIntensity, float duration)
    {
        _boostActive[slot] = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = 1f - Mathf.Pow(elapsed / duration, 2f); // ease-out quad
            _targetIntensity[slot] = _baseIntensity[slot] + boostIntensity * eased;
            yield return null;
        }
        _boostActive[slot] = false;
        _targetIntensity[slot] = _baseIntensity[slot];
    }

    /// <summary>
    /// Flows connect occupied slots ordered from highest to lowest priority,
    /// creating a visual "customer route" from the most to least popular department.
    /// </summary>
    private void RebuildFlows()
    {
        // Collect occupied slot indices sorted by priority descending
        var occupied = new List<int>(MAX_POINTS);
        for (int i = 0; i < MAX_POINTS; i++)
        {
            if (_baseIntensity[i] > 0f)
                occupied.Add(i);
        }

        // Sort by intensity descending (highest priority first)
        occupied.Sort((a, b) => _baseIntensity[b].CompareTo(_baseIntensity[a]));

        _activeFlows = 0;
        for (int k = 0; k < occupied.Count - 1 && _activeFlows < MAX_FLOWS; k++)
        {
            _flowStart[_activeFlows] = occupied[k];
            _flowEnd[_activeFlows]   = occupied[k + 1];
            _activeFlows++;
        }

        // Close the circuit only when 3+ departments are active
        if (occupied.Count >= 3 && _activeFlows < MAX_FLOWS)
        {
            _flowStart[_activeFlows] = occupied[occupied.Count - 1];
            _flowEnd[_activeFlows]   = occupied[0];
            _activeFlows++;
        }
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < MAX_POINTS; i++)
            if (_slots[i] == null && _baseIntensity[i] == 0f) return i;
        return -1;
    }

    private int FindLowestPrioritySlot()
    {
        int lowest = 0;
        for (int i = 1; i < MAX_POINTS; i++)
            if (_slots[i] != null && (_slots[lowest] == null || _slots[i].priorityLevel < _slots[lowest].priorityLevel))
                lowest = i;
        return lowest;
    }

    private int FindSlotOf(RetailPointLinker linker)
    {
        for (int i = 0; i < MAX_POINTS; i++)
            if (_slots[i] == linker) return i;
        return -1;
    }

    private RetailPointLinker PopHighestOverflow()
    {
        if (_overflow.Count == 0) return null;
        int best = 0;
        for (int i = 1; i < _overflow.Count; i++)
            if (_overflow[i].priorityLevel > _overflow[best].priorityLevel)
                best = i;
        var linker = _overflow[best];
        _overflow.RemoveAt(best);
        return linker;
    }

    private bool IsValidIndex(int i) => i >= 0 && i < MAX_POINTS;

    // ─── Scenario presets (manual/demo mode) ─────────────────────────────────

    private void InitScenarios()
    {
        _scenarios = new Dictionary<string, HeatPointConfig[]>
        {
            ["NormalFlow"] = new[]
            {
                new HeatPointConfig(new Vector3(-2.0f, 0f,  1.5f), 0.7f, 0.70f),
                new HeatPointConfig(new Vector3(-0.5f, 0f,  2.0f), 0.6f, 0.55f),
                new HeatPointConfig(new Vector3( 1.0f, 0f,  1.5f), 0.6f, 0.50f),
                new HeatPointConfig(new Vector3( 2.0f, 0f,  0.0f), 0.5f, 0.40f),
                new HeatPointConfig(new Vector3( 0.5f, 0f, -1.5f), 0.5f, 0.35f),
                new HeatPointConfig(new Vector3(-1.5f, 0f, -1.0f), 0.5f, 0.30f),
            },
            ["AsiaFlight"] = new[]
            {
                new HeatPointConfig(new Vector3(-2.5f, 0f,  2.0f), 0.8f, 1.00f),
                new HeatPointConfig(new Vector3(-1.0f, 0f,  2.5f), 0.75f, 0.90f),
                new HeatPointConfig(new Vector3( 0.5f, 0f,  2.2f), 0.70f, 0.80f),
                new HeatPointConfig(new Vector3( 2.0f, 0f,  1.5f), 0.65f, 0.65f),
                new HeatPointConfig(new Vector3( 2.5f, 0f,  0.0f), 0.55f, 0.45f),
                new HeatPointConfig(new Vector3( 1.0f, 0f, -1.0f), 0.50f, 0.25f),
            },
            ["DelayCrisis"] = new[]
            {
                new HeatPointConfig(new Vector3( 0.0f, 0f,  0.0f), 0.9f, 1.00f),
                new HeatPointConfig(new Vector3(-2.0f, 0f,  0.0f), 0.75f, 0.80f),
                new HeatPointConfig(new Vector3( 2.0f, 0f,  0.0f), 0.75f, 0.80f),
                new HeatPointConfig(new Vector3( 0.0f, 0f,  2.0f), 0.65f, 0.65f),
                new HeatPointConfig(new Vector3( 0.0f, 0f, -2.0f), 0.65f, 0.65f),
                new HeatPointConfig(new Vector3(-1.0f, 0f,  1.0f), 0.55f, 0.55f),
            },
        };
    }
}

[System.Serializable]
public struct HeatPointConfig
{
    public Vector3 position;
    public float   radius;
    public float   intensity;

    public HeatPointConfig(Vector3 position, float radius, float intensity)
    {
        this.position  = position;
        this.radius    = radius;
        this.intensity = intensity;
    }
}
