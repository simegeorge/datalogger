using System;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    //---   Formats data in accordance with RaceTechnolgy's .RUN format

    public class RunFormatter : Formatter
    {
        //---   Pre-allocate buffers for RaceTechnology messages
        private byte[] acceleration_    = new byte[ 10 ];
        private byte[] gps_pos_         = new byte[ 14 ];
        private byte[] speed_           = new byte[ 10 ];
        private byte[] time_            = new byte[ 6 ];
        private byte[] alt_             = new byte[ 10 ];
        private byte[] start_           = new byte[ 6 ];
        private byte[] run_             = new byte[ 11 ];
        private byte[] time_stamp_      = new byte[ 5 ];
        private byte[] preamble_        = new byte[ 8 ];
        private byte[] course_          = new byte[ 10 ];
        private byte[] date_            = new byte[ 10 ];
        private byte[] rpm_             = new byte[ 5 ];
        private byte[] analog_          = new byte[ 4 ];

        private Logger  logger_;

        public RunFormatter( Logger logger )
        {
            logger_ = logger;
        }

        //---   Checksum
        //---   http://www.race-technology.com/wiki/index.php/General/SerialDataFormat

        private byte Checksum( byte[] bytes, int count )
        {
            int checksum = 0;

            for ( int b=0; b < count; ++b )
                checksum += bytes[ b ];

            return (byte)(checksum & 0xff);
        }

        //---   Logger storage info
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/6LoggerStorageChannel

        public void FormatStart()
        {
            //---   Seems to want this mysterious preamble to work...

            preamble_[ 0 ] = 0x98;
            preamble_[ 1 ] = 0x1d;
            preamble_[ 2 ] = 0x00;
            preamble_[ 3 ] = 0x00;
            preamble_[ 4 ] = 0xc8;
            preamble_[ 5 ] = 0x00;
            preamble_[ 6 ] = 0x00;
            preamble_[ 7 ] = 0x00;

            logger_.Log( preamble_ );

            //---   Output a Log storage message using the following values empirically derived from an existing .run file : 
            //---       serial number = 2660, s/w version = 10, bootload version = 5

            start_[ 0 ] = 6;
            start_[ 1 ] = 0x0A;     //Serial number = Data2 + Data1 x 2^8
            start_[ 2 ] = 0x64;
            start_[ 3 ] = 10;       //Software version = Data3            
            start_[ 4 ] = 5;        //Bootload version = Data4

            start_[ 5 ] = Checksum( start_, 5 );

            logger_.Log( start_ );

            //---   Now output a run status message indicating what started the run

            run_[ 0 ] = 2;

            //If Data1 = 1,2, or 3:
            //Start method = Data1
            //Stop method = Data2
            //Pretrigger loop exit = Data3
            //Pretrigger time (minutes) = Data4
            //Postrigger time (minutes) = Data5
            //Autostart source = Data6
            //Autostop source = Data7
            //Lowest buffer value = Data8 x 2^8 + Data9

            run_[ 1 ] = 1;      // start reason : button press
            run_[ 2 ] = 0;      // stop reason (none here)
            run_[ 3 ] = 0;      // pretrigger loop exit
            run_[ 4 ] = 0;      // pretrigger time
            run_[ 5 ] = 0;      // posttrigger time
            run_[ 6 ] = 0;      // autostart source
            run_[ 7 ] = 0;      // autostop source
            run_[ 8 ] = 0xFF;   // lowest buffer value
            run_[ 9 ] = 0xFF;   

            run_[ 10 ] = Checksum( run_, 10 );

            logger_.Log( run_ );
        }

        //---   Time stamp
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/9TimeStamp

        public void FormatTimeStamp( int ts )
        {
            //---   The RUN format seems to require an incrementing count on each loop

            time_stamp_[ 0 ] = 9;
            time_stamp_[ 1 ] = (byte)((ts >> 16) & 0xff);
            time_stamp_[ 2 ] = (byte)((ts >> 8) & 0xff);
            time_stamp_[ 3 ] = (byte)(ts & 0xff);

            time_stamp_[ 4 ] = Checksum( time_stamp_, 4 );

            logger_.Log( time_stamp_ );
        }

        //---   Log accelerations
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/8Accelerations
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/92ZAccelerations

        public void FormatAccel( float x, float y, float z )
        {
            //---   We will actually output *two* messages here : one for XY and another for Z
            //---   The RaceTechnology format doesn't have one message for all 3 accelerations

            //---   Form XY acceleration message

            acceleration_[ 0 ] = 8;

            //Lateral = Data1 And 0x7F + Data2 / 0x100
            //If (Data1 And 0x80)=0 Then Lateral = -Lateral
            //Longitudinal = Data3 And &H7F + Data4 / 0x100
            //If (Data3 And &H80)=0 Then Longitudinal = -Longitudinal
            //Sign Convention
            //Longitudinal is positive for acceleration, negative for braking.
            //Lateral is positive for cornering around a RH bend, negative for cornering around a LH bend. 

            int xi = (int)(x * 256.0F);
            int yi = (int)(y * 256.0F);

            if ( xi < 0 )
            {
                xi = -xi;

                acceleration_[ 1 ] = (byte)((xi >> 8) & 0x7f);
                acceleration_[ 2 ] = (byte)(xi & 0xff);
            }
            else
            {
                acceleration_[ 1 ] = (byte)(((xi >> 8) & 0x7f) | 0x80);
                acceleration_[ 2 ] = (byte)(xi & 0xff);
            }

            if ( yi < 0 )
            {
                yi = -yi;

                acceleration_[ 3 ] = (byte)((yi >> 8) & 0x7f);
                acceleration_[ 4 ] = (byte)(yi & 0xff);
            }
            else
            {
                acceleration_[ 3 ] = (byte)(((yi >> 8) & 0x7f) | 0x80);
                acceleration_[ 4 ] = (byte)(yi & 0xff);
            }

            acceleration_[ 5 ] = Checksum( acceleration_, 5 );

            //---   Form Z acceleration message

            //Z accel = Data1 And 0x7F + Data2 / 0x100
            //If (Data1 And 0x80)=0 Then Z_Accel = -Z_Accel
            //Sign Convention
            //Lateral is positive for accelerating upwards (e.g. driving through a dip), negative for accelerating downwards (e.g. driving over a brow). 
            //This results in a stationary and level vertical acceleration of +1g (due to gravity). 

            acceleration_[ 6 ] = 92;

            int zi = (int)(z * 256.0F);

            if ( zi < 0 )
            {
                zi = -zi;

                acceleration_[ 7 ] = (byte)((zi >> 8) & 0x7f);
                acceleration_[ 8 ] = (byte)(zi & 0xff);
            }
            else
            {
                acceleration_[ 7 ] = (byte)(((zi >> 8) & 0x7f) | 0x80);
                acceleration_[ 8 ] = (byte)(zi & 0xff);
            }

            acceleration_[ 9 ] = (byte)((92 + acceleration_[ 7 ] + acceleration_[ 8 ]) & 0xff);  // quicker to calculate the checksum here manually

            logger_.Log( acceleration_ );
        }

        //---   Log GPS position
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/10GPSPositionalData

        public void FormatPosition( int lat, int lon, int pdop )
        {
            gps_pos_[ 0 ] = 10;

            //GpsLong = (Data1 And &H7F * 2^24 + Data2 * 2^16 + Data3 * 2^8 + Data4)
            //If (Data1 And &H80) Then GpsLong = GpsLong -(2 ^ 31)
            //GpsLong (degrees) = GpsLong * 0.0000001

            gps_pos_[ 1 ] = (byte)((lon >> 24) & 0xff);
            gps_pos_[ 2 ] = (byte)((lon >> 16) & 0xff);
            gps_pos_[ 3 ] = (byte)((lon >> 8) & 0xff);
            gps_pos_[ 4 ] = (byte)(lon & 0xff);

            //GpsLat = (Data5 And &H7F* 2^24 + Data6 * 2^16 + Data7 * 2^8 + Data8)
            //If (Data5 And &H80) Then GpsLat = GpsLat -(2 ^ 31)
            //GpsLat (degrees) = GpsLat * 0.0000001

            gps_pos_[ 5 ] = (byte)((lat >> 24) & 0xff);
            gps_pos_[ 6 ] = (byte)((lat >> 16) & 0xff);
            gps_pos_[ 7 ] = (byte)((lat >> 8) & 0xff);
            gps_pos_[ 8 ] = (byte)(lat & 0xff);

            //Positional Accuracy Estimate (mm) = (Data9 * 2^24 + Data10 * 2^16 + Data11 * 2^8 + Data12) 

            gps_pos_[ 9  ] = (byte)((pdop >> 24) & 0xff);
            gps_pos_[ 10 ] = (byte)((pdop >> 16) & 0xff);
            gps_pos_[ 11 ] = (byte)((pdop >> 8) & 0xff);
            gps_pos_[ 12 ] = (byte)(pdop & 0xff);

            gps_pos_[ 13 ] = Checksum( gps_pos_, 13 );

            logger_.Log( gps_pos_ );
        }

        //---   Log speed
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/11SpeedData

        public void FormatSpeed( int speed )
        {
            speed_[ 0 ] = 11;

            //Speed (m/s) = (Data1 * 2^24 + Data2 * 2^16 + Data3 * 2^8 + Data4)* 0.01

            speed_[ 1 ] = (byte)((speed >> 24) & 0xff);
            speed_[ 2 ] = (byte)((speed >> 16) & 0xff);
            speed_[ 3 ] = (byte)((speed >> 8) & 0xff);
            speed_[ 4 ] = (byte)(speed & 0xff);

            //Data source = Data5
            //0 = DL1, DL2, AX22 GPS module
            //1 = CAN data

            speed_[ 5 ] = 0;

            //SpeedAcc (m/s) =(Data6 * 2^16 + Data7 * 2^8 + Data8)* 0.01

            // TODO

            speed_[ 9 ] = Checksum( speed_, 9 );

            logger_.Log( speed_ );
        }

        //---   Log time
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/7GPSTimeStorageChannel

        public void FormatTime( int tow )
        {
            time_[ 0 ] = 7;

            time_[ 1 ] = (byte)((tow >> 24) & 0xff);
            time_[ 2 ] = (byte)((tow >> 16) & 0xff);
            time_[ 3 ] = (byte)((tow >> 8) & 0xff);
            time_[ 4 ] = (byte)(tow & 0xff);

            time_[ 5 ] = Checksum( time_, 5 );

            logger_.Log( time_ );
        }

        //---   Log altitude
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/57GPSAltitudeAndAltitudeAccuracyData

        public void FormatAltitude( int alt, int vdop )
        {
            alt_[ 0 ] = 57;

            //GpsAltitude (mm above sea level) = (Data1 x 2^24 + Data2 x 2^16 + Data3 x 2^8 + Data4)

            alt_[ 1 ] = (byte)((alt >> 24) & 0xff);
            alt_[ 2 ] = (byte)((alt >> 16) & 0xff);
            alt_[ 3 ] = (byte)((alt >> 8) & 0xff);
            alt_[ 4 ] = (byte)(alt & 0xff);

            //Altitude accuracy (mm) = (Data5 x 2^24 + Data6 x 2^16 + Data7 x 2^8 + Data8)

            alt_[ 5 ] = (byte)((vdop >> 24) & 0xff);
            alt_[ 6 ] = (byte)((vdop >> 16) & 0xff);
            alt_[ 7 ] = (byte)((vdop >> 8) & 0xff);
            alt_[ 8 ] = (byte)(vdop & 0xff);

            alt_[ 9 ] = Checksum( alt_, 9 );

            logger_.Log( alt_ );
        }

        //---   GPS course data
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/56GPSCourseData

        public void FormatHeading( int heading, int accuracy )
        {
            course_[ 0 ] = 56;

            //GPS Heading (degress x 10^-5) = (Data1 x 2^24 + Data2 x 2^16 + Data3 x 2^8 + Data4)

            course_[ 1 ] = (byte)((heading >> 24) & 0xff);
            course_[ 2 ] = (byte)((heading >> 16) & 0xff);
            course_[ 3 ] = (byte)((heading >> 8) & 0xff);
            course_[ 4 ] = (byte)(heading & 0xff);

            //GPS Heading accuracy (degrees x 10^-5) = (Data5 x 2^24 + Data6 x 2^16 + Data7 x 2^8 + Data8)

            course_[ 5 ] = (byte)((accuracy >> 24) & 0xff);
            course_[ 6 ] = (byte)((accuracy >> 16) & 0xff);
            course_[ 7 ] = (byte)((accuracy >> 8) & 0xff);
            course_[ 8 ] = (byte)(accuracy & 0xff);

            course_[ 9 ] = Checksum( course_, 9 );

            logger_.Log( course_ );
        }

        //---   Date
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/55DateStorageChannel

        public void FormatDate( int day, int month, int year, int hours, int minutes, int seconds )
        {
            date_[ 0 ] = 55;

            date_[ 1 ] = (byte)(seconds & 0xff);
            date_[ 2 ] = (byte)(minutes & 0xff);
            date_[ 3 ] = (byte)(hours & 0xff);
            date_[ 4 ] = (byte)(day & 0xff);
            date_[ 5 ] = (byte)(month & 0xff);
            date_[ 6 ] = (byte)((year >> 8) & 0xff);
            date_[ 7 ] = (byte)(year & 0xff);
            date_[ 8 ] = 0;

            date_[ 9 ] = Checksum( date_, 9 );

            logger_.Log( date_ );
        }

        //---   RPM
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/18RPMInput

        public void FormatRPM( int rpm )
        {
            rpm_[ 0 ] = 18;

            // TickPeriod = 1.66666666666667E-07 
            // Frequency = 1/((Data1 * 0x10000 + Data2 * 0x100 + Data3) * TickPeriod) 

            //---   Above formula doesn't translate into the values stored in the RUN format !
            //---   A closer (empirically derived) formula is 30 / (rpm * TickPeriod)
            //---   This is equivalent to 180,000,000 / rpm  (as 1/TickPeriod = 6,000,000)

            //---   (In fact, the default in the analysis software is for "2 pulses per rev"
            //---   This translates nicely into the 30 figure derived empirically above :)

            rpm = (int)(180000000f / (float)rpm);

            rpm_[ 1 ] = (byte)((rpm >> 16) & 0xff);
            rpm_[ 2 ] = (byte)((rpm >> 8) & 0xff);
            rpm_[ 3 ] = (byte)(rpm & 0xff);

            rpm_[ 4 ] = Checksum( rpm_, 4 );

            logger_.Log( rpm_ );
        }

        //---   Throttle position (use an analog channel to log this)
        //---   http://www.race-technology.com/wiki/index.php/DataAndConfigurationMessages/20-51AnalogueInputs

        public void FormatThrottlePosition( int throttle )
        {
            analog_[ 0 ] = 20;

            //Voltage [V]= (Data1 * 256 + Data2 ) / 1000 

            //---   Voltage range is 0 - 5V so map our throttle percentage to this range (100% = 5V)
            //---   This is then 5 * (throttle / 100) = 0.05 * throttle
            //---   We then scale this by 1000 so final calculation is 50 * throttle

            int v = 50 * throttle;

            analog_[ 1 ] = (byte)((v >> 8) & 0xff);
            analog_[ 2 ] = (byte)(v & 0xff);

            analog_[ 3 ] = Checksum( analog_, 3 );

            logger_.Log( analog_ );
        }
    }
}
