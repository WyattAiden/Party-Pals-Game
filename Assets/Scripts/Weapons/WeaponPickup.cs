using System.Collections;
using UnityEngine;

// Put on a pickup object with a trigger collider (Is Trigger ticked).
// Players walk into it to get the power weapon.
[RequireComponent(typeof(Collider))]
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private PowerWeaponData weapon;
    [Tooltip("Seconds until it reappears after being picked up. 0 = one-time pickup.")]
    [SerializeField] private float respawnDelay = 20f;

    [Header("Idle animation")]
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;

    private Collider col;
    private Renderer[] renderers;
    private Vector3 startPos;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
        startPos = transform.position;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        var handler = other.GetComponentInParent<PlayerWeaponHandler>();
        if (handler == null || !handler.CanPickUp(weapon)) return;

        var controller = other.GetComponentInParent<PlayerController>();
        handler.Equip(weapon, controller != null ? controller.FirePoint : null);

        if (respawnDelay > 0f) StartCoroutine(Respawn());
        else Destroy(gameObject);
    }

    IEnumerator Respawn()
    {
        SetVisible(false);
        yield return new WaitForSeconds(respawnDelay);
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        col.enabled = visible;
        foreach (var r in renderers) r.enabled = visible;
    }
}
