#nullable enable

using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace AssetLauncher
{
    /// <summary>
    /// "Item Selection" dropdown plus the Editor of the current item.
    /// The Editor body itself stays IMGUI (inside an IMGUIContainer) because asset inspectors are IMGUI.
    /// </summary>
    internal sealed class GroupInspectorView : VisualElement
    {
        private readonly AssetLauncherGroup m_Group;
        private readonly Button m_SelectionButton;
        private readonly Button m_TimelineButton;
        private readonly IMGUIContainer m_EditorContainer;

        private Vector2 m_ScrollPosition;

        public GroupInspectorView(AssetLauncherGroup group)
        {
            m_Group = group;

            AddToClassList(AssetLauncherStyles.Inspector);

            var selectionRow = new VisualElement();
            selectionRow.AddToClassList(AssetLauncherStyles.InspectorSelectionRow);
            Add(selectionRow);

            var selectionLabel = new Label("Item Selection");
            selectionLabel.AddToClassList(AssetLauncherStyles.InspectorSelectionLabel);
            selectionRow.Add(selectionLabel);

            m_SelectionButton = new Button(ShowSelectionDropdown);
            m_SelectionButton.AddToClassList(AssetLauncherStyles.PopupButton);
            // Reuse the editor's popup look.
            m_SelectionButton.AddToClassList("unity-base-popup-field__input");
            selectionRow.Add(m_SelectionButton);

            var separator = new VisualElement();
            separator.AddToClassList(AssetLauncherStyles.Separator);
            Add(separator);

            m_TimelineButton = new Button(OpenTimelineEditor) { text = "Open Timeline Editor" };
            m_TimelineButton.AddToClassList(AssetLauncherStyles.InspectorTimelineButton);
            Add(m_TimelineButton);

            m_EditorContainer = new IMGUIContainer(DrawEditor);
            m_EditorContainer.AddToClassList(AssetLauncherStyles.InspectorEditor);
            Add(m_EditorContainer);

            RegisterCallback<DetachFromPanelEvent>(_ => m_Group.OnSelectionChanged -= OnSelectionChanged);
            m_Group.OnSelectionChanged += OnSelectionChanged;

            Refresh();
        }

        public void Refresh()
        {
            m_SelectionButton.text = m_Group.CurrentItem?.NameWithComment ?? string.Empty;
            m_TimelineButton.style.display =
                m_Group.CurrentAsset is TimelineAsset ? DisplayStyle.Flex : DisplayStyle.None;
            m_ScrollPosition = Vector2.zero;
            m_EditorContainer.MarkDirtyRepaint();
        }

        private void OnSelectionChanged(AssetLauncherGroup _) => Refresh();

        private void ShowSelectionDropdown()
        {
            var items = m_Group.Items;
            var menu = new GenericDropdownMenu();

            menu.AddItem("(None)", m_Group.SelectIndex == AssetLauncherGroup.kInvalidIndex,
                () => m_Group.SelectItem(AssetLauncherGroup.kInvalidIndex));

            for (var index = 0; index < items.Count; ++index)
            {
                var capturedIndex = index;
                var name = items[index].NameWithComment;

                menu.AddItem(string.IsNullOrEmpty(name) ? "(Missing)" : name, index == m_Group.SelectIndex,
                    () => m_Group.SelectItem(capturedIndex));
            }

            menu.DropDown(m_SelectionButton.worldBound, m_SelectionButton, anchored: true);
        }

        private void OpenTimelineEditor()
        {
            if (m_Group.CurrentAsset is TimelineAsset timelineAsset)
            {
                AssetLauncherGroup.OpenTimelineEditor(timelineAsset);
            }
        }

        private void DrawEditor()
        {
            var editor = m_Group.Shared.Editor;

            if (editor == null || editor.target == null || m_Group.CurrentItem == null)
            {
                return;
            }

            using var scroll = new GUILayout.ScrollViewScope(m_ScrollPosition);
            m_ScrollPosition = scroll.scrollPosition;

            using var _ = new EditorGUILayout.VerticalScope();

            var hierarchyMode = EditorGUIUtility.hierarchyMode;
            EditorGUIUtility.hierarchyMode = false;
            {
                if (editor is MaterialEditor)
                {
                    editor.DrawHeader();
                }

                editor.OnInspectorGUI();
            }
            EditorGUIUtility.hierarchyMode = hierarchyMode;
        }
    }
}
