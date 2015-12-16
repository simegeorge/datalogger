using System;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    public interface Logger
    {
        void Start( string datetime );
        void Stop();

        void Log( byte[] b );
        void Log( string s );
    }
}
