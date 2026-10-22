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
            // NOTE: SHADER_GROUP_NAME and SINGLE_GROUP_NAME contain SHARED_GROUP_NAME
            return group.Name.Contains(SHARED_GROUP_NAME) ||
                   group.Name.Contains(RESIDENT_GROUP_NAME);
        }

        /// <summary>
        /// find the SpriteAtlas that packs the Sprite of the referenced asset
        /// </summary>
        /// <returns>the first SpriteAtlas that can bind the Sprite, or null</returns>
        internal static SpriteAtlasData FindPackingAtlas(RefAssetData refAsset, List<SpriteAtlasData> atlases)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(refAsset.path);
            foreach (var atlas in atlases)
            {
                // AFAIK no way to find SpriteAtlas contains a Sprite before instancing
                if (atlas.instance.CanBindTo(sprite))
                    return atlas;
            }
            return null;
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

            var sharedGroupDataList = new List<SharedGroupData>();
            // key: sorted names of referenced bundles (to find the group that has the same bundles)
            var sharedGroupDataMap = new Dictionary<string, SharedGroupData>();
            var shaderGroupData = new SharedGroupData(SHADER_GROUP_NAME, null);
            var residentGroupData = new SharedGroupData(RESIDENT_GROUP_NAME, null);
            var sortedBundles = new List<string>();

            foreach (var refAsset in refAssets)
            {
                // put them in the same group if resident assets
                var residentAsset = refAsset.isResident && refAsset.bundles.Count > 1;
                // Texture as Sprite of SpriteAtlas needs exception handling
                // Sprite is a sub-asset of Texture, but Sprite has the Texture as dependent
                if (refAsset.usedSubAssetTypes.Contains(typeof(Sprite)))
                {
                    var atlas = FindPackingAtlas(refAsset, atlases);
                    if (atlas != null)
                    {
                        // WARNING: If the original texture of a Sprite that is contained in SpriteAtlas is referenced,
                        //          the original texture is treated as a resident asset if the SpriteAtlas is resident.
                        residentAsset |= atlas.isResident;
                        // Sprite is not considered an implicit asset if only SpriteAtlas refers it
                        if (refAsset.usedSubAssetTypes.Count == 1)
                            continue;
                    }
                }

                if (residentAsset)
                {
                    residentGroupData.refAssets.Add(refAsset);
                    continue;
                }

                // support Shader Group
                if (agSettings.shaderGroup && refAsset.usedSubAssetTypes[0] == typeof(Shader))
                {
                    shaderGroupData.refAssets.Add(refAsset);
                    continue;
                }

                // ignore implicit assets that is not duplicated
                if (refAsset.bundles.Count == 1)
                    continue;

                // find existed shared groups that have the same bundles
                sortedBundles.Clear();
                sortedBundles.AddRange(refAsset.bundles);
                sortedBundles.Sort(System.StringComparer.Ordinal);
                var key = string.Join("\n", sortedBundles);
                if (!sharedGroupDataMap.TryGetValue(key, out var groupData))
                {
                    // no match, create new group
                    groupData = new SharedGroupData(SHARED_GROUP_NAME + "{0}", refAsset.bundles);
                    sharedGroupDataList.Add(groupData);
                    sharedGroupDataMap.Add(key, groupData);
                }
                groupData.refAssets.Add(refAsset);
            }

            var continued = sharedGroupDataList.Count > 0;

            // Resident Group
            if (residentGroupData.refAssets.Count > 0)
                sharedGroupDataList.Add(residentGroupData);
            // Shader Group
            if (agSettings.shaderGroup)
                sharedGroupDataList.Add(shaderGroupData);

            // assign to the group that has Packed Separately setting
            var singleGroup = CreateSharedGroup(settings, SINGLE_GROUP_NAME, agSettings.hashName,
                BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
            if (singleGroup == null)
                throw new System.NullReferenceException("Failed to create the group: " + SINGLE_GROUP_NAME);

            // assign to groups
            var sharedGroupCount = settings.groups.FindAll(group => group.name.Contains(SHARED_GROUP_NAME)).Count;
            foreach (var groupParam in sharedGroupDataList)
            {
                // if need to create a bundle that only has one asset, combining into a Packed-Separately group
                var group = singleGroup;

                if (groupParam.refAssets.Count > 1)
                {
                    var name = string.Format(groupParam.name, sharedGroupCount);
                    group = CreateSharedGroup(settings, name, agSettings.hashName,
                        BundledAssetGroupSchema.BundlePackingMode.PackTogether);
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
        static AddressableAssetGroup CreateSharedGroup(AddressableAssetSettings settings, string groupName, bool useHashName,
            BundledAssetGroupSchema.BundlePackingMode packingMode)
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

            // NOTE: set the final mode directly, each setter posts a modification event if the value changes
            schema.BundleMode = packingMode;
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

            var exitCode = AddrUtility.CalculateBundleWriteData(settings, out var aaContext, out var extractData, out _);
            if (exitCode < ReturnCode.Success)
                return (null, null);

            var bundleToAssetGroup = aaContext.bundleToAssetGroup;
            var writeData = extractData.WriteData;
            var validImplicitGuids = new Dictionary<GUID, (RefAssetData refAsset, HashSet<string> bundles)>();
            var atlases = new List<SpriteAtlasData>();
            var hasGroupingSettings = agSettings != null;

            // the same assets appear in every bundle that refers them, so cache the results per asset/object
            var pathCache = new Dictionary<GUID, (string path, bool valid)>();
            var objectCache = new Dictionary<ObjectIdentifier, (Object instance, bool isSubAsset)>();
            var checkedExplicitGuids = new HashSet<GUID>();
            var residentsGroup = settings.FindGroup(RESIDENT_GROUP_NAME);

            foreach (var fileToBundle in writeData.FileToBundle)
            {
                if (!writeData.FileToObjects.TryGetValue(fileToBundle.Key, out var objectIdList))
                    continue;

                // find the group
                var bundle = fileToBundle.Value;
                // cannot found the Built-in Shaders group
                if (!bundleToAssetGroup.TryGetValue(bundle, out var groupGuid))
                    continue;
                var isResident = hasGroupingSettings && (groupGuid == agSettings.residentGroupGUID);

                // there are multiple references from the same file
                foreach (var objectId in objectIdList)
                {
                    var guid = objectId.guid;

                    // ignore explicit assets that have been registered in any groups
                    if (excludeExplicitAsset && writeData.AssetToFiles.ContainsKey(guid))
                    {
                        // SpriteAtlas that has been registered (e.g. moved to the shared group in the previous process)
                        // is still necessary to determine whether Sprite belongs to SpriteAtlas
                        if (checkedExplicitGuids.Add(guid))
                        {
                            var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                            if (AssetDatabase.GetMainAssetTypeAtPath(atlasPath) == typeof(SpriteAtlas))
                            {
                                atlases.Add(new SpriteAtlasData()
                                {
                                    isResident = isResident || (residentsGroup != null && groupGuid == residentsGroup.Guid),
                                    instance = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath),
                                });
                            }
                        }
                        continue;
                    }

                    if (!pathCache.TryGetValue(guid, out var pathInfo))
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        pathInfo = (assetPath, AddrUtility.IsPathValidForEntry(assetPath));
                        pathCache.Add(guid, pathInfo);
                    }

                    // allow to entry Resources assets
                    // TextMeshPro has a legacy structure that depends on Resources
                    if (!pathInfo.valid)
                        continue;
                    var path = pathInfo.path;
                    var isResources = path.Contains("/Resources/");
                    if (isResources && hasGroupingSettings)
                    {
                        var selectedGroup = settings.FindGroup(g => g.Guid == groupGuid);
                        Debug.LogWarning($"Resources Asset is duplicated. - {path} / Group : {selectedGroup.name}");
                    }

                    // there are assets that have no type (PostProcessingVolume, Playable, ...)
                    if (!objectCache.TryGetValue(objectId, out var objectInfo))
                    {
                        var obj = ObjectIdentifier.ToObject(objectId);
                        objectInfo = (obj, obj != null && AssetDatabase.IsSubAsset(obj));
                        objectCache.Add(objectId, objectInfo);
                    }
                    var instance = objectInfo.instance;
                    var type = instance != null ? instance.GetType() : null;

                    // Material assets are very tiny, to reduce excessive bundle dependencies by allowing duplication
                    if (hasGroupingSettings && agSettings.allowDuplicatedMaterial && type == typeof(Material))
                        continue;

                    var isSubAsset = objectInfo.isSubAsset;

                    if (validImplicitGuids.TryGetValue(guid, out var found))
                    {
                        var refAsset = found.refAsset;
                        if (type != null && !refAsset.usedSubAssetTypes.Contains(type))
                            refAsset.usedSubAssetTypes.Add(type);
                        if (found.bundles.Add(bundle))
                            refAsset.bundles.Add(bundle);
                        refAsset.isSubAsset &= isSubAsset;
                        refAsset.isResident |= isResident;
                    }
                    else
                    {
                        var refAsset = new RefAssetData()
                        {
                            guid = guid.ToString(),
                            path = path,
                            isSubAsset = isSubAsset,
                            isResident = isResident,
                            isResources = isResources,
                            usedSubAssetTypes = new List<System.Type>() { type },
                            bundles = new List<string>() { bundle },
                        };
                        validImplicitGuids.Add(guid, (refAsset, new HashSet<string>() { bundle }));

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
                }
            }

            var implicitParams = new List<RefAssetData>(validImplicitGuids.Count);
            foreach (var found in validImplicitGuids.Values)
                implicitParams.Add(found.refAsset);
            return (implicitParams, atlases);
        }
    }
}
