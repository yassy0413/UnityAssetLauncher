using System;
using UnityEditor;
using UnityEngine;

namespace AssetLauncher
{
    [Serializable]
    public sealed class AssetLauncherItem
    {
        [SerializeField]
        private string m_Guid;

        [SerializeField]
        private string m_Comment;

        private UnityEngine.Object m_Asset;

        public UnityEngine.Object Asset
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
                    
                var path = AssetDatabase.GUIDToAssetPath(m_Guid);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }
                    
                m_Asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                return m_Asset;
            }
            set
            {
                m_Guid = string.Empty;
                m_Asset = value;

                if (m_Asset != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_Asset, out m_Guid, out long _);
                }
            }
        }

        public string Comment
        {
            get => m_Comment;
            set => m_Comment = value;
        }

        public string Name => Asset == null ? string.Empty : Asset.name;
    }
}