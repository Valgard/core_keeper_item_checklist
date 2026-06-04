using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Multi-select, sectioned filter popup. Closed: the header shows
    /// "Filter" / "Filter (N)" with N = active member count. Open: a scrollable
    /// list of section headers + checkbox rows, plus a "Clear all" row.
    /// OR within a section, AND across sections — the semantics live in
    /// ItemListViewModel; this widget only renders state and reports clicks.
    /// Reuses the Iter-5 clickable-control pattern (SpriteRenderer + 3D
    /// BoxCollider + ButtonUIElement) via FacetCheckboxButton.
    /// </summary>
    public sealed class FacetedFilterWidget : UIelement
    {
        // Editor-wired serialized fields.
        public PugText headerLabel;            // "Filter" / "Filter (N)"
        public FacetToggleButton toggle;       // header open/close button
        public SpriteRenderer caret;
        public GameObject popupPanel;          // toggled container
        public Transform rowContainer;         // parent for cloned rows + headers
        public GameObject checkboxTemplate;    // one FacetCheckboxButton row (inactive)
        public GameObject headerTemplate;      // one section-header PugText row (inactive)
        public Sprite caretClosed;
        public Sprite caretOpen;
        public float rowSpacing = 0.7f;

        // One row in the flat member table.
        private struct Member
        {
            public string section;     // section header text (for grouping/rendering)
            public string label;       // member label
            public Func<bool> isOn;    // current checked state
            public Action toggle;      // flip the model dimension
        }

        private readonly List<Member> _members = new List<Member>();
        private readonly List<FacetCheckboxButton> _rowPool = new List<FacetCheckboxButton>();
        private readonly List<PugText> _headerPool = new List<PugText>();
        private Action _onAnyChange;   // refresh header count after a toggle
        private Action _clearAll;
        private bool _open;
        private bool _armed;

        /// <summary>(Re)build the member table from the model + a label provider,
        /// then lay out the popup. Called from WireControls.</summary>
        public void Configure(IList<(string section, string label, Func<bool> isOn, Action toggle)> members,
            Func<int> activeCount, Action clearAll)
        {
            _members.Clear();
            foreach (var m in members)
                _members.Add(new Member { section = m.section, label = m.label, isOn = m.isOn, toggle = m.toggle });
            _onAnyChange = () => RenderHeader(activeCount);
            _clearAll = clearAll;
            EnsurePools();
            RenderHeader(activeCount);
            RebuildList();
            SetOpen(false);
            if (toggle != null) toggle.owner = this;
        }

        private void EnsurePools()
        {
            if (checkboxTemplate == null || rowContainer == null) return;
            for (int i = _rowPool.Count; i < _members.Count; i++)
            {
                var go = UnityEngine.Object.Instantiate(checkboxTemplate, rowContainer);
                var btn = go.GetComponent<FacetCheckboxButton>();
                if (btn == null) { UnityEngine.Object.Destroy(go); continue; }
                btn.owner = this;
                btn.memberId = i;
                _rowPool.Add(btn);
            }
            // section-header pool: at most one header per member (over-allocates
            // slightly, simpler than counting distinct sections up front).
            if (headerTemplate != null)
                for (int i = _headerPool.Count; i < _members.Count + 1; i++)
                {
                    var go = UnityEngine.Object.Instantiate(headerTemplate, rowContainer);
                    _headerPool.Add(go.transform.Find("Label")?.GetComponent<PugText>());
                }
        }

        private void RenderHeader(Func<int> activeCount)
        {
            int n = activeCount != null ? activeCount() : 0;
            if (headerLabel != null) headerLabel.Render(n > 0 ? $"Filter ({n})" : "Filter");
        }

        /// <summary>Lay out section headers + checkbox rows top-to-bottom.</summary>
        private void RebuildList()
        {
            int pos = 0;           // vertical slot counter
            int rowIdx = 0;        // checkbox-pool cursor
            int headerIdx = 0;     // header-pool cursor
            string lastSection = null;

            for (int m = 0; m < _members.Count; m++)
            {
                // New section → place a header row first.
                if (_members[m].section != lastSection)
                {
                    lastSection = _members[m].section;
                    if (!string.IsNullOrEmpty(lastSection) && headerIdx < _headerPool.Count && _headerPool[headerIdx] != null)
                    {
                        var ht = _headerPool[headerIdx];
                        ht.transform.parent.localPosition = new Vector3(0f, -(pos * rowSpacing), 0f);
                        if (!ht.transform.parent.gameObject.activeSelf) ht.transform.parent.gameObject.SetActive(true);
                        ht.Render(lastSection);   // colour is set on the headerTemplate PugText style in the prefab (gray)
                        headerIdx++; pos++;
                    }
                }
                if (rowIdx >= _rowPool.Count) break;
                var btn = _rowPool[rowIdx];
                if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
                btn.memberId = m;
                btn.transform.localPosition = new Vector3(0f, -(pos * rowSpacing), 0f);
                var label = btn.transform.Find("Label")?.GetComponent<PugText>();
                if (label != null)
                    label.Render((string.IsNullOrEmpty(_members[m].section) ? "" : "  ") + _members[m].label);
                btn.SetChecked(_members[m].isOn());
                rowIdx++; pos++;
            }
            // Hide unused pooled rows/headers.
            for (int i = rowIdx; i < _rowPool.Count; i++)
                if (_rowPool[i].gameObject.activeSelf) _rowPool[i].gameObject.SetActive(false);
            for (int i = headerIdx; i < _headerPool.Count; i++)
            {
                var p = _headerPool[i]?.transform.parent;
                if (p != null && p.gameObject.activeSelf) p.gameObject.SetActive(false);
            }
        }

        public void OnMemberClicked(int memberId)
        {
            if (memberId < 0 || memberId >= _members.Count) return;
            _members[memberId].toggle();
            if (memberId < _rowPool.Count) _rowPool[memberId].SetChecked(_members[memberId].isOn());
            _onAnyChange?.Invoke();
        }

        public void ClearAll()
        {
            _clearAll?.Invoke();
            for (int i = 0; i < _members.Count && i < _rowPool.Count; i++)
                _rowPool[i].SetChecked(_members[i].isOn());
            _onAnyChange?.Invoke();
        }

        public void TogglePopup() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (popupPanel != null) popupPanel.SetActive(open);
            if (caret != null) caret.sprite = open ? caretOpen : caretClosed;
            if (open) RebuildList();   // reflect external changes (e.g. re-bake)
        }

        // Click-outside-to-close (identical pattern to DropdownWidget.LateUpdate).
        private void LateUpdate()
        {
            if (!_open) { _armed = false; return; }
            if (!_armed) { _armed = true; return; }
            if (Input.GetMouseButtonDown(0)) SetOpen(false);
        }
    }
}
