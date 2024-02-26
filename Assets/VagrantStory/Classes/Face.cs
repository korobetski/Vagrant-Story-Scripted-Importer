using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VagrantStory.Classes
{
    [Serializable]
    public class Face
    {
        public byte type;
        public byte size;
        public byte side;
        public byte alpha;
        public byte verticesCount;
        public List<ushort> vertices;
        public List<Color32> colors;
        public List<Vector2> uv;

        public Face()
        {
            vertices = new List<ushort>();
            uv = new List<Vector2>();
            colors = new List<Color32>();
        }

        public Vector2 GetUV(int i, uint width, uint height)
        {
            width = width - 1;
            height = height - 1;
            float u = uv[i].x / width;
            float v = uv[i].y / height;
            return new Vector2(u, v);
        }
    }
}
