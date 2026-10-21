using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace AddrAuditor.Editor
{
    [CreateAssetMenu(fileName = "OptimizedBuildScriptPackedMode.asset", menuName = "Addressables/Content Builders/Optimized Build Script")]
    public class OptimizedBuildScriptPackedMode : BuildScriptPackedMode
    {
        public override string Name => "Optimized Build Script";
        bool sripUnityVersionCache;

        public override void ClearCachedData()
        {
            // NOTE: we should clear callbacks
            ContentPipeline.BuildCallbacks.PostScriptsCallbacks = null;
            ContentPipeline.BuildCallbacks.PostPackingCallback = null;

            //Debug.LogWarning("Clear BuildCallbacks-------------"); // for checking

            base.ClearCachedData();
        }

        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            // NOTE: 
            // we can reduce bundle data if disabled TypeTree with the following restrictions:
            // - required full build, not content update (no version compatibility) 
            // - using only for Player, not for Editor
            ContentPipeline.BuildCallbacks.PostScriptsCallbacks = (buildParameters, dependencyData) =>
            {
                buildParameters.ContentBuildFlags |= ContentBuildFlags.DisableWriteTypeTree;
                return ReturnCode.Success;
            };

            ContentPipeline.BuildCallbacks.PostPackingCallback = PostPacking;

            // no need to set (not be allowed) StripUnityVersion with DisableWriteTypeTree
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var serializedObject = new SerializedObject(settings);
            var stripUnityVersionProp = serializedObject.FindProperty("m_StripUnityVersionFromBundleBuild");
            this.sripUnityVersionCache = stripUnityVersionProp.boolValue;
            stripUnityVersionProp.boolValue = false;
            serializedObject.ApplyModifiedProperties();

            return base.BuildDataImplementation<TResult>(builderInput);
        }

        ReturnCode PostPacking(IBuildParameters buildParameters, IDependencyData dependencyData, IWriteData writeData)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var usedAssets = new HashSet<ObjectIdentifier>();

            // Explicit Groupのエントリから使用されるAssetを抽出
            // 参照されているAssetが欲しいのでGUIDではなくObjectIdentifier
            foreach (var group in settings.groups)
            {
                // 自動生成したSharedGroupを除外、prefixの"+"で弾いてもいいが
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                var includeInCatalog = schema.IncludeAddressInCatalog || schema.IncludeGUIDInCatalog || schema.IncludeLabelsInCatalog;
                if (group.ReadOnly || !includeInCatalog)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry.IsScene)
                    {
                        var buildSettings = buildParameters.GetContentBuildSettings();
                        var usageTags = new BuildUsageTagSet();
                        var sceneInfo = ContentBuildInterface.CalculatePlayerDependenciesForScene(entry.AssetPath, buildSettings, usageTags,
                            dependencyData.DependencyUsageCache);
                        foreach (var objId in sceneInfo.referencedObjects)
                            usedAssets.Add(objId);
                    }
                    else
                    {
                        // Explicit AssetのObjectIdentifier
                        var guid = new GUID(entry.guid);
                        var objects = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(guid, buildTarget);
                        foreach (var objId in objects)
                            usedAssets.Add(objId);

                        // Implicit AssetのObjectIdentifier
                        objects = ContentBuildInterface.GetPlayerDependenciesForObjects(objects, buildTarget, null);
                        foreach (var objId in objects)
                            usedAssets.Add(objId);

                        // NOTE: フォルダは依存関係としてSubAssetが検出されないので明示検索
                        if (entry.IsFolder && entry.SubAssets != null)
                        {
                            foreach (var subAsset in entry.SubAssets)
                            {
                                guid = new GUID(subAsset.guid);
                                objects = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(guid, buildTarget);
                                foreach (var objId in objects)
                                    usedAssets.Add(objId);
                            }
                        }
                    }
                }
            }

            // bundleに含まれるObjectIdentiferから参照をもたないもの（利用されていないもの）を除外する
            // 主としてfbxのRig, Avator, Mesh, Material等を目的とする
            // NOTE: fbxはコンテナ扱いなので必要なSubAssetの重複を解決する為にfbxをエントリすると使用しないSubAssetまでついてくる
            //       特にRigのGameObject/Transformはロード時にMain ThreadのPreloadManager.EarlyUpdateにてヒッチを起こす原因となる
            //       AnimationClip用のfbxを多用するケースにおいて不要なRigが対象に含まれシームレスロードのブロックとなった
            foreach (var op in writeData.WriteOperations)
            {
                // AssetBundleWriteOperation以外は不要
                if (op is AssetBundleWriteOperation abOp)
                {
                    foreach (var info in abOp.Info.bundleAssets)
                    {
                        for (var i = 0; i < info.includedObjects.Count; ++i)
                        {
                            var objId = info.includedObjects[i];
                            if (!usedAssets.Contains(objId))
                            {
                                info.includedObjects.RemoveAt(i);
                                --i;

                                var instance = ObjectIdentifier.ToObject(objId);
                                var path = AssetDatabase.GUIDToAssetPath(objId.guid);
                                Debug.Log($"Removed unreferenced IncludedObjects ---- {path} : {instance.name}[{instance.GetType()}]");
                            }
                        }
                    }
                }
            }

            var serializedObject = new SerializedObject(settings);
            var stripUnityVersionProp = serializedObject.FindProperty("m_StripUnityVersionFromBundleBuild");
            stripUnityVersionProp.boolValue = this.sripUnityVersionCache;
            serializedObject.ApplyModifiedProperties();

            return ReturnCode.Success;
        }
    }
}
