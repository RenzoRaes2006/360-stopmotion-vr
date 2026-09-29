using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Rondkijken zonder VR-bril: slepen met de muis (links of rechts) draait de camera,
// pijltjestoetsen ook, scrollwiel = zoomen. Doet niets als er een VR-bril actief is.
[RequireComponent(typeof(Camera))]
public class MuisRondkijken : MonoBehaviour
{
    public float muisGevoeligheid = 0.15f;
    public float toetsSnelheid = 90f;       // graden per seconde
    public Vector2 zoomBereik = new Vector2(30f, 100f);

    Camera cam;
    float yaw, pitch;
    bool actief;

    void Start()
    {
        actief = !XRSettings.isDeviceActive;
        if (!actief) return;

        // Zonder bril de head-tracking uitzetten, anders overschrijft die onze rotatie
        var tpd = GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        if (tpd != null) tpd.enabled = false;

        cam = GetComponent<Camera>();
        yaw = transform.eulerAngles.y;
        Debug.Log("[MuisRondkijken] Geen VR-bril: sleep met de muis om rond te kijken, scroll om te zoomen.");
    }

    void Update()
    {
        if (!actief) return;

        var muis = Mouse.current;
        if (muis != null)
        {
            if (muis.leftButton.isPressed || muis.rightButton.isPressed)
            {
                Vector2 d = muis.delta.ReadValue() * muisGevoeligheid;
                yaw += d.x;
                pitch -= d.y;
            }
            float scroll = muis.scroll.ReadValue().y;
            if (scroll != 0f)
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - scroll * 0.05f, zoomBereik.x, zoomBereik.y);
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            float s = toetsSnelheid * Time.unscaledDeltaTime;
            if (kb.leftArrowKey.isPressed) yaw -= s;
            if (kb.rightArrowKey.isPressed) yaw += s;
            if (kb.upArrowKey.isPressed) pitch -= s;
            if (kb.downArrowKey.isPressed) pitch += s;
        }

        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
