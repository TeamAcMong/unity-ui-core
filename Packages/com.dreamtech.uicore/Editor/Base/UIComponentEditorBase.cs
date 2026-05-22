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

        // ── Abstract contract ─────────────────────────────────────────────────

        protected abstract string[] TabNames { get; }
        protected abstract void DrawTabContent(int tabIndex);

        // ── Virtual overrides ─────────────────────────────────────────────────

        /// <summary>Header label shown above the tabs. Defaults to the component's class name.</summary>
        protected virtual string HeaderTitle => target.GetType().Name;

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

        // ── Inspector entry point ─────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            DrawTabs();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginVertical(UIEditorStyles.SectionBox);
            DrawTabContent(currentTabIndex);
            EditorGUILayout.EndVertical();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Play Mode Testing", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawPlayModeContent();
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Layout helpers ────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(HeaderTitle, UIEditorStyles.Header);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            string[] tabs = TabNames;
            for (int i = 0; i < tabs.Length; i++)
            {
                GUIStyle style = (i == currentTabIndex)
                    ? UIEditorStyles.TabActive
                    : UIEditorStyles.TabInactive;

                if (GUILayout.Button(tabs[i], style))
                    currentTabIndex = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Subclass utilities ────────────────────────────────────────────────

        /// <summary>Draw a labelled section with a HelpBox border.</summary>
        protected void DrawSection(string title, System.Action drawContent)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// Draws a single serialized property. Shows a warning box if the property
        /// path is not found — safer than letting a null ref slip through silently.
        /// </summary>
        /// <param name="propertyPath">Serialized field name (e.g. "clickCooldown").</param>
        /// <param name="label">Optional custom label. Pass null to use the property's own label.</param>
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
