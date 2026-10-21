using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.AddressableAssets.Initialization;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// Utiliities API
    /// </summary>
    public static class AddrUtility
    {
        public const string UNITY_BUILTIN_SHADERS = "unitybuiltinshaders";
        // SBPのCommonSettings.csから
        public static readonly GUID UNITY_BUILTIN_SHADERS_GUID = new ("0000000000000000e000000000000000");
        
        #region UI HELPER
        const float HELPBOX_HEIGHT = 50f;
        const float BUTTON_HEIGHT = 50f;
        
        public delegate bool IsPathCallback(string path);
        public static IsPathCallback IsPathValidForEntry;
        public static void ReloadInternalAPI()
        {
            // it is helpful to make extensions if public API
            if (IsPathValidForEntry == null)
            {
                var aagAssembly = typeof(AddressableAssetGroup).Assembly;
                var aauType = aagAssembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetUtility");
                var validMethod = aauType.GetMethod("IsPathValidForEntry",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null, new System.Type[] { typeof(string) }, null);
                if (validMethod != null)
                {
                    IsPathValidForEntry =
                        System.Delegate.CreateDelegate(typeof(IsPathCallback), validMethod) as IsPathCallback;
                }
                else
                {
                    Debug.LogError("Failed Reflection - IsPathValidForEntry ");
                }
            }
        }

        public static void CreateHelpBox(VisualElement root, string text)
        {
            var helpbox = new HelpBox(text, HelpBoxMessageType.Info);
            helpbox.style.height = new Length(HELPBOX_HEIGHT, LengthUnit.Pixel);
            root.Add(helpbox);
        }

        public static Box CreateSpace(VisualElement root, float ratio = 1f)
        {
            var box = new Box();
            box.style.height = new Length(10f * ratio, LengthUnit.Pixel);
            root.Add(box);
            return box;
        }

        public static Button CreateButton(VisualElement root, string text, string tooltip)
        {
            var button = new Button();
            button.text = text;
            button.tooltip = tooltip;
            button.style.height = new Length(BUTTON_HEIGHT, LengthUnit.Pixel);
            root.Add(button);
            return button;
        }

        public static Toggle CreateToggle(VisualElement root, string title, string tooltip, bool defaultValue, float minWidth = 220f)
        {
            var toggle = new Toggle(title);
            toggle.name = title;
            toggle.tooltip = tooltip;
            toggle.value = defaultValue;
            toggle.labelElement.style.minWidth = minWidth;
            root.Add(toggle);

            return toggle;
        }

        public static IntegerField CreateInteger(VisualElement root, string title, string tooltip, int defaultValue)
        {
            var integer = new IntegerField(title);
            integer.name = title;
            integer.tooltip = tooltip;
            integer.value = defaultValue;
            root.Add(integer);
            return integer;
        }

        public static SliderInt CreateSliderInt(VisualElement root, string title, string tooltip, int defaultValue,
            int min, int max)
        {
            var integer = new SliderInt(title, min, max);
            integer.name = title;
            integer.tooltip = tooltip;
            integer.value = defaultValue;
            root.Add(integer);
            return integer;
        }
        #endregion

        /// <summary>
        /// run the analysis build and get the write data (logs an error if failed)
        /// </summary>
        public static ReturnCode CalculateBundleWriteData(AddressableAssetSettings settings,
            out AddressableAssetsBuildContext aaContext, out ExtractDataTask extractData,
            out List<AssetBundleBuild> allBundleInputDefs)
        {
            allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, string>();
            CalculateInputDefinitions(settings, allBundleInputDefs, bundleToAssetGroup);
            aaContext = GetBuildContext(settings, bundleToAssetGroup);
            extractData = new ExtractDataTask();
            var exitCode = RefleshBuild(settings, allBundleInputDefs, extractData, aaContext);
            if (exitCode < ReturnCode.Success)
                Debug.LogError($"Analyze build failed. {exitCode}");
            return exitCode;
        }

        static AssetBundleBuild CreateUniqueBundle(AssetBundleBuild bid, Dictionary<string, string> bundleToAssetGroup)
        {
            var count = 1;
            var newName = bid.assetBundleName;
            while (bundleToAssetGroup.ContainsKey(newName) && count < 1000)
                newName = bid.assetBundleName.Replace(".bundle", $"{count++}.bundle");
            return new AssetBundleBuild
            {
                assetBundleName = newName,
                addressableNames = bid.addressableNames,
                assetBundleVariant = bid.assetBundleVariant,
                assetNames = bid.assetNames
            };
        }

        public static void CalculateInputDefinitions(AddressableAssetSettings settings,
            List<AssetBundleBuild> allBundleInputDefs, Dictionary<string, string> bundleToAssetGroup)
        {
            var updateFrequency = Mathf.Max(settings.groups.Count / 10, 1);
            var progressDisplayed = false;
            for (var groupIndex = 0; groupIndex < settings.groups.Count; ++groupIndex)
            {
                var group = settings.groups[groupIndex];
                if (group == null)
                    continue;

                if (!progressDisplayed || groupIndex % updateFrequency == 0)
                {
                    progressDisplayed = true;
                    if (EditorUtility.DisplayCancelableProgressBar("Calculating Input Definitions", "", (float)groupIndex / settings.groups.Count))
                    {
                        bundleToAssetGroup.Clear();
                        allBundleInputDefs.Clear();
                        break;
                    }
                }

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild)
                    continue;
                var bundleInputDefinitions = new List<AssetBundleBuild>();
                BuildScriptPackedMode.PrepGroupBundlePacking(group, bundleInputDefinitions, schema);

                for (var i = 0; i < bundleInputDefinitions.Count; i++)
                {
                    if (bundleToAssetGroup.ContainsKey(bundleInputDefinitions[i].assetBundleName))
                        bundleInputDefinitions[i] =
                            CreateUniqueBundle(bundleInputDefinitions[i], bundleToAssetGroup);

                    bundleToAssetGroup.Add(bundleInputDefinitions[i].assetBundleName, schema.Group.Guid);
                }

                allBundleInputDefs.AddRange(bundleInputDefinitions);
            }

            if (progressDisplayed)
                EditorUtility.ClearProgressBar();
        }

        public static AddressableAssetsBuildContext GetBuildContext(AddressableAssetSettings settings,
            Dictionary<string, string> bundleToAssetGroup)
        {
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.LogResourceManagerExceptions = settings.buildSettings.LogResourceManagerExceptions;

            return new AddressableAssetsBuildContext
            {
                Settings = settings,
                runtimeData = runtimeData,
                bundleToAssetGroup = bundleToAssetGroup,
                assetEntries = new (), // NOTE: for GenerateLocationListsTask
                assetGroupToBundles = new ()
            };
        }

        public static ReturnCode RefleshBuild(AddressableAssetSettings settings,
            List<AssetBundleBuild> allBundleInputDefs, ExtractDataTask extractData, 
            AddressableAssetsBuildContext aaContext)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var buildParams = new AddressableAssetsBundleBuildParameters(settings, aaContext.bundleToAssetGroup,
                buildTarget,
                buildTargetGroup, settings.buildSettings.bundleBuildPath);
            // no need prefix because only for analysis
            var builtinBundleName = $"{BuildScriptBase.BuiltInBundleBaseName}.bundle";
            
            var buildTasks = new List<IBuildTask>();
            // Setup
            buildTasks.Add(new RebuildSpriteAtlasCache()); // NOTE: individual Sprites are included if not create SpriteAtlas
            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new AddHashToBundleNameTask());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInBundle(builtinBundleName));
            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            // Writing
            buildTasks.Add(new GenerateLocationListsTask()); // NOTE: for DependenciesGraph

            buildTasks.Add(extractData);

            return ContentPipeline.BuildAssetBundles(buildParams,
                new BundleBuildContent(allBundleInputDefs),
                out var buildResults, buildTasks, aaContext);
        }
        

        #region SORTING
        
        public static string defaultGroupGuid = "";

        /// <summary>
        /// Addressable Groupのalphanumericソート
        /// </summary>
        public static int CompareGroup(AddressableAssetGroup a, AddressableAssetGroup b)
        {
            // NOTE: IsDefaultGroup()は内部でソート中のgroupsを毎回検索するのでおかしくなる
            if (a.Guid == defaultGroupGuid)
                return -1;
            if (b.Guid == defaultGroupGuid)
                return 1;
            var nameA = a.name;
            var nameB = b.name;
            if (nameA[0] == '+' && nameB[0] != '+')
                return 1;
            if (nameA[0] != '+' && nameB[0] == '+')
                return -1;

            return CompareAlphanumeric(nameA, nameB);
        }
        /// <summary>
        /// Addressable Groupのalphanumericソート
        /// </summary>
        public static int CompareGroup(string a, string b)
        {
            if (a.Contains(UNITY_BUILTIN_SHADERS))
                return -1;
            if (b.Contains(UNITY_BUILTIN_SHADERS))
                return 1;

            return CompareAlphanumeric(a, b);
        }

        /// <summary>
        /// ordinal comparison that aligns numbers with different digits
        /// </summary>
        public static int CompareAlphanumeric(string a, string b)
        {
            var ret = string.CompareOrdinal(a, b);
            // 桁数の違う数字を揃える
            // NOTE: count only ASCII digits (char.IsDigit also counts Unicode digits)
            var digitsA = CountDigits(a);
            var digitsB = CountDigits(b);
            if ((digitsA > 0 && digitsB > 0) && digitsA != digitsB)
            {
                if (ret > 0 && digitsA < digitsB)
                    return -1;
                else if (ret < 0 && digitsA > digitsB)
                    return 1;
            }

            return ret;
        }

        static int CountDigits(string s)
        {
            var count = 0;
            foreach (var c in s)
            {
                if (c >= '0' && c <= '9')
                    ++count;
            }
            return count;
        }
        
        #endregion
    }
}