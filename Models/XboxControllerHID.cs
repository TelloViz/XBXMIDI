// using Microsoft.Win32.SafeHandles;
// using System.Runtime.InteropServices;
// using System.Diagnostics;

// namespace XB2Midi.Models
// {
//     internal static class XboxControllerHID
//     {
//         [DllImport("kernel32.dll", SetLastError = true)]
//         private static extern SafeFileHandle CreateFile(
//             string lpFileName,
//             uint dwDesiredAccess,
//             uint dwShareMode,
//             IntPtr lpSecurityAttributes,
//             uint dwCreationDisposition,
//             uint dwFlagsAndAttributes,
//             IntPtr hTemplateFile);

//         [DllImport("kernel32.dll", SetLastError = true)]
//         private static extern bool WriteFile(
//             SafeFileHandle hFile,
//             byte[] lpBuffer,
//             uint nNumberOfBytesToWrite,
//             out uint lpNumberOfBytesWritten,
//             IntPtr lpOverlapped);

//         // Add SetupAPI imports
//         [DllImport("setupapi.dll", SetLastError = true)]
//         static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, 
//             IntPtr Enumerator, IntPtr hwndParent, uint Flags);

//         [DllImport("setupapi.dll", SetLastError = true)]
//         static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, 
//             IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, 
//             uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

//         [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
//         static extern bool SetupDiGetDeviceInterfaceDetail(
//             IntPtr DeviceInfoSet,
//             ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
//             IntPtr DeviceInterfaceDetailData,
//             uint DeviceInterfaceDetailDataSize,
//             out uint RequiredSize,
//             IntPtr DeviceInfoData);

//         [DllImport("setupapi.dll", SetLastError = true)]
//         static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

//         [StructLayout(LayoutKind.Sequential)]
//         struct SP_DEVICE_INTERFACE_DATA
//         {
//             public uint cbSize;
//             public Guid InterfaceClassGuid;
//             public uint Flags;
//             public IntPtr Reserved;
//         }

//         private const uint GENERIC_READ = 0x80000000;
//         private const uint GENERIC_WRITE = 0x40000000;
//         private const uint FILE_SHARE_READ = 0x00000001;
//         private const uint FILE_SHARE_WRITE = 0x00000002;
//         private const uint OPEN_EXISTING = 3;
//         private const uint DIGCF_PRESENT = 0x02;
//         private const uint DIGCF_DEVICEINTERFACE = 0x10;
//         private const int DEVICE_PATH_MAX_LENGTH = 256;

//         // HID GUID for Xbox controllers
//         private static readonly Guid XBOX_CONTROLLER_GUID = 
//             new Guid("EC87F1E3-C13B-4100-B5F7-8B84D54260CB");

//         public static bool SetLEDPattern(byte pattern)
//         {
//             try
//             {
//                 // Get HID device path for Xbox controller
//                 var devicePath = GetXboxControllerPath();
//                 if (string.IsNullOrEmpty(devicePath))
//                 {
//                     Debug.WriteLine("Could not find Xbox controller HID device");
//                     return false;
//                 }

//                 Debug.WriteLine($"Found Xbox controller at: {devicePath}");

//                 using var handle = CreateFile(devicePath,
//                     GENERIC_READ | GENERIC_WRITE,
//                     FILE_SHARE_READ | FILE_SHARE_WRITE,
//                     IntPtr.Zero,
//                     OPEN_EXISTING,
//                     0,
//                     IntPtr.Zero);

//                 if (handle.IsInvalid)
//                 {
//                     Debug.WriteLine($"Failed to open HID device: {Marshal.GetLastWin32Error()}");
//                     return false;
//                 }

//                 byte[] command = new byte[] { 0x01, 0x03, pattern, 0x00 };
//                 uint bytesWritten;

//                 Debug.WriteLine($"Sending LED command. Pattern: 0x{pattern:X2}");
//                 bool success = WriteFile(handle, command, (uint)command.Length, out bytesWritten, IntPtr.Zero);
                
