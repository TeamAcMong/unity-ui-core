using DreamTech.UICore.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Menus
{
    /// <summary>
    /// Gắn <see cref="ButtonFeedback"/> cho mọi Button / Toggle dưới các object đang chọn (có Undo). Bỏ qua nút đã có, và nút có
    /// tên kết thúc bằng hậu tố loại trừ của profile mặc định.
    /// </summary>
    internal static class ButtonFeedbackMenu
    {
        private const string MenuPath = "GameObject/DreamTech UI Core/Add Button Feedback To Buttons Under Selection";

        [MenuItem(MenuPath, false, 20)]
        private static void AddToSelection()
        {
            ButtonFeedbackProfile profile = ButtonFeedback.DefaultProfile != null
                ? ButtonFeedback.DefaultProfile
                : ButtonFeedbackProfile.BuiltInDefault;
            int added = 0;
            foreach (GameObject root in Selection.gameObjects)
            {
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                {
                    if (!(selectable is Button) && !(selectable is Toggle)) continue;
                    if (selectable.GetComponent<ButtonFeedback>() != null) continue;
                    string suffix = profile.ExcludeSuffix;
                    if (!string.IsNullOrEmpty(suffix) &&
                        selectable.name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) continue;
                    Undo.AddComponent<ButtonFeedback>(selectable.gameObject);
                    added++;
                }
            }
            Debug.Log("[DreamTech UI Core] Đã gắn Button Feedback cho " + added + " nút.");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAddToSelection() => Selection.gameObjects.Length > 0;
    }
}
