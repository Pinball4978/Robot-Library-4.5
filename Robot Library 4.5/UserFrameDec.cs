using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Matrix_Library_4_5;
using System.IO;

namespace Robot_Library_4_5
{
    public class UserFrameDec
    {
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
        public decimal W { get; set; }
        public decimal P { get; set; }
        public decimal R { get; set; }

        public UserFrameDec()
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
        public UserFrameDec(decimal x, decimal y, decimal z, decimal w, decimal p, decimal r)
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
        public UserFrameDec(string x, string y, string z, string w, string p, string r)
        {
            try
            {
                this.X = decimal.Parse(x);
                this.Y = decimal.Parse(y);
                this.Z = decimal.Parse(z);
                this.W = decimal.Parse(w);
                this.P = decimal.Parse(p);
                this.R = decimal.Parse(r);
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
        public MatrixDec toMatrix()
        {
            return new MatrixDec(this.X, this.Y, this.Z, (double)this.W, (double)this.P, (double)this.R, "ZYX", true);     //robots always do their rotations in the order of Z-Y-X
        }

        /// <summary>
        /// Creates a user frame from a matrix
        /// </summary>
        /// <param name="matrix">a 4x4 transformation matrix that had its rotation applied in the Z-Y-X order</param>
        public UserFrameDec(MatrixDec matrix)
        {
            double[] angles = matrix.parseZYXRotations();               //robots always do their rotations in the order of Z-Y-X
            this.X = matrix.get(0,3);
            this.Y = matrix.get(1,3);
            this.Z = matrix.get(2,3);
            this.W = (decimal)angles[0];
            this.P = (decimal)angles[1];
            this.R = (decimal)angles[2];
        }

        /// <summary>
        /// Saves the user frame to file in a readable form
        /// The file will contain the userframe in matrix form first, then it will list the individual X, Y, Z, W, P, R values 
        /// </summary>
        /// <param name="fileName">The full path name the user frame should be saved as</param>
        public void saveToFile(string fileName)
        {
            MatrixDec matrix = new MatrixDec(this.X, this.Y, this.Z, (double)this.W, (double)this.P, (double)this.R, "ZYX", true);
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
