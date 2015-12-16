using System;
using System.Threading;
using System.IO.Ports;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace NetduinoDataLogger
{
    public class Accelerometer : SynchronousSensor
    {
        private AnalogInput x_;
        private AnalogInput y_;
        private AnalogInput z_;

        //---   Calibration values : initially based on empirical measurement
        private int         calibration_x_ = 512;
        private int         calibration_y_ = 512;
        private int         calibration_z_ = 512;

        private const int   CALIBRATION_ITERATIONS = 100;

        //---   ADC input -> output G conversion parameters

        //private const float ADC_MAX          = 1024;                        // 10-bit ADC so range is 0 ... 1023
        //private const float ACCEL_RANGE      = 6;                           // Accelerometer is +/- 3G
        //private const float G_PER_UNIT       = ACCEL_RANGE / ADC_MAX;       // Each step measures this much G
        //private const float ACCEL_SCALE      = 256;                       // Scale by this much (as RaceTechnology divides by 0x100)
        //private const float ACCEL_CONVERSION = G_PER_UNIT * ACCEL_SCALE;  // Final conversion factor

        private const float G_PER_UNIT = 0.01F;


        //---   The formatter to use

        private Formatter   formatter_;

        //---   Constructor

        public Accelerometer( Formatter formatter )
        {
            formatter_ = formatter;

            x_ = new AnalogInput( Pins.GPIO_PIN_A0 );
            y_ = new AnalogInput( Pins.GPIO_PIN_A1 );
            z_ = new AnalogInput( Pins.GPIO_PIN_A2 );
        }


        public void ReadXYZ( out float x, out float y, out float z )
        {
            x = (float)(x_.Read() - calibration_x_) * G_PER_UNIT;
            y = (float)(y_.Read() - calibration_y_) * G_PER_UNIT;
            z = (float)(z_.Read() - calibration_z_) * G_PER_UNIT;
        }

        /*
        public void ReadXY( out int x, out int y )
        {
            x = (int)((float)(x_.Read() - calibration_x_) * ACCEL_CONVERSION);
            y = (int)((float)(y_.Read() - calibration_y_) * ACCEL_CONVERSION);
        }

        public void ReadZ( out int z )
        {
            z = (int)((float)(z_.Read() - calibration_z_) * ACCEL_CONVERSION);
        }
        */

        public bool Calibrate()
        {
            // DON'T CALIBRATE FOR NOW !

            //---   Calculate calibration values
            //---   Assumes accelerometer is on a flat and level surface

            //int x = 0;
            //int y = 0;
            //int z = 0;

            //for ( int i = 0; i < CALIBRATION_ITERATIONS; ++i )
            //{
            //    x += x_.Read();
            //    y += y_.Read();
            //    z += z_.Read();
            //}

            //calibration_x_ = x / CALIBRATION_ITERATIONS;
            //calibration_y_ = y / CALIBRATION_ITERATIONS;
            //calibration_z_ = z / CALIBRATION_ITERATIONS;

            return true;
        }

        public void ReadAndLog()
        {
            float x_accel, y_accel, z_accel;

            ReadXYZ( out x_accel, out y_accel, out z_accel );

            formatter_.FormatAccel( x_accel, y_accel, z_accel );
        }
    }
}
