using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Timeline;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Timeline;

namespace AssetLauncher
{
    [Serializable]
    public sealed class AssetLauncherGroup
    {
        private const int kCommentSpace = 8;
        private const int kInvalidIndex = -1;

        [SerializeField]
        private int m_Id;

        [SerializeField]
        private string m_GroupName;

        [SerializeField]
        private List<AssetLauncherItem> m_ItemList = new();

        [SerializeField]
        private bool m_FoldOut = true;

        [SerializeField]
        private int m_SelectIndex;

        [SerializeField]
        private Color m_FontColor = Color.white;
        
        [SerializeField]
        private Color m_BackgroundColor = Color.white;

        private UnityEngine.Object m_SelectItemObject;
        private ReorderableList m_ReorderableList;
        private Vector2 m_ScrollPosition;
        private GUILayoutOption m_ColorGuiWidth;

        public Action<AssetLauncherGroup> OnModified { get; set; }
        public Action<AssetLauncherGroup> OnModifiedName { get; set; }
        public AssetLauncherWindow.Settings Settings { get; set; }
        
        public AssetLauncherWindow.Shared Shared { get; set; }
        public Color FontColor => m_FontColor;
        public Color BackgroundColor => m_BackgroundColor;

        public int Id
        {
            get => m_Id;
            set => m_Id = value;
        }

        public string GroupName
        {
            get => m_GroupName;
            set => m_GroupName = value;
        }

        private AssetLauncherItem CurrentItem =>
            m_SelectIndex >= 0 && m_SelectIndex < m_ItemList.Count
                ? m_ItemList[m_SelectIndex]
                : null;

        public void DrawHeader()
        {
            GUILayout.Space(8);
            
            using (new GUILayout.HorizontalScope())
            {
                var groupName = EditorGUILayout.TextField("Group Name", m_GroupName);
                if (groupName != m_GroupName)
                {
                    m_GroupName = groupName;
                    OnModifiedName.Invoke(this);
                }

                m_ColorGuiWidth ??= GUILayout.Width(40);

                var fontColor = EditorGUILayout.ColorField(string.Empty, m_FontColor, m_ColorGuiWidth);
                if (fontColor != m_FontColor)
                {
                    m_FontColor = fontColor;
                    OnModified.Invoke(this);
                }

                var backgroundColor = EditorGUILayout.ColorField(string.Empty, m_BackgroundColor, m_ColorGuiWidth);
                if (backgroundColor != m_BackgroundColor)
                {
                    m_BackgroundColor = backgroundColor;
                    OnModified.Invoke(this);
                }
            }

            UpdateFoldOutTargetList(FoldOutWithMouseDown(m_FoldOut, "Target List"));
            if (TryAcceptDropOnRect(GUILayoutUtility.GetLastRect(), out var paths))
            {
                foreach (var path in paths)
                {
                    m_ItemList.Add(new AssetLauncherItem
                    {
                        Asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)
                    });
                }
                OnModified.Invoke(this);
            }
            if (m_FoldOut)
            {
                SetupReorderableList();
                m_ReorderableList.DoLayoutList();
            }
        }

        public void DrawBody()
        {
            var currentItem = CurrentItem;

            using (new GUILayout.HorizontalScope())
            {
                var buttonLabel = new GUIContent(currentItem?.NameWithComment);
                var buttonStyle = EditorStyles.popup;

                GUILayout.Label("Item Selection", GUILayout.Width(100));

                var rect = GUILayoutUtility.GetRect(buttonLabel, buttonStyle);
                if (GUI.Button(rect, buttonLabel, buttonStyle))
                {
                    new TargetDropdown(new AdvancedDropdownState())
                    {
                        OnItemSelected = SelectItem,
                        QueryItemList = Enumerable.Range(0, m_ItemList.Count)
                            .Where(x => m_ItemList[x] != null)
                            .Select(x => (m_ItemList[x].NameWithComment, x))
                    }.Show(rect);
                }
            }

            GUILayout.Box(string.Empty, GUILayout.ExpandWidth(true), GUILayout.Height(4));

            using var scroll = new GUILayout.ScrollViewScope(m_ScrollPosition);
            m_ScrollPosition = scroll.scrollPosition;

            if (Shared.Editor == null || currentItem == null)
            {
                return;
            }
            
            using var _ = new EditorGUILayout.VerticalScope();
            
            if (Shared.Editor is MaterialEditor) 
            {
                Shared.Editor.DrawHeader();
            }

            if (m_SelectItemObject is TimelineAsset timelineAsset)
            {
                if (GUILayout.Button("Open Timeline Editor"))
                {
                    OpenTimelineEditor(timelineAsset);
                }

                GUILayout.Space(8);
            }

            Shared.Editor.OnInspectorGUI();
        }

