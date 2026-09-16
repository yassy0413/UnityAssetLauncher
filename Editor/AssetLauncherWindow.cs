#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AssetLauncher
{
    internal sealed class AssetLauncherWindow : EditorWindow
    {
        private readonly List<AssetLauncherGroup> m_GroupInstanceList = new();

        private string m_GroupPath = string.Empty;
        private string m_SettingsPath = string.Empty;
        private Settings m_Settings = new();
        private readonly Shared m_Shared = new();

        // UI Toolkit
        private VisualElement? m_LayoutRoot;
        private VisualElement? m_GroupGrid;
        private VisualElement? m_GroupHeaderHost;
        private VisualElement? m_TargetListHost;
        private VisualElement? m_InspectorHost;
        private TargetListView? m_TargetListView;

        private static AssetLauncherWindow? Instance { get; set; }

        private static string DataPath =>
            Path.Join(Application.persistentDataPath, "AssetLauncher");

        private string GetGroupDataPath(int id) =>
            $"{m_GroupPath}/group_{id}.json";

        public enum ButtonTextAnchor
        {
            Left,
            Center,
            Right,
        }

        public enum Layout
        {
            Vertical,
            Horizontal,
        }

        [Serializable]
        public sealed class Settings
        {
            public List<int> GroupIdList = new();
            public int SelectGroupIndex;
            public bool FoldOut;
            public int GroupSelectionXCount = 4;
            public int GroupSelectionWidth = 300;
            public int GroupSelectionHeight = 60;
            public int TargetListHeight = 160;
            public int ItemCommentWidth = 180;
            public int ItemFontSize = 12;
            public bool EnabledItemComment;
            public ButtonTextAnchor ButtonTextAnchor = ButtonTextAnchor.Center;
            public Layout Layout = Layout.Vertical;
        }

        public sealed class Shared
        {
            private Editor? m_Editor;
            public Editor? Editor => m_Editor;

            public void SetEditor(Object targetObject) =>
                Editor.CreateCachedEditor(targetObject, null!, ref m_Editor!);

            public void SetImporterEditor(string path) =>
                Editor.CreateCachedEditor(AssetImporter.GetAtPath(path), null!, ref m_Editor!);

            public void ClearEditor()
            {
                if (m_Editor == null)
                {
                    return;
                }

                DestroyImmediate(m_Editor);
                m_Editor = null;
            }
        }

        [Shortcut("AssetLauncher/Open Key", KeyCode.L, ShortcutModifiers.Control)]
        [Shortcut("AssetLauncher/Open Mouse", KeyCode.Mouse2, ShortcutModifiers.Control)]
        [MenuItem("Window/Asset Launcher")]
        public static void ToggleShow()
        {
            if (Instance == null)
            {
                Instance = GetWindow<AssetLauncherWindow>(true, "Asset Launcher");
                Instance.ShowUtility();
                return;
            }

            Instance.Close();
            Instance = null;
        }

        private void OnEnable()
        {
            Setup();

            foreach (var group in m_GroupInstanceList)
            {
                WireGroup(group);
            }
        }

        private void OnDisable()
        {
            m_Shared.ClearEditor();
        }

        private void Update()
        {
            if (m_GroupInstanceList.Count <= 0)
            {
                return;
            }

            ProcessShortcutKey();
        }

        private void Setup()
        {
            var dataPath = DataPath;
            m_GroupPath = $"{dataPath}/group";

            if (!Directory.Exists(m_GroupPath))
            {
                Directory.CreateDirectory(m_GroupPath);
            }

            m_SettingsPath = $"{dataPath}/settings.json";
            m_Settings = LoadJson<Settings>(m_SettingsPath);

            if (m_Settings.GroupIdList.Count <= 0)
            {
                m_GroupInstanceList.Clear();
                AddGroup();
            }
            else
            {
                m_GroupInstanceList.Clear();
                m_GroupInstanceList.AddRange(m_Settings.GroupIdList
                    .Select(x =>
                    {
                        var path = GetGroupDataPath(x);
                        var group = LoadJson<AssetLauncherGroup>(path);
                        group.Shared = m_Shared;
                        group.EnsureSelection();
                        return group;
                    })
                    .OrderBy(static x => x.Id));

                SelectGroup(m_Settings.SelectGroupIndex);
            }

            if (m_Settings.GroupSelectionHeight < 64)
            {
                m_Settings.GroupSelectionHeight = 64;
            }

            if (m_Settings.TargetListHeight < 40)
            {
                m_Settings.TargetListHeight = 40;
            }

            m_Settings.ItemFontSize = Math.Clamp(m_Settings.ItemFontSize,
                TargetListView.kMinFontSize, TargetListView.kMaxFontSize);

            EnsureGroupSelectionHeight();
        }

        private void WireGroup(AssetLauncherGroup group)
        {
            group.OnModified = OnGroupModified;
            group.OnModifiedName = OnGroupModifiedName;
            group.Settings = m_Settings;
            group.Shared = m_Shared;
        }

        private AssetLauncherGroup? CurrentGroup =>
            m_Settings.SelectGroupIndex >= 0 && m_Settings.SelectGroupIndex < m_GroupInstanceList.Count
                ? m_GroupInstanceList[m_Settings.SelectGroupIndex]
                : null;

        // ------------------------------------------------------------------ UI: root

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            AssetLauncherStyles.Apply(root);

            root.Add(BuildHeader());
            BuildLayout();
        }

        private void BuildLayout()
        {
            m_LayoutRoot?.RemoveFromHierarchy();
            m_LayoutRoot = null;

            m_GroupHeaderHost = new VisualElement();
            m_TargetListHost = new VisualElement { style = { flexGrow = 1 } };
            m_InspectorHost = new VisualElement { style = { flexGrow = 1 } };

            var selector = BuildGroupSelector();
            TrackFixedPaneSize(selector, vertical: true,
                get: () => m_Settings.GroupSelectionHeight,
                set: v => m_Settings.GroupSelectionHeight = v);

            if (m_Settings.Layout == Layout.Vertical)
            {
                // [ Group selector ]
                // [ Group header   ]
                // [ Target list    ]  <- splitter ->
                // [ Inspector      ]
                var groupPane = new VisualElement();
                groupPane.AddToClassList(AssetLauncherStyles.GroupPane);
                groupPane.Add(m_GroupHeaderHost);

                var innerSplit = new TwoPaneSplitView(0, m_Settings.TargetListHeight,
                    TwoPaneSplitViewOrientation.Vertical) { style = { flexGrow = 1 } };
                innerSplit.Add(m_TargetListHost);
                innerSplit.Add(m_InspectorHost);
                groupPane.Add(innerSplit);

                TrackFixedPaneSize(m_TargetListHost, vertical: true,
                    get: () => m_Settings.TargetListHeight,
                    set: v => m_Settings.TargetListHeight = v);

                var split = new TwoPaneSplitView(0, m_Settings.GroupSelectionHeight,
                    TwoPaneSplitViewOrientation.Vertical) { style = { flexGrow = 1 } };
                split.Add(selector);
                split.Add(groupPane);

                m_LayoutRoot = split;
            }
            else
            {
                // [ Group selector | Inspector ]
                // [ Group header   |           ]
                // [ Target list    |           ]
                var leftBottom = new VisualElement();
                leftBottom.AddToClassList(AssetLauncherStyles.GroupPane);
                leftBottom.Add(m_GroupHeaderHost);
                leftBottom.Add(m_TargetListHost);

                var leftSplit = new TwoPaneSplitView(0, m_Settings.GroupSelectionHeight,
                    TwoPaneSplitViewOrientation.Vertical) { style = { flexGrow = 1 } };
                leftSplit.Add(selector);
                leftSplit.Add(leftBottom);

                TrackFixedPaneSize(leftSplit, vertical: false,
                    get: () => m_Settings.GroupSelectionWidth,
                    set: v => m_Settings.GroupSelectionWidth = v);

                var right = new VisualElement();
                right.AddToClassList(AssetLauncherStyles.GroupPane);
                right.Add(m_InspectorHost);

                var split = new TwoPaneSplitView(0, m_Settings.GroupSelectionWidth,
                    TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
                split.Add(leftSplit);
                split.Add(right);

                m_LayoutRoot = split;
            }

            rootVisualElement.Add(m_LayoutRoot);
            BindCurrentGroup();
        }

        /// <summary>Persist the size of a TwoPaneSplitView fixed pane whenever the user drags the splitter.</summary>
        private void TrackFixedPaneSize(VisualElement pane, bool vertical, Func<int> get, Action<int> set)
        {
            pane.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var size = (int)(vertical ? evt.newRect.height : evt.newRect.width);
                if (size <= 0 || size == get())
                {
                    return;
                }

                set(size);
                SaveSettings();
            });
        }

        private void BindCurrentGroup()
        {
            if (m_GroupHeaderHost == null || m_TargetListHost == null || m_InspectorHost == null)
            {
                return;
            }

            m_GroupHeaderHost.Clear();
            m_TargetListHost.Clear();
            m_InspectorHost.Clear();
            m_TargetListView = null;

            var group = CurrentGroup;
            if (group == null)
            {
                return;
            }

            m_GroupHeaderHost.Add(new GroupHeaderView(group));
            m_TargetListHost.Add(m_TargetListView = new TargetListView(group, m_Settings));
            m_InspectorHost.Add(new GroupInspectorView(group));
        }

        // ------------------------------------------------------------------ UI: settings header

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList(AssetLauncherStyles.Header);

            var foldout = new Foldout { text = "Settings", value = m_Settings.FoldOut };
            foldout.AddToClassList(AssetLauncherStyles.HeaderSettings);
            foldout.RegisterValueChangedCallback(evt =>
            {
                // Child toggles bubble ChangeEvent<bool> too; only react to the foldout itself.
                if (evt.target != foldout || m_Settings.FoldOut == evt.newValue)
                {
                    return;
                }

                m_Settings.FoldOut = evt.newValue;
                SaveSettings();
            });
            header.Add(foldout);

            var xCountField = new IntegerField("Group Selection xCount")
            {
                value = m_Settings.GroupSelectionXCount, isDelayed = true
            };
            xCountField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue <= 0)
                {
                    xCountField.SetValueWithoutNotify(m_Settings.GroupSelectionXCount);
                    return;
                }

                if (evt.newValue == m_Settings.GroupSelectionXCount)
                {
                    return;
                }

                m_Settings.GroupSelectionXCount = evt.newValue;
                EnsureGroupSelectionHeight();
                SaveSettings();
                BuildLayout();
            });
            foldout.Add(xCountField);

            var commentToggle = new Toggle("Enable Item Comment") { value = m_Settings.EnabledItemComment };
            foldout.Add(commentToggle);

            var commentWidthField = new IntegerField("Item Comment Width")
            {
                value = m_Settings.ItemCommentWidth, isDelayed = true
            };
            commentWidthField.style.display = m_Settings.EnabledItemComment ? DisplayStyle.Flex : DisplayStyle.None;
            commentWidthField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue < 60)
                {
                    commentWidthField.SetValueWithoutNotify(m_Settings.ItemCommentWidth);
                    return;
                }

                if (evt.newValue == m_Settings.ItemCommentWidth)
                {
                    return;
                }

                m_Settings.ItemCommentWidth = evt.newValue;
                SaveSettings();
                m_TargetListView?.Rebuild();
            });
            foldout.Add(commentWidthField);

            commentToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != commentToggle || evt.newValue == m_Settings.EnabledItemComment)
                {
                    return;
                }

                m_Settings.EnabledItemComment = evt.newValue;
                SaveSettings();
                commentWidthField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                m_TargetListView?.Rebuild();
            });

            var fontSizeField = new IntegerField("Item Font Size")
            {
                value = m_Settings.ItemFontSize, isDelayed = true
            };
            fontSizeField.RegisterValueChangedCallback(evt =>
            {
                var fontSize = Math.Clamp(evt.newValue, TargetListView.kMinFontSize, TargetListView.kMaxFontSize);
                if (fontSize != evt.newValue)
                {
                    fontSizeField.SetValueWithoutNotify(fontSize);
                }

                if (fontSize == m_Settings.ItemFontSize)
                {
                    return;
                }

                m_Settings.ItemFontSize = fontSize;
                SaveSettings();
                m_TargetListView?.Rebuild();
            });
            foldout.Add(fontSizeField);

            var anchorField = new EnumField("Button Text Anchor", m_Settings.ButtonTextAnchor);
            anchorField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is not ButtonTextAnchor anchor || anchor == m_Settings.ButtonTextAnchor)
                {
                    return;
                }

                m_Settings.ButtonTextAnchor = anchor;
                SaveSettings();
                RefreshGroupButtons();
            });
            foldout.Add(anchorField);

            var layoutField = new EnumField("Layout", m_Settings.Layout);
            layoutField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is not Layout layout || layout == m_Settings.Layout)
                {
                    return;
                }

                m_Settings.Layout = layout;
                SaveSettings();
                BuildLayout();
            });
            foldout.Add(layoutField);

            var folderButton = new Button(() => EditorUtility.RevealInFinder(DataPath))
            {
                tooltip = "Open the tool data folder."
            };
            folderButton.AddToClassList(AssetLauncherStyles.IconButton);
            folderButton.Add(new Image { image = AssetLauncherStyles.Icon("Folder Icon"), scaleMode = ScaleMode.ScaleToFit });
            header.Add(folderButton);

            return header;
        }

        // ------------------------------------------------------------------ UI: group selector

        private VisualElement BuildGroupSelector()
        {
            var selector = new VisualElement();
            selector.AddToClassList(AssetLauncherStyles.GroupSelector);

            var box = new VisualElement();
            box.AddToClassList(AssetLauncherStyles.Box);
            selector.Add(box);

            m_GroupGrid = new VisualElement();
            m_GroupGrid.AddToClassList(AssetLauncherStyles.GroupGrid);
            box.Add(m_GroupGrid);

            var footer = new VisualElement();
            footer.AddToClassList(AssetLauncherStyles.GroupFooter);
            selector.Add(footer);

            var addButton = new Button(AddGroup) { tooltip = "Add group" };
            addButton.AddToClassList(AssetLauncherStyles.IconButton);
            addButton.Add(new Image { image = AssetLauncherStyles.Icon("Toolbar Plus"), scaleMode = ScaleMode.ScaleToFit });
            footer.Add(addButton);

            var removeButton = new Button(() => RemoveGroup(m_Settings.SelectGroupIndex)) { tooltip = "Remove selected group" };
            removeButton.AddToClassList(AssetLauncherStyles.IconButton);
            removeButton.Add(new Image { image = AssetLauncherStyles.Icon("Toolbar Minus"), scaleMode = ScaleMode.ScaleToFit });
            footer.Add(removeButton);

            RefreshGroupButtons();
            return selector;
        }

        private void RefreshGroupButtons()
        {
            if (m_GroupGrid == null)
            {
                return;
            }

            m_GroupGrid.Clear();

            var columnCount = Math.Max(1, m_Settings.GroupSelectionXCount);
            var cellWidth = Length.Percent(100f / columnCount);

            var anchorClass = m_Settings.ButtonTextAnchor switch
            {
                ButtonTextAnchor.Left => AssetLauncherStyles.AnchorLeft,
                ButtonTextAnchor.Right => AssetLauncherStyles.AnchorRight,
                _ => AssetLauncherStyles.AnchorCenter,
            };

            for (var index = 0; index < m_GroupInstanceList.Count; ++index)
            {
                var group = m_GroupInstanceList[index];
                var capturedIndex = index;

                var cell = new VisualElement { style = { width = cellWidth, paddingLeft = 1, paddingRight = 1, paddingTop = 1, paddingBottom = 1 } };

                var button = new Button(() =>
                {
                    SelectGroup(capturedIndex);
                    SaveSettings();
                })
                {
                    text = group.GroupName,
                    tooltip = group.GroupName,
                };
                button.AddToClassList(AssetLauncherStyles.GroupButton);
                button.AddToClassList(anchorClass);
                button.EnableInClassList(AssetLauncherStyles.GroupButtonSelected, index == m_Settings.SelectGroupIndex);
                ApplyGroupColors(button, group);

                cell.Add(button);
                m_GroupGrid.Add(cell);
            }
        }

        /// <summary>
        /// Approximates the IMGUI tint (GUI.backgroundColor / GUI.contentColor). White means "no tint",
        /// so the default button look is kept in that case.
        /// </summary>
        private static void ApplyGroupColors(Button button, AssetLauncherGroup group)
        {
            var background = group.BackgroundColor;
            if (background != Color.white)
            {
                var baseColor = EditorGUIUtility.isProSkin
                    ? new Color(0.345f, 0.345f, 0.345f)
                    : new Color(0.894f, 0.894f, 0.894f);

                button.style.backgroundColor = new Color(
                    baseColor.r * background.r,
                    baseColor.g * background.g,
                    baseColor.b * background.b,
                    1f);
            }

            var font = group.FontColor;
            if (font != Color.white)
            {
                button.style.color = font;
            }
        }

        private void EnsureGroupSelectionHeight()
        {
            const int FooterHeight = 30;
            const int RowHeight = 22;
            const int VerticalPadding = 16;

            var columnCount = Math.Max(1, m_Settings.GroupSelectionXCount);
            var rowCount = Math.Max(1, (m_GroupInstanceList.Count + columnCount - 1) / columnCount);
            var requiredHeight = rowCount * RowHeight + FooterHeight + VerticalPadding;

            m_Settings.GroupSelectionHeight = Math.Max(64, requiredHeight);
        }

        // ------------------------------------------------------------------ groups

        private void AddGroup()
        {
            for (var index = 0;; ++index)
            {
                if (m_Settings.GroupIdList.Any(x => x == index))
                {
                    continue;
                }

                // Group index
                var newGroupIndex = m_GroupInstanceList
                    .Select(static x => x.GroupName)
                    .Select(static x => Regex.Replace(x, "group(\\d+)", "$1"))
                    .Select(static x => int.TryParse(x, out var v) ? v : 0)
                    .DefaultIfEmpty()
                    .Max() + 1;

                // Group id list
                m_Settings.GroupIdList.Add(index);

                // Group instance list
                var group = new AssetLauncherGroup
                {
                    Id = index,
                    GroupName = $"group{newGroupIndex}",
                };
                WireGroup(group);
                m_GroupInstanceList.Add(group);

                // Refresh
                SaveGroup(group);
                SelectGroup(m_GroupInstanceList.Count - 1);

                EnsureGroupSelectionHeight();
                SaveSettings();

                if (m_LayoutRoot != null)
                {
                    BuildLayout();
                }

                return;
            }
        }

        private void RemoveGroup(int index)
        {
            if (index < 0 || index >= m_GroupInstanceList.Count)
            {
                return;
            }

            var id = m_GroupInstanceList[index].Id;

            // Group id list
            m_Settings.GroupIdList.Remove(id);

            // Group instance list
            m_GroupInstanceList.RemoveAt(index);

            // Refresh
            File.Delete(GetGroupDataPath(id));
            SelectGroup(m_Settings.SelectGroupIndex - 1);

            EnsureGroupSelectionHeight();
            SaveSettings();

            if (m_LayoutRoot != null)
            {
                BuildLayout();
            }
        }

        private void SelectGroup(int index)
        {
            m_Settings.SelectGroupIndex = m_GroupInstanceList.Count > 0
                ? Math.Clamp(index, 0, m_GroupInstanceList.Count - 1)
                : 0;

            CurrentGroup?.RefreshEditor();

            BindCurrentGroup();
            RefreshGroupButtons();
        }

        // ------------------------------------------------------------------ persistence

        private static T LoadJson<T>(string path) where T : new()
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                // ignored
            }

            return new T();
        }

        private static void SaveJson<T>(string path, T target)
        {
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(target));
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void SaveSettings()
        {
            SaveJson(m_SettingsPath, m_Settings);
        }

        private void SaveGroup(AssetLauncherGroup group)
        {
            SaveJson(GetGroupDataPath(group.Id), group);
        }

        private void OnGroupModified(AssetLauncherGroup group)
        {
            SaveGroup(group);
        }

        private void OnGroupModifiedName(AssetLauncherGroup group)
        {
            SaveGroup(group);
            RefreshGroupButtons();
        }

        // ------------------------------------------------------------------ shortcuts

        private void ProcessShortcutKey()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (!keyboard.ctrlKey.isPressed)
            {
                return;
            }

            var count = m_GroupInstanceList.Count;

            for (var index = 0; index < count; ++index)
            {
                if (index == m_Settings.SelectGroupIndex)
                {
                    continue;
                }

                var group = m_GroupInstanceList[index];

                if (group.ShortcutKey == AssetLauncherShortcutKey.None)
                {
                    continue;
                }

                if (keyboard[(Key)group.ShortcutKey].isPressed)
                {
                    SelectGroup(index);
                    SaveSettings();
                    Repaint();
                    return;
                }
            }
#endif
        }
    }
}
