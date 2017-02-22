using Matrix_Library_4_5;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Robot_Library_4_5
{
    public class Paint_Iteration : IComparable
    {
        public int ID { get; set; }
        public string Part { get; set; }
        public string Paint { get; set; }
        public DateTime Start_Time { get; set; }
        public List<RobotMeasurementSeries> Measurement_Series { get; set; }

        public Paint_Iteration(int id, string part, string paint, DateTime time, List<RobotMeasurementSeries> series)
        {
            this.ID = id;
            this.Paint = paint;
            this.Part = part;
            this.Start_Time = time;
            this.Measurement_Series = series;
        }

        public int CompareTo(object o)
        {
            Paint_Iteration other = o as Paint_Iteration;
            return this.ID.CompareTo(other.ID);
        }

        public static string convertDateTimeToString(DateTime time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public float getCumalativeThicknessForPoint(string pointNumber)
        {
            float thickness = 0;
            foreach(RobotMeasurementSeries series in Measurement_Series)
            {
                if (series.Measurements.ContainsKey(pointNumber))
                {
                    thickness += series.Measurements[pointNumber].PaintThickness;
                }
            }
            return thickness;
        }

        public float getCumalativeThicknessForPoint(string pointNumber, int stopSeriesNumber)
        {
            float thickness = 0;
            for (int i = 0; i < stopSeriesNumber;i++ )
            {
                if (Measurement_Series[i].Measurements.ContainsKey(pointNumber))
                {
                    thickness += Measurement_Series[i].Measurements[pointNumber].PaintThickness;
                }
                else
                {
                    return -1;
                }
            }
            return thickness;
            
        }

        public float getAverageThickness(int stopSeriesNumber)
        {
            List<string> allPointNumbers = getAllPointNumbers();
            float thickness = 0;
            int numberOfPoints = 0;
            foreach(string pointNumber in allPointNumbers)
            {
                float temp = getCumalativeThicknessForPoint(pointNumber, stopSeriesNumber);
                if (temp >= 0)
                {
                    thickness += temp;
                    numberOfPoints++;
                }
            }
            if (numberOfPoints > 0)
                return thickness / numberOfPoints;
            else
                return 0;
        }

        public float getMinimumThicknessInSeries(int stopSeriesNumber)
        {
            List<string> allPointNumbers = getAllPointNumbers();
            float minThickness = float.MaxValue;
            foreach (string pointNumber in allPointNumbers)
            {
                float temp = getCumalativeThicknessForPoint(pointNumber, stopSeriesNumber);
                if (temp < minThickness && temp != -1)
                {
                    minThickness = temp;                   
                }
            }
            return minThickness;
        }

        public float getMaximumThicknessInSeries(int stopSeriesNumber)
        {
            List<string> allPointNumbers = getAllPointNumbers();
            float maxThickness = float.MinValue;
            foreach (string pointNumber in allPointNumbers)
            {
                float temp = getCumalativeThicknessForPoint(pointNumber, stopSeriesNumber);
                if (temp > maxThickness && temp != -1)
                {
                    maxThickness = temp;
                }
            }
            return maxThickness;
        }

        public Dictionary<string, float> getAllCumalativeThicknesses(int stopSeriesNumber)
        {
            List<string> allPointNumbers = getAllPointNumbers();
            Dictionary<string, float> ret = new Dictionary<string, float>();
            foreach (string pointNumber in allPointNumbers)
            {
                float temp = getCumalativeThicknessForPoint(pointNumber, stopSeriesNumber);
                if (temp >= 0)
                    ret.Add(pointNumber, temp);
            }
            return ret;
        }

        public Dictionary<string, float> getAllCumalativeThicknesses()
        {
            List<string> allPointNumbers = getAllPointNumbers();
            int stopSeriesNumber = Measurement_Series.Count;
            Dictionary<string, float> ret = new Dictionary<string, float>();
            foreach (string pointNumber in allPointNumbers)
            {
                float temp = getCumalativeThicknessForPoint(pointNumber, stopSeriesNumber);
                if (temp >= 0)
                    ret.Add(pointNumber, temp);
            }
            return ret;
        }

        public Dictionary<string, int> getAllSpeeds(int seriesNumber)
        {
            Dictionary<string, int> ret = new Dictionary<string, int>();
            foreach(RobotPointMeasurement point in Measurement_Series[seriesNumber].Measurements.Values)
            {
                ret.Add(point.PointNumber, point.Speed);
            }
            return ret;
        }

        public List<string> getAllPointNumbers()
        {
            List<string> ret = new List<string>();
            for (int i=0;i<Measurement_Series.Count;i++)
            {
                foreach(string pointNumber in Measurement_Series[i].Measurements.Keys)
                {
                    if (!ret.Contains(pointNumber))
                    {
                        ret.Add(pointNumber);
                    }
                }
            }
            ret.Sort(pointCompare);
            return ret;
        }

        public BestFitLine buildBestFitLineWithoutChecking(string pointNumber)
        {
            List<Point_2D> allPoints = new List<Point_2D>();
            foreach (RobotMeasurementSeries series in Measurement_Series)
            {
                if (series.Measurements.ContainsKey(pointNumber))
                {
                    allPoints.Add(new Point_2D(series.Measurements[pointNumber].Speed, series.Measurements[pointNumber].PaintThickness / series.NumberOfCoats));
                }
            }
            return new BestFitLine(allPoints);
        }

        public BestFitLine buildBestFitLine(string pointNumber)
        {
            List<Point_2D> allPoints = new List<Point_2D>();
            float minSpeed = float.MaxValue;
            float maxSpeed = float.MinValue;
            foreach (RobotMeasurementSeries series in Measurement_Series)
            {
                if (series.Measurements.ContainsKey(pointNumber))
                {
                    allPoints.Add(new Point_2D(series.Measurements[pointNumber].Speed, series.Measurements[pointNumber].PaintThickness / series.NumberOfCoats));
                    if (series.Measurements[pointNumber].Speed < minSpeed)
                        minSpeed = series.Measurements[pointNumber].Speed;
                    if (series.Measurements[pointNumber].Speed > maxSpeed)
                        maxSpeed = series.Measurements[pointNumber].Speed;
                }
                
            }
            if (allPoints.Count > 1 && (maxSpeed - minSpeed) >= 10)
            {
                BestFitLine ret = new BestFitLine(allPoints);
                if (ret.RegressionCoefficient > -0.000005 || ret.CorrelationCoefficient < 0.5 || ret.RegressionConstant < .001)
                    return null;
                else
                    return ret;
            }
            else
                return null;
        }

        public BestFitLine buildBestFitLine(string pointNumber, int seriesStop)
        {
            List<Point_2D> allPoints = new List<Point_2D>();
            for (int i = 0;i <= seriesStop; i++)
            {
                RobotMeasurementSeries series = Measurement_Series[i];
                if (series.Measurements.ContainsKey(pointNumber))
                {
                    allPoints.Add(new Point_2D(series.Measurements[pointNumber].Speed, series.Measurements[pointNumber].PaintThickness / series.NumberOfCoats));
                }
            }
            return new BestFitLine(allPoints);
        }

        private static int pointCompare(string p1, string p2)
        {
            int p1Index = p1.IndexOf("_");
            int p2Index = p2.IndexOf("_");
            if (p1Index >= 0)
                p1 = p1.Substring(0, p1Index);
            if (p2Index >= 0)
                p2 = p2.Substring(0, p2Index);
            int p1Number = int.Parse(p1);
            int p2Number = int.Parse(p2);
            if (p1Number == p2Number)
            {
                if (p1Index >= 0)
                    return 1;
                else if (p2Index >= 0)
                    return -1;
                else
                    return 0;
            }
            else
            {
                return p1Number - p2Number;
            }
        }
    }
}
