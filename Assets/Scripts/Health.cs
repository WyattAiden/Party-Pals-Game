using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    public event Action<float, float> Changed; // current, max
    public event Action Died;

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        Changed?.Invoke(Current, maxHealth);
        if (IsDead) Died?.Invoke();
    }
}