using System;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    interface AsynchronousSensor : Sensor
    {
        void Start();
        void Stop();
    }
}