//                 Debug.WriteLine($"LED command result: success={success}, bytesWritten={bytesWritten}, error={Marshal.GetLastWin32Error()}");
//                 return success;
//             }
//             catch (Exception ex)
//             {
//                 Debug.WriteLine($"Error setting LED pattern: {ex.Message}");
//                 return false;
//             }
//         }

//         private static string GetXboxControllerPath()
//         {
//             IntPtr deviceInfoSet = IntPtr.Zero;
//             try
//             {
//                 // Try with both GUIDs
//                 var guidsToTry = new[]
//                 {
//                     new Guid("745A17A0-74D3-11D0-B6FE-00A0C90F57DA"), // HID class GUID
//                     XBOX_CONTROLLER_GUID // Xbox specific GUID
//                 };

//                 foreach (var guid in guidsToTry)
//                 {
//                     Debug.WriteLine($"\nTrying GUID: {guid}");
//                     deviceInfoSet = SetupDiGetClassDevs(
//                         ref guid,
//                         IntPtr.Zero,
//                         IntPtr.Zero,
//                         DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

//                     if (deviceInfoSet == IntPtr.Zero || deviceInfoSet.ToInt64() == -1)
//                     {
//                         Debug.WriteLine($"Failed to get device info set for GUID {guid}: {Marshal.GetLastWin32Error()}");
//                         continue;
//                     }

//                     var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA
//                     {
//                         cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA))
//                     };

//                     Debug.WriteLine("=== Enumerating Devices ===");
//                     for (uint deviceIndex = 0; ; deviceIndex++)
//                     {
//                         bool success = SetupDiEnumDeviceInterfaces(
//                             deviceInfoSet,
//                             IntPtr.Zero,
//                             ref guid,
//                             deviceIndex,
//                             ref deviceInterfaceData);

//                         if (!success)
//                         {
//                             var error = Marshal.GetLastWin32Error();
//                             if (error == 259) // ERROR_NO_MORE_ITEMS
//                                 break;
//                             Debug.WriteLine($"Failed to enumerate device {deviceIndex}: Error {error}");
//                             continue;
//                         }

//                         uint requiredSize = 0;
//                         SetupDiGetDeviceInterfaceDetail(
//                             deviceInfoSet,
//                             ref deviceInterfaceData,
//                             IntPtr.Zero,
//                             0,
//                             out requiredSize,
//                             IntPtr.Zero);

//                         IntPtr detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);
//                         try
//                         {
//                             Marshal.WriteInt32(detailDataBuffer, IntPtr.Size == 8 ? 8 : 5);

//                             if (SetupDiGetDeviceInterfaceDetail(
//                                 deviceInfoSet,
//                                 ref deviceInterfaceData,
//                                 detailDataBuffer,
//                                 requiredSize,
//                                 out _,
//                                 IntPtr.Zero))
//                             {
//                                 string devicePath = Marshal.PtrToStringAuto(detailDataBuffer + 4);
//                                 Debug.WriteLine($"Found device: {devicePath}");

//                                 // Check for Xbox controller - try both USB and HID paths
//                                 if (devicePath.Contains("VID_045E") && devicePath.Contains("PID_028E"))
//                                 {
//                                     Debug.WriteLine($"Found Xbox controller: {devicePath}");
//                                     return devicePath;
//                                 }
//                             }
//                             else
//                             {
//                                 Debug.WriteLine($"Failed to get device details: {Marshal.GetLastWin32Error()}");
//                             }
//                         }
//                         finally
//                         {
//                             Marshal.FreeHGlobal(detailDataBuffer);
//                         }
//                     }
                    
//                     SetupDiDestroyDeviceInfoList(deviceInfoSet);
//                     deviceInfoSet = IntPtr.Zero;
//                 }

//                 Debug.WriteLine("No Xbox controller found with any GUID");
//                 return null;
//             }
//             finally
//             {
//                 if (deviceInfoSet != IntPtr.Zero && deviceInfoSet.ToInt64() != -1)
//                 {
//                     SetupDiDestroyDeviceInfoList(deviceInfoSet);
//                 }
//             }
//         }
//     }
// }