using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DreamTech.UICore.Animations.Modules;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Drawers
{
    /// <summary>
    /// Custom PropertyDrawer cho [SerializeReference, SubclassSelector].
    /// Renders a dropdown listing all concrete types that implement the field's base
    /// type (interface or abstract class). User selects a type → instantiated via
    /// Activator.CreateInstance and assigned to managedReferenceValue.
    ///
    /// Discovery: scans ALL loaded assemblies at first call per base type; results
    /// cached in _subclassCache. Custom modules (any [Serializable] class with a
    /// public no-arg ctor implementing IAnimationModule) in OTHER assemblies
    /// (e.g. the game project's asmdef) are included automatically.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, List<Type>> _subclassCache = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, true);

            float dropdownHeight = EditorGUIUtility.singleLineHeight + 2f;
            float contentHeight = property.managedReferenceValue != null
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

            // ── Dropdown row ──────────────────────────────────────────────────
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

            // Draw label prefix (matches Unity's standard property label indent)
            Rect labelRect = new Rect(dropdownRect.x, dropdownRect.y,
                EditorGUIUtility.labelWidth, dropdownRect.height);
            Rect btnRect = new Rect(dropdownRect.x + EditorGUIUtility.labelWidth, dropdownRect.y,
                dropdownRect.width - EditorGUIUtility.labelWidth, dropdownRect.height);

            EditorGUI.LabelField(labelRect, label);

            if (EditorGUI.DropdownButton(btnRect, new GUIContent(currentTypeName), FocusType.Keyboard))
            {
                ShowTypeMenu(property, baseType);
            }

            // ── Content below dropdown ────────────────────────────────────────
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

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Type GetManagedReferenceBaseType(SerializedProperty property)
        {
            // managedReferenceFieldTypename format: "<assembly> <full.type.name>"
            string typeStr = property.managedReferenceFieldTypename;
            if (string.IsNullOrEmpty(typeStr)) return null;

            int spaceIndex = typeStr.IndexOf(' ');
            if (spaceIndex < 0) return null;

            string assemblyName = typeStr.Substring(0, spaceIndex);
            string typeName = typeStr.Substring(spaceIndex + 1);

            // Try assembly-qualified form first
            Type t = Type.GetType($"{typeName}, {assemblyName}");
            if (t != null) return t;

            // Fallback: search by type name across all assemblies (handles nested namespaces)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        private static string GetCurrentTypeDisplayName(SerializedProperty property)
        {
            if (property.managedReferenceValue == null) return "(None)";
            Type t = property.managedReferenceValue.GetType();

            if (typeof(IAnimationModule).IsAssignableFrom(t))
            {
                try
                {
                    var instance = (IAnimationModule)property.managedReferenceValue;
                    if (!string.IsNullOrEmpty(instance.DisplayName)) return instance.DisplayName;
                }
                catch { /* ignore — access to live instance during drawcall can throw */ }
            }

            return FriendlyTypeName(t.Name);
        }

        private static void ShowTypeMenu(SerializedProperty property, Type baseType)
        {
            // Capture path + serializedObject because property is invalidated after menu closes
            SerializedObject so = property.serializedObject;
            string propertyPath = property.propertyPath;

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(None)"), property.managedReferenceValue == null, () =>
            {
                var prop = so.FindProperty(propertyPath);
                if (prop == null) return;
                prop.managedReferenceValue = null;
                so.ApplyModifiedProperties();
            });
            menu.AddSeparator("");

            foreach (var type in GetCompatibleTypes(baseType))
            {
                string displayName = GetDisplayNameForType(type);
                Type capturedType = type;
                bool isCurrent = property.managedReferenceValue?.GetType() == capturedType;

                menu.AddItem(new GUIContent(displayName), isCurrent, () =>
                {
                    var prop = so.FindProperty(propertyPath);
                    if (prop == null) return;
                    prop.managedReferenceValue = Activator.CreateInstance(capturedType);
                    so.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static List<Type> GetCompatibleTypes(Type baseType)
        {
            if (_subclassCache.TryGetValue(baseType, out var cached)) return cached;

            var types = new List<Type>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] asmTypes;
                try { asmTypes = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    // Partial load — use whatever loaded successfully
                    asmTypes = ex.Types;
                }

                if (asmTypes == null) continue;

                foreach (var t in asmTypes)
                {
                    if (t == null) continue;
                    if (t.IsAbstract || t.IsInterface) continue;
                    if (!baseType.IsAssignableFrom(t)) continue;
                    if (t.GetCustomAttribute<SerializableAttribute>() == null) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    types.Add(t);
                }
            }

            types = types.OrderBy(t => GetDisplayNameForType(t)).ToList();
            _subclassCache[baseType] = types;
            return types;
        }

        private static string GetDisplayNameForType(Type type)
        {
            if (typeof(IAnimationModule).IsAssignableFrom(type))
            {
                try
                {
                    var inst = (IAnimationModule)Activator.CreateInstance(type);
                    if (!string.IsNullOrEmpty(inst?.DisplayName)) return inst.DisplayName;
                }
                catch { /* ignore — DisplayName is cosmetic */ }
            }
            return FriendlyTypeName(type.Name);
        }

        private static string FriendlyTypeName(string name)
        {
            // Strip common suffixes for cleaner display: "ScaleModule" → "Scale"
            if (name.EndsWith("Module", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Module".Length);
            return name;
        }
    }
}
