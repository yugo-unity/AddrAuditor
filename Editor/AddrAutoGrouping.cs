using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.U2D;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// referenced asset in Addressable
    /// </summary>
    public class RefAssetData
    {
        public string guid;
        public string path;
        public bool isSubAsset;
        public bool isResident;
        public bool isResources;
        public List<System.Type> usedSubAssetTypes;

        public List<string> bundles; // referenced Bundle
        //public long fileSize;
    }

    /// <summary>
    /// referenced spriteAtlas in Addressable
    /// </summary>
    public class SpriteAtlasData
    {
        public bool isResident;
        public SpriteAtlas instance;
    }
    
    /// <summary>
    /// automatic grouping to resolve optimally for duplicated assets of Addressable
    /// collect implicit dependencies (Implicit Assets) and register to shared group with other assets that have the same dependencies
    /// </summary>
    public static class AddrAutoGrouping
    {
        public static readonly string SETTINGS_PATH = $"Assets/{nameof(AddrAutoGroupingSettings)}.asset";
        public const string SHARED_GROUP_NAME = "+Shared_";
        public const string SHADER_GROUP_NAME = "+Shared_Shader";
        public const string SINGLE_GROUP_NAME = "+Shared_Single";
        public const string RESIDENT_GROUP_NAME = "+Residents";

        // delegate long GetMemorySizeLongCallback(Texture tex);
        // GetMemorySizeLongCallback GetStorageMemorySizeLong = null;

        class SharedGroupData
        {
            public SharedGroupData(string name, List<string> bundles)
            {
                this.name = name;
                this.bundles = bundles;
            }

            public readonly string name;
            public readonly List<string> bundles; // referenced bundles
            public readonly List<RefAssetData> refAssets = new(); // info of contained assets
        }

        /// <summary>
        /// whether a group what is created automatically
        /// </summary>
        /// <param name="group">addressables group</param>
        public static bool IsAutoGroup(AddressableAssetGroup group)
        {
            return group.Name.Contains(SHARED_GROUP_NAME) ||
                   group.Name.Contains(SHADER_GROUP_NAME) ||
                   group.Name.Contains(SINGLE_GROUP_NAME) ||
                   group.Name.Contains(RESIDENT_GROUP_NAME);
        }

        /// <summary>
        /// resolve duplicated assets
        /// </summary>
        /// <param name="agSettings">settings for grouping automatically</param>
        /// <returns>whether it needs to recurse process</returns>
        public static bool Execute(AddrAutoGroupingSettings agSettings)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // get implicit assets
            var (refAssets, atlases) = CollectReferencedAssetInfo(settings, agSettings, true);
            if (refAssets == null)
                return false;

            // // Asset file size threshold for bundling separately
            // var SEPARATE_ASSET_SIZE = (long)agSettings.singleThreshold * 1024L;

            var sharedGroupDataList = new List<SharedGroupData>();
            var shaderGroupData = new SharedGroupData(SHADER_GROUP_NAME, null);
            var singleGroupData = new SharedGroupData(SINGLE_GROUP_NAME, null);
            var residentGroupData = new SharedGroupData(RESIDENT_GROUP_NAME, null);

            foreach (var refAsset in refAssets)
            {
                // put them in the same group if resident assets
                var residentAsset = refAsset.isResident && refAsset.bundles.Count > 1;
                // Texture as Sprite of SpriteAtlas needs exception handling
                // Sprite is a sub-asset of Texture, but Sprite has the Texture as dependent
                if (refAsset.usedSubAssetTypes.Contains(typeof(Sprite)))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(refAsset.path);
                    var packed = false;
                    foreach (var atlas in atlases)
                    {
                        // AFAIK no way to find SpriteAtlas contains a Sprite before instancing
                        if (atlas.instance.CanBindTo(sprite))
                        {
                            // Sprite is not considered an implicit asset if only SpriteAtlas refers it
                            packed = refAsset.usedSubAssetTypes.Count == 1;
                            // WARNING: If the original texture of a Sprite that is contained in SpriteAtlas is referenced,
                            //          the original texture is treated as a resident asset if the SpriteAtlas is resident.
                            residentAsset |= atlas.isResident;
                            break;
                        }
                    }

                    if (packed)
                        continue;
                }

                if (residentAsset)
                {
                    residentGroupData.refAssets.Add(refAsset);
                    continue;
                }

                // support Shader Group
                if (agSettings.shaderGroup)
                {
                    var assetType = refAsset.usedSubAssetTypes[0];
                    if (assetType == typeof(Shader))
                    {
                        shaderGroupData.refAssets.Add(refAsset);
                        continue;
                    }
                }

                // NOTE: if you have large assets that are scheduled to be updated frequently, you should consider follows
                // // if larger than the specified size, it will be a single bundle
                // if (SEPARATE_ASSET_SIZE > 0 && implicitParam.fileSize > SEPARATE_ASSET_SIZE)
                // {
                //     singleGroupParam.implicitParams.Add(implicitParam);
                //     continue;
                // }

                // ignore implicit assets that is not duplicated
                if (refAsset.bundles.Count == 1)
                    continue;

                var hit = sharedGroupDataList.Count > 0;
                // find existed shared groups
                foreach (var groupData in sharedGroupDataList)
                {
                    // no match referenced count
                    if (groupData.bundles.Count != refAsset.bundles.Count)
                    {
                        hit = false;
                        continue;
                    }

                    // confirm all bundles are the same 
                    hit = true;
                    foreach (var bundle in refAsset.bundles)
                    {
                        if (!groupData.bundles.Contains(bundle))
                        {
                            hit = false;
                            break;
                        }
                    }
                    if (hit)
                    {
                        groupData.refAssets.Add(refAsset);
                        break;
                    }
                }

                // no match, create new group
                if (!hit)
                {
                    var param = new SharedGroupData(SHARED_GROUP_NAME + "{0}", refAsset.bundles);
                    param.refAssets.Add(refAsset);
                    sharedGroupDataList.Add(param);
                }
            }

            var continued = sharedGroupDataList.Count > 0;

            // Resident Group
            if (residentGroupData.refAssets.Count > 0)
                sharedGroupDataList.Add(residentGroupData);
            // Shader Group
            if (agSettings.shaderGroup)
                sharedGroupDataList.Add(shaderGroupData);

            // assign to the group that has Packed Separately setting
            var singleGroup = CreateSharedGroup(settings, SINGLE_GROUP_NAME, agSettings.hashName);
            var schema = singleGroup.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            CreateOrMoveEntry(settings, singleGroup, singleGroupData);

            // assign to groups
            var sharedGroupCount = settings.groups.FindAll(group => group.name.Contains(SHARED_GROUP_NAME)).Count;
            foreach (var groupParam in sharedGroupDataList)
            {
                // if need to create a bundle that only has one asset, combining into a Packed-Separately group
                var group = singleGroup;

                if (groupParam.refAssets.Count > 1)
                {
                    var name = string.Format(groupParam.name, sharedGroupCount);
                    group = CreateSharedGroup(settings, name, agSettings.hashName);
                    sharedGroupCount++;
                }

                CreateOrMoveEntry(settings, group, groupParam);
            }

            // delete empty group
            if (singleGroup.entries.Count == 0)
                settings.RemoveGroup(singleGroup);

            // sort alphanumerically
            settings.groups.Sort(AddrUtility.CompareGroup);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                postEvent: true, settingsModified: true);

            return continued;
        }

        /// <summary>
        /// create addressables group for shared assets
        /// </summary>
        static AddressableAssetGroup CreateSharedGroup(AddressableAssetSettings settings, string groupName, bool useHashName)
        {
            // Shared-Single and Shared-Shader group are the unique group
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                var groupTemplate = settings.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;
                if (groupTemplate == null)
                {
                    Debug.LogError("Not found AddressableAssetGroupTemplate");
                    return null;
                }

                group = settings.CreateGroup(groupName, false, true, false,
                    groupTemplate.SchemaObjects);
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                schema = group.AddSchema<BundledAssetGroupSchema>();

            // must be disabled to apply
            schema.UseDefaultSchemaSettings = false;

            // no need to include in catalog, it is good for reducing catalog size
            schema.IncludeAddressInCatalog = false;
            schema.IncludeGUIDInCatalog = false;
            schema.IncludeLabelsInCatalog = false;

            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.AssetLoadMode = UnityEngine.ResourceManagement.ResourceProviders.AssetLoadMode.AllPackedAssetsAndDependencies;
            schema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid;
            schema.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.Dynamic;
            schema.UseAssetBundleCrc = schema.UseAssetBundleCache = false;
            if (useHashName)
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.FileNameHash;
            else
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;

            return group;
        }

        /// <summary>
        /// create Entry to specified group
        /// </summary>
        static void CreateOrMoveEntry(AddressableAssetSettings settings, AddressableAssetGroup group, SharedGroupData groupData)
        {
            foreach (var info in groupData.refAssets)
            {
                var entry = settings.CreateOrMoveEntry(info.guid, group, false, false);
                var addr = System.IO.Path.GetFileNameWithoutExtension(info.path);
                entry.SetAddress(addr, false);
            }
        }

        /// <summary>
        /// find referenced assets and get the info
        /// </summary>
        /// <param name="settings">Addressable Settings</param>
        /// <param name="agSettings">Auto Grouping Settings by AddrAuditor</param>
        /// <param name="excludeExplicitAsset">whether to include the explicit asset(Addressable Entry) in the return value</param>
        /// <returns>the data about included assets and that list of SpriteAtlas</returns>
        public static (List<RefAssetData>, List<SpriteAtlasData>) CollectReferencedAssetInfo(AddressableAssetSettings settings, 
                                                                                      AddrAutoGroupingSettings agSettings,
                                                                                      bool excludeExplicitAsset)
        {
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                return (null, null);
            }

            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, string>();
            AddrUtility.CalculateInputDefinitions(settings, allBundleInputDefs, bundleToAssetGroup);
            var aaContext = AddrUtility.GetBuildContext(settings, bundleToAssetGroup);
            var extractData = new ExtractDataTask();
            var exitCode = AddrUtility.RefleshBuild(settings, allBundleInputDefs, extractData, aaContext);
            if (exitCode < ReturnCode.Success)
            {
                Debug.LogError($"Analyze build failed. {exitCode}");
                return (null, null);
            }
            
            var writeData = extractData.WriteData;
            var validImplicitGuids = new Dictionary<GUID, RefAssetData>();
            var atlases = new List<SpriteAtlasData>();
            var hasGroupingSettings = agSettings != null; 

            foreach (var fileToBundle in writeData.FileToBundle)
            {
                if (writeData.FileToObjects.TryGetValue(fileToBundle.Key, out var objectIdList))
                {
                    // there are multiple references from the same file 
                    foreach (var objectId in objectIdList)
                    {
                        var guid = objectId.guid;

                        // ignore explicit assets that have been registered in any groups
                        if (excludeExplicitAsset && writeData.AssetToFiles.ContainsKey(guid))
                            continue;

                        // find the group
                        var bundle = fileToBundle.Value;
                        // cannot found the Built-in Shaders group
                        if (!bundleToAssetGroup.TryGetValue(bundle, out var groupGuid))
                            continue;
                        var path = AssetDatabase.GUIDToAssetPath(guid);

                        // allow to entry Resources assets
                        // TextMeshPro has a legacy structure that depends on Resources
                        if (!AddrUtility.IsPathValidForEntry(path))
                            continue;
                        var isResources = path.Contains("/Resources/"); 
                        if (isResources)
                        {
                            if (hasGroupingSettings)
                            {
                                var selectedGroup = settings.FindGroup(g => g.Guid == groupGuid);
                                Debug.LogWarning($"Resources Asset is duplicated. - {path} / Group : {selectedGroup.name}");
                            }
                        }

                        // // ignore Lightmap assets because it depends on Scene asset
                        // if (path.Contains("Lightmap-"))
                        //     continue;

                        // there are assets that have no type (PostProcessingVolume, Playable, ...)
                        var instance = ObjectIdentifier.ToObject(objectId);
                        var type = instance != null ? instance.GetType() : null;

                        // Material assets are very tiny, to reduce excessive bundle dependencies by allowing duplication
                        if (hasGroupingSettings && agSettings.allowDuplicatedMaterial)
                        {
                            if (type == typeof(Material))
                                continue;
                        }

                        var isSubAsset = instance != null && AssetDatabase.IsSubAsset(instance);
                        var isResident = hasGroupingSettings && (groupGuid == agSettings.residentGroupGUID);

                        if (validImplicitGuids.TryGetValue(guid, out var refAsset))
                        {
                            if (type != null && !refAsset.usedSubAssetTypes.Contains(type))
                                refAsset.usedSubAssetTypes.Add(type);
                            if (!refAsset.bundles.Contains(bundle))
                                refAsset.bundles.Add(bundle);
                            refAsset.isSubAsset &= isSubAsset;
                            refAsset.isResident |= isResident;
                        }
                        else
                        {
                            // // get the texture size before compression
                            // var fullPath = "";
                            // if (path.Contains("Packages/"))
                            //     fullPath = System.IO.Path.GetFullPath(path);
                            // else
                            //     fullPath = System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""),
                            //         path);
                            // var fileSize = 0L;
                            // if (instance is Texture)
                            //     fileSize = this.GetStorageMemorySizeLong(instance as Texture);
                            // if (fileSize == 0L)
                            //     fileSize = new System.IO.FileInfo(fullPath).Length;

                            refAsset = new RefAssetData()
                            {
                                guid = guid.ToString(),
                                path = path,
                                isSubAsset = isSubAsset,
                                isResident = isResident,
                                isResources = isResources,
                                usedSubAssetTypes = new List<System.Type>() { type },
                                bundles = new List<string>() { bundle },
                                //fileSize = fileSize,
                            };
                            validImplicitGuids.Add(guid, refAsset);

                            // this is necessary to determine whether Sprite belongs to SpriteAtlas which is included in Addressable
                            if (type == typeof(SpriteAtlas))
                            {
                                var atlasParam = new SpriteAtlasData()
                                {
                                    isResident = isResident,
                                    instance = instance as SpriteAtlas,
                                };
                                atlases.Add(atlasParam);
                            }
                        }

                        // for test
                        //Debug.Log($"{implicitPath} / Entry : {explicitPath} / Group : {selectedGroup.name}");
                    }
                }
            }

            var implicitParams = new List<RefAssetData>(validImplicitGuids.Values);
            return (implicitParams, atlases);
        }
    }
}
