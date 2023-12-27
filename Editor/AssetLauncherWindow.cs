using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace AssetLauncher
{
    public sealed class AssetLauncherWindow : EditorWindow
    {
        private List<AssetLauncherGroup> m_GroupInstanceList;
        private Vector2 m_ScrollPosition;
        private GUIContent m_GuiContentPlus;
        private GUIContent m_GuiContentMinus;
        private GUIStyle m_GuiStyleGroup;
        private GUIStyle m_GuiStyleGroupBold;
        private IMGUIContainer m_GroupSelectorPane;
        private IMGUIContainer m_GroupInspectorPane;
        private IMGUIContainer m_GroupTargetListPane;

        private string m_GroupPath;
        private string m_SettingsPath;
        private Settings m_Settings;
        private readonly Shared m_Shared = new();
        
        private static AssetLauncherWindow Instance { get; set; }

        private string GetGroupDataPath(int id) => $"{m_GroupPath}/group_{id}.json";

        public enum ButtonTextAnchor
        {
            Left,
            Center,
            Right,
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
            public int ItemCommentWidth = 180;
            public bool EnabledItemComment;
            public ButtonTextAnchor ButtonTextAnchor = ButtonTextAnchor.Center;
            public Layout Layout = Layout.Vertical;
        }
        
        public sealed class Shared
        {
            private Editor m_Editor;
            public Editor Editor => m_Editor;

            public void SetEditor(Object targetObject) =>
                Editor.CreateCachedEditor(targetObject, null, ref m_Editor);

            public void SetImporterEditor(string path) =>
                Editor.CreateCachedEditor(AssetImporter.GetAtPath(path), null, ref m_Editor);
        }

        public enum Layout
        {
            Vertical,
            Horizontal,
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
                group.OnModified = SaveGroup;
                group.OnModifiedName = ModifiedGroupName;
                group.Settings = m_Settings;
                group.Shared = m_Shared;
            }
        }

        private void Update()
        {
            if (m_GroupInstanceList?.Count <= 0)
            {
                return;
            }

            ProcessShortcutKey();
        }

        private void Setup()
        {
            if (m_GroupInstanceList != null)
            {
                return;
            }

            var dataPath = Path.Join(Application.persistentDataPath, "AssetLauncher");
            m_GroupPath = $"{dataPath}/group";

            if (!Directory.Exists(m_GroupPath))
            {
                Directory.CreateDirectory(m_GroupPath);
            }

            m_SettingsPath = $"{dataPath}/settings.json";
            m_Settings = LoadJson<Settings>(m_SettingsPath);

            if (m_Settings.GroupIdList.Count <= 0)
            {
                m_GroupInstanceList = new List<AssetLauncherGroup>();
                AddGroup();
            }
            else
            {
                m_GroupInstanceList = m_Settings.GroupIdList
                    .Select(x =>
                    {
                        var path = GetGroupDataPath(x);
                        var group = LoadJson<AssetLauncherGroup>(path);
                        group.Shared = m_Shared;
                        return group;
                    })
                    .ToList();

                SelectGroup(m_Settings.SelectGroupIndex);
            }

            if (m_Settings.GroupSelectionHeight < 64)
            {
                m_Settings.GroupSelectionHeight = 64;
            }
        }

        private AssetLauncherGroup CurrentGroup =>
            m_Settings.SelectGroupIndex >= 0 && m_Settings.SelectGroupIndex < m_GroupInstanceList.Count
                ? m_GroupInstanceList[m_Settings.SelectGroupIndex]
                : null;

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            rootVisualElement.Add(new IMGUIContainer(DrawHeader));

            VisualElement pane;
            if (m_Settings.Layout == Layout.Vertical)
            {
                pane = new TwoPaneSplitView(0, m_Settings.GroupSelectionHeight, TwoPaneSplitViewOrientation.Vertical);
                pane.Add(m_GroupSelectorPane = new IMGUIContainer(DrawGroupSelector));
                pane.Add(m_GroupInspectorPane = new IMGUIContainer(DrawInspector));
            }
            else
            {
                var paneV = new TwoPaneSplitView(0, m_Settings.GroupSelectionHeight, TwoPaneSplitViewOrientation.Vertical);
                paneV.Add(m_GroupSelectorPane = new IMGUIContainer(DrawGroupSelector));
                paneV.Add(m_GroupTargetListPane = new IMGUIContainer(DrawTargetList));

                pane = new TwoPaneSplitView(0, m_Settings.GroupSelectionWidth, TwoPaneSplitViewOrientation.Horizontal);
                pane.Add(paneV);
                pane.Add(m_GroupInspectorPane = new IMGUIContainer(DrawInspector));
            }

            rootVisualElement.Add(pane);
        }

        private void DrawHeader()
        {
            InitializeGuiStyles();
            
            var settingsFoldout = AssetLauncherGroup.FoldOutWithMouseDown(m_Settings.FoldOut, "Settings");
            if (m_Settings.FoldOut != settingsFoldout)
            {
                m_Settings.FoldOut = settingsFoldout;
                SaveSettings();
            }
            if (settingsFoldout)
            {
                using var _ = new EditorGUI.IndentLevelScope();

                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 200;

                var xCount = EditorGUILayout.IntField("Group Selection xCount", m_Settings.GroupSelectionXCount);
                if (xCount > 0 && xCount != m_Settings.GroupSelectionXCount)
                {
                    m_Settings.GroupSelectionXCount = xCount;
                    SaveSettings();
                }

                var enabledComment = EditorGUILayout.Toggle("Enable Item Comment", m_Settings.EnabledItemComment);
                if (enabledComment != m_Settings.EnabledItemComment)
                {
                    m_Settings.EnabledItemComment = enabledComment;
                    SaveSettings();
                }

                var commentWidth = EditorGUILayout.IntField("Item Comment Width", m_Settings.ItemCommentWidth);
                if (commentWidth >= 60 && commentWidth != m_Settings.ItemCommentWidth)
                {
                    m_Settings.ItemCommentWidth = commentWidth;
                    SaveSettings();
                }

                var buttonTextAnchor = (ButtonTextAnchor)EditorGUILayout.EnumPopup("Button Text Anchor", m_Settings.ButtonTextAnchor);
                if (buttonTextAnchor != m_Settings.ButtonTextAnchor)
                {
                    m_Settings.ButtonTextAnchor = buttonTextAnchor;
                    SaveSettings();
                    ResetGuiStyles();
                }

                var layout = (Layout)EditorGUILayout.EnumPopup("Layout", m_Settings.Layout);
                if (layout != m_Settings.Layout)
                {
                    m_Settings.Layout = layout;
                    SaveSettings();
                    CreateGUI();
                }

                GUILayout.Space(16);

                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        private void DrawGroupSelector()
        {
            var contentRect = m_GroupSelectorPane.contentRect;

            if (m_Settings.GroupSelectionHeight != (int)contentRect.height)
            {
                m_Settings.GroupSelectionHeight = (int)contentRect.height;
                SaveSettings();
            }

            const float FooterHeight = 30f;

            {
                var rect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height - FooterHeight);

                using var _ = new GUILayout.AreaScope(rect);

                using var scroll = new GUILayout.ScrollViewScope(m_ScrollPosition, EditorStyles.helpBox);
                m_ScrollPosition = scroll.scrollPosition;
                
                var index = DrawGroupSelectionGrid(m_Settings.SelectGroupIndex, rect.width - 24);
                if (index != m_Settings.SelectGroupIndex)
                {
                    SelectGroup(index);
                    SaveSettings();
                }
            }

            {
                var rect = new Rect(contentRect.x, contentRect.height - FooterHeight, contentRect.width, FooterHeight);

                using var _ = new GUILayout.AreaScope(rect);
                using var __ = new GUILayout.HorizontalScope();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(m_GuiContentPlus, GUILayout.ExpandWidth(false)))
                {
                    AddGroup();
                }

                if (GUILayout.Button(m_GuiContentMinus, GUILayout.ExpandWidth(false)))
                {
                    RemoveGroup(m_Settings.SelectGroupIndex);
                }

                GUILayout.Space(8);
            }
        }

        private void DrawTargetList()
        {
            var contentRect = m_GroupTargetListPane.contentRect;

            if (m_Settings.GroupSelectionWidth != (int)contentRect.width)
            {
                m_Settings.GroupSelectionWidth = (int)contentRect.width;
                SaveSettings();
            }

            CurrentGroup?.DrawHeader();
        }

        private void DrawInspector()
        {
            if (m_GroupInstanceList.Count <= 0)
            {
                return;
            }

            using var _ = new GUILayout.AreaScope(m_GroupInspectorPane.contentRect);

            var currentGroup = CurrentGroup;

            if (m_Settings.Layout == Layout.Vertical)
            {
                currentGroup?.DrawHeader();
            }

            currentGroup?.DrawBody();
        }

        private int DrawGroupSelectionGrid(int selected, float width)
        {
            var guiWidth = GUILayout.Width(width / m_Settings.GroupSelectionXCount);
            var contentColor = GUI.contentColor;
            var backgroundColor = GUI.backgroundColor;

            GUILayout.BeginHorizontal();
            var groupCount = m_GroupInstanceList.Count;
            for (var index = 0; index < groupCount; ++index)
            {
                if (index > 0 && index % m_Settings.GroupSelectionXCount == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }

                var group = m_GroupInstanceList[index];
                
                GUI.contentColor = group.FontColor;
                GUI.backgroundColor = group.BackgroundColor;

                if (GUILayout.Button(group.GroupName, selected == index? m_GuiStyleGroupBold : m_GuiStyleGroup, guiWidth))
                {
                    selected = index;
                }
            }
            GUILayout.EndHorizontal();
            
            GUI.contentColor = contentColor;
            GUI.backgroundColor = backgroundColor;
            return selected;
        }

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
                    .Select(x => x.GroupName)
                    .Select(x => Regex.Replace(x, "group(\\d+)", "$1"))
                    .Select(x => int.TryParse(x, out var v) ? v : 0)
                    .DefaultIfEmpty()
                    .Max() + 1;

                // Group id list
                m_Settings.GroupIdList.Add(index);

                // Group instance list
                var group = new AssetLauncherGroup
                {
                    Id = index,
                    GroupName = $"group{newGroupIndex}",
                    OnModified = SaveGroup,
                    OnModifiedName = ModifiedGroupName,
                    Settings = m_Settings,
                    Shared = m_Shared,
                };
                m_GroupInstanceList.Add(group);
                
                // Refresh
                SaveGroup(group);
                SelectGroup(m_GroupInstanceList.Count - 1);

                SaveSettings();
                return;
            }
        }

        private void RemoveGroup(int index)
        {
            if (index >= m_GroupInstanceList.Count)
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

            SaveSettings();
        }

        private void SelectGroup(int index)
        {
            m_Settings.SelectGroupIndex = m_GroupInstanceList.Count > 0
                ? Math.Clamp(index, 0, m_GroupInstanceList.Count - 1)
                : 0;
            
            GUI.FocusControl("");
            CurrentGroup?.RefreshEditor();
        }

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

        private void ModifiedGroupName(AssetLauncherGroup group)
        {
            SaveGroup(group);
        }

        private void InitializeGuiStyles()
        {
            if (m_GuiContentPlus != null)
            {
                return;
            }

            var buttonTextAnchor = m_Settings.ButtonTextAnchor switch
            {
                ButtonTextAnchor.Left => TextAnchor.MiddleLeft,
                ButtonTextAnchor.Center => TextAnchor.MiddleCenter,
                _ => TextAnchor.MiddleRight
            };

            m_GuiContentPlus = new GUIContent(EditorGUIUtility.IconContent("Toolbar Plus"));
            m_GuiContentMinus = new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus"));
            m_GuiStyleGroup = new GUIStyle(GUI.skin.button)
            {
                alignment = buttonTextAnchor
            };
            m_GuiStyleGroupBold = new GUIStyle(GUI.skin.button)
            {
                alignment = buttonTextAnchor,
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
        }

        private void ResetGuiStyles()
        {
            m_GuiContentPlus = null;
        }

        private void ProcessShortcutKey()
        {
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
        }
    }
}