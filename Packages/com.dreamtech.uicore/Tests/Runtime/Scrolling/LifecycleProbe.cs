using DreamTech.UICore.Scrolling;
using UnityEngine;

namespace DreamTech.UICore.Tests.Scrolling
{
    /// <summary>Đếm số lần view được bind / trả về pool (test gắn lên template).</summary>
    public sealed class LifecycleProbe : MonoBehaviour, IVirtualItemLifecycle
    {
        public int Bound;
        public int Recycled;

        public void OnItemBound(VirtualItem item) => Bound++;

        public void OnItemRecycled(VirtualItem item) => Recycled++;
    }
}
