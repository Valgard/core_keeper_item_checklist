using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// MonoBehaviour attached to the root of <c>ItemChecklistWindow.prefab</c>.
    /// Each <see cref="SerializeField"/> below appears as a slot in the
    /// Unity Inspector — drag the corresponding GameObject from the
    /// Hierarchy into the slot to wire it up.
    /// </summary>
    public sealed class ItemChecklistWindowView : MonoBehaviour
    {
        [SerializeField] public InputField searchField;
        [SerializeField] public Dropdown filterDropdown;
        [SerializeField] public ScrollRect scrollRect;
        [SerializeField] public RectTransform rowContainer;
        [SerializeField] public ItemRowView rowPrefab;
        [SerializeField] public Text counterLabel;
        [SerializeField] public Button closeButton;
    }
}
