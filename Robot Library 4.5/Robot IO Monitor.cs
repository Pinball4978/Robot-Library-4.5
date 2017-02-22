using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FRRobot;

namespace Robot_Library_4._5
{
    public class Robot_IO_Monitor
    {
        FRCRobot m_robot;
        public User_Operator_Panel_Signals UOPs { get; set; }
        public Digital_Signals Digitals { get; set; }
        public Robot_Registers Registers { get; set; }

        public Robot_IO_Monitor(FRCRobot robot)
        {
            m_robot = robot;
            UOPs = new User_Operator_Panel_Signals(robot);
            Digitals = new Digital_Signals(robot);
            Registers = new Robot_Registers(robot);
        }

        public class Robot_Registers
        {
            FRCRobot m_robot;
            FRCRegNumeric[] m_registers;

            public Robot_Registers(FRCRobot robot)
            {
                m_robot = robot;
                m_registers = new FRCRegNumeric[robot.RegNumerics.Count];
                for (int i = 1;i<=robot.RegNumerics.Count;i++)
                {
                    m_registers[i - 1] = robot.RegNumerics[i].Value;
                }
            }

            public int get(int i)
            {
                return m_registers[i - 1].RegLong;
            }

            public void set (int i, int value)
            {
                m_registers[i - 1].RegLong = value;
            }
        }

        public class Digital_Signals
        {
            FRCRobot m_robot;
            public Digital_Outputs DO { get; set; }
            public Digital_Inputs DI { get; set; }
            
            public Digital_Signals (FRCRobot robot)
            {
                this.m_robot = robot;
                this.DO = new Digital_Outputs(robot);
                this.DI = new Digital_Inputs(robot);
            }

            public class Digital_Outputs
            {
                FRCRobot m_robot;
                FRCDigitalIOType digitalOutType;
                FRCDigitalIOSignal[] doSignals;

                public Digital_Outputs(FRCRobot robot)
                {
                    m_robot = robot;
                    digitalOutType = (FRCDigitalIOType)m_robot.IOTypes[FREIOTypeConstants.frDOutType];
                    doSignals = new FRCDigitalIOSignal[digitalOutType.Signals.Count];
                    for (int i=0;i<digitalOutType.Signals.Count;i++)
                    {
                        doSignals[i] = (FRCDigitalIOSignal)digitalOutType.Signals[i + 1];
                    }
                }

                public bool get(int i)
                {
                    return doSignals[i - 1].Value;
                }

                public void set(int i, bool value)
                {
                    doSignals[i - 1].Value = value;
                }
            }

            public class Digital_Inputs
            {
                FRCRobot m_robot;
                FRCDigitalIOType digitalInType;
                FRCDigitalIOSignal[] diSignals;

                public Digital_Inputs(FRCRobot robot)
                {
                    m_robot = robot;
                    digitalInType = (FRCDigitalIOType)m_robot.IOTypes[FREIOTypeConstants.frDInType];
                    diSignals = new FRCDigitalIOSignal[digitalInType.Signals.Count];
                    for (int i=0;i<digitalInType.Signals.Count;i++)
                    {
                        diSignals[i] = (FRCDigitalIOSignal)digitalInType.Signals[i + 1];
                    }
                }

                public bool get(int i)
                {
                    return diSignals[i - 1].Value;
                }

                public void set(int i, bool value)
                {
                    diSignals[i - 1].Value = value;
                }
            }
        }

        public class User_Operator_Panel_Signals
        {
            FRCRobot m_robot;
            public User_Outputs UO { get; set; }
            public User_Inputs UI { get; set; }
            
            public User_Operator_Panel_Signals (FRCRobot robot)
            {
                this.m_robot = robot;
                this.UO = new User_Outputs(robot);
                this.UI = new User_Inputs(robot);
            }

