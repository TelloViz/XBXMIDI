# Xbox 360 Controller LED Control DLL Project

## Project Overview
Create a C++ DLL that allows programmatic control of the LED ring lights on a wired Xbox 360 controller, for use with a C# application. The project implements low-level USB communication to send LED control commands directly to the controller.

## Technical Specifications

### USB Device Details
- Device Class: 0xFF (Vendor Specific)
- Device SubClass: 0xFF
- Device Protocol: 0xFF
- Vendor ID (VID): 0x045E (Microsoft)
- Product ID (PID): 0x028E (Xbox 360 Controller)
- Interface 0: Control Data
  - OUT Endpoint (0x01): Used for LED commands
  - Packet Size: 8 bytes maximum
  - Max Control Endpoint Size: 8 bytes
  - Update Rate: 125 Hz (bInterval: 8ms)

### LED Control Protocol
Command Structure (8 bytes total):
- Byte 0: 0x01 (LED command type)
- Byte 1: 0x03 (packet length)
- Byte 2: LED pattern value
- Bytes 3-7: Padding (0x00)

### LED Pattern Values
```cpp
enum LEDPattern {
    OFF = 0x00,
    BLINK_ALL = 0x01,
    FLASH_1 = 0x02,
    FLASH_2 = 0x03,
    FLASH_3 = 0x04,
    FLASH_4 = 0x05,
    ON_1 = 0x06,
    ON_2 = 0x07,
    ON_3 = 0x08,
    ON_4 = 0x09,
    ROTATE = 0x0A,
    BLINK = 0x0B,
    SLOW_BLINK = 0x0C,
    ALTERNATE = 0x0D
};
```

## Implementation Requirements

### Core DLL Functions

#### 1. Device Management
```cpp
extern "C" {
    XBOXLED_API bool Initialize();  // Find and connect to controller
    XBOXLED_API bool IsConnected();  // Check connection status
    XBOXLED_API void Cleanup();  // Release resources
}
```

#### 2. LED Control
```cpp
extern "C" {
    XBOXLED_API bool SetLEDPattern(LEDPattern pattern);
    XBOXLED_API LEDPattern GetCurrentPattern();
}
```

### USB Communication Details
1. Device Enumeration:
   - Use SetupDiGetClassDevs with GUID_DEVINTERFACE_HID
   - Validate device using VID/PID match
   - Access Interface 0 for LED control

2. Command Sending:
   - Use HidD_SetOutputReport() (not WriteFile)
   - Ensure 8-byte aligned packets
   - Send to OUT Endpoint 0x01
   - Include proper padding bytes

### Error Handling
- Device Connection Errors
  - Device not found (return false)
  - Access denied (return false)
  - Invalid handle (return false)
- Communication Errors
  - Error 87 (Invalid Parameters)
  - Timeout errors
  - Write failures
- Resource Management
  - Proper handle cleanup
  - Memory deallocation
  - Thread safety

## Development Requirements
- Visual Studio 2022
- Windows SDK (for USB/HID APIs)
- C++17 or later
- Required Libraries:
  - setupapi.lib
  - hid.lib

## C# Integration Example
```csharp
public class Xbox360Controller : IDisposable
{
    [DllImport("Xbox360LED.dll")]
    private static extern bool Initialize();

    [DllImport("Xbox360LED.dll")]
    private static extern bool SetLEDPattern(LEDPattern pattern);

    [DllImport("Xbox360LED.dll")]
    private static extern void Cleanup();

    public Xbox360Controller()
    {
        if (!Initialize())
            throw new InvalidOperationException("Failed to initialize controller");
    }

    public void SetLED(LEDPattern pattern)
    {
        if (!SetLEDPattern(pattern))
            throw new InvalidOperationException("Failed to set LED pattern");
    }

    public void Dispose()
    {
        Cleanup();
    }
}
```