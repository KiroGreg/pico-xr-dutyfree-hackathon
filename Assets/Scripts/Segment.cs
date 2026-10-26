using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Kommt auf JEDES verschiebbare Objekt (Segment_0 ... Segment_5).
/// Das Objekt wird NICHT mehr getragen und hat KEINE Physik. Es meldet nur,
/// wenn der Nutzer mit dem Controller-Ray darauf zeigt und Select drueckt.
/// Das eigentliche Verschieben/Begrenzen/Blockieren macht der SlotManager.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class Segment : MonoBehaviour
{
    [Header("Highlight beim Sperren")]
    [Tooltip("Farbe, solange dieses Objekt gesperrt (zum Verschieben aktiv) ist.")]
    public Color highlightColor = Color.yellow;

    [Header("Kollision")]
    [Tooltip("Grundflaeche als Kreis-Radius (in Metern). Bestimmt, wie nah zwei " +
             "Objekte aneinander koennen. Groesser = mehr Abstand. Zum Tisch/Modell passend einstellen.")]
    public float footprintRadius = 0.15f;

    private SlotManager manager;
    private XRSimpleInteractable interactable;

    // Alle Renderer des Modells (auch in Kind-Objekten) + ihre Ursprungsfarben,
    // damit beim Auswaehlen das KOMPLETTE Modell gefaerbt werden kann.
    private Renderer[] rends;
    private Color[][] originalColors;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        // true = auch inaktive Kind-Objekte einsammeln.
        rends = GetComponentsInChildren<Renderer>(true);
        originalColors = new Color[rends.Length][];

        for (int i = 0; i < rends.Length; i++)
        {
            // .materials erzeugt eigene Material-Kopien fuer DIESES Objekt,
            // damit das Faerben nicht alle Modelle mitfaerbt.
            Material[] mats = rends[i].materials;
            originalColors[i] = new Color[mats.Length];
            for (int j = 0; j < mats.Length; j++)
            {
                originalColors[i][j] = mats[j].color;
            }
        }
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnTapped);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnTapped);
    }

    /// <summary>Wird vom SlotManager beim Start aufgerufen.</summary>
    public void SetManager(SlotManager m)
    {
        manager = m;
    }

    // Wird aufgerufen, wenn der Ray auf dieses Objekt zeigt und Select gedrueckt wird.
    private void OnTapped(SelectEnterEventArgs args)
    {
        if (manager != null)
        {
            // Wir geben den Controller (Ray-Quelle) mit, damit der Manager weiss,
            // wohin gezogen werden soll.
            Transform raySource = args.interactorObject.transform;
            manager.OnSegmentTapped(this, raySource);
        }
    }

    /// <summary>Schaltet das farbliche Hervorheben fuer das GANZE Modell an oder aus.</summary>
    public void SetHighlighted(bool on)
    {
        if (rends == null) return;

        for (int i = 0; i < rends.Length; i++)
        {
            Material[] mats = rends[i].materials;
            for (int j = 0; j < mats.Length; j++)
            {
                mats[j].color = on ? highlightColor : originalColors[i][j];
            }
        }
    }
}