            public class User_Outputs
            {
                FRCRobot m_robot;
                public bool Comand_Enabled 
                { 
                    get { return uopOutSignals[0].Value; }
                    set { uopOutSignals[0].Value = value; }
                }
                public bool System_Ready
                {
                    get { return uopOutSignals[1].Value; }
                    set { uopOutSignals[1].Value = value; }
                }
                public bool Program_Running
                {
                    get { return uopOutSignals[2].Value; }
                    set { uopOutSignals[2].Value = value; }
                }
                public bool Program_Paused
                {
                    get { return uopOutSignals[3].Value; }
                    set { uopOutSignals[3].Value = value; }
                }
                public bool Motion_Held
                {
                    get { return uopOutSignals[4].Value; }
                    set { uopOutSignals[4].Value = value; }
                }
                public bool Fault
                {
                    get { return uopOutSignals[5].Value; }
                    set { uopOutSignals[5].Value = value; }
                }
                public bool At_Perch
                {
                    get { return uopOutSignals[6].Value; }
                    set { uopOutSignals[6].Value = value; }
                }
                public bool TP_Enabled
                {
                    get { return uopOutSignals[7].Value; }
                    set { uopOutSignals[7].Value = value; }
                }
                public bool Battery_Alarm
                {
                    get { return uopOutSignals[8].Value; }
                    set { uopOutSignals[8].Value = value; }
                }
                public bool Busy
                {
                    get { return uopOutSignals[9].Value; }
                    set { uopOutSignals[9].Value = value; }
                }
                public bool Acknowledge_Bit_1
                {
                    get { return uopOutSignals[10].Value; }
                    set { uopOutSignals[10].Value = value; }
                }
                public bool Acknowledge_Bit_2
                {
                    get { return uopOutSignals[11].Value; }
                    set { uopOutSignals[11].Value = value; }
                }
                bool Acknowledge_Bit_3
                {
                    get { return uopOutSignals[12].Value; }
                    set { uopOutSignals[12].Value = value; }
                }
                public bool Acknowledge_Bit_4
                {
                    get { return uopOutSignals[13].Value; }
                    set { uopOutSignals[13].Value = value; }
                }
                public bool Acknowledge_Bit_5
                {
                    get { return uopOutSignals[14].Value; }
                    set { uopOutSignals[14].Value = value; }
                }
                public bool Acknowledge_Bit_6
                {
                    get { return uopOutSignals[15].Value; }
                    set { uopOutSignals[15].Value = value; }
                }
                public bool Acknowledge_Bit_7
                {
                    get { return uopOutSignals[16].Value; }
                    set { uopOutSignals[16].Value = value; }
                }
                public bool Acknowledge_Bit_8
                {
                    get { return uopOutSignals[17].Value; }
                    set { uopOutSignals[17].Value = value; }
                }
                public bool SNACK
                {
                    get { return uopOutSignals[18].Value; }
                    set { uopOutSignals[18].Value = value; }
                }
                public bool Reserved
                {
                    get { return uopOutSignals[19].Value; }
                    set { uopOutSignals[19].Value = value; }
                }

                FRCUOPIOType UOType;
                FRCUOPIOSignal[] uopOutSignals;
                
                public User_Outputs (FRCRobot robot)
                {
                    m_robot = robot;

                    UOType = (FRCUOPIOType)m_robot.IOTypes[FREIOTypeConstants.frUOPOutType];
                    uopOutSignals = new FRCUOPIOSignal[20];
                    for (int i=0;i<20;i++)
                    {
                        uopOutSignals[i] = (FRCUOPIOSignal)UOType.Signals[i+1];
                    }
                }
            }

            public class User_Inputs
            {
                FRCRobot m_robot;
                FRCUOPIOType UIType;
                FRCUOPIOSignal[] uopInSignals;

                public bool Imediate_Stop
                {
                    get { return uopInSignals[0].Value; }
                    set { uopInSignals[0].Value = value; }
                }
                public bool Hold
                {
                    get { return uopInSignals[1].Value; }
                    set { uopInSignals[1].Value = value; }
                }
                public bool Safe_Speed
                {
                    get { return uopInSignals[2].Value; }
                    set { uopInSignals[2].Value = value; }
                }
                public bool Cycle_Stop
                {
                    get { return uopInSignals[3].Value; }
                    set { uopInSignals[3].Value = value; }
                }
                public bool Fault_Reset
                {
                    get { return uopInSignals[4].Value; }
                    set { uopInSignals[4].Value = value; }
                }
                public bool Start
                {
                    get { return uopInSignals[5].Value; }
                    set { uopInSignals[5].Value = value; }
                }
                public bool Home
                {
                    get { return uopInSignals[6].Value; }
                    set { uopInSignals[6].Value = value; }
                }
                public bool Enable
                {
                    get { return uopInSignals[7].Value; }
                    set { uopInSignals[7].Value = value; }
                }
                public bool Style_Bit_1
                {
                    get { return uopInSignals[8].Value; }
                    set { uopInSignals[8].Value = value; }
                }
                public bool Style_Bit_2
                {
                    get { return uopInSignals[9].Value; }
                    set { uopInSignals[9].Value = value; }
                }
                public bool Style_Bit_3
                {
                    get { return uopInSignals[10].Value; }
                    set { uopInSignals[10].Value = value; }
                }
                public bool Style_Bit_4
                {
                    get { return uopInSignals[11].Value; }
                    set { uopInSignals[11].Value = value; }
                }
                public bool Style_Bit_5
                {
                    get { return uopInSignals[12].Value; }
                    set { uopInSignals[12].Value = value; }
                }
                public bool Style_Bit_6
                {
                    get { return uopInSignals[13].Value; }
                    set { uopInSignals[13].Value = value; }
                }
                public bool Style_Bit_7
                {
                    get { return uopInSignals[14].Value; }
                    set { uopInSignals[14].Value = value; }
                }
                public bool Style_Bit_8
                {
                    get { return uopInSignals[15].Value; }
                    set { uopInSignals[15].Value = value; }
                }
                public bool PNS_Strobe
                {
                    get { return uopInSignals[16].Value; }
                    set { uopInSignals[16].Value = value; }
                }
                public bool Production_Start
                {
                    get { return uopInSignals[17].Value; }
                    set { uopInSignals[17].Value = value; }
                }
                
                public User_Inputs (FRCRobot robot)
                {
                    m_robot = robot;

                    UIType = (FRCUOPIOType)m_robot.IOTypes[FREIOTypeConstants.frUOPInType];
                    uopInSignals = new FRCUOPIOSignal[18];
                    for (int i=0;i<18;i++)
                    {
                        uopInSignals[i] = (FRCUOPIOSignal)UIType.Signals[i+1];
                    }
                }
            }

        }
    }
}
