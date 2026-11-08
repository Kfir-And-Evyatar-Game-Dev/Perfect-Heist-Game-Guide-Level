using UnityEngine;

/// <summary>The target item. Needs a trigger collider so characters know when they stand on it.</summary>
[RequireComponent(typeof(Collider2D))]
public class Diamond : MonoBehaviour
{
    public SpriteRenderer body;

    public bool Collected { get; private set; }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
    }

    public void Collect(Character by)
    {
        Collected = true;
        if (body != null) body.enabled = false;
        Debug.Log($"{by.DisplayName} picked up the diamond.");
    }

    public void ResetState()
    {
        Collected = false;
        if (body != null) body.enabled = true;
    }
}
