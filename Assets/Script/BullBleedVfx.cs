using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BullBleedVfx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BullStats bullStats;
    [SerializeField] private GameObject woundBleedPrefab;
    [SerializeField] private GameObject heavyBleedPrefab;

    [Header("Behaviour")]
    [SerializeField, Range(1, 12)] private int maxPersistentBleeds = 4;
    [SerializeField, Range(0f, 100f)] private float minDamageForHeavyBleed = 40f;
    [SerializeField, Range(0f, 0.5f)] private float outwardOffset = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float verticalOffset = 0.03f;
    [SerializeField, Range(0f, 45f)] private float randomYawJitter = 16f;
    [SerializeField] private Vector2 randomScaleRange = new Vector2(0.92f, 1.08f);
    [SerializeField, Range(0f, 10f)] private float bleedSprayDuration = 3.5f;

    [Header("Preferred Anchors")]
    [SerializeField] private string[] preferredAnchorNames = new[]    {
        "Spine_04",
        "Spine_03",
        "Spine_05",
        "neck_01",
        "neck_02",
        "head"
    };

    private readonly List<Transform> cachedAnchors = new List<Transform>();
    private readonly List<GameObject> activeBleeds = new List<GameObject>();
    private BullStats boundStats;
    private int nextAnchorIndex;

    private void Awake()
    {
        CacheAnchors();
    }

    private void Start()
    {
        TryBindBullStats();
    }

    private void OnEnable()
    {
        TryBindBullStats();
    }

    private void OnDisable()
    {
        UnbindBullStats();
    }

    private void TryBindBullStats()
    {
        if (bullStats == null)
            bullStats = GetComponent<BullStats>();

        if (bullStats == null || boundStats == bullStats)
            return;

        UnbindBullStats();
        boundStats = bullStats;
        boundStats.OnDamaged += HandleDamaged;
        boundStats.OnDefeated += HandleDefeated;
    }

    private void UnbindBullStats()
    {
        if (boundStats == null)
            return;

        boundStats.OnDamaged -= HandleDamaged;
        boundStats.OnDefeated -= HandleDefeated;
        boundStats = null;
    }

    private void HandleDamaged(float damageTaken)
    {
        CleanupMissingBleeds();

        GameObject prefab = SelectBleedPrefab(damageTaken);
        if (prefab == null)
            return;

        if (cachedAnchors.Count == 0)
            CacheAnchors();

        Transform anchor = GetNextAnchor();
        if (anchor == null)
            return;

        if (activeBleeds.Count >= Mathf.Max(1, maxPersistentBleeds))
            RemoveOldestBleed();

        Vector3 outward = GetOutwardDirection(anchor);
        Vector3 spawnPosition = anchor.position + outward * outwardOffset + Vector3.up * verticalOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, Random.Range(-randomYawJitter, randomYawJitter), 0f);

        GameObject bleedInstance = Instantiate(prefab, spawnPosition, spawnRotation, anchor);
        float scaleMultiplier = Random.Range(randomScaleRange.x, randomScaleRange.y);
        bleedInstance.transform.localScale *= scaleMultiplier;
        activeBleeds.Add(bleedInstance);
        StartCoroutine(StopBleedSprayAfterDuration(bleedInstance, bleedSprayDuration));
    }

    private void HandleDefeated()
    {
        if (heavyBleedPrefab == null)
            return;

        HandleDamaged(minDamageForHeavyBleed);
    }

    public void ClearBleeds()
    {
        CleanupMissingBleeds();

        for (int i = activeBleeds.Count - 1; i >= 0; i--)
        {
            if (activeBleeds[i] != null)
                Destroy(activeBleeds[i]);
        }

        activeBleeds.Clear();
        nextAnchorIndex = 0;
        CacheAnchors();
    }

    private GameObject SelectBleedPrefab(float damageTaken)
    {
        if (heavyBleedPrefab != null && boundStats != null && (damageTaken >= minDamageForHeavyBleed || boundStats.InFinalPhase))
            return heavyBleedPrefab;

        return woundBleedPrefab != null ? woundBleedPrefab : heavyBleedPrefab;
    }

    private void CacheAnchors()
    {
        cachedAnchors.Clear();

        if (preferredAnchorNames != null)
        {
            for (int i = 0; i < preferredAnchorNames.Length; i++)
            {
                string anchorName = preferredAnchorNames[i];
                if (string.IsNullOrWhiteSpace(anchorName))
                    continue;

                Transform anchor = FindChildRecursive(transform, anchorName);
                if (anchor != null && !cachedAnchors.Contains(anchor))
                    cachedAnchors.Add(anchor);
            }
        }

        if (cachedAnchors.Count == 0)
            cachedAnchors.Add(transform);
    }

    private Transform GetNextAnchor()
    {
        if (cachedAnchors.Count == 0)
            return null;

        Transform anchor = cachedAnchors[nextAnchorIndex % cachedAnchors.Count];
        nextAnchorIndex++;
        return anchor;
    }

    private void RemoveOldestBleed()
    {
        CleanupMissingBleeds();
        if (activeBleeds.Count == 0)
            return;

        GameObject oldest = activeBleeds[0];
        activeBleeds.RemoveAt(0);
        if (oldest != null)
            Destroy(oldest);
    }

    private void CleanupMissingBleeds()
    {
        for (int i = activeBleeds.Count - 1; i >= 0; i--)
        {
            if (activeBleeds[i] == null)
                activeBleeds.RemoveAt(i);
        }
    }

    private IEnumerator StopBleedSprayAfterDuration(GameObject bleedInstance, float duration)
    {
        if (bleedInstance == null || duration <= 0f)
            yield break;

        yield return new WaitForSeconds(duration);

        if (bleedInstance == null)
            yield break;

        ParticleSystem[] particleSystems = bleedInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem != null)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private Vector3 GetOutwardDirection(Transform anchor)
    {
        Vector3 outward = anchor.position - transform.position;
        outward.y = 0f;

        if (outward.sqrMagnitude <= 0.0001f)
        {
            outward = transform.forward;
            outward.y = 0f;
        }

        if (outward.sqrMagnitude <= 0.0001f)
            outward = Vector3.forward;

        return outward.normalized;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == targetName)
                return children[i];
        }

        return null;
    }
}
