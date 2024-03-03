using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VagrantStory.Classes;

namespace VagrantStory.Formats
{
    public enum Materials
    {
        NONE = 0,
        WOOD = 1,
        LEATHER = 2,
        BRONZE = 3,
        IRON = 4,
        HAGANE = 5,
        SILVER = 6,
        DAMASCUS = 7
    }

    public class WEP : ScriptableObject
    {
        public string Filename;

        public byte WEPId;
        public byte Id;
        public string Name;
        public string Description;

        public uint Signature;
        public byte NumBones;
        public byte NumGroups;
        public ushort NumTriangles;
        public ushort NumQuads;
        public ushort NumPolygons;
        public uint NumFaces { get => (uint)NumTriangles + NumQuads + NumPolygons; }

        // Pointers values need +16 in WEP and more in ZUD
        public uint PtrTexture;
        public byte[] Padding0x30;

        public uint PtrTextureSection;
        public uint PtrGroupSection;
        public uint PtrVertexSection;
        public uint PtrFaceSection;

        public Bone[] Bones;
        public Group[] Groups;
        public Vertex[] Vertices;
        public Face[] Faces;
        public TIM TIM;

        public byte[] Footer;


        public void ParseFromBuffer(BinaryReader buffer, long limit)
        {

            Signature = buffer.ReadUInt32(); // signiture "H01"

            NumBones = buffer.ReadByte();
            NumGroups = buffer.ReadByte();
            NumTriangles = buffer.ReadUInt16();
            NumQuads = buffer.ReadUInt16();
            NumPolygons = buffer.ReadUInt16();

            long dec = buffer.BaseStream.Position + 4;
            PtrTexture = (uint)(buffer.ReadUInt32() + dec);
            Padding0x30 = buffer.ReadBytes(0x30); // padding

            PtrTextureSection = (uint)(buffer.ReadUInt32() + dec); // same as texturePtr1 in WEP but maybe not in ZUD
            PtrGroupSection = (uint)(buffer.ReadUInt32() + dec);
            PtrVertexSection = (uint)(buffer.ReadUInt32() + dec);
            PtrFaceSection = (uint)(buffer.ReadUInt32() + dec);

            // Bones section
            Bones = new Bone[NumBones];
            for (sbyte i = 0; i < NumBones; i++)
            {
                Bone bone = new Bone();
                bone.index = i;
                //bone.name = "bone_" + i;
                // https://github.com/morris/vstools/blob/master/src/WEPBone.js
                bone.length = buffer.ReadInt32();
                bone.parentBoneId = buffer.ReadSByte();
                bone.groupId = buffer.ReadSByte();
                bone.mountId = buffer.ReadByte();
                bone.bodyPartId = buffer.ReadByte();
                bone.mode = buffer.ReadSByte();
                bone.unk = buffer.ReadBytes(7); // always 0000000
                if (bone.parentBoneId != -1 && bone.parentBoneId != 47)
                {
                    bone.SetParentBone(Bones[bone.parentBoneId]);
                }
                Bones[i] = bone;
            }

            // Group section
            if (buffer.BaseStream.Position != PtrGroupSection) buffer.BaseStream.Position = PtrGroupSection;
            Groups = new Group[NumGroups];
            for (uint i = 0; i < NumGroups; i++)
            {
                Group group = new Group();
                group.boneIndex = buffer.ReadInt16();
                group.numVertices = buffer.ReadUInt16();
                // if (group.boneIndex != -1) group.bone = bones[group.boneIndex];

                Groups[i] = group;
            }

            // Vertices section
            if (buffer.BaseStream.Position != PtrVertexSection) buffer.BaseStream.Position = PtrVertexSection;

            uint numVertices = Groups[Groups.Length - 1].numVertices;
            Vertices = new Vertex[numVertices];
            int g = 0;
            for (uint i = 0; i < numVertices; i++)
            {
                if (i >= Groups[g].numVertices)
                {
                    g++;
                }

                Vertex vertex = new Vertex();
                vertex.index = i;
                vertex.group = (byte)g;
                vertex.bone = Bones[Groups[g].boneIndex];

                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = (int)Groups[g].boneIndex;
                bw.weight0 = 1;

                vertex.position = new Vector4(buffer.ReadInt16(), buffer.ReadInt16(), buffer.ReadInt16(), buffer.ReadInt16());
                vertex.boneWeight = bw;

                Vertices[i] = vertex;
            }

            // Polygone section
            if (buffer.BaseStream.Position != PtrFaceSection)
            {
                buffer.BaseStream.Position = PtrFaceSection;
            }

            Faces = new Face[NumFaces];
            for (uint i = 0; i < NumFaces; i++)
            {
                Face face = new Face();
                face.type = buffer.ReadByte();
                face.size = buffer.ReadByte();
                face.side = buffer.ReadByte();
                face.alpha = buffer.ReadByte();
                face.verticesCount = 3;
                if (face.type == 36)
                {
                    face.verticesCount = 3;
                }
                else if (face.type == 44)
                {
                    face.verticesCount = 4;
                }

                face.vertices = new List<ushort>();
                for (uint j = 0; j < face.verticesCount; j++)
                {
                    ushort vId = (ushort)(buffer.ReadUInt16() / 4);
                    face.vertices.Add(vId);
                }
                face.uv = new List<Vector2>();
                for (uint j = 0; j < face.verticesCount; j++)
                {
                    int u = buffer.ReadByte();
                    int v = buffer.ReadByte();
                    face.uv.Add(new Vector2(u, v));
                }

                Faces[i] = face;
            }

            // Textures section
            if (buffer.BaseStream.Position != PtrTextureSection)
            {
                buffer.BaseStream.Position = PtrTextureSection;
            }
            
            TIM = ScriptableObject.CreateInstance<TIM>();
            TIM.name = Filename + ".WEP.TIM";
            TIM.ParseWEPFromBuffer(buffer);
            
            // rotations
            // its look like SEQAnim rotationPerBone
            Footer = buffer.ReadBytes(24);
        }

