using System.Linq;
using PugMod;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// MonoBehaviour on the Content GameObject (inside RowsContainer).
    /// Implements IScrollable so UIScrollWindow knows the scrollable area's size.
    ///
    /// Critical: DefaultExecutionOrder(-100) makes this Awake fire BEFORE UIScrollWindow.Awake.
    /// In Awake we self-register as UIScrollWindow._scrollable via API.Reflection. Otherwise
    /// UIScrollWindow.Awake sees null _scrollable, logs "does not implement IScrollable",
    /// and disables itself permanently (Awake only runs once per lifetime).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ItemChecklistContent : UIelement, IScrollable
    {
        public int RowCount { get; set; }
        public float RowHeight = ItemRow.RowHeight;

        private static readonly MemberInfo MiScrollable =
            typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "_scrollable");

        private void Awake()
        {
            var sw = GetComponentInParent<UIScrollWindow>(true);
            if (sw != null && MiScrollable != null)
                API.Reflection.SetValue(MiScrollable, sw, this);
        }

        public void UpdateContainingElements(float scroll) { }
        public bool IsBottomElementSelected() => false;
        public bool IsTopElementSelected() => false;
        public float GetCurrentWindowHeight() => RowCount * RowHeight;
    }
}
