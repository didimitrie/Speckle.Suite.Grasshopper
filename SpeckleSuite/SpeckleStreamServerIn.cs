using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Windows.Forms;
using SocketIOClient;
using System.IO;
using Grasshopper.Kernel.Types;
using GH_IO.Serialization;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Parameters;
using Grasshopper.GUI;
using System.Dynamic;
using Grasshopper.Kernel.Data;
using Grasshopper;

namespace SpeckleSuite
{
    public class SpeckleStreamServerIn : GH_Component, IGH_VariableParameterComponent
    {

        public Client mySocket = null;
        GH_Document GrasshopperDocument;

        SpeckleUtils myUtils;

        public bool connected = false, isLoggedIn = false;
        public string serverId = null;

        string paramnames = "";

        Action expireComponentAction;

        public List<dynamic> jobQueue = new List<dynamic>();
        public dynamic nextJob = null;
        public int jobsLeft = 0;
        public string currentRequestId;
        public SpeckleStreamServerOut SENDER = null;
        public bool pushSolution = true;

        /// <summary>
        /// Initializes a new instance of the SpeckleStreamServerIn class.
        /// </summary>
        public SpeckleStreamServerIn()
          : base("SpeckleStreamServerIn", "SpeckleStreamServerIn",
              "Description",
              "Params", "Speckle Server")
        {
        }

        private void expireComponent()
        {
            this.ExpireSolution(true);
        }

        public override void CreateAttributes()
        {
            m_attributes = new SpeckleStreamServerInAttr(this);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("serverId", serverId);
            writer.SetString("paramnames", paramnames);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            serverId = null;
            reader.TryGetString("serverId", ref serverId);
            paramnames = null;
            reader.TryGetString("paramnames", ref paramnames);
            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            GrasshopperDocument = this.OnPingDocument();
            Grasshopper.Instances.DocumentServer.DocumentRemoved += DocumentServer_DocumentRemoved;

            myUtils = new SpeckleUtils();
            expireComponentAction = expireComponent;

            mySocket = new Client(myUtils.socketServer.ToString());
            mySocket.RetryConnectionAttempts = 10000;


            mySocket.On("connect", (data) =>
            {
                System.Diagnostics.Debug.WriteLine("SERVER INPUT > Connected to server.");
                if (serverId == null)
                {
                    mySocket.Emit("new-server", myUtils.APIKEY);
                }
                else
                {
                    mySocket.Emit("server-input-rejoin-stream", serverId);
                }
            });

            mySocket.On("server-confirmation", (data) =>
            {
                System.Diagnostics.Debug.WriteLine("SERVER INPUT > Got server confirmation.");
                serverId = data.Json.Args[0];
                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            });

            mySocket.On("client-request", (data) =>
            {
                System.Diagnostics.Debug.WriteLine("SERVER INPUT > Client request. Job count: " + jobQueue.Count);
                var temp = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.ToJsonString());

                dynamic request = new ExpandoObject();
                request.structure = temp.args[0].structure;
                request.objects = temp.args[0].objects;
                request.requestId = temp.args[0].requestId;
                jobQueue.Add(request);
                if (nextJob == null)
                {
                    nextJob = jobQueue[0];
                    Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
                }
            });

            //reinstate param change listeneres
            for (int i = 1; i < Params.Output.Count; i++)
                Params.Output[i].ObjectChanged += Param_ObjectChanged;

            GrasshopperDocument.SolutionEnd += GrasshopperDocument_SolutionEnd;

            isLoggedIn = myUtils.hasApiKey();
            if (!isLoggedIn)
                isLoggedIn = myUtils.promptForApiKey();
            if (isLoggedIn)
                mySocket.Connect();

        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);

            dynamic data = new System.Dynamic.ExpandoObject();
            data.serverId = serverId;
            mySocket.Emit("server-deleted", data);

