```
The Device Descriptor
The top level descriptor is the device descriptor. This is the big picture – it tells the host what USB standard to use when talking to the device, the identification for who made it and what product it is, and the number of configuration descriptors that follow.

Here’s the wired controller’s full device descriptor as reported by Wireshark:

bLength: 18
bDescriptorType: 0x01 (DEVICE)
bcdUSB: 0x0200
bDeviceClass: 0xFF (Vendor Specific)
bDeviceSubClass: 0xFF
bDeviceProtocol: 0xFF
bMaxPacketSize0: 8
idVendor: 0x045E (Microsoft Corp.)
idProduct: 0x028E (Xbox360 Controller)
bcdDevice: 0x0114
iManufacturer: 1
iProduct: 2
iSerialNumber: 3
bNumConfigurations: 1
Raw Hex: 0x12 0x01 0x00 0x02 0xff 0xff 0xff 0x08 0x5e 0x04 0x8e 0x02 0x14 0x01 0x01 0x02 0x03 0x01
The device descriptor (bDescriptorType of 0x01) is 18 bytes in length (bLength). It tells us that the controller uses USB 2.0 (bcdUSB), and that the max packet size for endpoint 0 (the device control endpoint) is 8 bytes (bMaxPacketSize0).

The device’s USB device class, subclass, and device protocol are all set at 0xFF (bDeviceClass, bDeviceSubClass, and bDeviceProtocol). This indicates that these are vendor-specific and don’t follow any existing USB device standards.

The Vendor ID (VID) identifies the controller as being made by Microsoft Corporation (idVendor), and the Product ID (PID) identifies it as an Xbox 360 Controller (idProduct). This specific controller is hardware version 0x0114 (bcdDevice). This is a vendor-defined version number that is encoded as a binary coded decimal (bcd). With 8421 encoding this translates to decimal numbers ‘1’, ‘1’, ‘4’, which could possibly mean in semantic versioning that this controller is version 1.1.4.

The strings describing the manufacturer, product, and serial number are in indices 1, 2, and 3 respectively (iManufacturer, iProduct, and iSerialNumber). Lastly, the device only has one configuration (bNumConfigurations).

Configuration Descriptor
Next up is the configuration descriptor. For each configuration, this describes how the device is powered and how many interfaces it has. This can be interpreted as a “header” of sorts for the following interface and endpoint descriptors.

Per the device descriptor above, the wired Xbox 360 controller only has one configuration:

bLength: 9
bDescriptorType: 0x02 (CONFIGURATION)
wTotalLength: 153
bNumInterfaces: 4
bConfigurationValue: 1
iConfiguration: 0
bmAttributes: 0b10100000 (NOT SELF-POWERED, REMOTE-WAKEUP)
bMaxPower: 250 (500 mA)
Raw Hex: 0x09 0x02 0x99 0x00 0x04 0x01 0x00 0xa0 0xfa
The configuration descriptor (bDescriptorType of 0x02) is 9 bytes long (bLength). The total length of all following interface and endpoint descriptors (including the 9 bytes for the configuration descriptor itself) is 153 bytes (wTotalLength). The device contains a total of 4 interfaces (bNumInterfaces) and the value used to select this specific configuration is 1 (bConfigurationValue). From the available string descriptors, no string describes this configuration (iConfiguration of 0).

The device attributes (bmAttributes) is a bitmap of boolean flags describing the device’s capabilities. Bits 0-4 are reserved and are 0 in the descriptor. Bit 5 is ‘1’ indicating that the controller supports remote wakeup (it can wake the host from standby). Bit 6 is ‘0’ indicating that the device is not self-powered, and bit 7 is ‘1’ indicating that the device is powered by the bus.

Since the device attributes say the controller is powered by the bus, the bMaxPower variable tells the host how much power the controller can drain in 2 mA units. In this case, 500 mA.

Interface Descriptors
Immediately following the configuration descriptor are the interface descriptors. The interface descriptors group the lower-level endpoints into logical groups that perform a single function of the device.

According to the configuration descriptor, the wired Xbox 360 controller has 4 interfaces.

Interface 0: Control Data
bLength: 9
bDescriptorType: 0x04 (INTERFACE)
bInterfaceNumber: 0
bAlternateSetting: 0
bNumEndpoints: 2
bInterfaceClass: 0xFF (Vendor Specific)
bInterfaceSubClass: 0x5D
bInterfaceProtocol: 0x01
iInterface: 0
Raw Hex: 0x09 0x04 0x00 0x00 0x02 0xff 0x5d 0x01 0x00
The first interface descriptor (bDescriptorType of 0x04) is 9 bytes long (bLength). This is the first interface (bInterfaceNumber, indexed at 0). It contains two endpoints (bNumEndpoints), and has no string descriptor describing it (iInterface indexed to 0).

All four interfaces use a vendor-specific interface class (bInterfaceClass). This makes it impossible to infer anything specific about the interface subclass or protocol, since they are proprietary. The USB specification also allows setting (bAlternateSetting) to mark an interface descriptor as an alternate. None of the four controller’s interfaces make use of this feature.

From testing, it appears that this interface is used for sending / receiving control data.

Unknown Descriptor (If0)
Immediately following the interface descriptor is an additional unknown descriptor of type 0x21. This bDescriptorType ID isn’t listed in any of the public USB standards I can find, and none of the USB analysis programs I’m using seem to know either. My best guess is that this is some sort of vendor-specific descriptor that contains more specific details about the interface layout, much like an HID usage page.

All four interfaces have one of these unknown descriptors, using either type 0x21 or type 0x41 (Interface 3).

bLength: 17
bDescriptorType: 0x21 (UNKNOWN)
???: 0x00, 0x01, 0x01
???: 0x25
bEndpointAddress: 0x81 IN Endpoint: 1
bMaxDataSize: 20
???: 0x00, 0x00, 0x00, 0x00, 0x13
bEndpointAddress: 0x01 OUT Endpoint: 1
bMaxDataSize: 8
???: 0x00, 0x00
Raw Hex: 0x11 0x21 0x00 0x01 0x01 0x25 0x81 0x14 0x00 0x00 0x00 0x00 0x13 0x01 0x08 0x00 0x00
The first two bytes are defined by the USB standard, and must refer to the length of the descriptor (bLength) and the descriptor type (bDescriptorType). Past that the attributes for the rest of the descriptor are anyone’s guess.

I’m certain that the endpoint numbers for the interface are included here along with their expected packet sizes. 0x81 is the endpoint for the control data sent from the controller to the host, and is usually 20 bytes long (0x14, the next number after the endpoint). 0x01 is the endpoint for the control data received from the host and can be up to 8 bytes long when receiving rumble data (0x08, also the next number after its endpoint). The endpoint numbers at least can be confirmed by modifying the bEndpointAddresss value in the endpoint descriptors themselves – if these unknown descriptor values are not changed as well, the Windows driver will not communicate properly.

In addition to that, the first three bytes after the bDescriptorType seem to be standardized between the unknown descriptors: 0x00, 0x01, 0x01. I don’t know what these values refer to, or what the other ??? values refer to. Hopefully someone with more USB knowledge may be able to fill me in.

Endpoint 1 IN: Control Data Send
The next descriptor is the first endpoint descriptor. The endpoint descriptors describe the lowest level portions of the bus, and help the host determine the bandwidth requirements.

bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x81 IN Endpoint: 1
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 4
Raw Hex: 0x07 0x05 0x81 0x03 0x20 0x00 0x04
This first endpoint descriptor (bDescriptorType of 0x05) is 7 bytes long (bLength). The field bEndpointAddress specifies the endpoint number and endpoint direction. Bits 0-3 describe the endpoint number (0x#1 = 1), and bit 7 describes the direction (0x8# = IN, from the perspective of the host). The max number of bytes it’s capable of sending in a single update is 32 (wMaxPacketSize).

This endpoint uses an interrupt transfer type (bmAttributes bits 0 and 1 set high). With the interrupt transfer type, the bInterval setting specifies the polling interval in USB ‘frames’. For low speed and full speed devices each ‘frame’ is 1 ms, meaning that this endpoint is polled for new data every 4 ms, or 250 Hz.

Note that although this is an “in” endpoint, it’s titled as control data send because the “in” and “out” terminology refers to the connection from the host’s perspective. Bear that in mind when your look at the endpoint direction.

This is the endpoint used to send the control surface information from the controller to the computer.

Endpoint 1 OUT: Control Data Receive
bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x01 OUT Endpoint: 1
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 8
Raw Hex: 0x07 0x05 0x01 0x03 0x20 0x00 0x08
The second endpoint descriptor is almost identical to the first, except that it’s an OUT endpoint (receiving data from the host), and updates at half of the speed (bInterval of 8 rather than 4, or 125 Hz vs 250 Hz).

This is the endpoint used to receive control data information sent from the computer to the controller, including LED patterns and rumble motor data.

Interface 1: Headset (and Expansion Port?)
bLength: 9
bDescriptorType: 0x04 (INTERFACE)
bInterfaceNumber: 1
bAlternateSetting: 0
bNumEndpoints: 4
bInterfaceClass: 0xFF (Vendor Specific)
bInterfaceSubClass: 0x5D
bInterfaceProtocol: 0x03
iInterface: 0
Raw Hex: 0x09 0x04 0x01 0x00 0x04 0xff 0x5d 0x03 0x00
Interface 1 is quite similar to Interface 0, except that it has 4 endpoints rather than 2 (bNumEndpoints) and uses a bInterfaceProtocol (vendor-specific, unknown) of 0x03 rather than 0x01.

From testing, it appears that this interface handles the controller’s headset. Since they’re physically connected, I’m also assuming this handles the expansion port at the bottom of the controller.

Unknown Descriptor (If1)
bLength: 27
bDescriptorType: 0x21 (UNKNOWN)
???: 0x00, 0x01, 0x01
???: 0x01
bEndpointAddress: 0x82 IN Endpoint: 2
bMaxDataSize: 64
???: 0x01
bEndpointAddress: 0x02 OUT Endpoint: 2
bMaxdataSize: 32
???: 0x16
bEndpointAddress: 0x83 IN Endpoint: 3
bMaxDataSize: 0
???: 0x00, 0x00, 0x00, 0x00, 0x00, 0x16
bEndpointAddress: 0x03 OUT Endpoint: 3
bMaxDataSize: 0
???: 0x00, 0x00, 0x00, 0x00, 0x00
Raw Hex: 0x1b 0x21 0x00 0x01 0x01 0x01 0x82 0x40 0x01 0x02 0x20 0x16 0x83 0x00 0x00 0x00 0x00 0x00 0x00 0x16 0x03 0x00 0x00 0x00 0x00 0x00 0x00
Another unknown descriptor, this one significantly longer and packed with more juicy unknown details. If my assumptions about the endpoint addresses and data packet lengths hold true, then this seems to indicate that the pair of #3 endpoints aren’t expecting any data whatsoever. How bizarre…

Endpoint 2 IN: Microphone Data Send
bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x82 IN Endpoint: 2
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 2
Raw Hex: 0x07 0x05 0x82 0x03 0x20 0x00 0x02
Similar to the endpoints for interface 0, the first endpoint for interface 1 uses an interrupt transfer type, receiving a max of 32 bytes every 2 ms. It’s an IN endpoint with address 2.

This is the endpoint used to send the microphone data from the controller’s headset port to the host.

Endpoint 2 OUT: Headset Audio Receive
bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x02 OUT Endpoint: 2
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 4
Raw Hex: 0x07 0x05 0x02 0x03 0x20 0x00 0x04
This OUT endpoint is almost identical to its sister “IN” endpoint, except that it functions at half of the rate (bInterval of 4 rather than 2).

This is the endpoint used to push audio data from the host to the controller’s mono headset output.

Endpoint 3 IN: Unknown, Send
bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x83 IN Endpoint: 3
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 64
Raw Hex: 0x07 0x05 0x83 0x03 0x20 0x00 0x40
This IN endpoint is similar to the other two audio endpoints, but it defines a significantly longer bInterval of 64 frames.

Since this is bundled with the headset audio endpoints, this endpoint is presumably used for the 4-pin expansion port on the bottom of the controller. I tried connecting an Xbox 360 chatpad to the controller’s expansion port but didn’t see any data reported over USB. Perhaps the chatpad need to be initialized somehow, though I don’t have an actual Xbox 360 to test with.

Endpoint 3 OUT: Unknown, Receive
bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x03 OUT Endpoint: 3
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 16
Raw Hex: 0x07 0x05 0x03 0x03 0x20 0x00 0x10
This OUT endpoint is similar to the other three endpoints above, but defines a shorter bInterval of 16 frames.

Again I’d assume this endpoint is used for the 4-pin expansion port next to the headset jack, although I haven’t noticed any data on the endpoint from my PC except on initialization.

Interface 2: Unknown
bLength: 9
bDescriptorType: 0x04 (INTERFACE)
bInterfaceNumber: 2
bAlternateSetting: 0
bNumEndpoints: 1
bInterfaceClass: 0xFF (Vendor Specific)
bInterfaceSubClass: 0x5D
bInterfaceProtocol: 0x02
iInterface: 0
Raw Hex: 0x09 0x04 0x02 0x00 0x01 0xff 0x5d 0x02 0x00
Interface 2 is very similar to Interface 1 and 0, except that it has only one endpoint (bNumEndpoints) and uses a bInterfaceProtocol (vendor-specific, unknown) of 0x02 rather than 0x01 (If0) or 0x02 (If1).

Unknown Descriptor (If2)
bLength: 9
bDescriptorType: 0x21 (UNKNOWN)
???: 0x00, 0x01, 0x01
???: 0x22
bEndpointAddress: 0x84 IN Endpoint: 4
bMaxDataSize: 7
???: 0x00
Raw Hex: 0x09 0x21 0x00 0x01 0x01 0x22 0x84 0x07 0x00
Yet another unknown descriptor to go with the unknown interface. Significantly less to unpack compared to the more complicated unknown descriptor from Interface 1, although just as mysterious.

Endpoint 4 IN: Unknown, Send
bLength: 7
bDescriptorType: 0x05 (ENDPOINT)
bEndpointAddress: 0x84 IN Endpoint: 4
bmAttributes: 0x03
wMaxPacketSize: 32
bInterval: 16
Raw Hex: 0x07 0x05 0x84 0x03 0x20 0x00 0x10
This is the last endpoint defined by the configuration descriptor. It is yet another interrupt-type data output (IN) endpoint, using an interval of 16 ms (bInterval). In my testing the Windows driver never polls this endpoint for data, so I don’t know what it is used for.

Interface 3: Security Method
bLength: 9
bDescriptorType: 0x04 (INTERFACE)
bInterfaceNumber: 3
bAlternateSetting: 0
bNumEndpoints: 0
bInterfaceClass: 0xFF (Vendor Specific)
bInterfaceSubClass: 0xFD
bInterfaceProtocol: 0x13
iInterface: 4
Raw Hex: 0x09 0x04 0x03 0x00 0x00 0xff 0xfd 0x13 0x04
The last interface has no endpoints associated with it, but does have a string descriptor associated with it (iInterface). The string descriptor at index 4 reads:

Xbox Security Method 3, Version 1.00, © 2005 Microsoft Corporation. All rights reserved.

This indicates that although it has no associated endpoint, this interface is used for the controller security method that ensures only genuine or licensed controllers can connect to the Xbox 360 console. From my testing it appears that this security method isn’t implemented on the Windows 10 driver.

Unknown Descriptor (If3)
bLength: 6
bDescriptorType: 0x41 (UNKNOWN)
???: 0x00, 0x01, 0x01
???: 0x03
Raw Hex: 0x06 0x41 0x00 0x01 0x01 0x03
Finally the last unknown descriptor. This is of a different bDescriptorType than the other unknowns (0x41 rather than 0x21). As this interface has no endpoints, this unknown descriptor has no bEndpointAddress fields. The typical 3-byte start is there, as is one additional ‘0x03’ byte. I still have no clue what the significance of these bytes is.

This unknown descriptor marks the end of the configuration descriptor, at a total of 153 bytes.

String Descriptors
Last but not least are the string descriptors. These are optional, human-readable information that can be linked to device information, configuration descriptors, or interface descriptors by index. I obtained the string descriptors using this tool, which was also used to double-check the other descriptors.

Index	LANGID	String
0x01	0x0000	"©Microsoft Corporation"
0x02	0x0000	"Controller"
0x03	0x0000	"08FEC93"
0x04	0x0000	"Xbox Security Method 3, Version 1.00, © 2005 Microsoft Corporation. All rights reserved."
According to the Xbox 360 wired controller’s device descriptor, the manufacturer is provided by string 0x01 (iManufacturer), the product by string 0x02 (iProduct), and the serial number by string 0x03 (iSerialNumber). The fourth descriptor is used to describe interface 3 (iInterface).

I’m assuming that the serial number string is different for each controller, although I only have one controller on hand so I can’t compare them.
```