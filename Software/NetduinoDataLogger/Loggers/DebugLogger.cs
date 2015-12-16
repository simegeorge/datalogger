using System;
using System.Text;
using Microsoft.SPOT;

namespace NetduinoDataLogger
{
    public class DebugLogger : Logger
    {
        bool        logging_ = false;

        public DebugLogger()
        {
        }

        public void Start( string datetime )
        {
            logging_ = true;
            Debug.Print( "Logging started" );
        }

        public void Stop()
        {
            logging_ = false;
            Debug.Print( "Logging stopped" );
        }

        public void Log( string s )
        {
            if ( logging_ )
                Debug.Print( s );
        }

        public void Log( byte[] bytes )
        {
            if ( logging_ )
                Debug.Print( ToString( bytes ) );
        }


        //---   Convert the byte array to a string
        //---   Note : can't use any "normal" .net mechanisms in the micro framework !
        //---   http://social.msdn.microsoft.com/forums/en-US/csharpgeneral/thread/3928b8cb-3703-4672-8ccd-33718148d1e3/

        private string ToString( byte[] bytes )
        {
            char[] c = new char[ bytes.Length * 3 ];

            byte b;

            for ( int y=0, x=0; y < bytes.Length; ++y, ++x )
            {
                b = ((byte)(bytes[ y ] >> 4));
                c[ x ] = (char)(b > 9 ? b + 0x37 : b + 0x30);

                b = ((byte)(bytes[ y ] & 0xF));
                c[ ++x ] = (char)(b > 9 ? b + 0x37 : b + 0x30);

                c[ ++x ] = ' ';
            }

            return new string( c );
        }
    }
}
