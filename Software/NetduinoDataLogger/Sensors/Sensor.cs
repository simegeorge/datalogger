using System;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    public interface Sensor
    {
        bool    Calibrate();
    }
}
