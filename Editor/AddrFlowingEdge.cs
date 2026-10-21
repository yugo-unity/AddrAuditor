
using System.Reflection;
using UnityEngine;
using UnityEditor.Experimental.GraphView;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// custom edge
    /// </summary>
    public class FlowingEdge : Edge
    {
        private static readonly Color SELECTED_COLOR = Color.green;

        static readonly FieldInfo selectedColorField = typeof(Edge).GetField("m_SelectedColor", BindingFlags.Instance | BindingFlags.NonPublic);
        Color selectedDefaultColor;


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
                    this.selectedDefaultColor = (Color)selectedColorField.GetValue(this);
                }
                else
                {
                    selectedColorField.SetValue(this, this.selectedDefaultColor);
                    this.edgeControl.inputColor = this.edgeControl.outputColor = this.selectedDefaultColor;
                }
            }
        }

        public FlowingEdge()
        {
            this.capabilities &= ~Capabilities.Deletable; // Edgeの削除を禁止
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
            {
                selectedColorField.SetValue(this, SELECTED_COLOR);
                this.edgeControl.inputColor = this.edgeControl.outputColor = SELECTED_COLOR;
            }
            return base.UpdateEdgeControl();
        }
        
    }
}