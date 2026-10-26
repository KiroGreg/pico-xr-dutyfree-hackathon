using UnityEngine;
using Unity.XR.PXR;

/// <summary>
/// Aktiviert beim Start den Video-See-Through (Passthrough) auf PICO-Headsets,
/// sodass man die echte Umgebung sieht und nur die virtuellen Objekte
/// (Tisch + Shop-Segmente) darin schweben.
///
/// Dieses Skript kommt EINMAL in die Szene (z. B. auf ein leeres Objekt
/// "SeeThrough" oder auf das XR Origin).
/// </summary>
public class SeeThroughActivator : MonoBehaviour
{
    [Header("Kamera")]
    [Tooltip("Die VR-Kamera (Main Camera unter dem XR Origin). " +
             "Leer lassen = es wird automatisch Camera.main genommen.")]
    public Camera xrCamera;

    private void Start()
    {
        // 1) See-Through im PICO-System einschalten.
        //    (Statisches Property aus dem PICO SDK 3.4.0 / Namespace Unity.XR.PXR)
        PXR_Manager.EnableVideoSeeThrough = true;

        // 2) Kamera "durchsichtig" machen. Ohne diesen Schritt zeichnet die
        //    Kamera einen undurchsichtigen Hintergrund (Skybox/Farbe) und
        //    ueberdeckt damit das Kamerabild der echten Umgebung.
        Camera cam = xrCamera != null ? xrCamera : Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparentes Schwarz
        }
        else
        {
            Debug.LogWarning("[SeeThroughActivator] Keine Kamera gefunden. " +
                "Bitte die VR-Kamera im Feld 'XR Camera' zuweisen.");
        }
    }
}
