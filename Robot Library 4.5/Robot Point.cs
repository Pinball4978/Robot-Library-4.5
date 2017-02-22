using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Matrix_Library_4_5;

namespace Robot_Library_4_5
{
    public class Robot_Point
    {
        public string PointNumber { get; set; }
        public Point_3D Point { get; set; }
        public int Speed { get; set; }

        public Robot_Point(string number, Point_3D point)
        {
            this.PointNumber = number;
            this.Point = point;
        }

        public Robot_Point(Robot_Point toClone)
        {
            this.PointNumber = toClone.PointNumber;
            this.Point = new Point_3D(toClone.Point);
        }

        public static List<Robot_Point> sortByProximity(List<Robot_Point> allPoints, Robot_Point pointToCompareTo)
        {
            List<Robot_Point> allPointsCopy = new List<Robot_Point>();
            foreach (Robot_Point point in allPoints)
            {
                allPointsCopy.Add(new Robot_Point(point));
            }
            if (allPointsCopy.Count <= 1)
            {
                if (allPointsCopy.Count == 1 && allPointsCopy[0].Equals(pointToCompareTo))
                {
                    allPointsCopy.RemoveAt(0);
                }
                return allPointsCopy;
            }
            int pivot = partition(0, allPointsCopy.Count);
            Robot_Point pivotPoint = allPointsCopy[pivot];
            allPointsCopy.RemoveAt(pivot);
            List<Robot_Point> less = new List<Robot_Point>();
            List<Robot_Point> greater = new List<Robot_Point>();
            foreach (Robot_Point indexPoint in allPointsCopy)
            {
                if (pointToCompareTo.Point.findDistanceBetweenPoints(indexPoint.Point) <= pointToCompareTo.Point.findDistanceBetweenPoints(pivotPoint.Point))
                {
                    less.Add(indexPoint);
                }
                else
                {
                    greater.Add(indexPoint);
                }
            }
            return concatenate(sortByProximity(less, pointToCompareTo), pivotPoint, sortByProximity(greater, pointToCompareTo));
        }

        private static int partition(int left, int right)
        {
            Random rand = new Random();
            return rand.Next(left, right);
        }

        private static List<Robot_Point> concatenate(List<Robot_Point> less, Robot_Point pivot, List<Robot_Point> more)
        {
            List<Robot_Point> ret = new List<Robot_Point>();
            for (int i = 0; i < less.Count; i++)
            {
                ret.Add(less[i]);
            }
            ret.Add(pivot);
            for (int i = 0; i < more.Count; i++)
            {
                ret.Add(more[i]);
            }
            return ret;
        }

        public static void removePointsThatAreFarAway(List<Robot_Point> list, Robot_Point pointToCompareTo, double maxDistance)
        {
            for (int i=0;i<list.Count;i++)
            {
                if (list[i].Point.findDistanceBetweenPoints(pointToCompareTo.Point) > maxDistance)
                {
                    list.RemoveAt(i);
                    i--;
                }
            }
        }

        override public bool Equals(Object o)
        {
            Robot_Point other = o as Robot_Point;
            return (other.PointNumber.Equals(this.PointNumber));
        }

        public bool Equals(Robot_Point o)
        {
            return (o.PointNumber.Equals(this.PointNumber));
        }
    }
}