        public Mesh BuildMesh()
        {
            Mesh weaponMesh = new Mesh();
            List<Vector3> meshVertices = new List<Vector3>();
            List<int> meshTriangles = new List<int>();
            List<Vector2> meshTrianglesUV = new List<Vector2>();
            List<BoneWeight> meshWeights = new List<BoneWeight>();

            foreach (Bone bone in Bones)
            {
                if (bone.parentBoneId != -1 && bone.parentBoneId != 47) bone.SetParentBone(Bones[bone.parentBoneId]);
            }

            foreach (Group group in Groups)
            {
                group.bone = Bones[group.boneIndex];
            }

            foreach (Vertex vertex in Vertices)
            {
                vertex.bone = Bones[Groups[vertex.group].boneIndex];
            }

            // hard fixes for staves 39.WEP to 3F.WEP
            // the "bone" of the first group should be rotated to looks good but we correct vertices position instead of using bones
            List<string> staves = new List<string> { "39", "3A", "3B", "3C", "3D", "3E", "3F" };
            if (staves.Contains(Filename))
            {
                for (int i = 0; i < Groups[0].numVertices; i++)
                {
                    Vertices[i].position.x = (-Groups[0].bone.length * 2 - Vertices[i].position.x);
                    Vertices[i].position.y = -Vertices[i].position.y;
                }
            }

            // Geometry
            for (int i = 0; i < Faces.Length; i++)
            {
                if (Faces[i].type == 0x2C)
                {
                    if (Faces[i].side != 4)
                    {
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));

                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[3]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[3]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(3, TIM.width, TIM.height));

                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[3]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[3]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(3, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));

                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[3]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[3]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(3, TIM.width, TIM.height));
                    }
                    else
                    {
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));

                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[3]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[3]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(3, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                    }
                }
                else if (Faces[i].type == 0x24)
                {
                    if (Faces[i].side != 4)
                    {
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));

                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));
                    }
                    else
                    {
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[0]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[0]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[1]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[1]].boneWeight);
                        meshTriangles.Add(meshVertices.Count);
                        meshVertices.Add(Vertices[Faces[i].vertices[2]].GetAbsPosition());
                        meshWeights.Add(Vertices[Faces[i].vertices[2]].boneWeight);
                        meshTrianglesUV.Add(Faces[i].GetUV(1, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(2, TIM.width, TIM.height));
                        meshTrianglesUV.Add(Faces[i].GetUV(0, TIM.width, TIM.height));
                    }
                }
            }

            for (int i = 0; i < meshVertices.Count; i++)
            {
                meshVertices[i] = -meshVertices[i] / 128;
            }

            weaponMesh.name = Filename + "_mesh";
            weaponMesh.vertices = meshVertices.ToArray();
            weaponMesh.triangles = meshTriangles.ToArray();
            weaponMesh.uv = meshTrianglesUV.ToArray();
            weaponMesh.boneWeights = meshWeights.ToArray();
            weaponMesh.Optimize();

            return weaponMesh;
        }

        public Material BuildMaterial(Texture texture)
        {
            Shader shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.name = Filename + "_mat";
            mat.SetTexture("_MainTex", texture);
            mat.SetTextureScale("_MainTex", new Vector2(1, 1));
            mat.SetTextureOffset("_MainTex", new Vector2(0, 0));
            mat.SetFloat("_Mode", 1);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Glossiness", 0.0f);
            mat.SetFloat("_SpecularHighlights", 0.0f);
            mat.SetFloat("_GlossyReflections", 0.0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 2450;

            return mat;
        }
    }
}