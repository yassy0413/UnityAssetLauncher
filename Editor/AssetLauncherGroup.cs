#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace AssetLauncher
{
    /// <summary>
    /// A launcher group: a named, colored list of target assets plus the currently selected item.
    /// This class holds data and behaviour only; drawing is done by the views in Editor/UI.
    /// </summary>
    [Serializable]
    internal sealed class AssetLauncherGroup
    {
        public const int kInvalidIndex = -1;

        [SerializeField]
        private int m_Id;

        [SerializeField]
        private string m_GroupName = string.Empty;

#if ENABLE_INPUT_SYSTEM
        [SerializeField]
        private AssetLauncherShortcutKey m_ShortcutKey = AssetLauncherShortcutKey.None;
#endif

        [SerializeField]
        private List<AssetLauncherItem> m_ItemList = new();

        // Kept for JSON compatibility with data written by 1.0.x. No longer used by the UI.
#pragma warning disable 414
        [SerializeField]
        private bool m_FoldOut = true;
#pragma warning restore 414

        [SerializeField]
        private int m_SelectIndex;

        [SerializeField]
        private Color m_FontColor = Color.white;

        [SerializeField]
        private Color m_BackgroundColor = Color.white;

        /// <summary>Raised whenever the group's persisted data changed (the window saves it).</summary>
        public Action<AssetLauncherGroup> OnModified { get; set; } = _ => { };

        /// <summary>Raised when something shown on the group button changed (name, colors, shortcut key).</summary>
        public Action<AssetLauncherGroup> OnModifiedName { get; set; } = _ => { };

        /// <summary>Raised when the current item changed, so views can refresh highlight and inspector.</summary>
        public Action<AssetLauncherGroup> OnSelectionChanged { get; set; } = _ => { };

        /// <summary>Raised when the item list changed (add / remove / reorder).</summary>
        public Action<AssetLauncherGroup> OnItemsChanged { get; set; } = _ => { };

        public AssetLauncherWindow.Shared Shared { get; set; } = null!;

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

        public Color FontColor
        {
            get => m_FontColor;
            set => m_FontColor = value;
        }

        public Color BackgroundColor
        {
            get => m_BackgroundColor;
            set => m_BackgroundColor = value;
        }

#if ENABLE_INPUT_SYSTEM
        public AssetLauncherShortcutKey ShortcutKey
        {
            get => m_ShortcutKey;
            set => m_ShortcutKey = value;
        }
#endif

        /// <summary>The live item list. Views may use it as a ListView items source; mutate it through this class.</summary>
        public List<AssetLauncherItem> Items => m_ItemList;

        public int SelectIndex => m_SelectIndex;

        public AssetLauncherItem? CurrentItem =>
            m_SelectIndex >= 0 && m_SelectIndex < m_ItemList.Count
                ? m_ItemList[m_SelectIndex]
                : null;

        public Object? CurrentAsset => CurrentItem?.Asset;

        // ------------------------------------------------------------------ items

        public void AddAssetPaths(IEnumerable<string> paths) =>
            InsertAssetPaths(m_ItemList.Count, paths);

        /// <summary>Insert assets before <paramref name="index"/> (0 = top, Count = append).</summary>
        public void InsertAssetPaths(int index, IEnumerable<string> paths)
        {
            var newItems = paths
                .Where(static x => !string.IsNullOrEmpty(x))
                .Select(AssetLauncherItem.FromAssetPath)
                .Where(static x => x != null)
                .Select(static x => x!)
                .ToList();

            if (newItems.Count <= 0)
            {
                return;
            }

            index = Math.Clamp(index, 0, m_ItemList.Count);
            m_ItemList.InsertRange(index, newItems);

            if (m_SelectIndex >= index)
            {
                m_SelectIndex += newItems.Count;
            }

            OnModified.Invoke(this);
            OnItemsChanged.Invoke(this);
        }

        public void RemoveItems(IEnumerable<int> indices)
        {
            var targets = indices
                .Where(x => x >= 0 && x < m_ItemList.Count)
                .Distinct()
                .OrderByDescending(static x => x)
                .ToList();

            if (targets.Count <= 0)
            {
                return;
            }

            var removedCurrent = targets.Contains(m_SelectIndex);
            var shift = targets.Count(x => x < m_SelectIndex);
            var minRemoved = targets[targets.Count - 1];

            foreach (var index in targets)
            {
                m_ItemList.RemoveAt(index);
            }

            int newIndex;
            if (m_ItemList.Count <= 0)
            {
                newIndex = kInvalidIndex;
            }
            else if (removedCurrent)
            {
                // Same rule as the previous ReorderableList implementation: fall back to the item above.
                newIndex = minRemoved - 1;
            }
            else
            {
                newIndex = m_SelectIndex - shift;
            }

            OnItemsChanged.Invoke(this);
            SelectItem(newIndex, forceRefresh: true);
        }

        /// <summary>Called after a ListView reorder already moved the element inside <see cref="Items"/>.</summary>
        public void NotifyItemMoved(int from, int to)
        {
            if (from == to)
            {
                return;
            }

            if (m_SelectIndex == from)
            {
                m_SelectIndex = to;
            }
            else if (from < m_SelectIndex && to >= m_SelectIndex)
            {
                --m_SelectIndex;
            }
            else if (from > m_SelectIndex && to <= m_SelectIndex)
            {
                ++m_SelectIndex;
            }

            OnModified.Invoke(this);
            // The ListView already reflects the move; only the "current" highlight may have changed.
            OnSelectionChanged.Invoke(this);
        }

        public void SetComment(int index, string comment)
        {
            if (index < 0 || index >= m_ItemList.Count)
            {
                return;
            }

            if (m_ItemList[index].Comment == comment)
            {
                return;
            }

            m_ItemList[index].Comment = comment;
            OnModified.Invoke(this);
        }

        // ------------------------------------------------------------------ selection

        public void SelectItem(int index, bool forceRefresh = false)
        {
            if (index < 0 || index >= m_ItemList.Count)
            {
                index = kInvalidIndex;
            }

            if (!forceRefresh && m_SelectIndex == index)
            {
                return;
            }

            CurrentItem?.ReleaseAsset();
            m_SelectIndex = index;
            OnModified.Invoke(this);

            RefreshEditor();
            OnSelectionChanged.Invoke(this);
        }

        public void DeselectItem() => SelectItem(kInvalidIndex);

        /// <summary>Clamp the persisted selection into range (used right after loading).</summary>
        public void EnsureSelection()
        {
            if (m_ItemList.Count <= 0)
            {
                m_SelectIndex = kInvalidIndex;
                return;
            }

            m_SelectIndex = Math.Clamp(m_SelectIndex, kInvalidIndex, m_ItemList.Count - 1);
        }

        public void RefreshEditor()
        {
            var currentItem = CurrentItem;
            var currentAsset = currentItem?.Asset;
            if (currentItem == null || currentAsset == null)
            {
                Shared.ClearEditor();
                return;
            }

            var path = AssetDatabase.GetAssetPath(currentAsset);
            var requiredImporterEditor = false;

            switch (currentAsset)
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
                        // We can not create Prefab Importer Editor.
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
                Shared.SetEditor(currentAsset);
            }
        }

        public static void OpenTimelineEditor(TimelineAsset timelineAsset)
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

        private static Object? GetFirstContainsAsset(string path)
        {
            var dir = Directory
                .EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(dir))
            {
                return AssetDatabase.LoadAssetAtPath<Object>(dir);
            }

            var meta = Directory
                .EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(static x => !x.EndsWith(".meta", StringComparison.InvariantCulture));

            return string.IsNullOrEmpty(meta) ? null : AssetDatabase.LoadAssetAtPath<Object>(meta);
        }
    }
}
