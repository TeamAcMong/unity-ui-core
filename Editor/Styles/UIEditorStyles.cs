using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Styles
{
    /// <summary>
    /// Shared GUI styles and color palette for UI Core custom inspectors.
    /// All styles are lazy-initialized. All textures carry HideFlags.HideAndDontSave
    /// so they are not serialized and are cleaned up on domain reload.
    /// </summary>
    public static class UIEditorStyles
    {
        private static bool _initialized;

        // ── Cached textures ────────────────────────────────────────────────────
        private static Texture2D _tabActiveBg;
        private static Texture2D _sectionBoxBg;
        private static Texture2D _cardBgTex;
        private static Texture2D _cardBorderTex;
        private static Texture2D _headerBgTex;
        private static Texture2D _accentBarTex;
        private static Texture2D _animAccentTex;
        private static Texture2D _behaviorAccentTex;
        private static Texture2D _pillBgTex;
        private static Texture2D _iconBtnHoverTex;
        private static Texture2D _playModeBgTex;
        private static Texture2D _dividerTex;
        private static Texture2D _helpInfoBgTex;
        private static Texture2D _helpSuccessBgTex;
        private static Texture2D _helpWarningBgTex;
        private static Texture2D _helpDangerBgTex;
        private static Texture2D _tabUnderlineTex;

        // ── Cached styles ──────────────────────────────────────────────────────
        private static GUIStyle _headerStyle;
        private static GUIStyle _tabActiveStyle;
        private static GUIStyle _tabInactiveStyle;
        private static GUIStyle _sectionBoxStyle;
        private static GUIStyle _heroHeaderStyle;
        private static GUIStyle _heroSubtitleStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _cardBackgroundStyle;
        private static GUIStyle _moduleCardStyle;
        private static GUIStyle _iconButtonStyle;
        private static GUIStyle _pillStyle;
        private static GUIStyle _mutedLabelStyle;
        private static GUIStyle _emptyStateLabelStyle;
        private static GUIStyle _playModeHeaderStyle;
        private static GUIStyle _moduleCardHeaderStyle;

        // ── Color Palette (theme-aware) ────────────────────────────────────────

        public static Color Accent =>
            EditorGUIUtility.isProSkin
                ? new Color(0.26f, 0.59f, 0.98f)
                : new Color(0.16f, 0.49f, 0.86f);

        public static Color AccentDim =>
            Accent * new Color(1f, 1f, 1f, 0.5f);

        public static Color CardBg =>
            EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.04f)
                : new Color(0f, 0f, 0f, 0.04f);

        public static Color CardBorder =>
            EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(0f, 0f, 0f, 0.12f);

        public static Color HeaderBg =>
            EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.13f, 0.13f)
                : new Color(0.85f, 0.85f, 0.85f);

        public static Color AnimationModuleColor => new Color(0.32f, 0.58f, 0.95f);   // blue
        public static Color BehaviorModuleColor  => new Color(0.95f, 0.65f, 0.30f);   // orange
        public static Color SuccessColor         => new Color(0.40f, 0.85f, 0.50f);
        public static Color WarningColor         => new Color(1.00f, 0.70f, 0.30f);
        public static Color DangerColor          => new Color(0.95f, 0.40f, 0.40f);

        public static Color MutedText =>
            EditorGUIUtility.isProSkin
                ? new Color(0.7f, 0.7f, 0.7f)
                : new Color(0.4f, 0.4f, 0.4f);

        // Legacy alias kept for backward compat
        public static readonly Color AccentColor  = new Color(0.40f, 0.70f, 1.00f);
        public static readonly Color ErrorColor   = new Color(1.00f, 0.40f, 0.40f);

        // ── Public style accessors (lazy init) ────────────────────────────────

        public static GUIStyle Header          { get { Init(); return _headerStyle;          } }
        public static GUIStyle TabActive       { get { Init(); return _tabActiveStyle;        } }
        public static GUIStyle TabInactive     { get { Init(); return _tabInactiveStyle;      } }
        public static GUIStyle SectionBox      { get { Init(); return _sectionBoxStyle;       } }
        public static GUIStyle HeroHeader      { get { Init(); return _heroHeaderStyle;       } }
        public static GUIStyle HeroSubtitle    { get { Init(); return _heroSubtitleStyle;     } }
        public static GUIStyle SectionHeader   { get { Init(); return _sectionHeaderStyle;    } }
        public static GUIStyle CardBackground  { get { Init(); return _cardBackgroundStyle;   } }
        public static GUIStyle ModuleCard      { get { Init(); return _moduleCardStyle;       } }
        public static GUIStyle IconButton      { get { Init(); return _iconButtonStyle;       } }
        public static GUIStyle Pill            { get { Init(); return _pillStyle;             } }
        public static GUIStyle MutedLabel      { get { Init(); return _mutedLabelStyle;       } }
        public static GUIStyle EmptyStateLabel { get { Init(); return _emptyStateLabelStyle;  } }
        public static GUIStyle PlayModeHeader  { get { Init(); return _playModeHeaderStyle;   } }
        public static GUIStyle ModuleCardHeader{ get { Init(); return _moduleCardHeaderStyle; } }

        // ── Built-in icon helpers ──────────────────────────────────────────────

        public static GUIContent IconAdd       => EditorGUIUtility.IconContent("d_Toolbar Plus");
        public static GUIContent IconRemove    => EditorGUIUtility.IconContent("d_Toolbar Minus");
        public static GUIContent IconDuplicate => EditorGUIUtility.IconContent("d_TreeEditor.Duplicate");
        public static GUIContent IconSettings  => EditorGUIUtility.IconContent("d_Settings");
        public static GUIContent IconPlay      => EditorGUIUtility.IconContent("d_PlayButton");
        public static GUIContent IconWarn      => EditorGUIUtility.IconContent("console.warnicon.sml");
        public static GUIContent IconInfo      => EditorGUIUtility.IconContent("console.infoicon.sml");
        public static GUIContent IconHelp      => EditorGUIUtility.IconContent("_Help");
        public static GUIContent IconAnimation => EditorGUIUtility.IconContent("d_Animation.Play");
        public static GUIContent IconBehavior  => EditorGUIUtility.IconContent("d_PrefabModel On Icon");
        public static GUIContent IconDragHandle=> EditorGUIUtility.IconContent("d_AvatarPivot");
        public static GUIContent IconFold      => new GUIContent("▶");
        public static GUIContent IconUnfold    => new GUIContent("▼");

        // ── Initialization ─────────────────────────────────────────────────────

        private static void Init()
        {
            if (_initialized) return;

            bool isPro = EditorGUIUtility.isProSkin;

            // ── Textures ─────────────────────────────────────────────────────
            _tabActiveBg       = MakeTex(2, 2, new Color(0.26f, 0.59f, 0.98f, 0.85f));
            _sectionBoxBg      = MakeTex(2, 2, new Color(isPro ? 0.20f : 0.80f, isPro ? 0.20f : 0.80f, isPro ? 0.20f : 0.80f, 0.30f));
            _cardBgTex         = MakeTex(2, 2, isPro ? new Color(1f, 1f, 1f, 0.04f) : new Color(0f, 0f, 0f, 0.04f));
            _cardBorderTex     = MakeTex(2, 2, isPro ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.12f));
            _headerBgTex       = MakeTex(2, 2, isPro ? new Color(0.13f, 0.13f, 0.13f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f));
            _accentBarTex      = MakeTex(2, 2, new Color(0.26f, 0.59f, 0.98f, 1f));
            _animAccentTex     = MakeTex(2, 2, new Color(0.32f, 0.58f, 0.95f, 1f));
            _behaviorAccentTex = MakeTex(2, 2, new Color(0.95f, 0.65f, 0.30f, 1f));
            _pillBgTex         = MakeTex(2, 2, new Color(0.26f, 0.59f, 0.98f, 0.25f));
            _iconBtnHoverTex   = MakeTex(2, 2, isPro ? new Color(1f, 1f, 1f, 0.10f) : new Color(0f, 0f, 0f, 0.08f));
            _playModeBgTex     = MakeTex(2, 2, isPro ? new Color(0.18f, 0.15f, 0.30f, 1f) : new Color(0.85f, 0.83f, 0.95f, 1f));
            _dividerTex        = MakeTex(2, 2, isPro ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.12f));
            _helpInfoBgTex     = MakeTex(2, 2, isPro ? new Color(0.20f, 0.30f, 0.50f, 0.35f) : new Color(0.80f, 0.90f, 1.00f, 0.50f));
            _helpSuccessBgTex  = MakeTex(2, 2, isPro ? new Color(0.15f, 0.35f, 0.20f, 0.35f) : new Color(0.85f, 1.00f, 0.88f, 0.50f));
            _helpWarningBgTex  = MakeTex(2, 2, isPro ? new Color(0.40f, 0.30f, 0.10f, 0.40f) : new Color(1.00f, 0.95f, 0.80f, 0.50f));
            _helpDangerBgTex   = MakeTex(2, 2, isPro ? new Color(0.45f, 0.15f, 0.15f, 0.40f) : new Color(1.00f, 0.88f, 0.88f, 0.50f));
            _tabUnderlineTex   = MakeTex(2, 2, new Color(0.26f, 0.59f, 0.98f, 1f));

            // ── Legacy header ────────────────────────────────────────────────
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 4, 4),
            };

            // ── Tab styles — underline indicator variant ──────────────────────
            _tabActiveStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(10, 10, 6, 6),
                normal    = { textColor = isPro ? Color.white : new Color(0.08f, 0.08f, 0.08f) },
                hover     = { textColor = isPro ? Color.white : new Color(0.08f, 0.08f, 0.08f) },
            };

            _tabInactiveStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(10, 10, 6, 6),
                normal    = { textColor = isPro ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.35f, 0.35f, 0.35f) },
                hover     = { background = _iconBtnHoverTex, textColor = isPro ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.15f) },
            };

            // ── Section box (legacy compat) ───────────────────────────────────
            _sectionBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _sectionBoxBg },
                padding = new RectOffset(8, 8, 6, 6),
                margin  = new RectOffset(0, 0, 4, 4),
            };

            // ── Hero header — 16pt bold ───────────────────────────────────────
            _heroHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 2, 2),
                normal    = { textColor = isPro ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.10f, 0.10f, 0.10f) },
            };

            // ── Hero subtitle — 11pt muted ────────────────────────────────────
            _heroSubtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 0, 2),
                normal    = { textColor = isPro ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.40f, 0.40f, 0.40f) },
            };

            // ── Section header — 13pt bold ────────────────────────────────────
            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 4, 4),
                normal    = { textColor = isPro ? new Color(0.90f, 0.90f, 0.90f) : new Color(0.12f, 0.12f, 0.12f) },
            };

            // ── Card background ───────────────────────────────────────────────
            _cardBackgroundStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _cardBgTex },
                padding = new RectOffset(8, 8, 6, 6),
                margin  = new RectOffset(0, 0, 4, 4),
            };

            // ── Module card (tighter padding — header managed manually) ────────
            _moduleCardStyle = new GUIStyle(GUI.skin.box)
            {
                normal  = { background = _cardBgTex },
                padding = new RectOffset(6, 6, 4, 4),
                margin  = new RectOffset(0, 0, 2, 2),
            };

            // ── Module card header row ────────────────────────────────────────
            _moduleCardHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 0, 0),
            };

            // ── Icon button 22x22 ─────────────────────────────────────────────
            _iconButtonStyle = new GUIStyle(GUIStyle.none)
            {
                padding  = new RectOffset(2, 2, 2, 2),
                margin   = new RectOffset(1, 1, 1, 1),
                fixedWidth  = 22f,
                fixedHeight = 22f,
                alignment = TextAnchor.MiddleCenter,
                hover     = { background = _iconBtnHoverTex },
            };

            // ── Pill badge ────────────────────────────────────────────────────
            _pillStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize  = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(6, 6, 2, 2),
                normal    = { background = _pillBgTex, textColor = isPro ? Color.white : Color.white },
            };

            // ── Muted label ───────────────────────────────────────────────────
            _mutedLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal   = { textColor = isPro ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.40f, 0.40f, 0.40f) },
            };

            // ── Empty state label ─────────────────────────────────────────────
            _emptyStateLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                normal    = { textColor = isPro ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.45f, 0.45f, 0.45f) },
            };

            // ── Play mode header ──────────────────────────────────────────────
            _playModeHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                padding   = new RectOffset(4, 4, 4, 4),
                normal    = { background = _playModeBgTex, textColor = isPro ? new Color(0.85f, 0.80f, 1.00f) : new Color(0.25f, 0.15f, 0.50f) },
            };

            // Register disposal on domain reload
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;

            _initialized = true;
        }

        // ── Texture factory ────────────────────────────────────────────────────

        /// <summary>
        /// Create a solid-color texture. HideAndDontSave prevents serialization / leaks.
        /// MUST only be called from Init() — never from OnGUI paths.
        /// </summary>
        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(width, height) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        // ── Cleanup ────────────────────────────────────────────────────────────

        private static void Dispose()
        {
            DestroyTex(ref _tabActiveBg);
            DestroyTex(ref _sectionBoxBg);
            DestroyTex(ref _cardBgTex);
            DestroyTex(ref _cardBorderTex);
            DestroyTex(ref _headerBgTex);
            DestroyTex(ref _accentBarTex);
            DestroyTex(ref _animAccentTex);
            DestroyTex(ref _behaviorAccentTex);
            DestroyTex(ref _pillBgTex);
            DestroyTex(ref _iconBtnHoverTex);
            DestroyTex(ref _playModeBgTex);
            DestroyTex(ref _dividerTex);
            DestroyTex(ref _helpInfoBgTex);
            DestroyTex(ref _helpSuccessBgTex);
            DestroyTex(ref _helpWarningBgTex);
            DestroyTex(ref _helpDangerBgTex);
            DestroyTex(ref _tabUnderlineTex);
            _initialized = false;
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null) Object.DestroyImmediate(tex);
            tex = null;
        }
    }
}
