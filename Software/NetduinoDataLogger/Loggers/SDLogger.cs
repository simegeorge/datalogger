using System;
using System.Threading;
using System.IO;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using SecretLabs.NETMF.IO;

namespace NetduinoDataLogger
{
    public class SDLogger : Logger
    {
        private const string    SD_ROOT        = "SD1";
        private const string    SD_ROOT_DIR    = @"\SD1";
        private DateTime        GPS_WEEK_START = new DateTime( 1980, 1, 6, 0, 0, 0 );   // GPS week starts on 6/1/1980 00:00:00

        private FileStream      file_;
        private bool            logging_ = false;


        public SDLogger()
        {
            Mount();
        }


        //---   SD card functions

        protected bool  Mount()
        {
            try
            {
                StorageDevice.MountSD( SD_ROOT, SPI_Devices.SPI1, Pins.GPIO_PIN_D10 );
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected bool Write( byte[] buffer )
        {
            lock ( this )
            {
                try
                {
                    file_.Write( buffer, 0, buffer.Length );

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /*
        protected string GetLogFilename( int week, int tow )
        {
            //---   Get the current time (in GMT) from the GPS time
            //---   http://java.itags.org/java-essentials/13065/
            DateTime    time = GPS_WEEK_START.AddSeconds( week * (7 * 24 * 3600) + (tow * 0.01) );

            //---   Run filename depends on time from GPS
            string  filename = "log_" + time.ToString( "ddMMyy_HHmmss" ) + ".run";

            //---   See if the file exists on the SD card (which it might if there is no GPS fix yet...)
            string[] files = System.IO.Directory.GetFiles( SD_ROOT_DIR );
            bool     found = false;

            foreach ( string file in files )
            {
                if ( file == filename )
                {
                    found = true;
                    break;
                }
            }

            if ( found )
            {
            }

            return Path.Combine( SD_ROOT, filename );
        }
        */

        protected string GetLogFilename( string datetime )
        {
            //---   Run filename depends on time from GPS
            string  filename = "log_" + datetime + ".run";

            //---   See if the file exists on the SD card (which it might if there is no GPS fix yet...)
            string[] files = System.IO.Directory.GetFiles( SD_ROOT_DIR );
            bool     found = false;

            foreach ( string file in files )
            {
                if ( file == filename )
                {
                    found = true;
                    break;
                }
            }

            if ( found )
            {
            }

            return Path.Combine( SD_ROOT, filename );
        }

        //---   Logger implementation

        public void Start( string datetime )
        {
            try
            {
                file_ = new FileStream( GetLogFilename( datetime ), FileMode.Create, FileAccess.Write, FileShare.None, 512 );
                logging_ = true;
            }
            catch
            {
                Debug.Print( "Failed to open SD card for writing" );
                file_ = null;
                logging_ = false;
            }
        }

        public void Stop()
        {
            lock ( this )
            {
                logging_ = false;

                try
                {
                    file_.Flush();
                    file_.Close();
                }
                catch
                {
                    file_ = null;
                }
            }
        }

        public void Log( byte[] bytes )
        {
            if ( logging_ )
                Write( bytes );
        }

        public void Log( string s )
        {
            if ( logging_ )
                Write( System.Text.Encoding.UTF8.GetBytes( s ) );
        }

    }
}
