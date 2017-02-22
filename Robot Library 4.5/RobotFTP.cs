using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace Robot_Library_4_5
{
    public class RobotFTP
    {
        FtpWebRequest ftp;
        string robotIPAddress;
        string robotDirectory;
        const int TIMEOUT = 10000;

        public RobotFTP(string IPAddress)
        {
            robotIPAddress = IPAddress;
            robotDirectory = "/md:/";
        }

        public RobotFTP(RobotFTP oneToCopy)
        {
            this.robotIPAddress = oneToCopy.robotIPAddress;
            this.robotDirectory = oneToCopy.robotDirectory;
        }

        /// <summary>
        /// Downloads a file from the robot, likely to throw a WebException whenever it fails
        /// </summary>
        /// <param name="file">The name of the file to download</param>
        /// <param name="destinationDirectory">The location the file will be downloaded to</param>
        public bool downloadFile(string file, string destinationDirectory)
        {
            try
            {
                if (destinationDirectory.ElementAt(destinationDirectory.Length - 1) != '\\')
                {
                    destinationDirectory += "\\";
                }
                ftp = (FtpWebRequest)WebRequest.Create("ftp://" + robotIPAddress + robotDirectory + file);
                ftp.Method = WebRequestMethods.Ftp.DownloadFile;
                ftp.UseBinary = true;
                ftp.Timeout = TIMEOUT;
                FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
                Stream responseStream = response.GetResponseStream();
                using (FileStream output = File.Create(destinationDirectory + file))
                {
                    copyStream(responseStream, output);
                }
                response.Close();
                return true;
            }
            catch (WebException)
            {
                return false;
            }
        }

        private void copyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[32768];
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }

        /// <summary>
        /// Uploads a file to the robot, likely to throw a WebException whenever it fails
        /// </summary>
        /// <param name="file">The name of the file to upload</param>
        /// <param name="sourceDirectory">The directory the file can be found in</param>
        public bool uploadFile(string file, string sourceDirectory)
        {
            try 
	        {	        
		        if (sourceDirectory.ElementAt(sourceDirectory.Length - 1) != '\\')
                {
                    sourceDirectory += "\\";
                }
                ftp = (FtpWebRequest)WebRequest.Create("ftp://" + robotIPAddress + robotDirectory + file);
                ftp.Method = WebRequestMethods.Ftp.UploadFile;
                ftp.Timeout = TIMEOUT;
                // Copy the contents of the file to the request stream.
                using (FileStream sourceStream = File.Open(sourceDirectory + file, FileMode.Open))
                {
                    using (Stream requestStream = ftp.GetRequestStream())
                    {
                        copyStream(sourceStream, requestStream);
                    }
                }
                FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
                response.Close();
                return true;
	        }
	        catch (WebException)
	        {
		        return false;
	        }
        }

        /// <summary>
        /// Deletes a file from the robot, likely to throw a WebException whenever it fails
        /// </summary>
        /// <param name="file">The name of the file to be deleted off of the robot</param>
        public bool deleteFile(string file)
        {
            try
            {
                ftp = (FtpWebRequest)WebRequest.Create("ftp://" + robotIPAddress + robotDirectory + file);
                ftp.UseBinary = true;
                ftp.Method = WebRequestMethods.Ftp.DeleteFile;
                ftp.Timeout = TIMEOUT;
                FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
                response.Close();
                return true;
            }
            catch (WebException)
            {
                return false;
            }
        }

        /// <summary>
        /// Changes the robot's active directory
        /// </summary>
        /// <param name="newDirectory">The name of the directory the robot should switch to. e.g. MC, MD, UD1, UT1</param>
        public void changeRobotDirectory(string newDirectory)
        {
            newDirectory = newDirectory.ToLower();
            if (newDirectory.Contains("mc"))
            {
                robotDirectory = "/mc:/";
            }
            else if (newDirectory.Contains("md"))
            {
                robotDirectory = "/md:/";
            }
            else if (newDirectory.Contains("ud1"))
            {
                robotDirectory = "/ud1:/";
            }
            else if (newDirectory.Contains("ut1"))
            {
                robotDirectory = "/ut1:/";
            }
        }

        public bool fileIsOnRobot(string file)
        {
            string updatedSearchPattern = "^" + file + "$";
            ftp = (FtpWebRequest)WebRequest.Create(@"ftp://" + robotIPAddress + robotDirectory);
            ftp.Method = WebRequestMethods.Ftp.ListDirectory;
            ftp.UseBinary = true;
            ftp.Timeout = TIMEOUT;
            FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
            Stream responseStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(responseStream);
            string temp;
            Regex regExpPattern = new Regex(updatedSearchPattern);
            bool ret = false;
            while ((temp = reader.ReadLine()) != null)
            {
                if (regExpPattern.IsMatch(temp))
                {
                    ret = true;
                    break;
                }
            }
            //reader.Close();
            return ret;
        }

        /// <summary>
        /// Retrieves a list of all of the files in the robot's current directory, likely to throw a WebException whenever it fails
        /// </summary>
        /// <returns>A list of all of the files in the robot's current directory</returns>
        public string[] listDirectory()
        {
            ftp = (FtpWebRequest)WebRequest.Create(@"ftp://" + robotIPAddress + robotDirectory);
            ftp.Method = WebRequestMethods.Ftp.ListDirectory;
            ftp.UseBinary = true;
            ftp.Timeout = TIMEOUT;
            FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
            Stream responseStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(responseStream);
            string temp;
            List<string> robotProgramList = new List<string>();
            while ((temp = reader.ReadLine()) != null)
            {
                robotProgramList.Add(temp);
            }

            robotProgramList.Sort();                                        //the robot sorts case sensitively, this will cause everything added to the robot's listview to be added with a case insensitive sorting
            return robotProgramList.ToArray();
        }

        /// <summary>
        /// Returns a list of files that match a wildcard expression, likely to throw a WebException whenever it fails
        /// </summary>
        /// <param name="searchPattern">The wildcard pattern being searched for. e.g. a* - all files that begin with the letter a, a*.tp - all files that begin with the letter a and end with .tp</param>
        /// <returns>A list of files that matched the wildcard expression</returns>
        public string[] listDirectory(string searchPattern)
        {
            string updatedSearchPattern = searchPattern;
            if (searchPattern.Equals(""))
            {
                updatedSearchPattern = ".+";                     //one or more of any characters
            }
            else
            {
                int indexOfExtension = updatedSearchPattern.IndexOf('.');
                if (indexOfExtension >= 0)              //has an extension
                {
                    updatedSearchPattern = "^" + updatedSearchPattern + "$";          //make patterns match use whole pattern
                }
                else
                {                                       //no extension
                    updatedSearchPattern = "^" + updatedSearchPattern + "\\." + ".+$";
                }
                int indexOfStar = updatedSearchPattern.IndexOf("*");
                while (indexOfStar >= 0)                //use *'s like a unix wildcard 
                {
                    updatedSearchPattern = updatedSearchPattern.Insert(indexOfStar, ".");
                    indexOfStar = updatedSearchPattern.IndexOf('*', indexOfStar + 2);
                }
            }
            ftp = (FtpWebRequest)WebRequest.Create(@"ftp://" + robotIPAddress + robotDirectory);
            ftp.Method = WebRequestMethods.Ftp.ListDirectory;
            ftp.UseBinary = true;
            ftp.Timeout = TIMEOUT;
            FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
            Stream responseStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(responseStream);
            string temp;
            List<string> robotProgramList = new List<string>();
            Regex regExpPattern = new Regex(updatedSearchPattern);
            while ((temp = reader.ReadLine()) != null)
            {
                if (regExpPattern.IsMatch(temp))
                {
                    robotProgramList.Add(temp);
                }
            }
            robotProgramList.Sort();                                        //the robot sorts case sensitively, this will cause everything added to the robot's listview to be added with a case insensitive sorting
            return robotProgramList.ToArray();
        }
    }
}
