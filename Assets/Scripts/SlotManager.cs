using UnityEngine;

/// <summary>
/// Steuert das freie Verschieben der Segmente auf der Tischplatte.
///
/// Ablauf: Ray auf ein Segment + Select druecken = sperren. Danach folgt das
/// Segment dem Controller-Ray auf der Tischebene. Erneut Select druecken = ablegen.
///
/// Es gibt KEINE Physik: bewegt wird nur per transform. Die Hoehe (Y) bleibt fix,
/// die Position wird auf die Tischflaeche begrenzt, und Ueberlappungen mit anderen
/// Segmenten werden blockiert (das andere Objekt wird nicht weggeschoben).
///
/// (Der Klassenname bleibt "SlotManager", damit die bestehende Szenen-Verknuepfung
///  erhalten bleibt - die Slot-/Tausch-Logik von frueher wurde ersetzt.)
/// </summary>
public class SlotManager : MonoBehaviour
{
    [Header("Verschiebbare Objekte")]
    [Tooltip("Alle Segmente, die verschoben werden koennen (Segment_0 ... Segment_5).")]
    public Segment[] segments;

    [Header("Tischflaeche (Begrenzung)")]
    [Tooltip("Objekt in der Mitte der runden Tischplatte. Bestimmt den Mittelpunkt der erlaubten Flaeche.")]
    public Transform tableSurface;

    [Tooltip("Radius der runden Tischplatte in Metern (vom Mittelpunkt bis zum Rand).")]
    public float tableRadius = 0.6f;

    // Aktuell gesperrtes (zum Verschieben aktives) Segment, oder null.
    private Segment lockedSegment;
    // Der Controller, von dem aus der Ziel-Strahl ausgeht.
    private Transform raySource;

    private void Awake()
    {
        // Segmente beim Manager registrieren. Positionen NICHT veraendern -
        // sie bleiben dort, wo sie in der Szene stehen.
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].SetManager(this);
        }
    }

    private void Update()
    {
        if (lockedSegment == null || raySource == null) return;

        // 1) Wo trifft der Controller-Ray die waagerechte Tischebene (auf Hoehe des Segments)?
        float surfaceY = lockedSegment.transform.position.y;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
        Ray ray = new Ray(raySource.position, raySource.forward);
        if (!plane.Raycast(ray, out float dist)) return;

        Vector3 target = ray.GetPoint(dist);

        // 2) Auf die Tischflaeche begrenzen (Hoehe bleibt fix).
        target = ClampToTable(target, lockedSegment, surfaceY);

        // 3) Kollision achsweise pruefen -> man kann an anderen Objekten entlanggleiten,
        //    schiebt sie aber nicht weg.
        Vector3 current = lockedSegment.transform.position;
        Vector3 result = current;

        Vector3 tryX = new Vector3(target.x, surfaceY, current.z);
        if (!IsBlocked(lockedSegment, tryX, current)) result.x = target.x;

        Vector3 tryZ = new Vector3(result.x, surfaceY, target.z);
        if (!IsBlocked(lockedSegment, tryZ, current)) result.z = target.z;

        lockedSegment.transform.position = result;
    }

    /// <summary>Wird vom Segment aufgerufen, wenn es angetippt wurde.</summary>
    public void OnSegmentTapped(Segment tapped, Transform interactorTransform)
    {
        // Nichts gesperrt -> dieses Segment sperren und ans Verschieben koppeln.
        if (lockedSegment == null)
        {
            lockedSegment = tapped;
            raySource = interactorTransform;
            tapped.SetHighlighted(true);
            return;
        }

        // Dasselbe Segment erneut angetippt -> ablegen.
        if (lockedSegment == tapped)
        {
            tapped.SetHighlighted(false);
            lockedSegment = null;
            raySource = null;
            return;
        }

        // Ein anderes Segment, waehrend eins gesperrt ist -> ignorieren
        // (erst das aktuelle ablegen, dann das naechste sperren).
    }

    /// <summary>Klemmt eine Position auf die erlaubte runde Tischflaeche (Kreis).</summary>
    private Vector3 ClampToTable(Vector3 p, Segment seg, float surfaceY)
    {
        if (tableSurface == null)
        {
            return new Vector3(p.x, surfaceY, p.z);
        }

        Vector3 c = tableSurface.position;

        // Abstand vom Tischmittelpunkt, nur in X/Z.
        float dx = p.x - c.x;
        float dz = p.z - c.z;
        float distSq = dx * dx + dz * dz;

        // Erlaubter Radius = Tischradius minus Footprint, damit nichts ueberhaengt.
        float maxR = Mathf.Max(0f, tableRadius - seg.footprintRadius);

        if (distSq > maxR * maxR)
        {
            // Auf den Kreisrand zurueckholen.
            float dist = Mathf.Sqrt(distSq);
            dx = dx / dist * maxR;
            dz = dz / dist * maxR;
        }
        return new Vector3(c.x + dx, surfaceY, c.z + dz);
    }

    /// <summary>
    /// Prueft, ob die Bewegung nach 'candidate' blockiert werden soll.
    /// Footprints werden als Kreise auf der Tischebene behandelt.
    /// Blockiert wird NUR, wenn 'candidate' ein anderes Segment ueberlappt UND
    /// dabei NAEHER an dieses heran will als die aktuelle Position 'current'.
    /// Dadurch kann man ueberlappende Objekte auseinanderziehen und an ihnen
    /// entlanggleiten - nur tieferes Hineinschieben wird verhindert.
    /// </summary>
    private bool IsBlocked(Segment moving, Vector3 candidate, Vector3 current)
    {
        for (int i = 0; i < segments.Length; i++)
        {
            Segment other = segments[i];
            if (other == moving) continue;

            Vector3 o = other.transform.position;
            float minDist = moving.footprintRadius + other.footprintRadius;

            // Abstand der Zielposition zum anderen Objekt (nur X/Z).
            float cdx = candidate.x - o.x;
            float cdz = candidate.z - o.z;
            float candDistSq = cdx * cdx + cdz * cdz;

            if (candDistSq < minDist * minDist)
            {
                // Wuerde ueberlappen. Nur blockieren, wenn es naeher rangeht als jetzt.
                float ndx = current.x - o.x;
                float ndz = current.z - o.z;
                float curDistSq = ndx * ndx + ndz * ndz;

                if (candDistSq < curDistSq - 0.0001f)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
