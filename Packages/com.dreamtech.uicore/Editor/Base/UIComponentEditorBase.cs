using DreamTech.UICore.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Base
{
    /// <summary>
    /// Template base class for UI Core custom inspectors.
    /// Subclasses implement <see cref="TabNames"/> and <see cref="DrawTabContent"/>
    /// to provide a tabbed inspector layout. Optionally override
    /// <see cref="DrawPlayModeContent"/> for runtime test buttons.
    /// </summary>
    public abstract class UIComponentEditorBase : UnityEditor.Editor
    {
        protected int currentTabIndex;

        // ── Abstract contract ──────────────────────────────────────────────────

        protected abstract string[] TabNames { get; }
        protected abstract void DrawTabContent(int tabIndex);

        // ── Virtual overrides ──────────────────────────────────────────────────

        /// <summary>Header label shown above the tabs. Defaults to the component's class name.</summary>
        protected virtual string HeaderTitle => target.GetType().Name;

        /// <summary>Optional subtitle shown under the hero header.</summary>
        protected virtual string HeaderSubtitle => "Modular UI component";

        /// <summary>Optional icon shown in the hero header.</summary>
        protected virtual GUIContent HeaderIcon => null;

        /// <summary>
        /// Override to add play-mode test buttons/labels.
        /// Called only when <c>Application.isPlaying</c>.
        /// </summary>
        protected virtual void DrawPlayModeContent()
        {
            EditorGUILayout.LabelField(
                "(Override DrawPlayModeContent to add test buttons)",
                EditorStyles.miniLabel);
        }

        // ── Inspector entry point ──────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeroHeader(HeaderTitle, HeaderSubtitle, HeaderIcon);
            EditorGUILayout.Space(6f);
            DrawTabBar();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginVertical(UIEditorStyles.CardBackground);
            DrawTabContent(currentTabIndex);
            EditorGUILayout.EndVertical();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10f);
                DrawPlayModePanel(DrawPlayModeContent);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Tab bar with underline indicator ──────────────────────────────────

        private void DrawTabBar()
        {
            const float tabHeight = 28f;
            const float underlineH = 2f;

            EditorGUILayout.BeginHorizontal();

            string[] tabs = TabNames;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool isActive = (i == currentTabIndex);
                GUIStyle style = isActive ? UIEditorStyles.TabActive : UIEditorStyles.TabInactive;

                if (GUILayout.Button(tabs[i], style, GUILayout.Height(tabHeight)))
                    currentTabIndex = i;

                // Draw 2px accent underline on active tab
                if (isActive)
                {
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    Rect underlineRect = new Rect(lastRect.x, lastRect.yMax - underlineH, lastRect.width, underlineH);
                    EditorGUI.DrawRect(underlineRect, UIEditorStyles.Accent);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Layout primitives (protected for subclass use) ─────────────────────

        /// <summary>
        /// Draw a hero header: HeaderBg band with optional icon, bold title and muted subtitle,
        /// followed by a 2px accent line at the bottom.
        /// </summary>
        protected void DrawHeroHeader(string title, string subtitle = null, GUIContent icon = null)
        {
            Rect headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(headerRect, UIEditorStyles.HeaderBg);

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(8f);

            if (icon != null && icon.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(24f), GUILayout.Height(24f));
                EditorGUILayout.Space(6f);
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, UIEditorStyles.HeroHeader);
            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, UIEditorStyles.HeroSubtitle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
            EditorGUILayout.EndVertical();

            // 2px accent line below the hero band
            Rect accentRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(accentRect.x, accentRect.yMax - 2f, accentRect.width, 2f), UIEditorStyles.Accent);
            EditorGUILayout.Space(2f);
        }

        /// <summary>
        /// Draw a collapsible section card with title and optional icon.
        /// Returns the new foldout state; wrap content in <c>if (foldout) { ... }</c>.
        /// </summary>
        /// <summary>
        /// Draw a collapsible section card with title and optional icon.
        /// Returns the new foldout state. ALWAYS call <see cref="EndSectionCard"/> after this,
        /// regardless of the return value — the card's vertical group is always open.
        /// Pattern:
        /// <code>
        ///   if (DrawSectionCard("Title", ref _fold)) { /* draw content */ }
        ///   EndSectionCard();
        /// </code>
        /// </summary>
        protected bool DrawSectionCard(string title, ref bool foldout, GUIContent icon = null, bool collapsible = true)
        {
            EditorGUILayout.BeginVertical(UIEditorStyles.CardBackground);

            // Header row
            EditorGUILayout.BeginHorizontal();

            if (collapsible)
            {
                GUIContent arrow = foldout ? UIEditorStyles.IconUnfold : UIEditorStyles.IconFold;
                if (GUILayout.Button(arrow, GUIStyle.none, GUILayout.Width(16f), GUILayout.Height(16f)))
                    foldout = !foldout;
            }

            if (icon != null && icon.image != null)
                GUILayout.Label(icon, GUILayout.Width(16f), GUILayout.Height(16f));

            EditorGUILayout.LabelField(title, UIEditorStyles.SectionHeader);
            EditorGUILayout.EndHorizontal();

            // Thin accent separator under the section header
            Rect headerRowRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(
                new Rect(headerRowRect.x, headerRowRect.yMax + 2f, headerRowRect.width, 1f),
                new Color(UIEditorStyles.Accent.r, UIEditorStyles.Accent.g, UIEditorStyles.Accent.b, 0.25f));
            EditorGUILayout.Space(4f);

            if (!collapsible) foldout = true;

            // NOTE: vertical group is always left open here — EndSectionCard() closes it.
            return foldout;
        }

        /// <summary>
        /// Always call this after <see cref="DrawSectionCard"/> to close the card's vertical group.
        /// </summary>
        protected void EndSectionCard()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        /// <summary>Draw a 1px horizontal divider line.</summary>
        protected void DrawDivider(float verticalSpacing = 4f)
        {
            EditorGUILayout.Space(verticalSpacing);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(
                UIEditorStyles.MutedText.r,
                UIEditorStyles.MutedText.g,
                UIEditorStyles.MutedText.b,
                0.30f));
            EditorGUILayout.Space(verticalSpacing);
        }

        /// <summary>Help card severity levels.</summary>
        protected enum HelpType { Info, Success, Warning, Danger }

        /// <summary>
        /// Modern help card: tinted background, 3px colored left border, icon + message.
        /// </summary>
        protected void DrawHelpCard(string message, HelpType type = HelpType.Info)
        {
            Color borderColor;
            GUIContent icon;
            Color bgColor;

            switch (type)
            {
                case HelpType.Success:
                    borderColor = UIEditorStyles.SuccessColor;
                    bgColor     = new Color(0.40f, 0.85f, 0.50f, EditorGUIUtility.isProSkin ? 0.12f : 0.15f);
                    icon        = UIEditorStyles.IconInfo;
                    break;
                case HelpType.Warning:
                    borderColor = UIEditorStyles.WarningColor;
                    bgColor     = new Color(1.00f, 0.70f, 0.30f, EditorGUIUtility.isProSkin ? 0.12f : 0.15f);
                    icon        = UIEditorStyles.IconWarn;
                    break;
                case HelpType.Danger:
                    borderColor = UIEditorStyles.DangerColor;
                    bgColor     = new Color(0.95f, 0.40f, 0.40f, EditorGUIUtility.isProSkin ? 0.12f : 0.15f);
                    icon        = UIEditorStyles.IconWarn;
                    break;
                default: // Info
                    borderColor = UIEditorStyles.Accent;
                    bgColor     = new Color(0.26f, 0.59f, 0.98f, EditorGUIUtility.isProSkin ? 0.10f : 0.12f);
                    icon        = UIEditorStyles.IconInfo;
                    break;
            }

            Rect cardRect = EditorGUILayout.BeginVertical();

            // Draw tinted background
            EditorGUI.DrawRect(cardRect, bgColor);

            EditorGUILayout.BeginHorizontal();

            // 3px left border drawn after BeginHorizontal so we have the final rect
            EditorGUILayout.Space(8f); // reserve space for border + gap

            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(icon, GUILayout.Width(16f), GUILayout.Height(16f));
            EditorGUILayout.Space(4f);

            var msgStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 11,
                wordWrap = true,
            };
            GUILayout.Label(message, msgStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Draw 3px left border over the card
            Rect borderRect = new Rect(cardRect.x, cardRect.y, 3f, cardRect.height);
            EditorGUI.DrawRect(borderRect, borderColor);

            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// Empty state indicator: centered icon + muted message + optional CTA button.
        /// </summary>
        protected void DrawEmptyState(string message, string actionLabel = null, System.Action onAction = null)
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.BeginVertical();

            // Center the content
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();

            // Large grayed icon
            var iconContent = EditorGUIUtility.IconContent("d_Package Manager");
            if (iconContent != null && iconContent.image != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUILayout.Label(iconContent, GUILayout.Width(32f), GUILayout.Height(32f));
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4f);

            // Muted message
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(message, UIEditorStyles.EmptyStateLabel, GUILayout.MaxWidth(220f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Optional action button
            if (!string.IsNullOrEmpty(actionLabel) && onAction != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(actionLabel, GUILayout.Width(140f), GUILayout.Height(24f)))
                    onAction();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12f);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draw a "Play Mode Tools" panel with a distinctive purple-tinted background.
        /// Content is disabled when not in play mode (uses EditorGUI.BeginDisabledGroup).
        /// </summary>
        protected void DrawPlayModePanel(System.Action drawTests)
        {
            EditorGUILayout.Space(2f);

            Rect panelRect = EditorGUILayout.BeginVertical();

            bool isPro = EditorGUIUtility.isProSkin;
            Color panelBg = isPro
                ? new Color(0.18f, 0.15f, 0.30f, 1f)
                : new Color(0.88f, 0.85f, 0.96f, 1f);
            EditorGUI.DrawRect(panelRect, panelBg);

            EditorGUILayout.Space(6f);

            // Header row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(6f);
            GUILayout.Label(UIEditorStyles.IconPlay, GUILayout.Width(16f), GUILayout.Height(16f));
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Play Mode Tools", UIEditorStyles.PlayModeHeader);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);
            DrawDivider(2f);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical();
            drawTests?.Invoke();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            EditorGUILayout.EndVertical();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(6f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// Draw a small pill badge inline (call inside a horizontal group).
        /// </summary>
        protected void DrawPill(string label, Color color)
        {
            var style = new GUIStyle(UIEditorStyles.Pill)
            {
                normal = { background = UIEditorStyles.MakeTex(2, 2, new Color(color.r, color.g, color.b, 0.25f)) },
            };
            GUILayout.Label(label, style, GUILayout.ExpandWidth(false));
        }

        /// <summary>22×22 icon button. Returns true when clicked.</summary>
        protected bool IconButton(GUIContent icon, string tooltip = null, params GUILayoutOption[] options)
        {
            var content = tooltip != null ? new GUIContent(icon.image, tooltip) : icon;
            if (options == null || options.Length == 0)
                options = new[] { GUILayout.Width(22f), GUILayout.Height(22f) };
            return GUILayout.Button(content, UIEditorStyles.IconButton, options);
        }

        // ── Legacy helpers (backward compat) ──────────────────────────────────

        /// <summary>Draw a labelled section with a HelpBox border (legacy).</summary>
        protected void DrawSection(string title, System.Action drawContent)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// Draw a single serialized property. Shows a warning box if the property
        /// path is not found — safer than letting a null ref slip through silently.
        /// </summary>
        protected void DrawProperty(string propertyPath, string label = null)
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyPath);
            if (prop != null)
            {
                if (label != null)
                    EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
                else
                    EditorGUILayout.PropertyField(prop, true);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Property '{propertyPath}' not found on {target.GetType().Name}.",
                    MessageType.Warning);
            }
        }
    }
}
