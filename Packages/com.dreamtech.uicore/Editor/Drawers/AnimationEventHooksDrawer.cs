using DreamTech.UICore.Animations.Events;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Drawers
{
    /// <summary>
    /// Custom drawer cho <see cref="AnimationEventHooks"/> — render 3 UnityEvent inline
    /// thay vì wrap trong foldout cha. Mỗi UnityEvent vẫn giữ foldout mặc định của Unity,
    /// nhưng không còn lớp foldout "Animation Event Hooks" thừa ở trên cùng.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimationEventHooks))]
    public class AnimationEventHooksDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float total = 0f;
            int count = 0;
            SerializedProperty iter = property.Copy();
            SerializedProperty end  = iter.GetEndProperty();
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iter, end)) break;
                total += EditorGUI.GetPropertyHeight(iter, true);
                count++;
            }
            if (count > 0)
                total += EditorGUIUtility.standardVerticalSpacing * (count - 1);
            return total;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float y = position.y;
            SerializedProperty iter = property.Copy();
            SerializedProperty end  = iter.GetEndProperty();
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iter, end)) break;
                float h = EditorGUI.GetPropertyHeight(iter, true);
                Rect r = new Rect(position.x, y, position.width, h);
                EditorGUI.PropertyField(r, iter, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }
}
