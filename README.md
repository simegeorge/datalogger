# Datalogger Readme

This project is a prototype data logger for track-day driving. The idea is to collect various information as a car is being driven around a racetrack, including its position, speed, acceleration (lateral and longitudinal) together with engine parameters such as RPM and throttle position, The collected data can then be analysed later to identify consistency and areas to improve on.

The hardware platform consists of a [Netduino](http://www.netduino.com/) board connected to a [3-axis accelerometer](https://www.coolcomponents.co.uk/adxl337-breakout.html) and [GPS sensor](https://www.coolcomponents.co.uk/venus-gps-with-sma-connector.html), with data captured to an SD card. I designed, and had manufactured, an accompanying PCB shield to interface to the car's ECU using the standard OBD protocol. The software is written in C# using the [.Net micro framework](http://www.netmf.com/)

### Prototype hardware

![Datalogger](https://github.com/simegeorge/datalogger/blob/master/Images/Datalogger.jpg?raw=true "Datalogger")

![Datalogger showing OBD shield](https://github.com/simegeorge/datalogger/blob/master/Images/Datalogger%20with%20OBD%20shield.jpg?raw=true "Datalogger showing OBD shield")

### OBD shield

![PCB](https://github.com/simegeorge/datalogger/blob/master/Images/OBD%20PCB.png?raw=true "PCB")

![Eagle screenshot](https://github.com/simegeorge/datalogger/blob/master/Images/Eagle%20Screenshot.png?raw=true "Eagle screenshot")
