using System;
using UnityEngine;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Đánh dấu field [SerializeReference] để Editor hiển thị dropdown chọn concrete subclass.
    /// Custom PropertyDrawer trong DreamTech.UICore.Editor pick up attribute này và render
    /// dropdown tất cả các type implement interface / kế thừa base class của field.
    /// <example>
    /// <code>
    /// [SerializeReference, SubclassSelector]
    /// private List&lt;IAnimationModule&gt; _modules = new List&lt;IAnimationModule&gt;();
    /// </code>
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
