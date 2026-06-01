using System.Collections.Generic;
using System.Linq;
using PugMod;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// MonoBehaviour on the Content GameObject (= UIScrollWindow.scrollingContent).
    /// Owns a small fixed pool of ItemRow GameObjects and recycles them as the
    /// scroll position changes, so only the rows visible in the viewport exist
    /// instead of one GameObject per catalog entry (~10720).
    ///
    /// DefaultExecutionOrder(-100): self-register as UIScrollWindow._scrollable in
    /// Awake before UIScrollWindow.Awake runs. Defensive only — the prefab also
    /// wires `scrollable` directly, so UIScrollWindow.Awake sets _scrollable too.
    /// Load-bearing prerequisite: UIScrollWindow.Awake sets enabled=false
    /// PERMANENTLY if its serialized `scrollable` field is not an IScrollable, and
    /// a disabled UIScrollWindow never runs LateUpdate (scroll-recycle silently
    /// stops). The prefab must keep `scrollable` pointing at this component.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ItemChecklistContent : UIelement, IScrollable
    {
        public float RowHeight = ItemRow.RowHeight;

        // Used only if UIScrollWindow.windowHeight is unavailable/zero.
        private const float FallbackWindowHeight = 9.25f;

        private static readonly MemberInfo MiScrollable =
            typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "_scrollable");

        private readonly List<ItemRow> _pool = new List<ItemRow>();
        private GameObject _rowPrefab;
        private UIScrollWindow _scrollWindow;
        private int _count;            // reported entry count (catalog.Count)
        private int _lastFirstIndex = -1;

        // Iter-6: the label's prefab default colour, used for Common/Poor names
        // (GetSlotBorderRarityColor returns this for them). Captured once from
        // the first pooled row's PugText (whose getter returns style.color before
        // any tint), so a future prefab style change is picked up automatically.
        private Color _defaultLabelColor = Color.white;
        private bool _defaultLabelColorCaptured;

        public int PoolSize => _pool.Count;

        private void Awake()
        {
            _scrollWindow = GetComponentInParent<UIScrollWindow>(true);
            if (_scrollWindow != null && MiScrollable != null)
                API.Reflection.SetValue(MiScrollable, _scrollWindow, this);
        }

        /// <summary>Idempotent: stores the row prefab used to build the pool.</summary>
        public void Init(GameObject rowPrefab)
        {
            _rowPrefab = rowPrefab;
            if (_scrollWindow == null)
                _scrollWindow = GetComponentInParent<UIScrollWindow>(true);
        }

        /// <summary>
        /// Instantiate the row pool up to the size the current viewport needs.
        /// Idempotent and grows-only: a no-op once the pool already covers
        /// ComputePoolSize(), but tops up if a later call needs more rows (e.g.
        /// the first build happened before windowHeight was known).
        /// </summary>
        public void EnsurePool()
        {
            if (_rowPrefab == null) return;
            int target = ComputePoolSize();
            for (int k = _pool.Count; k < target; k++)
            {
                var go = Object.Instantiate(_rowPrefab, transform);
                _pool.Add(go.GetComponent<ItemRow>());
            }
            if (!_defaultLabelColorCaptured && _pool.Count > 0
                && _pool[0] != null && _pool[0].label != null)
            {
                _defaultLabelColor = _pool[0].label.color;   // PugText.color getter → style.color
                _defaultLabelColorCaptured = true;
            }
        }

        private int ComputePoolSize()
        {
            float wh = (_scrollWindow != null && _scrollWindow.windowHeight > 0f)
                ? _scrollWindow.windowHeight
                : FallbackWindowHeight;
            return Mathf.CeilToInt(wh / RowHeight) + 2;   // +2 buffer: partial row top + bottom
        }

        /// <summary>Set the total entry count; drives reported height + scrollbar.</summary>
        public void SetCount(int count) => _count = count;

        /// <summary>
        /// Forced rebind of the visible window, bypassing the frame-saving guard.
        /// Used by every non-scroll trigger (open, discovery, re-bake) where the
        /// index may be unchanged but the binding is stale.
        /// </summary>
        public void RefreshVisible()
        {
            _lastFirstIndex = -1;
            UpdateContainingElements(transform.localPosition.y);
        }

        // IScrollable ---------------------------------------------------------

        public void UpdateContainingElements(float scroll)
        {
            int first = ClampFirstIndex(Mathf.FloorToInt(scroll / RowHeight));
            if (first == _lastFirstIndex) return;
            Rebind(first);
        }

        public bool IsBottomElementSelected() => false;
        public bool IsTopElementSelected() => false;
        public float GetCurrentWindowHeight() => _count * RowHeight;

        // ---------------------------------------------------------------------

        private int ClampFirstIndex(int first)
        {
            if (_count <= 0) return 0;
            return Mathf.Clamp(first, 0, Mathf.Max(0, _count - 1));
        }

        private void Rebind(int first)
        {
            _lastFirstIndex = first;
            var catalog = ItemChecklistMod.Catalog;
            var model = ItemChecklistMod.ListView;
            var state = DiscoveredState.Instance;
            for (int k = 0; k < _pool.Count; k++)
            {
                var row = _pool[k];
                if (row == null) continue;
                int displayIdx = first + k;
                if (catalog == null || model == null || state == null || displayIdx >= _count)
                {
                    if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                    continue;
                }
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                row.transform.localPosition = new Vector3(0f, -(displayIdx * RowHeight), 0f);
                int catalogIdx = model.Order[displayIdx];
                var entry = catalog.GetByIndex(catalogIdx);
                // CK-authoritative rarity colour. useDefaultColorForCommon: true →
                // Common/Poor resolve to the label's normal colour (no visible tint),
                // Uncommon+ get slotBorderRarityColors[(int)(rarity+1)].
                Color rarityColor = Manager.ui.GetSlotBorderRarityColor(
                    entry.Rarity, useDefaultColorForCommon: true, defaultColor: _defaultLabelColor);
                row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName,
                    state.IsDiscovered(entry.ObjectId, entry.Variation),
                    rarityColor, entry.Rarity);
            }
        }

        private void OnDestroy()
        {
            // PugText shared-pool hygiene (IB BasicEntriesListRenderer.ClearList):
            // release pool resources before the GameObjects are torn down.
            foreach (var row in _pool)
            {
                if (row == null) continue;
                foreach (var pugText in row.GetComponentsInChildren<PugText>(true))
                    pugText.Clear();
            }
            _pool.Clear();
        }
    }
}
