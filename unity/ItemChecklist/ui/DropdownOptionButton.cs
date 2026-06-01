using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Click target for one option row inside the popup.</summary>
    public sealed class DropdownOptionButton : ButtonUIElement
    {
        public DropdownWidget owner;
        public int index;
        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            if (!canBeClicked) return;
            base.OnLeftClicked(mod1, mod2);
            if (owner != null) owner.SelectOption(index);
        }
    }
}
