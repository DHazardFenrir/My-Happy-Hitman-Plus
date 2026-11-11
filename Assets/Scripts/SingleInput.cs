using UnityEngine;

// The game's single input (space, click or tap).
// Computed directly instead of cached in Update, so every script sees the same value
// during the frame regardless of execution order.
public class SingleInput : MonoBehaviour
{
    public static bool ActionPressed =>
        Autopilot.PressedThisFrame ||
        Input.GetKeyDown(KeyCode.Space) ||
        Input.GetMouseButtonDown(0) ||
        (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

    // The button is down this frame (for "hold and release" minigames)
    public static bool ActionHeld =>
        Autopilot.Holding ||
        Input.GetKey(KeyCode.Space) ||
        Input.GetMouseButton(0) ||
        Input.touchCount > 0;
}