        private void OpenTimelineEditor(TimelineAsset timelineAsset)
        {
            var window = TimelineEditor.GetOrCreateWindow();

            if (window == null)
            {
                return;
            }

            window.SetTimeline(timelineAsset);
            window.Show();

            if (!window.hasFocus)
            {
                window.Focus();
            }
        }

        private void SetupReorderableList()
        {
            if (m_ReorderableList != null)
            {
                return;
            }
            
            var elementType = typeof(UnityEngine.Object);
            
            m_ReorderableList = new ReorderableList(m_ItemList, elementType)
            {
                draggable = true,
                multiSelect = true,
                elementHeightCallback = index => EditorGUIUtility.singleLineHeight,
                headerHeight = 0,

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    
                    var item = m_ItemList[index];
                    var modified = false;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var commentWidth = Settings.EnabledItemComment ? Settings.ItemCommentWidth : 0;
                        var commentSpace = Settings.EnabledItemComment ? kCommentSpace : 0;

                        var objectFieldRect = new Rect(rect.x, rect.y, rect.width - commentWidth - commentSpace, rect.height);
                        var asset = EditorGUI.ObjectField(objectFieldRect, item.Asset, elementType, false);
                        if (!ReferenceEquals(asset, item.Asset))
                        {
                            item.Asset = asset;
                            modified = true;
                        }

                        if (commentWidth > 0)
                        {
                            var commentRect = new Rect(rect.x + rect.width - commentWidth, rect.y, commentWidth, rect.height);
                            var comment = EditorGUI.TextField(commentRect, item.Comment);
                            if (comment != item.Comment)
                            {
                                item.Comment = comment;
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        OnModified.Invoke(this);
                    }
                },

                onSelectCallback = list =>
                {
                    if (list.selectedIndices.Count >= 1)
                    {
                        if (list.selectedIndices.Contains(m_SelectIndex))
                        {
                            return;
                        }

                        SelectItem(list.selectedIndices[0]);
                        return;
                    }

                    DeSelectItem();
                },
                onAddCallback = list =>
                {
                    m_ItemList.Add(new AssetLauncherItem());
                    OnModified.Invoke(this);
                },
                onRemoveCallback = list =>
                {
                    if (m_ItemList.Count <= 0)
                    {
                        return;
                    }

                    if (list.selectedIndices.Count > 1)
                    {
                        foreach (var index in list.selectedIndices.Reverse())
                        {
                            m_ItemList.RemoveAt(index);
                        }
                        DeSelectItem();
                    }
                    else if (list.selectedIndices.Count == 1)
                    {
                        m_ItemList.RemoveAt(list.selectedIndices[0]);
                        do
                        {
                            if (m_ItemList.Count <= 0)
                            {
                                DeSelectItem();
                                break;
                            }

                            var newIndex = list.selectedIndices[0] - 1;
                            if (newIndex < 0)
                            {
                                DeSelectItem();
                                break;
                            }

                            SelectItem(newIndex);
                        } while (false);
                    }
                    else
                    {
                        m_ItemList.RemoveAt(m_ItemList.Count - 1);
                        OnModified.Invoke(this);
                    }
                }
            };

            if (m_ItemList.Count > 0)
            {
                m_ReorderableList.index = m_SelectIndex;
                SelectItem(m_SelectIndex);
            }
        }

