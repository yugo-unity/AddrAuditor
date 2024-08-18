using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

namespace UTJ
{
    /// <summary>
    /// display line flow
    /// Adjusted the following:
    /// https://forum.unity.com/threads/how-to-add-flow-effect-to-edges-in-graphview.1326012/
    /// </summary>
    public class FlowingEdge : Edge
    {
        const float FLOW_SPEED = 150f;
        
        float _flowSize = 6f;
        readonly Image flowImg;

        float totalEdgeLength, passedEdgeLength, currentPhaseLength;
        int phaseIndex;
        double phaseStartTime, phaseDuration;

        readonly FieldInfo selectedColorField;
        Color selectedDefaultColor;

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
        /// ポイント表示の有効無効
        /// </summary>
        bool _activeFlow;
        public bool activeFlow
        {
            get => _activeFlow;
            set
            {
                if (_activeFlow == value)
                    return;

                this.selected = _activeFlow = value;
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
            this.selectedColorField = typeof(Edge).GetField("m_SelectedColor", BindingFlags.Instance | BindingFlags.NonPublic);
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
        void UpdateFlow()
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
                this.phaseDuration = this.currentPhaseLength / FLOW_SPEED;
            }
        }

        /// <summary>
        /// Edgeが変形された時のコールバック
        /// </summary>
        /// <param name="evt"></param>
        void OnEdgeControlGeometryChanged(GeometryChangedEvent evt)
        {
            this.ResetFlowing();
        }

        /// <summary>
        /// ポイントの座標と距離再計算
        /// </summary>
        void ResetFlowing()
        {
            this.phaseIndex = 0;
            this.passedEdgeLength = 0f;
            this.phaseStartTime = EditorApplication.timeSinceStartup;
            this.currentPhaseLength = Vector2.Distance(this.edgeControl.controlPoints[phaseIndex],
                this.edgeControl.controlPoints[phaseIndex + 1]);
            this.phaseDuration = this.currentPhaseLength / FLOW_SPEED;
            this.flowImg.transform.position = this.edgeControl.controlPoints[phaseIndex];

            // Calculate edge path length
            this.totalEdgeLength = 0;
            for (var i = 0; i < this.edgeControl.controlPoints.Length - 1; i++)
            {
                var p = this.edgeControl.controlPoints[i];
                var pNext = this.edgeControl.controlPoints[i + 1];
                var phaseLen = Vector2.Distance(p, pNext);
                this.totalEdgeLength += phaseLen;
            }

            if (this.activeFlow)
                this.selectedColorField.SetValue(this, Color.green);
        }
    }
}