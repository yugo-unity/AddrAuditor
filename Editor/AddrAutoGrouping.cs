using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.U2D;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// Addressablesの最適な重複解決の自動グルーピング
    /// 暗黙の依存Asset（ImplicitAssets）を検出して同じ依存関係を持つAssetとグループ（＝Assetbundle）をまとめる
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

        class SharedGroupParam
        {
            public SharedGroupParam(string name, List<string> bundles)
            {
                this.name = name;
                this.bundles = bundles;
            }

            public readonly string name;
            public readonly List<string> bundles; // referenced bundles
            public readonly List<ImplicitParam> implicitParams = new(); // info of contained assets
        }

        class ImplicitParam
        {
            public string guid;
            public string path;
            public bool isSubAsset;
            public bool isResident;
            public List<System.Type> usedSubAssetTypes;

            public List<string> bundles; // referenced Bundles
            //public long fileSize;
        }

        class SpriteAtlasParam
        {
            public bool isResident;
            public SpriteAtlas instance;
        }

        /// <summary>
        /// whether a group what is created automatically
        /// </summary>
        /// <param name="group">Addressables Group</param>
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
        /// <param name="groupingSettings">settings for grouping automatically</param>
        /// <returns>whether it needs to recurse process</returns>
        public static bool Execute(AddrAutoGroupingSettings groupingSettings)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // // 単品のbundleにするAssetのファイルサイズの閾値
            // var SEPARATE_ASSET_SIZE = (long)groupingSettings.singleThreshold * 1024L;

            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                return false;
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
                return false;
            }

            // get implicit assets
            var (implicitParams, atlases) = CollectImplicitParams(aaContext.bundleToAssetGroup, extractData.WriteData, groupingSettings);

            var sharedGroupParams = new List<SharedGroupParam>();
            var shaderGroupParam = new SharedGroupParam(SHADER_GROUP_NAME, null);
            var singleGroupParam = new SharedGroupParam(SINGLE_GROUP_NAME, null);
            var residentGroupParam = new SharedGroupParam(RESIDENT_GROUP_NAME, null);

            foreach (var implicitParam in implicitParams)
            {
                // put them in the same group if resident assets
                var residentAsset = implicitParam.isResident && implicitParam.bundles.Count > 1;

                // SpriteAtlas confirmation is required, because Sprite assets needs exception handling 
                if (implicitParam.usedSubAssetTypes.Contains(typeof(Sprite)))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(implicitParam.path);
                    var packed = false;
                    foreach (var atlas in atlases)
                    {
                        // AFAK no way to find SpriteAtlas contains a Sprite before instancing
                        if (atlas.instance.CanBindTo(sprite))
                        {
                            // Sprite is not implicit assets if SpriteAtlas contains it
                            packed = implicitParam.usedSubAssetTypes.Count == 1;
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
                    residentGroupParam.implicitParams.Add(implicitParam);
                    continue;
                }

                // support Shader Group
                // NOTE: shaders as resident assets, Resident group contains them
                if (groupingSettings.shaderGroup)
                {
                    var assetType = implicitParam.usedSubAssetTypes[0];
                    if (assetType == typeof(Shader))
                    {
                        shaderGroupParam.implicitParams.Add(implicitParam);
                        continue;
                    }
                }

                // // 指定サイズより大きい場合は単品のbundleにする
                // if (SEPARATE_ASSET_SIZE > 0 && implicitParam.fileSize > SEPARATE_ASSET_SIZE)
                // {
                //     singleGroupParam.implicitParams.Add(implicitParam);
                //     continue;
                // }

                // ignore implicit assets that is not duplicated
                if (implicitParam.bundles.Count == 1)
                    continue;

                var hit = sharedGroupParams.Count > 0;
                // find existed shared groups
                foreach (var groupParam in sharedGroupParams)
                {
                    // no match referenced count
                    if (groupParam.bundles.Count != implicitParam.bundles.Count)
                    {
                        hit = false;
                        continue;
                    }

                    // confirm all bundles are the same 
                    hit = true;
                    foreach (var bundle in implicitParam.bundles)
                    {
                        if (!groupParam.bundles.Contains(bundle))
                        {
                            hit = false;
                            break;
                        }
                    }
                    if (hit)
                    {
                        groupParam.implicitParams.Add(implicitParam);
                        break;
                    }
                }

                // no match, create new group
                if (!hit)
                {
                    var param = new SharedGroupParam(SHARED_GROUP_NAME + "{0}", implicitParam.bundles);
                    param.implicitParams.Add(implicitParam);
                    sharedGroupParams.Add(param);
                }
            }

            var continued = sharedGroupParams.Count > 0;

            // Resident Group
            if (residentGroupParam.implicitParams.Count > 0)
                sharedGroupParams.Add(residentGroupParam);

            // Shader Group
            if (groupingSettings.shaderGroup)
                sharedGroupParams.Add(shaderGroupParam);

            // assign to the group that has Packed Separately setting
            var singleGroup = CreateSharedGroup(settings, SINGLE_GROUP_NAME, groupingSettings.hashName);
            var schema = singleGroup.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            CreateOrMoveEntry(settings, singleGroup, singleGroupParam);

            // assign to groups
            var sharedGroupCount = settings.groups.FindAll(group => group.name.Contains(SHARED_GROUP_NAME)).Count;
            foreach (var groupParam in sharedGroupParams)
            {
                // 1個しかAssetがないGroupは単一グループにまとめる
                var group = singleGroup;

                if (groupParam.implicitParams.Count > 1)
                {
                    var name = string.Format(groupParam.name, sharedGroupCount);
                    group = CreateSharedGroup(settings, name, groupingSettings.hashName);
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
        /// SharedAsset用のGroupの作成
        /// </summary>
        static AddressableAssetGroup CreateSharedGroup(AddressableAssetSettings settings, string groupName,
            bool useHashName)
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

            // NOTE: must be disabled to apply
            schema.UseDefaultSchemaSettings = false;

            // NOTE: no need to include in catalog, it is good for reducing catalog size
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
        /// Create Entry to specified group
        /// </summary>
        static void CreateOrMoveEntry(AddressableAssetSettings settings, AddressableAssetGroup group, SharedGroupParam groupParam)
        {
            foreach (var implicitParam in groupParam.implicitParams)
            {
                var entry = settings.CreateOrMoveEntry(implicitParam.guid, group, false, false);
                var addr = System.IO.Path.GetFileNameWithoutExtension(implicitParam.path);
                entry.SetAddress(addr, false);
            }
        }

        /// <summary>
        /// find duplicated implicit assets and get the info
        /// </summary>
        static (List<ImplicitParam>, List<SpriteAtlasParam>) CollectImplicitParams(Dictionary<string, string> bundleToAssetGroup,
            IBundleWriteData writeData, AddrAutoGroupingSettings groupingSettings)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var validImplicitGuids = new Dictionary<GUID, ImplicitParam>();
            var atlases = new List<SpriteAtlasParam>();

            foreach (var fileToBundle in writeData.FileToBundle)
            {
                if (writeData.FileToObjects.TryGetValue(fileToBundle.Key, out var objectIdList))
                {
                    // NOTE: there are multiple references from the same file 
                    foreach (var objectId in objectIdList)
                    {
                        var guid = objectId.guid;

                        // ignore explicit assets that have been registered in groups
                        if (writeData.AssetToFiles.ContainsKey(guid))
                            continue;

                        // find the group
                        var bundle = fileToBundle.Value;
                        // NOTE: cannot found the Built-in Shaders group
                        if (!bundleToAssetGroup.TryGetValue(bundle, out var groupGuid))
                            continue;
                        var path = AssetDatabase.GUIDToAssetPath(guid);

                        // allow to entry Resources assets, but outputting warning log
                        // NOTE: TextMeshPro has a legacy structure that depends on Resources
                        if (!AddrUtility.IsPathValidForEntry(path))
                            continue;
                        if (path.Contains("/Resources/"))
                        {
                            var selectedGroup = settings.FindGroup(g => g.Guid == groupGuid);
                            Debug.LogWarning($"Resources Asset is duplicated. - {path} / Group : {selectedGroup.name}");
                        }

                        // ignore Lightmap assets because it depends on Scene asset
                        if (path.Contains("Lightmap-"))
                            continue;

                        // NOTE: there are assets that have no type (PostProcessingVolume, Playable, ...)
                        var instance = ObjectIdentifier.ToObject(objectId);
                        var type = instance != null ? instance.GetType() : null;

                        // NOTE: Material assets are very tiny, to reduce excessive bundle dependencies by allowing duplication
                        if (groupingSettings.allowDuplicatedMaterial)
                        {
                            if (type == typeof(Material))
                                continue;
                        }

                        var isSubAsset = instance != null && AssetDatabase.IsSubAsset(instance);
                        var isResident = groupGuid == groupingSettings.residentGroupGUID;

                        if (validImplicitGuids.TryGetValue(guid, out var param))
                        {
                            if (type != null && !param.usedSubAssetTypes.Contains(type))
                                param.usedSubAssetTypes.Add(type);
                            if (!param.bundles.Contains(bundle))
                                param.bundles.Add(bundle);
                            param.isSubAsset &= isSubAsset;
                            param.isResident |= isResident;
                        }
                        else
                        {
                            // // Textureは圧縮フォーマットでサイズが著しく変わるので対応する
                            // // NOTE: AssetBundleのLZ4圧縮後の結果は流石に内容物によって変わるのでビルド前チェックは無理
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

                            param = new ImplicitParam()
                            {
                                guid = guid.ToString(),
                                path = path,
                                isSubAsset = isSubAsset,
                                isResident = isResident,
                                usedSubAssetTypes = new List<System.Type>() { type },
                                bundles = new List<string>() { bundle },
                                //fileSize = fileSize,
                            };
                            validImplicitGuids.Add(guid, param);

                            // SpriteAtlasは単品チェックでバラのテクスチャが引っかからないように集めておく
                            if (type == typeof(SpriteAtlas))
                            {
                                var atlasParam = new SpriteAtlasParam()
                                {
                                    isResident = isResident,
                                    instance = instance as SpriteAtlas,
                                };
                                atlases.Add(atlasParam);
                            }
                        }

                        // for checking
                        //Debug.Log($"{implicitPath} / Entry : {explicitPath} / Group : {selectedGroup.name}");
                    }
                }
            }

            var implicitParams = new List<ImplicitParam>(validImplicitGuids.Values);
            return (implicitParams, atlases);
        }
    }
}
