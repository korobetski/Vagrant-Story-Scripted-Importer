using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using VagrantStory.Formats;

namespace VagrantStory
{

    [ScriptedImporter(150, "WEP")]
    public class WepImporter : ScriptedImporter
    {
        public bool buildScriptableObject = true;
        public bool buildMesh = true;
        public bool buildModel = true;
        public bool buildPalette = true;
        public bool buildTextures = true;
        public bool buildMaterial = true;
        public Materials weaponMaterial = Materials.WOOD;


        public override void OnImportAsset(AssetImportContext ctx)
        {
            FileStream fs = File.OpenRead(ctx.assetPath);
            BinaryReader buffer = new BinaryReader(fs);

            string[] hash = ctx.assetPath.Split('/');
            string cleanFilename = hash[hash.Length - 1];

            WEP weapon = ScriptableObject.CreateInstance<WEP>();
            weapon.Filename = cleanFilename;
            weapon.ParseFromBuffer(buffer, fs.Length);

            Texture texture = null;
            Mesh mesh = null;
            Material material = null;

            if (buildScriptableObject)
            {
                weapon.name = cleanFilename + "_DEF";
                ctx.AddObjectToAsset("WEP_DEFINITION", weapon);
            }
            if (buildMesh)
            {
                mesh = weapon.BuildMesh();
                mesh.name = cleanFilename + "_MESH";
                ctx.AddObjectToAsset("WEP_MESH", mesh);
            }
            if (buildTextures)
            {
                Texture2D[] textures = weapon.TIM.GetTextures();
                for (byte i = 0; i < textures.Length; i++)
                {
                    textures[i].name = cleanFilename + "_TEX_" + ((Materials)i);
                    ctx.AddObjectToAsset("WEP_TEXTURE", textures[i]);
                    if ((Materials) i == weaponMaterial)
                    {
                        texture = textures[i];
                    }
                }
            }
            if (buildMaterial)
            {
                material = weapon.BuildMaterial(texture);
                material.name = cleanFilename + "_MAT";
                ctx.AddObjectToAsset("WEP_MATERIAL", material);
            }

            if (buildModel)
            {

                GameObject modelContainer = new GameObject(cleanFilename);
                MeshFilter mf = modelContainer.AddComponent<MeshFilter>();
                MeshRenderer mr = modelContainer.AddComponent<MeshRenderer>();
                mf.mesh = mesh;
                mr.material = material;
                ctx.AddObjectToAsset(cleanFilename, modelContainer);
                ctx.SetMainObject(modelContainer);
            }

            fs.Close();
        }


        [CustomEditor(typeof(WepImporter))]
        public class WepImporterEditor : ScriptedImporterEditor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buildScriptableObject"), new GUIContent("Generate Scriptable Object ?"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buildMesh"), new GUIContent("Generate 3D Mesh ?"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buildModel"), new GUIContent("Generate 3D Model ?"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buildPalette"), new GUIContent("Generate Palette ?"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buildTextures"), new GUIContent("Generate Textures ?"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("buildMaterial"), new GUIContent("Generate Material ?"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponMaterial"), new GUIContent("Smithing Material"));

                serializedObject.ApplyModifiedProperties();
                base.ApplyRevertGUI();
            }
        }
    }
}
