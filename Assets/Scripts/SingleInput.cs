using UnityEngine;

// Input único del juego (espacio, click o tap).
// Se calcula directamente en lugar de guardarse en Update: así todos los scripts
// ven el mismo valor durante el frame, sin depender del orden de ejecución.
public class SingleInput : MonoBehaviour
{
    public static bool ActionPressed =>
        Input.GetKeyDown(KeyCode.Space) ||
        Input.GetMouseButtonDown(0) ||
        (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
}
