using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Robot_Library_4_5
{
    public class UserFrameBounds
    {
        private double m_minX;
        private double m_maxX;
        private double m_minY;
        private double m_maxY;
        private double m_minZ;
        private double m_maxZ;
        private double m_minW;
        private double m_maxW;
        private double m_minP;
        private double m_maxP;
        private double m_minR;
        private double m_maxR;

        public UserFrameBounds(XElement bounds)
        {
            m_minX = double.Parse(bounds.Element("Min_X").Value);
            m_maxX = double.Parse(bounds.Element("Max_X").Value);
            m_minY = double.Parse(bounds.Element("Min_Y").Value);
            m_maxY = double.Parse(bounds.Element("Max_Y").Value);
            m_minZ = double.Parse(bounds.Element("Min_Z").Value);
            m_maxZ = double.Parse(bounds.Element("Max_Z").Value);
            m_minW = double.Parse(bounds.Element("Min_W").Value);
            m_maxW = double.Parse(bounds.Element("Max_W").Value);
            m_minP = double.Parse(bounds.Element("Min_P").Value);
            m_maxP = double.Parse(bounds.Element("Max_P").Value);
            m_minR = double.Parse(bounds.Element("Min_R").Value);
            m_maxR = double.Parse(bounds.Element("Max_R").Value);
        }

        public bool isInBounds(UserFrame frame)
        {
            if (frame.X > m_minX && frame.X < m_maxX && frame.Y > m_minY && frame.Y < m_maxY && frame.Z > m_minZ && frame.Z < m_maxZ)
            {
                if (frame.W > m_minW && frame.W < m_maxW && frame.P > m_minP && frame.P < m_maxP && frame.R > m_minR && frame.R < m_maxR)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
