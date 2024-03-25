using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using System.IO;

namespace UTJ
{
    [CreateAssetMenu(fileName = "OptimizedBuildScriptPackedMode.asset", menuName = "Addressables/Content Builders/Optimized Build Script")]
    public class OptimizedBuildScriptPackedMode : BuildScriptPackedMode
    {
        public override string Name => "Optimized Build Script";

        protected override string ProcessGroup(AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            //this.aaContext = aaContext;

#if !UNITY_EDITOR
            ContentPipeline.BuildCallbacks.PostScriptsCallbacks = (buildParameters, dependencyData) => {
                buildParameters.ContentBuildFlags |= ContentBuildFlags.DisableWriteTypeTree;
                return ReturnCode.Success;
            };
#endif

            ContentPipeline.BuildCallbacks.PostWritingCallback = PostWriting;

            return base.ProcessGroup(assetGroup, aaContext);
        }

        public ReturnCode PostWriting(IBuildParameters parameters, IDependencyData dependencyData, IWriteData writeData,
            IBuildResults results)
        {
            var bundleResults = (IBundleBuildResults)results;
            foreach (var ret in bundleResults.BundleInfos.Values)
                EncryptUsingAesStream(ret.FileName);

            return ReturnCode.Success;
        }

        unsafe static void EncryptUsingAesStream(string fileName)
        {
            var bundleName = Path.GetFileNameWithoutExtension(fileName);
            var password = System.Text.Encoding.UTF8.GetBytes(Application.unityVersion);
            Span<byte> salt = stackalloc byte[bundleName.Length];
            bundleName.ConvertTo(salt);
            var fullPath = Application.dataPath + "/../" + fileName;
            var data = File.ReadAllBytes(fullPath); // NOTE: need to read before stream is opened
            using (var cryptor = new SeekableAesStream(fullPath, password, salt, true))
            {
                cryptor.Write(data, 0, data.Length);
            }
        }

        // AddressableAssetsBuildContext aaContext;
        // System.Type animationClipType = typeof(AnimationClip);

        // ReturnCode PostDependency(IBuildParameters buildParameters, IDependencyData dependencyData) {
        //     foreach (var guid in dependencyData.AssetInfo.Keys) {
        //         var path = AssetDatabase.GUIDToAssetPath(guid);
        //         //-----------------------------------------------------------------------------
        //         // Determine if the SubAsset should be removed from the bundle by the file path.
        //         // You can do another way.
        //         // 
        //         // 本サンプルではファイルパスから対象エントリを指定する
        //         // 今回はfbxの拡張子全てに対して行うが、@指定があるかどうかでフィルタリングすべき
        //         //-----------------------------------------------------------------------------
        //         if (path.Contains(".fbx")) {
        //             if (dependencyData.AssetInfo.TryGetValue(guid, out var info)) {
        //                 // //-----------------------------------------------------------------------------
        //                 // // If you want DisableVisibleSubAssetRepresentations is enabled and custom m_ExtendedAssetData,
        //                 // // you will have to custom Addressables package
        //                 // // 
        //                 // // なんらかの理由でDisableVisibleSubAssetRepresentationsを有効にしたくない場合（SubAssetのSprite指定でロードしたいとか‘）
        //                 // // 本処理を行うにはパッケージの改変が必要
        //                 // //-----------------------------------------------------------------------------
        //                 // if (!this.aaContext.Settings.DisableVisibleSubAssetRepresentations && m_ExtendedAssetData != null) {
        //                 //     if (m_ExtendedAssetData.ExtendedData.TryGetValue(guid, out var extendedAssetData)) {
        //                 //         var representations = extendedAssetData.Representations;
        //                 //         for (var i = 0; i < representations.Count; ++i) {
        //                 //             if (ObjectIdentifier.ToObject(representations[i]).GetType() != animationClipType) {
        //                 //                 representations.RemoveAt(i);
        //                 //                 --i;
        //                 //             }
        //                 //         }
        //                 //     }
        //                 // }
        //
        //                 for (var i = 0; i < info.includedObjects.Count; ++i) {
        //                     var objId = info.includedObjects[i];
        //                     if (ObjectIdentifier.ToObject(objId).GetType() != animationClipType) {
        //                         info.includedObjects.RemoveAt(i);
        //                         i--;
        //                     }
        //                 }
        //             }
        //         }
        //
        //         //-----------------------------------------------------------------------------
        //         // If Sprite is entried, Sprite is a sub asset of Texture.
        //         // Sprite will be main asset by moving from includedObject[1] to includedObject[0].
        //         // Texture asset is refered from Sprite.mainTexture.
        //         // 
        //         // Sprite単独でエントリする場合、SpriteはTextureのSubAssetなので同様に対応が必要
        //         // includedObjects[0]にSpriteを設定することでSpriteがMainAssetへ昇格
        //         // Textureの参照はSpriteが持っているのでそちらから代替
        //         //-----------------------------------------------------------------------------
        //         if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D)) {
        //             if (dependencyData.AssetInfo.TryGetValue(guid, out var info)) {
        //                 info.includedObjects.RemoveAt(0);
        //
        //                 for (var i = 1; i < info.includedObjects.Count; ++i) {
        //                     info.includedObjects.RemoveAt(i);
        //                     i--;
        //                 }
        //             }
        //         }
        //     }
        //
        //     return ReturnCode.Success;
        // }

