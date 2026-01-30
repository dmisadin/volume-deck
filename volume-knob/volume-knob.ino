// D2-D5 button test -> sends "BTN 2".."BTN 5" on press
const int pins[] = {2, 3, 4, 5};
int lastState[4];
unsigned long lastMs[4];

void setup() {
  Serial.begin(9600);
  for (int i = 0; i < 4; i++) {
    pinMode(pins[i], INPUT_PULLUP);
    lastState[i] = HIGH;
    lastMs[i] = 0;
  }
  delay(300);
  Serial.println("READY");
}

void loop() {
  unsigned long now = millis();

  for (int i = 0; i < 4; i++) {
    int s = digitalRead(pins[i]);

    // Detect press with simple debounce
    if (lastState[i] == HIGH && s == LOW && (now - lastMs[i] > 200)) {
      Serial.println(pins[i]);   // prints 2,3,4,5
      lastMs[i] = now;
    }

    lastState[i] = s;
  }

  delay(5);
}
