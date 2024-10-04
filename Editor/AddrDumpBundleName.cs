using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.Build.Pipeline;
using UnityEngine;

namespace AddrAuditor.Editor
{
    internal partial class AddrAuditor
    {
        /// <summary>
        /// AddressablesのbundleのHashとグループ名のダンプ
        /// MemoryProfilerではHash名しかでないので照合用
        /// </summary>
        static void DumpBundleName()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, string>();
            AddrUtility.CalculateInputDefinitions(settings, allBundleInputDefs, bundleToAssetGroup);
            var aaContext = AddrUtility.GetBuildContext(settings, bundleToAssetGroup);
            var extractData = new ExtractDataTask();
            var exitCode = AddrUtility.RefleshBuild(settings, allBundleInputDefs, extractData, aaContext);
            if (exitCode < ReturnCode.Success)
            {
                Debug.LogError($"Analyze build failed. {exitCode}");
                return;
            }

            foreach (var pair in extractData.WriteData.FileToBundle)
            {
                var bundleName = pair.Value;

                // Hashを取り除いてグループ名と結合
                var temp = System.IO.Path.GetFileName(bundleName).Split(new string[] { "_assets_", "_scenes_" },
                    System.StringSplitOptions.None);
                var title = temp[temp.Length - 1];
                if (aaContext.bundleToAssetGroup.TryGetValue(bundleName, out var groupGUID))
                {
                    var groupName = aaContext.Settings
                        .FindGroup(findGroup => findGroup && findGroup.Guid == groupGUID).name;
                    title = $"{groupName}/{title}";
                }

                // MemoryManagerでは {FileID}.bundle で表示される
                // Console Logに出力して該当IDを検索すれば該当ファイルがわかるようにする
                Debug.LogWarning($"File ID : {pair.Key} || Internal Name {temp[0]} || Group+Asset {title}");
            }
        }
    }
}