        //ReturnCode PostWriting(IBuildParameters buildParameters, IDependencyData dependencyData, IWriteData writeData, IBuildResults buildResults) {
        //    for (var i = 0; i < this.aaContext.locations.Count; ++i) {
        //        var entry = this.aaContext.locations[i];

        //        if (entry.ResourceType == typeof(IAssetBundleResource))
        //            continue;

        //        var address = "";
        //        //-----------------------------------------------------------------------------
        //        // Normaly index=1 is GUID, but index=2 will be GUID
        //        // when "Include Address in catalog" is enabled. So using foreach
        //        // 
        //        // 通常 Index 1 がGUIDだが、Group SettingsのInclude Address設定が無効だと 0
        //        // 設定に依存する為に全走査
        //        //-----------------------------------------------------------------------------
        //        var keys = this.aaContext.locations[i].Keys;
        //        foreach (var key in keys) {
        //            address = AssetDatabase.GUIDToAssetPath(key as string);
        //            if (string.IsNullOrEmpty(address))
        //                continue;
        //            break;
        //        }

        //        //-----------------------------------------------------------------------------
        //        // Delete asset path that is not loaded on runtime
        //        // This sample is delete AnimationClip address on the premiss of loading
        //        // Animation as AnimationController/AnimatorOverrideController
        //        // 
        //        // 直接呼びださないAssetをcatalogから弾く
        //        // AnimatorControllerおよびAnimatorOverrideControllerで管理する前提で
        //        // AnimationClipの原本はcatalogから弾くなど
        //        //-----------------------------------------------------------------------------
        //        if (address.Contains(".fbx") || entry.ResourceType == animationClipType) {
        //            this.aaContext.locations.RemoveAt(i);
        //            --i;
        //        }

        //        // And any Material, Texture, etc... if you have no need to explicit loading
        //    }

        //    //-----------------------------------------------------------------------------
        //    // Export scene name/Dynamic Id mapping.
        //    // For anyone who want to use SceneManager.GetSceneByName
        //    // with GUID/Dynamic internal asset naming mode.
        //    // 
        //    // internal Asset NamingがGUID/Dynamicの場合、ランタイムのシーン名がIDになる
        //    // ビルド時にシーン名とIDの対象テーブルを用意しておくことで
        //    // SceneManager.GetSceneByNameを従来通り利用可能
        //    //-----------------------------------------------------------------------------
        //    StringBuilder builder = new StringBuilder();
        //    builder.AppendLine("using System.Collections.Generic;\n");
        //    builder.AppendLine("namespace AddressablesExtend {");
        //    builder.AppendLine("\t/// <summary>");
        //    builder.AppendLine("\t/// Mapping asset name / internal name");
        //    builder.AppendLine("\t/// </summary>");
        //    builder.AppendLine("\tpublic static class AssetTable {");
        //    builder.AppendLine("\t\tpublic static readonly Dictionary<string, string> internalSceneName = new () {");
        //    builder.Append("\t\t\t{ ");
        //    foreach (var op in writeData.WriteOperations) {
        //        var sceneOp = op as SceneBundleWriteOperation;
        //        if (sceneOp == null)
        //            continue;

        //        foreach (var scenes in sceneOp.Info.bundleScenes) {
        //            var sceneName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(scenes.asset));
        //            var address = Path.GetFileNameWithoutExtension(scenes.address);
        //            builder.Append($"\"{sceneName}\", \"{address}\" }},\n");
        //        }
        //    }
        //    builder.AppendLine("\t\t};");
        //    builder.AppendLine("\t};");
        //    builder.AppendLine("};");
        //    builder.Replace("\r\n", "\n", 0, builder.Length); // Convert CRLF to LF
        //    File.WriteAllText(Application.dataPath + "\\AddressablesExtend\\AssetTable.cs", builder.ToString(), Encoding.UTF8);
        //    AssetDatabase.Refresh(ImportAssetOptions.ImportRecursive);

        //    return ReturnCode.Success;
        //}

        //-----------------------------------------------------------------------------
        // Normaly you can confirm bundle name/hash mapping
        // by Build Layout when Preference/Addressables/Debug Build Layout is enabled.
        // On the other hand, you can also like this.
        // 
        // Bundle NamingがHash to Filenameの場合、元が何のbundleだったか確認するのに
        // 通常はBuild Layoutを出力することで確認出来るが下記手法で自前で出力することも可能
        //-----------------------------------------------------------------------------
        //protected override string ConstructAssetBundleName(AddressableAssetGroup assetGroup, BundledAssetGroupSchema schema, BundleDetails info, string assetBundleName) {
        //    var ret = base.ConstructAssetBundleName(assetGroup, schema, info, assetBundleName);

        //    // Collect bundle name/hash mapping
        //    Debug.LogWarning($"Bundle name to Hash : {assetBundleName} / {info.Hash}");

        //    return ret;
        //}
    }
}