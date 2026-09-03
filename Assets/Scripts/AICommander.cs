using UnityEngine;
using System.Collections.Generic;

public class AICommander : MonoBehaviour
{
    [Header("Commander Identity")]
    public Faction aiFaction = Faction.Defender;

    [Header("Economy (Passive Drip)")]
    public int aiCommandXP = 0;
    public int xpPerTick = 10;
    public float xpTickInterval = 3f;

    [Header("Decision Making")]
    [Tooltip("How often the AI checks the store to make purchases")]
    public float decisionInterval = 4f; 

    private float xpTimer = 0f;
    private float decisionTimer = 0f;

    private struct PurchaseOption
    {
        public PurchasableUnit unit;
        public CapturePoint sourceBase;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isTransitioningSector) return;

        // 1. Do nothing while waiting on the main menu
        if (GameManager.Instance.playerFaction == Faction.None) return;

        // 2. THE AIRTIGHT LOCK: 
        // If we are NOT testing, and this AI matches the human's faction, skip this frame entirely!
        if (!GameManager.Instance.enableAutoTestMode && aiFaction == GameManager.Instance.playerFaction)
        {
            return; 
        }

        // 3. Drip XP over time (Only runs if AI is allowed to act)
        xpTimer += Time.deltaTime;
        if (xpTimer >= xpTickInterval)
        {
            aiCommandXP += xpPerTick;
            xpTimer = 0f;
        }

        // 4. Evaluate purchases on an interval (Only runs if AI is allowed to act)
        decisionTimer += Time.deltaTime;
        if (decisionTimer >= decisionInterval)
        {
            MakeTacticalPurchase();
            decisionTimer = 0f;
        }
    }

    private void MakeTacticalPurchase()
    {
        Sector currentSector = GameManager.Instance.sectors[GameManager.Instance.currentSectorIndex];
        List<PurchaseOption> affordableOptions = new List<PurchaseOption>();

        // Scan all capture points in the active sector
        foreach (CapturePoint cp in currentSector.capturePoints)
        {
            if (cp != null && cp.IsControlledBy(aiFaction))
            {
                PurchasableUnit[] availableUnits = cp.GetAvailableUnits(aiFaction);
                
                // Add any unit the AI can afford to the list of options
                foreach (PurchasableUnit unit in availableUnits)
                {
                    if (aiCommandXP >= unit.xpCost)
                    {
                        affordableOptions.Add(new PurchaseOption { unit = unit, sourceBase = cp });
                    }
                }
            }
        }

        // If there is nothing to buy, wait until next time
        if (affordableOptions.Count == 0) return;

        // Pick a random affordable unit to mix up the army composition
        int randomIndex = Random.Range(0, affordableOptions.Count);
        PurchaseOption chosenOption = affordableOptions[randomIndex];

        ExecutePurchase(chosenOption.unit, chosenOption.sourceBase);
    }

    private void ExecutePurchase(PurchasableUnit unit, CapturePoint sourceBase)
    {
        aiCommandXP -= unit.xpCost;
        Transform spawnLocation = sourceBase.GetNextAvailableSpawnPoint();
        Instantiate(unit.unitPrefab, spawnLocation.position, spawnLocation.rotation);
        
        Debug.Log($"🤖 AI COMMANDER ({aiFaction}): Deployed {unit.unitDisplayName} at {sourceBase.capturePointName}! Remaining XP: {aiCommandXP}");
    }
}