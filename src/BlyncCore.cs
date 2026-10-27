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
        public const string FlashRedBlue = "flash-red-blue";
        public const string Off = "off";

        public static bool IsValid(string state)
        {
            return state == Red || state == Green || state == FlashRedBlue || state == Off;
        }

        public static string Describe(string state)
        {
            switch (state)
            {
                case Red: return "Red";
                case Green: return "Green";
                case FlashRedBlue: return "Flashing red / blue";
                case Off: return "Off";
                default: return "Unknown";
            }
        }
    }

    // Finds Embrava lights and writes Blynclight output reports to them.
    public static class BlyncDevice
    {
        // Embrava USB vendor ID, as it appears in HID device paths.
        const string EmbravaVid = "vid_2c0d";
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
                            if (path.ToLowerInvariant().Contains(EmbravaVid)) paths.Add(path);
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(devInfo); }
            return paths;
        }

        // Blynclight report: [reportId, R, B, G, light flags, sound flags, volume, 0xFF, 0x22]. Light flag bit0 = off.
        public static byte[] ReportFor(string color)
        {
            switch (color)
            {
                case "red": return Report(255, 0, 0, false);
                case "green": return Report(0, 255, 0, false);
                case "blue": return Report(0, 0, 255, false);
                default: return Report(0, 0, 0, true);
            }
        }

        static byte[] Report(byte r, byte g, byte b, bool off)
        {
            return new byte[] { 0x00, r, b, g, (byte)(off ? 0x01 : 0x00), 0x00, 0x00, 0xFF, 0x22 };
        }

        // Writes one output report, padded to the device's declared report length.
        public static void Write(string path, byte[] report)
        {
            using (var h = CreateFile(path, 0x40000000 /*GENERIC_WRITE*/, 3 /*share r/w*/, IntPtr.Zero, 3 /*OPEN_EXISTING*/, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the Blynclight");
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
