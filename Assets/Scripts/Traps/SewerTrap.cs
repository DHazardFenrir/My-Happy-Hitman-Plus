using UnityEngine;

public class SewerTrap : MonoBehaviour
{
    [SerializeField] private SpriteRenderer grateSpriteRenderer;
    [SerializeField] private SpriteRenderer sewerWorker;
    [SerializeField] private Sprite workerSpriteChanged;
    [SerializeField] private Sprite openGrateSprite;
    [Tooltip("Only used if no grate sprite is assigned")]
    [SerializeField] private float catchRadius = 0.8f;
    [Tooltip("How centered the monito must be over the grate (fraction of the grate width)")]
    [Range(0.1f, 1f)] [SerializeField] private float grateWidth = 0.8f;
    [Tooltip("Vertical margin between the monito's body and the grate")]
    [SerializeField] private float verticalMargin = 0.5f;
    [SerializeField] GameObject particleGas;

    private SewerStage stageManager;

    public void Activate(SewerStage manager)
    {
        stageManager = manager;
        if (stageManager == null) return;

        // Measured before the grate sprite changes
        NPCWalker caught = FindNPCOverGrate();
        SoundFX.Play(Sfx.SewerOpen);

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
            SoundFX.Play(Sfx.SewerFall);
            stageManager.OnNPCFellInSewer(caught);
        }
        else
        {
            // Nobody on top: fail
            stageManager.OnTrapMissed();
        }
    }

    // The monito whose center is over the grate falls (horizontally, since that's how they walk).
    // The zone is the real size of the grate sprite, not a generic circle.
    private NPCWalker FindNPCOverGrate()
    {
        Bounds zone = GrateBounds();
        zone.Expand(new Vector3(0f, verticalMargin * 2f, 0f));
        return stageManager.FindVictim(zone, zone.extents.x * grateWidth);
    }

    public Bounds Zone => GrateBounds();

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
