# Shared Blynclight USB code. Dot-source this file: . "$PSScriptRoot\BlynclightCore.ps1"

# Embrava USB vendor ID; Blynclight family product IDs vary by model.
$EmbravaVid = '2C0D'

if (-not ('BlyncHid' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class BlyncHid {
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

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    // Writes one output report, padded to the device's declared report length.
    public static void Write(string path, byte[] report) {
        using (var h = CreateFile(path, 0x40000000 /*GENERIC_WRITE*/, 3 /*share r/w*/, IntPtr.Zero, 3 /*OPEN_EXISTING*/, 0, IntPtr.Zero)) {
            if (h.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not open " + path);
            int len = report.Length;
            IntPtr pd;
            if (HidD_GetPreparsedData(h, out pd)) {
                HIDP_CAPS caps;
                HidP_GetCaps(pd, out caps);
                HidD_FreePreparsedData(pd);
                if (caps.OutputReportByteLength > 0) len = caps.OutputReportByteLength;
            }
            var buf = new byte[len];
            Array.Copy(report, buf, Math.Min(report.Length, len));
            int written;
            if (!WriteFile(h, buf, len, out written, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Write to Blynclight failed");
        }
    }
}
'@
}

function Get-Blynclight {
    # HID interface class GUID; build the device path Windows uses for CreateFile.
    $hidGuid = '{4d1e55b2-f16f-11cf-88cb-001111000030}'
    Get-PnpDevice -PresentOnly -Class HIDClass |
        Where-Object { $_.InstanceId -match "^HID\\VID_$EmbravaVid&PID_([0-9A-F]{4})" } |
        ForEach-Object {
            [pscustomobject]@{
                Name       = $_.FriendlyName
                ProductId  = $Matches[1]
                InstanceId = $_.InstanceId
                Path       = '\\?\' + ($_.InstanceId -replace '\\', '#').ToLower() + "#$hidGuid"
            }
        }
}

function ConvertTo-Rgb([string]$value) {
    $named = @{
        red = 'FF0000'; green = '00FF00'; blue = '0000FF'; yellow = 'FFFF00'; orange = 'FF6000'
        purple = '8000FF'; magenta = 'FF00FF'; cyan = '00FFFF'; white = 'FFFFFF'; pink = 'FF4080'
    }
    $hex = if ($named.ContainsKey($value.ToLower())) { $named[$value.ToLower()] } else { $value.TrimStart('#') }
    if ($hex -notmatch '^[0-9A-Fa-f]{6}$') { throw "Unknown color '$value'. Use a name ($($named.Keys -join ', ')) or a hex code like FF8800." }
    , @([Convert]::ToByte($hex.Substring(0, 2), 16), [Convert]::ToByte($hex.Substring(2, 2), 16), [Convert]::ToByte($hex.Substring(4, 2), 16))
}

# Blynclight report: [reportId, R, B, G, light flags, sound flags, volume, 0xFF, 0x22]
# Light flags: bit0 off, bit1 dim, bit2 flash, bits3-5 flash speed (1 slow, 2 medium, 4 fast).
function New-Report([byte[]]$rgb, [bool]$lightOff, [bool]$dim, [bool]$flash, [string]$speed) {
    $speedBits = @{ slow = 1; medium = 2; fast = 4 }[$speed]
    $flags = 0
    if ($lightOff) { $flags = $flags -bor 0x01 }
    if ($dim) { $flags = $flags -bor 0x02 }
    if ($flash) { $flags = $flags -bor 0x04 -bor ($speedBits -shl 3) }
    [byte[]]@(0x00, $rgb[0], $rgb[2], $rgb[1], $flags, 0x00, 0x00, 0xFF, 0x22)
}

# Sends one report to every connected Blynclight.
function Send-BlynclightReport([object[]]$lights, [byte[]]$report) {
    foreach ($light in $lights) { [BlyncHid]::Write($light.Path, $report) }
}
