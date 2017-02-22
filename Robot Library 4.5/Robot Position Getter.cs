using FRRobot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Robot_Library_4_5
{
    public class Robot_Position_Getter
    {
        FRCRobot robot;
        public Robot_Position_Getter(string address)
        {
            robot = new FRCRobot();
            robot.ConnectEx(address, false, 2, 1);
        }

        public List<decimal> getCurrentRobotPostion()
        {
            List<decimal> ret = new List<decimal>();
            FRCCurPosition currentPostion = robot.CurPosition;
            FRCXyzWpr cartesianPosition = (FRCXyzWpr)currentPostion.Group[1, FRECurPositionConstants.frWorldDisplayType].Formats[FRETypeCodeConstants.frXyzWpr];
            ret.Add((decimal)cartesianPosition.X);
            ret.Add((decimal)cartesianPosition.Y);
            ret.Add((decimal)cartesianPosition.Z);
            ret.Add((decimal)cartesianPosition.W);
            ret.Add((decimal)cartesianPosition.P);
            ret.Add((decimal)cartesianPosition.R);
            foreach (double axisValues in cartesianPosition.Ext)
            {
                ret.Add((decimal)axisValues);
            }
            return ret;
        }
    }
}
