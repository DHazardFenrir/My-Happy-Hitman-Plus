using UnityEngine;

// Marks a collider (ground, wall, post...) that traps crash into.
// If the car or the piano hits a TrapBlocker without hitting a monito, the minigame is failed.
// Stages create one automatically if the prefab has none.
public class TrapBlocker : MonoBehaviour
{
    public static TrapBlocker Create(Transform parent, string name, Vector3 worldPosition, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPosition;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = size;

        return go.AddComponent<TrapBlocker>();
    }
}
