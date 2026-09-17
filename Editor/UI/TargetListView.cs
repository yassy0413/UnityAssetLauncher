#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetLauncher
{
    /// <summary>
    /// Target asset list of a group, drawn as a flat "icon + name" list.
    ///
    ///  - Click a row     : select the asset in the Project window (ping it) and show its Editor in the inspector pane.
    ///  - Double click   : open the asset; frame scene objects like the Hierarchy.
    ///  - Right click     : context menu with "Remove".
    ///  - Drag rows       : reorder.
    ///  - Drop assets     : anywhere on this element; they are inserted at the highlighted position
    ///                      (between rows, or at the end when dropped on the empty area below the rows).
    ///  - Delete/Backspace: remove the selected rows (when the list has focus).
    /// </summary>
    internal sealed class TargetListView : VisualElement
    {
        public const int kMinFontSize = 8;
        public const int kMaxFontSize = 40;
        private const int kRowPadding = 8;
        private const int kMinIconSize = 16;
        private const int kMaxIconSize = 32;

        private readonly AssetLauncherGroup m_Group;
        private readonly AssetLauncherWindow.Settings m_Settings;
        private readonly ListView m_ListView;
        private readonly Label m_Placeholder;
        private readonly VisualElement m_DropMarker;
        private ScrollView? m_ScrollView;
        private int m_DropInsertIndex = -1;
        private readonly List<VisualElement> m_BoundRows = new();
        private IVisualElementScheduledItem? m_RefreshTask;

        private static Texture2D? s_MissingIcon;

        public TargetListView(AssetLauncherGroup group, AssetLauncherWindow.Settings settings)
        {
            m_Group = group;
            m_Settings = settings;

            AddToClassList(AssetLauncherStyles.TargetList);

            m_ListView = new ListView
            {
                itemsSource = group.Items,
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                reorderable = true,
                reorderMode = ListViewReorderMode.Simple,
                selectionType = SelectionType.Multiple,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                showBorder = false,
                showBoundCollectionSize = false,
                makeItem = MakeRow,
                bindItem = BindRow,
                unbindItem = UnbindRow,
            };
            m_ListView.itemIndexChanged += OnItemIndexChanged;
            m_ListView.RegisterCallback<KeyDownEvent>(OnKeyDown);
            Add(m_ListView);

            m_Placeholder = new Label("Drop assets here")
            {
                pickingMode = PickingMode.Ignore,
            };
            m_Placeholder.AddToClassList(AssetLauncherStyles.TargetListPlaceholder);
            Add(m_Placeholder);

            m_DropMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            m_DropMarker.AddToClassList(AssetLauncherStyles.TargetListDropMarker);
            m_DropMarker.style.display = DisplayStyle.None;
            Add(m_DropMarker);

            // Drop handling on the whole element, trickle-down so the ListView can not swallow external drags.
            RegisterCallback<DragEnterEvent>(OnDragEnter, TrickleDown.TrickleDown);
            RegisterCallback<DragLeaveEvent>(OnDragLeave, TrickleDown.TrickleDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            RegisterCallback<DragExitedEvent>(OnDragExited, TrickleDown.TrickleDown);

            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                m_Group.OnItemsChanged -= OnItemsChanged;
                m_Group.OnSelectionChanged -= OnSelectionChanged;
            });

            m_Group.OnItemsChanged += OnItemsChanged;
            m_Group.OnSelectionChanged += OnSelectionChanged;

            Rebuild();
        }

        private int FontSize => Mathf.Clamp(m_Settings.ItemFontSize, kMinFontSize, kMaxFontSize);
        private int RowHeight => FontSize + kRowPadding;
        private int IconSize => Mathf.Clamp(FontSize + 4, kMinIconSize, kMaxIconSize);

        /// <summary>Full rebuild; call when the item count, font size or the comment column settings changed.</summary>
        public void Rebuild()
        {
            style.fontSize = FontSize;
            m_ListView.fixedItemHeight = RowHeight;
            m_BoundRows.Clear();
            m_ListView.Rebuild();
            m_ScrollView = null;
            m_Placeholder.style.display = m_Group.Items.Count <= 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ------------------------------------------------------------------ rows

        private sealed class RowElements
        {
            public int Index = AssetLauncherGroup.kInvalidIndex;
            public Image Icon = null!;
            public Label Name = null!;
            public TextField Comment = null!;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList(AssetLauncherStyles.Row);
            row.style.height = RowHeight;

            var data = new RowElements();
            row.userData = data;

            data.Icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            data.Icon.AddToClassList(AssetLauncherStyles.RowIcon);
            data.Icon.style.width = IconSize;
            data.Icon.style.height = IconSize;
            data.Icon.style.minWidth = IconSize;
            row.Add(data.Icon);

            data.Name = new Label();
            data.Name.AddToClassList(AssetLauncherStyles.RowName);
            row.Add(data.Name);

            // Icon and name share one click target: select the asset in the Project window and show its Editor.
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || data.Index < 0 || data.Index >= m_Group.Items.Count)
                {
                    return;
                }

                if (evt.target is VisualElement target &&
                    (target is TextField || target.GetFirstAncestorOfType<TextField>() != null))
                {
                    return;
                }

                var asset = m_Group.Items[data.Index].Asset;
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }

                m_Group.SelectItem(data.Index);
                if (evt.clickCount == 2 && asset != null)
                {
                    if (!EditorUtility.IsPersistent(asset) && (asset is GameObject || asset is Component))
                    {
                        SceneView.FrameLastActiveSceneView();
                    }
                    else
                    {
                        AssetDatabase.OpenAsset(asset);
                    }
                    evt.StopPropagation();
                }
            });

            data.Comment = new TextField { isDelayed = true };
            data.Comment.AddToClassList(AssetLauncherStyles.RowComment);
            data.Comment.RegisterValueChangedCallback(evt =>
            {
                if (data.Index >= 0)
                {
                    m_Group.SetComment(data.Index, evt.newValue);
                }
            });
            // Do not let text editing start a row drag / selection.
            data.Comment.RegisterCallback<PointerDownEvent>(static evt => evt.StopPropagation());
            data.Comment.RegisterCallback<PointerUpEvent>(static evt => evt.StopPropagation());
            row.Add(data.Comment);

            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (data.Index < 0)
                {
                    return;
                }

                var index = data.Index;
                evt.menu.AppendAction("Remove", _ => RemoveFromContextMenu(index));
            }));

            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (row.userData is not RowElements data)
            {
                return;
            }

            data.Index = index;

            var item = index >= 0 && index < m_Group.Items.Count ? m_Group.Items[index] : null;
            var path = item?.AssetPath ?? string.Empty;
            var exists = !string.IsNullOrEmpty(path);
            data.Icon.image = exists ? AssetDatabase.GetCachedIcon(path) : MissingIcon;
            data.Name.text = exists ? item!.Name : "(Missing)";
            data.Name.tooltip = path;
            data.Name.EnableInClassList(AssetLauncherStyles.RowNameMissing, !exists);
            if (!m_BoundRows.Contains(row))
            {
                m_BoundRows.Add(row);
            }

            row.EnableInClassList(AssetLauncherStyles.RowCurrent, index == m_Group.SelectIndex);

            if (m_Settings.EnabledItemComment)
            {
                data.Comment.style.display = DisplayStyle.Flex;
                data.Comment.style.width = m_Settings.ItemCommentWidth;
                data.Comment.SetValueWithoutNotify(item?.Comment ?? string.Empty);
            }
            else
            {
                data.Comment.style.display = DisplayStyle.None;
            }
        }

        private void UnbindRow(VisualElement row, int index)
        {
            if (row.userData is RowElements data)
            {
                data.Index = AssetLauncherGroup.kInvalidIndex;
                data.Icon.image = null;
                m_BoundRows.Remove(row);
            }
        }

        private static Texture2D? MissingIcon =>
            s_MissingIcon ??= AssetLauncherStyles.Icon("console.warnicon.sml");

        // ------------------------------------------------------------------ events from the group

        // Deferred: these can be raised from inside ListView callbacks (e.g. after a reorder),
        // and rebuilding the list synchronously from there is not safe.
        private void OnItemsChanged(AssetLauncherGroup _)
        {
            if (m_RefreshTask == null)
            {
                m_RefreshTask = schedule.Execute(RefreshItems);
            }
            else
            {
                m_RefreshTask.ExecuteLater(0);
            }
        }

        public void RefreshItems()
        {
            m_ListView.RefreshItems();
            m_Placeholder.style.display = m_Group.Items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSelectionChanged(AssetLauncherGroup _)
        {
            foreach (var row in m_BoundRows)
            {
                if (row.userData is RowElements data)
                {
                    row.EnableInClassList(AssetLauncherStyles.RowCurrent, data.Index == m_Group.SelectIndex);
                }
            }
        }

        private void OnItemIndexChanged(int from, int to) => m_Group.NotifyItemMoved(from, to);

        // ------------------------------------------------------------------ keyboard

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
            {
                return;
            }

            // Typing inside a comment field must not delete rows.
            if (evt.target is VisualElement target &&
                (target is TextField || target.GetFirstAncestorOfType<TextField>() != null))
            {
                return;
            }

            var selected = m_ListView.selectedIndices.ToList();
            if (selected.Count <= 0)
            {
                return;
            }

            evt.StopPropagation();
            m_ListView.ClearSelection();
            m_Group.RemoveItems(selected);
        }

        // ------------------------------------------------------------------ remove

        private void RemoveFromContextMenu(int clickedIndex)
        {
            var selected = m_ListView.selectedIndices.ToList();

            var targets = selected.Contains(clickedIndex)
                ? (IEnumerable<int>)selected
                : new[] { clickedIndex };

            m_ListView.ClearSelection();
            m_Group.RemoveItems(targets);
        }

        // ------------------------------------------------------------------ drag & drop (external assets)

        private static bool HasExternalAssets() =>
            DragAndDrop.paths is { Length: > 0 };

        private void OnDragEnter(DragEnterEvent evt)
        {
            if (HasExternalAssets())
            {
                AddToClassList(AssetLauncherStyles.TargetListDropActive);
            }
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            // Leave events also fire when moving between child rows; only reset when the pointer left this element.
            if (!worldBound.Contains(evt.mousePosition))
            {
                HideDropFeedback();
            }
        }

        private void OnDragExited(DragExitedEvent evt) => HideDropFeedback();

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (!HasExternalAssets())
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            AddToClassList(AssetLauncherStyles.TargetListDropActive);
            UpdateDropMarker(evt.mousePosition);
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (!HasExternalAssets())
            {
                HideDropFeedback();
                return;
            }

            var insertIndex = m_DropInsertIndex >= 0 ? m_DropInsertIndex : ComputeInsertIndex(evt.mousePosition);
            HideDropFeedback();

            var paths = DragAndDrop.paths.ToArray();
            DragAndDrop.AcceptDrag();
            evt.StopPropagation();

            m_Group.InsertAssetPaths(insertIndex, paths);
        }

        private void HideDropFeedback()
        {
            RemoveFromClassList(AssetLauncherStyles.TargetListDropActive);
            m_DropMarker.style.display = DisplayStyle.None;
            m_DropInsertIndex = -1;
        }

        /// <summary>
        /// Top (panel y) of the first row, taking the scroll offset into account.
        /// ListView.contentContainer is null on recent Unity versions, so go through the inner ScrollView.
        /// </summary>
        private float ContentTop()
        {
            m_ScrollView ??= m_ListView.Q<ScrollView>();

            var content = m_ScrollView?.contentContainer;
            return content != null ? content.worldBound.yMin : m_ListView.worldBound.yMin;
        }

        /// <summary>Insertion index for a pointer position (panel coordinates): nearest row boundary.</summary>
        private int ComputeInsertIndex(Vector2 mousePosition)
        {
            var count = m_Group.Items.Count;
            if (count <= 0)
            {
                return 0;
            }

            var index = Mathf.RoundToInt((mousePosition.y - ContentTop()) / RowHeight);
            return Mathf.Clamp(index, 0, count);
        }

        private void UpdateDropMarker(Vector2 mousePosition)
        {
            m_DropInsertIndex = ComputeInsertIndex(mousePosition);

            if (m_Group.Items.Count <= 0)
            {
                m_DropMarker.style.display = DisplayStyle.None;
                return;
            }

            // Marker y in this element's local space, clamped to the visible part of the list.
            var markerWorldY = ContentTop() + m_DropInsertIndex * RowHeight;
            var listBounds = m_ListView.worldBound;
            markerWorldY = Mathf.Clamp(markerWorldY, listBounds.yMin, listBounds.yMax);

            m_DropMarker.style.top = markerWorldY - worldBound.yMin - 1f;
            m_DropMarker.style.display = DisplayStyle.Flex;
        }
    }
}
