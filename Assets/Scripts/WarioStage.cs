using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WarioStage : MonoBehaviour
{
    [Header("Base Config")]
    public float timeLimit = 15f;
    public Transform spawnPoint;
    public GameObject npcPrefab;

    [Tooltip("Cuántos monitos distractores aparecen además del objetivo")]
    public int extraNPCsCount = 3;

    [Tooltip("Velocidad de los monitos en este minijuego (0 = la del prefab)")]
    public float npcMoveSpeed = 1.4f;

    [Tooltip("Margen respecto al borde de la pantalla del celular donde aparecen/desaparecen los monitos")]
    public float screenMargin = 0.35f;

    [Tooltip("Segundos que tiene la trampa para golpear a alguien después de activarse. Si no golpea a nadie, se falla.")]
    public float trapResolveTime = 2.5f;

    [Header("Camera Config")]
    public Transform cameraFocusPoint;
    public float stageOrthographicSize = 10f;

    public NPCAppearance.ClothingPart targetClothing { get; private set; }
    public bool isActive { get; protected set; } = false;
    public bool isFinished { get; protected set; } = false;
    public bool succeeded { get; protected set; } = false;
    public float TimeRemaining { get; private set; }

    protected readonly List<NPCWalker> spawnedNPCs = new List<NPCWalker>();

    // Área del mundo que se ve en la pantalla del celular
    protected Rect view;

    private static readonly NPCAppearance.ClothingPart[] availableCombinations =
    {
        NPCAppearance.ClothingPart.RedShirt   | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.RedShirt   | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.WhiteShirt | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.WhiteShirt | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.BlackShirt | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.BlackShirt | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.GreenShirt | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.GreenShirt | NPCAppearance.ClothingPart.BluePants,
        NPCAppearance.ClothingPart.BlueShirt  | NPCAppearance.ClothingPart.BlackPants,
        NPCAppearance.ClothingPart.BlueShirt  | NPCAppearance.ClothingPart.BluePants
    };

    // Se elige el objetivo ANTES de instanciar el stage, para poder mostrar la card primero.
    public static NPCAppearance.ClothingPart PickRandomTarget()
    {
        return availableCombinations[Random.Range(0, availableCombinations.Length)];
    }

    public Vector3 GetCameraPosition()
    {
        Vector3 pos = cameraFocusPoint != null ? cameraFocusPoint.position : transform.position;
        pos.z = -10f;
        return pos;
    }

    public virtual void SetupStage(NPCAppearance.ClothingPart target)
    {
        targetClothing = target;
        TimeRemaining = timeLimit;

        // Primero se enfoca la cámara para saber qué parte del mundo se ve en el celular
        if (RenderCameraFollow.Instance != null)
        {
            RenderCameraFollow.Instance.FocusOnStage(this);
            view = RenderCameraFollow.Instance.GetVisibleWorldRect(spawnPoint.position.z);
        }
        else
        {
            view = new Rect(spawnPoint.position.x - 1f, spawnPoint.position.y - 5f, 1000f, 10f);
        }

        List<NPCAppearance.ClothingPart> extraNPCPool = new List<NPCAppearance.ClothingPart>(availableCombinations);
        extraNPCPool.Remove(target);

        int extras = Mathf.Min(extraNPCsCount, extraNPCPool.Count);

        // Los monitos aparecen DENTRO de la pantalla del celular (nunca entran desde fuera del marco):
        // una fila que empieza en el spawnPoint o en el borde izquierdo visible, lo que esté más adentro.
        // Luego se barajan las posiciones para que el objetivo no sea siempre el primero.
        float startX = Mathf.Max(spawnPoint.position.x, view.xMin + screenMargin);
        List<Vector3> slots = new List<Vector3>();
        float currentXOffset = 0f;
        for (int i = 0; i <= extras; i++)
        {
            slots.Add(new Vector3(startX + currentXOffset, spawnPoint.position.y, spawnPoint.position.z));
            currentXOffset += Random.Range(0.6f, 0.8f);
        }
        Shuffle(slots);

        SpawnNPC(slots[0], target, true);

        for (int i = 0; i < extras; i++)
        {
            int randDistractor = Random.Range(0, extraNPCPool.Count);
            SpawnNPC(slots[i + 1], extraNPCPool[randDistractor], false);
            extraNPCPool.RemoveAt(randDistractor);
        }
    }

    private void SpawnNPC(Vector3 position, NPCAppearance.ClothingPart clothing, bool isTarget)
    {
        // Los monitos son hijos del stage: se destruyen junto con él
        GameObject npcObj = Instantiate(npcPrefab, position, Quaternion.identity, transform);

        NPCAppearance appearance = npcObj.GetComponent<NPCAppearance>();
        if (appearance != null) appearance.SetupAppearance(clothing);

        NPCWalker walker = npcObj.GetComponent<NPCWalker>();
        if (walker != null)
        {
            walker.clothingPart = clothing;
            walker.isTarget = isTarget;
            walker.CanMove = false; // No caminan hasta que empieza el minijuego
            if (npcMoveSpeed > 0f) walker.moveSpeed = npcMoveSpeed;
            walker.SetWalkDirection(Vector3.right);
            walker.PopIn();
            spawnedNPCs.Add(walker);
        }
    }

    public virtual void StartStage()
    {
        isActive = true;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc != null) npc.CanMove = true;
        }
    }

    // El timer vive en la clase base; los stages implementan OnStageUpdate.
    protected virtual void Update()
    {
        if (!isActive) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            EndMinigame(false);
            return;
        }

        DespawnNPCsAtScreenEdge();
        if (!isActive) return;

        OnStageUpdate();
    }

    // Los monitos desaparecen antes de llegar al borde del celular.
    // Si ya no queda ningún monito en pantalla, se falla.
    private void DespawnNPCsAtScreenEdge()
    {
        bool anyoneLeft = false;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone) continue;
            anyoneLeft = true;

            float x = npc.transform.position.x;
            bool atEdge = npc.WalkDirection.x >= 0 ? x > view.xMax - screenMargin : x < view.xMin + screenMargin;
            if (atEdge && !npc.IsPopping) npc.PopOut(true);
        }

        if (!anyoneLeft) EndMinigame(false);
    }

    protected abstract void OnStageUpdate();

    // Llamar cuando la trampa se activa: si en trapResolveTime no golpeó a nadie, se falla.
    protected void BeginTrapResolve()
    {
        StartCoroutine(TrapResolveRoutine());
    }

    private IEnumerator TrapResolveRoutine()
    {
        yield return new WaitForSeconds(trapResolveTime);
        if (isActive) EndMinigame(false);
    }

    // Altura de los pies y de la cabeza de los monitos (para colocar suelo y paredes automáticas)
    protected float NPCFeetY()
    {
        foreach (NPCWalker npc in spawnedNPCs)
            if (npc != null) return npc.BodyRect.yMin;
        return spawnPoint.position.y;
    }

    protected float NPCHeadY()
    {
        foreach (NPCWalker npc in spawnedNPCs)
            if (npc != null) return npc.BodyRect.yMax;
        return spawnPoint.position.y;
    }

    // Elige al monito que REALMENTE está debajo de la trampa: su cuerpo debe tocar la zona en vertical
    // y su centro estar dentro de la parte central de la trampa. Si hay varios, el más centrado.
    // Así rozar al monito de al lado ya no cuenta.
    public NPCWalker FindVictim(Bounds trapZone, float centerTolerance)
    {
        NPCWalker best = null;
        float bestDx = float.MaxValue;
        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null || npc.IsDead || npc.IsGone) continue;

            Rect body = npc.BodyRect;
            if (body.yMax < trapZone.min.y || body.yMin > trapZone.max.y) continue;

            float dx = Mathf.Abs(body.center.x - trapZone.center.x);
            if (dx > centerTolerance || dx >= bestDx) continue;

            best = npc;
            bestDx = dx;
        }
        return best;
    }

    // La trampa golpeó a un monito
    public void OnTrapHitNPC(NPCWalker npc)
    {
        if (!isActive || npc == null || npc.IsDead) return;

        npc.Kill();
        bool isMatch = npc.isTarget || (npc.clothingPart & targetClothing) == targetClothing;
        EndMinigame(isMatch);
    }

    // La trampa no golpeó a nadie (chocó con el suelo/pared o la alcantarilla estaba vacía)
    public void OnTrapMissed()
    {
        if (!isActive) return;
        EndMinigame(false);
    }

    protected void EndMinigame(bool success)
    {
        if (isFinished) return;

        isActive = false;
        isFinished = true;
        succeeded = success;
        StopAllCoroutines();
        // El GameManager se encarga de mostrar la card de resultado y destruir el stage.
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
