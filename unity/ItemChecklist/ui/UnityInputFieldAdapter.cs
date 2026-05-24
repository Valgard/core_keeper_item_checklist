using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Adapter that wraps a <see cref="UnityEngine.UI.InputField"/> so it
    /// satisfies CK's <see cref="InputManager.TextInputInterface"/>. Used
    /// when our UI window is open: passing this adapter to
    /// <see cref="InputManager.SetActiveInputField"/> tells CK there is an
    /// active text input and the game must stop consuming WASD/etc.
    ///
    /// <para>Most interface methods delegate to the wrapped Unity
    /// InputField. The ones without a clean Unity equivalent return
    /// reasonable defaults — CK only invokes them in console /
    /// on-screen-keyboard contexts we don't care about on desktop.</para>
    /// </summary>
    public sealed class UnityInputFieldAdapter : InputManager.TextInputInterface
    {
        private readonly InputField field;

        public UnityInputFieldAdapter(InputField field) { this.field = field; }

        public bool WasAutoActivated { get; set; }
        public int MaxCharactersForOnScreenKeyboard => field.characterLimit > 0 ? field.characterLimit : 256;

        public string GetInputText() => field.text ?? "";

        public void SetInputText(string input)
        {
            field.text = input ?? "";
            field.caretPosition = field.text.Length;
        }

        public void AppendString(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            field.text = (field.text ?? "") + input;
            field.caretPosition = field.text.Length;
        }

        public void Deactivate(bool commit) => field.DeactivateInputField();

        public void MoveCharMarker(int n)
        {
            int pos = field.caretPosition + n;
            if (pos < 0) pos = 0;
            int max = field.text?.Length ?? 0;
            if (pos > max) pos = max;
            field.caretPosition = pos;
        }

        public void RemoveCharAtMarker()
        {
            string t = field.text ?? "";
            int p = field.caretPosition;
            if (p >= 0 && p < t.Length) { field.text = t.Remove(p, 1); field.caretPosition = p; }
        }

        public void RemoveCharBehindMarker()
        {
            string t = field.text ?? "";
            int p = field.caretPosition;
            if (p > 0 && p <= t.Length) { field.text = t.Remove(p - 1, 1); field.caretPosition = p - 1; }
        }

        public string GetHintString() => (field.placeholder is Text ph) ? ph.text : "";

        public bool IsHidden() => field.contentType == InputField.ContentType.Password;
    }
}
