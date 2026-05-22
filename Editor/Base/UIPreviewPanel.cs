using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
using DreamTech.UICore.Editor.Preview;
using DreamTech.UICore.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Base
{
    /// <summary>
    /// Stateless helper that draws the "Preview Animation" panel at the top of the
    /// Animation tab in UIAnimatedComponent custom inspectors.
    ///
    /// Layout:
    /// <code>
    /// ┌─ Preview Animation ───────────────────────────────────────┐
    /// │  State: [Pressed ▼]   [▶ Play]   [↺ Reset]              │
    /// │  ⌛ Running: ScaleModule → Pressed              [⏹ Stop] │  (only when active)
    /// │  ████████████░░░░░░░░  60%                               │  (only when active)
    /// └──────────────────────────────────────────────────────────┘
    /// </code>
    ///
    /// Call <see cref="Draw"/> at the top of a component's Animation tab DrawXxx method,
    /// passing the scene-instance target and the owning <see cref="Editor"/> (for Repaint).
    /// </summary>
    public static class UIPreviewPanel
    {
        // ── Persistent UI selection (session-static, no serialization needed) ──
        private static UIState _selectedState = UIState.Pressed;

        /// <summary>
        /// Draw the Preview Animation panel.
        /// </summary>
        /// <param name="target">The UIAnimatedComponent to preview. May be null (panel is skipped).</param>
        /// <param name="editor">The owning Editor, used to call <c>Repaint()</c>.</param>
        public static void Draw(UIAnimatedComponent target, UnityEditor.Editor editor)
        {
            if (target == null) return;

            bool isSceneInstance = target.gameObject.scene.IsValid()
                                   && target.gameObject.scene.isLoaded;

            EditorGUILayout.BeginVertical(UIEditorStyles.CardBackground);

            // ── Header row ───────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(UIEditorStyles.IconPlay, GUILayout.Width(16f), GUILayout.Height(16f));
            EditorGUILayout.Space(4f);
            GUILayout.Label("Preview Animation", UIEditorStyles.SectionHeader);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            // ── Controls (disabled when not a scene instance OR preview already running) ──
            using (new EditorGUI.DisabledScope(!isSceneInstance || PreviewSession.IsActive))
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label("State:", GUILayout.Width(40f));
                _selectedState = (UIState)EditorGUILayout.EnumPopup(_selectedState, GUILayout.Width(100f));

                GUILayout.Space(6f);

                if (GUILayout.Button(
                        new GUIContent(" ▶ Play", "Run all enabled animation modules for this state"),
                        GUILayout.Height(22f), GUILayout.MinWidth(80f)))
                {
                    PreviewSession.PreviewState(target, _selectedState, () => editor.Repaint());
                }

                if (GUILayout.Button(
                        new GUIContent(" ↺ Reset", "Restore initial state"),
                        GUILayout.Height(22f), GUILayout.MinWidth(70f)))
                {
                    PreviewSession.CancelActive();
                    editor.Repaint();
                }

                EditorGUILayout.EndHorizontal();
            }

            // ── Running indicator + progress bar (only when active) ───────────
            if (PreviewSession.IsActive)
            {
                EditorGUILayout.Space(4f);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("⌛ " + PreviewSession.CurrentInfo, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        new GUIContent("⏹ Stop", "Cancel preview"),
                        GUILayout.Height(18f), GUILayout.MinWidth(60f)))
                {
                    PreviewSession.CancelActive();
                    editor.Repaint();
                }
                EditorGUILayout.EndHorizontal();

                // Progress bar
                Rect progressRect = GUILayoutUtility.GetRect(0f, 6f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(progressRect, new Color(0f, 0f, 0f, 0.20f));
                float fill = Mathf.Clamp01(PreviewSession.Progress);
                if (fill > 0f)
                {
                    Rect fillRect = new Rect(progressRect.x, progressRect.y,
                        progressRect.width * fill, progressRect.height);
                    EditorGUI.DrawRect(fillRect, UIEditorStyles.Accent);
                }

                // Force continuous repaint so progress bar animates smoothly
                editor.Repaint();
            }

            // ── Warning when not a scene instance ────────────────────────────
            if (!isSceneInstance)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "Preview only works on scene instances. Drag the prefab into the scene to test.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
