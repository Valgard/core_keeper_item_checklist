using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Reusable dropdown. Configure with option labels + a selected index + an
    /// onSelected callback. Iter-7 instantiates it for the sort mode; Iter-8 for
    /// the discovery filter — no widget changes needed. Click-outside-to-close is
    /// added in a later task (the popup currently closes only via selecting a row
    /// or re-clicking the toggle).
    /// </summary>
    public sealed class DropdownWidget : UIelement
    {
        // Editor-wired serialized fields (authored into the prefab in a later task).
        public PugText selectedLabel;          // shows the current option text
        public DropdownToggleButton toggle;    // open/close button (carries the caret)
        public SpriteRenderer caret;           // ui_group_expand/collapse glyph
        public GameObject popupPanel;          // toggled container holding the rows
        public Transform rowContainer;         // parent for cloned option rows
        public GameObject rowTemplate;         // one option-row prefab (inactive)
        public Sprite caretClosed;             // ui_group_expand (collapsed)
        public Sprite caretOpen;               // ui_group_collapse (expanded)

        private readonly List<DropdownOptionButton> _rows = new List<DropdownOptionButton>();
        private readonly List<PugText> _rowLabels = new List<PugText>();
        private readonly List<SpriteRenderer> _rowSelectedMarks = new List<SpriteRenderer>();
        private string[] _labels = Array.Empty<string>();
        private Action<int> _onSelected;
        private int _selected;
        private bool _open;

        public void Configure(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelected)
        {
            _onSelected = onSelected;
            _labels = new string[labels.Count];
            for (int i = 0; i < labels.Count; i++) _labels[i] = labels[i];
            BuildRows();
            SetSelected(selectedIndex, fire: false);
            SetOpen(false);
            if (toggle != null) toggle.owner = this;
        }

        private void BuildRows()
        {
            if (rowTemplate == null || rowContainer == null) return;
            // Clone-or-reuse: grow the row pool to _labels.Length (mirrors EnsurePool).
            for (int i = _rows.Count; i < _labels.Length; i++)
            {
                var go = UnityEngine.Object.Instantiate(rowTemplate, rowContainer);
                go.transform.localPosition = new Vector3(0f, -(i * ItemRow.RowHeight), 0f);
                var btn = go.GetComponent<DropdownOptionButton>();
                if (btn == null)
                {
                    Debug.LogError("[ItemChecklist] DropdownWidget rowTemplate is missing a DropdownOptionButton component");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }
                btn.owner = this;
                btn.index = i;
                _rows.Add(btn);
                _rowLabels.Add(FindLabel(go));
                _rowSelectedMarks.Add(FindSelectedMark(go));
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _labels.Length;
                _rows[i].gameObject.SetActive(used);
                if (used && _rowLabels[i] != null) _rowLabels[i].Render(_labels[i]);
            }
        }

        public void TogglePopup() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (popupPanel != null) popupPanel.SetActive(open);
            if (caret != null) caret.sprite = open ? caretOpen : caretClosed;
        }

        public void SelectOption(int index)
        {
            SetSelected(index, fire: true);
            SetOpen(false);
        }

        private void SetSelected(int index, bool fire)
        {
            _selected = index;
            if (selectedLabel != null && index >= 0 && index < _labels.Length)
                selectedLabel.Render(_labels[index]);
            for (int i = 0; i < _rowSelectedMarks.Count; i++)
                if (_rowSelectedMarks[i] != null)
                    _rowSelectedMarks[i].enabled = (i == index);
            if (fire) _onSelected?.Invoke(index);
        }

        // Template child lookup — child names must match the prefab authoring task.
        private static PugText FindLabel(GameObject row) =>
            row.transform.Find("Label")?.GetComponent<PugText>();
        private static SpriteRenderer FindSelectedMark(GameObject row) =>
            row.transform.Find("SelectedMark")?.GetComponent<SpriteRenderer>();
    }
}
