using System;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    public interface Formatter
    {
        void FormatStart();
        void FormatTimeStamp( int ts );

        void FormatAccel( float x, float y, float z );
        void FormatPosition( int lat, int lon, int pdop );
        void FormatSpeed( int speed );
        void FormatTime( int tow );
        void FormatAltitude( int sea_level_alt, int vdop );
        void FormatHeading( int heading, int accuracy );
        void FormatDate( int day, int month, int year, int hours, int minutes, int seconds );
        void FormatRPM( int rpm );
        void FormatThrottlePosition( int throttle );
    }
}
