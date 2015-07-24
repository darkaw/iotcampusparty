/*
* IoT Dimmer Light Test
* 
* This sketch connects to a control site using an Arduino Wiznet
* Ethernet shield and outputs the directed intensity as a binary 
* number on pins 5 - 7, where Most Significant bit is 7.
* 
* Circuit:
*   Ethernet shield attached to pins 10, 11, 12, 13
* 
* Created July 9th, 2015
* by Ulises Ruiz, Juan Romero and Xavier Guzman.
* 
* Copyright (c) 2015 Grupo Flextronics, S.A. de C.V.
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
* 
*/
// Pin 13 has an LED connected on most Arduino boards.
// give it a name:

// the setup routine runs once when you press reset:
void setup() {
  // initialize the digital pin as an output.
  pinMode(5, OUTPUT);
  pinMode(6, OUTPUT);
  pinMode(7, OUTPUT);
  pinMode(8, OUTPUT);
  turnSignal(HIGH); 
}

// the loop routine runs over and over again forever:
void loop() {
  outputIntensity(0);delay(1000);               // wait for a second
  turnSignal(LOW);
  outputIntensity(1);delay(1000);               // wait for a second
  turnSignal(HIGH);
  outputIntensity(2);delay(1000);               // wait for a second
  turnSignal(LOW);
  outputIntensity(3);delay(1000);               // wait for a second
  turnSignal(HIGH);
  outputIntensity(4);delay(1000);               // wait for a second
  turnSignal(LOW);
  outputIntensity(5);delay(1000);               // wait for a second
  turnSignal(HIGH);
  outputIntensity(6);delay(1000);               // wait for a second
  turnSignal(LOW);
  outputIntensity(7);delay(1000);               // wait for a second
  turnSignal(HIGH);
}

void outputIntensity(int intensity)
{
  Serial.print("Changing intensity to: "); Serial.println(intensity);
  digitalWrite(5, intensity & 0x01? HIGH : LOW);
  digitalWrite(6, intensity & 0x02? HIGH : LOW);
  digitalWrite(7, intensity & 0x04? HIGH : LOW);
}

void turnSignal(int state) {
  digitalWrite(8, state);
}