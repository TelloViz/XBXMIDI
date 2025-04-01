using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;  // Add this for KeyEventArgs
using System.Linq;  // Add this for Cast<T>() extension method
using NAudio.Midi;
using System.Collections.ObjectModel;
using System.Threading.Tasks;  // Add this for Task
using XB2Midi.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;  // Add this line for StringBuilder


namespace XB2Midi.Views
{
    public partial class MainWindow : Window
    {
        private XboxController? controller;  // Rename from physicalController
        private MidiOutput? midiOutput;
        private MappingManager? mappingManager;
        private ObservableCollection<string> midiLog = new();
        private TestControllerSimulator testSimulator;

        private const string LED_INTERFACE_PATH = @"\\?\HID#VID_045E&PID_028E&IG_00#6&1e0d8bd5&0&00";
        private SafeFileHandle? ledHandle;
        private const int LED_REPORT_LENGTH = 0x20;  // From USB descriptor wMaxPacketSize
        private const byte LED_ENDPOINT = 0x01;      // From USB descriptor bEndpointAddress

        private readonly string debugLogPath = @".\Views\DebugOutput.txt";

        private static class LedPatterns
        {
            public const byte OFF = 0x00;
            public const byte ALL_BLINK = 0x01;
            public const byte TOP_LEFT_BLINK = 0x02;
            public const byte TOP_RIGHT_BLINK = 0x03;
            public const byte BOTTOM_LEFT_BLINK = 0x04;
            public const byte BOTTOM_RIGHT_BLINK = 0x05;
            public const byte TOP_LEFT_ON = 0x06;
            public const byte TOP_RIGHT_ON = 0x07;
            public const byte BOTTOM_LEFT_ON = 0x08;
            public const byte BOTTOM_RIGHT_ON = 0x09;
            public const byte ROTATE = 0x0A;
            public const byte BLINK_PREV = 0x0B;
            public const byte ALL_ON = 0x0F;
        }

        private struct DeviceInfo
        {
            public string Path;
            public string HardwareId;
            public bool IsHidDevice;
            public bool IsXboxDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public Int32 Size;
            public UInt16 VendorID;
            public UInt16 ProductID;
            public UInt16 VersionNumber;
        }

