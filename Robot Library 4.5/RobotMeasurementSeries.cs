using Robot_Library_4_5;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Robot_Library_4_5
{
    public class RobotMeasurementSeries : IComparable
    {
        public int IterationID { get; set; }
        public int ID { get; set; }
        public int NumberOfCoats { get; set; }
        public Dictionary<string, RobotPointMeasurement> Measurements { get; set; }
        public ProgramAdjuster RobotProgram { get; set; }
        public int AMMPMeasurementSeriesID { get; set; }

        public RobotMeasurementSeries(int iteration, string id, string coatNumber, Dictionary<string, RobotPointMeasurement> measures, byte[] program, int ammpSeries)
        {
            this.IterationID = iteration;
            this.ID = int.Parse(id);
            this.NumberOfCoats = int.Parse(coatNumber);
            this.Measurements = measures;
            this.RobotProgram = new ProgramAdjuster(program);
            this.AMMPMeasurementSeriesID = ammpSeries;
        }

        public bool Equals(object o)
        {
            RobotMeasurementSeries otherMeas = o as RobotMeasurementSeries;
            return (this.ID == otherMeas.ID);
        }

        public bool Equals(RobotMeasurementSeries meas)
        {
            return (this.ID == meas.ID);
        }

        public int CompareTo(object o)
        {
            RobotMeasurementSeries other = o as RobotMeasurementSeries;
            return this.ID.CompareTo(other.ID);
        }

        public float getAverageThickness()
        {
            int numberOfMeasurements = 0;
            float thickness = 0;
            foreach (string pointNumber in Measurements.Keys)
            {
                numberOfMeasurements++;
                thickness += Measurements[pointNumber].PaintThickness;
            }
            if (numberOfMeasurements > 0)
                return thickness / numberOfMeasurements;
            else
                return 0;
        }

        public Dictionary<string, float> getAllThicknesses()
        {
            Dictionary<string, float> ret = new Dictionary<string, float>();
            foreach (string pointNumber in Measurements.Keys)
            {
                ret.Add(pointNumber, Measurements[pointNumber].PaintThickness);
            }
            return ret;
        }
    }
}
