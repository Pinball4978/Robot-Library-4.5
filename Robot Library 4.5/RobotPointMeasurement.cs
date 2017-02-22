using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Robot_Library_4_5
{
    public class RobotPointMeasurement
    {
        public int MeasureSeries { get; set; }
        public string PointNumber { get; set; }
        public float PaintThickness { get; set; }
        public int Speed { get; set; }

        public RobotPointMeasurement(int measSeries, string number, float thickness, int speed)
        {
            MeasureSeries = measSeries;
            PointNumber = number;
            PaintThickness = thickness;
            Speed = speed;
        }

        public RobotPointMeasurement(string measSeries, string number, string thickness, string speed)
        {
            MeasureSeries = int.Parse(measSeries);
            PointNumber = number;
            if (thickness.Equals(string.Empty))
            {
                PaintThickness = -1;
            }
            else
            {
                PaintThickness = float.Parse(thickness);
            }
            if (speed.Equals(string.Empty))
            {
                Speed = 0;
            }
            else
            {
                Speed = int.Parse(speed);
            }
        }
    }
}
