# Volume Deck

A small USB volume controller for Windows built with an Arduino Nano, KY-040 rotary encoder, and a 0.96" SSD1306 OLED display.

![Volume Deck product](/docs/product.jpg)

The device allows you to quickly switch between audio sessions and control their volume using a physical knob, similar to a simplified Stream Deck but focused on audio control.

The Arduino communicates with a .NET desktop application over serial using a small binary protocol.

![Volume Deck schema](/docs/schema.png)

## Features
- Rotary encoder volume control
- Per-application audio session switching
- Mute toggle
- OLED display showing active session
- Automatic USB connection detection
- Fast binary serial protocol