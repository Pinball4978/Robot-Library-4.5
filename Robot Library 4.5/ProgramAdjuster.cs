using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Matrix_Library_4_5;

namespace Robot_Library_4_5
{
    /// <summary>
    /// This class works on the .ls files that FANUC robots use. The class is used to make modifications to those files. 
    /// </summary>
    public class ProgramAdjuster
    {
        public enum AxisType { LINEAR, ROTATIONAL };
        private List<AxisType> m_axisTypes;
        private string m_lsFileName;
        private List<string>[] m_programSections;

        private const float MIN_PAINT_THICKNESS_FOR_CHANGE = 0.004f;

        #region Properties

        public List<AxisType> ExtendedAxisTypes
        {
            get
            {
                return m_axisTypes;
            }
        }

        public string FullFileName
        {
            get 
            {
                return m_lsFileName;
            }
        }

        public string FileName
        {
            get
            {
                int index = m_lsFileName.LastIndexOf('\\');
                return m_lsFileName.Substring(index + 1);
            }
        }

        public string ProgramComment
        {
            get
            {
                string temp = m_programSections[0].ElementAt(3);
                int indexOfCommentBegin = temp.IndexOf('\"');
                int indexOfCommentEnd = temp.IndexOf('\"', indexOfCommentBegin+1);
                return temp.Substring(indexOfCommentBegin + 1, indexOfCommentEnd - indexOfCommentBegin - 1);
            }
            set 
            {
                changeProgramComment(value);
            }
        }

        /// <summary>
        /// Concatonates all of the user frames together into one string
        /// </summary>
        public string UserFramesUsed
        {
            get
            {
                string ret = "";
                List<int> frames = returnAllUsedUserFrames();
                for (int i = 0; i < frames.Count; i++)
                {
                    ret += frames[i].ToString();
                    if (i + 1 < frames.Count)
                    {
                        ret += ", ";
                    }
                }
                return ret;
            }
        }

        /// <summary>
        /// Concatonates all of the tool frames together into one string
        /// </summary>
        public string ToolFramesUsed
        {
            get
            {
                string ret = "";
                List<int> frames = returnAllUsedToolFrames();
                for (int i = 0; i < frames.Count; i++)
                {
                    ret += frames[i].ToString();
                    if (i + 1 < frames.Count)
                    {
                        ret += ", ";
                    }
                }
                return ret;
            }
        }

        public int NumberOfPoints
        {
            get
            {
                return m_programSections[2].Count - 1;
            }
        }

        #endregion

        #region Opening/Saving File

        public ProgramAdjuster(string lsFile)
        {
            m_axisTypes = new List<AxisType>();
            m_axisTypes.Add(AxisType.LINEAR);     //X
            m_axisTypes.Add(AxisType.LINEAR);     //Y
            m_axisTypes.Add(AxisType.LINEAR);     //Z
            m_axisTypes.Add(AxisType.ROTATIONAL); //W
            m_axisTypes.Add(AxisType.ROTATIONAL); //P
            m_axisTypes.Add(AxisType.ROTATIONAL); //R
            FileInfo file = new FileInfo(lsFile);
            if (file.Exists && file.Extension.Equals(".ls", StringComparison.CurrentCultureIgnoreCase))
            {
                m_lsFileName = lsFile;
                m_programSections = new List<string>[3];          // the ls file to be read in is broken up into three sections
                m_programSections[0] = new List<string>();
                m_programSections[1] = new List<string>();
                m_programSections[2] = new List<string>();
                using (TextReader reader = new StreamReader(lsFile))
                {
                    string temp = reader.ReadLine();
                    while (!temp.Equals("/MN"))                 //the program header is the first section
                    {
                        m_programSections[0].Add(temp + "\r\n");
                        temp = reader.ReadLine();
                    }
                    m_programSections[0].Add("/MN\r\n");
                    temp = reader.ReadLine();
                    while (!temp.Equals("/POS"))                //the program lines are the second section
                    {
                        m_programSections[1].Add(temp + "\r\n");
                        temp = reader.ReadLine();
                    }
                    m_programSections[1].Add("/POS\r\n");
                    temp = reader.ReadLine();
                    while (!temp.Equals("/END"))                //the points are the third section
                    {
                        string temp2 = temp;
                        temp += "\r\n";
                        while (!temp2.Equals("};"))             // each element in the third list is all of the data associated with a single point
                        {
                            temp2 = reader.ReadLine();
                            temp += temp2 + "\r\n";
                        }
                        m_programSections[2].Add(temp);
                        temp = reader.ReadLine();
                    }
                    m_programSections[2].Add("/END\r\n");
                }
                determineExtendedAxisStatus();
            }
        }

        public ProgramAdjuster(byte[] fileInBinary)
        {
            char[] charArray = new char[fileInBinary.Length];
            for (int i=0;i<fileInBinary.Length;i++)
            {
                charArray[i] = (char)fileInBinary[i];
            }
            StringBuilder builder = new StringBuilder();
            builder.Append(charArray);
            string fullFile = builder.ToString();            
            int endOfFirstSection = fullFile.IndexOf("/MN");
            int endOfSecondSection = fullFile.IndexOf("/POS");
            m_programSections = new List<string>[3];          // the ls file to be read in is broken up into three sections
            m_programSections[0] = new List<string>();
            m_programSections[1] = new List<string>();
            m_programSections[2] = new List<string>();
            string firstPart = fullFile.Substring(0, endOfFirstSection);
            string[] firstSplit = firstPart.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < firstSplit.Length; i++)
            {
                m_programSections[0].Add(firstSplit[i] + "\n");
            }
            string secondPart = fullFile.Substring(endOfFirstSection, endOfSecondSection - endOfFirstSection);
            string[] secondSplit = secondPart.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            m_programSections[0].Add(secondSplit[0] + "\n");
            for (int i=1; i < secondSplit.Length;i++)
            {
                m_programSections[1].Add(secondSplit[i] + "\n");
            }
            string thirdPart = fullFile.Substring(endOfSecondSection);
            string[] thirdSplit = thirdPart.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            m_programSections[1].Add(thirdSplit[0] + "\n");
            for (int i=1;i<thirdSplit.Length;i++)
            {
                string tempPointString = "";
                while (!thirdSplit[i].Contains(';') && !thirdSplit[i].Contains("/END"))
                {
                    tempPointString += thirdSplit[i] + "\n";
                    i++;
                }
                tempPointString += thirdSplit[i] + "\n";
                m_programSections[2].Add(tempPointString);
            }
        }

