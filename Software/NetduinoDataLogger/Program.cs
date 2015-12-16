using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

using System.Collections;


namespace NetduinoDataLogger
{
    public class Program
    {
        //---   Frequency of logging

        private const int   SLEEP_TIME = 100;       // milliseconds (10Hz)

        //---   Inputs

        ArrayList           synchronous_sensors_  = new ArrayList();
        ArrayList           asynchronous_sensors_ = new ArrayList();

        GPS                 gps_;

        //---   Start/stop recording button

        InterruptPort       button_;

        //---   Outputs

        Formatter           formatter_;
        Logger              logger_;
        OutputPort          recording_led_;

        //---   State

        bool                recording_ = false;
        int                 time_step_ = 0;


        //---   Constructor

        public Program()
        {
            //---   Create outputs

            //logger_ = new DebugLogger();
            logger_ = new SDLogger();

            //formatter_ = new DebugFormatter( logger_ );
            formatter_ = new RunFormatter( logger_ );

            recording_led_ = new OutputPort( Pins.ONBOARD_LED, false );

            //---   Create our sensors

            gps_ = new GPS( formatter_ );

            asynchronous_sensors_.Add( gps_ );
            asynchronous_sensors_.Add( new OBD( formatter_ ) );

            synchronous_sensors_.Add( new Accelerometer( formatter_ ) );

            //---   Calibration

            foreach ( Sensor s in asynchronous_sensors_ )
                s.Calibrate();

            foreach ( Sensor s in synchronous_sensors_ )
                s.Calibrate();

            //---   The button used to start/stop recording

            button_ = new InterruptPort( Pins.GPIO_PIN_D4, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow );

            button_.OnInterrupt += new NativeEventHandler( button_OnInterrupt );

            //---   Performance monitoring
            PerformanceMonitor.Instance.AddPerformanceMonitor( PerformanceMonitor.Type.MAIN );
        }

        #region Button handler

        //---   Button pressed event handler

        private DateTime    time_of_last_button_press_ = new DateTime( 2000, 1, 1 );
        private const long  ONE_SECOND_IN_TICKS        = TimeSpan.TicksPerSecond;

        private void button_OnInterrupt( uint data1, uint data2, DateTime time )
        {
            //---   Debounce
            //---   For a belt-and-braces debounce, also ignore any button presses that happened less than one second ago

            button_.DisableInterrupt();

            TimeSpan    time_since_last_button_press = DateTime.Now - time_of_last_button_press_;

            if ( time_since_last_button_press.Ticks > ONE_SECOND_IN_TICKS )
            {
                time_of_last_button_press_ = DateTime.Now;

                if ( recording_ )
                {
                    //---   Stop recording

                    foreach ( AsynchronousSensor s in asynchronous_sensors_ )
                        s.Stop();

                    logger_.Stop();
                    recording_ = false;
                }
                else
                {
                    //---   Start recording

                    logger_.Start( gps_.DateTime() );
                    formatter_.FormatStart();

                    foreach ( AsynchronousSensor s in asynchronous_sensors_ )
                        s.Start();

                    time_step_ = 0;
                    recording_ = true;
                }

                recording_led_.Write( recording_ );
            }

            button_.EnableInterrupt();
        }

        #endregion

        //---   Timer callback : do some processing
        //---   This gets called every time slice (e.g. every 100ms for 10Hz)

        private void DoWork( object state )
        {
            //---   Performance monitoring
            PerformanceMonitor.Instance.StartWork( PerformanceMonitor.Type.MAIN );

            if ( recording_ )
            {
                //---   Time stamp
                //---   To get "real world" times in the Analysis software, we need 100 time stamps per second.
                //---   As our time slice is 10Hz, we need to log 10 time stamps here

                for ( int i=0; i < 10; ++i )
                    formatter_.FormatTimeStamp( time_step_++ );

                //---   Poll our synchonous inputs

                foreach ( SynchronousSensor s in synchronous_sensors_ )
                    s.ReadAndLog();
            }

            //---   Performance monitoring
            PerformanceMonitor.Instance.StopWork( PerformanceMonitor.Type.MAIN );
        }


        //---   Main loop

        public void Run()
        {
            Timer   run_timer = new Timer( new TimerCallback( DoWork ), null, 0, SLEEP_TIME );

            Thread.Sleep( Timeout.Infinite );
        }


        //---   Entry point

        public static void Main()
        {
            Program prog = new Program();

            prog.Run();
        }
    }
}
