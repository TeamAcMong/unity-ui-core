using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Behaviors;
using DreamTech.UICore.Editor.Styles;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DreamTech.UICore.Editor.Drawers
{
    /// <summary>
    /// Renders a <c>List&lt;IModule&gt;</c> serialized via <c>[SerializeReference]</c> as a
    /// production-grade accordion card list:
    /// <list type="bullet">
    ///   <item>Drag-to-reorder via ReorderableList</item>
    ///   <item>Per-card: accent bar, enabled toggle, display name, type pill, foldout, context menu</item>
    ///   <item>Add button opens a searchable popup grouped by Built-in / Custom</item>
    ///   <item>Context menu: Duplicate / Remove / Move Up / Move Down</item>
    /// </list>
    /// <para>
    /// Collapsed/expanded state is persisted in a static dictionary keyed by property path,
    /// surviving Inspector redraws within a session.
    /// </para>
    /// </summary>
    public static class ModuleListDrawer
    {
        // ── State caches ───────────────────────────────────────────────────────

        /// <summary>Expanded state per element: key = "propertyPath[index]".</summary>
        private static readonly Dictionary<string, bool> _expandedState = new Dictionary<string, bool>();

        /// <summary>ReorderableList instances cached per property path.</summary>
        private static readonly Dictionary<string, ReorderableList> _listCache = new Dictionary<string, ReorderableList>();

        /// <summary>Discovered concrete types per base type.</summary>
        private static readonly Dictionary<Type, List<Type>> _typeCache = new Dictionary<Type, List<Type>>();

        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Draw the module list.
        /// </summary>
        /// <param name="listProperty">SerializedProperty of the List field (must be array).</param>
        /// <param name="moduleBaseType">Interface or base type for discovery (e.g. typeof(IAnimationModule)).</param>
        /// <param name="accentColor">Card accent bar color: AnimationModuleColor (blue) or BehaviorModuleColor (orange).</param>
        /// <param name="moduleIcon">Icon shown next to each card header.</param>
        /// <param name="addButtonLabel">Label for the Add button.</param>
        public static void Draw(
            SerializedProperty listProperty,
            Type               moduleBaseType,
            Color              accentColor,
            GUIContent         moduleIcon,
            string             addButtonLabel = "Add Module")
        {
            if (listProperty == null) return;

            string propPath = listProperty.propertyPath;

            // ── Header row: list title + item count + Add button ───────────────
            EditorGUILayout.BeginHorizontal();

            string listLabel = ObjectNames.NicifyVariableName(listProperty.displayName);
            EditorGUILayout.LabelField(listLabel, UIEditorStyles.SectionHeader, GUILayout.ExpandWidth(true));

            int count = listProperty.arraySize;
            if (count > 0)
            {
                // Count badge
                var badgeStyle = new GUIStyle(UIEditorStyles.Pill)
                {
                    normal = { background = UIEditorStyles.MakeTex(2, 2, new Color(accentColor.r, accentColor.g, accentColor.b, 0.22f)) },
                };
                GUILayout.Label(count.ToString(), badgeStyle, GUILayout.ExpandWidth(false));
                GUILayout.Space(4f);
            }

            // Add button
            var addContent = new GUIContent(UIEditorStyles.IconAdd.image, addButtonLabel);
            if (GUILayout.Button(addContent, UIEditorStyles.IconButton))
            {
                ShowAddPopup(listProperty, moduleBaseType, accentColor);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);

            // ── Empty state ────────────────────────────────────────────────────
            if (count == 0)
            {
                DrawEmptyState(addButtonLabel);
                return;
            }

            // ── Reorderable list ───────────────────────────────────────────────
            ReorderableList rl = GetOrCreateList(listProperty, accentColor, moduleIcon);
            rl.serializedProperty = listProperty;
            rl.DoLayoutList();
        }

        // ── ReorderableList factory ────────────────────────────────────────────

        private static ReorderableList GetOrCreateList(
            SerializedProperty listProp,
            Color              accentColor,
            GUIContent         moduleIcon)
        {
            string key = listProp.propertyPath;

            if (_listCache.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var rl = new ReorderableList(
                listProp.serializedObject,
                listProp,
                draggable:    true,
                displayHeader: false,
                displayAddButton: false,
                displayRemoveButton: false);

            rl.showDefaultBackground = false;

            // Element height: we compute it dynamically
            rl.elementHeightCallback = (index) =>
            {
                string expandKey = key + "[" + index + "]";
                bool expanded = GetExpanded(expandKey);

                if (!expanded) return 32f; // collapsed card header only

                var element = listProp.GetArrayElementAtIndex(index);
                if (element.managedReferenceValue == null) return 32f;

                // Header + property content height
                float propHeight = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
                // propHeight includes the top-level element; subtract one line (header is drawn separately)
                return 32f + Mathf.Max(0f, propHeight - EditorGUIUtility.singleLineHeight) + 8f;
            };

            rl.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                DrawModuleCard(rect, listProp, index, key, accentColor, moduleIcon);
            };

            rl.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
            {
                // We paint our own backgrounds in drawElementCallback
            };

            _listCache[key] = rl;
            return rl;
        }

        // ── Card renderer ──────────────────────────────────────────────────────

        private static void DrawModuleCard(
            Rect               rect,
            SerializedProperty listProp,
            int                index,
            string             listKey,
            Color              accentColor,
            GUIContent         moduleIcon)
        {
            string expandKey = listKey + "[" + index + "]";
            bool expanded    = GetExpanded(expandKey);

            var element = listProp.GetArrayElementAtIndex(index);
            object instance = element.managedReferenceValue;

            // ── Card background ────────────────────────────────────────────────
            bool isPro = EditorGUIUtility.isProSkin;
            Color cardBg = isPro ? new Color(1f, 1f, 1f, 0.04f) : new Color(0f, 0f, 0f, 0.03f);
            EditorGUI.DrawRect(rect, cardBg);

            // 3px left accent bar
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accentColor);

            // ── Header row (32px tall) ─────────────────────────────────────────
            const float headerH  = 32f;
            const float padding  = 6f;

            Rect headerRow = new Rect(rect.x + 3f + padding, rect.y, rect.width - 3f - padding * 2f - 24f, headerH);

            // Drag handle icon (cosmetic)
            float cx = headerRow.x;

            Rect dragRect = new Rect(cx, headerRow.y + (headerH - 16f) * 0.5f, 12f, 16f);
            var handleContent = UIEditorStyles.IconDragHandle;
            if (handleContent?.image != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.40f);
                GUI.DrawTexture(dragRect, handleContent.image, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            cx += 16f;

            // Enabled toggle
            var enabledProp = element.FindPropertyRelative("enabled");
            if (enabledProp != null)
            {
                Rect toggleRect = new Rect(cx, headerRow.y + (headerH - 14f) * 0.5f, 14f, 14f);
                EditorGUI.BeginChangeCheck();
                bool newEnabled = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    enabledProp.boolValue = newEnabled;
                    listProp.serializedObject.ApplyModifiedProperties();
                }
                cx += 18f;
            }

            // Module icon
            if (moduleIcon?.image != null)
            {
                Rect iconRect = new Rect(cx, headerRow.y + (headerH - 14f) * 0.5f, 14f, 14f);
                GUI.DrawTexture(iconRect, moduleIcon.image, ScaleMode.ScaleToFit);
                cx += 18f;
            }

            // Display name
            string displayName = GetModuleDisplayName(instance);
            string typeName    = instance != null ? FriendlyTypeName(instance.GetType().Name) : "null";

            float availableForName = headerRow.xMax - cx - 60f; // reserve space for pill + arrows
            Rect  nameRect = new Rect(cx, headerRow.y + (headerH - EditorGUIUtility.singleLineHeight) * 0.5f,
                                      availableForName, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(nameRect, displayName, UIEditorStyles.ModuleCardHeader);
            cx += availableForName + 4f;

            // Type pill (muted, small)
            float pillW = 52f;
            Rect pillRect = new Rect(cx, headerRow.y + (headerH - 16f) * 0.5f, pillW, 16f);
            var pillBg = UIEditorStyles.MakeTex(2, 2, new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));
            var pillStyle = new GUIStyle(UIEditorStyles.Pill)
            {
                normal = { background = pillBg, textColor = isPro ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.30f, 0.30f, 0.30f) },
                fontSize = 9,
            };
            GUI.Label(pillRect, typeName, pillStyle);
            cx += pillW + 4f;

            // Foldout arrow
            Rect arrowRect = new Rect(cx, headerRow.y + (headerH - 16f) * 0.5f, 16f, 16f);
            GUIContent arrowContent = expanded ? UIEditorStyles.IconUnfold : UIEditorStyles.IconFold;
            if (GUI.Button(arrowRect, arrowContent, GUIStyle.none))
            {
                SetExpanded(expandKey, !expanded);
            }

            // ⋮ context menu button (right edge)
            Rect menuBtnRect = new Rect(rect.xMax - 22f, rect.y + (headerH - 18f) * 0.5f, 18f, 18f);
            if (GUI.Button(menuBtnRect, "⋮", EditorStyles.miniLabel))
            {
                ShowCardContextMenu(listProp, index, listKey);
            }

            // ── Expanded properties ────────────────────────────────────────────
            if (expanded && instance != null)
            {
                float contentY = rect.y + headerH + 4f;
                float contentH = rect.height - headerH - 8f;
                if (contentH > 0f)
                {
                    Rect contentRect = new Rect(rect.x + 8f, contentY, rect.width - 16f, contentH);
                    EditorGUI.indentLevel++;
                    EditorGUI.PropertyField(contentRect, element, GUIContent.none, true);
                    EditorGUI.indentLevel--;
                }
            }

            // Bottom separator line
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                new Color(1f, 1f, 1f, isPro ? 0.05f : 0.08f));
        }

        // ── Context menu ───────────────────────────────────────────────────────

        private static void ShowCardContextMenu(SerializedProperty listProp, int index, string listKey)
        {
            SerializedObject so       = listProp.serializedObject;
            string           propPath = listProp.propertyPath;
            int              count    = listProp.arraySize;

            var menu = new GenericMenu();

            if (index > 0)
            {
                menu.AddItem(new GUIContent("Move Up"), false, () =>
                {
                    var p = so.FindProperty(propPath);
                    p.MoveArrayElement(index, index - 1);
                    so.ApplyModifiedProperties();
                    InvalidateListCache(listKey);
                });
            }
            else menu.AddDisabledItem(new GUIContent("Move Up"));

            if (index < count - 1)
            {
                menu.AddItem(new GUIContent("Move Down"), false, () =>
                {
                    var p = so.FindProperty(propPath);
                    p.MoveArrayElement(index, index + 1);
                    so.ApplyModifiedProperties();
                    InvalidateListCache(listKey);
                });
            }
            else menu.AddDisabledItem(new GUIContent("Move Down"));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                var p = so.FindProperty(propPath);
                p.InsertArrayElementAtIndex(index);
                so.ApplyModifiedProperties();
                InvalidateListCache(listKey);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                var p = so.FindProperty(propPath);
                // Two DeleteArrayElement calls needed when element has a managed reference
                p.DeleteArrayElementAtIndex(index);
                if (p.arraySize > index)
                    p.DeleteArrayElementAtIndex(index);
                so.ApplyModifiedProperties();
                InvalidateListCache(listKey);
            });

            menu.ShowAsContext();
        }

        // ── Add popup ─────────────────────────────────────────────────────────

        private static void ShowAddPopup(SerializedProperty listProp, Type baseType, Color accentColor)
        {
            SerializedObject so       = listProp.serializedObject;
            string           propPath = listProp.propertyPath;

            var types = GetCompatibleTypes(baseType);

            var menu = new GenericMenu();

            const string builtInNs = "DreamTech.UICore";

            var builtIn = types.Where(t => t.Namespace != null && t.Namespace.StartsWith(builtInNs, StringComparison.Ordinal)).ToList();
            var custom  = types.Where(t => t.Namespace == null || !t.Namespace.StartsWith(builtInNs, StringComparison.Ordinal)).ToList();

            if (builtIn.Count > 0)
            {
                foreach (var t in builtIn)
                {
                    Type capturedType = t;
                    string displayName = "Built-in/" + GetDisplayNameForType(t, baseType);
                    menu.AddItem(new GUIContent(displayName), false, () =>
                    {
                        AppendModule(so, propPath, capturedType, listProp.propertyPath);
                    });
                }
            }

            if (custom.Count > 0)
            {
                if (builtIn.Count > 0) menu.AddSeparator("Custom/");
                foreach (var t in custom)
                {
                    Type capturedType = t;
                    string displayName = "Custom/" + GetDisplayNameForType(t, baseType);
                    menu.AddItem(new GUIContent(displayName), false, () =>
                    {
                        AppendModule(so, propPath, capturedType, listProp.propertyPath);
                    });
                }
            }

            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No types found"));
            }

            menu.ShowAsContext();
        }

        private static void AppendModule(SerializedObject so, string propPath, Type type, string listKey)
        {
            var p = so.FindProperty(propPath);
            if (p == null) return;

            int newIndex = p.arraySize;
            p.InsertArrayElementAtIndex(newIndex);
            var el = p.GetArrayElementAtIndex(newIndex);
            el.managedReferenceValue = Activator.CreateInstance(type);
            so.ApplyModifiedProperties();

            // Auto-expand newly added card
            SetExpanded(propPath + "[" + newIndex + "]", true);
            InvalidateListCache(listKey);
        }

        // ── Empty state ────────────────────────────────────────────────────────

        private static void DrawEmptyState(string addButtonLabel)
        {
            EditorGUILayout.BeginVertical(UIEditorStyles.CardBackground);
            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"No modules. Click + to {addButtonLabel.ToLower()}.",
                UIEditorStyles.EmptyStateLabel,
                GUILayout.MaxWidth(240f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.EndVertical();
        }

        // ── Type discovery helpers ─────────────────────────────────────────────

        private static List<Type> GetCompatibleTypes(Type baseType)
        {
            if (_typeCache.TryGetValue(baseType, out var cached)) return cached;

            var types = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] asmTypes;
                try { asmTypes = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { asmTypes = ex.Types; }
                if (asmTypes == null) continue;

                foreach (var t in asmTypes)
                {
                    if (t == null || t.IsAbstract || t.IsInterface) continue;
                    if (!baseType.IsAssignableFrom(t)) continue;
                    if (t.GetCustomAttribute<SerializableAttribute>() == null) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    types.Add(t);
                }
            }

            types.Sort((a, b) => string.Compare(
                GetDisplayNameForType(a, baseType),
                GetDisplayNameForType(b, baseType),
                StringComparison.OrdinalIgnoreCase));

            _typeCache[baseType] = types;
            return types;
        }

        private static string GetDisplayNameForType(Type type, Type baseType)
        {
            // Try IAnimationModule.DisplayName
            if (typeof(IAnimationModule).IsAssignableFrom(type))
            {
                try
                {
                    var inst = (IAnimationModule)Activator.CreateInstance(type);
                    if (!string.IsNullOrEmpty(inst?.DisplayName)) return inst.DisplayName;
                }
                catch { /* ignore */ }
            }

            // Try IBehaviorModule.DisplayName
            if (typeof(IBehaviorModule).IsAssignableFrom(type))
            {
                try
                {
                    var inst = (IBehaviorModule)Activator.CreateInstance(type);
                    if (!string.IsNullOrEmpty(inst?.DisplayName)) return inst.DisplayName;
                }
                catch { /* ignore */ }
            }

            return FriendlyTypeName(type.Name);
        }

        private static string GetModuleDisplayName(object instance)
        {
            if (instance == null) return "(None)";

            if (instance is IAnimationModule am && !string.IsNullOrEmpty(am.DisplayName))
                return am.DisplayName;

            if (instance is IBehaviorModule bm && !string.IsNullOrEmpty(bm.DisplayName))
                return bm.DisplayName;

            return FriendlyTypeName(instance.GetType().Name);
        }

        private static string FriendlyTypeName(string name)
        {
            if (name.EndsWith("Module", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Module".Length);
            if (name.EndsWith("Behavior", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Behavior".Length);
            return name;
        }

        // ── Expand state helpers ───────────────────────────────────────────────

        private static bool GetExpanded(string key)
        {
            _expandedState.TryGetValue(key, out bool val);
            return val;
        }

        private static void SetExpanded(string key, bool value)
        {
            _expandedState[key] = value;
        }

        // ── Cache invalidation ─────────────────────────────────────────────────

        private static void InvalidateListCache(string key)
        {
            _listCache.Remove(key);
        }
    }
}