        public byte[] toByteArray()
        {
            List<byte> ret = new List<byte>();
            for (int i=0;i<m_programSections[0].Count;i++)
            {
                char[] tempChar = m_programSections[0].ElementAt(i).ToCharArray();
                for (int j=0;j<tempChar.Length;j++)
                {
                    ret.Add((byte)tempChar[j]);
                    //byte[] tempByte = BitConverter.GetBytes(tempChar[j]);
                    //for (int k=0;k<tempByte.Length;k++)
                    //{
                    //    ret.Add(tempByte[k]);
                    //}
                }
            }
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                char[] tempChar = m_programSections[1].ElementAt(i).ToCharArray();
                for (int j = 0; j < tempChar.Length; j++)
                {
                    ret.Add((byte)tempChar[j]);
                    //byte[] tempByte = BitConverter.GetBytes(tempChar[j]);
                    //for (int k = 0; k < tempByte.Length; k++)
                    //{
                    //    ret.Add(tempByte[k]);
                    //}
                }
            }
            for (int i = 0; i < m_programSections[2].Count; i++)
            {
                char[] tempChar = m_programSections[2].ElementAt(i).ToCharArray();
                for (int j = 0; j < tempChar.Length; j++)
                {
                    ret.Add((byte)tempChar[j]);
                    //byte[] tempByte = BitConverter.GetBytes(tempChar[j]);
                    //for (int k = 0; k < tempByte.Length; k++)
                    //{
                    //    ret.Add(tempByte[k]);
                    //}
                }
            }
            return ret.ToArray();
        }

        /// <summary>
        /// Goes through the first point to determine what kind of extended axes this program has
        /// </summary>
        private void determineExtendedAxisStatus()
        {
            if (!m_programSections[2].ElementAt(0).Equals("/END\r\n"))
            {
                string examplePointString = m_programSections[2].ElementAt(0);
                int indexOfDecimal = examplePointString.IndexOf("R = ");
                if (indexOfDecimal == -1)
                {
                    indexOfDecimal = examplePointString.IndexOf("J6=");
                }
                indexOfDecimal = examplePointString.IndexOf('.', indexOfDecimal);
                indexOfDecimal = examplePointString.IndexOf('.', indexOfDecimal + 1);
                while (indexOfDecimal >= 0)
                {
                    string units = examplePointString.Substring(indexOfDecimal + 4, 4);
                    units = units.Trim();
                    if (units.Equals("mm") || units.Equals("mm,"))
                    {
                        m_axisTypes.Add(AxisType.LINEAR);
                    }
                    else
                    {
                        m_axisTypes.Add(AxisType.ROTATIONAL);
                    }
                    indexOfDecimal = examplePointString.IndexOf('.', indexOfDecimal + 1);
                }
            }
        }

        /// <summary>
        /// Saves the .ls file to file with all of the changes that have been applied to it
        /// </summary>
        /// <param name="fileName">the name the file will be saved as (pass in full name)</param>
        public void saveProgramToFile(string fileName, bool isPaintProcess)
        {
            int indexOfExtension = fileName.LastIndexOf('.');
            if (indexOfExtension == -1)
            {
                fileName += ".ls";
                indexOfExtension = fileName.LastIndexOf('.');
            }
            if (fileName.Substring(indexOfExtension).Equals(".ls", StringComparison.CurrentCultureIgnoreCase))
            {
                using (TextWriter output = new StreamWriter(fileName))
                {
                    int indexOfFileName = fileName.LastIndexOf('\\');
                    if (isPaintProcess)
                    {
                        output.WriteLine("/PROG  " + fileName.Substring(indexOfFileName + 1, indexOfExtension - indexOfFileName - 1).ToUpper() + "\t\tProcess");
                    }
                    else
                    {
                        output.WriteLine("/PROG  " + fileName.Substring(indexOfFileName + 1, indexOfExtension - indexOfFileName - 1).ToUpper());
                    }
                    if (isPaintProcess)
                    {
                        for (int i = 1; i < m_programSections[0].Count; i++)
                        {
                            output.Write(m_programSections[0].ElementAt(i));
                        }
                    }
                    else
                    {
                        for (int i = 1; i < m_programSections[0].Count; i++)
                        {
                            string temp = m_programSections[0].ElementAt(i);
                            if (m_programSections[0].ElementAt(i).Equals("PAINT_PROCESS;" + Environment.NewLine))
                            {
                                i++;
                                temp = m_programSections[0].ElementAt(i);
                                while (!m_programSections[0].ElementAt(i).Equals(Environment.NewLine))
                                {
                                    i++;
                                    temp = m_programSections[0].ElementAt(i);
                                }
                                i++;
                                temp = m_programSections[0].ElementAt(i);
                                while (!m_programSections[0].ElementAt(i).Equals(Environment.NewLine) && !m_programSections[0].ElementAt(i).Equals("/MN" + Environment.NewLine))
                                {
                                    i++;
                                    temp = m_programSections[0].ElementAt(i);
                                }
                                if (m_programSections[0].ElementAt(i).Equals("/MN" + Environment.NewLine))
                                {
                                    output.Write(m_programSections[0].ElementAt(i));
                                }
                            }
                            else
                            {
                                output.Write(m_programSections[0].ElementAt(i));
                            }
                        }
                    }
                    if (isPaintProcess)
                    {
                        for (int i = 0; i < m_programSections[1].Count; i++)
                        {
                            output.Write(m_programSections[1].ElementAt(i));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < m_programSections[1].Count; i++)
                        {
                            if (m_programSections[1].ElementAt(i).Contains("Preset"))
                            {
                                string temp = m_programSections[1].ElementAt(i);
                                int indexOfEnd = temp.IndexOf(';');
                                int indexOfPreset = temp.IndexOf("Preset");
                                output.Write(temp.Substring(0, indexOfPreset) + "!" + temp.Substring(indexOfPreset));
                            }
                            else
                            {
                                output.Write(m_programSections[1].ElementAt(i));
                            }
                        }
                    }
                    for (int i = 0; i < m_programSections[2].Count; i++)
                    {
                        output.Write(m_programSections[2].ElementAt(i));
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// changes the program's comment in the header
        /// </summary>
        /// <param name="newComment">the new comment for this program</param>
        public void changeProgramComment(string newComment)
        {
            string newCommentLine = "COMMENT		= \"" + newComment + "\";\n";
            m_programSections[0].RemoveAt(3);
            m_programSections[0].Insert(3, newCommentLine);
        }

        #region Compare Programs

        public string findDifferencesBetweenPrograms(ProgramAdjuster otherProgram)
        {
            const string excelProgramSpaceing = ",,,,";
            const string excelPointSpaceing = ",,,";
            string ret = "Program Line Differences:" + Environment.NewLine + "Program 1 Values" + excelProgramSpaceing + "Program 2 Values" + Environment.NewLine;
            int i = 0, j = 0;
            for (; i < this.m_programSections[1].Count - 1 && j < otherProgram.m_programSections[1].Count - 1; i++, j++)
            {
                string tempLine1 = this.m_programSections[1].ElementAt(i).Replace(Environment.NewLine, "");
                string tempLine2 = otherProgram.m_programSections[1].ElementAt(j).Replace(Environment.NewLine, "");
                tempLine1 = tempLine1.Replace(',', ' ');
                tempLine2 = tempLine2.Replace(',', ' ');
                if (!tempLine1.Equals(tempLine2))
                {
                    ret += tempLine1 + excelProgramSpaceing + tempLine2 + Environment.NewLine;
                }
            }
            while (i < this.m_programSections[1].Count - 1)
            {
                string tempLine1 = this.m_programSections[1].ElementAt(i).Replace(Environment.NewLine, "");
                tempLine1 = tempLine1.Replace(',', ' ');
                ret += tempLine1 + Environment.NewLine;
                i++;
            }
            while (j < otherProgram.m_programSections[1].Count - 1)
            {
                string tempLine2 = otherProgram.m_programSections[1].ElementAt(j).Replace(Environment.NewLine, "");
                tempLine2 = tempLine2.Replace(',', ' ');
                ret += excelProgramSpaceing + tempLine2 + Environment.NewLine;
                j++;
            }
            ret += Environment.NewLine + "Point Differences:" + Environment.NewLine;
            i = j = 0;
            for (; i < this.m_programSections[2].Count - 1 && j < otherProgram.m_programSections[2].Count - 1; i++, j++)
            {
                int program1PointNumber = parsePointNumberFromPointString(this.m_programSections[2].ElementAt(i));
                int program2PointNumber = parsePointNumberFromPointString(otherProgram.m_programSections[2].ElementAt(j));
                if (program1PointNumber < program2PointNumber)
                {
                    ret += excelProgramSpaceing + "Program 2 doesn't have a point with number " + program1PointNumber + "." + Environment.NewLine;
                    j--;
                }
                else if (program2PointNumber < program1PointNumber)
                {
                    ret += "Program 1 doesn't have a point with number " + program2PointNumber + "." + Environment.NewLine;
                    i--;
                }
                else
                {
                    string prog1PointString = this.m_programSections[2].ElementAt(i);
                    string prog2PointString = otherProgram.m_programSections[2].ElementAt(j);
                    if (!prog1PointString.Equals(prog2PointString))
                    {
                        ret += program1PointNumber + Environment.NewLine;
                        List<string>[] diffs = findDifferencesBetweenPointStrings(prog1PointString, prog2PointString);
                        for (int k = 0; k < diffs[0].Count; k++)
                        {
                            ret += "," + diffs[0].ElementAt(k) + excelPointSpaceing + diffs[1].ElementAt(k) + Environment.NewLine;
                        }
                    }
                }
            }
            while (i < this.m_programSections[2].Count - 1)
            {
                int program1PointNumber = parsePointNumberFromPointString(this.m_programSections[2].ElementAt(i));
                ret += excelProgramSpaceing + "Program 2 doesn't have a point with number " + program1PointNumber + "." + Environment.NewLine;
                i++;
            }
            while (j < otherProgram.m_programSections[2].Count - 1)
            {
                int program2PointNumber = parsePointNumberFromPointString(otherProgram.m_programSections[2].ElementAt(j));
                ret += "Program 1 doesn't have a point with number " + program2PointNumber + "." + Environment.NewLine;
                j++;
            }
            return ret;
        }

        public void findDifferencesBetweenPrograms(ProgramAdjuster otherProgram, string fileLocation)
        {
            int indexOfExtension = fileLocation.LastIndexOf('.');
            if (indexOfExtension == -1)
            {
                fileLocation += ".csv";
            }
            else
            {
                fileLocation = fileLocation.Substring(0, indexOfExtension) + ".csv";
            }
            using (TextWriter output = new StreamWriter(fileLocation))
            {
                output.WriteLine(findDifferencesBetweenPrograms(otherProgram));
            }
        }

        #endregion

        #region Change Program Line Stuff

        public void addToolOffset(int offsetNumber)
        {
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                if (line.Contains("P[") || line.Contains("PR["))
                {
                    int indexOfLineEnd = line.IndexOf(';');
                    line = line.Insert(indexOfLineEnd - 4, " Tool_Offset,PR[" + offsetNumber + "]");
                    m_programSections[1].Insert(i, line);
                    m_programSections[1].RemoveAt(i + 1);
                }
            }
        }

        public void addToolOffset(int offsetNumber, int startingPoint, int endingPoint)
        {
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                if (line.Contains("P[") || line.Contains("PR["))
                {
                    int indexOfPointNumber = line.IndexOf('[');
                    int indexOfClosePointNumber = line.IndexOf(']');
                    int pointNumber = int.Parse(line.Substring(indexOfPointNumber + 1, indexOfClosePointNumber - indexOfPointNumber - 1));
                    if (pointNumber >= startingPoint && pointNumber <= endingPoint)
                    {
                        int indexOfLineEnd = line.IndexOf(';');
                        line = line.Insert(indexOfLineEnd - 4, " Tool_Offset,PR[" + offsetNumber + "]");
                        m_programSections[1].Insert(i, line);
                        m_programSections[1].RemoveAt(i + 1);
                    }
                }
            }
        }

        public void addGroupOutputSettingToLines(int ioNumber)
        {
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                if (line.Contains("P[") || line.Contains("PR["))
                {
                    int pointNumber = parsePointNumberFromLine(line);
                    string lineToInsert = " 0:  GO[" + ioNumber + "]=0 ;GO[] = " + pointNumber + " ;";
                    m_programSections[1].Insert(i + 1, lineToInsert);
                    i++;
                }
            }
        }

        public void changePointTerminationType(int terminationSpeed, int pointNumber)
        {
            for (int i=0;i<m_programSections[1].Count; i++)
            {
                try
                {
                    string line = m_programSections[1].ElementAt(i);
                    int point = parsePointNumberFromLine(line);
                    if (point == pointNumber)
                    {
                        int indexOfTerm = line.IndexOf("sec");
                        if (indexOfTerm >= 0)
                        {
                            indexOfTerm += 4;
                            string newLine = line.Substring(0, indexOfTerm);
                            int indexOfCloseTerm = line.IndexOf(' ', indexOfTerm);
                            if (terminationSpeed == 0)
                            {
                                newLine += "FINE" + line.Substring(indexOfCloseTerm);
                                m_programSections[1].Insert(i, newLine);
                                m_programSections[1].RemoveAt(i + 1);
                            }
                            else if (terminationSpeed > 0 && terminationSpeed <=100)
                            {
                                newLine += "CNT" + terminationSpeed + line.Substring(indexOfCloseTerm);
                                m_programSections[1].Insert(i, newLine);
                                m_programSections[1].RemoveAt(i + 1);
                            }
                        }
                    }
                }
                catch (Exception)
                {

                }
            }
        }

        public void changePointTerminationTypeForRangePoints(int terminationSpeed, int startPoint, int endPoint)
        {
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                try
                {
                    string line = m_programSections[1].ElementAt(i);
                    int point = parsePointNumberFromLine(line);
                    if (point >= startPoint && point <= endPoint)
                    {
                        int indexOfTerm = line.IndexOf("sec");
                        if (indexOfTerm >= 0)
                        {
                            indexOfTerm += 4;
                            string newLine = line.Substring(0, indexOfTerm);
                            int indexOfCloseTerm = line.IndexOf(' ', indexOfTerm);
                            if (terminationSpeed == 0)
                            {
                                newLine += "FINE" + line.Substring(indexOfCloseTerm);
                                m_programSections[1].Insert(i, newLine);
                                m_programSections[1].RemoveAt(i + 1);
                            }
                            else if (terminationSpeed > 0 && terminationSpeed <= 100)
                            {
                                newLine += "CNT" + terminationSpeed + line.Substring(indexOfCloseTerm);
                                m_programSections[1].Insert(i, newLine);
                                m_programSections[1].RemoveAt(i + 1);
                            }
                        }
                    }
                }
                catch (Exception)
                {

                }
            }
        }

        #endregion

        #region Speed Change Stuff

        /// <summary>
        /// replaces the speed value for a move statement in the program section of the file you are modifying
        /// </summary>
        /// <param name="pointNumber">the point number that you want to change the speed of the move for</param>
        /// <param name="newSpeed">the new speed to be used for the move statement to the given point number</param>
        public void replaceSpeedForPoint(int pointNumber, int newSpeed)
        {
            List<int> lineIndexes = new List<int>();
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string tempString = m_programSections[1].ElementAt(i);
                if (parsePointNumberFromLine(tempString) == pointNumber)
                    lineIndexes.Add(i);
            }
            foreach (int indexOfLine in lineIndexes)
            {
                string lineWithPointInIt = m_programSections[1].ElementAt(indexOfLine);
                int indexOfMoveType = lineWithPointInIt.IndexOf(':') + 1;
                int indexOfPoint = lineWithPointInIt.IndexOf(']');
                int indexOfUnits;
                if (lineWithPointInIt.ElementAt(indexOfMoveType).Equals('J'))
                {                                               //Joint moves' speed values are a percentage 
                    if (newSpeed > 100)
                    {
                        newSpeed = 100;
                    }
                    indexOfUnits = lineWithPointInIt.IndexOf('%');
                }
                else if (lineWithPointInIt.ElementAt(indexOfMoveType).Equals('C'))
                {
                    lineWithPointInIt = m_programSections[1].ElementAt(indexOfLine + 1);
                    indexOfPoint = lineWithPointInIt.IndexOf(']');
                    indexOfUnits = lineWithPointInIt.IndexOf("mm/sec"); ;
                }
                else
                {                                               //Linear moves' speed values are in mm/sec
                    indexOfUnits = lineWithPointInIt.IndexOf("mm/sec");
                }
                string temp = lineWithPointInIt.Substring(0, indexOfPoint + 2) + newSpeed + lineWithPointInIt.Substring(indexOfUnits);      //recreate the existing program line with a different speed substituted in
                m_programSections[1].Insert(indexOfLine, temp);
                m_programSections[1].RemoveAt(indexOfLine + 1);                         //update programe sections with updated program line
            }
        }

        /// <summary>
        /// Adjusts the speed for a move statement by a percenatage amount of the existing speed of that point
        /// </summary>
        /// <param name="pointNumber">the point number of the move statement that will be changed</param>
        /// <param name="percentChange">the percentage of change that is to be applied to the point. Ex -10.0 slows down the point's speed by 10%, 10.0 would speed up the point by 10%</param>
        public void adjustSpeedByPercentForPoint(int pointNumber, double percentChange)
        {
            List<int> lineIndexes = new List<int>();
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string tempString = m_programSections[1].ElementAt(i);
                if (parsePointNumberFromLine(tempString) == pointNumber)
                    lineIndexes.Add(i);
            }
            foreach (int indexOfLine in lineIndexes)
            {
                string lineWithPointInIt = m_programSections[1].ElementAt(indexOfLine);
                int indexOfMoveType = lineWithPointInIt.IndexOf(':') + 1;
                int indexOfPoint = lineWithPointInIt.IndexOf(']');
                int indexOfUnits;
                double newSpeed;
                if (lineWithPointInIt.ElementAt(indexOfMoveType).Equals('J'))
                {                                                       //Joint moves' speed values are a percentage 
                    indexOfUnits = lineWithPointInIt.IndexOf('%');
                }
                else if (lineWithPointInIt.ElementAt(indexOfMoveType).Equals('C'))  //skip circulare move lines
                {
                    continue;
                }
                else
                {                                                       //Linear moves' speed values are in mm/sec
                    indexOfUnits = lineWithPointInIt.IndexOf("mm/sec");
                }
                string temp = lineWithPointInIt.Substring(indexOfPoint + 2, indexOfUnits - indexOfPoint - 2);
                double originalSpeed = double.Parse(temp);              //determine the current speed of the move
                newSpeed = originalSpeed * (1.0 + percentChange / 100.0);       //adjust the speed
                string lineWithReplacedSpeed = lineWithPointInIt.Substring(0, indexOfPoint + 2) + (int)newSpeed + lineWithPointInIt.Substring(indexOfUnits);        //recreate the existing program line with a different speed substituted in
                m_programSections[1].Insert(indexOfLine, lineWithReplacedSpeed);
                m_programSections[1].RemoveAt(indexOfLine + 1);                                   //update programe sections with updated program line
            }
        }

        /// <summary>
        /// Creates a new program that is the same as this if the base speed of the program was different
        /// </summary>
        /// <param name="originalBaseSpeed">The base speed of this program</param>
        /// <param name="newBaseSpeed">The base speed of the new program</param>
        /// <returns>A new program that has a different base speed</returns>
        public ProgramAdjuster createCopyBasedOnDifferentBaseSpeed(int originalBaseSpeed, int newBaseSpeed)
        {
            ProgramAdjuster ret = new ProgramAdjuster(this.toByteArray());
            for (int i=0;i<this.m_programSections[1].Count;i++)
            {
                string line = this.m_programSections[1].ElementAt(i);
                int startIndex = line.IndexOf("L P[");
                if (startIndex >= 0)
                {
                    startIndex = line.IndexOf(']', startIndex);
                    int endIndex = line.IndexOf("mm/sec");
                    string speedString = line.Substring(startIndex + 2, endIndex - startIndex - 2);
                    float originalSpeed = float.Parse(speedString);
                    int pointNumber = this.parsePointNumberFromLine(line);
                    float ratio = originalSpeed / originalBaseSpeed;
                    ret.replaceSpeedForPoint(pointNumber, (int)Math.Round(ratio * newBaseSpeed));
                }
            }
            return ret;
        }

        /// <summary>
        /// Changes the speed for an entire range of points
        /// </summary>
        /// <param name="startingPoint">the first point to be adjusted</param>
        /// <param name="endingPoint">the last point that will be adjusted</param>
        /// <param name="newSpeed">the new speed all of these points are being set to</param>
        public void changeSpeedsForRangeOfPoints(int startingPoint, int endingPoint, int newSpeed)
        {
            int largerNumber = Math.Max(startingPoint, endingPoint);                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = Math.Min(startingPoint, endingPoint); i <= largerNumber; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
            {
                replaceSpeedForPoint(i, newSpeed);
            }
        }

        /// <summary>
        /// Changes the speed for an entire range of points by a percentage of each point's individual current speed
        /// </summary>
        /// <param name="startingPoint">the first point to be adjusted</param>
        /// <param name="endingPoint">the last point that will be adjusted</param>
        /// <param name="percentChange">the new speed all of these points are being set to</param>
        public void changeSpeedsForRangeOfPoints(int startingPoint, int endingPoint, double percentChange)
        {
            int largerNumber = Math.Max(startingPoint, endingPoint);                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = Math.Min(startingPoint, endingPoint); i <= largerNumber; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
            {
                adjustSpeedByPercentForPoint(i, percentChange);
            }
        }

        /// <summary>
        /// Changes the speed for all the points in the program
        /// </summary>
        /// <param name="newSpeed">the new speed all of these points are being set to</param>
        public void changeSpeedsForAllPoints(int newSpeed)
        {
            int lastPointNumber = parsePointNumberFromPointString(m_programSections[2].ElementAt(m_programSections[2].Count - 2));      //find the last point of the program
            for (int i = 1; i <= lastPointNumber; i++)                                                                              //go through all points in the program
            {
                replaceSpeedForPoint(i, newSpeed);
            }
        }

        /// <summary>
        /// Changes the speed for all of the points by a percentage of each point's individual current speed
        /// </summary>
        /// <param name="percentChange">the new speed all of these points are being set to</param>
        public void changeSpeedsForAllPoints(double percentChange)
        {
            int lastPointNumber = parsePointNumberFromPointString(m_programSections[2].ElementAt(m_programSections[2].Count - 2));      //find the last point of the program
            for (int i = 1; i <= lastPointNumber; i++)                                                                              //go through all points in the program
            {
                adjustSpeedByPercentForPoint(i, percentChange);
            }
        }

        public void copyPointSpeedsFromOtherProgram(string otherFile)
        { 
            FileInfo otherFileInfo = new FileInfo(otherFile);
            if (otherFileInfo.Exists && otherFileInfo.Extension.Equals(".ls"))
            {
                ProgramAdjuster otherProgram = new ProgramAdjuster(otherFile);
                for (int i=1;i<=this.NumberOfPoints;i++)
                {
                    replaceSpeedForPoint(i, otherProgram.getSpeedForRobotPoint(i));
                }
            }
        }

        public void changeAllProgramSpeedsBasedOnFile(string csvFile)
        {
            using (StreamReader reader = new StreamReader(csvFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] lineSplit = line.Split(',');
                    int pointNumber = int.Parse(lineSplit[0]);
                    if (!lineSplit[1].Equals("") && !lineSplit[1].Equals("#VALUE!"))
                    {
                        double temp = double.Parse(lineSplit[1]);
                        int newSpeed = (int)Math.Round(temp);
                        replaceSpeedForPoint(pointNumber, newSpeed);
                    }
                }
            }
        }

        /// <summary>
        /// Adjust programs speeds, in order to get an even coating, based on previous measurents
        /// </summary>
        /// <param name="numberOfCoatsToApply">how many coats the program we are adjusting will be used for</param>
        /// <param name="targetThickness">the thickness that we are trying to get to on the next series of coats</param>
        /// <param name="minDepositionRate">the minimum rate that the paint being sprayed can go on the part</param>
        /// <param name="maxDepositionRate">the maximum rate that the paint being sprayed can go on the part</param>
        /// <param name="speedChangeWarningThreshold">a number in the the range of 0 to 1, that is a percentage that flags a warning once a speed change goes over that threshold</param>
        /// <param name="pointAdjustLimits">the limits on the speeds that the robot can handle as well the flag for each point on whether the robot point should be adjusted</param>
        /// <param name="allMeasurements">all of the measurements associated with the paint and part that this program is used for</param>
        /// <param name="willOutputDebugFile">optional paramater on whether to create a .csv file that lists all of the changes that this function call has made to the program</param>
        /// <param name="debugFileName">the name of the file the debug information will be placed in, it should end with .csv</param>
        /// <returns>After making the speed adjustment, a flag is returned that indicates various conditions that may have been met in the course of updateing the path's speeds</returns>
        public DynaResultFlag dynaAdjustProgram(int numberOfCoatsToApply, float targetThickness, float minDepositionRate, float maxDepositionRate, float speedChangeWarningPercent, Dictionary<string, PointSpeedLimits> pointAdjustLimits, List<Paint_Iteration> allMeasurements, bool willOutputDebugFile = false, string debugFileName = "DynaCoat Change Log.csv")
        {
            bool hasLargeDepositionRate = false;
            bool hasSmallDepositionRate = false;
            bool speedsHaveBeenClipped = false;
            bool speedChangeIsOverThreshold = false;
            List<PointChangeData> allChanges = new List<PointChangeData>();
            int[] pointTravelOrder = getPointTravelOrder();
            int totalNumberOfSeries = countSeries(allMeasurements);


            #region Make initial adjustments
            List<int> pointsThatHaveHadTheirSpeedAdjusted = new List<int>();
            for (int i = 1;i<pointTravelOrder.Length;i++)
            {
                if (pointAdjustLimits[pointTravelOrder[i].ToString()].CanAdjust)
                {
                    if (totalNumberOfSeries == 1)          //only have one measurement to base next speed on
                    {
                        string precedingMidPointName = pointTravelOrder[i-1] + "_" + pointTravelOrder[i];
                        string pointName = pointTravelOrder[i].ToString();
                        Dictionary<string, RobotPointMeasurement> solitarySeriesMeasurements = allMeasurements[0].Measurement_Series[0].Measurements;
                        RobotPointMeasurement pointThatIsBeingUsed = null;
                        bool paintIsThickEnough = false;
                        if (solitarySeriesMeasurements.ContainsKey(precedingMidPointName) && solitarySeriesMeasurements[precedingMidPointName].PaintThickness > MIN_PAINT_THICKNESS_FOR_CHANGE) //there is a mid-point preceding this point that we can use to make our adjustment
                        {
                            pointThatIsBeingUsed = solitarySeriesMeasurements[precedingMidPointName];
                            paintIsThickEnough = true;
                        }
                        else if (solitarySeriesMeasurements.ContainsKey(pointName) && solitarySeriesMeasurements[pointName].PaintThickness > MIN_PAINT_THICKNESS_FOR_CHANGE)     //no mid-point before this point, just use the point's own measurements to make an adjustment
                        {
                            pointThatIsBeingUsed = solitarySeriesMeasurements[pointName];
                            paintIsThickEnough = true;
                        }
                        if (paintIsThickEnough)
                        {
                            float actualDepositionRate = pointThatIsBeingUsed.PaintThickness / allMeasurements[0].Measurement_Series[0].NumberOfCoats;
                            float targetDepositionRate = (targetThickness - pointThatIsBeingUsed.PaintThickness) / numberOfCoatsToApply;
                            PointChangeData pointChanges = new PointChangeData(pointName, pointThatIsBeingUsed.PaintThickness, actualDepositionRate, targetDepositionRate);
                            if (targetDepositionRate > maxDepositionRate || targetDepositionRate < minDepositionRate)
                            {
                                speedsHaveBeenClipped = true;
                                pointChanges.Note = "Deposition rate limited by paint specifications";
                            }
                            pointChanges.Note += "; Normal Speed Adjust";
                            targetDepositionRate = Math.Max(minDepositionRate, targetDepositionRate);
                            targetDepositionRate = Math.Min(maxDepositionRate, targetDepositionRate);
                            int originalSpeed = getSpeedForRobotPoint(pointTravelOrder[i]);
                            pointChanges.OriginalSpeed = originalSpeed;
                            int newSpeed = (int)Math.Round((actualDepositionRate / targetDepositionRate) * originalSpeed);
                            newSpeed = (int)Math.Round((newSpeed - originalSpeed) * 0.35) + originalSpeed;          //only take 35% of the propesed adjustment on the first iteration
                            pointChanges.AttemptedSpeed = newSpeed;
                            if (newSpeed < pointAdjustLimits[pointName].MinSpeed || newSpeed > pointAdjustLimits[pointName].MaxSpeed)
                            {
                                speedsHaveBeenClipped = true;
                                pointChanges.Note += "; Limited by robot speed";
                            }
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointName].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointName].MaxSpeed);       //can't let the new speed be higher than the max
                            pointChanges.FinalNewSpeed = newSpeed;
                            float depositionRateUsed = actualDepositionRate / ((float)newSpeed / originalSpeed);
                            pointChanges.DepositionRateUsed = depositionRateUsed;
                            allChanges.Add(pointChanges);
                            if (depositionRateUsed > maxDepositionRate)
                                hasLargeDepositionRate = true;
                            else if (depositionRateUsed < minDepositionRate)
                                hasSmallDepositionRate = true;
                            replaceSpeedForPoint(pointTravelOrder[i], newSpeed);
                            pointsThatHaveHadTheirSpeedAdjusted.Add(pointTravelOrder[i]);
                        }
                    }
                    else if (totalNumberOfSeries > 1)                //have multiple measurements to use to base next speed on, will use a best fit line to try to get the correct speed
                    {
                        string precedingMidPointName = pointTravelOrder[i - 1] + "_" + pointTravelOrder[i];
                        string pointName = pointTravelOrder[i].ToString();
                        Paint_Iteration latestIteration = allMeasurements[allMeasurements.Count - 1];
                        Dictionary<string, RobotPointMeasurement> mostRecentSeriesMeasurements = latestIteration.Measurement_Series[latestIteration.Measurement_Series.Count - 1].Measurements;
                        RobotPointMeasurement pointThatIsBeingUsed = null;
                        bool paintIsThickEnough = false;
                        if (mostRecentSeriesMeasurements.ContainsKey(precedingMidPointName) && mostRecentSeriesMeasurements[precedingMidPointName].PaintThickness > MIN_PAINT_THICKNESS_FOR_CHANGE) //there is a mid-point preceding this point that we can use to make our adjustment
                        {
                            pointThatIsBeingUsed = mostRecentSeriesMeasurements[precedingMidPointName];
                            paintIsThickEnough = true;
                        }
                        else if (mostRecentSeriesMeasurements.ContainsKey(pointName) && mostRecentSeriesMeasurements[pointName].PaintThickness > MIN_PAINT_THICKNESS_FOR_CHANGE)     //no mid-point before this point, just use the point's own measurements to make an adjustment
                        {
                            pointThatIsBeingUsed = mostRecentSeriesMeasurements[pointName];
                            paintIsThickEnough = true;
                        }
                        if (paintIsThickEnough)
                        {
                            BestFitLine speedForThicknessPredicter = buildBestFitLine(allMeasurements, pointThatIsBeingUsed.PointNumber);
                            int newSpeed = 0;
                            float currentThicknessAtPoint = getTotalThicknessFromAllSeries(latestIteration.Measurement_Series, pointThatIsBeingUsed.PointNumber);
                            float targetDepositionRate = (targetThickness - currentThicknessAtPoint) / numberOfCoatsToApply;
                            PointChangeData pointChanges = new PointChangeData(pointName, currentThicknessAtPoint, -1, targetDepositionRate);
                            if (targetDepositionRate > maxDepositionRate || targetDepositionRate < minDepositionRate)
                            {
                                speedsHaveBeenClipped = true;
                                pointChanges.Note = "Deposition rate limited by paint specifications";
                            }
                            targetDepositionRate = Math.Max(minDepositionRate, targetDepositionRate);
                            targetDepositionRate = Math.Min(maxDepositionRate, targetDepositionRate);
                            float depositionRateUsed = 0;
                            pointChanges.OriginalSpeed = pointThatIsBeingUsed.Speed;
                            if (speedForThicknessPredicter == null)
                            {
                                pointChanges.Note += "; Normal Speed Adjust";
                                float actualDepositionRate = pointThatIsBeingUsed.PaintThickness / latestIteration.Measurement_Series[latestIteration.Measurement_Series.Count - 1].NumberOfCoats;
                                int nominalSpeed = pointThatIsBeingUsed.Speed;
                                newSpeed = (int)Math.Round((actualDepositionRate / targetDepositionRate) * nominalSpeed);
                                newSpeed = (int)Math.Round((newSpeed - nominalSpeed) * 0.35) + nominalSpeed;          //only take 35% of the propesed adjustment when we don't have a good predictive line
                                depositionRateUsed = actualDepositionRate / ((float)newSpeed / nominalSpeed);
                            }
                            else
                            {
                                pointChanges.Note += "; Fit Line Speed Adjust";
                                newSpeed = (int)Math.Round(speedForThicknessPredicter.findValueOfX(targetDepositionRate));
                                depositionRateUsed = (float)speedForThicknessPredicter.findValueOfY(newSpeed);
                            }
                            if (newSpeed < pointAdjustLimits[pointName].MinSpeed || newSpeed > pointAdjustLimits[pointName].MaxSpeed)
                            {
                                speedsHaveBeenClipped = true;
                                pointChanges.Note += "; Limited by robot speed";
                            }
                            pointChanges.AttemptedSpeed = newSpeed;
                            pointChanges.DepositionRateUsed = depositionRateUsed;
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointName].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointName].MaxSpeed);       //can't let the new speed be higher than the max
                            pointChanges.FinalNewSpeed = newSpeed;
                            allChanges.Add(pointChanges);
                            if (depositionRateUsed > maxDepositionRate)
                                hasLargeDepositionRate = true;
                            else if (depositionRateUsed < minDepositionRate)
                                hasSmallDepositionRate = true;
                            if (!double.IsNaN(newSpeed))
                            {
                                replaceSpeedForPoint(pointTravelOrder[i], newSpeed);
                                pointsThatHaveHadTheirSpeedAdjusted.Add(pointTravelOrder[i]);
                            }
                        }
                    }
                }
            }
            pointsThatHaveHadTheirSpeedAdjusted.Sort();
            #endregion
            //fill in the gaps in the data
            #region make estimations on gaps in data
            List<int> pointTravelOrderList = pointTravelOrder.ToList();
            for (int i = 0; i < pointsThatHaveHadTheirSpeedAdjusted.Count - 1; i++)
            {
                int iteratedPoint = pointsThatHaveHadTheirSpeedAdjusted[i];
                int nextIteratedPoint = pointsThatHaveHadTheirSpeedAdjusted[i + 1];
                int nextPoint = findPointAfter(pointTravelOrder, iteratedPoint);
                int pointBefore = findPointBefore(pointTravelOrder, nextIteratedPoint);
                if (nextPoint != nextIteratedPoint)
                {
                    int numberOfPointsBetween = pointTravelOrderList.IndexOf(nextIteratedPoint) - pointTravelOrderList.IndexOf(iteratedPoint) - 1;
                    int lastSpeedBeforeGap = getSpeedForRobotPoint(iteratedPoint);
                    int firstSpeedAfterGap = getSpeedForRobotPoint(nextIteratedPoint);
                    if (numberOfPointsBetween == 1)
                    {
                        int newSpeed = (lastSpeedBeforeGap + firstSpeedAfterGap) / 2;
                        string pointString = "" + nextPoint;
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            int oldSpeed = getSpeedForRobotPoint(nextPoint);
                            PointChangeData changeData = new PointChangeData(pointString, oldSpeed, newSpeed, newSpeed, "Adjusted by gap fill"); 
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            if (newSpeed != changeData.FinalNewSpeed)
                            {
                                changeData.FinalNewSpeed = newSpeed;
                                changeData.Note += "; Limited by Robot Speed";
                            }
                            allChanges.Add(changeData);
                            //make the change in the program
                            replaceSpeedForPoint(nextPoint, newSpeed);
                        }
                    }
                    else if (numberOfPointsBetween == 2)
                    {
                        int delta = (firstSpeedAfterGap - lastSpeedBeforeGap) / 3;
                        int newSpeed = lastSpeedBeforeGap + delta;
                        string pointString = "" + nextPoint;
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            int oldSpeed = getSpeedForRobotPoint(nextPoint);
                            PointChangeData changeData = new PointChangeData(pointString, oldSpeed, newSpeed, newSpeed, "Adjusted by gap fill");
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            if (newSpeed != changeData.FinalNewSpeed)
                            {
                                changeData.FinalNewSpeed = newSpeed;
                                changeData.Note += "; Limited by Robot Speed";
                            }
                            allChanges.Add(changeData);
                            //make the change in the program
                            replaceSpeedForPoint(nextPoint, newSpeed);
                        }
                        newSpeed = lastSpeedBeforeGap + delta * 2;
                        pointString = "" + pointBefore;
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            int oldSpeed = getSpeedForRobotPoint(pointBefore);
                            PointChangeData changeData = new PointChangeData(pointString, oldSpeed, newSpeed, newSpeed, "Adjusted by gap fill");
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            if (newSpeed != changeData.FinalNewSpeed)
                            {
                                changeData.FinalNewSpeed = newSpeed;
                                changeData.Note += "; Limited by Robot Speed";
                            }
                            allChanges.Add(changeData);
                            //make the change in the program
                            replaceSpeedForPoint(pointBefore, newSpeed);
                        }
                    }
                    else //if (numberOfPointsBetween > 2)
                    {
                        int newSpeed = (lastSpeedBeforeGap + firstSpeedAfterGap) / 2;
                        int transitionSpeed = (lastSpeedBeforeGap + newSpeed) / 2;
                        string pointString = "" + nextPoint;
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            int oldSpeed = getSpeedForRobotPoint(nextPoint);
                            PointChangeData changeData = new PointChangeData(pointString, oldSpeed, transitionSpeed, transitionSpeed, "Adjusted by gap fill");
                            transitionSpeed = Math.Max(transitionSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            transitionSpeed = Math.Min(transitionSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            if (transitionSpeed != changeData.FinalNewSpeed)
                            {
                                changeData.FinalNewSpeed = newSpeed;
                                changeData.Note += "; Limited by Robot Speed";
                            }
                            allChanges.Add(changeData);
                            //make the change in the program
                            replaceSpeedForPoint(nextPoint, transitionSpeed);
                        }
                        transitionSpeed = (firstSpeedAfterGap + newSpeed) / 2;
                        pointString = "" + pointBefore;
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            int oldSpeed = getSpeedForRobotPoint(pointBefore);
                            PointChangeData changeData = new PointChangeData(pointString, oldSpeed, transitionSpeed, transitionSpeed, "Adjusted by gap fill");
                            transitionSpeed = Math.Max(transitionSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            transitionSpeed = Math.Min(transitionSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            if (transitionSpeed != changeData.FinalNewSpeed)
                            {
                                changeData.FinalNewSpeed = newSpeed;
                                changeData.Note += "; Limited by Robot Speed";
                            } 
                            allChanges.Add(changeData);
                            //make the change in the program
                            replaceSpeedForPoint(pointBefore, transitionSpeed);
                        }
                        for (int j = pointTravelOrderList.IndexOf(iteratedPoint) + 2; j <= pointTravelOrderList.IndexOf(nextIteratedPoint) - 2; j++)
                        {
                            newSpeed = (lastSpeedBeforeGap + firstSpeedAfterGap) / 2;
                            pointString = pointTravelOrder[j].ToString();
                            if (pointAdjustLimits[pointString].CanAdjust)
                            {
                                int oldSpeed = getSpeedForRobotPoint(pointTravelOrder[j]);
                                PointChangeData changeData = new PointChangeData(pointString, oldSpeed, newSpeed, newSpeed, "Adjusted by gap fill");
                                newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                                newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                                //update the list of changes
                                if (newSpeed != changeData.FinalNewSpeed)
                                {
                                    changeData.FinalNewSpeed = newSpeed;
                                    changeData.Note += "; Limited by Robot Speed";
                                }
                                allChanges.Add(changeData);
                                //make the change in the program
                                replaceSpeedForPoint(pointTravelOrder[j], newSpeed);
                            }
                        }
                    }
                }
            }
            #endregion
            //adjust the points right before and after all of the points that were adjusted
            #region Make estimated corrections for first and last point
            if (pointsThatHaveHadTheirSpeedAdjusted.Count > 0)
            {
                int firstPointThatWasAdjusted = pointsThatHaveHadTheirSpeedAdjusted[0];
                int oldSpeedPointInsideRun = getSpeedForRobotPoint(firstPointThatWasAdjusted);
                int pointBeforeFirstPoint = findPointBefore(pointTravelOrder, firstPointThatWasAdjusted);
                int oldSpeedPointOutsideRun = getSpeedForRobotPoint(pointBeforeFirstPoint);
                int newSpeedPointOutsideRun = (int)((oldSpeedPointInsideRun - oldSpeedPointOutsideRun) * 0.75) + oldSpeedPointOutsideRun;
                string adjustingPointString = "" + pointBeforeFirstPoint;
                if (pointAdjustLimits[adjustingPointString].CanAdjust)
                {
                    newSpeedPointOutsideRun = Math.Max(newSpeedPointOutsideRun, pointAdjustLimits[adjustingPointString].MinSpeed);       //can't let the new speed be lower than the minimum
                    newSpeedPointOutsideRun = Math.Min(newSpeedPointOutsideRun, pointAdjustLimits[adjustingPointString].MaxSpeed);       //can't let the new speed be higher than the max
                    //update the list of changes
                    int oldSpeed = getSpeedForRobotPoint(pointBeforeFirstPoint);
                    PointChangeData changeData = new PointChangeData(adjustingPointString, oldSpeed, newSpeedPointOutsideRun, newSpeedPointOutsideRun, "First point adjust");
                    allChanges.Add(changeData);
                    //make the change in the program
                    replaceSpeedForPoint(pointBeforeFirstPoint, newSpeedPointOutsideRun);
                }
                int lastPointThatWasAdjusted = pointsThatHaveHadTheirSpeedAdjusted[pointsThatHaveHadTheirSpeedAdjusted.Count - 1];
                oldSpeedPointInsideRun = getSpeedForRobotPoint(lastPointThatWasAdjusted);
                int pointAfterLastPoint = findPointAfter(pointTravelOrder, lastPointThatWasAdjusted);
                oldSpeedPointOutsideRun = getSpeedForRobotPoint(pointAfterLastPoint);
                newSpeedPointOutsideRun = (int)((oldSpeedPointInsideRun - oldSpeedPointOutsideRun) * 0.75) + oldSpeedPointOutsideRun;
                adjustingPointString = "" + pointAfterLastPoint;
                if (pointAdjustLimits[adjustingPointString].CanAdjust)
                {
                    newSpeedPointOutsideRun = Math.Max(newSpeedPointOutsideRun, pointAdjustLimits[adjustingPointString].MinSpeed);       //can't let the new speed be lower than the minimum
                    newSpeedPointOutsideRun = Math.Min(newSpeedPointOutsideRun, pointAdjustLimits[adjustingPointString].MaxSpeed);       //can't let the new speed be higher than the max
                    //update the list of changes
                    int oldSpeed = getSpeedForRobotPoint(pointAfterLastPoint);
                    PointChangeData changeData = new PointChangeData(adjustingPointString, oldSpeed, newSpeedPointOutsideRun, newSpeedPointOutsideRun, "Last point adjust");
                    allChanges.Add(changeData);
                    //make the change in the program
                    replaceSpeedForPoint(pointAfterLastPoint, newSpeedPointOutsideRun);
                }
            }
            #endregion
            //scan through the program looking for outlier speeds for points
            #region Tone down outliers
            for (int i = 1; i < pointTravelOrder.Length - 1; i++)
            {
                int firstPointSpeed = getSpeedForRobotPoint(pointTravelOrder[i-1]);
                int middlePointSpeed = getSpeedForRobotPoint(pointTravelOrder[i]);
                int thirdPointSpeed = getSpeedForRobotPoint(pointTravelOrder[i+1]);
                int firstPointSpeedMargin = firstPointSpeed / 2;
                int thirdPointSpeedMargin = thirdPointSpeed / 2;
                if (firstPointSpeed < thirdPointSpeed)
                {
                    if (middlePointSpeed > (thirdPointSpeed + thirdPointSpeedMargin) || middlePointSpeed < (firstPointSpeed - firstPointSpeedMargin))
                    {
                        int newSpeed = (firstPointSpeed + thirdPointSpeed) / 2;
                        string pointString = pointTravelOrder[i].ToString();
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            updateFinalSpeedInAListPointChangeData(ref allChanges, pointString, newSpeed);
                            replaceSpeedForPoint(pointTravelOrder[i], newSpeed);
                        }
                    }
                }
                else if (firstPointSpeed > thirdPointSpeed)
                {
                    if (middlePointSpeed > (firstPointSpeed + firstPointSpeedMargin) || middlePointSpeed < (thirdPointSpeed - thirdPointSpeedMargin))
                    {
                        int newSpeed = (firstPointSpeed + thirdPointSpeed) / 2;
                        string pointString = pointTravelOrder[i].ToString();
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            newSpeed = Math.Max(newSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            newSpeed = Math.Min(newSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            updateFinalSpeedInAListPointChangeData(ref allChanges, pointString, newSpeed);
                            replaceSpeedForPoint(pointTravelOrder[i], newSpeed);
                        }
                    }
                }
                else         //firstPointSpeed == thirdPointSpeed
                {
                    if (middlePointSpeed > (firstPointSpeed + firstPointSpeedMargin) || middlePointSpeed < (firstPointSpeed - firstPointSpeedMargin))
                    {
                        string pointString = pointTravelOrder[i].ToString();
                        if (pointAdjustLimits[pointString].CanAdjust)
                        {
                            firstPointSpeed = Math.Max(firstPointSpeed, pointAdjustLimits[pointString].MinSpeed);       //can't let the new speed be lower than the minimum
                            firstPointSpeed = Math.Min(firstPointSpeed, pointAdjustLimits[pointString].MaxSpeed);       //can't let the new speed be higher than the max
                            //update the list of changes
                            updateFinalSpeedInAListPointChangeData(ref allChanges, pointString, firstPointSpeed);
                            replaceSpeedForPoint(pointTravelOrder[i], firstPointSpeed);
                        }
                    }
                }
            }
            #endregion
            //check all changes for points that go over threshold
            #region Check Speeds Over Threshold
            foreach (PointChangeData changeData in allChanges)
            {
                int speedChangeDeltaThreshold = (int)Math.Round(changeData.OriginalSpeed * speedChangeWarningPercent);
                int speedChange = Math.Abs(changeData.FinalNewSpeed - changeData.OriginalSpeed);
                if (speedChange > speedChangeDeltaThreshold)
                {
                    speedChangeIsOverThreshold = true;
                    break;
                }
            }
            #endregion

            if (willOutputDebugFile)
            {
                using (TextWriter output = new StreamWriter(debugFileName))
                {
                    allChanges.Sort();
                    output.WriteLine("Point Number, Current Thickness, Current Deposition Rate, Target Deposition Rate, Deposition Rate Used, Original Speed, Proposed Speed Change, Speed Change Made, Notes");
                    foreach (PointChangeData changedPoint in allChanges)
                    {
                        output.WriteLine(changedPoint.PointNumber + ", " + changedPoint.CurrentThicknessAtPoint + ", " + changedPoint.CurrentDepositionRate + ", " + changedPoint.TargetDepositionRate + ", " + changedPoint.DepositionRateUsed + ", " + changedPoint.OriginalSpeed + ", " + changedPoint.AttemptedSpeed + ", " + changedPoint.FinalNewSpeed + ", " + changedPoint.Note);
                    }
                }
            }

            DynaResultFlag result = DynaResultFlag.None;
            if (hasLargeDepositionRate)
                result |= DynaResultFlag.LargeDepositionRatesPresent;
            if (hasSmallDepositionRate)
                result |= DynaResultFlag.SmallDepositionRatesPresent;
            if (speedsHaveBeenClipped)
                result |= DynaResultFlag.SpeedsHaveBeenClipped;
            if (speedChangeIsOverThreshold)
                result |= DynaResultFlag.SpeedChangeOverPercentThreshold;
            return result;
        }

        private static int countSeries(List<Paint_Iteration> list)
        {
            int sum = 0;
            foreach(Paint_Iteration iteration in list)
            {
                sum += iteration.Measurement_Series.Count;
            }
            return sum;
        }

        private static int findPointBefore(int[] travelOrder, int pointNumber)
        {
            for (int i = 1; i < travelOrder.Length; i++)
            {
                if (travelOrder[i] == pointNumber)
                {
                    return travelOrder[i - 1];
                }
            }
            return -1;
        }

        private static int findPointAfter(int[] travelOrder, int pointNumber)
        {
            for (int i = 0; i < travelOrder.Length - 1; i++)
            {
                if (travelOrder[i] == pointNumber)
                {
                    return travelOrder[i + 1];
                }
            }
            return -1;
        }

        private BestFitLine buildBestFitLine(List<Paint_Iteration> allMeasures, string pointName)
        {
            List<Point_2D> allPoints = new List<Point_2D>();
            float minSpeed = float.MaxValue;
            float maxSpeed = float.MinValue;
            foreach (Paint_Iteration iteration in allMeasures)
            {
                foreach (RobotMeasurementSeries series in iteration.Measurement_Series)
                {
                    if (series.Measurements.ContainsKey(pointName))
                    {
                        allPoints.Add(new Point_2D(series.Measurements[pointName].Speed, series.Measurements[pointName].PaintThickness / series.NumberOfCoats));
                        if (series.Measurements[pointName].Speed < minSpeed)
                            minSpeed = series.Measurements[pointName].Speed;
                        if (series.Measurements[pointName].Speed > maxSpeed)
                            maxSpeed = series.Measurements[pointName].Speed;
                    }
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

        private static float getTotalThicknessFromAllSeries(List<RobotMeasurementSeries> allMeasures, string pointName)
        {
            float total = 0;
            foreach (RobotMeasurementSeries series in allMeasures)
            {
                if (series.Measurements.ContainsKey(pointName))
                {
                    total += series.Measurements[pointName].PaintThickness;
                }
            }
            return total;
        }

        public struct PointSpeedLimits
        {
            public bool CanAdjust;
            public int MaxSpeed;
            public int MinSpeed;
        }

        private static void updateFinalSpeedInAListPointChangeData(ref List<PointChangeData> list, string pointNumber, int newSpeed)
        {
            for (int i = 0; i < list.Count; i++)
            {
                PointChangeData temp = list[i];
                if (temp.PointNumber.Equals(pointNumber))
                {
                    temp.FinalNewSpeed = newSpeed;
                    temp.Note += "; Toned down for being an outlier";
                    list.Insert(i, temp);
                    list.RemoveAt(i + 1);
                    return;
                }
            }
        }

        class PointChangeData : IComparable
        {
            public string PointNumber { get; set;}
            public float CurrentThicknessAtPoint { get; set;}
            public float CurrentDepositionRate { get; set; }
            public float TargetDepositionRate { get; set; }
            public float DepositionRateUsed { get; set; }
            public int OriginalSpeed { get; set; }
            public int AttemptedSpeed { get; set; }
            public int FinalNewSpeed { get; set; }
            public string Note { get; set; }

            public PointChangeData(string number, float thickness, float originalDeposition, float targetDeposition)
            {
                this.PointNumber = number;
                this.CurrentThicknessAtPoint = thickness;
                this.CurrentDepositionRate = originalDeposition;
                this.TargetDepositionRate = targetDeposition;
            }

            public PointChangeData(string number, int orignalSpeed, int attemptedSpeed, int finalSpeed, string note)
            {
                this.PointNumber = number;
                this.OriginalSpeed = orignalSpeed;
                this.AttemptedSpeed = attemptedSpeed;
                this.FinalNewSpeed = finalSpeed;
                this.Note = note;
            }

            public int CompareTo(object o)
            {
                PointChangeData other = o as PointChangeData;
                int firstPointIndexOfUnderscore = this.PointNumber.IndexOf('_');
                int firstNumber = 0;
                if (firstPointIndexOfUnderscore >= 0)
                    firstNumber = int.Parse(this.PointNumber.Substring(0, firstPointIndexOfUnderscore));
                else
                    firstNumber = int.Parse(this.PointNumber);
                int secondPointIndexOfUnderscore = other.PointNumber.IndexOf('_');
                int secondNumber = 0;
                if (secondPointIndexOfUnderscore >= 0)
                    secondNumber = int.Parse(other.PointNumber.Substring(0, secondPointIndexOfUnderscore));
                else
                    secondNumber = int.Parse(other.PointNumber);
                if (firstNumber == secondNumber)
                {
                    if (firstPointIndexOfUnderscore >= 0 && secondPointIndexOfUnderscore >= 0)
                    {
                        // shouldn't ever happen
                        return 0;
                    }
                    else if (firstPointIndexOfUnderscore >= 0)
                    {
                        return 1;
                    }
                    else if (secondPointIndexOfUnderscore >= 0)
                    {
                        return -1;
                    }
                    else
                    {
                        //shouldn't ever happen
                        return 0;
                    }
                }
                else
                    return firstNumber - secondNumber;
            }
        }

        [Flags]
        public enum DynaResultFlag
        {
            None = 0,
            LargeDepositionRatesPresent = 1,
            SmallDepositionRatesPresent = 2,
            SpeedsHaveBeenClipped = 4,
            SpeedChangeOverPercentThreshold = 8
        }
        
        #endregion

        #region Change Axis Stuff

        /// <summary>
        /// replaces all of the extended axis values of the modified program with the extended axis values of another .ls file 
        /// </summary>
        /// <param name="otherFile">the full name of the file you are coping extended axis values from</param>
        public void replaceExtendedAxisWithValuesFromAnotherFile(string otherFile)
        {
            FileInfo otherFileInfo = new FileInfo(otherFile);
            if (otherFileInfo.Exists && otherFileInfo.Extension.Equals(".ls"))
            {
                TextReader input = new StreamReader(otherFile);
                string temp = input.ReadLine();
                while (!temp.Equals("/POS"))                //read to point section of other file
                {
                    temp = input.ReadLine();
                }
                temp = input.ReadLine();
                while (!temp.Equals("/END"))
                {
                    int indexOfPoint = temp.IndexOf('[');
                    int indexOfClosingBracket = temp.IndexOf(']');
                    int pointNumberInOtherFile = int.Parse(temp.Substring(indexOfPoint + 1, indexOfClosingBracket - indexOfPoint - 1));
                    int indexOfMatchingPointInProgramBeingAdjusted = searchForPointNumberInPointsSection(pointNumberInOtherFile);
                    if (indexOfMatchingPointInProgramBeingAdjusted >= 0)
                    {                                                   //found a point with the same number in the file being adjusted as the point we are reading in now from other file
                        input.ReadLine();           //group
                        input.ReadLine();           //config
                        input.ReadLine();           //x,y,z
                        input.ReadLine();           //w,p,r
                        temp = input.ReadLine();    //E1,E2,E3
                        int indexOfExtendedAxisValues = temp.IndexOf("E1=");
                        temp = temp.Substring(indexOfExtendedAxisValues) + "\r\n";
                        string matchingPointString = m_programSections[2].ElementAt(indexOfMatchingPointInProgramBeingAdjusted);
                        indexOfExtendedAxisValues = matchingPointString.IndexOf("E1=", indexOfPoint);
                        int indexOfPointEnd = matchingPointString.IndexOf("};", indexOfPoint);
                        string pointsWithReplacedValues = matchingPointString.Substring(0, indexOfExtendedAxisValues) + temp + matchingPointString.Substring(indexOfPointEnd);      //recreate the point's string with extended axis values from other file substituted in
                        m_programSections[2].Insert(indexOfMatchingPointInProgramBeingAdjusted, pointsWithReplacedValues);
                        m_programSections[2].RemoveAt(indexOfMatchingPointInProgramBeingAdjusted + 1);                    //update programe sections with updated program line
                        input.ReadLine();
                        temp = input.ReadLine();
                    }
                    else
                    {                                   //no matching point read onto next point in file
                        input.ReadLine();  //group
                        input.ReadLine();  //config
                        input.ReadLine();  //x,y,z
                        input.ReadLine();  //w,p,r
                        input.ReadLine();  //E1,E2,E3
                        input.ReadLine();  //point close
                        temp = input.ReadLine();
                    }
                }
                input.Close();
            }
        }

        
        /// <summary>
        /// Sets the values of an extended axis as an offset from another axis value for all of the points in the program
        /// </summary>
        /// <param name="offset">the amount of offset</param>
        /// <param name="valueToOffsetFrom">the axis that is being offset from</param>
        /// <param name="extendAxisToOffset">The extended axis that is being offset</param>
        public void setExtendedAxisValuesAsAnOffsetOfPointValue(double offset, char valueToOffsetFrom, string extendAxisToOffset)
        {
            for (int i = 0; i < m_programSections[2].Count; i++)
            {                                   //go through all points in program
                string pointString = m_programSections[2].ElementAt(i);
                if (!pointString.Equals("/END\n"))
                {
                    int startIndexOfValueToOffsetFrom = pointString.IndexOf(valueToOffsetFrom);
                    int endIndexOfValueToOffsetFrom = pointString.IndexOf("mm,", startIndexOfValueToOffsetFrom);
                    string temp = pointString.Substring(startIndexOfValueToOffsetFrom + 3, endIndexOfValueToOffsetFrom - startIndexOfValueToOffsetFrom - 3);        //retrieve string section that holds just the point value of the axis we are offsetting from
                    temp = temp.Trim();
                    double pointValue = double.Parse(temp);                 //parse current value of the axis the extended axis will be offsetting from
                    int startIndexOfExtendedAxis = pointString.IndexOf(extendAxisToOffset);
                    int endIndexOfExtendedAxis = pointString.IndexOf("mm", startIndexOfExtendedAxis);
                    string firstPart = pointString.Substring(0, startIndexOfExtendedAxis + 4);              //retrieve everything in the point string before the extended axis value
                    string secondPart = pointString.Substring(endIndexOfExtendedAxis - 1);                  //retrieve everything in the point string after the extended axis value
                    string pointStringWithReplaceExtendedAxisValues = firstPart + Math.Round(pointValue + offset, 3) + secondPart;      //create string with extended axis value replaced
                    m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(i + 1);                          //update program sections with updated program line
                }
            }
        }

        /// <summary>
        /// Sets the values of an exteneded axis for every point in the program as an offset of the other axis values at that point
        /// </summary>
        /// <param name="offset">the amount of initial offset</param>
        /// <param name="valuesToOffsetFrom">the axes that will be figured into the offset. Y axis values are subtracted. X and Z axis values are added.</param>
        /// <param name="extendAxisToOffset">the extended axis that is being offset</param>
        public void setExtendedAxisValuesAsAnOffsetOfPointValues(double offset, string valuesToOffsetFrom, string extendAxisToOffset)
        {
            char[] axisLetters = valuesToOffsetFrom.ToCharArray();
            for (int i = 0; i < m_programSections[2].Count; i++)
            {                               //go through all points in program
                string pointString = m_programSections[2].ElementAt(i);
                if (!pointString.Equals("/END\r\n"))
                {
                    double pointValue = 0;
                    for (int j = 0; j < axisLetters.Length; j++)
                    {
                        int startIndexOfValueToOffsetFrom = pointString.IndexOf(axisLetters[j]);
                        int endIndexOfValueToOffsetFrom = pointString.IndexOf("mm,", startIndexOfValueToOffsetFrom);
                        string temp = pointString.Substring(startIndexOfValueToOffsetFrom + 3, endIndexOfValueToOffsetFrom - startIndexOfValueToOffsetFrom - 3);        //find axis value of one of the axes being offset from
                        temp = temp.Trim();
                        if (axisLetters[j] == 'Y')
                        {                   //subtract y axis value from running offset
                            pointValue -= double.Parse(temp);
                        }
                        else
                        {                   //add in x and z axes values to running offset
                            pointValue += double.Parse(temp);
                        }
                    }
                    //if ((pointValue + offset) < 0)                this doesn't look right... wait to see if taking this out breaks anything
                    //{
                    //    pointValue = -1.0 * Math.Abs(offset);
                    //}
                    int startIndexOfExtendedAxis = pointString.IndexOf(extendAxisToOffset);
                    int endIndexOfExtendedAxis = pointString.IndexOf("mm", startIndexOfExtendedAxis);
                    string firstPart = pointString.Substring(0, startIndexOfExtendedAxis + 4);          //retrieve everything in the point string before the extended axis value
                    string secondPart = pointString.Substring(endIndexOfExtendedAxis - 1);              //retrieve everything in the point string after the extended axis value
                    string pointStringWithReplaceExtendedAxisValues = firstPart + (Math.Round(pointValue + offset, 3)).ToString("#.000") + secondPart;      //create string with extended axis value replaced
                    m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
        }

        /// <summary>
        /// Sets the values of an exteneded axis for a section of points in the program as an offset of the other axis values at that point
        /// </summary>
        /// <param name="offset">the amount of initial offset</param>
        /// <param name="valuesToOffsetFrom">the axes that will be figured into the offset. Y axis values are subtracted. X and Z axis values are added.</param>
        /// <param name="extendAxisToOffset">the extended axis that is being offset</param>
        /// <param name="startingPoint">the first point in the series of points that will have its extended axis value changed</param>
        /// <param name="endingPoint">the last point in the series of points that will have its extended axis value changed</param>
        public void setExtendedAxisValuesAsAnOffsetOfPointValues(double offset, string valuesToOffsetFrom, string extendAxisToOffset, int startingPoint, int endingPoint)
        {
            char[] axisLetters = valuesToOffsetFrom.ToCharArray();
            int endingIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= endingIndex; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
            {                   //go through all points between first point and last point including the first and last point
                string pointString = m_programSections[2].ElementAt(i);
                if (!pointString.Equals("/END\n"))
                {
                    double pointValue = 0;
                    for (int j = 0; j < axisLetters.Length; j++)
                    {
                        int startIndexOfValueToOffsetFrom = pointString.IndexOf(axisLetters[j]);
                        int endIndexOfValueToOffsetFrom = pointString.IndexOf("mm,", startIndexOfValueToOffsetFrom);
                        string temp = pointString.Substring(startIndexOfValueToOffsetFrom + 3, endIndexOfValueToOffsetFrom - startIndexOfValueToOffsetFrom - 3);         //find axis value of one of the axes being offset from
                        temp = temp.Trim();
                        if (axisLetters[j] == 'Y')
                        {                   //subtract y axis value from running offset
                            pointValue -= double.Parse(temp);
                        }
                        else
                        {                   //add in x and z axes values to running offset
                            pointValue += double.Parse(temp);
                        }
                    }
                    //if ((pointValue + offset) < 0)                this doesn't look right... wait to see if taking this out breaks anything
                    //{
                    //    pointValue = -1.0 * Math.Abs(offset);
                    //}
                    int startIndexOfExtendedAxis = pointString.IndexOf(extendAxisToOffset);
                    int endIndexOfExtendedAxis = pointString.IndexOf("mm", startIndexOfExtendedAxis);
                    string firstPart = pointString.Substring(0, startIndexOfExtendedAxis + 4);              //retrieve everything in the point string before the extended axis value
                    string secondPart = pointString.Substring(endIndexOfExtendedAxis - 1);                  //retrieve everything in the point string after the extended axis value
                    string pointStringWithReplaceExtendedAxisValues = firstPart + (Math.Round(pointValue + offset, 3)).ToString("#.000") + secondPart;      //create string with extended axis value replaced
                    m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(i + 1);                      //update program sections with updated program line
                }
            }
        }

        /// <summary>
        /// Sets the values of an extended axis as an offset from another axis value for a series of points in the program
        /// </summary>
        /// <param name="offset">the amount of offset</param>
        /// <param name="valueToOffsetFrom">the axis that is being offset from</param>
        /// <param name="extendAxisToOffset">The extended axis that is being offset</param>
        /// <param name="startingPoint">the first point in the series of points that will have its extended axis value changed</param>
        /// <param name="endingPoint">the last point in the series of points that will have its extended axis value changed</param>
        public void setExtendedAxisValuesAsAnOffsetOfPointValue(double offset, char valueToOffsetFrom, string extendAxisToOffset, int startingPoint, int endingPoint)
        {
            int endingIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= endingIndex; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
            {                           //go through all points between first point and last point including the first and last point
                string pointString = m_programSections[2].ElementAt(i);
                if (!pointString.Equals("/END\n"))
                {
                    int startIndexOfValueToOffsetFrom = pointString.IndexOf(valueToOffsetFrom);
                    int endIndexOfValueToOffsetFrom = pointString.IndexOf("mm,", startIndexOfValueToOffsetFrom);
                    string temp = pointString.Substring(startIndexOfValueToOffsetFrom + 3, endIndexOfValueToOffsetFrom - startIndexOfValueToOffsetFrom - 3);        //retrieve string section that holds just the point value of the axis we are offsetting from
                    temp = temp.Trim();
                    double pointValue = double.Parse(temp);             //parse current value of the axis the extended axis will be offsetting from
                    int startIndexOfExtendedAxis = pointString.IndexOf(extendAxisToOffset);
                    int endIndexOfExtendedAxis = pointString.IndexOf("mm", startIndexOfExtendedAxis);
                    string firstPart = pointString.Substring(0, startIndexOfExtendedAxis + 4);          //retrieve everything in the point string before the extended axis value
                    string secondPart = pointString.Substring(endIndexOfExtendedAxis - 1);              //retrieve everything in the point string after the extended axis value
                    string pointStringWithReplaceExtendedAxisValues = firstPart + Math.Round(pointValue + offset, 3).ToString("#.000") + secondPart;        //create string with extended axis value replaced
                    m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
        }

        public void setExtendedAxisValuesFromTextfile(string fullFileName)
        {
            int numberOfExtendedAxis = m_axisTypes.Count - 6;
            using (TextReader reader = new StreamReader(fullFileName))
            {
                string line = reader.ReadLine();
                string[] splitLine;
                while (line != null)
                {
                    splitLine = line.Split(',');
                    try
                    {
                        int pointNumber = int.Parse(splitLine[0]);
                        for (int i=1;i<=numberOfExtendedAxis;i++)
                        {
                            double newExtendedAxisValue = double.Parse(splitLine[i]);
                            replaceAxisValue(newExtendedAxisValue, "E" + i, pointNumber);
                        }
                    }
                    catch (FormatException)
                    {
                        //not a point, skip this line
                    }
                    line = reader.ReadLine();
                }
            }
        }

        /// <summary>
        /// add some amount (in mms) to one of the axes for a series of points
        /// </summary>
        /// <param name="additionalValue">the amount being added into the axis</param>
        /// <param name="axis">the axis being added to</param>
        /// <param name="startingPoint">the first point in the series of points that will have its extended axis value changed</param>
        /// <param name="endingPoint">the last point in the series of points that will have its extended axis value changed</param>
        public void addAmountToAxisValue(double additionalValue, string axis, int startingPoint, int endingPoint)
        {
            if (indexOfAxis(axis) < m_axisTypes.Count)
            {
                int endingIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
                if (endingIndex < 0)
                {
                    endingIndex = m_programSections[2].Count - 1;
                }
                for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= endingIndex; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
                {               //go through all points in the series being changed
                    string pointString = m_programSections[2].ElementAt(i);
                    if (!pointString.Equals("/END\n"))
                    {
                        int startIndexOfValueToAddTo = pointString.IndexOf(axis);
                        if (startIndexOfValueToAddTo >= 0)
                        {
                            int endIndexOfValueToAddTo;
                            if (m_axisTypes[indexOfAxis(axis)] == AxisType.LINEAR)
                            {
                                endIndexOfValueToAddTo = pointString.IndexOf("mm", startIndexOfValueToAddTo);
                            }
                            else
                            {
                                endIndexOfValueToAddTo = pointString.IndexOf("deg", startIndexOfValueToAddTo);
                            }
                            string temp = pointString.Substring(startIndexOfValueToAddTo + 3, endIndexOfValueToAddTo - startIndexOfValueToAddTo - 3);           //retrieve string section that holds just the point value of the axis that is haveing something added to it
                            temp = temp.Trim();
                            double pointValue = double.Parse(temp);                 //parse current value of the axis the extended axis will be offsetting from
                            string firstPart = pointString.Substring(0, startIndexOfValueToAddTo + 3);          //retrieve everything in the point string before the axis value
                            string secondPart = pointString.Substring(endIndexOfValueToAddTo - 1);              //retrieve everything in the point string after the axis value
                            string pointStringWithReplaceExtendedAxisValues = string.Format("{0}{1,9} {2}", firstPart, Math.Round(pointValue + additionalValue, 3).ToString("#.000"), secondPart);      //create string with extended axis value replaced with the new value that is rounded to three digits and is right justified with 9 digits of width 
                            m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                            m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                        }
                    }
                }
            }
        }

        /// <summary>
        /// add some amount (in mms) to one of the axes for all of the points
        /// </summary>
        /// <param name="additionalValue">the amount being added into the axis</param>
        /// <param name="axis">the axis being added to</param>
        public void addAmountToAxisValue(double additionalValue, string axis)
        {
            if (indexOfAxis(axis) < m_axisTypes.Count)
            {
                for (int i = 0; i <= m_programSections[2].Count - 1; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
                {               //go through all points in the series being changed
                    string pointString = m_programSections[2].ElementAt(i);
                    if (!pointString.Equals("/END\n"))
                    {
                        int startIndexOfValueToAddTo = pointString.IndexOf(axis);
                        if (startIndexOfValueToAddTo >= 0)
                        {
                            int endIndexOfValueToAddTo;
                            if (m_axisTypes[indexOfAxis(axis)] == AxisType.LINEAR)
                            {
                                endIndexOfValueToAddTo = pointString.IndexOf("mm", startIndexOfValueToAddTo);
                            }
                            else
                            {
                                endIndexOfValueToAddTo = pointString.IndexOf("deg", startIndexOfValueToAddTo);
                            }
                            string temp = pointString.Substring(startIndexOfValueToAddTo + 3, endIndexOfValueToAddTo - startIndexOfValueToAddTo - 3);           //retrieve string section that holds just the point value of the axis that is haveing something added to it
                            temp = temp.Trim();
                            double pointValue = double.Parse(temp);                 //parse current value of the axis the extended axis will be offsetting from
                            string firstPart = pointString.Substring(0, startIndexOfValueToAddTo + 4);          //retrieve everything in the point string before the axis value
                            string secondPart = pointString.Substring(endIndexOfValueToAddTo - 1);              //retrieve everything in the point string after the axis value
                            string pointStringWithReplaceExtendedAxisValues = string.Format("{0}{1,9} {2}", firstPart, Math.Round(pointValue + additionalValue, 3).ToString("#.000"), secondPart);      //create string with extended axis value replaced with the new value that is rounded to three digits and is right justified with 9 digits of width 
                            m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                            m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add some amount (in mms) to one of the axes for a series points. This was made with rail extended axes in mind. 
        /// This function will affect the points outside of the range of points to be changed by adding value to the points before in increasing 100mm increments.
        /// The points after the series will step down in value added from the additional value in 100mm increments
        /// </summary>
        /// <param name="additionalValue">the amount being added to the axis</param>
        /// <param name="axis">the axis that is having value added to it</param>
        /// <param name="startingPoint">the first point in the series of points that will have its extended axis value changed</param>
        /// <param name="endingPoint">the last point in the series of points that will have its extended axis value changed</param>
        public void addAmountToAxisValueWithTransitions(double additionalValue, string axis, int startingPoint, int endingPoint)
        {
            int endTransitionIndex = (int)(Math.Abs(additionalValue) / 100) - 1;
            for (int i = 0; i < endTransitionIndex; i++)
            {               //go through all points in the transition area before the series of points being changed
                int pointSectionIndex = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint) - endTransitionIndex + i);
                string pointString = m_programSections[2].ElementAt(pointSectionIndex);
                if (!pointString.Equals("/END\n"))
                {
                    int startIndexOfValueToAddTo = pointString.IndexOf(axis);
                    int endIndexOfValueToAddTo = pointString.IndexOf("mm", startIndexOfValueToAddTo);
                    string temp = pointString.Substring(startIndexOfValueToAddTo + 3, endIndexOfValueToAddTo - startIndexOfValueToAddTo - 3);       //retrieve string section that holds just the point value of the axis that is haveing something added to it
                    temp = temp.Trim();
                    double pointValue = double.Parse(temp);                 //parse current value of the extended axis
                    string firstPart = pointString.Substring(0, startIndexOfValueToAddTo + 5);          //retrieve everything in the point string before the axis value
                    string secondPart = pointString.Substring(endIndexOfValueToAddTo - 2);              //retrieve everything in the point string after the axis value
                    string pointStringWithReplaceExtendedAxisValues = firstPart + Math.Round(pointValue + (additionalValue / 100 * (i + 1)), 3).ToString("#.000") + secondPart;     //create string with extended axis value replaced with the new value (some multiple of 100 added in based on how far this point is from the main points being changed)
                    m_programSections[2].Insert(pointSectionIndex, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(pointSectionIndex + 1);          //update program sections with updated program line
                }
            }
            int endingIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= endingIndex; i++)      //make sure the loop starts from the smaller number in counts up to the larger number
            {               //go through all points in the series being changed
                string pointString = m_programSections[2].ElementAt(i);
                if (!pointString.Equals("/END\n"))
                {
                    int startIndexOfValueToAddTo = pointString.IndexOf(axis);
                    int endIndexOfValueToAddTo = pointString.IndexOf("mm", startIndexOfValueToAddTo);
                    string temp = pointString.Substring(startIndexOfValueToAddTo + 3, endIndexOfValueToAddTo - startIndexOfValueToAddTo - 3);       //retrieve string section that holds just the point value of the axis that is haveing something added to it
                    temp = temp.Trim();
                    double pointValue = double.Parse(temp);                 //parse current value of the extended axis
                    string firstPart = pointString.Substring(0, startIndexOfValueToAddTo + 5);          //retrieve everything in the point string before the axis value
                    string secondPart = pointString.Substring(endIndexOfValueToAddTo - 2);              //retrieve everything in the point string after the axis value
                    string pointStringWithReplaceExtendedAxisValues = string.Format("{0}{1,9} {2}", firstPart, Math.Round(pointValue + additionalValue, 3).ToString("#.000"), secondPart);      //create string with extended axis value replaced with the new value that is rounded to three digits and is right justified with 9 digits of width 
                    m_programSections[2].Insert(i, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(i + 1);                          //update program sections with updated program line
                }
            }
            for (int i = endTransitionIndex; i > 0; i--)
            {               //go through all points in the transition area after the series of points being changed
                int pointSectionIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint) + endTransitionIndex - i + 1);
                string pointString = m_programSections[2].ElementAt(pointSectionIndex);
                if (!pointString.Equals("/END\n"))
                {
                    int startIndexOfValueToAddTo = pointString.IndexOf(axis);
                    int endIndexOfValueToAddTo = pointString.IndexOf("mm", startIndexOfValueToAddTo);
                    string temp = pointString.Substring(startIndexOfValueToAddTo + 3, endIndexOfValueToAddTo - startIndexOfValueToAddTo - 3);       //retrieve string section that holds just the point value of the axis that is haveing something added to it
                    temp = temp.Trim();
                    double pointValue = double.Parse(temp);             //parse current value of the extended axis
                    string firstPart = pointString.Substring(0, startIndexOfValueToAddTo + 5);          //retrieve everything in the point string before the axis value
                    string secondPart = pointString.Substring(endIndexOfValueToAddTo - 2);              //retrieve everything in the point string after the axis value
                    string pointStringWithReplaceExtendedAxisValues = firstPart + Math.Round(pointValue + (additionalValue / 100 * (i - 1)), 3).ToString("#.000") + secondPart;     //create string with extended axis value replaced with the new value (some multiple of 100 added in based on how far this point is from the main points being changed)
                    m_programSections[2].Insert(pointSectionIndex, pointStringWithReplaceExtendedAxisValues);
                    m_programSections[2].RemoveAt(pointSectionIndex + 1);      //update program sections with updated program line
                }
            }
        }

        public void replaceAxisValuesForAllPoints(double value, string axis)
        {
            for (int i = 1; i <= NumberOfPoints; i++)
            {
                replaceAxisValue(value, axis, i);
            }
        }

        public void removeAxis(string axis)
        {
            if (axis.Equals("E1"))
            {
                for (int i=0;i<m_programSections[2].Count - 1;i++)
                {
                    string pointString = m_programSections[2].ElementAt(i);
                    int indexOfAxis = pointString.IndexOf(axis);
                    if (indexOfAxis >= 0)
                    {
                        int indexOfNextAxis = pointString.IndexOf("E2");
                        int indexOfEnd = pointString.IndexOf("};");
                        if (indexOfNextAxis >= 0)
                        {
                            string newPointString = pointString.Substring(0, indexOfAxis) + pointString.Substring(indexOfNextAxis);
                            newPointString = newPointString.Replace("E2", "E1");
                            newPointString = newPointString.Replace("E3", "E2");
                            m_programSections[2].Insert(i, newPointString);
                            m_programSections[2].RemoveAt(i + 1);
                        }
                        else if (indexOfEnd >= 0)
                        {
                            string newPointString = pointString.Substring(0, indexOfAxis) + pointString.Substring(indexOfEnd);
                            m_programSections[2].Insert(i, newPointString);
                            m_programSections[2].RemoveAt(i + 1);
                        }
                    }
                }
            }
            else if (axis.Equals("E2"))
            {
                for (int i = 0; i < m_programSections[2].Count - 1; i++)
                {
                    string pointString = m_programSections[2].ElementAt(i);
                    int indexOfAxis = pointString.IndexOf(axis);
                    if (indexOfAxis >= 0)
                    {
                        int indexOfNextAxis = pointString.IndexOf("E3");
                        int indexOfEnd = pointString.IndexOf("};");
                        if (indexOfNextAxis >= 0)
                        {
                            string newPointString = pointString.Substring(0, indexOfAxis) + pointString.Substring(indexOfNextAxis);
                            newPointString = newPointString.Replace("E3", "E2");
                            m_programSections[2].Insert(i, newPointString);
                            m_programSections[2].RemoveAt(i + 1);
                        }
                        else if (indexOfEnd >= 0)
                        {
                            string newPointString = pointString.Substring(0, indexOfAxis) + pointString.Substring(indexOfEnd);
                            m_programSections[2].Insert(i, newPointString);
                            m_programSections[2].RemoveAt(i + 1);
                        }
                    }
                }
            }
            else if (axis.Equals("E3"))
            {
                for (int i = 0; i < m_programSections[2].Count - 1; i++)
                {
                    string pointString = m_programSections[2].ElementAt(i);
                    int indexOfAxis = pointString.IndexOf(axis);
                    if (indexOfAxis >= 0)
                    {
                        int indexOfEnd = pointString.IndexOf("};");
                        if (indexOfEnd >= 0)
                        {
                            string newPointString = pointString.Substring(0, indexOfAxis) + pointString.Substring(indexOfEnd);
                            m_programSections[2].Insert(i, newPointString);
                            m_programSections[2].RemoveAt(i + 1);
                        }
                    }
                }
            }
        }

        #endregion

        #region Change User/Tool Frame Stuff

        /// <summary>
        /// changes the tool frame of the entire program
        /// </summary>
        /// <param name="newToolFrameNumber">the new tool frame number</param>
        public void changeToolFrameWhileFixingTCPInPlace(int newToolFrameNumber)
        {
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfToolNumber = pointString.IndexOf("UT :");
                int indexOfComma = pointString.IndexOf(',', indexOfToolNumber);
                string firstPart = pointString.Substring(0, indexOfToolNumber + 5);
                string secondPart = pointString.Substring(indexOfComma);
                string pointStringWithReplacedToolNumber = firstPart + newToolFrameNumber + secondPart;             //recreate the point's string with the tool frame number replaced with the new value
                m_programSections[2].Insert(i, pointStringWithReplacedToolNumber);
                m_programSections[2].RemoveAt(i + 1);                                         //update programe sections with updated program line
            }
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string line = m_programSections[1].ElementAt(i);                  //go through the program lines and change any active tool asignments to assign to the correct tool number
                if (line.Contains("UTOOL_NUM"))
                { 
                    int indexOfEqualSign = line.IndexOf('=');
                    string updatedLine = line.Substring(0, indexOfEqualSign + 1) + newToolFrameNumber + " ;\r\n";
                    m_programSections[1].Insert(i, updatedLine);
                    m_programSections[1].RemoveAt(i + 1);
                }
            }
        }

        /// <summary>
        /// changes the tool number for a range of points
        /// </summary>
        /// <param name="startingPoint">the first point to have its tool number changed</param>
        /// <param name="endingPoint">the final point to have its tool number changed</param>
        /// <param name="newToolFrameNumber">the new tool number</param>
        public void changeToolFrameWhileFixingTCPInPlace(int startingPoint, int endingPoint, int newToolFrameNumber)
        {
            int endingIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= endingIndex; i++)      //make sure the loop starts from the smaller number and counts up to the larger number
            {
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfToolNumber = pointString.IndexOf("UT :");
                int indexOfComma = pointString.IndexOf(',', indexOfToolNumber);
                string firstPart = pointString.Substring(0, indexOfToolNumber + 5);
                string secondPart = pointString.Substring(indexOfComma);
                string pointStringWithReplacedToolNumber = firstPart + newToolFrameNumber + secondPart;                     //recreate the point's string with the tool frame number replaced with the new value
                m_programSections[2].Insert(i, pointStringWithReplacedToolNumber);
                m_programSections[2].RemoveAt(i + 1);                             //update program sections with updated program line
            }
            int indexOfFirstPoint = 0;
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string tempString = m_programSections[1].ElementAt(i);
                int index = tempString.IndexOf("P[" + Math.Min(startingPoint, endingPoint) + "]");
                if (index >= 0)
                {
                    indexOfFirstPoint = i;                                //locate the line with the point number we are looking for
                    break;
                }
            }
            for (int i = indexOfFirstPoint; i > 0; i--)
            {
                string line = m_programSections[1].ElementAt(i);                  //go through the program lines and change the active tool asignments closest to the points that were changed to assign to the correct tool number
                if (line.Contains("UTOOL_NUM"))
                {
                    int indexOfEqualSign = line.IndexOf('=');
                    string updatedLine = line.Substring(0, indexOfEqualSign + 1) + newToolFrameNumber + " ;\r\n";
                    m_programSections[1].Insert(i, updatedLine);
                    m_programSections[1].RemoveAt(i + 1);                  //update program sections with updated program line
                    break;
                }
            }
        }

        /// <summary>
        /// Changes the userframe of program without converting the positional data 
        /// (converting the positional data would keep all of the points in the same position in real 3d space)
        /// </summary>
        /// <param name="newFrameNumber">the new user frame number</param>
        public void changeUserFrameWithOutConvertingPositionData(int newFrameNumber)
        {
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {       //go through all of the points in the program
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfFrameNumber = pointString.IndexOf("UF :");           //find the index of the part in the point string where the user frame is listed
                if (indexOfFrameNumber > 0)
                {
                    int indexOfComma = pointString.IndexOf(',', indexOfFrameNumber);            //find index of comma between user frame and user tool
                    string updatedString = pointString.Substring(0, indexOfFrameNumber + 5) + newFrameNumber + pointString.Substring(indexOfComma);       //create the string with the userframe replaced with the new user frame
                    m_programSections[2].Insert(i, updatedString);
                    m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
            for (int i = 0; i < m_programSections[1].Count; i++)
            {       //go through all of the program lines in the program
                string pointString = m_programSections[1].ElementAt(i);
                int indexOfUframe = pointString.IndexOf("UFRAME_NUM");
                if (indexOfUframe > 0)
                {
                    int indexOfEqualSign = pointString.IndexOf('=', indexOfUframe);
                    string updatedString = pointString.Substring(0, indexOfEqualSign + 1) + newFrameNumber + " ;\r\n";     //replace the active user frame number of the assignment instruction with a new active user frame number
                    m_programSections[1].Insert(i, updatedString);
                    m_programSections[1].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
        }

        public void changeUserFrameWithPositionDataConverted(int newFrameNumber, Matrix oldFrame, Matrix newFrame)
        {
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {       //go through all of the points in the program
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfFrameNumber = pointString.IndexOf("UF :");           //find the index of the part in the point string where the user frame is listed
                if (indexOfFrameNumber > 0)
                {
                    string updatedString = pointString.Substring(0, indexOfFrameNumber + 5) + newFrameNumber + pointString.Substring(indexOfFrameNumber + 6);       //create the string with the userframe replaced with the new user frame
                    double x = parseAxisValueFromPointString(pointString, "X");         //parse all of the values out of the point string
                    double y = parseAxisValueFromPointString(pointString, "Y");
                    double z = parseAxisValueFromPointString(pointString, "Z");
                    double w = parseAxisValueFromPointString(pointString, "W");
                    double p = parseAxisValueFromPointString(pointString, "P");
                    double r = parseAxisValueFromPointString(pointString, "R");
                    Matrix point = new Matrix(x, y, z, w, p, r, "ZYX", true);             //create the transformation matrix
                    Matrix convertedPoint = newFrame.findPointOfCorrespondingCoordinateSystem(oldFrame, point);
                    double[] angles = convertedPoint.parseZYXRotations();
                    //create the section of the point string that holds all of the X,Y,Z,W,P,R values
                    string newString = String.Format("X = {0,9}  mm, Y = {1,9}  mm, Z = {2,9}  mm,\n\tW = {3,9} deg, P = {4,9} deg, R = {5,9} deg", Math.Round(convertedPoint.get(0, 3), 3).ToString("#0.000"), Math.Round(convertedPoint.get(1, 3), 3).ToString("#0.000"), Math.Round(convertedPoint.get(2, 3), 3).ToString("#0.000"), Math.Round(angles[0], 3).ToString("#0.000"), Math.Round(angles[1], 3).ToString("#0.000"), Math.Round(angles[2], 3).ToString("#0.000"));
                    int indexStart = updatedString.IndexOf("X =");                //find the beginning of the part of the point string that will be replaced
                    int otherIndex = updatedString.IndexOf("R =");
                    int indexEnd = updatedString.IndexOf("deg", otherIndex);      //find the end of the part of the point string that will be replaced
                    m_programSections[2].Insert(i, updatedString.Substring(0, indexStart) + newString + updatedString.Substring(indexEnd + 3));       //add the point string with the replaced values back into the program
                    m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
            for (int i = 0; i < m_programSections[1].Count; i++)
            {       //go through all of the program lines in the program to update the lines where the active user frame is changed
                string pointString = m_programSections[1].ElementAt(i);
                int indexOfUframe = pointString.IndexOf("UFRAME_NUM");
                if (indexOfUframe > 0)
                {
                    int indexOfEqualSign = pointString.IndexOf('=', indexOfUframe);
                    string updatedString = pointString.Substring(0, indexOfEqualSign + 1) + newFrameNumber + pointString.Substring(indexOfEqualSign + 2);     //replace the active user frame number of the assignment instruction with a new active user frame number
                    m_programSections[1].Insert(i, updatedString);
                    m_programSections[1].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
        }

        public void changeUserFrameWithPositionDataConverted(int startingPoint, int endingPoint, int newFrameNumber, Matrix oldFrame, Matrix newFrame)
        {
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {       //go through all of the points in the program
                string pointString = m_programSections[2].ElementAt(i);
                int pointNumber = parsePointNumberFromPointString(pointString);
                if (pointNumber >= startingPoint && pointNumber <= endingPoint)
                {
                    int indexOfFrameNumber = pointString.IndexOf("UF :");           //find the index of the part in the point string where the user frame is listed
                    if (indexOfFrameNumber > 0)
                    {
                        string updatedString = pointString.Substring(0, indexOfFrameNumber + 5) + newFrameNumber + pointString.Substring(indexOfFrameNumber + 6);       //create the string with the userframe replaced with the new user frame
                        double x = parseAxisValueFromPointString(pointString, "X");         //parse all of the values out of the point string
                        double y = parseAxisValueFromPointString(pointString, "Y");
                        double z = parseAxisValueFromPointString(pointString, "Z");
                        double w = parseAxisValueFromPointString(pointString, "W");
                        double p = parseAxisValueFromPointString(pointString, "P");
                        double r = parseAxisValueFromPointString(pointString, "R");
                        Matrix point = new Matrix(x, y, z, w, p, r, "ZYX", true);             //create the transformation matrix
                        Matrix convertedPoint = newFrame.findPointOfCorrespondingCoordinateSystem(oldFrame, point);
                        double[] angles = convertedPoint.parseZYXRotations();
                        //create the section of the point string that holds all of the X,Y,Z,W,P,R values
                        string newString = String.Format("X = {0,9}  mm, Y = {1,9}  mm, Z = {2,9}  mm,\n\tW = {3,9} deg, P = {4,9} deg, R = {5,9} deg", Math.Round(convertedPoint.get(0, 3), 3).ToString("#0.000"), Math.Round(convertedPoint.get(1, 3), 3).ToString("#0.000"), Math.Round(convertedPoint.get(2, 3), 3).ToString("#0.000"), Math.Round(angles[0], 3).ToString("#0.000"), Math.Round(angles[1], 3).ToString("#0.000"), Math.Round(angles[2], 3).ToString("#0.000"));
                        int indexStart = updatedString.IndexOf("X =");                //find the beginning of the part of the point string that will be replaced
                        int otherIndex = updatedString.IndexOf("R =");
                        int indexEnd = updatedString.IndexOf("deg", otherIndex);      //find the end of the part of the point string that will be replaced
                        m_programSections[2].Insert(i, updatedString.Substring(0, indexStart) + newString + updatedString.Substring(indexEnd + 3));       //add the point string with the replaced values back into the program
                        m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                    }
                }
            }
            for (int i = 0; i < m_programSections[1].Count; i++)
            {       //go through all of the program lines in the program to update the lines where the active user frame is changed
                string pointString = m_programSections[1].ElementAt(i);
                int indexOfUframe = pointString.IndexOf("UFRAME_NUM");
                if (indexOfUframe > 0)
                {
                    int indexOfEqualSign = pointString.IndexOf('=', indexOfUframe);
                    string updatedString = pointString.Substring(0, indexOfEqualSign + 1) + newFrameNumber + pointString.Substring(indexOfEqualSign + 2);     //replace the active user frame number of the assignment instruction with a new active user frame number
                    m_programSections[1].Insert(i, updatedString);
                    m_programSections[1].RemoveAt(i + 1);                  //update program sections with updated program line
                }
            }
        }

        /// <summary>
        /// Changes the userframe of program without converting the positional data for a range of points
        /// (converting the positional data would keep all of the points in the same position in real 3d space)
        /// </summary>
        /// <param name="startingPoint">the first point to have its user frame changed</param>
        /// <param name="endingPoint">the last point to have its user frame changed</param>
        /// <param name="newFrameNumber">the new user frame number</param>
        public void changeUserFrameWithOutConvertingPositionData(int startingPoint, int endingPoint, int newFrameNumber)
        {
            int endingIndex = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                        //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= endingIndex; i++)      //make sure the loop starts from the smaller number and counts up to the larger number
            {
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfUserNumber = pointString.IndexOf("UF :");
                int indexOfComma = pointString.IndexOf(',', indexOfUserNumber);            //find index of comma between user frame and user tool   
                string firstPart = pointString.Substring(0, indexOfUserNumber + 5);
                string secondPart = pointString.Substring(indexOfComma);
                string pointStringWithReplacedUserNumber = firstPart + newFrameNumber + secondPart;                     //recreate the point's string with the user frame number replaced with the new value
                m_programSections[2].Insert(i, pointStringWithReplacedUserNumber);
                m_programSections[2].RemoveAt(i + 1);                             //update program sections with updated program line
            }
            int indexOfFirstPoint = 0;
            for (int i = 0; i < m_programSections[1].Count; i++)
            {
                string tempString = m_programSections[1].ElementAt(i);
                int index = tempString.IndexOf("P[" + Math.Min(startingPoint, endingPoint) + "]");
                if (index >= 0)
                {
                    indexOfFirstPoint = i;                                //locate the line with the point number we are looking for
                    break;
                }
            }
            for (int i = indexOfFirstPoint; i > 0; i--)
            {
                string line = m_programSections[1].ElementAt(i);                  //go through the program lines and change the active user frame asignments, closest to the points, that were changed to assign to the correct user number
                if (line.Contains("UFRAME_NUM"))
                {
                    int indexOfEqualSign = line.IndexOf('=');
                    string updatedLine = line.Substring(0, indexOfEqualSign + 1) + newFrameNumber + " ;\r\n";
                    m_programSections[1].Insert(i, updatedLine);
                    m_programSections[1].RemoveAt(i + 1);                  //update program sections with updated program line
                    break;
                }
            }
        }

        #endregion

        #region Change Configuration

        /// <summary>
        /// changes the configuration of all of the points in the program
        /// </summary>
        /// <param name="flip_UnFlip">the flip/unflipped state of the new configuration</param>
        /// <param name="up_Down">the up/down state of the arm of the new configuration</param>
        /// <param name="front_Back">the front/back state of the new configuration</param>
        /// <param name="axis4Rotation">the number of rotations on axis 4</param>
        /// <param name="axis5Rotation">the number of rotations on axis 5</param>
        /// <param name="axis6Rotation">the number of rotations on axis 6</param>
        public void changeConfiguration(char flip_UnFlip, char up_Down, char front_Back, int axis4Rotation, int axis5Rotation, int axis6Rotation)
        {
            if ((flip_UnFlip == 'f' || flip_UnFlip == 'F' || flip_UnFlip == 'n' || flip_UnFlip == 'N') && (up_Down == 'u' || up_Down == 'U' || up_Down == 'd' || up_Down == 'D') && (front_Back == 't' || front_Back == 'T' || front_Back == 'b' || front_Back == 'B') && axis4Rotation < 4 && axis4Rotation > -4 && axis5Rotation < 4 && axis5Rotation > -4 && axis6Rotation < 4 && axis6Rotation > -4)
            {               //if the arguments are lowercase letters replace that letter with the uppercase letter
                if (flip_UnFlip == 'f')
                {
                    flip_UnFlip = 'F';
                }
                if (flip_UnFlip == 'n')
                {
                    flip_UnFlip = 'N';
                }
                if (up_Down == 'u')
                {
                    up_Down = 'U';
                }
                if (up_Down == 'd')
                {
                    up_Down = 'D';
                }
                if (front_Back == 't')
                {
                    front_Back = 'T';
                }
                if (front_Back == 'b')
                {
                    front_Back = 'B';
                }
                for (int i=0;i<m_programSections[2].Count-1;i++)
                {   //go through all points in the program
                    string pointString = m_programSections[2].ElementAt(i);
                    int indexOfConfig = pointString.IndexOf("CONFIG");          //locate the configuration section of the point string
                    if (indexOfConfig > 0)
                    {
                        int indexOfEndConfig = pointString.IndexOf('\'', indexOfConfig + 10);
                        string updatedString = pointString.Substring(0, indexOfConfig + 10) + flip_UnFlip + ' ' + up_Down + ' ' + front_Back + ", " + axis4Rotation + ", " + axis5Rotation + ", " +axis6Rotation + pointString.Substring(indexOfEndConfig);       //recreate the point string with the values of the arguments replacing the values in the configuration section
                        m_programSections[2].Insert(i, updatedString);
                        m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                    }
                }
            }
        }

        /// <summary>
        /// change the configuratino of the robot for a range of points
        /// </summary>
        /// <param name="flip_UnFlip">the flip/unflipped state of the new configuration</param>
        /// <param name="up_Down">the up/down state of the arm of the new configuration</param>
        /// <param name="front_Back">the front/back state of the new configuration</param>
        /// <param name="axis4Rotation">the number of rotations on axis 4</param>
        /// <param name="axis5Rotation">the number of rotations on axis 5</param>
        /// <param name="axis6Rotation">the number of rotations on axis 6</param>
        /// <param name="startingPoint">the first point to have its configuration changed</param>
        /// <param name="endingPoint">the last point to have its configuration changed</param>
        public void changeConfiguration(char flip_UnFlip, char up_Down, char front_Back, int axis4Rotation, int axis5Rotation, int axis6Rotation, int startingPoint, int endingPoint)
        {
            if ((flip_UnFlip == 'f' || flip_UnFlip == 'F' || flip_UnFlip == 'n' || flip_UnFlip == 'N') && (up_Down == 'u' || up_Down == 'U' || up_Down == 'd' || up_Down == 'D') && (front_Back == 't' || front_Back == 'T' || front_Back == 'b' || front_Back == 'B') && axis4Rotation < 4 && axis4Rotation > -4 && axis5Rotation < 4 && axis5Rotation > -4 && axis6Rotation < 4 && axis6Rotation > -4)
            {                           //if the arguments are lowercase letters replace that letter with the uppercase letter
                if (flip_UnFlip == 'f')
                {
                    flip_UnFlip = 'F';
                }
                if (flip_UnFlip == 'n')
                {
                    flip_UnFlip = 'N';
                }
                if (up_Down == 'u')
                {
                    up_Down = 'U';
                }
                if (up_Down == 'd')
                {
                    up_Down = 'D';
                }
                if (front_Back == 't')
                {
                    front_Back = 'T';
                }
                if (front_Back == 'b')
                {
                    front_Back = 'B';
                }
                int finalPoint = searchForPointNumberInPointsSection(Math.Max(startingPoint, endingPoint));                         //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
                for (int i = searchForPointNumberInPointsSection(Math.Min(startingPoint, endingPoint)); i <= finalPoint; i++)       //make sure the loop starts from the smaller number and counts up to the larger number
                {               //go through the points in the range
                    string pointString = m_programSections[2].ElementAt(i);
                    int indexOfConfig = pointString.IndexOf("CONFIG");              //locate the configuration section of the point string
                    if (indexOfConfig > 0)
                    {
                        int indexOfEndConfig = pointString.IndexOf('\'', indexOfConfig + 10);
                        string updatedString = pointString.Substring(0, indexOfConfig + 10) + flip_UnFlip + ' ' + up_Down + ' ' + front_Back + ", " + axis4Rotation + ", " + axis5Rotation + ", " + axis6Rotation + pointString.Substring(indexOfEndConfig);       //recreate the point string with the values of the arguments replacing the values in the configuration section
                        m_programSections[2].Insert(i, updatedString);
                        m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
                    }
                }
            }
        }

        public void addExtendedAxis(string name, string units)
        {
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            { 
                string pointString = m_programSections[2].ElementAt(i);
                int index = pointString.LastIndexOf("deg");
                string updatedString = pointString.Substring(0, index + 3) + ", \r\n\t" + name + "=     0.000  mm" + pointString.Substring(index + 3);
                m_programSections[2].Insert(i, updatedString);
                m_programSections[2].RemoveAt(i + 1);                  //update program sections with updated program line
            }
        }

        #endregion

        #region Add Points

        public void addPointsToMakeDistanceBetweenPointsAMinimum(double maximumDistanceBetweenPoints)
        {
            int nextNewPointNumber = NumberOfPoints + 1;
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string firstLine = m_programSections[1].ElementAt(i);
                int indexOfFirstPoint = firstLine.IndexOf("P[");
                if (indexOfFirstPoint >= 0)
                {
                    int indexOfCloseBracket = firstLine.IndexOf(']');
                    string temp = firstLine.Substring(indexOfFirstPoint + 2, indexOfCloseBracket - indexOfFirstPoint - 2);
                    int firstPointNumber = int.Parse(temp);
                    int secondPointNumber = -1;
                    int j = i + 1;
                    for (; j < m_programSections[1].Count - 1; j++)
                    {
                        string secondLine = m_programSections[1].ElementAt(j);
                        int indexOfSecondPoint = secondLine.IndexOf("P[");
                        if (indexOfSecondPoint >= 0)
                        {
                            indexOfCloseBracket = secondLine.IndexOf(']');
                            temp = secondLine.Substring(indexOfSecondPoint + 2, indexOfCloseBracket - indexOfSecondPoint - 2);
                            secondPointNumber = int.Parse(temp);
                            break;
                        }
                    }

                    if (secondPointNumber > 0)
                    {
                        Matrix firstPointMatrix = turnPointIntoTransformationMatrix(firstPointNumber);
                        Matrix secondPointMatrix = turnPointIntoTransformationMatrix(secondPointNumber);
                        Vector firstPointVector = firstPointMatrix.turnPointMatrixInto3x1Vector();
                        Vector secondPointVector = secondPointMatrix.turnPointMatrixInto3x1Vector();
                        double distanceBetweenPoints = (secondPointVector.subtract(firstPointVector)).magnitudeAll();
                        if (distanceBetweenPoints > maximumDistanceBetweenPoints)
                        {
                            Matrix newPoint = (firstPointMatrix.add(secondPointMatrix)).multiply(0.5);
                            int indexOfFirstPointInPointSection = searchForPointNumberInPointsSection(firstPointNumber);
                            string newPointString = m_programSections[2].ElementAt(indexOfFirstPointInPointSection);
                            indexOfCloseBracket = newPointString.IndexOf(']');
                            newPointString = newPointString.Substring(0, 2) + nextNewPointNumber + newPointString.Substring(indexOfCloseBracket);
                            m_programSections[2].Insert(m_programSections[2].Count - 1, newPointString);
                            replacePointValuesWithThoseOfAMatrix(nextNewPointNumber, newPoint);
                            if (m_axisTypes.Count > 6)
                            {
                                double firstRailValue = getAxisValueForPoint("E1", firstPointNumber);
                                double secondRailValue = getAxisValueForPoint("E1", secondPointNumber);
                                replaceAxisValue((firstRailValue + secondRailValue) / 2, "E1", nextNewPointNumber);
                            }
                            string newLine = m_programSections[1].ElementAt(j);         //get the line instructions from the next point
                            int indexOfOpenBracket = newLine.IndexOf('[');
                            indexOfCloseBracket = newLine.IndexOf(']');
                            newLine = newLine.Substring(0, indexOfOpenBracket + 1) + nextNewPointNumber + newLine.Substring(indexOfCloseBracket);
                            m_programSections[1].Insert(i + 1, newLine);
                            nextNewPointNumber++;
                            i--;
                        }
                    }
                }
            }
        }

        public void addPointsToMakeDistanceBetweenPointsAMinimum(double maximumDistanceBetweenPoints, int startingPoint, int endingPoint)
        {
            int nextNewPointNumber = NumberOfPoints + 1;
            int originalNumberOfPoints = nextNewPointNumber;
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string firstLine = m_programSections[1].ElementAt(i);
                int indexOfFirstPoint = firstLine.IndexOf("P[");
                if (indexOfFirstPoint >= 0)
                {
                    int indexOfCloseBracket = firstLine.IndexOf(']');
                    string temp = firstLine.Substring(indexOfFirstPoint + 2, indexOfCloseBracket - indexOfFirstPoint - 2);
                    int firstPointNumber = int.Parse(temp);
                    if (firstPointNumber < startingPoint)
                    {
                        continue;
                    }
                    int secondPointNumber = -1;
                    int j = i + 1;
                    for (; j < m_programSections[1].Count - 1; j++)
                    {
                        string secondLine = m_programSections[1].ElementAt(j);
                        int indexOfSecondPoint = secondLine.IndexOf("P[");
                        if (indexOfSecondPoint >= 0)
                        {
                            indexOfCloseBracket = secondLine.IndexOf(']');
                            temp = secondLine.Substring(indexOfSecondPoint + 2, indexOfCloseBracket - indexOfSecondPoint - 2);
                            secondPointNumber = int.Parse(temp);
                            break;
                        }
                    }
                    if (secondPointNumber > endingPoint && secondPointNumber < originalNumberOfPoints)
                    {
                        continue;
                    }


                    if (secondPointNumber > 0)
                    {
                        Matrix firstPointMatrix = turnPointIntoTransformationMatrix(firstPointNumber);
                        Matrix secondPointMatrix = turnPointIntoTransformationMatrix(secondPointNumber);
                        Vector firstPointVector = firstPointMatrix.turnPointMatrixInto3x1Vector();
                        Vector secondPointVector = secondPointMatrix.turnPointMatrixInto3x1Vector();
                        double distanceBetweenPoints = (secondPointVector.subtract(firstPointVector)).magnitudeAll();
                        if (distanceBetweenPoints > maximumDistanceBetweenPoints)
                        {
                            Matrix newPoint = (firstPointMatrix.add(secondPointMatrix)).multiply(0.5);
                            int indexOfFirstPointInPointSection = searchForPointNumberInPointsSection(firstPointNumber);
                            string newPointString = m_programSections[2].ElementAt(indexOfFirstPointInPointSection);
                            indexOfCloseBracket = newPointString.IndexOf(']');
                            newPointString = newPointString.Substring(0, 2) + nextNewPointNumber + newPointString.Substring(indexOfCloseBracket);
                            m_programSections[2].Insert(m_programSections[2].Count - 1, newPointString);
                            replacePointValuesWithThoseOfAMatrix(nextNewPointNumber, newPoint);
                            if (m_axisTypes.Count > 6)
                            {
                                double firstRailValue = getAxisValueForPoint("E1", firstPointNumber);
                                double secondRailValue = getAxisValueForPoint("E1", secondPointNumber);
                                replaceAxisValue((firstRailValue + secondRailValue) / 2, "E1", nextNewPointNumber);
                            }
                            string newLine = m_programSections[1].ElementAt(j);         //get the line instructions from the next point
                            int indexOfOpenBracket = newLine.IndexOf('[');
                            indexOfCloseBracket = newLine.IndexOf(']');
                            newLine = newLine.Substring(0, indexOfOpenBracket + 1) + nextNewPointNumber + newLine.Substring(indexOfCloseBracket);
                            m_programSections[1].Insert(i + 1, newLine);
                            nextNewPointNumber++;
                            i--;
                        }
                    }
                }
            }
        }

        public void addInMidPointsToProgram()
        {
            int newPointNumber = 5001;
            for (int i = 0; i < m_programSections[1].Count - 1; i++)      //go through program adding midpoints
            {
                string line = m_programSections[1].ElementAt(i);
                if (line.Contains("P["))
                {
                    int firstPointNumber = parsePointNumberFromLine(line);
                    int secondPointNumber = 0;
                    int secondPointLineIndex = -1;
                    for (int j = i + 1; j < m_programSections[1].Count - 1; j++)
                    {
                        string secondLine = m_programSections[1].ElementAt(j);
                        if (secondLine.Contains("P["))
                        {
                            secondPointNumber = parsePointNumberFromLine(secondLine);
                            secondPointLineIndex = j;
                            break;
                        }
                    }
                    if (secondPointNumber != 0 && !isPointInJointRepresentation(firstPointNumber) && !isPointInJointRepresentation(secondPointNumber)) //there is a point later than the one we are currently at
                    {
                        Matrix firstPointMatrix = turnPointIntoTransformationMatrix(firstPointNumber);
                        Matrix secondPointMatrix = turnPointIntoTransformationMatrix(secondPointNumber);
                        Matrix midPoint = (firstPointMatrix.add(secondPointMatrix)).multiply(0.5);
                        int indexOfColon = line.IndexOf(':');
                        int currentLineNumber = int.Parse(line.Substring(0, indexOfColon).Trim());
                        string newLine = m_programSections[1].ElementAt(secondPointLineIndex);
                        int indexOfOpenBracket = newLine.IndexOf('[');
                        int indexOfCloseBracket = newLine.IndexOf(']');
                        newLine = newLine.Substring(0, indexOfOpenBracket + 1) + newPointNumber + ": " + firstPointNumber + "_" + secondPointNumber + newLine.Substring(indexOfCloseBracket);
                        m_programSections[1].Insert(i + 1, newLine);
                        string newPointString = m_programSections[2].ElementAt(searchForPointNumberInPointsSection(secondPointNumber));
                        indexOfOpenBracket = newPointString.IndexOf('[');
                        indexOfCloseBracket = newPointString.IndexOf(']');
                        newPointString = newPointString.Substring(0, indexOfOpenBracket + 1) + newPointNumber + ":\"" + firstPointNumber + "_" + secondPointNumber + "\"" + newPointString.Substring(indexOfCloseBracket);
                        m_programSections[2].Insert(m_programSections[2].Count - 1, newPointString);
                        replacePointValuesWithThoseOfAMatrix(newPointNumber, midPoint);
                        if (m_axisTypes.Count >= 7)
                        {
                            double p1E1Value = getAxisValueForPoint("E1", firstPointNumber);
                            double p2E1Value = getAxisValueForPoint("E1", secondPointNumber);
                            replaceAxisValue((p1E1Value + p2E1Value) / 2, "E1", newPointNumber);
                        }
                        if (m_axisTypes.Count >= 8)
                        {
                            double p1E2Value = getAxisValueForPoint("E2", firstPointNumber);
                            double p2E2Value = getAxisValueForPoint("E2", secondPointNumber);
                            replaceAxisValue((p1E2Value + p2E2Value) / 2, "E2", newPointNumber);
                        }
                        if (m_axisTypes.Count >= 9)
                        {
                            double p1E3Value = getAxisValueForPoint("E3", firstPointNumber);
                            double p2E3Value = getAxisValueForPoint("E3", secondPointNumber);
                            replaceAxisValue((p1E3Value + p2E3Value) / 2, "E3", newPointNumber);
                        }
                        i++;
                        newPointNumber++;
                    }
                }
            }
            int lineNumber = 1;             //go through the program section and reset all of the line numbers
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                int indexOfColon = line.IndexOf(':');
                int thisLineNumber = int.Parse(line.Substring(0, indexOfColon).Trim());
                if (thisLineNumber != lineNumber)
                {
                    line = String.Format("{0,4}", lineNumber++) + line.Substring(indexOfColon);
                    m_programSections[1].RemoveAt(i);
                    m_programSections[1].Insert(i, line);
                }
            }
        }

        #endregion

        #region Delete Points

        public void deleteRangeOfPoints(int startingPointNumber, int endingPointNumber)
        {
            //first delete references in program lines
            for (int i=0;i<m_programSections[1].Count - 1;i++)
            {
                int point = parsePointNumberFromLine(m_programSections[1].ElementAt(i));
                if (point <= endingPointNumber && point >= startingPointNumber)
                {
                    m_programSections[1].RemoveAt(i);
                    i--;
                }
            }
            //then delete point positional data
            for (int i=0;i<m_programSections[2].Count - 1;i++)
            {
                int point = parsePointNumberFromPointString(m_programSections[2].ElementAt(i));
                if (point <= endingPointNumber && point >= startingPointNumber)
                {
                    m_programSections[2].RemoveAt(i);
                    i--;
                }
            }
        }

        #endregion

        #region Adjust By Transformation Matrix Section

        /// <summary>
        /// Adjusts a point by turning the specified point into a 4x4 matrix that corresponds with that point in the userspace
        /// Then post-multiplies that matrix (which is representative of the point) by the transformation matrix.
        /// In laymen's terms the transformation matrix will cause the point to translate to a new location based on values in the transformation matrix travelling in the point's own coordinate system,
        /// then that point's cooridate system will be rotated about according to the values in the transformation matrix.
        /// The resultant 4x4 matrix then has its X,Y,Z,W,P, and R values parsed out. Those values are then used to everwrite the original values for that point.
        /// </summary>
        /// <param name="pointNumber">The point that is being changed</param>
        /// <param name="transform">The 4x4 transformation matrix that will transform the point.</param>
        public void adjustPointByTransformationMatrix(int pointNumber, Matrix transform)
        {
            if (!isPointInJointRepresentation(pointNumber))
            {
                int pointNumberInProgram = searchForPointNumberInPointsSection(pointNumber);
                int[] matrixSize = transform.size();
                if (pointNumberInProgram >= 0 && matrixSize[0] == 4 && matrixSize[1] == 4)
                {
                    string pointString = m_programSections[2].ElementAt(pointNumberInProgram);        //retrieve the point string for the specified point
                    double x = parseAxisValueFromPointString(pointString, "X");                     //parse all of the values out of the point string
                    double y = parseAxisValueFromPointString(pointString, "Y");
                    double z = parseAxisValueFromPointString(pointString, "Z");
                    double w = parseAxisValueFromPointString(pointString, "W");
                    double p = parseAxisValueFromPointString(pointString, "P");
                    double r = parseAxisValueFromPointString(pointString, "R");
                    Matrix pointMatrix = new Matrix(x, y, z, w, p, r, "ZYX", true);                 //create the matrix for the original point values
                    Matrix newPointPosition = pointMatrix.multiply(transform);                      //transform the point by the transformation matrix
                    double[] newAngles = newPointPosition.parseZYXRotations();                      //parse out the angles of the new point in the order the robot will need them
                    //create the section of the point string that holds all of the X,Y,Z,W,P,R values
                    string newString = String.Format("X = {0,9}  mm, \tY = {1,9}  mm, \tZ = {2,9}  mm,\n\tW = {3,9} deg, \tP = {4,9} deg, \tR = {5,9} deg", Math.Round(newPointPosition.get(0, 3), 3).ToString("#0.000"), Math.Round(newPointPosition.get(1, 3), 3).ToString("#0.000"), Math.Round(newPointPosition.get(2, 3), 3).ToString("#0.000"), Math.Round(newAngles[0], 3).ToString("#0.000"), Math.Round(newAngles[1], 3).ToString("#0.000"), Math.Round(newAngles[2], 3).ToString("#0.000"));
                    int indexStart = pointString.IndexOf("X =");                //find the beginning of the part of the point string that will be replaced
                    int otherIndex = pointString.IndexOf("R =");
                    int indexEnd = pointString.IndexOf("deg", otherIndex);      //find the end of the part of the point string that will be replaced
                    m_programSections[2].Insert(pointNumberInProgram, pointString.Substring(0, indexStart) + newString + pointString.Substring(indexEnd + 3));        //add the point string with the replaced values back into the program
                    m_programSections[2].RemoveAt(pointNumberInProgram + 1);                   //remove the old point string
                }
            }
        }

        /// <summary>
        /// Adjusts a range of points by turning each point in the range into a 4x4 matrix that corresponds with that point in the userspace
        /// Then post-multiplies that matrix (which is representative of the point) by the transformation matrix.
        /// The resultant 4x4 matrix then has its X,Y,Z,W,P, and R values parsed out. Those values are then used to everwrite the original values for that point.
        /// </summary>
        /// <param name="startingPoint">the first point being transformed</param>
        /// <param name="endingPoint">the last point being transformed</param>
        /// <param name="transform">The 4x4 transformation matrix that will transform the point.</param>
        public void adjustRangeOfPointsByTransformationMatrix(int startingPoint, int endingPoint, Matrix transform)
        {
            int largerPoint = Math.Max(startingPoint, endingPoint);                         //in case arguments were passed in reverse i.e. startingPoint = 5 && endingPoint = 1
            for (int i = Math.Min(startingPoint, endingPoint); i <= largerPoint; i++)       //make sure the loop starts from the smaller number and counts up to the larger number
            {
                adjustPointByTransformationMatrix(i, transform);
            }
        }

        /// <summary>
        /// Addjust all fo the points in a program by turning each point into a 4x4 matrix that corresponds with that point in the userspace.
        /// Then post-multiplies that matrix (which is representative of the point) by the transformation matrix.
        /// The resultant 4x4 matrix then has its X,Y,Z,W,P, and R values parsed out. Those values are then used to everwrite the original values for that point.
        /// </summary>
        /// <param name="transform"></param>
        public void adjustAllPointsByTransformationMatrix(Matrix transform)
        {
            int[] matrixSize = transform.size();
            if (matrixSize[0] == 4 && matrixSize[1] == 4)
            {
                for (int i = 0; i < m_programSections[2].Count - 1; i++)
                {               //go through all the points in the program
                    string pointString = m_programSections[2].ElementAt(i);                       //retrieve the point string
                    double x = parseAxisValueFromPointString(pointString, "X");                 //parse all of the values out of the point string
                    double y = parseAxisValueFromPointString(pointString, "Y");
                    double z = parseAxisValueFromPointString(pointString, "Z");
                    double w = parseAxisValueFromPointString(pointString, "W");
                    double p = parseAxisValueFromPointString(pointString, "P");
                    double r = parseAxisValueFromPointString(pointString, "R");
                    Matrix pointMatrix = new Matrix(x, y, z, w, p, r, "ZYX", true);             //create the matrix for the original point values
                    Matrix newPointPosition = pointMatrix.multiply(transform);                  //transform the point by the transformation matrix
                    double[] newAngles = newPointPosition.parseZYXRotations();                  //parse out the angles of the new point in the order the robot will need them
                    //create the section of the point string that holds all of the X,Y,Z,W,P,R values
                    string newString = String.Format("X = {0,9}  mm, Y = {1,9}  mm, Z = {2,9}  mm,\n\tW = {3,9} deg, P = {4,9} deg, R = {5,9} deg", Math.Round(newPointPosition.get(0, 3), 3).ToString("#.000"), Math.Round(newPointPosition.get(1, 3), 3).ToString("#.000"), Math.Round(newPointPosition.get(2, 3), 3).ToString("#.000"), Math.Round(newAngles[0], 3).ToString("#.000"), Math.Round(newAngles[1], 3).ToString("#.000"), Math.Round(newAngles[2], 3).ToString("#.000"));
                    int indexStart = pointString.IndexOf("X =");                //find the beginning of the part of the point string that will be replaced
                    int otherIndex = pointString.IndexOf("R =");
                    int indexEnd = pointString.IndexOf("deg", otherIndex);      //find the end of the part of the point string that will be replaced
                    m_programSections[2].Insert(i, pointString.Substring(0, indexStart) + newString + pointString.Substring(indexEnd + 3));       //add the point string with the replaced values back into the program
                    m_programSections[2].RemoveAt(i + 1);                          //remove the old point string
                }
            }
        }

        public void adjustAllPointsForCorrection(Matrix correction)
        {
            int[] matrixSize = correction.size();
            if (matrixSize[0] == 4 && matrixSize[1] == 4)
            {
                for (int i = 0; i < m_programSections[2].Count - 1; i++)
                {               //go through all the points in the program
                    string pointString = m_programSections[2].ElementAt(i);                       //retrieve the point string
                    double x = parseAxisValueFromPointString(pointString, "X");                 //parse all of the values out of the point string
                    double y = parseAxisValueFromPointString(pointString, "Y");
                    double z = parseAxisValueFromPointString(pointString, "Z");
                    double w = parseAxisValueFromPointString(pointString, "W");
                    double p = parseAxisValueFromPointString(pointString, "P");
                    double r = parseAxisValueFromPointString(pointString, "R");
                    Matrix pointMatrix = new Matrix(x, y, z, w, p, r, "ZYX", true);             //create the matrix for the original point values
                    Matrix newPointPosition = correction.multiply(pointMatrix);                  //transform the point by the transformation matrix
                    double[] newAngles = newPointPosition.parseZYXRotations();                  //parse out the angles of the new point in the order the robot will need them
                    //create the section of the point string that holds all of the X,Y,Z,W,P,R values
                    string newString = String.Format("X = {0,9}  mm, Y = {1,9}  mm, Z = {2,9}  mm,\n\tW = {3,9} deg, P = {4,9} deg, R = {5,9} deg", Math.Round(newPointPosition.get(0, 3), 3).ToString("#.000"), Math.Round(newPointPosition.get(1, 3), 3).ToString("#.000"), Math.Round(newPointPosition.get(2, 3), 3).ToString("#.000"), Math.Round(newAngles[0], 3).ToString("#.000"), Math.Round(newAngles[1], 3).ToString("#.000"), Math.Round(newAngles[2], 3).ToString("#.000"));
                    int indexStart = pointString.IndexOf("X =");                //find the beginning of the part of the point string that will be replaced
                    int otherIndex = pointString.IndexOf("R =");
                    int indexEnd = pointString.IndexOf("deg", otherIndex);      //find the end of the part of the point string that will be replaced
                    m_programSections[2].Insert(i, pointString.Substring(0, indexStart) + newString + pointString.Substring(indexEnd + 3));       //add the point string with the replaced values back into the program
                    m_programSections[2].RemoveAt(i + 1);                          //remove the old point string
                }
            }
        }

        #endregion

        #region Helper Functions

        public DateTime getModifiedDate()
        {
            for (int i=0;i<m_programSections[0].Count;i++)
            {
                string headerLine = m_programSections[0].ElementAt(i);
                int indexOfEquals = headerLine.IndexOf('=');
                if (indexOfEquals >= 0)
                {
                    string attribute = headerLine.Substring(0, indexOfEquals - 1).Trim();
                    if (attribute.Equals("MODIFIED"))
                    {
                        int indexOfDate = headerLine.IndexOf("DATE");
                        int indexOfTime = headerLine.IndexOf("TIME");
                        string dateString = headerLine.Substring(indexOfDate + 5, 8);
                        string timeString = headerLine.Substring(indexOfTime + 5, 8);
                        int year = int.Parse(dateString.Substring(0, 2)) + 2000;
                        int month = int.Parse(dateString.Substring(3, 2));
                        int day = int.Parse(dateString.Substring(6, 2));
                        int hour = int.Parse(timeString.Substring(0, 2));
                        int minute = int.Parse(timeString.Substring(3, 2));
                        int second = int.Parse(timeString.Substring(6, 2));
                        return new DateTime(year, month, day, hour, minute, second);
                    }
                }
            }
            return new DateTime();
        }

        public List<string> getAllCalledPrograms()
        {
            List<string> ret = new List<string>();
            for (int i=0;i<m_programSections[1].Count;i++)
            {
                string programLine = m_programSections[1].ElementAt(i);
                int indexOfCallStatement = programLine.IndexOf("CALL");
                if (indexOfCallStatement >= 0)
                {
                    int indexOfSemiColon = programLine.IndexOf(';');
                    string program = programLine.Substring(indexOfCallStatement + 5, indexOfSemiColon - indexOfCallStatement - 5).Trim();
                    ret.Add(program);
                }
            }
            return ret;
        }

        public int parsePointNumberFromLine(string line)
        {
            if (line.Contains("P["))
            {
                int indexOfOpenBracket = line.IndexOf('[');
                int indexOfCloseBracket = line.IndexOf(']', indexOfOpenBracket);
                line = line.Substring(indexOfOpenBracket + 1, indexOfCloseBracket - indexOfOpenBracket - 1);
                int indexOfColon = line.IndexOf(':');
                if (indexOfColon >= 0)
                {
                    return int.Parse(line.Substring(0, indexOfColon));
                }
                else
                {
                    return int.Parse(line);
                }
            }
            else
                return -1;
        }

        public string replacePointNumberInLine(string line, int newNumber)
        {
            if (line.Contains("P[") || line.Contains("PR["))
            {
                int indexOfOpenBracket = line.IndexOf('[');
                int indexOfCloseBracket = line.IndexOf(']', indexOfOpenBracket);
                int indexOfColon = line.IndexOf(':', indexOfOpenBracket);
                string firstPart = line.Substring(0, indexOfOpenBracket + 1);
                string secondPart = "";
                if (indexOfColon >= 0 && indexOfColon < indexOfCloseBracket)
                {
                    secondPart = line.Substring(indexOfColon);
                }
                else
                {
                    secondPart = line.Substring(indexOfCloseBracket);
                }
                return firstPart + newNumber + secondPart;
            }
            else
                return "";
        }

        public void createTextFileForPointDataAddingPointMidPoints(string fileName)
        {
            using (TextWriter output = new StreamWriter(fileName))
            {
                string line;
                int[] pointTravelOrder = getPointTravelOrder();
                for (int i = 1; i < pointTravelOrder.Length; i++)
                {
                    Matrix firstPointMatrix = turnPointIntoTransformationMatrix(pointTravelOrder[i-1]);
                    Matrix secondPointMatrix = turnPointIntoTransformationMatrix(pointTravelOrder[i]);
                    Vector firstPointVector = firstPointMatrix.turnPointMatrixInto3x1Vector();
                    Vector secondPointVector = secondPointMatrix.turnPointMatrixInto3x1Vector();
                    Matrix midPoint = (firstPointMatrix.add(secondPointMatrix)).multiply(0.5);
                    Vector midPointVector = midPoint.turnPointMatrixInto3x1Vector();
                    line = String.Format("p{0},{1},{2},{3}", pointTravelOrder[i - 1], firstPointVector.get(0).ToString("#0.000"), firstPointVector.get(1).ToString("#0.000"), firstPointVector.get(2).ToString("#0.000"));
                    output.WriteLine(line);
                    line = String.Format("p{0}_{1},{2},{3},{4}", pointTravelOrder[i - 1], pointTravelOrder[i], midPointVector.get(0).ToString("#0.000"), midPointVector.get(1).ToString("#0.000"), midPointVector.get(2).ToString("#0.000"));
                    output.WriteLine(line);
                }
                Matrix lastPointMatrix = turnPointIntoTransformationMatrix(pointTravelOrder[pointTravelOrder.Length -1]);
                Vector lastPointVector = lastPointMatrix.turnPointMatrixInto3x1Vector();
                line = String.Format("p{0},{1},{2},{3}", (pointTravelOrder[pointTravelOrder.Length - 1]), lastPointVector.get(0).ToString("#0.000"), lastPointVector.get(1).ToString("#0.000"), lastPointVector.get(2).ToString("#0.000"));
                output.WriteLine(line);
            }
        }

        public void createTextFileforPointData(string fileName)
        {
            using (TextWriter output = new StreamWriter(fileName))
            {
                List<Robot_Point> robotPoints = exportPoints();
                for (int i=0;i<robotPoints.Count;i++)
                {
                    int indexOfOpenQuote = robotPoints[i].PointNumber.IndexOf('"');
                    if (indexOfOpenQuote >= 0)
                    {
                        int indexOfCloseQuote = robotPoints[i].PointNumber.IndexOf('"', indexOfOpenQuote + 1);
                        output.WriteLine(robotPoints[i].PointNumber.Substring(indexOfOpenQuote + 1, indexOfCloseQuote - indexOfOpenQuote - 1) + ", " + robotPoints[i].Point.X + ", " + robotPoints[i].Point.Y + ", " + robotPoints[i].Point.Z);
                    }
                    else
                    {
                        output.WriteLine(robotPoints[i].PointNumber + ", " + robotPoints[i].Point.X + ", " + robotPoints[i].Point.Y + ", " + robotPoints[i].Point.Z);
                    }
                }
            }
        }

        public void createTextFileForPointDataWithRailValues(string fileName)
        {
            using (TextWriter output = new StreamWriter(fileName))
            {
                output.Write("Point Number,X,Y,Z");
                if (m_axisTypes.Count > 6)
                    output.Write(",E1");
                if (m_axisTypes.Count > 7)
                    output.Write(",E2");
                if (m_axisTypes.Count > 8)
                    output.Write(",E3");
                output.WriteLine("");
                for (int i = 0;i<m_programSections[2].Count - 1 ;i++)
                {
                    string pointString = m_programSections[2].ElementAt(i);
                    if (!isPointInJointRepresentation(pointString))
                    {
                        output.Write(parsePointNumberFromPointString(pointString) + "," + parseAxisValueFromPointString(pointString, "X") + "," + parseAxisValueFromPointString(pointString, "Y") + "," + parseAxisValueFromPointString(pointString, "Z"));
                        if (m_axisTypes.Count > 6)
                            output.Write("," + parseAxisValueFromPointString(pointString, "E1"));
                        if (m_axisTypes.Count > 7)
                            output.Write("," + parseAxisValueFromPointString(pointString, "E2"));
                        if (m_axisTypes.Count > 8)
                            output.Write("," + parseAxisValueFromPointString(pointString, "E3"));
                        output.WriteLine("");
                    }
                }
            }
        }

        public void createTextFileForPointSpeeds(string fileName)
        {
            using (TextWriter output = new StreamWriter(fileName))
            {
                string line;
                for (int i = 0; i < m_programSections[1].Count - 1; i++)
                {
                    line = m_programSections[1].ElementAt(i);
                    if (line.Contains("P[") || line.Contains("PR["))
                    {
                        int pointNumber = parsePointNumberFromLine(line);
                        int indexOfCloseingBracket = line.IndexOf(']') + 2;
                        int indexOfMoveType = line.IndexOf(':') + 1;
                        int indexOfUnits;
                        if (line.ElementAt(indexOfMoveType).Equals('J'))
                        {                                               //Joint moves' speed values are a percentage 
                            indexOfUnits = line.IndexOf('%');
                        }
                        else
                        {                                               //Linear moves' speed values are in mm/sec
                            indexOfUnits = line.IndexOf("mm/sec");
                        }
                        string currentSpeed = line.Substring(indexOfCloseingBracket, indexOfUnits - indexOfCloseingBracket).Trim();
                        output.WriteLine(pointNumber + "," + currentSpeed);
                    }
                }
            }
        }

        private bool isPointInJointRepresentation(int pointNumber)
        {
            int point = searchForPointNumberInPointsSection(pointNumber);
            string pointString = m_programSections[2].ElementAt(point);           //retrieve the point string for the correct point
            if (pointString.Contains("J1") && pointString.Contains("J2") && pointString.Contains("J3") && pointString.Contains("J4") && pointString.Contains("J5") && pointString.Contains("J6"))
                return true;
            else
                return false;
        }

        private bool isPointInJointRepresentation(string pointString)
        {
            if (pointString.Contains("J1") && pointString.Contains("J2") && pointString.Contains("J3") && pointString.Contains("J4") && pointString.Contains("J5") && pointString.Contains("J6"))
                return true;
            else
                return false;
        }

        public void renumberPoints()
        {
            int nextPointNumber = 1;                //the counter for what point number is next to be used in the program
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {                           //go through the program code section in order to determine the number order of the points
                string line = m_programSections[1].ElementAt(i);
                int indexOfPointNumber = line.IndexOf("P[");
                if (indexOfPointNumber >= 0)
                {
                    int indexOfClosePointBracket = line.IndexOf(']');
                    int pointNumber = int.Parse(line.Substring(indexOfPointNumber + 2, indexOfClosePointBracket - indexOfPointNumber - 2)); //find what point number we are on
                    bool nextPointNumberAlreadyExists = (searchForPointNumberInPointsSection(nextPointNumber) >= 0);
                    if (pointNumber != nextPointNumber && nextPointNumberAlreadyExists) //if the next point number to be used is already in use we will have to do some swapping
                    {
                        changeAPointNumberToADifferentNumber(pointNumber, int.MaxValue);
                        changeAPointNumberToADifferentNumber(nextPointNumber, pointNumber);
                        changeAPointNumberToADifferentNumber(int.MaxValue, nextPointNumber);
                    }
                    else if (pointNumber != nextPointNumber)            //the next point number to use hasn't been used at all in the program yet
                    {
                        changeAPointNumberToADifferentNumber(pointNumber, nextPointNumber);
                    }
                    nextPointNumber++;          //going onto the next point
                }
            }
        }

        /// <summary>
        /// This goes the a program and makes it so that any two points that are at the same location are using the same point number in the program section
        /// </summary>
        public void joinPointsThatAreAtTheSameLocation(double marginForMatch)
        {
            for (int i=0;i<m_programSections[2].Count - 1;i++)
            {
                for (int j=i + 1;j<m_programSections[2].Count - 1;j++)
                {
                    Matrix point1 = turnPointStringIntoTransformationMatrix(m_programSections[2].ElementAt(i));
                    Matrix point2 = turnPointStringIntoTransformationMatrix(m_programSections[2].ElementAt(j));
                    if (point1 != null && point2 != null && point1.Equals(point2, marginForMatch))
                    {           //these points are in the same location go through program section and change reference of second point to that of the first point
                        int firstNumber = parsePointNumberFromPointString(m_programSections[2].ElementAt(i));
                        int secondNumber = parsePointNumberFromPointString(m_programSections[2].ElementAt(j));
                        for (int k=0;k<m_programSections[1].Count - 1;k++)
                        {
                            string line = m_programSections[1].ElementAt(k);
                            if (parsePointNumberFromLine(line) == secondNumber)
                            {
                                string newLine = replacePointNumberInLine(line, firstNumber);
                                m_programSections[1].RemoveAt(k);
                                m_programSections[1].Insert(k, newLine);
                            }
                        }
                    }
                }
            }
        }

        private static List<string>[] findDifferencesBetweenPointStrings(string pointString1, string pointString2)
        {
            List<string>[] ret = new List<string>[2];
            ret[0] = new List<string>();
            ret[1] = new List<string>();
            for (int i = 0; i < pointString1.Length && i < pointString2.Length; i++)
            {
                if (!pointString1.ElementAt(i).Equals(pointString2.ElementAt(i)))
                {
                    int temp = pointString1.Length;
                    int indexOfEquals = pointString1.Substring(0, i).LastIndexOf('=');
                    if (indexOfEquals == -1)
                    {
                        indexOfEquals = pointString1.Substring(0, i).LastIndexOf(':');
                    }
                    while (i < pointString1.Length && i < pointString2.Length && !((i+1 < pointString1.Length && pointString1.Substring(i, 2).Equals("mm")) || (i+2 < pointString1.Length && pointString1.Substring(i, 3).Equals("deg"))))
                    {
                        i++;
                    }
                    ret[0].Add(pointString1.Substring(indexOfEquals - 2, i - indexOfEquals + 2));
                    ret[1].Add(pointString2.Substring(indexOfEquals - 2, i - indexOfEquals + 2));
                    i++;
                }
            }
            return ret;
        }

        private void changeAPointNumberToADifferentNumber(int numberToChange, int newNumber)
        {
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {               //go through the entire program section and change the point number to the new number
                string line = m_programSections[1].ElementAt(i);
                int indexOfPointNumber = line.IndexOf("P[");
                if (indexOfPointNumber >= 0)
                {
                    int indexOfClosePointBracket = line.IndexOf(']');
                    int pointNumber = int.Parse(line.Substring(indexOfPointNumber + 2, indexOfClosePointBracket - indexOfPointNumber - 2));
                    if (pointNumber == numberToChange)
                    {
                        line = line.Replace("P[" + numberToChange + "]", "P[" + newNumber + "]");
                        m_programSections[1].RemoveAt(i);
                        m_programSections[1].Insert(i, line);
                    }
                }
            }
            int indexOfPointInPointSection = searchForPointNumberInPointsSection(numberToChange);
            string pointString = m_programSections[2].ElementAt(indexOfPointInPointSection);      //find the point in the points section
            pointString = pointString.Replace("P[" + numberToChange + "]", "P[" + newNumber + "]"); //replace the point number in the points section
            m_programSections[2].RemoveAt(indexOfPointInPointSection);
            bool haveInserted = false;
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {                                                   //insert the updated point string into the correct location in the point section
                string line = m_programSections[2].ElementAt(i);
                int thisPoint = parsePointNumberFromPointString(line);
                if (thisPoint > newNumber)
                {
                    m_programSections[2].Insert(i, pointString);
                    haveInserted = true;
                    break;
                }
            }
            if (!haveInserted)
            {
                m_programSections[2].Insert(m_programSections[2].Count - 1, pointString);
            }
        }

        /// <summary>
        /// finds the index of number of an axis
        /// </summary>
        /// <param name="axis">the axis we want to know the index of</param>
        /// <returns>the zero-based index of the axis asked for</returns>
        private int indexOfAxis(string axis)
        {
            if (axis.Equals("X", StringComparison.CurrentCultureIgnoreCase))
            {
                return 0;
            }
            else if (axis.Equals("Y", StringComparison.CurrentCultureIgnoreCase))
            {
                return 1;
            }
            else if (axis.Equals("Z", StringComparison.CurrentCultureIgnoreCase))
            {
                return 2;
            }
            else if (axis.Equals("W", StringComparison.CurrentCultureIgnoreCase))
            {
                return 3;
            }
            else if (axis.Equals("P", StringComparison.CurrentCultureIgnoreCase))
            {
                return 4;
            }
            else if (axis.Equals("R", StringComparison.CurrentCultureIgnoreCase))
            {
                return 5;
            }
            else if (axis.Equals("E1", StringComparison.CurrentCultureIgnoreCase))
            {
                return 6;
            }
            else if (axis.Equals("E2", StringComparison.CurrentCultureIgnoreCase))
            {
                return 7;
            }
            else if (axis.Equals("E3", StringComparison.CurrentCultureIgnoreCase))
            {
                return 8;
            }
            return -1;
        }

        /// <summary>
        /// Goes through every point in the program and builds a list of all the unique user frame numbers
        /// </summary>
        /// <returns>A list containing every user frame number that is used in the program</returns>
        private List<int> returnAllUsedUserFrames()
        {
            List<int> ret = new List<int>();
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfUserFrame = pointString.IndexOf("UF :");
                int indexOfComma = pointString.IndexOf(',', indexOfUserFrame);
                pointString = pointString.Substring(indexOfUserFrame + 4, indexOfComma - indexOfUserFrame - 4).Trim();
                int pointNumber = int.Parse(pointString);
                if (!ret.Contains(pointNumber))
                {
                    ret.Add(pointNumber);
                }
            }
            return ret;
        }

        /// <summary>
        /// Goes through every point in the program and builds a list of all the unique tool frame numbers
        /// </summary>
        /// <returns>A list containing every tool frame number that is used in the program</returns>
        private List<int> returnAllUsedToolFrames()
        {
            List<int> ret = new List<int>();
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfUserFrame = pointString.IndexOf("UT :");
                int indexOfComma = pointString.IndexOf(',', indexOfUserFrame);
                pointString = pointString.Substring(indexOfUserFrame + 4, indexOfComma - indexOfUserFrame - 4).Trim();
                int pointNumber = int.Parse(pointString);
                if (!ret.Contains(pointNumber))
                {
                    ret.Add(pointNumber);
                }
            }
            return ret;
        }

        /// <summary>
        /// searches through the points for the index of the specified point
        /// </summary>
        /// <param name="pointNumber">the point you are looking for</param>
        /// <returns>the index in programSections[2] of the point specified in the argument</returns>
        private int searchForPointNumberInPointsSection(int pointNumber)
        {
            string temp;
            int searchPoint = 0;            //variable to be used for the point number that is parsed out of a point string
            int searchPointIndex = 0;
            if (m_programSections[2].Count - 2 < pointNumber)
            {                   //the point number being looked for is a larger number than the total number of points
                temp = m_programSections[2].ElementAt(m_programSections[2].Count -2);
                searchPointIndex = m_programSections[2].Count - 2;
                searchPoint = parsePointNumberFromPointString(temp);                //parse out the point value of the last point in the program
                if (pointNumber == searchPoint)
                {                                                                   //if this is the point being searched for return
                    return m_programSections[2].Count -2;
                }
            }
            else
            {                   //the point being searched for isn't a larger number than the total number of points
                temp = m_programSections[2].ElementAt(pointNumber);                   //start the search at the point string that is the same number as the point being searched for (point being searched for should be close by)
                searchPointIndex = pointNumber;
                searchPoint = parsePointNumberFromPointString(temp);
                if (pointNumber == searchPoint)
                {                                                                   //if this is the point being searched for return
                    return pointNumber;
                }
            }
            
            for (int i = 1; i < m_programSections[2].Count; i++)
            {
                if (searchPointIndex - i >= 0)
                {           //search downward through the points
                    temp = m_programSections[2].ElementAt(searchPointIndex-i);
                    searchPoint = parsePointNumberFromPointString(temp);
                    if (pointNumber == searchPoint)
                    {                                                               //if this is the point being searched for return
                        return (searchPointIndex - i);
                    }
                }
                if (searchPointIndex + i < m_programSections[2].Count - 1)
                {           //search upward through the points
                    temp = m_programSections[2].ElementAt(searchPointIndex + i);
                    searchPoint = parsePointNumberFromPointString(temp);
                    if (pointNumber == searchPoint)
                    {                                                               //if this is the point being searched for return
                        return (searchPointIndex + i);
                    }
                }
            }
            return -1;                                      //failed to find the point return a non-possible index
        }

        /// <summary>
        /// Parses the value of an axis from the provided point String
        /// </summary>
        /// <param name="pointString">the point string for the point we want to know the value of an axis for</param>
        /// <param name="axis">the axis you want to know the value for</param>
        /// <returns>the value of the axis spedified in the argument</returns>
        private double parseAxisValueFromPointString(string pointString, string axis)
        {
            if (axis.ToUpper().Equals("W") || axis.ToUpper().Equals("P") || axis.ToUpper().Equals("R") || axis.ToUpper().Contains('J'))
            {
                int indexStart = pointString.IndexOf(axis.ToUpper() + " =");        //find beginning of the axis value
                int indexEnd = pointString.IndexOf("deg", indexStart);              //find the end of the axis value (angles are in deg)
                string temp = pointString.Substring(indexStart + 3, indexEnd - (indexStart + 3)).Trim();        //retrieve section of point string that holds just the value of the axis
                return double.Parse(temp);                                          //parse that string out into a double 
            }
            else
            {
                int indexStart;                                                 //find beginning of the axis value
                if (axis.Equals("E1") || axis.Equals("E2") || axis.Equals("E3"))
                {
                    indexStart = pointString.IndexOf(axis.ToUpper() + "=");
                }
                else
                {
                    indexStart = pointString.IndexOf(axis.ToUpper() + " =");
                }
                int indexEnd;                               //find the end of the axis value (linear axeses are in mm)
                if (axis.Equals("E1"))
                {
                    if (m_axisTypes[0] == AxisType.LINEAR)
                    {
                        indexEnd = pointString.IndexOf("mm", indexStart);
                    }
                    else
                    {
                        indexEnd = pointString.IndexOf("deg", indexStart);
                    }
                }
                else if (axis.Equals("E2"))
                {
                    if (m_axisTypes[1] == AxisType.LINEAR)
                    {
                        indexEnd = pointString.IndexOf("mm", indexStart);
                    }
                    else
                    {
                        indexEnd = pointString.IndexOf("deg", indexStart);
                    }
                }
                else if (axis.Equals("E3"))
                {
                    if (m_axisTypes[2] == AxisType.LINEAR)
                    {
                        indexEnd = pointString.IndexOf("mm", indexStart);
                    }
                    else
                    {
                        indexEnd = pointString.IndexOf("deg", indexStart);
                    }
                }
                else
                {
                    indexEnd = pointString.IndexOf("mm", indexStart);
                }
                string temp = pointString.Substring(indexStart + 3, indexEnd - (indexStart + 3)).Trim();        //retrieve section of point string that holds just the value of the axis
                return double.Parse(temp);                                          //parse that string out into a double 
            }
        }

        /// <summary>
        /// Retrieves the axis value for a specififed point
        /// </summary>
        /// <param name="axis">the name of the axis you want the value for</param>
        /// <param name="pointNumber">the point you want to get the axis value for</param>
        /// <returns>the axis value for the specified axis at the specified point</returns>
        private double getAxisValueForPoint(string axis, int pointNumber)
        {
            int index = searchForPointNumberInPointsSection(pointNumber);
            return parseAxisValueFromPointString(m_programSections[2].ElementAt(index), axis);
        }

        /// <summary>
        /// Parses the point number from a point string
        /// </summary>
        /// <param name="pointString">the point string from programSections[2]</param>
        /// <returns>the point number that was in the argument</returns>
        private int parsePointNumberFromPointString(string pointString)
        {
            int indexOfOpeningBracket = pointString.IndexOf('[');
            int indexOfClosingBracket = pointString.IndexOf(']');
            if (pointString.IndexOf(':') < indexOfClosingBracket)
            {
                indexOfClosingBracket = pointString.IndexOf(':');
            }
            return int.Parse(pointString.Substring(indexOfOpeningBracket + 1, indexOfClosingBracket - indexOfOpeningBracket -1));
        }

        /// <summary>
        /// Creates the transformation matrix that matches up with a specified point
        /// </summary>
        /// <param name="pointNumber">the point you want to create a matrix from</param>
        /// <returns>A 4x4 matrix that corresponds with the point's X, Y, Z, W, P ,R values</returns>
        public Matrix turnPointIntoTransformationMatrix(int pointNumber)
        {
            int point = searchForPointNumberInPointsSection(pointNumber);
            string pointString = m_programSections[2].ElementAt(point);           //retrieve the point string for the correct point
            double x = parseAxisValueFromPointString(pointString, "X");         //parse all of the values out of the point string
            double y = parseAxisValueFromPointString(pointString, "Y");
            double z = parseAxisValueFromPointString(pointString, "Z");
            double w = parseAxisValueFromPointString(pointString, "W");
            double p = parseAxisValueFromPointString(pointString, "P");
            double r = parseAxisValueFromPointString(pointString, "R");
            Matrix ret = new Matrix(x, y, z, w, p, r, "ZYX", true);             //create the transformation matrix
            return ret;
        }

        /// <summary>
        /// Creates the transformation matrix that matches up with a specified point
        /// </summary>
        /// <param name="pointString">the point string you want to create a matrix from</param>
        /// <returns>A 4x4 matrix that corresponds with the point's X, Y, Z, W, P ,R values</returns>
        public Matrix turnPointStringIntoTransformationMatrix(string pointString)
        {
            if (isPointInJointRepresentation(pointString))
            {
                return null;
            }
            else
            {
                double x = parseAxisValueFromPointString(pointString, "X");         //parse all of the values out of the point string
                double y = parseAxisValueFromPointString(pointString, "Y");
                double z = parseAxisValueFromPointString(pointString, "Z");
                double w = parseAxisValueFromPointString(pointString, "W");
                double p = parseAxisValueFromPointString(pointString, "P");
                double r = parseAxisValueFromPointString(pointString, "R");
                Matrix ret = new Matrix(x, y, z, w, p, r, "ZYX", true);             //create the transformation matrix
                return ret;
            }
        }

        /// <summary>
        /// Changes all of the values of a point to those specified by the passed in transformation matrix
        /// </summary>
        /// <param name="pointNumber">the point in the program that is haveing its values replaced</param>
        /// <param name="newValues">the matrix with the new values.</param>
        public void replacePointValuesWithThoseOfAMatrix(int pointNumber, Matrix newValues)
        {
            int[] size = newValues.size();
            if (size[0] == 4 && size[1] == 4)
            {
                int pointNumberInProgram = searchForPointNumberInPointsSection(pointNumber);
                string pointString = m_programSections[2].ElementAt(pointNumberInProgram);
                double[] angles = newValues.parseZYXRotations();
                //create the section of the point string that holds all of the X,Y,Z,W,P,R values
                string newString = String.Format("X = {0,9}  mm, Y = {1,9}  mm, Z = {2,9}  mm,\n\tW = {3,9} deg, P = {4,9} deg, R = {5,9} deg", Math.Round(newValues.get(0, 3), 3).ToString("#0.000"), Math.Round(newValues.get(1, 3), 3).ToString("#.000"), Math.Round(newValues.get(2, 3), 3).ToString("#0.000"), Math.Round(angles[0], 3).ToString("#0.000"), Math.Round(angles[1], 3).ToString("#0.000"), Math.Round(angles[2], 3).ToString("#0.000"));
                int indexStart = pointString.IndexOf("X =");                //find the beginning of the part of the point string that will be replaced
                int otherIndex = pointString.IndexOf("R =");
                int indexEnd = pointString.IndexOf("deg", otherIndex);      //find the end of the part of the point string that will be replaced
                m_programSections[2].Insert(pointNumberInProgram, pointString.Substring(0, indexStart) + newString + pointString.Substring(indexEnd + 3));       //add the point string with the replaced values back into the program
                m_programSections[2].RemoveAt(pointNumberInProgram + 1);        
            }
        }

        /// <summary>
        /// replaces the value of a given axis with the given value for a given point
        /// </summary>
        /// <param name="newValue">the new value to be plugged into the axis value for the point</param>
        /// <param name="axis">the axis you want to replace the value for</param>
        /// <param name="pointNumber">the point number you want to modify</param>
        public void replaceAxisValue(double newValue, string axis, int pointNumber)
        {
            int indexOfPoint = searchForPointNumberInPointsSection(pointNumber);
            string pointString = m_programSections[2].ElementAt(indexOfPoint);
            int indexOfAxis = pointString.IndexOf(axis);
            int endIndexOfAxis;
            if (axis.Equals("W") || axis.Equals("P") || axis.Equals("R"))
            {
                endIndexOfAxis = pointString.IndexOf("deg", indexOfAxis);
            }
            else
            {
                endIndexOfAxis = pointString.IndexOf("mm", indexOfAxis);
            }
            string firstPart = pointString.Substring(0, indexOfAxis + 3);
            string secondPart = pointString.Substring(endIndexOfAxis - 1);
            string pointStringWithReplacedAxisValue = firstPart + Math.Round(newValue, 3).ToString("#0.000") + secondPart;              //recreate the point's string with new axis value
            m_programSections[2].Insert(indexOfPoint, pointStringWithReplacedAxisValue);
            m_programSections[2].RemoveAt(indexOfPoint + 1);                                  //update program sections with updated program line
        }

        public int getSpeedForRobotPoint(int pointNumber)
        {
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string tempString = m_programSections[1].ElementAt(i);
                int index = tempString.IndexOf("P[" + pointNumber);
                if (index >= 0)
                {
                    int indexOfMoveType = tempString.IndexOf(':') + 1;
                    int indexOfPoint = tempString.IndexOf(']');
                    int indexOfUnits;
                    if (tempString.ElementAt(indexOfMoveType).Equals('J'))
                    {                                               //Joint moves' speed values are a percentage 
                        indexOfUnits = tempString.IndexOf('%');
                    }
                    else if (tempString.ElementAt(indexOfMoveType).Equals('C'))
                    {
                        tempString = m_programSections[1].ElementAt(i + 1);
                        indexOfPoint = tempString.IndexOf(']');
                        indexOfUnits = tempString.IndexOf("mm/sec"); ;
                    }
                    else
                    {                                               //Linear moves' speed values are in mm/sec
                        indexOfUnits = tempString.IndexOf("mm/sec");
                    }
                    string temp = tempString.Substring(indexOfPoint + 2, indexOfUnits - indexOfPoint - 2);
                    return int.Parse(temp);
                }
            }
            return -1;
        }

        public List<Robot_Point> exportPoints()
        {
            List<Robot_Point> ret = new List<Robot_Point>();
            for (int i = 0; i < m_programSections[2].Count - 1; i++)
            {
                string pointString = m_programSections[2].ElementAt(i);
                int indexOfOpeningBracket = pointString.IndexOf('[');
                int indexOfClosingBracket = pointString.IndexOf(']');
                string pointNumber = pointString.Substring(indexOfOpeningBracket + 1, indexOfClosingBracket - indexOfOpeningBracket - 1);
                double x = parseAxisValueFromPointString(pointString, "X");
                double y = parseAxisValueFromPointString(pointString, "Y");
                double z = parseAxisValueFromPointString(pointString, "Z");
                ret.Add(new Robot_Point(pointNumber, new Point_3D(x, y, z)));
            }
            return ret;
        }

        public void addStartMeasureMotionOptions(string startMeasureProgramName, string startMidMeasureProgramName)
        {
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                if (line.Contains("P["))
                {
                    try
                    {
                        int pointNumber = parsePointNumberFromLine(line);
                        if (pointNumber > 0)
                        {
                            if (pointNumber > 5000)
                            {
                                line = line.Substring(0, line.Length - 7) + " TA   0.05sec, CALL " + startMidMeasureProgramName + line.Substring(line.Length - 7);
                            }
                            else
                            {
                                line = line.Substring(0, line.Length - 7) + " TA   0.05sec, CALL " + startMeasureProgramName + line.Substring(line.Length - 7);
                            }
                            m_programSections[1].RemoveAt(i);
                            m_programSections[1].Insert(i, line);
                        }
                    }
                    catch (Exception)
                    {
                        //line doesn't have a move in it do nothing
                    }
                }
            }
        }

        public void addStartMeasureCalls(string startMeasureProgramName, string startMidMeasureProgramName)
        {
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                if (line.Contains("P["))
                {
                    try
                    {
                        int pointNumber = parsePointNumberFromLine(line);
                        if (pointNumber > 0)
                        {
                            string newLine = "";
                            if (pointNumber > 5000)
                            {
                                newLine = line.Substring(0, 5) + " CALL " + startMidMeasureProgramName + line.Substring(line.Length - 7);
                            }
                            else
                            {
                                newLine = line.Substring(0, 5) + " CALL " + startMeasureProgramName + line.Substring(line.Length - 7);
                            }
                            m_programSections[1].Insert(i + 1, newLine);
                        }
                    }
                    catch (Exception)
                    {
                        //line doesn't have a move in it do nothing
                    }
                }
            }
            int lineNumber = 1;             //go through the program section and reset all of the line numbers
            for (int i = 0; i < m_programSections[1].Count - 1; i++)
            {
                string line = m_programSections[1].ElementAt(i);
                int indexOfColon = line.IndexOf(':');
                int thisLineNumber = int.Parse(line.Substring(0, indexOfColon).Trim());
                if (thisLineNumber != lineNumber)
                {
                    line = String.Format("{0,4}", lineNumber++) + line.Substring(indexOfColon);
                    m_programSections[1].RemoveAt(i);
                    m_programSections[1].Insert(i, line);
                }
            }
        }

        public static ProgramAdjuster createAverageProgram(int[] coatCounts, params ProgramAdjuster[] programs)
        {
            if (coatCounts.Length == programs.Length && programs.Length > 0)
            {
                int totalCoats = coatCounts.Sum();
                int numberOfPoints = programs[0].NumberOfPoints;
                ProgramAdjuster ret = new ProgramAdjuster(programs[0].toByteArray());
                for (int i=1;i<=numberOfPoints;i++)
                {
                    float averageSpeed = ((float)programs[0].getSpeedForRobotPoint(i) / totalCoats) * coatCounts[0];
                    for (int j=1;j<programs.Length;j++)
                    {
                        averageSpeed += ((float)programs[j].getSpeedForRobotPoint(i) / totalCoats) * coatCounts[j];
                    }
                    int newSpeed = (int)Math.Round(averageSpeed);
                    ret.replaceSpeedForPoint(i, newSpeed);
                }
                return ret;
            }
            return null;
        }

        public int[] getPointTravelOrder()
        {
            List<int> ret = new List<int>();
            for (int i=0;i<m_programSections[1].Count;i++)
            {
                int pointNumber = parsePointNumberFromLine(m_programSections[1].ElementAt(i));
                if (pointNumber > 0)
                    ret.Add(pointNumber);
            }
            return ret.ToArray();
        }

        #endregion
    }
}
