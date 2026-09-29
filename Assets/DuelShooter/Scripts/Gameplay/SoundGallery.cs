using UnityEngine;
using System.Collections;
using System.Collections.Generic;
namespace DuelShooter
{
    public class SoundGallery : MonoBehaviour
    {

        public AudioClip[] sounds;
        public Dictionary<string, AudioClip> SoundList;

        public static SoundGallery MainSoundGallery;

        void Awake()
        {
            MainSoundGallery = this;

            SoundList = new Dictionary<string, AudioClip>();

            //for (int i = 0; i < sounds.Length; i++)
            //{
            //    //Menus[i].gameObject.SetActive(true);
            //    string n = sounds[i].name;
            //    if (SoundList.ContainsKey(n))
            //    {
            //        n = n + i.ToString();
            //    }
            //   SoundList.Add(n, sounds[i]);
            //}
        }
        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public static void PlaySound(string sound)
        {
            if (!MainSoundGallery.SoundList.ContainsKey(sound))
                return;

            MainSoundGallery.GetComponent<AudioSource>().pitch = 1;
            MainSoundGallery.GetComponent<AudioSource>().PlayOneShot(MainSoundGallery.SoundList[sound]);
        }

        public static void PlaySound(string sound, float pitch)
        {
            if (!MainSoundGallery.SoundList.ContainsKey(sound))
                return;

            MainSoundGallery.GetComponent<AudioSource>().pitch = pitch;
            MainSoundGallery.GetComponent<AudioSource>().PlayOneShot(MainSoundGallery.SoundList[sound]);

        }

    }
}
