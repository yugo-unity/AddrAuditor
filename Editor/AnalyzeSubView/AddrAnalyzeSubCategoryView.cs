using UnityEngine.UIElements;
using System.Collections.Generic;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// Subカテゴリインスタンス
    /// </summary>
    internal abstract class SubCategoryView
    {
        public VisualElement rootElement { get; private set; }

        public void CreateView(TwoPaneSplitViewOrientation orientation)
        {
            var dimension = orientation == TwoPaneSplitViewOrientation.Horizontal ? 500 : 70;
            var root = new TwoPaneSplitView(0, dimension, orientation);
            this.rootElement = root;
            this.OnCreateView();
            this.UpdateView(); // 初回更新
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public abstract void Analyze();

        /// <summary>
        /// 固有Viewの生成
        /// </summary>
        protected abstract void OnCreateView();

        /// <summary>
        /// 表示の更新
        /// カテゴリが選択された時に呼ばれる
        /// </summary>
        public abstract void UpdateView();
    }
}