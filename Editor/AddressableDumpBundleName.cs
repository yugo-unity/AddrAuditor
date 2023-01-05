/***********************************************************************************************************
 * AddressableDumpBundleName.cs
 * Copyright (c) Yugo Fujioka - Unity Technologies Japan K.K.
 * 
 * Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
 * https://unity.com/legal/licenses/unity-companion-license
 * Unless expressly provided otherwise, the Software under this license is made available strictly
 * on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
 * Please review the license for details on these and other terms and conditions.
***********************************************************************************************************/

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.Build.Pipeline;

namespace UTJ {
    internal class AddressableDumpBundleName : Editor {

        [MenuItem("UTJ/ADDR Dump Bundle Hash")]
        private static void OpenWindow() {
            new DumpBundleName();
        }

        /// <summary>
        /// AddressablesのbundleのHashとグループ名のダンプ
        /// MemoryProfilerではHash名しかでないので照合用
        /// </summary>
        class DumpBundleName : BundleRuleBase {
            public DumpBundleName() {
                var settings = AddressableAssetSettingsDefaultObject.Settings;

                // Analyze共通処理
                ClearAnalysis();
                if (!BuildUtility.CheckModifiedScenesAndAskToSave()) {
                    UnityEngine.Debug.LogError("Cannot run Analyze with unsaved scenes");
                    return;
                }
                CalculateInputDefinitions(settings);
                var context = GetBuildContext(settings);
                var exitCode = RefreshBuild(context);
                if (exitCode < ReturnCode.Success) {
                    UnityEngine.Debug.LogError($"Analyze build failed. {exitCode}");
                    return;
                }

                // Addressables 1.20以降はReflection不要
                //this.extractData = this.ExtractData;
                var extractDataField = this.GetType().GetField("m_ExtractData", BindingFlags.Instance | BindingFlags.NonPublic);
                var extractData = (ExtractDataTask)extractDataField.GetValue(this);

                foreach (var pair in extractData.WriteData.FileToBundle) {

                    var bundleName = pair.Value;

                    // Hashを取り除いてグループ名と結合
                    var temp = System.IO.Path.GetFileName(bundleName).Split(new string[] { "_assets_", "_scenes_" }, System.StringSplitOptions.None);
                    var title = temp[temp.Length - 1];
                    if (context.bundleToAssetGroup.TryGetValue(bundleName, out var groupGUID)) {
                        var groupName = context.Settings.FindGroup(findGroup => findGroup != null && findGroup.Guid == groupGUID).name;
                        title = $"{groupName}/{title}";
                    }

                    // MemoryManagerでは {FileID}.bundle で表示される
                    // Console Logに出力して該当IDを検索すれば該当ファイルがわかるようにする
                    UnityEngine.Debug.LogWarning($"File ID : {pair.Key} || Internal Name {temp[0]} || Group+Asset {title}");
                }
            }
        }
    }
}
