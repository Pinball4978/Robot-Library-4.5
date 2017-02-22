using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FRRobot;
using System.Runtime.InteropServices;

namespace Robot_Library_4_5
{
    /// <summary>
    /// A simple class that connects to a FANUC robot using the FANUC PCDK and then retrieves or sets the user frames for the connected robot
    /// </summary>
    public class RobotUserFrameAdjuster
    {
        FRCRobot robot;
        private bool isConnectedToRobot;
        public bool IsConnectedToRobot
        {
            get { return isConnectedToRobot; }
        }

        /// <summary>
        /// Creates an instance of the robot in the PCDK and then connects to the actual robot
        /// </summary>
        /// <param name="robotIPAddress">The IP address of the robot being connected to. I.E. "192.168.0.1"</param>
        public RobotUserFrameAdjuster(string robotIPAddress)
        {
            try
            {
                robot = new FRCRobot();
                robot.Connect(robotIPAddress);          //Will fail here if robot server isn't licensed
                isConnectedToRobot = true;
            }
            catch (COMException)
            {
                robot = null;
                isConnectedToRobot = false;
            }
        }

        /// <summary>
        /// Changes a user frame on the robot
        /// </summary>
        /// <param name="userFrameNumber">The number of the user frame being changed</param>
        /// <param name="newFrame">The new user frame that will be replacing one of the frames on the robot</param>
        public void setUserFrame(int userFrameNumber, UserFrame newFrame)
        {
            if (isConnectedToRobot && userFrameNumber < robot.UserFrames.Count)
            {
                FRCSysPosition robotUserFrame = robot.UserFrames[userFrameNumber];          //retrieve the user frame from the robot
                FRCXyzWpr cartesianPosition = robotUserFrame.get_Group(1).get_Formats(FRETypeCodeConstants.frXyzWpr);       //format the frame into the XYZWPR format
                cartesianPosition.X = newFrame.X;               //replace all of the user frame's values with ones from the argument user frame
                cartesianPosition.Y = newFrame.Y;
                cartesianPosition.Z = newFrame.Z;
                cartesianPosition.W = newFrame.W;
                cartesianPosition.P = newFrame.P;
                cartesianPosition.R = newFrame.R;
                robotUserFrame.Update();                        //cause the robot's actual user frame to change
            }
        }

        /// <summary>
        /// Retrieves a user frame from the robot
        /// </summary>
        /// <param name="userFrameNumber">the number of the user frame being retrieved</param>
        /// <returns>the requested user frame, if there are any problems null will be returned</returns>
        public UserFrame getUserFrame(int userFrameNumber)
        {
            if (isConnectedToRobot && userFrameNumber < robot.UserFrames.Count)
            {
                FRCSysPosition robotUserFrame = robot.UserFrames[userFrameNumber];          //retrieve the user frame from the robot
                FRCXyzWpr cartesianPosition = robotUserFrame.get_Group(1).get_Formats(FRETypeCodeConstants.frXyzWpr);       //format the frame into the XYZWPR format
                return new UserFrame(cartesianPosition.X, cartesianPosition.Y, cartesianPosition.Z, cartesianPosition.W, cartesianPosition.P, cartesianPosition.R);     //create a user frame from the values on the robot and return it
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Changes a user frame on the robot
        /// </summary>
        /// <param name="userFrameNumber">The number of the user frame being changed</param>
        /// <param name="newFrame">The new user frame that will be replacing one of the frames on the robot</param>
        public void setUserFrame(int userFrameNumber, UserFrameDec newFrame)
        {
            if (isConnectedToRobot && userFrameNumber < robot.UserFrames.Count)
            {
                FRCSysPosition robotUserFrame = robot.UserFrames[userFrameNumber];          //retrieve the user frame from the robot
                FRCXyzWpr cartesianPosition = robotUserFrame.get_Group(1).get_Formats(FRETypeCodeConstants.frXyzWpr);       //format the frame into the XYZWPR format
                cartesianPosition.X = (double)newFrame.X;               //replace all of the user frame's values with ones from the argument user frame
                cartesianPosition.Y = (double)newFrame.Y;
                cartesianPosition.Z = (double)newFrame.Z;
                cartesianPosition.W = (double)newFrame.W;
                cartesianPosition.P = (double)newFrame.P;
                cartesianPosition.R = (double)newFrame.R;
                robotUserFrame.Update();                        //cause the robot's actual user frame to change
            }
        }

        /// <summary>
        /// Retrieves a user frame from the robot
        /// </summary>
        /// <param name="userFrameNumber">the number of the user frame being retrieved</param>
        /// <returns>the requested user frame, if there are any problems null will be returned</returns>
        public UserFrameDec getUserFrameDec(int userFrameNumber)
        {
            if (isConnectedToRobot && userFrameNumber < robot.UserFrames.Count)
            {
                FRCSysPosition robotUserFrame = robot.UserFrames[userFrameNumber];          //retrieve the user frame from the robot
                FRCXyzWpr cartesianPosition = robotUserFrame.get_Group(1).get_Formats(FRETypeCodeConstants.frXyzWpr);       //format the frame into the XYZWPR format
                return new UserFrameDec((decimal)cartesianPosition.X, (decimal)cartesianPosition.Y, (decimal)cartesianPosition.Z, (decimal)cartesianPosition.W, (decimal)cartesianPosition.P, (decimal)cartesianPosition.R);     //create a user frame from the values on the robot and return it
            }
            else
            {
                return null;
            }
        }
    }
}
