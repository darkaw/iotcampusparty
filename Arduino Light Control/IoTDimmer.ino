/*
* IoT Dimmer
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

#include <SPI.h>
#include <Ethernet.h>

// Constants
#define MAX_BUFFER                128
#define SIGNIFICANCE_1              5
#define SIGNIFICANCE_2              6
#define SIGNIFICANCE_3              7
#define SIGNAL_PIN                  8
#define ETHERNET_SDCARD_SELECTOR    4
byte    MAC_ADDRESS[]               =   {0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED};
char*   CONTROL_SERVER_ADDRESS      =   "10.106.201.65";    
char*   VALID_TOKEN                 =   "{letmein";    
char*   DEVICE_NAME                 =   "device1";    
int     CONTROL_SERVER_PORT         =   9000;

// Global Variables.
EthernetClient client;



void setup() {
  // Open serial communications and wait for port to open:
  Serial.begin(9600);
  while (!Serial) {
    ;           // wait for serial port to connect. Needed for Leonardo only
  }
  
  // Set all pins to output mode
  pinMode(SIGNIFICANCE_1, OUTPUT);
  pinMode(SIGNIFICANCE_2, OUTPUT);
  pinMode(SIGNIFICANCE_3, OUTPUT);
  pinMode(SIGNAL_PIN,     OUTPUT);
  
  // Turn off light initially
  outputIntensity(0);
  
  turnSignal(HIGH);
  
  Serial.println("Waiting for DHCP...");
  
  // start the Ethernet connection:
  while (Ethernet.begin(MAC_ADDRESS) == 0) {
    Serial.println("Failed to configure Ethernet using DHCP!");
    delay(1000);
    Serial.println("Retrying DHCP...");
  }
  Serial.print("DHCP Initialized. IP Address is: "); Serial.println(Ethernet.localIP());
  
  // Give the Ethernet shield a second to initialize:
  delay(1000);
}


void loop()
{
    Serial.println("Connecting to control server:");
    char* receiveBuffer = (char*)malloc(MAX_BUFFER);
    int receiveBufferIndex = 0;
    if (client.connect(CONTROL_SERVER_ADDRESS, CONTROL_SERVER_PORT)) {
      delay(1000);
      Serial.println("Connected. Waiting for commands...");
      turnSignal(LOW); // We are connected so set setup signal to off
      while(true) {
        while (client.available()) {
          char incomingChar = client.read();
          receiveBuffer[receiveBufferIndex ++] = incomingChar;
          if(receiveBufferIndex > 2) {
            if(receiveBuffer[receiveBufferIndex - 2] == '\r' && receiveBuffer[receiveBufferIndex - 1] == '\n') {
              receiveBuffer[receiveBufferIndex] = 0;
              handleServerCommand(receiveBuffer); ;
              receiveBufferIndex = 0;
            }
          }
          delay(10);
        } // while(client.available())
      } // while (true)
    } else {
      Serial.println("connection failed");
      delay(5000);
  }
}

void handleServerCommand(char* serverMessage)
{
    Serial.print("Received message from control server: ");
    Serial.println(serverMessage);
    int serverMessageLength = strlen(serverMessage);
    int tokenLength = strlen(VALID_TOKEN);
    if(serverMessageLength > tokenLength) {
      
      char* indexOfColon = strchr(serverMessage, ':');
      
      if(indexOfColon !=  NULL ) {
        *indexOfColon = 0;
        
        char* seekPointer = indexOfColon + 1;
        
        if(!strcmp(serverMessage, VALID_TOKEN)) {
          
          while(isspace(*seekPointer)) seekPointer ++;
          
          char* indexOfEqualTo = strchr(seekPointer, '=');
          
          if(indexOfEqualTo !=  NULL ) {
            *indexOfEqualTo = 0;
            indexOfEqualTo ++;
            
            if(!strcmp(seekPointer, DEVICE_NAME)) {
              int intensityValue = *indexOfEqualTo - '0';
              if(intensityValue >= 0 && intensityValue <= 7) {
                outputIntensity(intensityValue);
              } else {
                Serial.print("Invalid message: Invalid intensity: "); Serial.println(intensityValue);
              }
            } else {
              Serial.print("Invalid message: Missmatch device name: "); Serial.println(seekPointer);
            }
          } else {
            Serial.println("Invalid message: No '='");
          }
        } else {
          Serial.println("Invalid message: invalid token");
        }
      } else {
        Serial.println("Invalid message: No ':'");
      }
    }  else {
      Serial.println("Invalid message: Size is "); Serial.println(serverMessageLength);
    }
}

void outputIntensity(int intensity)
{
  Serial.print("Changing intensity to: "); Serial.println(intensity);
  digitalWrite(SIGNIFICANCE_1, intensity & 0x01? LOW : HIGH);
  digitalWrite(SIGNIFICANCE_2, intensity & 0x02? LOW : HIGH);
  digitalWrite(SIGNIFICANCE_3, intensity & 0x04? LOW : HIGH);
}

void turnSignal(int state) {
  digitalWrite(SIGNAL_PIN, state);
}


