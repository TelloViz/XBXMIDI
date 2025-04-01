```
One of my recent projects has been trying to modify some microcontrollers to function as XInput devices, emulating an Xbox controller. The first step in this process is to fetch and then break down the device’s “USB descriptors”. These descriptors are a hierarchy of standardized reports that describe features of the device including who makes it, what version of USB it supports, how it’s powered, and more. By copying the Xbox controller’s descriptors onto my own device, I can convince the computer that my device is also an Xbox controller and will behave like one, and therefore use the Xbox controller’s driver to easily interface with games.

But rather than just copying and pasting the descriptor from one place to another, I want to try and understand exactly what’s going on behind the scenes. I want to understand how the information in these descriptors translates into features of the device’s behavior.


Methodology
I’m using a genuine Microsoft Xbox 360 wired controller, connected to my desktop which is running Windows 10. To sniff the USB data I’m using Wireshark version 2.6.4 with the USBPcap plugin. After plugging in the controller Windows will request the device and configuration descriptors, and the controller will respond in kind. Wireshark provides both the raw data from the USB packet and a breakdown of the values using its built-in analyzers. I’ve verified those breakdowns to the best of my ability using publicly available information about the USB standard.

There are plenty of other tools you can use to read these descriptors, either from the host (PC) or with an inline USB analyzer. This is just what was easy and worked for me. It would have been better to use an inline analyzer with an actual Xbox 360 console, but unfortunately I have neither of those things on hand. Thankfully this method has told me everything I need to know to emulate the control data endpoints.

Some of this information may be a little hard to follow if you’re not familiar with USB communication. There are a number of websites that helped me make sense of these descriptors, including USB Made Simple, USB in a NutShell, and of course the public standards themselves – available at USB.org.

There have also been a few people who have posted online information about how the Xbox 360 controller’s USB works, such as Zach Littell, who created an XInput implementation for the Teensy. There is also some information available in the (now defunct) Free60 wiki via the Internet Archive. These are useful resources, although neither of them did a thorough breakdown of what the descriptor means.

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

The Complete Descriptor
Hopefully that provides a good breakdown of the controller’s USB descriptors and what they mean. Since my end goal is to re-use these descriptors to add XInput support to my own embedded projects, I need these descriptors in C arrays for use in the microcontroller’s USB stack. Here are the full arrays for the controller’s device and configuration descriptors, with comments:

// Xbox 360 Wired Controller USB Descriptors
// Assembled by David Madison
// www.partsnotincluded.com
const byte DeviceDescriptor[] = {
	0x12,        // bLength
	0x01,        // bDescriptorType
	0x00, 0x02,  // bcdUSB (2.0)
	0xFF,        // bDeviceClass
	0xFF,        // bDeviceSubClass
	0xFF,        // bDeviceProtocol
	0x08,        // bMaxPacketSize0
	0x5E, 0x04,  // idEVendor (Microsoft Corp.)
	0x8E, 0x02,  // idProduct (Xbox360 Controller)
	0x14, 0x01,  // bcdDevice
	0x01,        // iManufacturer
	0x02,        // iProduct
	0x03,        // iSerialNumber
	0x01,        // bNumConfigurations
};
const byte ConfigurationDescriptor[] = {
	// Configuration Descriptor
	0x09,        // bLength
	0x02,        // bDescriptorType (CONFIGURATION)
	0x99, 0x00,  // wTotalLength (153)
	0x04,        // bNumInterfaces
	0x01,        // bConfigurationValue
	0x00,        // iConfiguration
	0xA0,        // bmAttributes
	0xFA,        // bMaxPower
	/* ---------------------------------------------------- */
	// Interface 0: Control Data
	0x09,        // bLength
	0x04,        // bDescriptorType (INTERFACE)
	0x00,        // bInterfaceNumber
	0x00,        // bAlternateSetting
	0x02,        // bNumEndpoints
	0xFF,        // bInterfaceClass
	0x5D,        // bInterfaceSubClass
	0x01,        // bInterfaceProtocol
	0x00,        // iInterface
	// Unknown Descriptor (If0)
	0x11,        // bLength
	0x21,        // bDescriptorType
	0x00, 0x01, 0x01, 0x25,  // ???
	0x81,        // bEndpointAddress (IN, 1)
	0x14,        // bMaxDataSize
	0x00, 0x00, 0x00, 0x00, 0x13,  // ???
	0x01,        // bEndpointAddress (OUT, 1)
	0x08,        // bMaxDataSize
	0x00, 0x00,  // ???
	// Endpoint 1: Control Surface Send
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x81,        // bEndpointAddress (IN, 1)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x04,        // bInterval
	// Endpoint 1: Control Surface Receive
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x01,        // bEndpointAddress (OUT, 1)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x08,        // bInterval
	/* ---------------------------------------------------- */
	// Interface 1: Headset (and Expansion Port?)
	0x09,        // bLength
	0x04,        // bDescriptorType (INTERFACE)
	0x01,        // bInterfaceNumber
	0x00,        // bAlternateSetting
	0x04,        // bNumEndpoints
	0xFF,        // bInterfaceClass
	0x5D,        // bInterfaceSubClass
	0x03,        // bInterfaceProtocol
	0x00,        // iInterface
	// Unknown Descriptor (If1)
	0x1B,        // bLength
	0x21,        // bDescriptorType
	0x00, 0x01, 0x01, 0x01,  // ???
	0x82,        // bEndpointAddress (IN, 2)
	0x40,        // bMaxDataSize
	0x01,        // ???
	0x02,        // bEndpointAddress (OUT, 2)
	0x20,        // bMaxDataSize
	0x16,        // ???
	0x83,        // bEndpointAddress (IN, 3)
	0x00,        // bMaxDataSize
	0x00, 0x00, 0x00, 0x00, 0x00, 0x16,  // ???
	0x03,        // bEndpointAddress (OUT, 3)
	0x00,        // bMaxDataSize
	0x00, 0x00, 0x00, 0x00, 0x00,  // ???
	// Endpoint 2: Microphone Data Send
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x82,        // bEndpointAddress (IN, 2)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x02,        // bInterval
	// Endpoint 2: Headset Audio Receive
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x02,        // bEndpointAddress (OUT, 2)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x04,        // bInterval
	// Endpoint 3: Unknown, Send
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x83,        // bEndpointAddress (IN, 3)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x40,        // bInterval
	// Endpoint 3: Unknown, Receive
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x03,        // bEndpointAddress (OUT, 3)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x10,        // bInterval
	/* ---------------------------------------------------- */
	// Interface 2: Unknown
	0x09,        // bLength
	0x04,        // bDescriptorType (INTERFACE)
	0x02,        // bInterfaceNumber
	0x00,        // bAlternateSetting
	0x01,        // bNumEndpoints
	0xFF,        // bInterfaceClass
	0x5D,        // bInterfaceSubClass
	0x02,        // bInterfaceProtocol
	0x00,        // iInterface
	// Unknown Descriptor (If2)
	0x09,        // bLength
	0x21,        // bDescriptorType
	0x00, 0x01, 0x01, 0x22,  // ???
	0x84,        // bEndpointAddress (IN, 4)
	0x07,        // bMaxDataSize
	0x00,        // ???
	// Endpoint 4: Unknown, Send
	0x07,        // bLength
	0x05,        // bDescriptorType (ENDPOINT)
	0x84,        // bEndpointAddress (IN, 4)
	0x03,        // bmAttributes
	0x20, 0x00,  // wMaxPacketSize
	0x10,        // bInterval
	/* ---------------------------------------------------- */
	// Interface 3: Security Method
	0x09,        // bLength
	0x04,        // bDescriptorType (INTERFACE)
	0x03,        // bInterfaceNumber
	0x00,        // bAlternateSetting
	0x00,        // bNumEndpoints
	0xFF,        // bInterfaceClass
	0xFD,        // bInterfaceSubClass
	0x13,        // bInterfaceProtocol
	0x04,        // iInterface
	// Unknown Descriptor (If3)
	0x06,        // bLength
	0x41,        // bDescriptorType
	0x00, 0x01, 0x01, 0x03,  // ???
};
Control Surface Data Format
With the device, configuration, and string descriptors finished, the last step before I can call this breakdown complete is to figure-out how the control surface information is formatted for USB transfer. This is easy enough to figure-out by manipulating the device’s controls and comparing them with the USB datastream from Wireshark. Here is the breakdown:

Control Surfaces (In)


Each update from the controller is a 20-byte packet sent from endpoint 1 IN (0x81) on interface 0. Byte 0 is the message type (0x00) and byte 1 is the length in bytes (0x14 = 20). The packed control surface data is stored in 12 bytes in the center of the message (2 – 13). The final 6 bytes, 14 – 19, are unused (0x00).

Buttons


Bytes 2 and 3 contain the packed states for the controller’s digital buttons in a bit array, where ‘1’ is pressed and ‘0’ is released. This includes all 8 surface buttons, the 2 joystick buttons, the center ‘Xbox’ button, and the directional pad.

From low to high bit, byte 2 maps to the directional pad (up, down, left, right), start, back, L3, and R3 buttons. The first three bits of byte 3 map to the left bumper (LB), right bumper (RB), and ‘Xbox’ button. Bits 4-7 map to the A, B, X, and Y buttons respectively. Bit 3 is unused (0).

Triggers


Bytes 4 and 5 contain 8-bit unsigned integers for the state of the left and right analog triggers (respectively), where a value of ‘0’ is released and a value of ‘255’ is fully depressed.

Joysticks


Bytes 6 – 13 contain the positions of the dual analog joysticks, as 16-bit signed integers. These are stored little endian (low byte first), X axis before Y, with north-east being positive.

The left joystick position is stored in bytes 6, 7, 8, and 9, and the right joystick position is stored in bytes 10, 11, 12, and 13.

Rumble and LED Data (Out)
Updates to the controller’s user feedback features are sent on endpoint 1 OUT (0x01), also on interface 0. These updates follow the same format as the sent data, where byte 0 is the ‘type’ identifier and byte 1 is the packet length (in bytes).

Rumble (0x00)


A type byte of ‘0x00’ indicates a rumble packet. The controller contains two rumble motors: a large weight in the left grip and a small weight in the right grip. The value for both of these motors is updated in a single packet.

The rumble values are 8-bit unsigned integers representing the motor speed, where ‘0’ is off and ‘255’ is max speed. The left motor’s rumble value is stored in index 3, while the right motor’s rumble value is stored in index 4.

This packet is typically 8 bytes long, with bytes 2, 5, 6, and 7 unused (0x00).

LEDs (0x01)


A type byte of ‘0x01’ indicates an LED packet. The controller has four green LEDs around its center ring that display the assigned player number or other status information. This packet selects which LED animation the controller displays. For more information about the LED animations and their timing, see this post.

The identifier for the LED animation is stored in packet index 2. This packet is typically 3 bytes long, with all bytes used.

Conclusion
Phew! Well, that was a long, detailed, dry technical post… Hopefully I’ve explained things well enough. I know I certainly have a better understanding of how the Xbox 360 controller’s USB functionality works, and I hope you do too.

This isn’t a complete breakdown, however. There are still plenty of mysteries left to solve with this controller, not least of which is the security method used to authenticate with the console (interface 3). There has been some success in reverse engineering it, and even a successful man in the middle attack (he even identified the security commands!) – although so far I don’t believe anyone has been able to break it (openly).

The next challenge is applying this knowledge to emulating an Xbox controller using a USB capable microcontroller. Stay tuned!


```