using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DuelShooter
{
    public enum CharStates
    {
        stand,
        run,
        dash,
        fall,
        land,
        die,
        ThrowDisk

    }
    public class PlayerChar : MonoBehaviour
    {

        [HideInInspector]
        public PlayerControl MyPlayerControl;

        [HideInInspector]
        public int ControllerNum = 0;
        [HideInInspector]
        public Vector3 AimVector;
        [HideInInspector]
        public float AimAngle = 0;
        [HideInInspector]
        public Vector3 DashDirection;

        [HideInInspector]
        public CharacterController CharCon;
        [HideInInspector]
        public AudioSource Audio;
        public Transform BodyBase;
        public Transform ArmBase;
        public Transform ArrowBase;
        public Transform MuzzleBase;
        public Transform WeaponPoint;
        public Transform HealthBarBase;
        public Renderer BodyRenderer;

        public Quaternion BodyBaseInitRotation;

        public GameObject BulletPrefab1;
        public GameObject BulletFirePrefab1;

        public GameObject KillEffectPrefab1;

        public GameObject HealthBarPrefab;
        public GameObject RagdollPrefab1;

        [HideInInspector]
        public WeaponModel CurrentWeaponModel;

        [HideInInspector]
        public Renderer[] HealthBarRenderers;
        public Material[] HealthBarMats;

        [HideInInspector]
        public CharStates CharState = CharStates.stand;
        [HideInInspector]
        public float StateStartTime;
        [HideInInspector]
        public float StateTime;
        //[HideInInspector]
        //public float Health = 100;
        [HideInInspector]
        public float Kills = 0;

        public Material[] PlayerMats;

        float DashReuseTimer = 0;

        [HideInInspector]
        public DamageControl MyDamageControl;

        [HideInInspector]
        public int PowerNum = 0;

        float FireDelay = 0;
        float ChangeEmptyWeaponDelay = 2;

        [HideInInspector]
        public Animator MyAnimator;

        float DamageShakeArc = 0;
        // Use this for initialization
        void Start()
        {
            BodyBaseInitRotation = BodyBase.rotation;

            CharCon = GetComponent<CharacterController>();
            Audio = GetComponent<AudioSource>();
            MyDamageControl = GetComponent<DamageControl>();
            AimVector = Vector3.forward;
            StartState(CharStates.stand);

            MyAnimator = GetComponent<Animator>();

            Renderer[] r = transform.GetComponentsInChildren<Renderer>();
            foreach (Renderer r1 in r)
            {
                if (r1.gameObject.name.Contains("BodyPart"))
                {
                    if (ControllerNum == 0)
                    {
                        r1.material = PlayerMats[0];
                    }
                    else if (ControllerNum == 1)
                    {
                        r1.material = PlayerMats[1];
                    }
                }
            }

            int count = 30;
            float angle = 360f / 30f;
            HealthBarRenderers = new Renderer[count];
            for (int i = 0; i < count; i++)
            {
                GameObject obj = Instantiate(HealthBarPrefab);
                Vector3 pos = HealthBarBase.position;
                obj.transform.position = pos + Quaternion.Euler(0, i * angle, 0) * new Vector3(0, 0, 1.8f);
                obj.transform.rotation = Quaternion.Euler(90, i * angle, 0);
                obj.transform.parent = HealthBarBase;
                HealthBarRenderers[i] = obj.GetComponent<Renderer>();
            }

            //int random = Random.Range(0, 4);
            //MyPlayerControl.CurrentWeaponNum = random;
            //MyPlayerControl.CurrentWeapon = MyPlayerControl.Weapons[random];
            //ArmWeapon();

            MyPlayerControl.CurrentWeaponNum = 0;
            MyPlayerControl.CurrentWeapon = MyPlayerControl.Weapons[0];
            ArmWeapon();

        }

        public void ArmWeapon()
        {
            if (CurrentWeaponModel != null)
            {
                Destroy(CurrentWeaponModel.gameObject);
                CurrentWeaponModel = null;
            }
            GameObject obj = Instantiate(MyPlayerControl.CurrentWeapon.WeaponModelPrefab);
            obj.transform.position = WeaponPoint.position;
            obj.transform.rotation = WeaponPoint.rotation;
            obj.transform.parent = WeaponPoint;
            CurrentWeaponModel = obj.GetComponent<WeaponModel>();
            MyPlayerControl.CurrentWeapon.CurrentWeaponModel = CurrentWeaponModel;
        }
        // Update is called once per frame
        void Update()
        {

            Vector3 Movement = Vector3.zero;
            bool Fire = false;
            bool FireHold = false;
            bool FireGrenade = false;
            bool Input_PlaceMine = false;
            bool DashInput = false;
            bool Input_ChangeWeapon = false;

            Vector3 TempAimVector = Vector3.zero;
            Vector3 CameraForward = CameraControl.MainCameraControl.transform.forward;
            CameraForward.y = 0;
            CameraForward.Normalize();
            Vector3 CameraRight = Quaternion.Euler(0, 90, 0) * CameraForward;

            if (MyPlayerControl.InputEnable)
            {
                if (ControllerNum == 0)
                {
                    Movement += InputControl.m_Main.m_Movement.z* CameraForward;
                    Movement += InputControl.m_Main.m_Movement.x * CameraRight;
                   

                    if (InputControl.m_Main.m_Fire2)
                    {
                        Input_ChangeWeapon = true;
                    }


                    if (InputControl.m_Main.m_Fire)
                    {
                        FireHold = true;
                    }

                    //if (Input.GetKey(KeyCode.Space))
                    //{
                    //    DashInput = true;
                    //}

                    //if (Input.GetMouseButtonDown(0))
                    //{
                    //    Fire = true;
                    //}

                    //if (Input.GetMouseButtonDown(1))
                    //{
                    //    FireGrenade = true;
                    //}

                    if (InputControl.m_Main.m_mobileControl) 
                    {
                        TempAimVector = Movement;
                    }
                    else
                    {
                        Ray r = CameraControl.MainCameraControl.MyCamera.ScreenPointToRay(Input.mousePosition);
                        Plane p = new Plane(Vector3.up, Vector3.zero);
                        float f = 0;
                        p.Raycast(r, out f);
                        Vector3 point = r.origin + f * r.direction;
                        TempAimVector = point - transform.position;
                        TempAimVector.y = 0;
                        TempAimVector.Normalize();

                        MyPlayerControl.ReticlePosition = Input.mousePosition;
                    }
                    

                    if (TempAimVector.magnitude != 0)
                    {
                        TempAimVector.Normalize();
                        AimVector = Vector3.RotateTowards(AimVector, TempAimVector, 20 * Time.deltaTime, 20 * Time.deltaTime);
                    }
                }
                else
                {
                    Movement += Input.GetAxis("JoyHorizontal") * CameraRight;
                    Movement += Input.GetAxis("JoyVertical") * CameraForward;

                    TempAimVector += Input.GetAxis("JoyHorizontalLook") * CameraRight;
                    TempAimVector += Input.GetAxis("JoyVerticalLook") * CameraForward;

                    if (Input.GetAxis("Joy1_RT") > 0.5f)
                    {
                        FireHold = true;
                    }

                    if (Input.GetButtonDown("Joy1_X"))
                    {
                        Input_ChangeWeapon = true;
                    }

                    if (Input.GetButtonDown("Joy1_A"))
                    {
                        Input_PlaceMine = true;
                    }

                    if (Input.GetButtonDown("Joy1_B"))
                    {
                        DashInput = true;
                    }

                    if (TempAimVector.magnitude != 0)
                    {
                        TempAimVector.Normalize();
                        AimVector = Vector3.RotateTowards(AimVector, TempAimVector, 20 * Time.deltaTime, 20 * Time.deltaTime);
                    }
                    MyPlayerControl.ReticlePosition = CameraControl.MainCameraControl.MyCamera.WorldToScreenPoint(transform.position + 10 * AimVector);
                }
            }

            //auto aim
            //if (MyPlayerControl.TargetPlayer.MyPlayerChar != null)
            //{
            //    AimVector = MyPlayerControl.TargetPlayer.MyPlayerChar.transform.position - transform.position;
            //    AimVector.y = 0;
            //    AimVector.Normalize();
            //}
            //else
            //{
            //    if (Movement.magnitude != 0)
            //    {
            //        AimVector = Vector3.RotateTowards(AimVector, Movement, 10 * Time.deltaTime, 10 * Time.deltaTime);
            //        //AimVector = Quaternion.Euler(0, AimAngle, 0)*Vector3.forward;
            //    }
            //    ArrowBase.gameObject.SetActive(false);
            //}

            if (MyPlayerControl.CurrentWeapon.AmmoCount <= 0 && MyPlayerControl.CurrentWeaponNum != 0)
            {
                ChangeEmptyWeaponDelay -= Time.deltaTime;
                if (ChangeEmptyWeaponDelay <= 0)
                {
                    ChangeEmptyWeaponDelay = 2;
                    Input_ChangeWeapon = true;
                }
            }

            if (Input_ChangeWeapon)
            {
                int num = MyPlayerControl.CurrentWeaponNum;
                while (true)
                {
                    num++;
                    if (num > 4)
                        num = 0;
                    if ((MyPlayerControl.Weapons[num].WeaponEnable && MyPlayerControl.Weapons[num].AmmoCount > 0) || num == 0)
                    {
                        MyPlayerControl.CurrentWeaponNum = num;
                        MyPlayerControl.CurrentWeapon = MyPlayerControl.Weapons[num];
                        ArmWeapon();
                        GameUI.MainGameUI.ShowWeaponInfo(MyPlayerControl.ControllerNum);
                        break;
                    }
                }
                ChangeEmptyWeaponDelay = 2;
                //SoundGallery.PlaySound("ChangeWeapon");
            }

            FireDelay -= Time.deltaTime;
            if (FireDelay <= 0)
                FireDelay = 0;

            if (MyPlayerControl.CurrentWeapon.AutoFire)
            {
                if (FireHold)
                {
                    if (FireDelay == 0)
                    {
                        if (MyPlayerControl.CurrentWeapon.AmmoCount > 0 || MyPlayerControl.CurrentWeapon.InfiniteAmmo)
                        {
                            FireWeapon();
                        }
                        else
                        {
                            //SoundGallery.PlaySound("EmptyFire1");
                        }
                        FireDelay = MyPlayerControl.CurrentWeapon.FireDelay;
                    }
                }
            }
            else
            {
                if (Fire)
                {
                    if (FireDelay == 0)
                    {
                        if (MyPlayerControl.CurrentWeapon.AmmoCount > 0 || MyPlayerControl.CurrentWeapon.InfiniteAmmo)
                        {
                            FireWeapon();
                        }
                        else
                        {
                            //SoundGallery.PlaySound("EmptyFire1");
                        }
                        FireDelay = MyPlayerControl.CurrentWeapon.FireDelay;
                    }
                }
            }

            DamageShakeArc -= 2 * Time.deltaTime;
            if (DamageShakeArc <= 0)
            {
                DamageShakeArc = 0;
            }

            HealthBarBase.transform.localScale = (1 + 0.06f * DamageShakeArc * Mathf.Sin(40 * Time.time)) * Vector3.one;

            //if (Input_PlaceMine)
            //{
            //    if (MyPlayerControl.Ammo_Mine > 0)
            //    {
            //        GameObject obj = (GameObject)Instantiate(GlobalContents.MainGlobalContent.ItemPrefabs[0]);
            //        Vector3 pos = transform.position;
            //        pos.y = 0;
            //        obj.transform.position = pos;
            //        MyPlayerControl.Ammo_Mine--;
            //        Mine_A m = obj.GetComponent<Mine_A>();
            //        m.Creator = MyPlayerControl;
            //    }
            //}


            //if (FireGrenade)
            //{
            //    GameObject obj = (GameObject)Instantiate(GrenadePrefab1);
            //    obj.transform.position = transform.position + new Vector3(0, 2, 0) + AimVector;
            //    Rigidbody rb = obj.GetComponent<Rigidbody>();
            //    rb.velocity = 10 * AimVector + new Vector3(0, 30, 0);
            //    rb.angularVelocity += new Vector3(40, 100, 50);

            //    SoundGallery.PlaySound("Throw1");
            //    if (MyPlayerControl.TargetPlayer.MyPlayerChar != null)
            //    {

            //    }
            //}

            DashReuseTimer -= Time.deltaTime;
            if (DashReuseTimer <= 0)
            {
                DashReuseTimer = 0;
            }




            float maxSpeed = 5;
            Movement.Normalize();
            Vector3 totalMovement = Vector3.zero;

            if (Movement.magnitude > 0)
            {
                transform.forward = Movement;
                MyAnimator.SetTrigger("Run");
            }
            else
            {
                MyAnimator.SetTrigger("Stand");
            }



            StateTime = Time.time - StateStartTime;

            switch (CharState)
            {
                case CharStates.stand:
                    maxSpeed = 10;
                    totalMovement = maxSpeed * Movement;
                    if (DashReuseTimer == 0 && Movement.magnitude > 0)
                    {
                        if (DashInput)
                        {
                            StartState(CharStates.dash);
                            DashReuseTimer = .5f;
                            DashDirection = Movement;

                            GameObject obj = Instantiate(GlobalContents.MainGlobalContent.DashEffectPrefabs[MyPlayerControl.ControllerNum]);
                            obj.transform.position = transform.position;
                            obj.transform.forward = DashDirection;
                            Destroy(obj, 2);

                            obj = Instantiate(GlobalContents.MainGlobalContent.DashSpherePrefabs[MyPlayerControl.ControllerNum]);
                            obj.transform.parent = transform;
                            obj.transform.localPosition = Vector3.zero;
                            obj.transform.forward = DashDirection;
                            Destroy(obj, .2f);
                        }
                    }
                    break;

                case CharStates.run:
                    break;

                case CharStates.ThrowDisk:
                    maxSpeed = 10;
                    totalMovement = maxSpeed * Movement;
                    BodyBase.localRotation = Quaternion.Euler(0, 0 - 720 * StateTime, 0) * BodyBase.localRotation;
                    if (StateTime >= .5f)
                    {
                        StartState(CharStates.stand);
                    }
                    break;

                case CharStates.dash:
                    maxSpeed = 50;
                    totalMovement = maxSpeed * DashDirection;
                    if (StateTime > .2f)
                    {
                        StartState(CharStates.stand);
                    }

                    if (CheckDashHit())
                    {
                        StartState(CharStates.stand);
                    }

                    break;
            }


            //movement
            totalMovement += 20 * Vector3.down;
            CharCon.Move(Time.deltaTime * totalMovement);

            for (int i = 0; i < 30; i++)
            {
                if (i < (int)(MyDamageControl.Damage / (MyDamageControl.MaxDamage / 30f)))
                {
                    if (MyDamageControl.Damage / MyDamageControl.MaxDamage > .3f)
                        HealthBarRenderers[i].material = HealthBarMats[1];
                    else
                        HealthBarRenderers[i].material = HealthBarMats[2];
                }
                else
                {
                    HealthBarRenderers[i].material = HealthBarMats[0];
                }
            }


            //fall
            if (transform.position.y <= -8f)
            {
                Vector3 dmgDir = Vector3.down + transform.position;
                dmgDir.Normalize();
                MyDamageControl.ApplyDamage(20, dmgDir, 2);
            }

            if (MyDamageControl.IsDead)
            {

                //if (transform.position.y <= 2)
                //{
                //    GameObject obj1 = Instantiate(GlobalContents.MainGlobalContent.DecalsPrefabs[1]);
                //    Vector3 pos = transform.position;
                //    pos.y = 0.1f;
                //    obj1.transform.position = pos;
                //    Destroy(obj1, 30);
                //}


                GameObject obj = Instantiate(KillEffectPrefab1);
                obj.transform.position = transform.position + new Vector3(0, 1, 0);
                Destroy(obj, 6);

                for (int i = 0; i < 1; i++)
                {
                    obj = Instantiate(RagdollPrefab1);
                    obj.transform.position = transform.position;
                    obj.transform.forward = AimVector;
                    Rigidbody[] rbs = obj.GetComponentsInChildren<Rigidbody>();
                    foreach (Rigidbody r in rbs)
                    {
                        float ForcePower = 4 + MyDamageControl.LastDamageFactor;
                        //r.AddForce(ForcePower* 100 * MyDamageControl.LastDamageDirection + new Vector3(0, ForcePower * 100, 0));
                        if (r.gameObject.name.Contains("Top") || r.gameObject.name.Contains("Bottom"))
                        {
                            r.AddForce(ForcePower * 1000 * MyDamageControl.LastDamageDirection + new Vector3(0, ForcePower * 1000, 0));
                        }
                    }
                    Ragdoll rd = obj.GetComponent<Ragdoll>();
                    rd.BodyRenderer.materials[0].color = MyPlayerControl.PlayerMats[0].color;
                }


                Destroy(obj, 8);


                MyPlayerControl.LastDeathPosition = transform.position;
                MyPlayerControl.Kill();
                int rs = Random.Range(1, 4);
                SoundGallery.PlaySound("Die" + rs.ToString());

                PlayerControl killer = null;
                foreach (PlayerControl p in GameControl.MainGameControl.PlayerControls)
                {
                    if (p != MyPlayerControl)
                    {
                        killer = p;
                        break;
                    }
                }

                if (killer != null)
                {
                    killer.Kills++;
                    GameControl.MainGameControl.ShowKillMessage(killer, MyPlayerControl, transform.position + new Vector3(0, 0, 4));
                    MenuControl.GeneralMenuControl.ShowMenu("ScoreMessage1");
                }

                CameraControl.MainCameraControl.StartShake(1, 1);
                GameControl.MainGameControl.StartSlowMotion();
                Destroy(gameObject);
            }
        }

        public void ShakeDamage()
        {
            DamageShakeArc = 2;
        }
        void LateUpdate()
        {
            BodyBase.rotation = Quaternion.LookRotation(AimVector) * Quaternion.Inverse(transform.rotation) * BodyBase.rotation;
            ArrowBase.rotation = Quaternion.LookRotation(AimVector);
            ArmBase.transform.localRotation = Quaternion.Euler(FireDelay * 20, 0, 0) * ArmBase.transform.localRotation;
            HealthBarBase.rotation = Quaternion.identity;

        }
        public void FireWeapon()
        {
            MyPlayerControl.CurrentWeapon.FireWeapon(transform.position + AimVector, AimVector);


            if (!MyPlayerControl.CurrentWeapon.InfiniteAmmo)
            {
                MyPlayerControl.CurrentWeapon.AmmoCount--;
            }
        }
        public void StartState(CharStates state)
        {
            CharState = state;
            StateStartTime = Time.time;
            StateTime = 0;

            //switch (CharState)
            //{
            //}
        }

        public bool CheckDashHit()
        {
            Collider[] colls = Physics.OverlapSphere(transform.position + DashDirection + new Vector3(0, 1, 0), 1.2f);
            foreach (Collider col in colls)
            {
                if (col.gameObject == gameObject)
                    continue;

                if (col.gameObject.tag == "Player")
                {
                    PlayerChar p = col.gameObject.GetComponent<PlayerChar>();
                    p.MyDamageControl.ApplyDamage(10, p.transform.position + new Vector3(0, 2, 0) - transform.position, 4);
                    //print(Vector3.Distance(col.bounds.center, transform.position));

                    GameObject obj = Instantiate(GlobalContents.MainGlobalContent.DashHitPrefab1);
                    obj.transform.position = transform.position + 1 * DashDirection + new Vector3(0, 1, 0);
                    Destroy(obj, 6);
                    CameraControl.MainCameraControl.StartShake(.5f, .5f);

                    return true;
                }
                else if (col.gameObject.tag == "Block")
                {
                    Rigidbody rb = col.gameObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddForceAtPosition(10000 * DashDirection + new Vector3(0, 5000, 0), col.gameObject.transform.position + new Vector3(0, 2, 0));
                    }

                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        d.ApplyDamage(3, DashDirection, 1);
                    }

                    GameObject obj = Instantiate(GlobalContents.MainGlobalContent.DashHitPrefab1);
                    obj.transform.position = transform.position + 1 * DashDirection + new Vector3(0, 1, 0);
                    Destroy(obj, 6);

                    return true;
                }
            }

            return false;
        }

    }
}
