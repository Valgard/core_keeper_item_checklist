using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Recycler-pattern virtualized list. Maintains a pool of ~30
    /// ItemRowView GameObjects, repositions and rebinds them as the user
    /// scrolls. Driven by a flat int[] of indices into the consumer's
    /// data source plus a row-data resolver callback.
    /// </summary>
    public sealed class VirtualScrollList
    {
        private const int PoolSize = 30;

        private readonly ScrollRect scrollRect;
        private readonly RectTransform content;
        private readonly ItemRowView rowPrefab;
        private readonly Func<int, (int objectId, Sprite icon, string name, bool isDiscovered)> rowDataAt;
        private readonly List<ItemRowView> pool = new List<ItemRowView>();

        private int[] visibleIndices = Array.Empty<int>();

        public VirtualScrollList(ScrollRect scrollRect, RectTransform content, ItemRowView rowPrefab,
                                 Func<int, (int, Sprite, string, bool)> rowDataAt)
        {
            this.scrollRect = scrollRect;
            this.content = content;
            this.rowPrefab = rowPrefab;
            this.rowDataAt = rowDataAt;
            scrollRect.onValueChanged.AddListener(_ => Repaint());

            for (int i = 0; i < PoolSize; i++)
            {
                var row = UnityEngine.Object.Instantiate(rowPrefab, content);
                row.gameObject.SetActive(false);
                pool.Add(row);
            }
        }

        public void SetIndices(int[] indices)
        {
            visibleIndices = indices ?? Array.Empty<int>();
            content.sizeDelta = new Vector2(content.sizeDelta.x,
                                            visibleIndices.Length * ItemRowView.RowHeight);
            scrollRect.verticalNormalizedPosition = 1f;     // top
            Repaint();
        }

        private void Repaint()
        {
            if (visibleIndices.Length == 0)
            {
                foreach (var r in pool) r.gameObject.SetActive(false);
                return;
            }

            float viewportH = scrollRect.viewport.rect.height;
            float scrollPx = content.anchoredPosition.y;
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollPx / ItemRowView.RowHeight));
            int last = Mathf.Min(visibleIndices.Length - 1,
                                 first + Mathf.CeilToInt(viewportH / ItemRowView.RowHeight) + 1);

            int p = 0;
            for (int i = first; i <= last && p < pool.Count; i++, p++)
            {
                var row = pool[p];
                row.gameObject.SetActive(true);
                var rt = (RectTransform) row.transform;
                rt.anchoredPosition = new Vector2(0, -i * ItemRowView.RowHeight);

                var (oid, icon, name, isDisc) = rowDataAt(visibleIndices[i]);
                row.Bind(oid, icon, name, isDisc);
            }
            for (; p < pool.Count; p++) pool[p].gameObject.SetActive(false);
        }
    }
}
