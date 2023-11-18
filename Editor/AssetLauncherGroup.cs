using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetLauncher
{
    [Serializable]
    public sealed class AssetLauncherGroup
    {
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

        private Editor m_SelectItemEditor;
        private ReorderableList m_ReorderableList;
        private Vector2 m_ScrollPosition;

        public Action<AssetLauncherGroup> OnModified { get; set; }
        public Action<AssetLauncherGroup> OnModifiedName { get; set; }

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

        public void OnInspectorGUI()
        {
            DrawHeader();
            DrawBody();
        }

        private void DrawHeader()
        {
            var groupName = EditorGUILayout.TextField("Group Name", m_GroupName);
            if (groupName != m_GroupName)
            {
                m_GroupName = groupName;
                OnModifiedName.Invoke(this);
            }

            UpdateFoldOutTargetList(EditorGUILayout.Foldout(m_FoldOut, "Target List"));
            if (m_FoldOut)
            {
                SetupReorderableList();
                m_ReorderableList.DoLayoutList();
            }

            if (m_ItemList.Count <= 0)
            {
                return;
            }

            var selectItem = m_SelectIndex < 0 ? null : m_ItemList[m_SelectIndex];
            var buttonLabel = new GUIContent(selectItem?.Name);
            var buttonStyle = EditorStyles.popup;
            var rect = GUILayoutUtility.GetRect(buttonLabel, buttonStyle);
            if (GUI.Button(rect, buttonLabel, buttonStyle))
            {
                new TargetDropdown(new AdvancedDropdownState())
                {
                    OnItemSelected = SelectItem,
                    QueryItemList = Enumerable.Range(0, m_ItemList.Count)
                        .Where(x => m_ItemList[x] != null)
                        .Select(x => (m_ItemList[x].Name, x))
                }.Show(rect);
            }
            
            GUILayout.Box(string.Empty, GUILayout.ExpandWidth(true), GUILayout.Height(4));
        }

        private void DrawBody()
        {
            using var scroll = new GUILayout.ScrollViewScope(m_ScrollPosition);
            m_ScrollPosition = scroll.scrollPosition;

            if (m_SelectItemEditor == null)
            {
                return;
            }
            
            using var _ = new EditorGUILayout.VerticalScope();
            
            if (m_SelectItemEditor is MaterialEditor) 
            {
                m_SelectItemEditor.DrawHeader();
            }
            
            m_SelectItemEditor.OnInspectorGUI();
        }

        private void SetupReorderableList()
        {
            if (m_ReorderableList != null)
            {
                return;
            }
            
            var elementType = typeof(Object);
            
            m_ReorderableList = new ReorderableList(m_ItemList, elementType)
            {
                draggable = true,
                multiSelect = false,
                elementHeightCallback = index => EditorGUIUtility.singleLineHeight,
                headerHeight = 0,

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.height = EditorGUIUtility.singleLineHeight;
                    
                    var item = m_ItemList[index];
                    var asset = EditorGUI.ObjectField(rect, item.Asset, elementType, false);
                    if (ReferenceEquals(asset, item.Asset))
                    {
                        return;
                    }

                    item.Asset = asset;
                    OnModified.Invoke(this);
                },

                onSelectCallback = list =>
                {
                    SelectItem(list.selectedIndices[0]);
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

                    m_ItemList.RemoveAt(list.selectedIndices.Count > 0 ? list.selectedIndices[0] : m_ItemList.Count - 1);
                    SelectItem(-1);
                    OnModified.Invoke(this);
                }
            };

            if (m_ItemList.Count > 0)
            {
                m_ReorderableList.index = m_SelectIndex;
                SelectItem(m_SelectIndex);
            }
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

        private void SelectItem(int index)
        {
            m_SelectIndex = index;

            if (m_SelectItemEditor != null)
            {
                Object.DestroyImmediate(m_SelectItemEditor);
                m_SelectItemEditor = null;
            }
            
            OnModified.Invoke(this);

            if (m_ReorderableList != null)
            {
                if (m_ReorderableList.index != m_SelectIndex)
                {
                    m_ReorderableList.index = m_SelectIndex;
                }
            }
            
            if (index < 0)
            {
                return;
            }

            var item = m_ItemList[index];
            var path = AssetDatabase.GetAssetPath(item.Asset);

            switch (item.Asset)
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
                    {
                        break;
                    }
                    m_SelectItemEditor = CreateImporterEditor(path);
                    break;
                
                case Texture2D:
                    m_SelectItemEditor = CreateImporterEditor(path);
                    break;
                
                default:
                    m_SelectItemEditor = Editor.CreateEditor(item.Asset);
                    break;
            }
        }

        private static Editor CreateImporterEditor(string path) 
        {
            var importer = AssetImporter.GetAtPath(path);
            return Editor.CreateEditor(importer);
        }

        private Object GetFirstContainsAsset(string path)
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