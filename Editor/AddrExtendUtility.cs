/***********************************************************************************************************
 * AddrExtendUtility.cs
 * Copyright (c) Yugo Fujioka - Unity Technologies Japan K.K.
 *
 * Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
 * https://unity.com/legal/licenses/unity-companion-license
 * Unless expressly provided otherwise, the Software under this license is made available strictly
 * on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
 * Please review the license for details on these and other terms and conditions.
 ***********************************************************************************************************/

using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.AddressableAssets.Initialization;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Experimental.GraphView;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;

namespace UTJ
{
    /// <summary>
    /// Edgeの方向表示
    /// Adjusted the following:
    /// https://forum.unity.com/threads/how-to-add-flow-effect-to-edges-in-graphview.1326012/
    /// </summary>
    public class FlowingEdge : Edge
    {
        #region MEMBER

        private float _flowSize = 6f;
        private readonly Image flowImg;

        private float totalEdgeLength, passedEdgeLength, currentPhaseLength;
        private int phaseIndex;
        private double phaseStartTime, phaseDuration;

        private FieldInfo selectedColorField = null;
        private Color selectedDefaultColor;

        #endregion


        #region PROPERTY

        /// <summary>
        /// ポイントサイズ
        /// </summary>
        public float flowSize
        {
            get => this._flowSize;
            set
            {
                this._flowSize = value;
                this.flowImg.style.width = new Length(this._flowSize, LengthUnit.Pixel);
                this.flowImg.style.height = new Length(this._flowSize, LengthUnit.Pixel);
            }
        }

        /// <summary>
        /// ポイントの移動速度
        /// </summary>
        public float flowSpeed { get; set; } = 150f;

        /// <summary>
        /// ポイント表示の有効無効
        /// </summary>
        private bool __activeFlow;

        public bool activeFlow
        {
            get => __activeFlow;
            set
            {
                if (__activeFlow == value)
                    return;

                this.selected = __activeFlow = value;
                if (value)
                {
                    this.selectedDefaultColor = (Color)this.selectedColorField.GetValue(this);
                    this.Add(this.flowImg);
                    this.ResetFlowing();
                }
                else
                {
                    this.Remove(this.flowImg);
                    this.selectedColorField.SetValue(this, this.selectedDefaultColor);
                }
            }
        }

        #endregion


        #region MAIN FUNCTION

