using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Reusable dropdown. Closed: the header shows the selected option. Open: a
    /// flush list of the NON-selected options sits directly under the header.
    /// Configure with labels + selected index + onSelected callback. Iter-7 uses
    /// it for the sort mode; Iter-8 reuses it for the discovery filter.
    /// </summary>
    public sealed class DropdownWidget : UIelement
    {
        // Editor-wired serialized fields.
        public PugText selectedLabel;          // header: shows the current option text
        public DropdownToggleButton toggle;    // open/close button (carries the caret)
        public SpriteRenderer caret;           // ui_group_expand/collapse glyph
        public GameObject popupPanel;          // toggled container holding the option rows
        public Transform rowContainer;         // parent for cloned option rows
        public GameObject rowTemplate;         // one option-row prefab (inactive)
        public Sprite caretClosed;             // ui_group_expand (collapsed)
        public Sprite caretOpen;               // ui_group_collapse (expanded)
        public float rowSpacing = 0.7f;        // compact option spacing (NOT the big list RowHeight)

        private readonly List<DropdownOptionButton> _rows = new List<DropdownOptionButton>();
        private readonly List<PugText> _rowLabels = new List<PugText>();
        private readonly List<SpriteRenderer> _rowSelectedMarks = new List<SpriteRenderer>();
        private string[] _labels = Array.Empty<string>();
        private Action<int> _onSelected;
        private int _selected;
        private bool _open;
        private bool _armed;   // click-outside-close arming: skip the frame the popup opened

        public void Configure(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelected)
        {
            _onSelected = onSelected;
            _labels = new string[labels.Count];
            for (int i = 0; i < labels.Count; i++) _labels[i] = labels[i];
            EnsurePool(Mathf.Max(0, _labels.Length - 1));
            _selected = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, _labels.Length - 1));
            RenderHeader();
            RebuildList();
            SetOpen(false);
            if (toggle != null) toggle.owner = this;
        }

        private void EnsurePool(int n)
        {
            if (rowTemplate == null || rowContainer == null) return;
            for (int i = _rows.Count; i < n; i++)
            {
                var go = UnityEngine.Object.Instantiate(rowTemplate, rowContainer);
                var btn = go.GetComponent<DropdownOptionButton>();
                if (btn == null)
                {
                    Debug.LogError("[ItemChecklist] DropdownWidget rowTemplate is missing a DropdownOptionButton component");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }
                btn.owner = this;
                _rows.Add(btn);
                _rowLabels.Add(FindLabel(go));
                _rowSelectedMarks.Add(FindSelectedMark(go));
            }
        }

        private void RenderHeader()
        {
            if (selectedLabel != null && _selected >= 0 && _selected < _labels.Length)
                selectedLabel.Render(_labels[_selected]);
        }

        /// <summary>Lay out the NON-selected options flush under the header.</summary>
        private void RebuildList()
        {
            int pos = 0;
            for (int opt = 0; opt < _labels.Length; opt++)
            {
                if (opt == _selected) continue;          // selected lives in the header
                if (pos >= _rows.Count) break;
                var btn = _rows[pos];
                btn.index = opt;                          // clicking selects this option
                if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
                btn.transform.localPosition = new Vector3(0f, -((pos + 1) * rowSpacing), 0f);
                if (_rowLabels[pos] != null) _rowLabels[pos].Render(_labels[opt]);
                if (_rowSelectedMarks[pos] != null) _rowSelectedMarks[pos].enabled = false;
                pos++;
            }
            for (int i = pos; i < _rows.Count; i++)
                if (_rows[i].gameObject.activeSelf) _rows[i].gameObject.SetActive(false);
        }

        public void SelectOption(int optionIndex)
        {
            _selected = optionIndex;
            RenderHeader();
            RebuildList();
            SetOpen(false);
            _onSelected?.Invoke(_selected);
        }

        public void TogglePopup() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (popupPanel != null) popupPanel.SetActive(open);
            if (caret != null) caret.sprite = open ? caretOpen : caretClosed;
        }

        // Click-outside-to-close. Runs in LateUpdate (after CK's UIMouse has
        // processed clicks this frame): an option/toggle click has already set
        // _open=false via SelectOption/TogglePopup, so only a genuine OUTSIDE
        // click reaches here with _open still true. The _armed guard skips the
        // frame the popup was opened on, so the opening click doesn't close it.
        private void LateUpdate()
        {
            if (!_open) { _armed = false; return; }
            if (!_armed) { _armed = true; return; }
            if (Input.GetMouseButtonDown(0))
                SetOpen(false);
        }

        // Template child lookup — child names must match the prefab authoring.
        private static PugText FindLabel(GameObject row) =>
            row.transform.Find("Label")?.GetComponent<PugText>();
        private static SpriteRenderer FindSelectedMark(GameObject row) =>
            row.transform.Find("SelectedMark")?.GetComponent<SpriteRenderer>();
    }
}
