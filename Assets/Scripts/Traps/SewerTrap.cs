using UnityEngine;

public class SewerTrap : MonoBehaviour
{
    [SerializeField] private SpriteRenderer grateSpriteRenderer;
    [SerializeField] private SpriteRenderer sewerWorker;
    [SerializeField] private Sprite workerSpriteChanged;
    [SerializeField] private Sprite openGrateSprite;
    [Tooltip("Solo se usa si no hay sprite de la tapa asignado")]
    [SerializeField] private float catchRadius = 0.8f;
    [Tooltip("Qué tan centrado debe estar el monito sobre la tapa (fracción del ancho de la tapa)")]
    [Range(0.1f, 1f)] [SerializeField] private float grateWidth = 0.8f;
    [Tooltip("Margen vertical entre el cuerpo del monito y la tapa")]
    [SerializeField] private float verticalMargin = 0.5f;
    [SerializeField] GameObject particleGas;

    private SewerStage stageManager;

    public void Activate(SewerStage manager)
    {
        stageManager = manager;
        if (stageManager == null) return;

        // Se mide antes de cambiar el sprite de la tapa
        NPCWalker caught = FindNPCOverGrate();

        if (grateSpriteRenderer != null && openGrateSprite != null)
        {
            grateSpriteRenderer.sprite = openGrateSprite;
        }

        if (sewerWorker != null && workerSpriteChanged != null)
        {
            sewerWorker.sprite = workerSpriteChanged;
        }

        if (caught != null)
        {
            if (particleGas != null) particleGas.SetActive(true);
            stageManager.OnNPCFellInSewer(caught);
        }
        else
        {
            // No había nadie encima: falla
            stageManager.OnTrapMissed();
        }
    }

    // Cae el monito cuyo centro está sobre la tapa (en horizontal, que es como caminan).
    // La zona es el tamaño real del sprite de la tapa, no un círculo genérico.
    private NPCWalker FindNPCOverGrate()
    {
        Bounds zone = GrateBounds();
        zone.Expand(new Vector3(0f, verticalMargin * 2f, 0f));
        return stageManager.FindVictim(zone, zone.extents.x * grateWidth);
    }

    private Bounds GrateBounds()
    {
        if (grateSpriteRenderer != null && grateSpriteRenderer.sprite != null) return grateSpriteRenderer.bounds;
        return new Bounds(transform.position, new Vector3(catchRadius * 2f, catchRadius * 2f, 1f));
    }

    private void OnDrawGizmosSelected()
    {
        Bounds grate = GrateBounds();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(grate.center, new Vector3(grate.size.x * grateWidth, grate.size.y + verticalMargin * 2f, 0f));
    }
}
