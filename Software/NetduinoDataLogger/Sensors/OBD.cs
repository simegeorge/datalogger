using System;
using System.Threading;
using System.IO.Ports;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;


namespace NetduinoDataLogger
{
    public class OBD : AsynchronousSensor
    {
        //---   OBD UART

        private const string    OBD_COM_PORT  = "COM2";
        private const int       OBD_BAUD_RATE = 38400;
        private const Parity    OBD_PARITY    = Parity.None;
        private const int       OBD_BITS      = 8;
        private const StopBits  OBD_STOP_BITS = StopBits.One;

        private SerialPort      obd_port_;

        //---   A buffer for the raw OBD data

        private byte[]          obd_data_buffer_ = new byte[ 256 ];

        //---   A buffer for the parsed OBD data

        private byte[]          obd_message_       = new byte[ 256 ];     // Big enough ?
        private int             obd_message_index_ = 0;

        //---   A buffer for the OBD commands

        private byte[]          obd_command_ = new byte[ 128 ];

        //---   The formatter to use

        private Formatter       formatter_;


        //---   Constructor

        public OBD( Formatter formatter )
        {
            formatter_ = formatter;

            //---   Performance monitoring
            PerformanceMonitor.Instance.AddPerformanceMonitor( PerformanceMonitor.Type.OBD );

            //---   Open the OBD port

            obd_port_ = new SerialPort( OBD_COM_PORT, OBD_BAUD_RATE, OBD_PARITY, OBD_BITS, OBD_STOP_BITS );

            obd_port_.Open();

            //---   Subscribe to the data output by the OBD
            obd_port_.DataReceived += new SerialDataReceivedEventHandler( obd_port_DataReceived );

            //---   Request the RPM
            RPMCommand();
        }


        //---   Asynchronous sensor implementation

        private int     it_ = 0;
        private bool    recording_ = false;

        public void Start()
        {
            it_ = 0;
            recording_ = true;
        }

        public void Stop()
        {
            recording_ = false;
        }

        //---   Calibration

        public bool Calibrate()
        {
            return true;
        }


        #region OBD commands

        private delegate void OBDCommand();

        private void InitCommand()
        {
            //Log( "InitCommand()\r\n" );

            obd_command_[ 0 ] = (byte)'0';
            obd_command_[ 1 ] = (byte)'1';
            obd_command_[ 2 ] = (byte)' ';
            obd_command_[ 3 ] = (byte)'0';
            obd_command_[ 4 ] = (byte)'0';
            obd_command_[ 5 ] = (byte)'\r';
            obd_command_[ 6 ] = (byte)'\n';

            obd_port_.Write( obd_command_, 0, 7 );
        }

        private void RPMCommand()
        {
            //Log( "RPMCommand()\r\n" );

            obd_command_[ 0 ] = (byte)'0';
            obd_command_[ 1 ] = (byte)'1';
            obd_command_[ 2 ] = (byte)' ';
            obd_command_[ 3 ] = (byte)'0';
            obd_command_[ 4 ] = (byte)'C';
            obd_command_[ 5 ] = (byte)'\r';
            obd_command_[ 6 ] = (byte)'\n';

            obd_port_.Write( obd_command_, 0, 7 );
        }

        private void ThrottlePositionCommand()
        {
            //Log( "ThrottlePositionCommand()\r\n" );

            obd_command_[ 0 ] = (byte)'0';
            obd_command_[ 1 ] = (byte)'1';
            obd_command_[ 2 ] = (byte)' ';
            obd_command_[ 3 ] = (byte)'1';
            obd_command_[ 4 ] = (byte)'1';
            obd_command_[ 5 ] = (byte)'\r';
            obd_command_[ 6 ] = (byte)'\n';

            obd_port_.Write( obd_command_, 0, 7 );
        }

        private void CoolantTempCommand()
        {
            //Log( "CoolantTempCommand()\r\n" );

            obd_command_[ 0 ] = (byte)'0';
            obd_command_[ 1 ] = (byte)'1';
            obd_command_[ 2 ] = (byte)' ';
            obd_command_[ 3 ] = (byte)'0';
            obd_command_[ 4 ] = (byte)'5';
            obd_command_[ 5 ] = (byte)'\r';
            obd_command_[ 6 ] = (byte)'\n';

            obd_port_.Write( obd_command_, 0, 7 );
        }

        private void NumFaultCodesCommand()
        {
            //Log( "NumFaultCodesCommand()\r\n" );

            obd_command_[ 0 ] = (byte)'0';
            obd_command_[ 1 ] = (byte)'1';
            obd_command_[ 2 ] = (byte)' ';
            obd_command_[ 3 ] = (byte)'0';
            obd_command_[ 4 ] = (byte)'1';
            obd_command_[ 5 ] = (byte)'\r';
            obd_command_[ 6 ] = (byte)'\n';

            obd_port_.Write( obd_command_, 0, 7 );
        }

        #endregion


