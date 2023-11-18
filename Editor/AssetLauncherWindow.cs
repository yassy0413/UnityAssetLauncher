using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace AssetLauncher
{
    public sealed class AssetLauncherWindow : EditorWindow
    {
        private List<AssetLauncherGroup> m_GroupInstanceList;
        private string[] m_GroupNameList = Array.Empty<string>();
        private Vector2 m_ScrollPosition;
        private GUIContent m_GuiContentPlus;
        private GUIContent m_GuiContentMinus;

        private string m_GroupPath;
        private string m_SettingsPath;
        private Settings m_Settings;

        private static AssetLauncherWindow Instance { get; set; }

        [Serializable]
        private sealed class Settings
        {
            public List<int> GroupIdList = new();
            public int SelectGroupIndex;
            public int xCount = 4;
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

        private void Awake()
        {
            m_GuiContentPlus = new GUIContent(EditorGUIUtility.IconContent("Toolbar Plus"));
            m_GuiContentMinus = new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus"));

            var dataPath = Path.Join(Application.persistentDataPath, "AssetLauncher");
            m_GroupPath = $"{dataPath}/group";

            if (!Directory.Exists(m_GroupPath))
            {
                Directory.CreateDirectory(m_GroupPath);
            }

            m_SettingsPath = $"{dataPath}/settings.json";
            m_Settings = LoadJson<Settings>(m_SettingsPath);

            m_GroupInstanceList = m_Settings.GroupIdList
                .Select(x =>
                {
                    var path = GetGroupDataPath(x);
                    return LoadJson<AssetLauncherGroup>(path);
                })
                .ToList();
            
            UpdateGroupNameList();
            SelectGroup(m_Settings.SelectGroupIndex);
        }

        private void OnEnable()
        {
            foreach (var group in m_GroupInstanceList)
            {
                group.OnModified = SaveGroup;
                group.OnModifiedName = ModifiedGroupName;
            }
        }

        private string GetGroupDataPath(int id) => $"{m_GroupPath}/group_{id}.json";

        private void OnGUI()
        {
            var xCount = EditorGUILayout.IntField("xCount", m_Settings.xCount, GUILayout.Width(200));
            if (xCount > 0)
            {
                m_Settings.xCount = xCount;
            }

            using (var scroll = new GUILayout.ScrollViewScope(m_ScrollPosition, EditorStyles.helpBox, GUILayout.Height(EditorGUIUtility.singleLineHeight * 3)))
            {
                m_ScrollPosition = scroll.scrollPosition;

                var index = GUILayout.SelectionGrid(m_Settings.SelectGroupIndex, m_GroupNameList, m_Settings.xCount);
                if (index != m_Settings.SelectGroupIndex)
                {
                    SelectGroup(index);
                    SaveSettings();
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(m_GuiContentPlus, GUILayout.ExpandWidth(false)))
                {
                    AddGroup();
                }

                if (GUILayout.Button(m_GuiContentMinus, GUILayout.ExpandWidth(false)))
                {
                    RemoveGroup(m_Settings.SelectGroupIndex);
                }
            }

            if (m_GroupInstanceList.Count > 0)
            {
                m_GroupInstanceList[m_Settings.SelectGroupIndex].OnInspectorGUI();
            }
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
                var newGroupIndex = m_GroupNameList
                    .Select(x => Regex.Replace(x, "group(\\d+)", "$1"))
                    .Select(x => int.TryParse(x, out var v) ? v : 0)
                    .DefaultIfEmpty()
                    .Max() + 1;

                // Group id list
                m_Settings.GroupIdList.Add(index);
                SaveSettings();

                // Group instance list
                var group = new AssetLauncherGroup
                {
                    Id = index,
                    GroupName = $"group{newGroupIndex}",
                    OnModified = SaveGroup,
                    OnModifiedName = ModifiedGroupName
                };
                m_GroupInstanceList.Add(group);
                
                // Refresh
                SaveGroup(group);
                UpdateGroupNameList();
                SelectGroup(m_GroupInstanceList.Count - 1);
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
            SaveSettings();
            
            // Refresh
            File.Delete(GetGroupDataPath(id));
            UpdateGroupNameList();
            SelectGroup(m_Settings.SelectGroupIndex - 1);
        }

        private void SelectGroup(int index)
        {
            m_Settings.SelectGroupIndex = m_GroupInstanceList.Count > 0
                ? Math.Clamp(index, 0, m_GroupInstanceList.Count - 1)
                : 0;
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
            UpdateGroupNameList();
        }
        
        private void UpdateGroupNameList()
        {
            m_GroupNameList = m_GroupInstanceList.Select(x => x.GroupName).ToArray();
        }
    }
}