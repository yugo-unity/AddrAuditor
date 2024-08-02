using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UTJ
{
    internal partial class AddrAuditor : EditorWindow
    {
        public static void CleanupMaterialProperty()
        {
            var serachFolder = new string[] { "Assets", };
            //var guids = AssetDatabase.FindAssets("t:Material"); // Packages�܂ߑS��������
            var guids = AssetDatabase.FindAssets("t:Material", serachFolder); // �C�Ӄf�B���N�g���w��

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null)
                    continue;

                // Property�̎��W
                var properties = new HashSet<string>();
                var count = ShaderUtil.GetPropertyCount(m.shader);
                for (int i = 0; i < count; i++)
                {
                    var propName = ShaderUtil.GetPropertyName(m.shader, i);
                    properties.Add(propName);
                }

                var so = new SerializedObject(m);
                var sp = so.FindProperty("m_SavedProperties");

                // �e�N�X�`����Property
                var texEnvSp = sp.FindPropertyRelative("m_TexEnvs");
                for (var i = texEnvSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = texEnvSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;

                    if (!properties.Contains(propName))
                        texEnvSp.DeleteArrayElementAtIndex(i);
                }

                // float��Enum��Property
                var floatsSp = sp.FindPropertyRelative("m_Floats");
                for (var i = floatsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = floatsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        floatsSp.DeleteArrayElementAtIndex(i);
                }

                // Color��Property
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