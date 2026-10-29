using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Krabbz.Blynclight
{
    // The light states the app understands. These strings are also what's saved in state.txt.
    public static class LightState
    {
        public const string Red = "red";
        public const string Green = "green";
        // Cycles quickly through the color wheel.
        public const string Party = "party";
        // Saved by versions before Party Mode replaced red/blue flashing; loaded as Party.
        public const string LegacyFlashRedBlue = "flash-red-blue";
        public const string Off = "off";
        // The app leaves the light alone so Embrava Connect can show Teams/Calendar presence.
        public const string FollowTeams = "teams";
        // Flashes red and blue: shown while the mic is in use, or picked by hand with the On A Call button.
        public const string CallFlash = "call-flash";

        public static bool IsValid(string state)
        {
            return state == Red || state == Green || state == Party || state == Off || state == FollowTeams || state == CallFlash;
        }

        public static string Describe(string state)
        {
            switch (state)
            {
                case Red: return "Busy";
                case Green: return "Available";
                case Party: return "Test lights";
                case Off: return "Off";
                case FollowTeams: return "Following Teams status";
                case CallFlash: return "On a call";
                default: return "Unknown";
            }
        }
    }

    public enum LightKind { Blynclight, Luxafor }

    public class LightDevice
    {
        public string Path;
        public LightKind Kind;
    }

    public struct LightColor
    {
        public byte R, G, B;
        public bool IsOff { get { return R == 0 && G == 0 && B == 0; } }
        public static readonly LightColor Off = new LightColor();
        public static LightColor Rgb(byte r, byte g, byte b) { return new LightColor { R = r, G = g, B = b }; }
    }

    // Finds supported busy lights (Embrava Blynclight, Luxafor Flag) and writes their USB output reports.
    public static class BlyncDevice
    {
        // USB IDs as they appear in HID device paths: Embrava's vendor ID, and the Luxafor Flag's vendor/product ID.
        const string EmbravaVid = "vid_2c0d";
        const string LuxaforId = "vid_04d8&pid_f372";
        static Guid hidInterfaceGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");

        const int DIGCF_PRESENT = 0x2;
        const int DIGCF_DEVICEINTERFACE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr devInfo, IntPtr devInfoData, ref Guid classGuid, int index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr devInfo, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int detailSize, out int requiredSize, IntPtr devInfoData);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr devInfo);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(SafeFileHandle h, byte[] buf, int len, out int written, IntPtr ovl);
        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")]
        static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")]
        static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);

        public static List<string> FindPaths()
        {
            return FindPaths(EmbravaVid);
        }

        // HID device paths containing idMatch (lower case, e.g. "vid_2c0d" or "vid_04d8&pid_f372").
        public static List<string> FindPaths(string idMatch)
        {
            var paths = new List<string>();
            IntPtr devInfo = SetupDiGetClassDevs(ref hidInterfaceGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (devInfo == new IntPtr(-1)) return paths;
            try
            {
                var data = new SP_DEVICE_INTERFACE_DATA();
                data.cbSize = Marshal.SizeOf(data);
                for (int i = 0; SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidInterfaceGuid, i, ref data); i++)
                {
                    int size;
                    SetupDiGetDeviceInterfaceDetail(devInfo, ref data, IntPtr.Zero, 0, out size, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal(size);
                    try
                    {
                        // cbSize is the fixed part of SP_DEVICE_INTERFACE_DETAIL_DATA_W, which differs by process bitness.
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (SetupDiGetDeviceInterfaceDetail(devInfo, ref data, detail, size, out size, IntPtr.Zero))
                        {
                            string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                            if (path.ToLowerInvariant().Contains(idMatch)) paths.Add(path);
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(devInfo); }
            return paths;
        }

        // Every supported light currently plugged in.
        public static List<LightDevice> FindLights()
        {
            var lights = new List<LightDevice>();
            foreach (var path in FindPaths(EmbravaVid)) lights.Add(new LightDevice { Path = path, Kind = LightKind.Blynclight });
            foreach (var path in FindPaths(LuxaforId)) lights.Add(new LightDevice { Path = path, Kind = LightKind.Luxafor });
            return lights;
        }

        // Named colors used by light states and Teams status; anything else means off.
        public static LightColor ColorFor(string name)
        {
            switch (name)
            {
                case "red": return LightColor.Rgb(255, 0, 0);
                case "green": return LightColor.Rgb(0, 255, 0);
                case "blue": return LightColor.Rgb(0, 0, 255);
                case "yellow": return LightColor.Rgb(255, 255, 0);
                case "purple": return LightColor.Rgb(128, 0, 255);
                default: return LightColor.Off;
            }
        }

        public static byte[] ReportFor(LightKind kind, LightColor color)
        {
            // Luxafor Flag: [reportId, 0x01 static color, 0xFF all LEDs, R, G, B, 0, 0, 0]. Off is black.
            if (kind == LightKind.Luxafor)
                return new byte[] { 0x00, 0x01, 0xFF, color.R, color.G, color.B, 0x00, 0x00, 0x00 };
            return Report(color.R, color.G, color.B, color.IsOff);
        }

        // Fully saturated, full-brightness color at a point on the color wheel (0 = red, 120 = green, 240 = blue).
        public static void HueToRgb(int hue, out byte r, out byte g, out byte b)
        {
            hue = ((hue % 360) + 360) % 360;
            int rising = (int)Math.Round(255.0 * (hue % 60) / 60);
            int falling = 255 - rising;
            switch (hue / 60)
            {
                case 0: r = 255; g = (byte)rising; b = 0; break;
                case 1: r = (byte)falling; g = 255; b = 0; break;
                case 2: r = 0; g = 255; b = (byte)rising; break;
                case 3: r = 0; g = (byte)falling; b = 255; break;
                case 4: r = (byte)rising; g = 0; b = 255; break;
                default: r = 255; g = 0; b = (byte)falling; break;
            }
        }

        // Blynclight report: [reportId, R, B, G, light flags, sound flags, volume, 0xFF, 0x22]. Light flag bit0 = off.
        static byte[] Report(byte r, byte g, byte b, bool off)
        {
            return new byte[] { 0x00, r, b, g, (byte)(off ? 0x01 : 0x00), 0x00, 0x00, 0xFF, 0x22 };
        }

        // Writes one output report, padded to the device's declared report length.
        public static void Write(string path, byte[] report)
        {
            using (var h = CreateFile(path, 0x40000000 /*GENERIC_WRITE*/, 3 /*share r/w*/, IntPtr.Zero, 3 /*OPEN_EXISTING*/, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the light");
                int len = report.Length;
                IntPtr pd;
                if (HidD_GetPreparsedData(h, out pd))
                {
                    HIDP_CAPS caps;
                    HidP_GetCaps(pd, out caps);
                    HidD_FreePreparsedData(pd);
                    if (caps.OutputReportByteLength > 0) len = caps.OutputReportByteLength;
                }
                var buf = new byte[len];
                Array.Copy(report, buf, Math.Min(report.Length, len));
                int written;
                if (!WriteFile(h, buf, len, out written, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Write to the Blynclight failed");
            }
        }
    }
}
