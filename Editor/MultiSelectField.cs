using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// the list that can be selected multiple values
    /// </summary>
    /// <typeparam name="T">class of list contents</typeparam>
    public class MultiSelectField<T> : VisualElement where T : class
    {
        const int INITIAL_CAPACITY = 1000;
        List<T> availableList = new (INITIAL_CAPACITY); // 全体リスト
        List<bool> selectedStates = new (INITIAL_CAPACITY); // 選択状態
        List<T> selection = new (INITIAL_CAPACITY); // 選択されているものだけのリスト
        Button tabButton;
        VisualElement headerBox, root, label;
        ScrollView scrollView;
        List<Toggle> toggles = new (INITIAL_CAPACITY);
        EditorWindow window;

        public List<T> selectedValues => selection;
        public System.Action<List<T>> OnSelectionChanged;
        System.Func<T, string> toggleChangedCallback;
        System.Action<bool> buttonClickedCallback; 

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="window">using EditorWindow</param>
        /// <param name="root">parent element</param>
        /// <param name="list">displayed list</param>
        /// <param name="buttonLabel">label for button to open/close</param>
        /// <param name="listName">label for list</param>
        /// <param name="toggleChangedCallback">callback when changed any toggle</param>
        /// <param name="buttonClickedCallback">callback when clicked button to open/close</param>
        public MultiSelectField(EditorWindow window, VisualElement root, List<T> list, string buttonLabel, string listName,
            System.Func<T, string> toggleChangedCallback, System.Action<bool> buttonClickedCallback)
        {
            this.window = window;
            this.root = root;
            this.toggleChangedCallback = toggleChangedCallback;
            this.buttonClickedCallback = buttonClickedCallback;

            this.tabButton = new Button(this.OpenCloseList);
            this.tabButton.text = buttonLabel;
            this.tabButton.style.height = new Length(30f, LengthUnit.Pixel);
            this.Add(this.tabButton);

            if (!string.IsNullOrEmpty(listName))
            {
                this.headerBox = AddrUtility.CreateSpace(root);
                this.headerBox.style.display = DisplayStyle.None;
                this.label = new Label(listName);
                this.label.style.unityFontStyleAndWeight = FontStyle.Bold;
                this.label.style.display = DisplayStyle.None;
                root.Add(label);
            }

            this.scrollView = new ScrollView();
            this.scrollView.style.minWidth = root.style.width;
            this.scrollView.style.flexDirection = FlexDirection.Row;
            //this.scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            this.root.Add(this.scrollView);
            this.root.style.display = DisplayStyle.None;
            
            this.UpdateList(list);
            // ウィンドウの高さを取得しScrollViewの高さを設定
            this.window.rootVisualElement.RegisterCallback<GeometryChangedEvent>(this.UpdateScrollViewHeight);
        }

        /// <summary>
        /// update contents in ScrollView
        /// </summary>
        /// <param name="list">new list</param>
        public void UpdateList(List<T> list)
        {
            this.availableList = list;

            foreach (var toggle in this.toggles)
                this.scrollView.Remove(toggle);
            this.scrollView.Clear();
            this.toggles.Clear();
            this.selectedStates.Clear();
            this.selection.Clear();
            
            // 更新後にスクロールバー調整をする
            this.scrollView.style.height = StyleKeyword.Auto;
            this.scrollView.RegisterCallbackOnce<GeometryChangedEvent>(this.UpdateScrollViewHeight);

            for (var i = 0; i < this.availableList.Count; i++)
            {
                var index = i;
                var title = this.toggleChangedCallback?.Invoke(availableList[i]);
                var toggle = AddrUtility.CreateToggle(scrollView, title, null, true, 250);
                toggle.RegisterValueChangedCallback((evt) => OnToggleValueChanged(index, evt.newValue));
                this.toggles.Add(toggle);
                this.selection.Add(this.availableList[i]);
                this.selectedStates.Add(true);
            }
        }

        /// <summary>
        /// limit ScrollView height in EditorWindow
        /// </summary>
        /// <param name="evt">changed event</param>
        void UpdateScrollViewHeight(GeometryChangedEvent evt)
        {
            var limit = this.window.rootVisualElement.worldBound.height - 30f;
            // Debug.LogWarning(//$"scrollView world height : {this.scrollView.worldBound.height} / " +
            //                  //$"scrollView layout height : {this.scrollView.layout.height} / " +
            //                  //$"scrollView resolve height : {this.scrollView.resolvedStyle.height} / " +
            //                  $"window world height : {limit} / " +
            //                  //$"content container height : {this.scrollView.contentContainer.layout.height} / " +
            //                  $"new height : {evt.newRect.height}");

            if (evt.newRect.height > limit)
                this.scrollView.style.height = limit;
        }

        /// <summary>
        /// Process when Button is clicked
        /// </summary>
        void OpenCloseList()
        {
            if (this.root == null)
                return;
            var displayStyle = this.root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            this.root.style.display = displayStyle;
            if (this.headerBox != null)
            {
                this.headerBox.style.display = this.label.style.display = displayStyle;
                this.headerBox.style.display = displayStyle;
            }
            this.buttonClickedCallback?.Invoke(displayStyle == DisplayStyle.Flex);

            this.scrollView.style.height = StyleKeyword.Auto;
        }

        /// <summary>
        /// Callback when Toggle is changed
        /// </summary>
        /// <param name="index">toggle number</param>
        /// <param name="isOn">toggle state</param>
        void OnToggleValueChanged(int index, bool isOn)
        {
            var changed = false;
            if (index == 0)
            {
                changed = this.selectedStates[0] != isOn;
                this.selectedStates[0] = isOn;
                this.toggles[0].value = isOn;
                for (var i = 1; i < this.selectedStates.Count; i++)
                {
                    this.selectedStates[i] = isOn;
                    this.toggles[i].SetValueWithoutNotify(isOn);
                }
            }
            else
            {
                changed = this.selectedStates[index] != isOn;
                this.selectedStates[index] = isOn;
                if (!isOn)
                    this.toggles[0].SetValueWithoutNotify(false);
            }
            
            this.selection.Clear();
            for (var i = 0; i < this.selectedStates.Count(); i++)
            {
                if (this.selectedStates[i])
                    this.selection.Add(this.availableList[i]);
            }
            if (changed)
                OnSelectionChanged?.Invoke(this.selection);
        }
    }
}
