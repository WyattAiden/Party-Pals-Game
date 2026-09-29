using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public class GlobalContents : MonoBehaviour
    {

        public static GlobalContents MainGlobalContent;


        public GameObject[] WeaponPrefabs;
        public GameObject[] CratePrefabs;
        public GameObject[] ItemPrefabs;
        public GameObject[] PickupPrefabs;

        public Color[] PlayerUIColors;

        public GameObject[] SpawnEffectPrefabs;
        public GameObject[] SpawnTrailPrefabs;
        public GameObject[] DashEffectPrefabs;
        public GameObject[] DashSpherePrefabs;
        public GameObject BulletTrailPrefab1;
        public GameObject DashHitPrefab1;

        public GameObject[] DecalsPrefabs;
        void Awake()
        {
            MainGlobalContent = this;
        }
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}