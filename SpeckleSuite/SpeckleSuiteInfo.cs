using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace SpeckleSuite
{
    public class SpeckleSuiteInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "SpeckleSuite";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "Speckle Suite offers various ways of design communication.";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("cda122e4-bda5-4599-8180-ceb8b45ae264");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Dimitrie A. Stefanescu @idid / UCL The Bartlett School of Architecture";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "twitter: @idid, email: d.stefanescu@ucl.ac.uk";
            }
        }
    }
}