        private void obd_port_DataReceived( object sender, SerialDataReceivedEventArgs e )
        {
            //---   Performance monitoring
            PerformanceMonitor.Instance.StartWork( PerformanceMonitor.Type.OBD );

            //---   Try to be clever and only allocate when we need to
            //---   (this should save needless garbage collection)

            int bytes_to_read = obd_port_.BytesToRead;

            if ( bytes_to_read > obd_data_buffer_.Length )
                obd_data_buffer_ = new byte[ bytes_to_read ];

            //---   Read the OBD data

            obd_port_.Read( obd_data_buffer_, 0, bytes_to_read );

            //---   Test
            //Log( "Raw data from ODB: <" );
            //Log( obd_data_buffer_, bytes_to_read );
            //Log( ">\r\n" );
            //---   End

            //---   Parse the data

            for ( int i = 0; i < bytes_to_read; ++i )
            {
                byte b = obd_data_buffer_[ i ];

                //---   Ignore whitespace (and suprious nulls as per ELM327 datasheet, page 7)
                if ( b == 0x00 || b == 0x20 || b == 0x0A || b == 0x0D )
                    continue;

                if ( b == '>' )
                {
                    //---   Message received : will be in odb_data_buffer_[ 0 ... obd_message_index_ ]
                    ParseMessage();

                    //---   Reset the buffer for the next message
                    obd_message_index_ = 0;

                    //---   Request the next parameter, alternating between the ones we want
                    ++it_;
                    if ( it_ % 2 == 0 )
                        RPMCommand();
                    else
                        ThrottlePositionCommand();
                }
                else
                {
                    obd_message_[ obd_message_index_++ ] = b;
                }
            }

            //---   Performance monitoring
            PerformanceMonitor.Instance.StopWork( PerformanceMonitor.Type.OBD );
        }


        private void ParseMessage()
        {
            try
            {
                //Log( "OBD message : <" );
                //string msg = new string( System.Text.Encoding.UTF8.GetChars( obd_message_ ) );
                //Log( msg.Substring( 0, obd_message_index_ ) );
                //Log( ">\r\n" );

                int i = 0;

                while ( i < obd_message_index_ )
                {
                    if ( obd_message_[ i ] == '4' && obd_message_[ i + 1 ] == '1' )
                    {
                        //---   OBD command response of the form :
                        //---       410C128D (channel 12 = RPM, 128D = rpm*4)

                        //---   See what the PID is
                        //---   http://www.blafusel.de/obd/obd2lcd_4en.html

                        int pid = CharPairToInt( obd_message_[ i + 2 ], obd_message_[ i + 3 ] );

                        switch ( pid )
                        {
                            case 0x05:
                                {
                                    //---   Coolant temp (offset by 40 degrees C)
                                    int temp = CharPairToInt( obd_message_[ i + 4 ], obd_message_[ i + 5 ] ) - 40;
                                    //Log( "Coolant temp : " + temp + "\r\n" );

                                    i += 6;
                                }
                                break;

                            case 0x0C:
                                {
                                    //---   RPM (measured in 1/4 RPMs)
                                    int rpm = CharQuadToInt( obd_message_[ i + 4 ], obd_message_[ i + 5 ], obd_message_[ i + 6 ], obd_message_[ i + 7 ] ) / 4;
                                    //Log( "RPM : " + rpm + "\r\n" );

                                    if ( recording_ )
                                        formatter_.FormatRPM( rpm );

                                    i += 8;
                                }
                                break;

                            case 0x11:
                                {
                                    //---   Throttle (in %)
                                    int throttle = CharPairToInt( obd_message_[ i + 4 ], obd_message_[ i + 5 ] ) * 100 / 255;
                                    //Log( "Throttle : " + throttle + "% \r\n" );

                                    if ( recording_ )
                                        formatter_.FormatThrottlePosition( throttle );

                                    i += 6;
                                }
                                break;

                            default:
                                {
                                    //---   Unrecognised PID
                                    i += 4;
                                }
                                break;
                        }
                    }
                    else
                    {
                        ++i;
                    }
                }
            }
            catch
            {
                //---   Malformed/unexpected message : ignore it
            }
        }


        #region ASCII character to byte conversions

        //---   Convert ASCII hex character (0-F) to an integer

        private int CharToInt( byte b )
        {
            //---   0 = 0x30 ... 9 = 0x39, A = 0x41 ... F = 0x46

            return (b > 0x39) ? (10 + b - 0x41) : (b - 0x30);
        }

        private int CharPairToInt( byte b1, byte b2 )
        {
            return (CharToInt( b1 ) << 4) | CharToInt( b2 );
        }

        private int CharQuadToInt( byte b1, byte b2, byte b3, byte b4 )
        {
            return (CharToInt( b1 ) << 12) | (CharToInt( b2 ) << 8) | (CharToInt( b3 ) << 4) | CharToInt( b4 );
        }

        #endregion

    }
}
