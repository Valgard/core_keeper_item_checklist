using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Root component of <c>ItemChecklistWindow.prefab</c>. Inherits from
    /// CK's abstract <c>UIelement</c> base — this is what (a) registers
    /// the window with CK's modal-UI tracking so gameplay input
    /// (WASD/actions) is blocked while shown, and (b) puts the prefab in
    /// CK's UICamera-rooted UI layer so the hardware cursor renders on
    /// top.
    ///
    /// <para>Same Item Browser pattern (<c>ItemBrowserUI : ItemBrowserView
    /// : UIelement</c>). UIelement has no abstract members — we don't
    /// have to override anything.</para>
    /// </summary>
    public sealed class ItemChecklistWindowView : UIelement
    {
        [SerializeField] public InputField searchField;
        [SerializeField] public Dropdown filterDropdown;
        [SerializeField] public ScrollRect scrollRect;
        [SerializeField] public RectTransform rowContainer;
        [SerializeField] public ItemRowView rowPrefab;
        [SerializeField] public PugText counterLabel;
        [SerializeField] public Button closeButton;
    }
}
