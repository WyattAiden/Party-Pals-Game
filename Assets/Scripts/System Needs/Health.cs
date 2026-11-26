using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    public event Action<float, float> Changed; // current, max
    public event Action Died;
    public event Action<float, GameObject> Damaged; // amount lost, source (player or hazard, may be null)

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead) return;
        float before = Current;
        Current = Mathf.Max(0f, Current - amount);
        Damaged?.Invoke(before - Current, source);
        Changed?.Invoke(Current, maxHealth);
        if (IsDead) Died?.Invoke();
    }
}