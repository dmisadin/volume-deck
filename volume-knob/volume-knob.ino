#include "RotaryKY040.h"
#include <Adafruit_SSD1306.h>
#define CLK 5
#define DT  4
#define SW  6

enum SerialVolumeControl : byte { 
  PreviousSession = 2, 
  NextSession = 3, 
  VolumeDown = 4,
  VolumeUp = 5,
  MuteToggle = 6
};

const String handshakeKey = "VOLUME_KNOB_READY";
const String handshakeQuestion = "VOLUME_KNOB_REQUEST";
bool isConnected = false;
const int pins[] = {2, 3, SW};
const int PIN_COUNT = sizeof(pins) / sizeof(pins[0]);
int lastState[PIN_COUNT];
unsigned long lastMs[PIN_COUNT];

RotaryKY040 encoder(DT, CLK);
Adafruit_SSD1306 display(128, 64, &Wire, 4);

void setup() {
  Serial.begin(9600);
  for (int i = 0; i < PIN_COUNT; i++) {
    pinMode(pins[i], INPUT_PULLUP);
    lastState[i] = HIGH;
    lastMs[i] = 0;
  }
  delay(300);
  Serial.println(handshakeKey);
  encoder.begin(true);
  setupOLED();
}

void loop() {
  if (isConnected || HandleHandshakeRequest())
  {
    unsigned long now = millis();

    for (int i = 0; i < PIN_COUNT; i++) {
      int s = digitalRead(pins[i]);

      // Detect press with simple debounce
      if (lastState[i] == HIGH && s == LOW && (now - lastMs[i] > 200)) {
        Serial.println(pins[i]);   // prints 2,3,6
        lastMs[i] = now;
      }

      lastState[i] = s;
    }

    auto dir = encoder.read();
    if (dir == RotaryKY040::CW)
      Serial.println(SerialVolumeControl::VolumeUp);
    else if (dir == RotaryKY040::CCW)
      Serial.println(SerialVolumeControl::VolumeDown);

    UpdateOLED();
  }
}

bool HandleHandshakeRequest()
{
  if (!Serial.available())
    return false;

  String line = Serial.readStringUntil('\n');
  line.trim();

  if (line == handshakeQuestion)
  {
    Serial.println(handshakeKey);
    isConnected = true;
    return true;
  }

  return false;
}

// Protocol: [0xAA][TYPE][LENGTH][STRING...]
static const uint8_t SOF = 0xAA;

enum RxState : byte {
  WAIT_SOF,
  READ_TYPE,
  READ_LEN,
  READ_PAYLOAD
};

enum FrameType : byte {
  Ping = 0,
  Pong = 1,
  UpdateTopText = 2,
  UpdateBottomText = 3
};

void handleMessage(FrameType type, const char* s);
void updateTopText(const char* text);
void updateBottomText(const char* text);

RxState state = WAIT_SOF;

FrameType msgType = UpdateTopText;
uint8_t msgLen = 0;
uint8_t idx = 0;

char buf[32];
char topText[24] = "";
char bottomText[6] = "";

void setupOLED() {
  if (!display.begin(SSD1306_SWITCHCAPVCC, 0x3C)) {
    Serial.println(F("SSD1306 allocation failed"));
    for(;;);
  }
  display.clearDisplay(); 
  analogReference(INTERNAL);

  updateTopText("Volume Deck v1.0");
  updateBottomText("Hi!");
}

void UpdateOLED() {
  while (Serial.available() > 0) {
    uint8_t b = (uint8_t)Serial.read();

    switch (state) {
      case WAIT_SOF:
        if (b == SOF) state = READ_TYPE;
        break;

      case READ_TYPE:
        msgType = b;
        state = READ_LEN;
        break;

      case READ_LEN:
        msgLen = b;
        idx = 0;

        if (msgLen > sizeof(buf)) {
          state = WAIT_SOF;
          break;
        }

        if (msgLen == 0) {
          buf[0] = '\0';
          handleMessage(msgType, buf);
          state = WAIT_SOF;
        } else {
          state = READ_PAYLOAD;
        }
        break;

      case READ_PAYLOAD:
        // If payload is ASCII/UTF-8 text, just store bytes
        buf[idx++] = (char)b;

        if (idx >= msgLen) {
          buf[msgLen] = '\0'; // null terminate for C string functions
          handleMessage(msgType, buf);
          state = WAIT_SOF;
        }
        break;
    }
  }
}

void handleMessage(FrameType type, const char* s)
{
  switch (type)
  {
    case FrameType::Ping:
      Serial.println(FrameType::Pong);
      return;

    case FrameType::UpdateTopText:
      strncpy(topText, s, sizeof(topText) - 1);
      topText[sizeof(topText) - 1] = '\0';
      break;

    case FrameType::UpdateBottomText:
      strncpy(bottomText, s, sizeof(bottomText) - 1);
      bottomText[sizeof(bottomText) - 1] = '\0';
      break;
  }

  renderDisplay();
}

void renderDisplay() {
  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);

  int16_t x1, y1;
  uint16_t w, h;

  display.setTextSize(1);
  display.getTextBounds(topText, 0, 0, &x1, &y1, &w, &h);

  int topX = (128 - w) / 2;  // 128 = screen width
  display.setCursor(topX, 0);
  display.println(topText);

  display.setTextSize(3);
  display.getTextBounds(bottomText, 0, 0, &x1, &y1, &w, &h);

  int bottomX = (128 - w) / 2;
  display.setCursor(bottomX, 30);
  display.print(bottomText);

  display.display();
}

void updateTopText(const char* text)
{
  strncpy(topText, text, sizeof(topText) - 1);
  topText[sizeof(topText) - 1] = '\0';
  renderDisplay();
}

void updateBottomText(const char* text)
{
  strncpy(bottomText, text, sizeof(bottomText) - 1);
  bottomText[sizeof(bottomText) - 1] = '\0';
  renderDisplay();
}
