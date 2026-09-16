# Unity Asset Launcher

[日本語](README_jp.md)

Unity Asset Launcher is a simple editor window for keeping frequently used Unity assets close at hand.

Register folders, ScriptableObjects, materials, textures, Timeline assets, prefabs, scenes, or any other project asset, then select and inspect them from one compact utility window.

![Scriptable Assets](Editor/StoreDocument/ScriptableAssets.png)

![Folders](Editor/StoreDocument/Folders.png)

## Features

- Register frequently used assets in launcher groups.
- Select registered assets and display their Inspector directly inside the launcher.
- Organize assets into multiple groups.
- Rename groups and customize group button colors.
- Reorder launcher items.
- Add optional comments to launcher items.
- Drag and drop assets into a group.
- Open the Timeline Editor for registered `TimelineAsset` items.
- Quickly toggle the launcher window with shortcuts.
- Switch groups with optional group shortcut keys.
- Store settings as personal local editor data.

## Usage

Open the launcher from:

```text
Window > Asset Launcher
```

The same menu item toggles the launcher window. If the window is already open, selecting the menu item closes it.

You can also toggle the window with:

```text
Ctrl + L
Ctrl + Middle Mouse Button
```

![Item Selection](Editor/StoreDocument/ItemSelection.gif)

## Groups

Assets are organized into groups.

Use the `+` and `-` buttons in the group area to add or remove groups.

Each group can define:

- Group name
- Font color
- Background color
- Optional shortcut key
- Target asset list

When the Unity Input System is enabled, each group can also be assigned a `Ctrl + Key` shortcut for quick group switching.

## Items

Add assets to the current group by dragging them from the Project window onto the target list. A marker shows where they will be inserted: drop between two rows to insert there, or on the empty space below the rows to append.

Each row shows the asset icon and the asset name:

- Click a row to select the asset in the Project window (it is also pinged) and show its Inspector in the launcher window.
- Drag a row to reorder items.
- Right-click a row and choose `Remove`, or press `Delete` / `Backspace` while the list has focus, to delete it. When several rows are selected, all of them are removed.

For folders, the launcher pings the first contained asset so you can jump into the folder quickly.

For supported importer-backed assets such as textures, the launcher displays the importer inspector.

For `TimelineAsset` items, an `Open Timeline Editor` button is shown.

## Item comments

Item comments can be enabled from the settings foldout.

![Settings and Folders](Editor/StoreDocument/SettingsAndFolders.png)

Enable:

```text
Settings > Enable Item Comment
```

When enabled, each launcher item has a comment field. The comment is shown next to the asset name in the item selection dropdown.

This is useful when multiple assets have similar names, or when an asset needs a short note about its purpose.

## Settings

Open the `Settings` foldout at the top of the launcher window to configure:

- Group selection column count
- Item comment visibility
- Item comment width
- Item font size (target list rows)
- Group button text alignment
- Layout mode

The launcher supports two layout modes:

- `Vertical`: Group selector at the top, Inspector below.
- `Horizontal`: Group selector and target list on the left, Inspector on the right.

In the `Vertical` layout the boundary between the target list and the Inspector can be dragged.

Use the folder icon in the header to reveal the launcher data folder in Finder / Explorer.

## Shortcuts

Window toggle shortcuts:

```text
Ctrl + L
Ctrl + Middle Mouse Button
```

Group switching shortcuts are available when the Unity Input System is enabled. Assign them per group from:

```text
Group > Shortcut Key (Ctrl+)
```

## Settings storage

Launcher data is saved under Unity's `Application.persistentDataPath`:

```text
AssetLauncher/settings.json
AssetLauncher/group/group_{id}.json
```

This keeps the launcher configuration personal to each user and outside the project by default.

## Installation with UPM

You can install this package from Unity Package Manager using the Git URL:

```text
https://github.com/yassy0413/UnityAssetLauncher.git
```

![Package Manager Step 1](Editor/StoreDocument/PackageManager01.png)

![Package Manager Step 2](Editor/StoreDocument/PackageManager02.png)

## Package information

- Package name: `com.yassy.assetlauncher`
- Unity version: `2020.1` or newer
- License: `GPL-3.0`
