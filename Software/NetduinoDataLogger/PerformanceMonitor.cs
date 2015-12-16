//#define MONITOR_PERFORMANCE

using System;
using System.Threading;
using System.Collections;
using System.IO.Ports;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace NetduinoDataLogger
{
    public class PerformanceMonitor
    {
#if MONITOR_PERFORMANCE
        private Timer       timer_;
#endif
        private long[]      starts_ = new long[ 3 ];
        private long[]      performance_counters_ = new long[ 3 ];

        public enum Type
        {
            GPS  = 0,
            MAIN = 1,
            OBD = 2
        }

        private static PerformanceMonitor   self_ = null;

        public static PerformanceMonitor    Instance
        {
            get 
            { 
                if ( self_ == null ) 
                    self_ = new PerformanceMonitor(); 
                return self_; 
            }
        }

        private PerformanceMonitor()
        {
#if MONITOR_PERFORMANCE
            timer_ = new Timer( new TimerCallback( DoWork ), null, 0, 1000 );   // call DoWork() every second
#endif
        }

        public void AddPerformanceMonitor( Type type )
        {
            performance_counters_[ (int)type ] = 0;
        }

        public void StartWork( Type type )
        {
#if MONITOR_PERFORMANCE
            starts_[ (int)type ] = DateTime.Now.Ticks;
#endif
        }

        public void StopWork( Type type )
        {
#if MONITOR_PERFORMANCE
            long diff = DateTime.Now.Ticks - (long)starts_[ (int)type ];

            performance_counters_[ (int)type ] += diff;
#endif
        }

#if MONITOR_PERFORMANCE
        private void DoWork( object state )
        {
            //---   Dump our performance counters

            Debug.Print( "GPS  : " + performance_counters_[ (int)Type.GPS ] );
            Debug.Print( "Main : " + performance_counters_[ (int)Type.MAIN ] );
            Debug.Print( "OBD  : " + performance_counters_[ (int)Type.OBD ] );

            //---   Reset our counters

            performance_counters_[ (int)Type.GPS ] = 0;
            performance_counters_[ (int)Type.MAIN ] = 0;
            performance_counters_[ (int)Type.OBD ] = 0;
        }
#endif

    }

}
