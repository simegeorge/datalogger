using System;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    interface SynchronousSensor : Sensor
    {
        void    ReadAndLog();
    }
}
