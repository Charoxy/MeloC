using System;
using System.Runtime.InteropServices;
using Ryujinx.UI.Common.Helper;
using System.Threading;

namespace Ryujinx.Headless.SDL2
{
    public static class AlertHelper
    {
        // Legacy framework keyboard (broken on iOS 26).
        [DllImport("RyujinxHelper.framework/RyujinxHelper", CallingConvention = CallingConvention.Cdecl)]
        public static extern void showKeyboardAlert(string title, string message, string placeholder);

        [DllImport("RyujinxHelper.framework/RyujinxHelper", CallingConvention = CallingConvention.Cdecl)]
        public static extern void showAlert(string title, string message, bool showCancel);

        [DllImport("RyujinxHelper.framework/RyujinxHelper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr getKeyboardInput();

        [DllImport("RyujinxHelper.framework/RyujinxHelper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clearKeyboardInput();

        // Swift-side native keyboard (resolved by dyld at runtime to symbols exported by MeloNX.app).
        [DllImport("__Internal", EntryPoint = "melonx_show_text_input", CallingConvention = CallingConvention.Cdecl)]
        private static extern void melonx_show_text_input(string title, string message, string placeholder);

        [DllImport("__Internal", EntryPoint = "melonx_get_text_input_state", CallingConvention = CallingConvention.Cdecl)]
        private static extern int melonx_get_text_input_state();

        [DllImport("__Internal", EntryPoint = "melonx_get_text_input_result", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr melonx_get_text_input_result();

        [DllImport("__Internal", EntryPoint = "melonx_clear_text_input", CallingConvention = CallingConvention.Cdecl)]
        private static extern void melonx_clear_text_input();

        // states: 0 = pending, 1 = accepted, 2 = cancelled
        public static void ShowAlertWithTextInput(string title, string message, string placeholder, Action<string> onTextEntered)
        {
            try
            {
                melonx_clear_text_input();
                melonx_show_text_input(title ?? string.Empty, message ?? string.Empty, placeholder ?? string.Empty);
                Console.WriteLine("[AlertHelper] Using Swift native text input bridge");
            }
            catch (DllNotFoundException ex)
            {
                Console.WriteLine($"[AlertHelper] Swift bridge not found (DllNotFound: {ex.Message}), falling back to legacy framework");
                LegacyShowAlertWithTextInput(title, message, placeholder, onTextEntered);
                return;
            }
            catch (EntryPointNotFoundException ex)
            {
                Console.WriteLine($"[AlertHelper] Swift bridge symbols missing (EntryPointNotFound: {ex.Message}), falling back to legacy framework");
                LegacyShowAlertWithTextInput(title, message, placeholder, onTextEntered);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlertHelper] Swift bridge call failed ({ex.GetType().Name}: {ex.Message}), falling back to legacy framework");
                LegacyShowAlertWithTextInput(title, message, placeholder, onTextEntered);
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    Thread.Sleep(100);

                    int state = melonx_get_text_input_state();
                    if (state == 0) continue;

                    string result = string.Empty;
                    if (state == 1)
                    {
                        IntPtr ptr = melonx_get_text_input_result();
                        if (ptr != IntPtr.Zero)
                        {
                            result = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
                        }
                    }

                    melonx_clear_text_input();
                    onTextEntered?.Invoke(result);
                    return;
                }
            });
        }

        private static void LegacyShowAlertWithTextInput(string title, string message, string placeholder, Action<string> onTextEntered)
        {
            showKeyboardAlert(title, message, placeholder);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string result = null;
                while (result == null)
                {
                    Thread.Sleep(100);

                    IntPtr inputPtr = getKeyboardInput();
                    if (inputPtr != IntPtr.Zero)
                    {
                        result = Marshal.PtrToStringAnsi(inputPtr);
                        clearKeyboardInput();

                        onTextEntered?.Invoke(result);
                    }
                }
            });
        }


        public static void ShowAlert(string title, string message, bool cancel) {
            showAlert(title, message, cancel);
        }
    }
}
