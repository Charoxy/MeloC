using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Headless.SDL2
{
    /// <summary>
    /// Resolves the Swift @_cdecl text input symbols exported by the MeloNX
    /// main executable. NativeAOT on iOS does not honour DllImport("__Internal"),
    /// so we look the symbols up via NativeLibrary.GetMainProgramHandle() and
    /// invoke them through unmanaged function pointers.
    /// </summary>
    internal static unsafe class iOSTextInputBridge
    {
        private static int _initState; // 0 = not tried, 1 = ok, 2 = failed

        private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> _show;
        private static delegate* unmanaged[Cdecl]<int> _state;
        private static delegate* unmanaged[Cdecl]<IntPtr> _result;
        private static delegate* unmanaged[Cdecl]<void> _clear;

        public static bool IsAvailable
        {
            get
            {
                EnsureInit();
                return Volatile.Read(ref _initState) == 1;
            }
        }

        public static string LastError { get; private set; } = string.Empty;

        private static void EnsureInit()
        {
            if (Volatile.Read(ref _initState) != 0) return;

            try
            {
                IntPtr handle = NativeLibrary.GetMainProgramHandle();

                if (!NativeLibrary.TryGetExport(handle, "melonx_show_text_input", out IntPtr pShow) ||
                    !NativeLibrary.TryGetExport(handle, "melonx_get_text_input_state", out IntPtr pState) ||
                    !NativeLibrary.TryGetExport(handle, "melonx_get_text_input_result", out IntPtr pResult) ||
                    !NativeLibrary.TryGetExport(handle, "melonx_clear_text_input", out IntPtr pClear))
                {
                    LastError = "Swift @_cdecl text input symbols not exported by main binary";
                    Volatile.Write(ref _initState, 2);
                    return;
                }

                _show = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)pShow;
                _state = (delegate* unmanaged[Cdecl]<int>)pState;
                _result = (delegate* unmanaged[Cdecl]<IntPtr>)pResult;
                _clear = (delegate* unmanaged[Cdecl]<void>)pClear;

                Volatile.Write(ref _initState, 1);
            }
            catch (Exception ex)
            {
                LastError = $"{ex.GetType().Name}: {ex.Message}";
                Volatile.Write(ref _initState, 2);
            }
        }

        public static void Show(string title, string message, string placeholder)
        {
            EnsureInit();
            if (_show == null) throw new InvalidOperationException("Swift bridge not available");

            IntPtr t = Marshal.StringToCoTaskMemUTF8(title ?? string.Empty);
            IntPtr m = Marshal.StringToCoTaskMemUTF8(message ?? string.Empty);
            IntPtr p = Marshal.StringToCoTaskMemUTF8(placeholder ?? string.Empty);
            try
            {
                _show(t, m, p);
            }
            finally
            {
                Marshal.FreeCoTaskMem(t);
                Marshal.FreeCoTaskMem(m);
                Marshal.FreeCoTaskMem(p);
            }
        }

        public static int GetState()
        {
            EnsureInit();
            if (_state == null) return -1;
            return _state();
        }

        public static IntPtr GetResultPointer()
        {
            EnsureInit();
            if (_result == null) return IntPtr.Zero;
            return _result();
        }

        public static void Clear()
        {
            EnsureInit();
            if (_clear == null) return;
            _clear();
        }
    }
}
