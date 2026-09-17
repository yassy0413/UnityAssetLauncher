#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssetLauncher
{
    [Serializable]
    internal sealed class AssetLauncherItem
    {
        [SerializeField]
        private string m_Guid = string.Empty;

        [SerializeField]
        private string m_Comment = string.Empty;

        private UnityEngine.Object? m_Asset;

        public UnityEngine.Object? Asset
        {
            get
            {
                if (m_Asset != null)
                {
                    return m_Asset;
                }

                if (string.IsNullOrEmpty(m_Guid))
                {
                    return null;
                }

                var path = AssetPath;
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                m_Asset = AssetDatabase.LoadMainAssetAtPath(path);
                return m_Asset;
            }
            set
            {
                m_Guid = string.Empty;
                m_Asset = value;

                if (m_Asset != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_Asset, out m_Guid, out _);
                }
            }
        }

        public static AssetLauncherItem? FromAssetPath(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? null : new AssetLauncherItem { m_Guid = guid };
        }

        public string Comment
        {
            get => m_Comment;
            set => m_Comment = value;
        }

        // List labels must not load every asset (and its dependencies) into memory.
        public string AssetPath => string.IsNullOrEmpty(m_Guid)
            ? string.Empty : AssetDatabase.GUIDToAssetPath(m_Guid);

        public void ReleaseAsset() => m_Asset = null;

        public string Name => m_Asset != null ? m_Asset.name : Path.GetFileNameWithoutExtension(AssetPath);
        public string NameWithComment => string.IsNullOrEmpty(m_Comment) ? Name : $"{Name} ({m_Comment})";
    }
}