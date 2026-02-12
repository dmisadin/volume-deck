#pragma once
#include <Arduino.h>

class RotaryKY040 {
public:
  enum Dir : uint8_t { NONE = 0x00, CW = 0x10, CCW = 0x20 };

  RotaryKY040(uint8_t pinDT, uint8_t pinCLK);

  void begin(bool usePullups = true);
  Dir read();              // returns CW / CCW / NONE
  int32_t count() const;   // optional stored counter
  void reset(int32_t v = 0);

private:
  uint8_t _pinDT, _pinCLK;
  uint8_t _state;
  int32_t _counter;

  static const uint8_t _ttable[7][4];
};