        public static bool FoldOutWithMouseDown(bool foldOut, string content)
        {
            var newFoldOut = EditorGUILayout.Foldout(foldOut, content);

            if (newFoldOut != foldOut)
            {
                return newFoldOut;
            }

            var currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown)
            {
                if (GUILayoutUtility.GetLastRect().Contains(currentEvent.mousePosition))
                {
                    currentEvent.Use();
                    return !foldOut;
                }
            }

            return foldOut;
        }

        private static bool TryAcceptDropOnRect(Rect rect, out string[] paths)
        {
            var currentEvent = Event.current;

            if (!rect.Contains(currentEvent.mousePosition))
            {
                paths = Array.Empty<string>();
                return false;
            }

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    currentEvent.Use();
                    break;
                
                case EventType.DragPerform:
                    paths = DragAndDrop.paths;
                    DragAndDrop.AcceptDrag();
                    currentEvent.Use();
                    return true;
            }

            paths = Array.Empty<string>();
            return false;
        }

        private void UpdateFoldOutTargetList(bool on)
        {
            if (m_FoldOut == on)
            {
                return;
            }
            m_FoldOut = on;
            OnModified.Invoke(this);
        }

        public void RefreshEditor()
        {
            var currentItem = CurrentItem;
            if (currentItem == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(currentItem.Asset);
            var requiredImporterEditor = false;
            
            switch (currentItem.Asset)
            {
                case DefaultAsset:
                    if (!AssetDatabase.IsValidFolder(path))
                    {
                        break;
                    }

                    var asset = GetFirstContainsAsset(path);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                    break;
                
                case GameObject:
                    if (path.EndsWith(".prefab"))
                    {// We can not create Prefab Importer Editor.
                        break;
                    }
                    requiredImporterEditor = true;
                    break;
                
                case Texture2D:
                    requiredImporterEditor = true;
                    break;
            }

            if (requiredImporterEditor)
            {
                Shared.SetImporterEditor(path);
            }
            else
            {
                Shared.SetEditor(currentItem.Asset);
            }
        }

        private void SelectItem(int index)
        {
            m_SelectIndex = index;

            m_SelectItemObject = null;

            OnModified.Invoke(this);

            if (m_ReorderableList != null)
            {
                if (m_ReorderableList.index != m_SelectIndex)
                {
                    if (m_SelectIndex < 0)
                    {
                        m_ReorderableList.ClearSelection();
                    }
                    else
                    {
                        m_ReorderableList.index = m_SelectIndex;
                    }
                }
            }
            
            if (index < 0)
            {
                return;
            }
            
            m_SelectItemObject = m_ItemList[index].Asset;
            RefreshEditor();
        }

        private void DeSelectItem()
        {
            SelectItem(kInvalidIndex);
        }

        private UnityEngine.Object GetFirstContainsAsset(string path)
        {
            var dir = Directory
                .GetDirectories(path, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            
            if (!string.IsNullOrEmpty(dir))
            {
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dir);
            }

            var meta = Directory
                .GetFiles(path, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(x => !x.EndsWith(".meta"));

            if (!string.IsNullOrEmpty(meta))
            {
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(meta);
            }

            return null;
        }

        private sealed class TargetDropdownItem : AdvancedDropdownItem
        {
            public int Index { get; }

            public TargetDropdownItem(string name, int index) : base(name)
            {
                Index = index;
            }
        }

        private sealed class TargetDropdown : AdvancedDropdown
        {
            public IEnumerable<(string name, int id)> QueryItemList { get; set; }
            public Action<int> OnItemSelected { get; set; }

            public TargetDropdown(AdvancedDropdownState state) : base(state)
            {
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is TargetDropdownItem v)
                {
                    OnItemSelected?.Invoke(v.Index);
                }
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(string.Empty);
                root.AddChild(new TargetDropdownItem(string.Empty, -1));
                foreach (var (name, index) in QueryItemList)
                {
                    root.AddChild(new TargetDropdownItem(name, index));
                }
                return root;
            }
        }
    }
}