using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.UIElements;
using UnityEngine;
using VagrantStory.Classes;

namespace VagrantStory.Formats
{

    public class TIM : ScriptableObject
    {
        public uint index;
        public uint length;
        public uint h = 0;
        public uint bpp = 0;
        public uint imgLen = 0;
        public uint fx = 0;
        public uint fy = 0;
        public uint width = 0;
        public uint height = 0;
        public uint dataLen = 0;
        public uint dataPtr = 0;

        public ushort numColors;
        public byte numPalettes;
        public Palette[] palettes;
        public byte[] clut;



        public void ParseWEPFromBuffer(BinaryReader buffer)
        {
            numPalettes = 8;
            // the first palette is the common colors (16)
            imgLen = buffer.ReadUInt32();
            bpp = buffer.ReadByte();
            width = (uint)buffer.ReadByte() * 2;
            height = (uint)buffer.ReadByte() * 2;
            numColors = buffer.ReadByte();

            if (numColors > 0)
            {
                uint numCommonColors = (uint)(numColors / 3);
                uint numPaletteColors = numCommonColors * 2;
                palettes = new Palette[numPalettes];

                for (uint i = 0; i < numPalettes; i++)
                {
                    if (i == 0)
                    {
                        palettes[0] = new Palette(numCommonColors);
                        for (uint j = 0; j < numCommonColors; j++)
                        {
                            palettes[0].colors[j] = (BitColorConverter(buffer.ReadUInt16()));
                        }
                    }
                    else
                    {
                        palettes[i] = new Palette(numPaletteColors);
                        for (uint j = 0; j < numPaletteColors; j++)
                        {
                            palettes[i].colors[j] = (BitColorConverter(buffer.ReadUInt16()));
                        }
                    }
                }
            }

            clut = new byte[width * height];
            for (uint i = 0; i < height * width; i++)
            {
                clut[i] = buffer.ReadByte();
            }
            clut = ReverseCLUT();
        }

        public Texture2D[] GetTextures()
        {
            Texture2D[] textures = new Texture2D[numPalettes];
            for (byte i = 0;i < numPalettes; i++)
            {
                textures[i] = GetTexture(i);
            }
            return textures;
        }

        public Texture2D GetTexture(byte paletteId = 0, bool is8bits = false)
        {
            Texture2D tex = new Texture2D(1, 1);
            uint numCommonColors = (uint)(numColors / 3);
            uint numPaletteColors = numCommonColors * 2;
            List<Color> colors = new List<Color>();

            if (paletteId == 0)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        colors.Add(Grayscale(clut[(int)((y * width) + x)]));
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (clut[(int)((y * width) + x)] < numCommonColors)
                        {
                            colors.Add(palettes[0].colors[clut[(int)((y * width) + x)]]);
                        }
                        else
                        {
                            colors.Add(palettes[paletteId].colors[clut[(int)((y * width) + x)] - numCommonColors]);
                        }
                    }
                }
            }

            tex = new Texture2D((int)width, (int)height, TextureFormat.ARGB32, false);
            tex.SetPixels(colors.ToArray());
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();
            return tex;
        }

        private byte[] ReverseCLUT()
        {
            uint i = 0;
            List<byte> cluts = new List<byte>();
            // width can be wrong
            uint width = (uint)clut.Length / height;
            for (uint x = 0; x < height; x++)
            {
                List<byte> cl2 = new List<byte>();
                for (uint y = 0; y < width; y++)
                {
                    cl2.Add(clut[i]);
                    i++;
                }
                cl2.Reverse();
                cluts.AddRange(cl2);
            }
            cluts.Reverse();
            return cluts.ToArray();
        }

        public Color32 BitColorConverter(ushort rawColor)
        {
            if (rawColor == 0)
            {
                return new Color32(0, 0, 0, 0);
            }
            else
            {
                int a = (rawColor & 0x8000) >> 15;
                int b = (rawColor & 0x7C00) >> 10;
                int g = (rawColor & 0x03E0) >> 5;
                int r = (rawColor & 0x001F);
                if (r == 0 && g == 0 && b == 0)
                {
                    if ((rawColor & 0x8000) == 0)
                    {
                        // black, and the alpha bit is NOT set
                        a = (byte)0; // totally transparent
                    }
                    else
                    {
                        // black, and the alpha bit IS set
                        a = (byte)255; // totally opaque
                    }
                }
                else
                {
                    if ((rawColor & 0x8000) == 0)
                    {
                        // some color, and the alpha bit is NOT set
                        a = (byte)255; // totally opaque
                    }
                    else
                    {
                        // some color, and the alpha bit IS set
                        a = AlphaFromGrayscale(new Color32((byte)(r * 8), (byte)(g * 8), (byte)(b * 8), 0)); // some variance of transparency
                        a = 255;
                    }
                }

                Color32 color = new Color32((byte)(r * 8), (byte)(g * 8), (byte)(b * 8), (byte)a);
                return color;
            }
        }
        
        private Color32 Grayscale(byte c)
        {
            float ratio = 255 / numColors;
            byte v = (byte)Math.Floor(c * ratio);
            return new Color32(v, v, v, 255);
        }

        private byte AlphaFromGrayscale(Color32 cr)
        {
            return (byte)Mathf.Round((cr.r + cr.g + cr.b) / 3);
        }

    }

}