using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Behaviors;
using DreamTech.UICore.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Drawers
{
    /// <summary>
    /// Custom PropertyDrawer for [SerializeReference, SubclassSelector].
    /// Renders a dropdown listing all concrete types that implement the field's base
    /// type (interface or abstract class). User selects a type → instantiated via
    /// Activator.CreateInstance and assigned to managedReferenceValue.
    ///
    /// Discovery: scans ALL loaded assemblies at first call per base type; results
    /// cached in _subclassCache. Custom modules in OTHER assemblies are included automatically.
    ///
    /// Clicking the dropdown opens a <see cref="SubclassPopupContent"/> with real-time
    /// search filtering and Built-in / Custom grouping.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, List<Type>>   _subclassCache    = new Dictionary<Type, List<Type>>();
        private static readonly Dictionary<Type, string>        _displayNameCache = new Dictionary<Type, string>();

        private const string BuiltInNamespace = "DreamTech.UICore";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, true);

            float dropdownHeight = EditorGUIUtility.singleLineHeight + 2f;
            float contentHeight  = property.managedReferenceValue != null
                ? EditorGUI.GetPropertyHeight(property, GUIContent.none, true)
                : 0f;
            return dropdownHeight + contentHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // ── Dropdown row ─────────────────────────────────────────────────
            Rect dropdownRect = new Rect(position.x, position.y, position.width,
                EditorGUIUtility.singleLineHeight);

            Type baseType = GetManagedReferenceBaseType(property);
            if (baseType == null)
            {
                EditorGUI.LabelField(dropdownRect, label,
                    new GUIContent("(SubclassSelector: cannot resolve base type)"));
                return;
            }

            string currentTypeName = GetCurrentTypeDisplayName(property);

            Rect labelRect = new Rect(dropdownRect.x, dropdownRect.y,
                EditorGUIUtility.labelWidth, dropdownRect.height);
            Rect btnRect = new Rect(
                dropdownRect.x + EditorGUIUtility.labelWidth,
                dropdownRect.y,
                dropdownRect.width - EditorGUIUtility.labelWidth,
                dropdownRect.height);

            EditorGUI.LabelField(labelRect, label);

            if (EditorGUI.DropdownButton(btnRect, new GUIContent(currentTypeName), FocusType.Keyboard))
            {
                ShowSearchablePopup(btnRect, property, baseType);
            }

            // ── Content below dropdown ───────────────────────────────────────
            if (property.managedReferenceValue != null)
            {
                Rect contentRect = new Rect(
                    position.x,
                    position.y + EditorGUIUtility.singleLineHeight + 2f,
                    position.width,
                    position.height - EditorGUIUtility.singleLineHeight - 2f);

                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(contentRect, property, GUIContent.none, true);
                EditorGUI.indentLevel--;
            }
        }

        // ── Searchable popup ─────────────────────────────────────────────────

        private static void ShowSearchablePopup(Rect activatorRect, SerializedProperty property, Type baseType)
        {
            SerializedObject so          = property.serializedObject;
            string           propertyPath = property.propertyPath;
            Type             currentType  = property.managedReferenceValue?.GetType();
            var              allTypes      = GetCompatibleTypes(baseType);

            var content = new SubclassPopupContent(
                allTypes,
                currentType,
                baseType,
                (selectedType) =>
                {
                    if (so == null) return;
                    var prop = so.FindProperty(propertyPath);
                    if (prop == null) return;
                    prop.managedReferenceValue = selectedType != null
                        ? Activator.CreateInstance(selectedType)
                        : null;
                    so.ApplyModifiedProperties();
                });

            PopupWindow.Show(activatorRect, content);
        }

        // ── Legacy GenericMenu fallback (used by ModuleListDrawer internally) ─

        private static void ShowTypeMenu(SerializedProperty property, Type baseType)
        {
            SerializedObject so          = property.serializedObject;
            string           propertyPath = property.propertyPath;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), property.managedReferenceValue == null, () =>
            {
                if (so == null) return;
                var prop = so.FindProperty(propertyPath);
                if (prop == null) return;
                prop.managedReferenceValue = null;
                so.ApplyModifiedProperties();
            });
            menu.AddSeparator("");

            foreach (var type in GetCompatibleTypes(baseType))
            {
                string displayName = GetDisplayNameForType(type);
                Type   captured    = type;
                bool   isCurrent   = property.managedReferenceValue?.GetType() == captured;

                menu.AddItem(new GUIContent(displayName), isCurrent, () =>
                {
                    if (so == null) return;
                    var prop = so.FindProperty(propertyPath);
                    if (prop == null) return;
                    prop.managedReferenceValue = Activator.CreateInstance(captured);
                    so.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        internal static Type GetManagedReferenceBaseType(SerializedProperty property)
        {
            string typeStr = property.managedReferenceFieldTypename;
            if (string.IsNullOrEmpty(typeStr)) return null;

            int spaceIndex = typeStr.IndexOf(' ');
            if (spaceIndex < 0) return null;

            string assemblyName = typeStr.Substring(0, spaceIndex);
            string typeName     = typeStr.Substring(spaceIndex + 1);

            Type t = Type.GetType($"{typeName}, {assemblyName}");
            if (t != null) return t;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        internal static string GetCurrentTypeDisplayName(SerializedProperty property)
        {
            if (property.managedReferenceValue == null) return "(None)";
            Type t = property.managedReferenceValue.GetType();

            if (property.managedReferenceValue is IAnimationModule am)
            {
                try { if (!string.IsNullOrEmpty(am.DisplayName)) return am.DisplayName; }
                catch { /* ignore */ }
            }

            if (property.managedReferenceValue is IBehaviorModule bm)
            {
                try { if (!string.IsNullOrEmpty(bm.DisplayName)) return bm.DisplayName; }
                catch { /* ignore */ }
            }

            return FriendlyTypeName(t.Name);
        }

        internal static List<Type> GetCompatibleTypes(Type baseType)
        {
            if (_subclassCache.TryGetValue(baseType, out var cached)) return cached;

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

            types = types.OrderBy(GetDisplayNameForType).ToList();
            _subclassCache[baseType] = types;
            return types;
        }

        internal static string GetDisplayNameForType(Type type)
        {
            if (_displayNameCache.TryGetValue(type, out var cached)) return cached;

            string name;

            if (typeof(IAnimationModule).IsAssignableFrom(type))
            {
                try
                {
                    var inst = (IAnimationModule)Activator.CreateInstance(type);
                    if (!string.IsNullOrEmpty(inst?.DisplayName))
                    {
                        _displayNameCache[type] = inst.DisplayName;
                        return inst.DisplayName;
                    }
                }
                catch { /* ignore */ }
            }

            if (typeof(IBehaviorModule).IsAssignableFrom(type))
            {
                try
                {
                    var inst = (IBehaviorModule)Activator.CreateInstance(type);
                    if (!string.IsNullOrEmpty(inst?.DisplayName))
                    {
                        _displayNameCache[type] = inst.DisplayName;
                        return inst.DisplayName;
                    }
                }
                catch { /* ignore */ }
            }

            name = FriendlyTypeName(type.Name);
            _displayNameCache[type] = name;
            return name;
        }

        internal static string FriendlyTypeName(string name)
        {
            if (name.EndsWith("Module",   StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Module".Length);
            if (name.EndsWith("Behavior", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Behavior".Length);
            return name;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PopupWindowContent — searchable grouped type picker
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Searchable popup for SubclassSelectorDrawer.
    /// Features: real-time search, Built-in / Custom grouping, keyboard navigation.
    /// </summary>
    internal class SubclassPopupContent : PopupWindowContent
    {
        private const string BuiltInNs = "DreamTech.UICore";
        private const float  WindowW   = 320f;
        private const float  WindowH   = 360f;
        private const float  RowH      = 22f;

        private readonly List<Type>          _allTypes;
        private readonly Type                _currentType;
        private readonly Type                _baseType;
        private readonly Action<Type>        _onSelected;

        private string        _search           = "";
        private Vector2       _scroll;
        private int           _keyboardIndex    = -1;  // index into _selectableIndices
        private List<TypeRow> _visibleRows      = new List<TypeRow>();
        private List<int>     _selectableIndices = new List<int>();  // visibleRow indices of non-header rows
        private bool          _focusDone;

        private struct TypeRow
        {
            public Type   Type;      // null = "(None)"
            public string Label;
            public string TypeName;  // muted type name
            public bool   IsHeader;
            public string HeaderText;
        }

        public SubclassPopupContent(
            List<Type>   allTypes,
            Type         currentType,
            Type         baseType,
            Action<Type> onSelected)
        {
            _allTypes    = allTypes;
            _currentType = currentType;
            _baseType    = baseType;
            _onSelected  = onSelected;
            RebuildRows();
        }

        public override Vector2 GetWindowSize() => new Vector2(WindowW, WindowH);

        public override void OnGUI(Rect rect)
        {
            // ── Search bar ────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();

            GUI.SetNextControlName("SubclassSearch");
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

            if (EditorGUI.EndChangeCheck())
            {
                RebuildRows();
                _keyboardIndex = -1;
            }

            EditorGUILayout.EndHorizontal();

            // Auto-focus search field once — guard prevents stealing focus on every Repaint.
            if (!_focusDone && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl("SubclassSearch");
                _focusDone = true;
            }

            // ── Keyboard navigation ───────────────────────────────────────────
            HandleKeyboard();

            // ── Scrollable list ───────────────────────────────────────────────
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            bool isPro = EditorGUIUtility.isProSkin;

            for (int i = 0; i < _visibleRows.Count; i++)
            {
                var row = _visibleRows[i];

                if (row.IsHeader)
                {
                    // Section header
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    EditorGUILayout.LabelField(row.HeaderText, EditorStyles.miniLabel,
                        GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                    continue;
                }

                bool isCurrent  = row.Type == _currentType;
                bool isSelected = _keyboardIndex >= 0
                    && _keyboardIndex < _selectableIndices.Count
                    && _selectableIndices[_keyboardIndex] == i;

                // Row background
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowH);
                if (isSelected)
                    EditorGUI.DrawRect(rowRect, UIEditorStyles.Accent * new Color(1, 1, 1, 0.25f));
                else if (isCurrent)
                    EditorGUI.DrawRect(rowRect, UIEditorStyles.Accent * new Color(1, 1, 1, 0.12f));

                // Click to select
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    Select(row.Type);
                    Event.current.Use();
                }

                // Checkmark for currently selected
                float cx = rowRect.x + 4f;
                if (isCurrent)
                {
                    EditorGUI.LabelField(new Rect(cx, rowRect.y, 14f, RowH), "✓",
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = UIEditorStyles.Accent },
                        });
                }
                cx += 16f;

                // Display name
                float nameW = rowRect.width - cx - 80f - rowRect.x;
                EditorGUI.LabelField(
                    new Rect(cx, rowRect.y + (RowH - EditorGUIUtility.singleLineHeight) * 0.5f, nameW, EditorGUIUtility.singleLineHeight),
                    row.Label);

                // Type name (muted)
                var mutedStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal    = { textColor = UIEditorStyles.MutedText },
                };
                EditorGUI.LabelField(
                    new Rect(rowRect.xMax - 80f, rowRect.y, 76f, RowH),
                    row.TypeName, mutedStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        public override void OnOpen()
        {
            _search    = "";
            _focusDone = false;
            RebuildRows();
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private void HandleKeyboard()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (_selectableIndices == null || _selectableIndices.Count == 0) return;

            if (e.keyCode == KeyCode.DownArrow)
            {
                _keyboardIndex = Mathf.Min(_keyboardIndex + 1, _selectableIndices.Count - 1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                // Allow navigating to -1 (nothing selected) only from 0; clamp at 0 otherwise.
                _keyboardIndex = Mathf.Max(_keyboardIndex - 1, 0);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (_keyboardIndex >= 0 && _keyboardIndex < _selectableIndices.Count)
                {
                    Select(_visibleRows[_selectableIndices[_keyboardIndex]].Type);
                    e.Use();
                }
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                e.Use();
            }
        }

        private void Select(Type type)
        {
            _onSelected?.Invoke(type);
            editorWindow.Close();
        }

        private void RebuildRows()
        {
            _visibleRows.Clear();
            _selectableIndices.Clear();

            string filter = _search?.Trim() ?? "";

            // "(None)" row
            if (string.IsNullOrEmpty(filter))
            {
                _visibleRows.Add(new TypeRow
                {
                    Type     = null,
                    Label    = "(None)",
                    TypeName = "",
                });
            }

            var filtered = string.IsNullOrEmpty(filter)
                ? _allTypes
                : _allTypes.Where(t =>
                    GetDisplayName(t).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.Name.IndexOf(filter,           StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var builtIn = filtered.Where(t => t.Namespace != null && t.Namespace.StartsWith(BuiltInNs, StringComparison.Ordinal)).ToList();
            var custom  = filtered.Where(t => t.Namespace == null || !t.Namespace.StartsWith(BuiltInNs, StringComparison.Ordinal)).ToList();

            if (builtIn.Count > 0)
            {
                _visibleRows.Add(new TypeRow { IsHeader = true, HeaderText = "Built-in" });
                foreach (var t in builtIn)
                    _visibleRows.Add(MakeRow(t));
            }

            if (custom.Count > 0)
            {
                _visibleRows.Add(new TypeRow { IsHeader = true, HeaderText = "Custom" });
                foreach (var t in custom)
                    _visibleRows.Add(MakeRow(t));
            }

            // Build selectable-index lookup (non-header rows only) for keyboard navigation.
            _selectableIndices.Clear();
            for (int i = 0; i < _visibleRows.Count; i++)
                if (!_visibleRows[i].IsHeader) _selectableIndices.Add(i);

            // Clamp keyboard cursor to valid range after filter change.
            if (_keyboardIndex >= _selectableIndices.Count) _keyboardIndex = _selectableIndices.Count - 1;
            if (_keyboardIndex < -1) _keyboardIndex = -1;
        }

        private TypeRow MakeRow(Type t) => new TypeRow
        {
            Type     = t,
            Label    = GetDisplayName(t),
            TypeName = SubclassSelectorDrawer.FriendlyTypeName(t.Name),
        };

        private string GetDisplayName(Type t) => SubclassSelectorDrawer.GetDisplayNameForType(t);
    }
}
