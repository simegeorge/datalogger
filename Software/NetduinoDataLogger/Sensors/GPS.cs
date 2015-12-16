using System;
using System.Threading;
using System.IO.Ports;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using ElzeKool;

namespace NetduinoDataLogger
{
    public class GPS : AsynchronousSensor
    {
        //---   GPS UART

        private const string    GPS_COM_PORT    = "COM1";
        private const int       GPS_BAUD_RATE   = 57600;
        private const Parity    GPS_PARITY      = Parity.None;
        private const int       GPS_BITS        = 8;
        private const StopBits  GPS_STOP_BITS   = StopBits.One;

        private SerialPort      gps_port_;

        //---   Conversion constants

        private const double    KNOTS_TO_RUN_FORMAT_SPEED                 = 0.51444 * 100.0;                // knots to m/s * 100
        private const double    GPS_DEGREES_TO_RUN_FORMAT_DEGREES         = 10e4;                           // GPS 4807.038 = 48 deg 07.038 min
        private const double    GPS_HEADING_DEGREES_TO_RUN_FORMAT_DEGREES = 10e5;
        private const double    ALT_TO_RUN_FORMAT_ALT                     = 100.0;
        private const double    MS_TO_RUN_FORMAT_SPEED                    = 100.0;                          // m/s * 100
        private const double    GPS_HEADING_RADIANS_TO_RUN_FORMAT_DEGREES = (180.0 / exMath.PI) * 10e4;

        //---   A buffer for the raw GPS data

        private byte[]          gps_data_buffer_ = new byte[ 256 ];

        //---   Current GPS time

        private DateTime        gps_epoch_ = new DateTime( 1980, 1, 6 );    // start of GPS week time

        private int             gps_week_ = 0;
        private int             gps_tow_  = 0;

        //---   The formatter to use

        private Formatter       formatter_;

        //---   Constructor

        public GPS( Formatter formatter )
        {
            formatter_ = formatter;

            //---   Open the GPS port

            gps_port_ = new SerialPort( GPS_COM_PORT, GPS_BAUD_RATE, GPS_PARITY, GPS_BITS, GPS_STOP_BITS );

            gps_port_.Open();

            //---   Set up the GPS

            // TODO

            //---   Performance monitoring
            PerformanceMonitor.Instance.AddPerformanceMonitor( PerformanceMonitor.Type.GPS );

            //---   Subscribe to the data output by the GPS
            gps_port_.DataReceived += new SerialDataReceivedEventHandler( gps_port_DataReceived );
        }

        //---   No calibration required

        public bool Calibrate()
        {
            return true;
        }

        //---   Get the current GPS date and time

        public string DateTime()
        {
            DateTime    current_week = gps_epoch_.AddDays( gps_week_ * 7 );

            DateTime    now = current_week.AddSeconds( gps_tow_ / 100 );

            return now.ToString( "ddMMyyyy_HHmmss" );
        }

        //---   Start/stop

        private bool    recording_ = false;

        public void Start()
        {
            recording_ = true;
        }

        public void Stop()
        {
            recording_ = false;
        }

        //---   GPS data received event handler

        private void gps_port_DataReceived( object sender, SerialDataReceivedEventArgs e )
        {
            //---   Performance monitoring
            PerformanceMonitor.Instance.StartWork( PerformanceMonitor.Type.GPS );

            //---   Try to be clever and only allocate when we need to
            //---   (this should save needless garbage collection)

            int bytes_to_read = gps_port_.BytesToRead;

            if ( bytes_to_read > gps_data_buffer_.Length )
                gps_data_buffer_ = new byte[ bytes_to_read ];

            //---   Read the GPS data

            gps_port_.Read( gps_data_buffer_, 0, bytes_to_read );

            //---   Parse the data

            ParseBinaryMessage( gps_data_buffer_, bytes_to_read );
            //ParseNMEAMessage( gps_data_buffer_, bytes_to_read );

            //---   Performance monitoring
            PerformanceMonitor.Instance.StopWork( PerformanceMonitor.Type.GPS );
        }

