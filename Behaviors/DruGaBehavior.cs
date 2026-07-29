using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LethalBoomba.Behaviors
{
    public class DruGaBehavior : GrabbableObject
    {
        private AudioSource AudioSrc;
        [SerializeField]
        private AudioClip EatSfx;

        void Awake()
        {
            AudioSrc = GetComponent<AudioSource>();
        }
    }
}
