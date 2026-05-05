using Ryujinx.HLE.UI;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Headless.SDL2
{
    /// <summary>
    /// iOS implementation of IDynamicTextInputHandler that presents a real
    /// UIAlertController via Swift @_cdecl symbols (see TextInputBridge.swift)
    /// instead of auto-emitting "MeloNX" like HeadlessDynamicTextInputHandler.
    /// </summary>
    internal class iOSDynamicTextInputHandler : IDynamicTextInputHandler
    {
        private bool _canProcessInput;
        private int _alertInFlight; // 0 = idle, 1 = presenting

        public event DynamicTextChangedHandler TextChangedEvent;
        public event KeyPressedHandler KeyPressedEvent { add { } remove { } }
        public event KeyReleasedHandler KeyReleasedEvent { add { } remove { } }
        public event Action<bool> SubmitEvent;

        public bool TextProcessingEnabled
        {
            get => Volatile.Read(ref _canProcessInput);
            set
            {
                Volatile.Write(ref _canProcessInput, value);
                if (value && Interlocked.CompareExchange(ref _alertInFlight, 1, 0) == 0)
                {
                    PresentAndPoll();
                }
            }
        }

        private void PresentAndPoll()
        {
            if (!iOSTextInputBridge.IsAvailable)
            {
                Console.WriteLine($"[iOSDynamicTextInputHandler] Swift bridge unavailable: {iOSTextInputBridge.LastError}");
                Volatile.Write(ref _alertInFlight, 0);
                SubmitEvent?.Invoke(false);
                return;
            }

            try
            {
                iOSTextInputBridge.Clear();
                iOSTextInputBridge.Show("Software Keyboard", string.Empty, string.Empty);
                Console.WriteLine("[iOSDynamicTextInputHandler] presented native UIAlertController");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[iOSDynamicTextInputHandler] Show failed: {ex.GetType().Name}: {ex.Message}");
                Volatile.Write(ref _alertInFlight, 0);
                SubmitEvent?.Invoke(false);
                return;
            }

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(100);

                    int state = iOSTextInputBridge.GetState();
                    if (state == 0) continue;
                    if (state < 0) { Volatile.Write(ref _alertInFlight, 0); return; }

                    string text = string.Empty;
                    bool accepted = state == 1;
                    if (accepted)
                    {
                        try
                        {
                            IntPtr ptr = iOSTextInputBridge.GetResultPointer();
                            if (ptr != IntPtr.Zero)
                            {
                                text = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
                            }
                        }
                        catch { /* ignore */ }
                    }

                    try { iOSTextInputBridge.Clear(); } catch { }

                    int cursor = text.Length;
                    TextChangedEvent?.Invoke(text, cursor, cursor, false);

                    Thread.Sleep(50);
                    SubmitEvent?.Invoke(accepted);

                    Volatile.Write(ref _alertInFlight, 0);
                    return;
                }
            });
        }

        public void SetText(string text, int cursorBegin) { }
        public void SetText(string text, int cursorBegin, int cursorEnd) { }
        public void Dispose() { }
    }
}
