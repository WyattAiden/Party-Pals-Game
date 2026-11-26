using UnityEngine;
using UnityEngine.UI;

public class AmmoDisplay : MonoBehaviour
{
    [Header("GlueGun")]
    [SerializeField] private GameObject ammoGroup;
    [SerializeField] private RectTransform plunger;
    [SerializeField] private Image glueStick;
    [SerializeField] private float plungerFullX;
    [SerializeField] private float plungerEmptyX;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private string glueGunName;

    private PlayerWeaponHandler weaponHandler;

    public void Setup(PlayerWeaponHandler w)
    {
        weaponHandler = w;
        w.AmmoChanged += OnAmmoChanged;
        ammoGroup.SetActive(false);
        weaponIcon.gameObject.SetActive(false);

    }

    private void OnAmmoChanged(PowerWeaponData weapon, int ammo)
    {
        if (weapon == null || weapon.displayName != glueGunName)
        {
            weaponIcon.gameObject.SetActive(false);
            ammoGroup.SetActive(false);
            return;
        }
        else
        {
            weaponIcon.gameObject.SetActive(true);
            ammoGroup.SetActive (true);

            float t = (float)ammo / weapon.startingAmmo;
            glueStick.fillAmount = t;
            float x = Mathf.Lerp(plungerEmptyX, plungerFullX, t);
            Vector2 pos = plunger.anchoredPosition;
            pos.x = x;
            plunger.anchoredPosition = pos;

        }
    }

    private void OnDestroy()
    {
        if (weaponHandler != null)
        {
            weaponHandler.AmmoChanged -= OnAmmoChanged;
        }
    }

}
