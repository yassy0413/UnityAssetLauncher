#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetLauncher
{
    /// <summary>
    /// Loads the shared style sheet and exposes the USS class names used by the launcher views.
    /// </summary>
    internal static class AssetLauncherStyles
    {
        // GUID of Editor/UI/AssetLauncher.uss (see its .meta). Resolving by GUID works both when the
        // tool is installed under Assets/ and when it is installed as a package.
        private const string kStyleSheetGuid = "9b2d6f4e1a3c4e8f90b7c5d2a6e1f438";
        private const string kStyleSheetName = "AssetLauncher";

        public const string Root = "al-root";

        public const string Header = "al-header";
        public const string HeaderSettings = "al-header__settings";
        public const string IconButton = "al-icon-button";

        public const string GroupSelector = "al-group-selector";
        public const string Box = "al-box";
        public const string GroupGrid = "al-group-grid";
        public const string GroupButton = "al-group-button";
        public const string GroupButtonSelected = "al-group-button--selected";
        public const string AnchorLeft = "al-anchor-left";
        public const string AnchorCenter = "al-anchor-center";
        public const string AnchorRight = "al-anchor-right";
        public const string GroupFooter = "al-group-footer";

        public const string GroupPane = "al-group-pane";
        public const string GroupHeader = "al-group-header";
        public const string GroupHeaderRow = "al-group-header__row";
        public const string GroupHeaderName = "al-group-header__name";
        public const string GroupHeaderColor = "al-group-header__color";

        public const string TargetList = "al-target-list";
        public const string TargetListDropActive = "al-target-list--drop-active";
        public const string TargetListPlaceholder = "al-target-list__placeholder";
        public const string TargetListDropMarker = "al-target-list__drop-marker";
        public const string Row = "al-row";
        public const string RowCurrent = "al-row--current";
        public const string RowIcon = "al-row__icon";
        public const string RowName = "al-row__name";
        public const string RowNameMissing = "al-row__name--missing";
        public const string RowComment = "al-row__comment";

        public const string Inspector = "al-inspector";
        public const string InspectorSelectionRow = "al-inspector__selection-row";
        public const string InspectorSelectionLabel = "al-inspector__selection-label";
        public const string PopupButton = "al-popup-button";
        public const string Separator = "al-separator";
        public const string InspectorTimelineButton = "al-inspector__timeline-button";
        public const string InspectorEditor = "al-inspector__editor";

        private static StyleSheet? s_StyleSheet;

        public static void Apply(VisualElement root)
        {
            root.AddToClassList(Root);

            var styleSheet = Load();
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private static StyleSheet? Load()
        {
            if (s_StyleSheet != null)
            {
                return s_StyleSheet;
            }

            var path = AssetDatabase.GUIDToAssetPath(kStyleSheetGuid);

            if (string.IsNullOrEmpty(path))
            {
                foreach (var guid in AssetDatabase.FindAssets($"{kStyleSheetName} t:StyleSheet"))
                {
                    var candidate = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(candidate) == kStyleSheetName)
                    {
                        path = candidate;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[AssetLauncher] AssetLauncher.uss was not found. The window will use default styles.");
                return null;
            }

            s_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            return s_StyleSheet;
        }

        public static Texture2D? Icon(string name)
        {
            var content = EditorGUIUtility.IconContent(name);
            return content?.image as Texture2D;
        }
    }
}
