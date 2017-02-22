using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Matrix_Library_4_5;
using System.IO;

namespace Robot_Library_4_5
{
    public class UserFrame
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double W { get; set; }
        public double P { get; set; }
        public double R { get; set; }

        public UserFrame()
        {
            this.X = 0;
            this.Y = 0;
            this.Z = 0;
            this.W = 0;
            this.P = 0;
            this.R = 0;
        }

        /// <summary>
        /// Creates an instance of a user frame
        /// </summary>
        /// <param name="x">the X position (in mms)</param>
        /// <param name="y">the Y position (in mms)</param>
        /// <param name="z">the Z position (in mms)</param>
        /// <param name="w">the X rotation (in degrees)</param>
        /// <param name="p">the Y rotation (in degrees)</param>
        /// <param name="r">the Z rotation (in degrees)</param>
        public UserFrame(double x, double y, double z, double w, double p, double r)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
            this.P = p;
            this.R = r;
        }

        /// <summary>
        /// Creates an instance of a user frame, parses the values from strings
        /// If parsing fails all values for the user frame will be set to 0
        /// </summary>
        /// <param name="x">the X position (in mms)</param>
        /// <param name="y">the Y position (in mms)</param>
        /// <param name="z">the Z position (in mms)</param>
        /// <param name="w">the X rotation (in degrees)</param>
        /// <param name="p">the Y rotation (in degrees)</param>
        /// <param name="r">the Z rotation (in degrees)</param>
        public UserFrame(string x, string y, string z, string w, string p, string r)
        {
            try
            {
                this.X = double.Parse(x);
                this.Y = double.Parse(y);
                this.Z = double.Parse(z);
                this.W = double.Parse(w);
                this.P = double.Parse(p);
                this.R = double.Parse(r);
            }
            catch (FormatException)
            {
                this.X = 0;
                this.Y = 0;
                this.Z = 0;
                this.W = 0;
                this.P = 0;
                this.R = 0;
            }
        }

        /// <summary>
        /// Converts the User Frame to its Matrix form
        /// </summary>
        /// <returns>A 4x4 transformation matrix where the translations match the x,y,z positions and the rotations correspond with the w,p,r values of the user frame</returns>
        public Matrix toMatrix()
        {
            return new Matrix(this.X, this.Y, this.Z, this.W, this.P, this.R, "ZYX", true);     //robots always do their rotations in the order of Z-Y-X
        }

        /// <summary>
        /// Creates a user frame from a matrix
        /// </summary>
        /// <param name="matrix">a 4x4 transformation matrix that had its rotation applied in the Z-Y-X order</param>
        public UserFrame(Matrix matrix)
        {
            double[] angles = matrix.parseZYXRotations();               //robots always do their rotations in the order of Z-Y-X
            this.X = matrix.get(0,3);
            this.Y = matrix.get(1,3);
            this.Z = matrix.get(2,3);
            this.W = angles[0];
            this.P = angles[1];
            this.R = angles[2];
        }

        /// <summary>
        /// Saves the user frame to file in a readable form
        /// The file will contain the userframe in matrix form first, then it will list the individual X, Y, Z, W, P, R values 
        /// </summary>
        /// <param name="fileName">The full path name the user frame should be saved as</param>
        public void saveToFile(string fileName)
        {
            Matrix matrix = new Matrix(this.X, this.Y, this.Z, this.W, this.P, this.R, "ZYX", true);
            using (TextWriter output = new StreamWriter(fileName))
            {
                output.WriteLine(matrix.ToString());
                output.WriteLine("X: " + this.X + " mm");
                output.WriteLine("Y: " + this.Y + " mm");
                output.WriteLine("Z: " + this.Z + " mm");
                output.WriteLine("W: " + this.W);
                output.WriteLine("P: " + this.P);
                output.WriteLine("R: " + this.R);
            }
        }
    }
}