        [DllImport("hid.dll")]
        private static extern bool HidD_GetAttributes(SafeFileHandle HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetOutputReport(SafeFileHandle HidDeviceObject, byte[] lpReportBuffer, uint ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll")]
        private static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            byte[] lpInBuffer,
            uint nInBufferSize,
            byte[] lpOutBuffer,
            uint nOutBufferSize,
            ref uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            SafeFileHandle hFile,
            [MarshalAs(UnmanagedType.LPArray)] byte[] lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public ushort[] Reserved;
        }

        public MainWindow()
        {
            InitializeComponent();
            
            // Clear debug log before any initialization
            ClearDebugLog();
            
            InitializeTestController();

            try
            {
                controller = new XboxController();
                midiOutput = new MidiOutput();
                mappingManager = new MappingManager(midiOutput);

                // Add LED initialization
                if (InitializeLedControl())
                {
                    Debug.WriteLine("LED control ready");
                    // Test with pattern 0x0A (rotating)
                    SetLedPattern(0x0A);
                }

                controller.InputChanged += Controller_InputChanged;
                controller.ConnectionChanged += Controller_ConnectionChanged;
                CompositionTarget.Rendering += (s, e) => controller?.Update();

                if (File.Exists("default_mappings.json"))
                    mappingManager?.LoadMappings("default_mappings.json");

                midiLog = new ObservableCollection<string>();
                MidiActivityLog.ItemsSource = midiLog;

                RefreshMidiDevices();

                if (mappingManager != null)
                {
                    mappingManager.MappingsChanged += (s, e) => 
                    {
                        MappingsView?.UpdateMappings(mappingManager.GetCurrentMappings());
                    };
                }

                UpdateControllerStatus(controller.IsConnected);

                TestVisualizer.SimulateInput += TestVisualizer_SimulateInput;

                if (!InitializeLedControl())
                {
                    Debug.WriteLine("Failed to initialize LED control.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing: {ex.Message}");
                Close();
            }
        }

        private void InitializeTestController()
        {
            testSimulator = new TestControllerSimulator();
            testSimulator.SimulatedInput += (s, e) =>
            {
                // Handle all simulated input including spring-back
                mappingManager?.HandleControllerInput(e);
                TestVisualizer?.UpdateControl(e);

                Dispatcher.Invoke(() =>
                {
                    // Log all movements including spring-back
                    if (e.InputType == ControllerInputType.Thumbstick)
                    {
                        dynamic value = e.Value;
                        TestResultsLog.Items.Insert(0, 
                            $"{DateTime.Now:HH:mm:ss.fff} - {e.InputName}: X={value.X}, Y={value.Y}");
                        if (TestResultsLog.Items.Count > 100)
                            TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                    }
                });
            };
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
            IntPtr hwndParent,
            uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr DeviceInfoSet,
            IntPtr DeviceInfoData,
            ref Guid InterfaceClassGuid,
            uint MemberIndex,
            ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr DeviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
            IntPtr DeviceInterfaceDetailData,
            uint DeviceInterfaceDetailDataSize,
            out uint RequiredSize,
            IntPtr DeviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        private bool InitializeLedControl()
        {
            try
            {
                LogLedEvent("Starting LED control initialization...");
                
                // Close any existing handle first
                if (ledHandle != null && !ledHandle.IsInvalid)
                {
                    ledHandle.Close();
                    ledHandle = null;
                }

                var devicePath = @"\\?\hid#vid_045e&pid_028e&ig_00#7&23500763&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
                
                LogLedEvent($"Attempting to open device: {devicePath}");

                // Open with FILE_FLAG_OVERLAPPED and proper sharing
                ledHandle = CreateFile(
                    devicePath,
                    (uint)(EFileAccess.GenericRead | EFileAccess.GenericWrite),
                    (uint)EFileShare.ReadWrite,  // Updated to use correct enum value
                    IntPtr.Zero,
                    (uint)ECreationDisposition.OpenExisting,
                    (uint)EFileAttributes.Normal | 0x40000000,   // FILE_FLAG_OVERLAPPED
                    IntPtr.Zero);

                if (ledHandle.IsInvalid)
                {
                    var lastError = Marshal.GetLastWin32Error();
                    LogLedEvent($"Failed to open device: Error {lastError} ({GetWin32ErrorMessage(lastError)})");
                    return false;
                }

                LogLedEvent("Successfully opened device");
                return true;
            }
            catch (Exception ex)
            {
                LogLedEvent($"Error in LED control: {ex.Message}");
                LogLedEvent($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private string GetWin32ErrorMessage(int errorCode)
        {
            var message = new StringBuilder(1024);
            FormatMessage(
                0x00001000, // FORMAT_MESSAGE_FROM_SYSTEM
                IntPtr.Zero,
                (uint)errorCode,
                0,
                message,
                (uint)message.Capacity,
                IntPtr.Zero);
            return message.ToString().Trim();
        }

        private bool SetLedPattern(byte pattern)
        {
            if (ledHandle?.IsInvalid != false)
            {
                LogLedEvent("Invalid handle");
                return false;
            }

            try
            {
                byte[] report = new byte[3];
                report[0] = 0x01;  // LED command type
                report[1] = 0x03;  // Length
                report[2] = pattern;  // LED pattern value

                LogLedEvent($"Sending LED pattern: {BitConverter.ToString(report)}");

                // Use HidD_SetOutputReport instead of WriteFile
                bool success = HidD_SetOutputReport(
                    ledHandle,
                    report,
                    (uint)report.Length);

                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    LogLedEvent($"Failed to set LED pattern: error {error} ({GetWin32ErrorMessage(error)})");
                    return false;
                }

                LogLedEvent($"Successfully set LED pattern: 0x{pattern:X2}");
                return true;
            }
            catch (Exception ex)
            {
                LogLedEvent($"Error setting LED pattern: {ex.Message}");
                return false;
            }
        }

        private void Controller_InputChanged(object? sender, ControllerInputEventArgs e)
        {
            Debug.WriteLine($"Controller Input: {e.InputType} {e.InputName} Value: {e.Value}");
            
            Dispatcher.Invoke(() =>
            {
                // Update appropriate visualizer based on source
                if (sender == controller)  // Updated from physicalController
                {
                    DebugVisualizer?.UpdateControl(e);
                    
                    // Only log physical controller input in the debug tab
                    if (e.InputType != ControllerInputType.Thumbstick || IsSignificantThumbstickMovement(e.Value))
                    {
                        InputLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputName}: {e.Value}");
                        while (InputLog.Items.Count > 100)
                            InputLog.Items.RemoveAt(InputLog.Items.Count - 1);
                    }
                }

                // Handle MIDI mapping for both controllers
                mappingManager?.HandleControllerInput(e);
            });
        }

        private bool IsSignificantThumbstickMovement(object value)
        {
            try
            {
                dynamic stick = value;
                short x = Convert.ToInt16(stick.X);
                short y = Convert.ToInt16(stick.Y);
                
                return Math.Abs(x) > 1000 || Math.Abs(y) > 1000;
            }
            catch
            {
                return false;
            }
        }

        private void HandleButtonMidi(string button, object value)
        {
            if (midiOutput == null) return;
            bool isPressed = Convert.ToInt32(value) != 0;
            
            var args = new ControllerInputEventArgs(
                ControllerInputType.Button,
                button,
                isPressed ? 127 : 0
            );
            
            mappingManager?.HandleControllerInput(args);
            
            if (isPressed)
            {
                LogMidiEvent($"Button {button} triggered");
            }
        }

        private void HandleTriggerMidi(string trigger, object value)
        {
            if (midiOutput == null) return;
            byte controlValue = Convert.ToByte(value);
            
            var args = new ControllerInputEventArgs(
                ControllerInputType.Trigger,
                trigger,
                controlValue
            );
            
            mappingManager?.HandleControllerInput(args);
            LogMidiEvent($"Trigger {trigger}: {controlValue}");
        }

        private void HandleThumbstickMidi(string stick, object value)
        {
            if (midiOutput == null) return;
            
            var args = new ControllerInputEventArgs(
                ControllerInputType.Thumbstick,
                stick,
                value
            );
            
            mappingManager?.HandleControllerInput(args);
            LogMidiEvent($"Stick {stick}: X={((dynamic)value).X}, Y={((dynamic)value).Y}");
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            InputLog.Items.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            controller?.Dispose();
            midiOutput?.Dispose();
            base.OnClosed(e);
        }

        private void RefreshMidiDevices()
        {
            var currentDevices = new List<string>();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                currentDevices.Add(MidiOut.DeviceInfo(i).ProductName);
            }

            MappingDeviceComboBox.ItemsSource = currentDevices;
        }

        private void MidiDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshMidiDevices();
        }

        private void LogMidiEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                midiLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(0);
            });
        }

