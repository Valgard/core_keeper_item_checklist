using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Click target that toggles its owning DropdownWidget's popup.</summary>
    public sealed class DropdownToggleButton : ButtonUIElement
    {
        public DropdownWidget owner;
        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            if (!canBeClicked) return;
            base.OnLeftClicked(mod1, mod2);
            if (owner != null) owner.TogglePopup();
        }
    }
}
