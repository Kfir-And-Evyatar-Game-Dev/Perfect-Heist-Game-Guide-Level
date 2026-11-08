using UnityEngine;

/// <summary>The getaway van. Characters inside its trigger count as escaped.</summary>
[RequireComponent(typeof(Collider2D))]
public class ExitZone : MonoBehaviour
{
    void Awake() => GetComponent<Collider2D>().isTrigger = true;
}