        private void SendMidiMessage(int deviceIndex, int channel, int noteNumber, int velocity)
        {
            if (midiOutput == null) return;
            
            try
            {
                midiOutput.SendNoteOn(deviceIndex, (byte)channel, (byte)noteNumber, (byte)velocity);
                LogMidiEvent($"Note On - Device: {deviceIndex}, Channel: {channel}, Note: {noteNumber}, Velocity: {velocity}");
            }
            catch (Exception ex)
            {
                LogMidiEvent($"Error sending MIDI: {ex.Message}");
            }
        }

        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ControllerInputComboBox.SelectedItem == null || 
                    MidiTypeComboBox.SelectedItem == null || 
                    MappingDeviceComboBox.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select controller input, MIDI message type, and MIDI device.", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }

                string controllerInput = (ControllerInputComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string midiType = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                
                if (!byte.TryParse(MidiChannelTextBox.Text, out byte channel) || channel < 1 || channel > 16)
                {
                    MessageBox.Show("Please enter a valid MIDI channel (1-16).", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }
                
                channel--;

                MidiMessageType messageType = midiType switch
                {
                    "Note" => MidiMessageType.Note,
                    "Control Change" => MidiMessageType.ControlChange,
                    "Pitch Bend" => MidiMessageType.PitchBend,
                    _ => MidiMessageType.ControlChange
                };

                var mapping = new MidiMapping
                {
                    ControllerInput = controllerInput.Replace(" Button", "").Replace(" ", ""),
                    MessageType = messageType,
                    Channel = channel,
                    MinValue = 0,
                    MaxValue = messageType == MidiMessageType.PitchBend ? 16383 : 127,
                    MidiDeviceIndex = MappingDeviceComboBox.SelectedIndex,
                    MidiDeviceName = MappingDeviceComboBox.SelectedItem?.ToString() ?? ""
                };

                if (messageType != MidiMessageType.PitchBend)
                {
                    if (!byte.TryParse(MidiValueTextBox.Text, out byte value) || value > 127)
                    {
                        MessageBox.Show("Please enter a valid value (0-127).", 
                                      "Validation Error", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Warning);
                        return;
                    }
                    
                    if (messageType == MidiMessageType.Note)
                    {
                        mapping.NoteNumber = value;
                    }
                    else
                    {
                        mapping.ControllerNumber = value;
                    }
                }

                mappingManager?.HandleMapping(mapping);

                MidiChannelTextBox.Text = "";
                MidiValueTextBox.Text = "";

                LogMidiEvent($"Added mapping: {mapping.ControllerInput} -> {mapping.MessageType} (Device: {mapping.MidiDeviceName})");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding mapping: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void Controller_ConnectionChanged(object? sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateControllerStatus(isConnected);
            });
        }

