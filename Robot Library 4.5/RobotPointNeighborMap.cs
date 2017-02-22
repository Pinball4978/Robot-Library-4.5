using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Robot_Library_4_5
{
    public class Robot_Point_Neighbor_Map : System.Collections.IEnumerable
    {
        List<Robot_Point_Map_Node> allNodes;
        
        public Robot_Point_Neighbor_Map(string robotProgram)
        {
            ProgramAdjuster program = new ProgramAdjuster(robotProgram);
            List<Robot_Point> allPoints = program.exportPoints();
            allNodes = new List<Robot_Point_Map_Node>();
            allNodes.Add(new Robot_Point_Map_Node(allPoints[0]));
            for (int i = 1; i < allPoints.Count; i++)
            {
                allNodes.Add(new Robot_Point_Map_Node(allPoints[i]));
                allNodes[i - 1].addNeighbor(allNodes[i]);
            }
            for (int i = 0; i < allNodes.Count; i++)
            {
                List<Robot_Point> closestPoints = Robot_Point.sortByProximity(allPoints, allPoints[i]);
                Robot_Point.removePointsThatAreFarAway(closestPoints, allPoints[i], 80);
                foreach (Robot_Point point in closestPoints)
                {
                    Robot_Point_Map_Node node = findPointsNode(point);
                    allNodes[i].addNeighbor(node);
                }
            }
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            for (int i = 0; i < allNodes.Count; i++)
            {
                yield return allNodes[i];
            }
        }

        private Robot_Point_Map_Node findPointsNode(Robot_Point pointToFind)
        {
            foreach (Robot_Point_Map_Node node in this)
            {
                if (node.nodeElement.Equals(pointToFind))
                {
                    return node;
                }
            }
            return null;
        }
    }

    public class Robot_Point_Map_Node
    {
        public Robot_Point nodeElement;
        public List<Robot_Point_Map_Node> neighbors;

        public Robot_Point_Map_Node(Robot_Point point)
        {
            nodeElement = point;
            neighbors = new List<Robot_Point_Map_Node>();
        }

        public void addNeighbor(Robot_Point_Map_Node node)
        {
            if (this.Equals(node))
            {

            }
            else
            {
                if (!neighbors.Contains(node))
                {
                    neighbors.Add(node);
                }
                if (!node.neighbors.Contains(this))
                {
                    node.neighbors.Add(this);
                }
            }
        }

        public void addNeighbor(Robot_Point point)
        {
            Robot_Point_Map_Node temp = new Robot_Point_Map_Node(point);
            neighbors.Add(temp);
            temp.neighbors.Add(this);
        }

        override public bool Equals(object o)
        {
            Robot_Point_Map_Node other = o as Robot_Point_Map_Node;
            return (this.nodeElement.Equals(other.nodeElement));
        }

        public bool Equals(Robot_Point_Map_Node other)
        {
            return this.nodeElement.Equals(other.nodeElement);
        }
    }
}
