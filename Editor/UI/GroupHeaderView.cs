#nullable enable

using UnityEditor.UIElements;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AssetLauncher
{
    /// <summary>Group name, button colors and (optionally) the group shortcut key.</summary>
    internal sealed class GroupHeaderView : VisualElement
    {
        public GroupHeaderView(AssetLauncherGroup group)
        {
            AddToClassList(AssetLauncherStyles.GroupHeader);

            var row = new VisualElement();
            row.AddToClassList(AssetLauncherStyles.GroupHeaderRow);
            Add(row);

            var nameField = new TextField("Group Name") { value = group.GroupName, isDelayed = false };
            nameField.AddToClassList(AssetLauncherStyles.GroupHeaderName);
            nameField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == group.GroupName)
                {
                    return;
                }

                group.GroupName = evt.newValue;
                group.OnModifiedName.Invoke(group);
            });
            row.Add(nameField);

            var fontColorField = new ColorField { value = group.FontColor, showEyeDropper = true };
            fontColorField.AddToClassList(AssetLauncherStyles.GroupHeaderColor);
            fontColorField.tooltip = "Font color of the group button";
            fontColorField.RegisterValueChangedCallback(evt =>
            {
                group.FontColor = evt.newValue;
                group.OnModifiedName.Invoke(group);
            });
            row.Add(fontColorField);

            var backgroundColorField = new ColorField { value = group.BackgroundColor, showEyeDropper = true };
            backgroundColorField.AddToClassList(AssetLauncherStyles.GroupHeaderColor);
            backgroundColorField.tooltip = "Background color of the group button";
            backgroundColorField.RegisterValueChangedCallback(evt =>
            {
                group.BackgroundColor = evt.newValue;
                group.OnModifiedName.Invoke(group);
            });
            row.Add(backgroundColorField);

#if ENABLE_INPUT_SYSTEM
            var shortcutField = new EnumField("Shortcut Key (Ctrl+)", group.ShortcutKey);
            shortcutField.SetEnabled(Keyboard.current != null);
            shortcutField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is not AssetLauncherShortcutKey key || key == group.ShortcutKey)
                {
                    return;
                }

                group.ShortcutKey = key;
                group.OnModifiedName.Invoke(group);
            });
            Add(shortcutField);
#endif
        }
    }
}