        private void UpdateControllerStatus(bool isConnected)
        {
            if (ControllerStatus != null)
            {
                ControllerStatus.Text = isConnected ? "Controller Connected" : "Controller Disconnected";
                ControllerStatus.Foreground = isConnected ? Brushes.Green : Brushes.Red;
            }
        }

        private void TestVisualizer_SimulateInput(object? sender, ControllerInputEventArgs e)
        {
            Debug.WriteLine($"TestVisualizer_SimulateInput: {e.InputType} - {e.InputName}");
            
            if (e.InputType == ControllerInputType.ThumbstickRelease)
            {
                dynamic value = e.Value;
                Point releasePos = value.ReleasePosition;
                Debug.WriteLine($"Starting spring-back: {e.InputName} at {releasePos}");
                testSimulator.SimulateStickRelease(e.InputName, releasePos);

                // Log the release event
                Dispatcher.Invoke(() => {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - Release: {e.InputName} from X={releasePos.X:F2}, Y={releasePos.Y:F2}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            else
            {
                Debug.WriteLine($"Handling regular input: {e.InputType} - {e.InputName} = {e.Value}");
                mappingManager?.HandleControllerInput(e);
                
                Dispatcher.Invoke(() => {
                    TestResultsLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {e.InputType}: {e.InputName} = {e.Value}");
                    if (TestResultsLog.Items.Count > 100)
                        TestResultsLog.Items.RemoveAt(TestResultsLog.Items.Count - 1);
                });
            }
            
            // Always update the visualizer
            TestVisualizer?.UpdateControl(e);
        }

        private void TriggerRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TestVisualizer != null)
            {
                TestVisualizer.TriggerRate = e.NewValue;
            }
        }

        private void MidiTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MidiValueTextBox != null)
            {
                bool isPitchBend = (MidiTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() == "Pitch Bend";
                MidiValueTextBox.IsEnabled = !isPitchBend;
                if (isPitchBend)
                {
                    MidiValueTextBox.Text = "";
                }
            }
        }

