#include "RotaryKY040.h"

// State definitions (same meaning as your #defines)
static const uint8_t R_START     = 0x0;
static const uint8_t R_CW_FINAL  = 0x1;
static const uint8_t R_CW_BEGIN  = 0x2;
static const uint8_t R_CW_NEXT   = 0x3;
static const uint8_t R_CCW_BEGIN = 0x4;
static const uint8_t R_CCW_FINAL = 0x5;
static const uint8_t R_CCW_NEXT  = 0x6;

const uint8_t RotaryKY040::_ttable[7][4] = {
  // R_START
  {R_START,    R_CW_BEGIN,  R_CCW_BEGIN, R_START},
  // R_CW_FINAL
  {R_CW_NEXT,  R_START,     R_CW_FINAL,  (uint8_t)(R_START | CW)},
  // R_CW_BEGIN
  {R_CW_NEXT,  R_CW_BEGIN,  R_START,     R_START},
  // R_CW_NEXT
  {R_CW_NEXT,  R_CW_BEGIN,  R_CW_FINAL,  R_START},
  // R_CCW_BEGIN
  {R_CCW_NEXT, R_START,     R_CCW_BEGIN, R_START},
  // R_CCW_FINAL
  {R_CCW_NEXT, R_CCW_FINAL, R_START,     (uint8_t)(R_START | CCW)},
  // R_CCW_NEXT
  {R_CCW_NEXT, R_CCW_FINAL, R_CCW_BEGIN, R_START},
};

RotaryKY040::RotaryKY040(uint8_t pinDT, uint8_t pinCLK)
: _pinDT(pinDT), _pinCLK(pinCLK), _state(R_START), _counter(0) {}

void RotaryKY040::begin(bool usePullups) {
  if (usePullups) {
    pinMode(_pinDT, INPUT_PULLUP);
    pinMode(_pinCLK, INPUT_PULLUP);
  } else {
    pinMode(_pinDT, INPUT);
    pinMode(_pinCLK, INPUT);
  }
}

RotaryKY040::Dir RotaryKY040::read() {
  // Read CLK and DT. Your original mapping was (CLK<<1)|DT
  uint8_t pinstate = (digitalRead(_pinCLK) << 1) | digitalRead(_pinDT);

  _state = _ttable[_state & 0x0F][pinstate];
  uint8_t result = _state & 0x30;

  if (result == CW)  { _counter++; return CW; }
  if (result == CCW) { _counter--; return CCW; }
  return NONE;
}

int32_t RotaryKY040::count() const { return _counter; }
void RotaryKY040::reset(int32_t v) { _counter = v; _state = R_START; }
