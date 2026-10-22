using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEngine;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// [experimental] simulate the bundle layout after registering implicit assets to the shared groups,
    /// to resolve duplicated assets without re-running the analysis build.
    /// it follows the packing of SBP (CalculateAssetDependencyData / CalculateSceneDependencyData / GenerateBundlePacking).
    /// </summary>
    internal class AddrAutoGroupingSimulator
    {
        const string UNITY_DEFAULT_RESOURCE_PATH = "library/unity default resources"; // CommonStrings.UnityDefaultResourcePath

        class SimFile
        {
            public string bundle;
            public bool isScene;
            public readonly List<GUID> owners = new(); // explicit assets (or a scene) in this file
            public List<ObjectIdentifier> objects;
        }

        class AssetInfo
        {
            public List<ObjectIdentifier> includedObjects;
            public List<ObjectIdentifier> referencedObjects;
            public HashSet<ObjectIdentifier> referencedObjectSet;
        }

        readonly AddressableAssetSettings settings;
        readonly BuildTarget target;
        readonly bool nonRecursive;
        readonly BuildSettings buildSettings;
        readonly BuildUsageCache usageCache = new();

        readonly HashSet<GUID> explicitAssets = new(); // explicit assets (except scenes)
        readonly HashSet<GUID> explicitAll = new(); // explicit assets and scenes
        readonly Dictionary<GUID, AssetInfo> assetInfos = new();
        readonly Dictionary<GUID, List<ObjectIdentifier>> sceneReferences = new();
        readonly Dictionary<GUID, string> scenePaths = new();
        readonly Dictionary<GUID, int> sceneOrder = new(); // order of scenes in the same bundle
        readonly List<SimFile> files = new();
        readonly List<SimFile> virtualFiles = new();
        readonly Dictionary<string, SimFile> virtualFileMap = new();
        readonly Dictionary<ObjectIdentifier, ObjectIdentifier[]> dependencyCache = new();
        readonly ObjectIdentifier[] singleObject = new ObjectIdentifier[1];

        public Dictionary<string, string> BundleToAssetGroup { get; }

        public AddrAutoGroupingSimulator(AddressableAssetSettings settings, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData)
        {
            this.settings = settings;
            this.target = EditorUserBuildSettings.activeBuildTarget;
            this.nonRecursive = settings.NonRecursiveBuilding;
            this.BundleToAssetGroup = new Dictionary<string, string>(aaContext.bundleToAssetGroup);
            var buildParams = new AddressableAssetsBundleBuildParameters(settings, aaContext.bundleToAssetGroup, this.target,
                BuildPipeline.GetBuildTargetGroup(this.target), settings.buildSettings.bundleBuildPath);
            this.buildSettings = buildParams.GetContentBuildSettings();

            var dependencyData = extractData.DependencyData;
            var writeData = extractData.WriteData;
            foreach (var pair in dependencyData.AssetInfo)
            {
                this.explicitAssets.Add(pair.Key);
                this.assetInfos.Add(pair.Key, new AssetInfo()
                {
                    includedObjects = pair.Value.includedObjects,
                    referencedObjects = pair.Value.referencedObjects,
                    referencedObjectSet = new HashSet<ObjectIdentifier>(pair.Value.referencedObjects),
                });
            }
            foreach (var pair in dependencyData.SceneInfo)
            {
                this.sceneReferences.Add(pair.Key, new List<ObjectIdentifier>(pair.Value.referencedObjects));
                this.scenePaths.Add(pair.Key, pair.Value.scene);
            }

            // keep the order of the build
            var fileMap = new Dictionary<string, SimFile>();
            foreach (var pair in writeData.FileToBundle)
            {
                if (!writeData.FileToObjects.TryGetValue(pair.Key, out var objects))
                    continue;
                var file = new SimFile() { bundle = pair.Value, objects = objects };
                fileMap.Add(pair.Key, file);
                this.files.Add(file);
            }
            foreach (var pair in writeData.AssetToFiles)
            {
                this.explicitAll.Add(pair.Key);
                if (pair.Value.Count == 0 || !fileMap.TryGetValue(pair.Value[0], out var file))
                    continue;
                file.owners.Add(pair.Key);
                if (this.sceneReferences.ContainsKey(pair.Key))
                {
                    file.isScene = true;
                    // the previous scenes of the same bundle follow the own file
                    this.sceneOrder[pair.Key] = pair.Value.Count - 1;
                }
            }
        }

        /// <summary>
        /// whether the asset has been registered in any groups
        /// </summary>
        public bool IsExplicitAsset(GUID guid) => this.explicitAll.Contains(guid);

        /// <summary>
        /// bundle name and objects of each file in the order of the build
        /// </summary>
        public IEnumerable<(string bundle, List<ObjectIdentifier> objects)> EnumerateFiles()
        {
            foreach (var file in this.files)
                yield return (file.bundle, file.objects);

            // new bundles are ordered by the groups (the groups are sorted after grouping)
            var groupOrder = new Dictionary<string, int>();
            for (var i = 0; i < this.settings.groups.Count; ++i)
            {
                if (this.settings.groups[i] != null)
                    groupOrder[this.settings.groups[i].Guid] = i;
            }
            var sorted = new List<SimFile>(this.virtualFiles);
            int GetOrder(SimFile file) => groupOrder.TryGetValue(this.BundleToAssetGroup[file.bundle], out var order) ? order : int.MaxValue;
            sorted.Sort((a, b) =>
            {
                var ret = GetOrder(a).CompareTo(GetOrder(b));
                return ret != 0 ? ret : this.virtualFiles.IndexOf(a).CompareTo(this.virtualFiles.IndexOf(b));
            });
            foreach (var file in sorted)
                yield return (file.bundle, file.objects);
        }

        /// <summary>
        /// register the assets as explicit assets of the groups and update the bundle layout
        /// </summary>
        public void AddExplicitAssets(IReadOnlyList<(GUID guid, AddressableAssetGroup group)> assets)
        {
            if (assets.Count == 0)
                return;

            var newAssets = new HashSet<GUID>();
            foreach (var (guid, group) in assets)
            {
                newAssets.Add(guid);
                this.explicitAssets.Add(guid);
                this.explicitAll.Add(guid);

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                var bundle = schema != null && schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                    ? $"~{group.Name}/{guid}"
                    : $"~{group.Name}";
                if (!this.virtualFileMap.TryGetValue(bundle, out var file))
                {
                    file = new SimFile() { bundle = bundle };
                    this.virtualFileMap.Add(bundle, file);
                    this.virtualFiles.Add(file);
                    this.BundleToAssetGroup[bundle] = group.Guid;
                }
                file.owners.Add(guid);
            }

            // the dependencies of the other assets stop at the new explicit assets
            foreach (var pair in this.assetInfos)
            {
                if (ContainsAny(pair.Value.referencedObjects, newAssets))
                    SetReferences(pair.Value, CollectAssetDependencies(pair.Key, pair.Value.includedObjects));
            }
            if (this.nonRecursive)
            {
                var keys = new List<GUID>(this.sceneReferences.Keys);
                foreach (var scene in keys)
                {
                    if (ContainsAny(this.sceneReferences[scene], newAssets))
                        this.sceneReferences[scene] = CollectSceneDependencies(scene);
                }
            }

            // dependencies of the new explicit assets
            foreach (var guid in newAssets)
            {
                var included = new List<ObjectIdentifier>(ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(guid, this.target));
                var info = new AssetInfo() { includedObjects = included };
                SetReferences(info, CollectAssetDependencies(guid, included));
                this.assetInfos.Add(guid, info);
            }

            // GenerateBundlePacking
            var previousSceneObjects = new Dictionary<string, HashSet<ObjectIdentifier>>();
            var previousSceneAssets = new Dictionary<string, HashSet<GUID>>();
            var sceneFiles = new List<SimFile>();
            foreach (var file in this.files)
            {
                if (file.isScene)
                    sceneFiles.Add(file);
                else
                    PackAssetFile(file);
            }
            foreach (var file in this.virtualFiles)
                PackAssetFile(file);
            sceneFiles.Sort((a, b) => this.sceneOrder[a.owners[0]].CompareTo(this.sceneOrder[b.owners[0]]));
            foreach (var file in sceneFiles)
            {
                var scene = file.owners[0];
                if (!previousSceneObjects.TryGetValue(file.bundle, out var prevObjects))
                {
                    prevObjects = new HashSet<ObjectIdentifier>();
                    previousSceneObjects.Add(file.bundle, prevObjects);
                    previousSceneAssets.Add(file.bundle, new HashSet<GUID>());
                }
                var prevAssets = previousSceneAssets[file.bundle];
                var references = new List<ObjectIdentifier>(this.sceneReferences[scene]);
                var referencedAssets = FilterReferences(scene, references, prevObjects, prevAssets);
                prevObjects.UnionWith(references);
                prevAssets.UnionWith(referencedAssets);
                file.objects = references;
            }
        }

        void PackAssetFile(SimFile file)
        {
            var allObjects = new HashSet<ObjectIdentifier>();
            foreach (var asset in file.owners)
            {
                var info = this.assetInfos[asset];
                allObjects.UnionWith(info.includedObjects);
                var references = new List<ObjectIdentifier>(info.referencedObjects);
                FilterReferences(asset, references, null, null);
                allObjects.UnionWith(references);
            }
            file.objects = new List<ObjectIdentifier>(allObjects);
        }

        static void SetReferences(AssetInfo info, List<ObjectIdentifier> references)
        {
            info.referencedObjects = references;
            info.referencedObjectSet = new HashSet<ObjectIdentifier>(references);
        }

        static bool ContainsAny(List<ObjectIdentifier> objects, HashSet<GUID> guids)
        {
            foreach (var obj in objects)
            {
                if (guids.Contains(obj.guid))
                    return true;
            }
            return false;
        }

        ObjectIdentifier[] GetDependencies(ObjectIdentifier obj)
        {
            if (!this.dependencyCache.TryGetValue(obj, out var dependencies))
            {
                this.singleObject[0] = obj;
                dependencies = ContentBuildInterface.GetPlayerDependenciesForObjects(this.singleObject, this.target, null, DependencyType.ValidReferences);
                this.dependencyCache.Add(obj, dependencies);
            }
            return dependencies;
        }

        /// <summary>
        /// same as CalculateAssetDependencyData.GetPlayerDependenciesForAsset (except the representations of explicit assets)
        /// </summary>
        List<ObjectIdentifier> CollectAssetDependencies(GUID asset, List<ObjectIdentifier> includedObjects)
        {
            var stack = new Stack<ObjectIdentifier>();
            foreach (var subObject in includedObjects)
            {
                foreach (var dependency in GetDependencies(subObject))
                {
                    if (dependency.guid != asset)
                        stack.Push(dependency);
                }
            }
            return CollectDependencies(asset, stack);
        }

        /// <summary>
        /// same as ExtensionMethods.FilterReferencedObjectIDs (except the representations of explicit assets)
        /// </summary>
        List<ObjectIdentifier> CollectSceneDependencies(GUID scene)
        {
            var sceneInfo = ContentBuildInterface.CalculatePlayerDependenciesForScene(this.scenePaths[scene], this.buildSettings,
                new BuildUsageTagSet(), this.usageCache, DependencyType.ValidReferences);
            return CollectDependencies(scene, new Stack<ObjectIdentifier>(sceneInfo.referencedObjects));
        }

        List<ObjectIdentifier> CollectDependencies(GUID asset, Stack<ObjectIdentifier> stack)
        {
            var collected = new HashSet<ObjectIdentifier>();
            var encounteredExplicit = new HashSet<ObjectIdentifier>();
            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                if (obj.guid != asset && this.explicitAssets.Contains(obj.guid))
                {
                    encounteredExplicit.Add(obj);
                }
                else if (!this.explicitAssets.Contains(obj.guid) && collected.Add(obj))
                {
                    foreach (var dependency in GetDependencies(obj))
                        stack.Push(dependency);
                }
            }
            collected.UnionWith(encounteredExplicit);
            return new List<ObjectIdentifier>(collected);
        }

        /// <summary>
        /// same as GenerateBundlePacking.FilterReferencesForAsset
        /// </summary>
        /// <returns>referenced explicit assets</returns>
        List<GUID> FilterReferences(GUID asset, List<ObjectIdentifier> references,
            HashSet<ObjectIdentifier> previousSceneObjects, HashSet<GUID> previousSceneAssets)
        {
            var referencedAssets = new List<GUID>();
            var referencedAssetSet = new HashSet<GUID>();
            // remove Default Resources and Includes for Assets assigned to Bundles
            references.RemoveAll(reference =>
            {
                if (string.Equals(reference.filePath, UNITY_DEFAULT_RESOURCE_PATH, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!this.assetInfos.ContainsKey(reference.guid))
                    return false;
                if (referencedAssetSet.Add(reference.guid))
                    referencedAssets.Add(reference.guid);
                return true;
            });

            // remove references also included by referenced assets (circular references are removed only if the GUID is higher)
            if (!this.nonRecursive)
            {
                foreach (var referencedAsset in referencedAssets)
                {
                    var info = this.assetInfos[referencedAsset];
                    if (asset > referencedAsset || asset == referencedAsset || !ContainsGuid(info.referencedObjects, asset))
                        references.RemoveAll(info.referencedObjectSet.Contains);
                }
            }

            // scenes can reference the same assets previously referenced
            if (previousSceneAssets != null)
            {
                foreach (var reference in previousSceneAssets)
                {
                    if (this.assetInfos.TryGetValue(reference, out var info) && references.RemoveAll(info.referencedObjectSet.Contains) > 0)
                        referencedAssets.Add(reference);
                }
            }
            // scenes can use data from previous sharedAssets in the same bundle
            if (previousSceneObjects != null)
                references.RemoveAll(previousSceneObjects.Contains);
            return referencedAssets;
        }

        static bool ContainsGuid(List<ObjectIdentifier> objects, GUID guid)
        {
            foreach (var obj in objects)
            {
                if (obj.guid == guid)
                    return true;
            }
            return false;
        }
    }
}
