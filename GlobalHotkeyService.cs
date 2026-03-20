using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenRecApp
{
    public class GlobalHotkeyService : IDisposable
    {
        // Modifiers
        public const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        // Virtual Keys
        // public const uint VK_Z = 0x5A; // Removed as key is now dynamic

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private int _hotkeyId = 9000;
        private IntPtr _handle;
        private HwndSource _source;

        public event EventHandler HotkeyPressed;

        public GlobalHotkeyService(uint modifiers, uint key)
        {
            var helper = new WindowInteropHelper(new System.Windows.Window());
            _source = HwndSource.FromHwnd(helper.EnsureHandle());
            _source.AddHook(HwndHook);
            
            _handle = _source.Handle;
            Register(modifiers, key);
        }

        public void Register(uint modifiers, uint key)
        {
            // Unregister any previously registered hotkey with the same ID
            UnregisterHotKey(_handle, _hotkeyId); 
            bool success = RegisterHotKey(_handle, _hotkeyId, modifiers, key);
            
            if (!success)
            {
                System.Windows.MessageBox.Show($"Failed to register global shortcut. Close other screen recorders.", "Recording App Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source.Dispose();
                _source = null; // Set to null after disposing
            }
            UnregisterHotKey(_handle, _hotkeyId);
        }
    }
}