        #region GPS parser state

        //---   Buffer to store the actual GPS message received as we parse

        private byte[]   gps_message_       = new byte[ 256 ];      // TODO: is this big enough ?
        private int      gps_message_index_ = 0;

        //---   GPS message parser state

        private enum ParserState
        {
            LOOKING,
            MAYBE_FOUND_START,
            MAYBE_FOUND_END,
            PROCESSING_MESSAGE
        }

        //---   Initially we're looking for the start of a GPS message

        private static ParserState  parser_state_ = ParserState.LOOKING;

        #endregion

        #region GPS NMEA message parser

        private void ParseNMEAMessage( byte[] buffer, int length )
        {
            for ( int i = 0; i < length; ++i )
            {
                byte b = buffer[ i ];

                switch ( parser_state_ )
                {
                    case ParserState.LOOKING:
                        if ( b == '$' )
                        {
                            parser_state_ = ParserState.PROCESSING_MESSAGE;
                            gps_message_[ gps_message_index_++ ] = b;
                        }
                        break;

                    case ParserState.PROCESSING_MESSAGE:
                        if ( b == '*' )
                        {
                            gps_message_[ gps_message_index_++ ] = b;
                            gps_message_[ gps_message_index_++ ] = 0;
                            HandleNMEAMessage( new string( System.Text.Encoding.UTF8.GetChars( gps_message_ ) ) );
                            parser_state_ = ParserState.LOOKING;
                            gps_message_index_ = 0;
                        }
                        else
                        {
                            gps_message_[ gps_message_index_++ ] = b;
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        #endregion

        #region GPS binary message parser

        //---   GPS message parser

        private void ParseBinaryMessage( byte[] buffer, int length )
        {
            for ( int i = 0; i < length; ++i )
            {
                byte b = buffer[ i ];

                switch ( parser_state_ )
                {
                    //---   These two states look for the start of a GPS message which is always 0xA0 0xA1

                    case ParserState.LOOKING:
                        if ( b == 0xA0 )
                            parser_state_ = ParserState.MAYBE_FOUND_START;
                        break;

                    case ParserState.MAYBE_FOUND_START:
                        if ( b == 0xA1 )
                            //---   Start of a GPS message !
                            parser_state_ = ParserState.PROCESSING_MESSAGE;
                        else
                            //---   Potential start wasn't actually a start
                            parser_state_ = ParserState.LOOKING;
                        break;

                    //---   These two states look for the end of a GPS message which is always 0x0D 0x0A

                    case ParserState.PROCESSING_MESSAGE:
                        gps_message_[ gps_message_index_++ ] = b;
                        if ( b == 0x0D )
                            parser_state_ = ParserState.MAYBE_FOUND_END;
                        break;

                    case ParserState.MAYBE_FOUND_END:
                        gps_message_[ gps_message_index_++ ] = b;
                        if ( b == 0x0A )
                        {
                            //---   End of a GPS message : handle it
                            HandleBinaryGPSMessage( gps_message_ );
                            //---   Reset state to look for next message
                            parser_state_ = ParserState.LOOKING;
                            gps_message_index_ = 0;
                        }
                        else
                        {
                            //---   Potential end wasn't actually a end
                            parser_state_ = ParserState.PROCESSING_MESSAGE;
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        #endregion

        #region GPS binary message handlers

        private void HandleBinaryGPSMessage( byte[] buffer )
        {
            //---   Extract the message info
            //---   (Note that we won't get the message preamble 0xA0 0xA1 as this has been stripped off by the parser)

            int     message_length = ((int)buffer[ 0 ] << 8) | (int)buffer[ 1 ];
            byte    message_type = buffer[ 2 ];

            //Debug.Print( BytesToString( buffer, message_length+3 ) );

            switch ( message_type )
            {
                case 0x80:
                    HandleSoftwareVersion( buffer );
                    break;

                case 0x81:
                    HandleSoftwareCRC( buffer );
                    break;

                case 0x83:
                    HandleACK( buffer );
                    break;

                case 0x84:
                    HandleNACK( buffer );
                    break;

                case 0x86:
                    HandlePositionUpdateRate( buffer );
                    break;

                case 0xA8:
                    HandleNavigationDataMessage( buffer );
                    break;

                case 0xAE:
                    HandleGPSDatum( buffer );
                    break;

                case 0xB3:
                    HandleWAASStatus( buffer );
                    break;

                case 0xB5:
                    HandleNavigationMode( buffer );
                    break;

                default:
                    //Debug.Print( BytesToString( buffer ) );
                    break;
            }
        }

        private void HandleSoftwareVersion( byte[] buffer )
        {
            Debug.Print( "Software type : " + buffer[ 3 ] +
                         " Kernel version : " + buffer[ 5 ] + "." + buffer[ 6 ] + "." + buffer[ 7 ] +
                         " ODM version : " + buffer[ 9 ] + "." + buffer[ 10 ] + "." + buffer[ 11 ] +
                         " Revision : " + buffer[ 13 ] + "." + buffer[ 14 ] + "." + buffer[ 15 ] );
        }

        private void HandleSoftwareCRC( byte[] buffer )
        {
            int   crc = ((int)buffer[ 4 ] << 8) | (int)buffer[ 5 ];

            Debug.Print( "Software type : " + buffer[ 3 ] + " Software CRC : " + crc );
        }

        private void HandleACK( byte[] buffer )
        {
            Debug.Print( "ACK for command " + buffer[ 3 ] );
        }

        private void HandleNACK( byte[] buffer )
        {
            Debug.Print( "NACK for command " + buffer[ 3 ] );
        }

        private void HandlePositionUpdateRate( byte[] buffer )
        {
            Debug.Print( "Position update rate : " + buffer[ 3 ] );
        }

        private void HandleNavigationDataMessage( byte[] buffer )
        {
            //---   Extract the GPS data from the binary message

            byte    fix_mode = buffer[ 3 ];
            byte    svs_in_fix = buffer[ 4 ];
            int     gps_week = ((int)buffer[ 5 ] << 8) | (int)buffer[ 6 ];
            int     tow = ((int)buffer[ 7 ] << 24) | ((int)buffer[ 8 ] << 16) | ((int)buffer[ 9 ] << 8) | (int)buffer[ 10 ];
            int     lat = ((int)buffer[ 11 ] << 24) | ((int)buffer[ 12 ] << 16) | ((int)buffer[ 13 ] << 8) | (int)buffer[ 14 ];
            int     lon = ((int)buffer[ 15 ] << 24) | ((int)buffer[ 16 ] << 16) | ((int)buffer[ 17 ] << 8) | (int)buffer[ 18 ];
            int     elipsoid_alt = ((int)buffer[ 19 ] << 24) | ((int)buffer[ 20 ] << 16) | ((int)buffer[ 21 ] << 8) | (int)buffer[ 22 ];
            int     sea_level_alt = ((int)buffer[ 23 ] << 24) | ((int)buffer[ 24 ] << 16) | ((int)buffer[ 25 ] << 8) | (int)buffer[ 26 ];
            int     gdop = ((int)buffer[ 27 ] << 8) | (int)buffer[ 28 ];
            int     pdop = ((int)buffer[ 29 ] << 8) | (int)buffer[ 30 ];
            int     hdop = ((int)buffer[ 31 ] << 8) | (int)buffer[ 32 ];
            int     vdop = ((int)buffer[ 33 ] << 8) | (int)buffer[ 34 ];
            int     tdop = ((int)buffer[ 35 ] << 8) | (int)buffer[ 36 ];
            int     ecef_x = ((int)buffer[ 37 ] << 24) | ((int)buffer[ 38 ] << 16) | ((int)buffer[ 39 ] << 8) | (int)buffer[ 40 ];
            int     ecef_y = ((int)buffer[ 41 ] << 24) | ((int)buffer[ 42 ] << 16) | ((int)buffer[ 43 ] << 8) | (int)buffer[ 44 ];
            int     ecef_z = ((int)buffer[ 45 ] << 24) | ((int)buffer[ 46 ] << 16) | ((int)buffer[ 47 ] << 8) | (int)buffer[ 48 ];
            int     ecef_vx = ((int)buffer[ 49 ] << 24) | ((int)buffer[ 50 ] << 16) | ((int)buffer[ 51 ] << 8) | (int)buffer[ 52 ];
            int     ecef_vy = ((int)buffer[ 53 ] << 24) | ((int)buffer[ 54 ] << 16) | ((int)buffer[ 55 ] << 8) | (int)buffer[ 56 ];
            int     ecef_vz = ((int)buffer[ 57 ] << 24) | ((int)buffer[ 58 ] << 16) | ((int)buffer[ 59 ] << 8) | (int)buffer[ 60 ];

            //---   Calculate speed and heading from ECEF velocity

            double  latd = ((double)lat * 1e-7) / 180 * exMath.PI;
            double  lond = ((double)lon * 1e-7) / 180 * exMath.PI;

            double x = ecef_vx * 0.01;
            double y = ecef_vy * 0.01;
            double z = ecef_vz * 0.01;

            double sinlat = exMath.Sin( latd );
            double coslat = exMath.Cos( latd );
            double sinlon = exMath.Sin( lond );
            double coslon = exMath.Cos( lond );

            double sinloncoslat = sinlon * coslat;

            double vn = -x * sinlat * coslon - y * sinlat * sinlon + z * sinloncoslat;
            double ve = -x * sinlon + y * coslon;
            double vd = -x * coslat * coslon - y * sinloncoslat - z * sinlat;

            double speed = exMath.Sqrt( vn * vn + ve * ve );

            double heading = exMath.Atan2( vn, ve );

            if ( heading < 0 ) 
                heading += exMath.TWOPI;

            gps_week_ = gps_week;
            gps_tow_  = tow;

            if ( recording_ )
            {
                formatter_.FormatTime( tow );
                formatter_.FormatAltitude( sea_level_alt, vdop );
                formatter_.FormatPosition( lat, lon, pdop );
                formatter_.FormatSpeed( (int)(speed * MS_TO_RUN_FORMAT_SPEED) );
                formatter_.FormatHeading( (int)(heading * GPS_HEADING_RADIANS_TO_RUN_FORMAT_DEGREES), 0 );

                //---   Output date
                //---   TODO : does this make a difference ?

                DateTime    current_week = gps_epoch_.AddDays( gps_week_ * 7 );
                DateTime    now = current_week.AddSeconds( gps_tow_ / 100 );

                formatter_.FormatDate( now.Day, now.Month, now.Year, now.Hour, now.Minute, now.Second );
            }

            //Debug.Print( "Navigation data message : Fix = " + gps_data.fix_mode + ", SVs = " + gps_data.svs_in_fix + ", GPS week = " + gps_data.gps_week + ", TOW = " + gps_data.tow );
            //Debug.Print( "  Lat = " + gps_data.lat + ", Long = " + gps_data.lon + ", ElipAlt = " + gps_data.elipsoid_alt + ", SeaAlt = " + gps_data.sea_level_alt );
            //Debug.Print( "  ECEF_x = " + gps_data.ecef_x + ", ECEF_y = " + gps_data.ecef_y + ", ECEF_z = " + gps_data.ecef_z );
            //Debug.Print( "  ECEF_vx = " + gps_data.ecef_vx + ", ECEF_vy = " + gps_data.ecef_vy + ", ECEF_vz = " + gps_data.ecef_vz );
            //Debug.Print( "" );
        }

        // Double buffered implementation
        //private void HandleNavigationDataMessage( byte[] buffer )
        //{
        //    //---   Get a buffer to write the new data to
        //    GPSData gps_data = gps_data_[ gps_data_index_ ];

        //    //---   Extract the GPS data from the binary message
        //    gps_data.fix_mode = buffer[ 3 ];
        //    gps_data.svs_in_fix = buffer[ 4 ];
        //    gps_data.gps_week = ((int)buffer[ 5 ] << 8) | (int)buffer[ 6 ];
        //    gps_data.tow = ((int)buffer[ 7 ] << 24) | ((int)buffer[ 8 ] << 16) | ((int)buffer[ 9 ] << 8) | (int)buffer[ 10 ];
        //    gps_data.lat = ((int)buffer[ 11 ] << 24) | ((int)buffer[ 12 ] << 16) | ((int)buffer[ 13 ] << 8) | (int)buffer[ 14 ];
        //    gps_data.lon = ((int)buffer[ 15 ] << 24) | ((int)buffer[ 16 ] << 16) | ((int)buffer[ 17 ] << 8) | (int)buffer[ 18 ];
        //    gps_data.elipsoid_alt = ((int)buffer[ 19 ] << 24) | ((int)buffer[ 20 ] << 16) | ((int)buffer[ 21 ] << 8) | (int)buffer[ 22 ];
        //    gps_data.sea_level_alt = ((int)buffer[ 23 ] << 24) | ((int)buffer[ 24 ] << 16) | ((int)buffer[ 25 ] << 8) | (int)buffer[ 26 ];
        //    gps_data.gdop = ((int)buffer[ 27 ] << 8) | (int)buffer[ 28 ];
        //    gps_data.pdop = ((int)buffer[ 29 ] << 8) | (int)buffer[ 30 ];
        //    gps_data.hdop = ((int)buffer[ 31 ] << 8) | (int)buffer[ 32 ];
        //    gps_data.vdop = ((int)buffer[ 33 ] << 8) | (int)buffer[ 34 ];
        //    gps_data.tdop = ((int)buffer[ 35 ] << 8) | (int)buffer[ 36 ];
        //    gps_data.ecef_x = ((int)buffer[ 37 ] << 24) | ((int)buffer[ 38 ] << 16) | ((int)buffer[ 39 ] << 8) | (int)buffer[ 40 ];
        //    gps_data.ecef_y = ((int)buffer[ 41 ] << 24) | ((int)buffer[ 42 ] << 16) | ((int)buffer[ 43 ] << 8) | (int)buffer[ 44 ];
        //    gps_data.ecef_z = ((int)buffer[ 45 ] << 24) | ((int)buffer[ 46 ] << 16) | ((int)buffer[ 47 ] << 8) | (int)buffer[ 48 ];
        //    gps_data.ecef_vx = ((int)buffer[ 49 ] << 24) | ((int)buffer[ 50 ] << 16) | ((int)buffer[ 51 ] << 8) | (int)buffer[ 52 ];
        //    gps_data.ecef_vy = ((int)buffer[ 53 ] << 24) | ((int)buffer[ 54 ] << 16) | ((int)buffer[ 55 ] << 8) | (int)buffer[ 56 ];
        //    gps_data.ecef_vz = ((int)buffer[ 57 ] << 24) | ((int)buffer[ 58 ] << 16) | ((int)buffer[ 59 ] << 8) | (int)buffer[ 60 ];

        //    //---   Swap the GPS data double buffer
        //    gps_data_index_ = (gps_data_index_ + 1) % NUM_GPS_DATA;

        //    logger_.LogPosition( gps_data.lat, gps_data.lon );

        //    //Debug.Print( "Navigation data message : Fix = " + gps_data.fix_mode + ", SVs = " + gps_data.svs_in_fix + ", GPS week = " + gps_data.gps_week + ", TOW = " + gps_data.tow );
        //    //Debug.Print( "  Lat = " + gps_data.lat + ", Long = " + gps_data.lon + ", ElipAlt = " + gps_data.elipsoid_alt + ", SeaAlt = " + gps_data.sea_level_alt );
        //    //Debug.Print( "  ECEF_x = " + gps_data.ecef_x + ", ECEF_y = " + gps_data.ecef_y + ", ECEF_z = " + gps_data.ecef_z );
        //    //Debug.Print( "  ECEF_vx = " + gps_data.ecef_vx + ", ECEF_vy = " + gps_data.ecef_vy + ", ECEF_vz = " + gps_data.ecef_vz );
        //    //Debug.Print( "" );
        //}

        private void HandleGPSDatum( byte[] buffer )
        {
            int datum = ((int)buffer[ 3 ] << 8) | (int)buffer[ 4 ];

            Debug.Print( "GPS Datum : " + datum );
        }

        private void HandleWAASStatus( byte[] buffer )
        {
            Debug.Print( "WAAS status : " + buffer[ 3 ] );
        }

        private void HandleNavigationMode( byte[] buffer )
        {
            Debug.Print( "Navigation mode : " + buffer[ 3 ] );
        }

        #endregion

        #region GPS NMEA message handlers

        private void HandleNMEAMessage( string nmea )
        {
            //Debug.Print( nmea );

            String statementType = nmea.Substring( 0, 6 );

            switch ( statementType )
            {
                case "$GPGGA":
                    HandleGPGGA( nmea );
                    break;
                case "$GPRMC":
                    HandleGPRMC( nmea );
                    break;
                default:
                    //Debug.Print( "Unrecognized NMEA String: " + nmea );
                    break;
            }
        }

        private void HandleGPGGA( string nmea )
        {
            /*
             $GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47

            Where:
                 GGA          Global Positioning System Fix Data
                 123519       Fix taken at 12:35:19 UTC
                 4807.038,N   Latitude 48 deg 07.038' N
                 01131.000,E  Longitude 11 deg 31.000' E
                 1            Fix quality: 0 = invalid
                                           1 = GPS fix (SPS)
                                           2 = DGPS fix
                                           3 = PPS fix
			                               4 = Real Time Kinematic
			                               5 = Float RTK
                                           6 = estimated (dead reckoning) (2.3 feature)
			                               7 = Manual input mode
			                               8 = Simulation mode
                 08           Number of satellites being tracked
                 0.9          Horizontal dilution of position
                 545.4,M      Altitude, Meters, above mean sea level
                 46.9,M       Height of geoid (mean sea level) above WGS84 ellipsoid
                 (empty field) time in seconds since last DGPS update
                 (empty field) DGPS station ID number
                 *47          the checksum data, always begins with *
            */

            try
            {
                string[]  parts = nmea.Split( ',' );

                //---   Reject invalid statements quickly
                if ( parts[ 2 ].Length == 0 )
                    return;

                //string  fix_time = parts[ 1 ];
                double    lat      = Double.Parse( parts[ 2 ] );
                string    lath     = parts[ 3 ];
                double    lon      = Double.Parse( parts[ 4 ] );
                string    lonh     = parts[ 5 ];
                //string  fix_qual = parts[ 6 ];
                //string  num_sats = parts[ 7 ];
                //double  hdop     = Double.Parse( parts[ 8 ] );
                double    alt      = Double.Parse( parts[ 9 ] );

                //---   Convert from ddmm.mmm to decimal degrees

                double lon_ddmmss = (lon / 100);
                int    lon_degrees = (int)lon_ddmmss;
                double lon_minutesseconds = ((lon_ddmmss - lon_degrees) * 100) / 60.0;

                double lat_ddmmss = (lat / 100);
                int    lat_degrees = (int)lat_ddmmss;
                double lat_minutesseconds = ((lat_ddmmss - lat_degrees) * 100) / 60.0;
                
                lon = lon_degrees + lon_minutesseconds;
                lat = lat_degrees + lat_minutesseconds;

                //---   Swap sign if appropriate

                if ( lath[ 0 ] == 'S' ) lat = -lat;
                if ( lonh[ 0 ] == 'W' ) lon = -lon;

                formatter_.FormatAltitude( (int)(alt * ALT_TO_RUN_FORMAT_ALT), 0 );
                formatter_.FormatPosition( (int)(lat * GPS_DEGREES_TO_RUN_FORMAT_DEGREES), (int)(lon * GPS_DEGREES_TO_RUN_FORMAT_DEGREES), 0 );
            }
            catch
            {
                //---   Error when parsing the NMEA : ignore it
            }
        }

        private void HandleGPRMC( string nmea )
        {
            /*
            $GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A

            Where:
                 RMC          Recommended Minimum sentence C
                 123519       Fix taken at 12:35:19 UTC
                 A            Status A=active or V=Void.
                 4807.038,N   Latitude 48 deg 07.038' N
                 01131.000,E  Longitude 11 deg 31.000' E
                 022.4        Speed over the ground in knots
                 084.4        Track angle in degrees True
                 230394       Date - 23rd of March 1994
                 003.1,W      Magnetic Variation
                 *6A          The checksum data, always begins with *
             */

            try
            {
                string[]  parts = nmea.Split( ',' );

                //---   Reject invalid statements quickly
                if ( parts[ 1 ].Length == 0 )
                    return;

                string    fix_time = parts[ 1 ];            // 202218.457
                //string  status   = parts[ 2 ];
                //double  lat      = Double.Parse( parts[ 3 ] );
                //string  lath     = parts[ 4 ];
                //double  lon      = Double.Parse( parts[ 5 ] );
                //string  lonh     = parts[ 6 ];
                double    speed    = Double.Parse( parts[ 7 ] );
                double    track    = Double.Parse( parts[ 8 ] );
                string    date     = parts[ 9 ];

                int hours   = Int32.Parse( fix_time.Substring( 0, 2 ) );
                int minutes = Int32.Parse( fix_time.Substring( 2, 2 ) );
                int seconds = Int32.Parse( fix_time.Substring( 4, 2 ) );

                int day   = Int32.Parse( date.Substring( 0, 2 ) );
                int month = Int32.Parse( date.Substring( 2, 2 ) );
                int year  = Int32.Parse( date.Substring( 4, 2 ) ) + 2000;   // Assume no dates before the millenium !

                formatter_.FormatDate( day, month, year, hours, minutes, seconds );
                formatter_.FormatSpeed( (int)(speed * KNOTS_TO_RUN_FORMAT_SPEED) );
                formatter_.FormatHeading( (int)(track * GPS_HEADING_DEGREES_TO_RUN_FORMAT_DEGREES), 0 );

                //gps_date_ = date;
                //gps_time_ = fix_time.Substring( 0, 6 );     // strip off the milliseconds part

                //---   Get GPS time of week
                /*
                DateTime    fix = new DateTime( year, month, day, hours, minutes, seconds );
                int         days_offset = 0;

                switch ( fix.DayOfWeek )
                {
                    case DayOfWeek.Saturday:
                        days_offset = -6;
                        break;
                    case DayOfWeek.Sunday:
                        days_offset = 0;
                        break;
                    case DayOfWeek.Monday:
                        days_offset = -1;
                        break;
                    case DayOfWeek.Tuesday:
                        days_offset = -2;
                        break;
                    case DayOfWeek.Wednesday:
                        days_offset = -3;
                        break;
                    case DayOfWeek.Thursday:
                        days_offset = -4;
                        break;
                    case DayOfWeek.Friday:
                        days_offset = -5;
                        break;
                }

                DateTime    start_of_week = fix.AddDays( days_offset );

                start_of_week = new DateTime( start_of_week.Year, start_of_week.Month, start_of_week.Day );

                double tow = (fix - start_of_week).TotalMilliseconds;
                */
            }
            catch
            {
                //---   Error when parsing the NMEA : ignore it
            }
        }

        #endregion
    }
}