        public FlowingEdge()
        {
            this.flowImg = new Image
            {
                name = "flow-image",
                style =
                {
                    width = new Length(flowSize, LengthUnit.Pixel),
                    height = new Length(flowSize, LengthUnit.Pixel),
                    borderTopLeftRadius = new Length(flowSize / 2, LengthUnit.Pixel),
                    borderTopRightRadius = new Length(flowSize / 2, LengthUnit.Pixel),
                    borderBottomLeftRadius = new Length(flowSize / 2, LengthUnit.Pixel),
                    borderBottomRightRadius = new Length(flowSize / 2, LengthUnit.Pixel),
                },
            };
            this.schedule.Execute(timer => { this.UpdateFlow(); }).Every(66); // 15fpsで更新
            this.capabilities &= ~Capabilities.Deletable; // Edgeの削除を禁止
            this.edgeControl.RegisterCallback<GeometryChangedEvent>(OnEdgeControlGeometryChanged);

            // 本来はCustomStyleで設定するものだが面倒なのでReflection
            this.selectedColorField =
                typeof(Edge).GetField("m_SelectedColor", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        /// <summary>
        /// Edgeにフォーカスされた際などのコールバック
        /// </summary>
        /// <returns></returns>
        public override bool UpdateEdgeControl()
        {
            // 内部的に戻されるので都度設定する
            // そもそも色を変えることを想定されていない
            if (this.activeFlow)
                this.selectedColorField.SetValue(this, Color.green);
            return base.UpdateEdgeControl();
        }

        /// <summary>
        /// 定時更新
        /// </summary>
        private void UpdateFlow()
        {
            if (!this.activeFlow)
                return;

            // Position
            var posProgress = (float)((EditorApplication.timeSinceStartup - this.phaseStartTime) / this.phaseDuration);
            var flowStartPoint = this.edgeControl.controlPoints[phaseIndex];
            var flowEndPoint = this.edgeControl.controlPoints[phaseIndex + 1];
            var flowPos = Vector2.Lerp(flowStartPoint, flowEndPoint, posProgress);
            this.flowImg.transform.position = flowPos - Vector2.one * flowSize / 2;

            // Color
            var colorProgress = (this.passedEdgeLength + this.currentPhaseLength * posProgress) / this.totalEdgeLength;
            var startColor = this.edgeControl.outputColor;
            var endColor = this.edgeControl.inputColor;
            var flowColor = Color.Lerp(startColor, endColor, (float)colorProgress);
            this.flowImg.style.backgroundColor = flowColor;

            // Enter next phase
            if (posProgress >= 0.99999f)
            {
                this.passedEdgeLength += this.currentPhaseLength;

                this.phaseIndex++;
                if (this.phaseIndex >= this.edgeControl.controlPoints.Length - 1)
                {
                    // Restart flow
                    this.phaseIndex = 0;
                    this.passedEdgeLength = 0f;
                }

                this.phaseStartTime = EditorApplication.timeSinceStartup;
                this.currentPhaseLength = Vector2.Distance(this.edgeControl.controlPoints[phaseIndex],
                    this.edgeControl.controlPoints[phaseIndex + 1]);
                this.phaseDuration = this.currentPhaseLength / this.flowSpeed;
            }
        }

        /// <summary>
        /// Edgeが変形された時のコールバック
        /// </summary>
        /// <param name="evt"></param>
        private void OnEdgeControlGeometryChanged(GeometryChangedEvent evt)
        {
            this.ResetFlowing();
        }

        /// <summary>
        /// ポイントの座標と距離再計算
        /// </summary>
        private void ResetFlowing()
        {
            this.phaseIndex = 0;
            this.passedEdgeLength = 0f;
            this.phaseStartTime = EditorApplication.timeSinceStartup;
            this.currentPhaseLength = Vector2.Distance(this.edgeControl.controlPoints[phaseIndex],
                this.edgeControl.controlPoints[phaseIndex + 1]);
            this.phaseDuration = this.currentPhaseLength / this.flowSpeed;
            this.flowImg.transform.position = this.edgeControl.controlPoints[phaseIndex];

            // Calculate edge path length
            this.totalEdgeLength = 0;
            for (int i = 0; i < this.edgeControl.controlPoints.Length - 1; i++)
            {
                var p = this.edgeControl.controlPoints[i];
                var pNext = this.edgeControl.controlPoints[i + 1];
                var phaseLen = Vector2.Distance(p, pNext);
                this.totalEdgeLength += phaseLen;
            }

            if (this.activeFlow)
                this.selectedColorField.SetValue(this, Color.green);
        }

        #endregion
    }


    /// <summary>
    /// 補助API
    /// </summary>
    public static class AddrUtility
    {
        #region UI HELPER
        const float HELPBOX_HEIGHT = 50f;
        const float BUTTON_HEIGHT = 50f;

        public static void CreateHelpBox(VisualElement root, string text)
        {
            var helpbox = new HelpBox(text, HelpBoxMessageType.Info);
            helpbox.style.height = new Length(HELPBOX_HEIGHT, LengthUnit.Pixel);
            root.Add(helpbox);
        }

        public static void CreateSpace(VisualElement root)
        {
            var box = new Box();
            box.style.height = new Length(10f, LengthUnit.Pixel);
            root.Add(box);
        }

        public static Button CreateButton(VisualElement root, string text)
        {
            var button = new Button();
            button.text = text;
            button.style.height = new Length(BUTTON_HEIGHT, LengthUnit.Pixel);
            root.Add(button);

            return button;
        }

        public static Toggle CreateToggle(VisualElement root, string title, string tooltip, bool defaultValue)
        {
            var toggle = new Toggle(title);
            toggle.name = title;
            toggle.tooltip = tooltip;
            toggle.value = defaultValue;
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

        // public static ReturnCode CalculateBundleWriteData(out AddressableAssetsBuildContext aaContext,
        //     out ExtractDataTask extractData, out List<AssetBundleBuild> allBundleInputDefs)
        // {
        //     var settings = AddressableAssetSettingsDefaultObject.Settings;
        //     allBundleInputDefs = new List<AssetBundleBuild>();
        //     var bundleToAssetGroup = new Dictionary<string, string>();
        //     
        //     CalculateInputDefinitions(settings, allBundleInputDefs, bundleToAssetGroup);
        //     aaContext = GetBuildContext(settings, bundleToAssetGroup);
        //     extractData = new ExtractDataTask();
        //     return RefleshBuild(settings, allBundleInputDefs, extractData, aaContext);
        // }

        public static AssetBundleBuild CreateUniqueBundle(AssetBundleBuild bid, Dictionary<string, string> bundleToAssetGroup)
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
                    if (EditorUtility.DisplayCancelableProgressBar("Calculating Input Definitions", "",
                            (float)groupIndex / settings.groups.Count))
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
                //locations = m_Locations,
                //providerTypes = new HashSet<System.Type>(),
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
            // NOTE: 実際に出力するわけじゃないのでprefixは不問
            //var builtinBundleName = GetBuiltInBundleNamePrefix(aaContext) + $"{BuildScriptBase.BuiltInBundleBaseName}.bundle";
            var builtinBundleName = $"{BuildScriptBase.BuiltInBundleBaseName}.bundle";
            
            var buildTasks = new List<IBuildTask>();
            // Setup
            //buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache()); // NOTE: Atlas作っておかないとバラでくる
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

            // TODO: BuildTaskは積んであるのでBuildTasksRunner.Runで直接読んだらまずいのか
            return ContentPipeline.BuildAssetBundles(buildParams,
                new BundleBuildContent(allBundleInputDefs),
                out var buildResults, buildTasks, aaContext);
        }
    }
}