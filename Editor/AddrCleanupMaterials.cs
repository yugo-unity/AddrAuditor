using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UTJ
{
    internal partial class AddrAuditor
    {
        static void CleanupMaterialProperty()
        {
            //var guids = AssetDatabase.FindAssets("t:Material"); // include Packages
            var serachFolder = new string[] { "Assets", };
            var guids = AssetDatabase.FindAssets("t:Material", serachFolder);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null)
                    continue;

                var properties = new HashSet<string>();
                var count = ShaderUtil.GetPropertyCount(m.shader);
                for (var i = 0; i < count; i++)
                {
                    var propName = ShaderUtil.GetPropertyName(m.shader, i);
                    properties.Add(propName);
                }

                var so = new SerializedObject(m);
                var sp = so.FindProperty("m_SavedProperties");

                var texEnvSp = sp.FindPropertyRelative("m_TexEnvs");
                for (var i = texEnvSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = texEnvSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;

                    if (!properties.Contains(propName))
                        texEnvSp.DeleteArrayElementAtIndex(i);
                }

                var floatsSp = sp.FindPropertyRelative("m_Floats");
                for (var i = floatsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = floatsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        floatsSp.DeleteArrayElementAtIndex(i);
                }
                
                var intSp = sp.FindPropertyRelative("m_Ints");
                for (var i = intSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = intSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        intSp.DeleteArrayElementAtIndex(i);
                }

                var colorsSp = sp.FindPropertyRelative("m_Colors");
                for (var i = colorsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = colorsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        colorsSp.DeleteArrayElementAtIndex(i);
                }

                so.ApplyModifiedProperties();
            }
        }
    }
}