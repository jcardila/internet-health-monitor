using System.Runtime.InteropServices;

namespace InternetHealth.App.Interop;

internal static class NativeMethods
{
    // --- iphlpapi ---
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    internal static extern int GetBestInterface(uint dwDestAddr, out uint pdwBestIfIndex);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    internal static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint physicalAddrLen);

    // --- user32 / dwm ---
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWCP_ROUND = 2;

    // --- wlanapi ---
    [DllImport("wlanapi.dll")]
    internal static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint opCode, IntPtr pReserved,
        out uint pdwDataSize, out IntPtr ppData, out uint pWlanOpcodeValueType);

    [DllImport("wlanapi.dll")]
    internal static extern uint WlanGetNetworkBssList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid,
        int dot11BssType, [MarshalAs(UnmanagedType.Bool)] bool bSecurityEnabled, IntPtr pReserved, out IntPtr ppWlanBssList);

    [DllImport("wlanapi.dll")]
    internal static extern void WlanFreeMemory(IntPtr pMemory);

    internal const uint WLAN_OPCODE_CURRENT_CONNECTION = 7;
    internal const uint WLAN_OPCODE_CHANNEL_NUMBER = 8;
    internal const uint WLAN_OPCODE_RSSI = 0x10000102;
    internal const uint ERROR_SUCCESS = 0;
    internal const uint ERROR_ACCESS_DENIED = 5;
}
