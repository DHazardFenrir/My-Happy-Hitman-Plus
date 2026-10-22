using UnityEngine;

// Marca un collider (suelo, pared, poste...) contra el que chocan las trampas.
// Si el carro o el piano chocan con un TrapBlocker sin haber golpeado a un monito, el minijuego se falla.
// Los stages crean uno automáticamente si el prefab no tiene ninguno.
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
