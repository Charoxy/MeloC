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
        [DllImport("__Internal", EntryPoint = "melonx_show_text_input", CallingConvention = CallingConvention.Cdecl)]
        private static extern void melonx_show_text_input(string title, string message, string placeholder);

        [DllImport("__Internal", EntryPoint = "melonx_get_text_input_state", CallingConvention = CallingConvention.Cdecl)]
        private static extern int melonx_get_text_input_state();

        [DllImport("__Internal", EntryPoint = "melonx_get_text_input_result", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr melonx_get_text_input_result();

        [DllImport("__Internal", EntryPoint = "melonx_clear_text_input", CallingConvention = CallingConvention.Cdecl)]
        private static extern void melonx_clear_text_input();

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
            try
            {
                melonx_clear_text_input();
                melonx_show_text_input("Software Keyboard", string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[iOSDynamicTextInputHandler] Failed to call Swift bridge: {ex.GetType().Name}: {ex.Message}");
                Volatile.Write(ref _alertInFlight, 0);
                // Fall back to firing accept with empty string so the game doesn't soft-lock.
                SubmitEvent?.Invoke(false);
                return;
            }

            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(100);

                    int state;
                    try { state = melonx_get_text_input_state(); }
                    catch { Volatile.Write(ref _alertInFlight, 0); return; }

                    if (state == 0) continue;

                    string text = string.Empty;
                    bool accepted = state == 1;
                    if (accepted)
                    {
                        try
                        {
                            IntPtr ptr = melonx_get_text_input_result();
                            if (ptr != IntPtr.Zero)
                            {
                                text = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
                            }
                        }
                        catch { /* ignore */ }
                    }

                    try { melonx_clear_text_input(); } catch { }

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
