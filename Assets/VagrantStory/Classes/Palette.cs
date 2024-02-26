using System;
using UnityEngine;

namespace VagrantStory.Classes
{
    [Serializable]
    public class Palette
    {
        public Color32[] colors;

        public Palette(uint numColors)
        {
            colors = new Color32[numColors];
        }
    }
}
