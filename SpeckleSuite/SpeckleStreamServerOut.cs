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

namespace SpeckleSuite
{
    public class SpeckleStreamServerOut : GH_Component, IGH_VariableParameterComponent
    {
        public string serverId = null, requestId = null;
        public Client mySocket = null;
        SpeckleUtils myUtils;
        private bool isLoggedIn;
        private GH_Document GrasshopperDocument;

        string paramnames = "";

        public SpeckleStreamServerIn RECEIVER = null;
        string oldRecGuid = null;

        bool isConnected = false;

        Action expireComponentAction;

        public string currentRequestId = null;

        /// <summary>
        /// Initializes a new instance of the SpeckleStreamServerOut class.
        /// </summary>
        public SpeckleStreamServerOut()
          : base("SpeckleStreamServerOut", "Nickname",
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
            m_attributes = new SpeckleStreamServerOutAttr(this);
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
            GrasshopperDocument = this.OnPingDocument();

            myUtils = new SpeckleUtils();
            expireComponentAction = expireComponent;


            mySocket = new Client(myUtils.socketServer.ToString());
            mySocket.RetryConnectionAttempts = 10000;

            oldRecGuid = null;

            isLoggedIn = myUtils.hasApiKey();

            if (!isLoggedIn)
                isLoggedIn = myUtils.promptForApiKey();

            mySocket.On("connect", (data) =>
            {
                //MessageBox.Show("HEya! I'm connected >>> " + serverId);
                if (serverId == null)
                    return;
                else
                    mySocket.Emit("server-output-rejoin-stream", serverId);
                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            });

            //reinstate param change listeneres
            for (int i = 1; i < Params.Input.Count; i++)
                Params.Input[i].ObjectChanged += Param_ObjectChanged;

            if (!isConnected && serverId != null && serverId != "")
            {
                mySocket.Connect();
            }

            base.AddedToDocument(document);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            mySocket.Close();
            mySocket.Dispose();
        }

        private void DocumentServer_DocumentRemoved(GH_DocumentServer sender, GH_Document doc)
        {
            GrasshopperDocument = this.OnPingDocument();
            if (GrasshopperDocument != doc) return;

            mySocket.Close();
            mySocket.Dispose();
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

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("<<< IN", "<<< IN", "Connect this to the server out component!", GH_ParamAccess.item);
            Params.Input[0].MutableNickName = false;
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string recGuid = null;
            DA.GetData(0, ref recGuid);

            if (recGuid == null) return; // means there's no input server pair so let's fuck off

            if ((recGuid != oldRecGuid))
            {
                RECEIVER = (SpeckleStreamServerIn)GrasshopperDocument.FindComponent(new Guid(recGuid));

                serverId = RECEIVER.serverId;
                oldRecGuid = recGuid;

                if (serverId != null)
                    mySocket.Connect();

            }

            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, serverId + " < ");

            if (RECEIVER == null) return;
            if (serverId == null || serverId == "") return;

            if (!myUtils.hasApiKey())
            {
                isLoggedIn = false;
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "You are not logged in.");
                return;
            }

            if (serverId == null) return;
            if (mySocket == null) return;

            //updateStreamStructure();

            if (RECEIVER.currentRequestId == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Server is idle.");
                return;
            }

            if (Params.Input.Count <= 1) return;

            dynamic payload = new System.Dynamic.ExpandoObject();
            payload.structure = new List<dynamic>();
            payload.objects = new List<dynamic>();
            payload.serverId = serverId;
            payload.requestId = RECEIVER.currentRequestId;

            List<dynamic> structure = new List<dynamic>();
            int count = 0;
            foreach (IGH_Param param in this.Params.Input)
            {
                if (count >= 1)
                {
                    dynamic structureItem = new ExpandoObject();
                    structureItem.name = param.NickName == null ? param.Name : param.NickName;
                    structureItem.guid = param.InstanceGuid.ToString();
                    structureItem.topology = getParamTopology(param);
                    foreach (Object myObj in param.VolatileData.AllData(true))
                    {
                        payload.objects.Add(StreamConverter.castFromGH(myObj, (string)structureItem.name));
                    }
                    payload.structure.Add(structureItem as dynamic);
                }

                count++;
            }


            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            mySocket.Emit("server-response", serialized);

            RECEIVER.currentRequestId = null;

        }

        public string getParamTopology(IGH_Param param)
        {
            string topology = "";
            foreach (Grasshopper.Kernel.Data.GH_Path mypath in param.VolatileData.Paths)
            {
                topology += mypath.ToString(false) + "-" + param.VolatileData.get_Branch(mypath).Count + " ";
            }
            return topology;
        }

        public void updateStreamStructure()
        {
            if (serverId == null) return;
            dynamic sendEventData = new ExpandoObject();
            List<dynamic> outputStructure = new List<dynamic>();
            string newparamnames = "";

            for (int i = 1; i < Params.Input.Count; i++)
            {
                IGH_Param param = Params.Input[i];
                dynamic structureItem = new System.Dynamic.ExpandoObject();
                structureItem.name = param.NickName == null ? param.Name : param.NickName;
                newparamnames += structureItem.name;
                structureItem.guid = param.InstanceGuid.ToString();
                structureItem.count = 0;
                foreach (Object myObj in param.VolatileData.AllData(true))
                {
                    structureItem.count++;
                }

                outputStructure.Add(structureItem as dynamic);
            }

            if (paramnames == newparamnames) return;

            paramnames = newparamnames;
            sendEventData.outputStructure = outputStructure;
            sendEventData.serverId = serverId;
            mySocket.Emit("server-update-output-structure", sendEventData);
        }

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input && index >= 1)
                return true;
            else
                return false;

        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input && Params.Input.Count > 2 && index != 0)
                return true;
            else
                return false;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            Grasshopper.Kernel.Parameters.Param_GenericObject param = new Param_GenericObject();

            param.Name = GH_ComponentParamServer.InventUniqueNickname("ABCDEFGHIJKLMNOPQRSTUVWXYZ", Params.Input);
            param.NickName = param.Name;
            param.Description = "Server Outputs // What the server sends away";
            param.Optional = true;
            param.Access = GH_ParamAccess.item;

            param.ObjectChanged += Param_ObjectChanged;

            return param;
        }

        private void Param_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            updateStreamStructure();
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        public void VariableParameterMaintenance()
        {
            updateStreamStructure();
            //return true;
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{3345c6d9-a4a8-4f78-a3dd-29a552b49781}"); }
        }
    }
}