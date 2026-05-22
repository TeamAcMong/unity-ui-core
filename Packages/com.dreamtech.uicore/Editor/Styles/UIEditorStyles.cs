using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Styles
{
    /// <summary>
    /// Shared GUI styles for UI Core custom inspectors. Lazy-initialized, textures carry
    /// HideFlags.HideAndDontSave so they are not serialized and are cleaned up properly on
    /// domain reload (Issue #9 fix).
    /// </summary>
    public static class UIEditorStyles
    {
        private static bool _initialized;

        private static GUIStyle _headerStyle;
        private static GUIStyle _tabActiveStyle;
        private static GUIStyle _tabInactiveStyle;
        private static GUIStyle _sectionBoxStyle;

        private static Texture2D _tabActiveBg;
        private static Texture2D _sectionBoxBg;

        // ── Public accessors (lazy init) ──────────────────────────────────────

        public static GUIStyle Header      { get { Init(); return _headerStyle;      } }
        public static GUIStyle TabActive   { get { Init(); return _tabActiveStyle;   } }
        public static GUIStyle TabInactive { get { Init(); return _tabInactiveStyle; } }
        public static GUIStyle SectionBox  { get { Init(); return _sectionBoxStyle;  } }

        // ── Palette ───────────────────────────────────────────────────────────

        public static readonly Color AccentColor  = new Color(0.40f, 0.70f, 1.00f);
        public static readonly Color WarningColor = new Color(1.00f, 0.70f, 0.30f);
        public static readonly Color ErrorColor   = new Color(1.00f, 0.40f, 0.40f);
        public static readonly Color SuccessColor = new Color(0.50f, 1.00f, 0.50f);

        // ── Initialization ────────────────────────────────────────────────────

        private static void Init()
        {
            if (_initialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 4, 4),
            };

            _tabActiveBg  = MakeTex(2, 2, new Color(0.30f, 0.50f, 0.80f, 0.85f));
            _sectionBoxBg = MakeTex(2, 2, new Color(0.20f, 0.20f, 0.20f, 0.30f));

            _tabActiveStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold,
                normal    = { background = _tabActiveBg, textColor = Color.white },
                hover     = { background = _tabActiveBg, textColor = Color.white },
                active    = { background = _tabActiveBg, textColor = Color.white },
            };

            _tabInactiveStyle = new GUIStyle(EditorStyles.toolbarButton);

            _sectionBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _sectionBoxBg },
                padding = new RectOffset(8, 8, 6, 6),
                margin  = new RectOffset(0, 0, 4, 4),
            };

            // Register disposal hook synchronously so it is guaranteed to fire even if a
            // domain reload is triggered before the next editor frame (H1 fix).
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;

            _initialized = true;
        }

        // ── Texture factory ───────────────────────────────────────────────────

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;

            var tex = new Texture2D(width, height)
            {
                hideFlags = HideFlags.HideAndDontSave,  // Issue #9 fix: no leak on domain reload
            };
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        private static void Dispose()
        {
            if (_tabActiveBg  != null) Object.DestroyImmediate(_tabActiveBg);
            if (_sectionBoxBg != null) Object.DestroyImmediate(_sectionBoxBg);
            _tabActiveBg  = null;
            _sectionBoxBg = null;
            _initialized  = false;
        }
    }
}