        private void TestNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MappingDeviceComboBox.SelectedIndex >= 0 && midiOutput != null)
            {
                midiOutput.SendNoteOn(MappingDeviceComboBox.SelectedIndex, 0, 60, 100);
                Task.Delay(100).ContinueWith(_ =>
                {
                    midiOutput.SendNoteOff(MappingDeviceComboBox.SelectedIndex, 0, 60);
                });
            }
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
        }

        private void HandleTestSimulatedInput(object? sender, ControllerInputEventArgs e)
        {
            mappingManager?.HandleControllerInput(e);
            TestVisualizer?.UpdateControl(e);
        }

        private void TestThumbstick_Released(string thumbstickName, Point lastPosition)
        {
            testSimulator.SimulateStickRelease(thumbstickName, lastPosition);
        }

        private void SpringBackRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (testSimulator != null)
            {
                testSimulator.SpringBackRate = e.NewValue;
                System.Diagnostics.Debug.WriteLine($"Spring-back rate updated to: {e.NewValue:F3}");
            }
        }

        private void InputLog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var selectedItems = InputLog.Items.Cast<string>()
                    .Where(item => InputLog.SelectedItems.Contains(item))
                    .ToList();
                
                if (selectedItems.Any())
                {
                    Clipboard.SetText(string.Join(Environment.NewLine, selectedItems));
                }
            }
        }

        // Move MappingLog handling to MappingManager class
        private void MappingManager_MappingEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                midiLog.Insert(0, $"{DateTime.Now:HH:mm:ss.fff} - {message}");
                while (midiLog.Count > 100)
                    midiLog.RemoveAt(midiLog.Count - 1);
            });
        }

        private void SetLedButton_Click(object sender, RoutedEventArgs e)
        {
            if (LedPatternComboBox.SelectedItem is ComboBoxItem item)
            {
                string pattern = item.Content.ToString() ?? "";
                byte patternByte = pattern switch
                {
                    "All Off (0x00)" => 0x00,
                    "1 (0x01)" => 0x01,
                    "2 (0x02)" => 0x02,
                    "3 (0x03)" => 0x03,
                    "4 (0x04)" => 0x04,
                    "Rotating (0x0A)" => 0x0A,
                    "Blinking (0x0B)" => 0x0B,
                    "All On (0x0F)" => 0x0F,
                    _ => 0x00
                };

                bool success = SetLedPattern(patternByte);
                LogLedEvent($"Set LED pattern {pattern}: {(success ? "Success" : "Failed")}");
            }
        }

        private void InitLedButton_Click(object sender, RoutedEventArgs e)
        {
            bool success = InitializeLedControl();
            LogLedEvent($"LED Control Initialization: {(success ? "Success" : "Failed")}");
        }

        private void LogLedEvent(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(logEntry);
            
            try
            {
                // Append to debug log file
                File.AppendAllText(debugLogPath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to debug log: {ex.Message}");
            }
            
            Dispatcher.Invoke(() =>
            {
                LedDebugLog.Items.Insert(0, logEntry);
                while (LedDebugLog.Items.Count > 100)
                    LedDebugLog.Items.RemoveAt(LedDebugLog.Items.Count - 1);
            });
        }

        private void ClearDebugLog()
        {
            try
            {
                // Ensure the Views directory exists
                string directory = Path.GetDirectoryName(debugLogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Clear and initialize the log file
                File.WriteAllText(
                    debugLogPath, 
                    $"Debug log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}"
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear debug log: {ex.Message}");
            }
        }

        private void CheckDevicePaths()
        {
            string setupLogPath = @"C:\Windows\INF\setupapi.dev.log";
            if (File.Exists(setupLogPath))
            {
                var recentEntries = File.ReadLines(setupLogPath)
                    .Where(line => line.Contains("VID_045E") && line.Contains("PID_028E"))
                    .Take(20)
                    .ToList();

                foreach (var entry in recentEntries)
                {
                    LogLedEvent($"Found device path: {entry}");
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint FormatMessage(
            uint dwFlags,
            IntPtr lpSource,
            uint dwMessageId,
            uint dwLanguageId,
            StringBuilder lpBuffer,
            uint nSize,
            IntPtr Arguments);

        private enum EFileAccess : uint
        {
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000
        }

        private enum EFileShare : uint
        {
            None = 0x00000000,
            Read = 0x00000001,
            Write = 0x00000002,
            ReadWrite = Read | Write
        }

        private enum ECreationDisposition : uint
        {
            OpenExisting = 3
        }

        private enum EFileAttributes : uint
        {
            Normal = 0x00000080
        }
    }
}
