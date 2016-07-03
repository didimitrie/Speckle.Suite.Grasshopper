//using System;
//using System.Xml;
//using System.Threading;

//using Microsoft.VisualBasic;

//using Grasshopper.Kernel;
//using Grasshopper;
//using RestSharp;
//using GH_IO.Serialization;
//using System.Windows.Forms;

//namespace SpeckleSuite
//{
//    public class MetaSpeckle : GH_Component
//    {
//        SpeckleUtils myUtils = new SpeckleUtils();

//        GH_Document GrasshopperDocument;
//        Thread uploadThread = null;
//        bool uploading = false;

//        string myResponse = "";

//        public MetaSpeckle()
//          : base("MetaSpeckle", "mspk",
//              "Sharing GH files has never been easier: just double click the component's icon and the current definition will be securely uploaded to speckle.xyz, ready to be downloaded by the people you choose to share it with!",
//              "Params", "SpeckleSuite")
//        {
//        }

       
//        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
//        {
//            //pManager.AddTextParameter("API Key", "Key", "Your Speckle Suite API key", GH_ParamAccess.item);
//        }

//        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
//        {
//            pManager.AddTextParameter("Link", "LNK", "This is the sharing link for your freshly uploaded definition.", GH_ParamAccess.item);
//        }

//        public override void AddedToDocument(GH_Document document)
//        {
//            base.AddedToDocument(document);
//            GrasshopperDocument = Instances.ActiveCanvas.Document;
//        }

//        public override bool AppendMenuItems(ToolStripDropDown menu)
//        {
//            base.AppendAdditionalMenuItems(menu);

//            if (myUtils.hasApiKey())
//                GH_DocumentObject.Menu_AppendItem(menu, @"Upload definition!", uploadMenuAction);
//            else
//                GH_DocumentObject.Menu_AppendItem(menu, @"Set your Speckle API Key (Login)", setApiKey);

//            GH_DocumentObject.Menu_AppendSeparator(menu);
//            GH_DocumentObject.Menu_AppendItem(menu, @"User Guide - Give it a read!", myUtils.openHelp);
//            GH_DocumentObject.Menu_AppendSeparator(menu);
//            GH_DocumentObject.Menu_AppendItem(menu, @"Github | MIT License", myUtils.gotoGithub);

//            if(myUtils.hasApiKey() == true)
//            {
//                GH_DocumentObject.Menu_AppendItem(menu, @"Reset your Speckle API Key (Logout)", resetKey);
//            }

//            return true;
//        }

//        private void setApiKey(Object sender, EventArgs e)
//        {
//            myUtils.promptForApiKey();
//            Action dlg = expireComponent;
//            Rhino.RhinoApp.MainApplicationWindow.Invoke(dlg);
//        }

//        public void resetKey(Object sender, EventArgs e)
//        {
//            myUtils.removeApiKey();
//            Action dlg = expireComponent;
//            Rhino.RhinoApp.MainApplicationWindow.Invoke(dlg);
//        }


//        private void uploadMenuAction(Object sender, EventArgs e)
//        {
//            this.startUploadThread();
//        }

//        /// <summary>
//        /// This is the method that actually does the work.
//        /// </summary>
//        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
//        /// to store data in output parameters.</param>
//        protected override void SolveInstance(IGH_DataAccess DA)
//        {

//            if (myUtils.hasApiKey())
//            {
//                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "You're good to go! " + myUtils.APIKEY);
//            }
//            else
//            {
//                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "It seems like your api key is missing or wrong.");
//            }
//            if (myResponse != "")
//            {
//                this.Params.Output[0].NickName = "LNK";
//                DA.SetData(0, myResponse);
//            }
//            else DA.SetData(0, "Nothing to show yet. Double click the component to upload the definition!");
//        }

//        public override void CreateAttributes()
//        {
//            m_attributes = new MetaSpeckleComponentAttributes(this);
//        }

//        public void startUploadThread()
//        {
//            if (! myUtils.hasApiKey()) return;

//            if (uploading) return;

//            this.Params.Output[0].NickName = "Uploading defintion!";

//            uploadThread = new Thread( () => uploadToServer() );
//            uploadThread.Name = "Uploader";
//            uploadThread.Priority = ThreadPriority.BelowNormal;
//            uploadThread.Start();

//        }

//        public void uploadToServer()
//        {
//            GH_Archive myArchive = new GH_Archive();
//            myArchive.CreateNewRoot(true);

//            GH_Document myDoc = new GH_Document();
//            myDoc.Properties.Description = "Defintion shared via speckle.xyz. Thanks for the custom!";

//            int i = 0;
//            foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
//            {
//                if (obj.Attributes.DocObject.ComponentGuid.ToString() != this.ComponentGuid.ToString())
//                    myDoc.AddObject(obj, false, i++);
//            }

//            myArchive.AppendObject(myDoc, "Definition");

//            myDoc.Dispose();

//            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(myArchive.Serialize_Xml());
//            var myEncodedDef = System.Convert.ToBase64String(plainTextBytes);

//            var client = new RestClient("http://localhost:3000/api/meta/upload");
//            var request = new RestRequest(Method.POST);

//            var name = "anonymous definition - shared with speckle!";

//            if(GrasshopperDocument.Properties.ProjectFileName != "") { 
//                name = GrasshopperDocument.Properties.ProjectFileName.Replace(".ghx","") + " - shared with speckle!";
//                name = name.Replace(".gh", "");
//            }
            
//            request.AddHeader("cache-control", "no-cache");
//            request.AddHeader("Accept-Encoding", "gzip");
//            request.AddHeader("content-type", "application/json");
//            request.AddParameter("application/json", "{\n    \"apikey\": \"00c645b0-d1c9-428c-8ba3-af5cb19408c5\",\n   \"name\": \"" + name + "\",\n    \"description\": \"Lorem ipsum dolor sic amet.\", \n    \"ghDef\": \"" + myEncodedDef + "\"\n}", ParameterType.RequestBody);

//            IRestResponse response = client.Execute(request);
//            try
//            {
//                myResponse = System.Text.Encoding.ASCII.GetString(response.RawBytes);
//            }
//            catch
//            {
//                myResponse = "Failed to upload. Sorry!";
//            }
//            Action dlg = expireComponent;
//            Rhino.RhinoApp.MainApplicationWindow.Invoke(dlg);
            
//        }

//        private void expireComponent()
//        {
//            this.ExpireSolution(true);
//            uploading = false;
//        }

//        /// <summary>
//        /// Provides an Icon for every component that will be visible in the User Interface.
//        /// Icons need to be 24x24 pixels.
//        /// </summary>
//        protected override System.Drawing.Bitmap Icon
//        {
//            get
//            {
//                // You can add image files to your project resources and access them like this:
//                //return Resources.IconForThisComponent;
//                return null;
//            }
//        }

//        /// <summary>
//        /// Each component must have a unique Guid to identify it. 
//        /// It is vital this Guid doesn't change otherwise old ghx files 
//        /// that use the old ID will partially fail during loading.
//        /// </summary>
//        public override Guid ComponentGuid
//        {
//            get { return new Guid("{3c1be493-221f-45bb-9f86-c0178b75cd75}"); }
//        }
//    }
//}