            mySocket.Close();
            mySocket.Dispose();
        }

        private void DocumentServer_DocumentRemoved(GH_DocumentServer sender, GH_Document doc)
        {
            GrasshopperDocument = this.OnPingDocument();
            if (GrasshopperDocument == doc)
            {
                dynamic payload = new ExpandoObject();
                payload.serverId = serverId;
                mySocket.Emit("server-offline", payload);
                mySocket.Close();
                mySocket.Dispose();
            }
        }

        // this schedules the next solution run
        private void GrasshopperDocument_SolutionEnd(object sender, GH_SolutionEventArgs e)
        {
            if (jobQueue.Count > 0)
            {
                nextJob = jobQueue[0];
                System.Diagnostics.Debug.WriteLine("Solution end. Starting a new one!");
                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            }
        }

        public override bool AppendMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            ToolStripDropDown mydrop = new ToolStripDropDown();

            GH_DocumentObject.Menu_AppendSeparator(menu);
            GH_DocumentObject.Menu_AppendItem(menu, @"Copy server ID to Clipboard (" + serverId + ")", copyRoomToClipboard);
            GH_DocumentObject.Menu_AppendSeparator(menu);
            GH_DocumentObject.Menu_AppendItem(menu, @"User Guide - Give it a read!", myUtils.openHelp);
            GH_DocumentObject.Menu_AppendItem(menu, @"Github | MIT License", myUtils.gotoGithub);
            GH_DocumentObject.Menu_AppendSeparator(menu);
            //GH_DocumentObject.Menu_AppendItem(menu, myUtils.hasApiKey() ? @"Reset your Speckle API Key (Logout)" : @"Set your Speckle API Key (Login)", resetKey);
            return true;
        }

        public void copyRoomToClipboard(Object sender, EventArgs e)
        {
            System.Windows.Forms.Clipboard.SetText(serverId);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("OUT >>>", "OUT >>>", "Connect this to the server out component!", GH_ParamAccess.item);

            Params.Output[0].MutableNickName = false;
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.SetData(0, this.InstanceGuid.ToString());

            if (!myUtils.hasApiKey())
            {
                isLoggedIn = false;
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "You are not logged in.");
                return;
            }

            if (serverId == null) return;
            if (mySocket == null) return;

            updateStreamStructure();

            if (jobQueue.Count <= 0) return;
            if (nextJob == null) return;

            this.currentRequestId = (string)nextJob.requestId;

            int totalCount = 0;
            int structureindex = 0;

            foreach (dynamic structure in nextJob.structure)
            {
                DataTree<object> myTree = new DataTree<object>();
                string[] treeTopology = structure.topology.ToString().Split(' ');

                string motherfucker = structure.topology.ToString();

                if (motherfucker != "")
                    foreach (string branch in treeTopology)
                    {
                        if (branch != "" && branch != null)
                        {
                            string[] branchTopology = branch.Split('-')[0].Split(';');
                            List<int> branchIndexes = new List<int>();
                            foreach (string t in branchTopology)
                                branchIndexes.Add(Convert.ToInt32(t));

                            int elCount = Convert.ToInt32(branch.Split('-')[1]);

                            GH_Path myPath = new GH_Path(branchIndexes.ToArray());

                            for (int i = 0; i < elCount; i++)
                            {
                                object myObj;
                                myObj = StreamConverter.castToGH(nextJob.objects[totalCount + i].type.ToString(), nextJob.objects[totalCount + i].value);


                                myTree.EnsurePath(myPath).Add(myObj);

                            }

                            totalCount += elCount;
                        }
                    }

                DA.SetDataTree(structureindex + 1, myTree);
                structureindex++;
            }



            jobQueue.Remove(nextJob);
            nextJob = null;

            this.Message = jobQueue.Count + " reqs";
        }

        public void updateStreamStructure()
        {
            dynamic sendEventData = new ExpandoObject();
            List<dynamic> inputStructure = new List<dynamic>();
            string newparamnames = "";

            for (int i = 1; i < Params.Output.Count; i++)
            {
                IGH_Param param = Params.Output[i];
                dynamic structureItem = new System.Dynamic.ExpandoObject();
                structureItem.name = param.NickName == null ? param.Name : param.NickName;

                newparamnames += structureItem.name;

                structureItem.guid = param.InstanceGuid.ToString();
                structureItem.count = 0;

                inputStructure.Add(structureItem as dynamic);
            }

            if (paramnames == newparamnames) return;

            paramnames = newparamnames;
            sendEventData.inputStructure = inputStructure;
            sendEventData.serverId = serverId;
            try
            {
                mySocket.Emit("server-update-input-structure", sendEventData);
            }
            catch { }
        }

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Output && index >= 1)
                return true;
            else
                return false;

        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Output && Params.Output.Count > 2 && index != 0)
                return true;
            else
                return false;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            Grasshopper.Kernel.Parameters.Param_GenericObject param = new Param_GenericObject();

            param.Name = GH_ComponentParamServer.InventUniqueNickname("ABCDEFGHIJKLMNOPQRSTUVWXYZ", Params.Output);
            param.NickName = param.Name;
            param.Description = "Server Inputs / What the server receives from clients";
            param.Optional = true;
            param.Access = GH_ParamAccess.tree;
            //param.AttributesChanged += Param_AttributesChanged;
            param.ObjectChanged += Param_ObjectChanged;
            return param;
        }

        private void Param_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            updateStreamStructure();
            //Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        public void VariableParameterMaintenance()
        {
            updateStreamStructure();
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{7b3e0ff4-ae4d-4e79-a27b-ade6dff3dd7d}"); }
        }
    }
}