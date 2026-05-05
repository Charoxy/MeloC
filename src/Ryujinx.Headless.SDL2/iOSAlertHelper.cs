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

        // states: 0 = pending, 1 = accepted, 2 = cancelled
        public static void ShowAlertWithTextInput(string title, string message, string placeholder, Action<string> onTextEntered)
        {
            if (!iOSTextInputBridge.IsAvailable)
            {
                Console.WriteLine($"[AlertHelper] Swift bridge unavailable ({iOSTextInputBridge.LastError}), falling back to legacy framework");
                LegacyShowAlertWithTextInput(title, message, placeholder, onTextEntered);
                return;
            }

            try
            {
                iOSTextInputBridge.Clear();
                iOSTextInputBridge.Show(title, message, placeholder);
                Console.WriteLine("[AlertHelper] Using Swift native text input bridge");
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

                    int state = iOSTextInputBridge.GetState();
                    if (state == 0) continue;
                    if (state < 0) return;

                    string result = string.Empty;
                    if (state == 1)
                    {
                        IntPtr ptr = iOSTextInputBridge.GetResultPointer();
                        if (ptr != IntPtr.Zero)
                        {
                            result = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
                        }
                    }

                    iOSTextInputBridge.Clear();
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
