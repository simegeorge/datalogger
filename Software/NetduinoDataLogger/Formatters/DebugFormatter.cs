using System;
using System.Text;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    //---   Simple formatter implementation for debugging

    public class DebugFormatter : Formatter
    {
        private Logger  logger_;

        public DebugFormatter( Logger logger )
        {
            logger_ = logger;
        }

        public void FormatStart()
        {
            logger_.Log( "Start" );
        }

        public void FormatTimeStamp( int ts )
        {
        }

        public void FormatDate( int day, int month, int year, int hours, int minutes, int seconds )
        {
        }

        public void FormatRPM( int rpm )
        {
            logger_.Log( "RPM: " + rpm );
        }

        public void FormatThrottlePosition( int throttle )
        {
            logger_.Log( "Throttle: " + throttle );
        }

        public void FormatAccel( float x, float y, float z )
        {
            logger_.Log( "X: " + x + " Y: " + y + " Z: " + z );
        }

        public void FormatPosition( int lat, int lon, int pdop )
        {
            logger_.Log( "Lat: " + lat + " Long: " + lon + " (" + pdop + ")" );
        }

        public void FormatSpeed( int speed )
        {
            logger_.Log( "Speed: " + speed );
        }

        public void FormatTime( int tow )
        {
            logger_.Log( "TOW: " + tow );
        }

        public void FormatAltitude( int alt, int vdop )
        {
            logger_.Log( "Altitude: " + alt + " (" + vdop + ")" );
        }

        public void FormatHeading( int heading, int accuracy )
        {
            logger_.Log( "Heading: " + heading );
        }
    }
}
