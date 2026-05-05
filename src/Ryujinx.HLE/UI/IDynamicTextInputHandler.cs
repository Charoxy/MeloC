using System;

namespace Ryujinx.HLE.UI
{
    public interface IDynamicTextInputHandler : IDisposable
    {
        event DynamicTextChangedHandler TextChangedEvent;
        event KeyPressedHandler KeyPressedEvent;
        event KeyReleasedHandler KeyReleasedEvent;

        // Fired when a UI-level confirmation occurs (e.g. iOS alert OK/Cancel).
        // bool argument: true = accept, false = cancel.
        // Default implementations may leave it as a no-op; only iOS uses it today.
        event Action<bool> SubmitEvent;

        bool TextProcessingEnabled { get; set; }

        void SetText(string text, int cursorBegin);
        void SetText(string text, int cursorBegin, int cursorEnd);
    }
}
