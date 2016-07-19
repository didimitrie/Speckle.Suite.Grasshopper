using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Windows.Forms;
using SocketIOClient;
using System.IO;
using Grasshopper.Kernel.Parameters;
using System.Linq;
using RestSharp;
using System.Threading;
using GH_IO.Serialization;
using Grasshopper.Kernel.Data;
using System.Diagnostics;
using Grasshopper;
using System.Dynamic;

namespace SpeckleSuite
{
    public class SpeckleStreamReceive : GH_Component, IGH_VariableParameterComponent
    {
        public Client mySocket = null;
        GH_Document GrasshopperDocument;

        SpeckleUtils myUtils;

        public bool connected;
        public string STREAM_ID = null;
        public dynamic output = null;
        public dynamic parsedOutput = null;
        public dynamic parsedOutputStructure = null;
        public dynamic parsedOutputObjects = null;

        public string docName = null;

        public bool streamingPaused = false;
        public bool isLoggedIn = false;
        public bool pullStream = false;

        public int retryAttempts = 0;

        public bool pullingStreamThreadIsRunning = false;

        public List<string> outputParams = new List<string>();

        public DateTime lastReceive;

        public bool isFromSavedDocument = true;

        Action expireComponentAction;

        public SpeckleStreamReceive()
          : base("Speckle Stream Receiver", "StreamReceiver",
              "Receive data from a speckle stream.",
                 "Params", "SpeckleSuite")
        {

        }

        public override void CreateAttributes()
        {
            m_attributes = new SpeckleStreamReceiveAttr(this);
        }

        /// <summary>
        /// Serializes stream id and output params structure on document save
        /// </summary>
        /// <param name="writer"></param>
        /// <returns></returns>
        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("STREAM_ID", STREAM_ID);
            writer.SetString("outputParams", String.Join(",", outputParams.ToArray()));
            return base.Write(writer);
        }

        /// <summary>
        /// Deserializes the stream id and output parameters structures on document load 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public override bool Read(GH_IReader reader)
        {
            isFromSavedDocument = true;

            reader.TryGetString("STREAM_ID", ref STREAM_ID);

            string myoutparams = null;
            reader.TryGetString("outputParams", ref myoutparams);

            string[] temp = myoutparams.Split(',');
            outputParams = temp.ToList();

            return base.Read(reader);
        }

        /// <summary>
        /// Initializes the socket and its behaviours. 
        /// </summary>
        /// <param name="document"></param>
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            GrasshopperDocument = this.OnPingDocument();

            // expire action
            expireComponentAction = expireComponent;

            myUtils = new SpeckleUtils();

            isLoggedIn = myUtils.hasApiKey();

            mySocket = new Client(myUtils.socketServer.ToString());
            mySocket.RetryConnectionAttempts = 10000;
            mySocket.Error += MySocket_SocketConnectionClosed;
            mySocket.ConnectionRetryAttempt += MySocket_ConnectionRetryAttempt;
            mySocket.Connect();

            mySocket.On("connect", (data) =>
            {
                //this.Message = streamingPaused ? "continous streaming \n OFF" : "continous streaming \n ON";
                retryAttempts = 0;

                mySocket.Emit("authenticate", myUtils.APIKEY as dynamic);

            });

            mySocket.On("authentication-result", data =>
            {
                if (data.Json.Args[0].success == false)
                {
                    MessageBox.Show("Failed authentication!");
                    return;
                }
                else
                {
                    if (STREAM_ID != null)
                    {
                        dynamic payload = new ExpandoObject();
                        payload.streamid = STREAM_ID;
                        payload.role = "receiver";
                        mySocket.Emit("join-stream", payload);
                    }
                }

            });

            mySocket.On("join-stream-result", data =>
            {
                if (data.Json.Args[0].success == false)
                {
                    MessageBox.Show("Error. Server said: " + data.Json.Args[0].message);
                }
                else
                {
                    Debug.WriteLine("Stream joined.");
                    Debug.WriteLine(data);
                    connected = true;
                    NickName = data.Json.Args[0].streamname;

                    mySocket.Emit("pull-stream", null);

                    Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
                }
            });

            mySocket.On("update-clients", (data) =>
            {
                if (streamingPaused) return;

                this.parsedOutput = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.Args[0]);
                parsedOutputStructure = parsedOutput.structure;
                //MessageBox.Show(data.Json.Args[0]);
                Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>Got Data");

                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            });

            mySocket.On("update-sliders", (data) =>
            {
                if (streamingPaused) return;

                this.parsedOutput = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.Args[0]);
                parsedOutputStructure = parsedOutput.structure;
                //MessageBox.Show(data.Json.Args[0]);
                Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>Got Data");

                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            });

            mySocket.On("update-structure", (data) =>
            {
                Debug.WriteLine("Hello new structure names");
                //dynamic temp = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.Args[0]);
                parsedOutputStructure = data.Json.Args[0].structure;

                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            });

            //mySocket.On("", data => { });
        }

        private void MySocket_ConnectionRetryAttempt(object sender, EventArgs e)
        {
            this.Message = "Recconecting... \n" + (++retryAttempts);
            Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
        }

        private void MySocket_SocketConnectionClosed(object sender, EventArgs e)
        {
            this.Message = "Connection Lost.";
            connected = false;
            Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
        }

        private void DocumentServer_DocumentRemoved(GH_DocumentServer sender, GH_Document doc)
        {
            GH_Document GrasshopperDocument = this.OnPingDocument();
            if (GrasshopperDocument == doc)
            {
                mySocket.Close();
                mySocket.Dispose();
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            dynamic data = new System.Dynamic.ExpandoObject();
            data.documentName = GrasshopperDocument.Properties.ProjectFileName + "," + this.Attributes.InstanceGuid.ToString();
            mySocket.Emit("receiver-deleted", data);
            mySocket.Close();
            mySocket.Dispose();
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            GH_DocumentObject.Menu_AppendItem(menu, @"Copy stream ID to Clipboard (" + STREAM_ID + ")", MENU_copyStreamIdToClipboard);
            GH_DocumentObject.Menu_AppendSeparator(menu);
            GH_DocumentObject.Menu_AppendItem(menu, @"View Stream " + STREAM_ID + " online.", MENU_viewStreamOnline);

            //return true;
        }

        private void MENU_copyStreamIdToClipboard(Object sender, EventArgs e)
        {
            System.Windows.Forms.Clipboard.SetText(STREAM_ID);
        }

        private void MENU_viewStreamOnline(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(@"http://46.101.85.144:3001/api/stream/query?streamid=" + STREAM_ID);
        }

        public void connectToRoom()
        {
            mySocket.Connect();
        }

        private void updateDocName()
        {
            dynamic data = new System.Dynamic.ExpandoObject();
            data.role = "receiver";
            data.documentName = GrasshopperDocument.Properties.ProjectFileName + "," + this.Attributes.InstanceGuid.ToString();
            docName = GrasshopperDocument.Properties.ProjectFileName + "," + this.Attributes.InstanceGuid.ToString();
            mySocket.Emit("update-doc-name", data);
        }

        private void expireComponent()
        {
            this.ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Stream key", "KEY", "Which speckle stream do you want to connect to?", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Output", "OUT", "What's been received.");
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, Int32 index)
        {
            return false;
        }
        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, Int32 index)
        {
            return false;
        }
        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, Int32 index)
        {
            return false;
        }
        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, Int32 index)
        {
            return null;
        }

        public void VariableParameterMaintenance()
        {
        }

        public void FixOutputParameters()
        {
            // outputParams -> current guids
            Debug.WriteLine("Trying to fix OUT params");
            if (parsedOutputStructure == null)
                return;
            Debug.WriteLine("Fixing OUT params");
            dynamic OUTStructure = parsedOutputStructure;
            parsedOutputStructure = null; // make sure we run on a fresh one
            // First run instance, when we have no previous record of sent params
            if (outputParams.Count <= 0)
            {
                Debug.WriteLine("outputParams.Count <= 0");
                // unregister any output params that are already there
                for (int i = Params.Output.Count - 1; i >= 0; i--)
                {
                    Params.UnregisterOutputParameter(Params.Output[i], true);
                }
                // add all the new ones
                foreach (dynamic structureElement in OUTStructure)
                {
                    Param_GenericObject myParam = new Param_GenericObject();
                    myParam.Name = myParam.NickName = structureElement.name;
                    myParam.MutableNickName = false;
                    myParam.Access = GH_ParamAccess.tree;
                    Params.RegisterOutputParam(myParam);
                    outputParams.Add(structureElement.guid.ToString());
                }
                return;
            }

            // Normal run instance: let's diff and smart merge/remove
            List<string> newParams = new List<string>();
            foreach (dynamic structureElement in OUTStructure)
            {
                newParams.Add(structureElement.guid.ToString());
            }

            // simple case: counts equal, just check for name changes. guids should not change
            // actually this should be run everytime; after the operations below we should
            // always have newparams.count == oldparams.count, no? 
            // to think about! 

            if (OUTStructure.Count == Params.Output.Count)
            {
                Debug.WriteLine("parsedOutput.args[0].outputStructure.Count == Params.Output.Count");
                bool changed = false;
                int count = 0;
                foreach (dynamic structureElement in OUTStructure)
                {
                    if (structureElement.name != Params.Output[count].Name)
                    {
                        Params.Output[count].Name = Params.Output[count].NickName = structureElement.name;
                        changed = true;
                    }
                    count++;
                }
                //if (changed)
                //  Params.OnParametersChanged();
                Debug.WriteLine("Changed: " + changed);
                return;
            }

            var inOldOnly = outputParams.Except(newParams).ToList();
            var inNewOnly = newParams.Except(outputParams).ToList();
            // old count < new count
            // inserting parameters at index. hope this works. 
            if (outputParams.Count < newParams.Count)
            {
                Debug.WriteLine("outputParams.Count < newParams.Count");
                int count = 0;
                foreach (dynamic structureElement in OUTStructure)
                {
                    string myguid = structureElement.guid;
                    if (inNewOnly.Contains(myguid))
                    {
                        // insert param
                        Param_GenericObject myParam = new Param_GenericObject();
                        myParam.Name = myParam.NickName = structureElement.name;
                        myParam.MutableNickName = false;
                        myParam.Access = GH_ParamAccess.tree;
                        Params.RegisterOutputParam(myParam, count);
                    }
                    count++;
                }

                outputParams = newParams;
                Params.OnParametersChanged();
                return;
            }
            // old count > new count
            // removing parameters that don't match
            if (outputParams.Count > newParams.Count)
            {
                Debug.WriteLine("outputParams.Count > newParams.Count");
                int count = 0;
                List<IGH_Param> toRemove = new List<IGH_Param>();
                foreach (string myguid in outputParams)
                {
                    if (inOldOnly.Contains(myguid))
                    {
                        toRemove.Add(Params.Output[count]);
                    }
                    count++;
                }

                foreach (IGH_Param myParam in toRemove)
                {
                    Params.UnregisterOutputParameter(myParam, true);
                }

                outputParams = newParams;
                //Params.OnParametersChanged();
                return;
            }

        }

        public override void ExpireSolution(bool recompute)
        {
            Debug.WriteLine("HELP I:M DYIN' " + isFromSavedDocument + " <<<<< "); // woot does dis do? don't remember
            if (!isFromSavedDocument)
            {
                FixOutputParameters();
                Params.OnParametersChanged();
            }
            base.ExpireSolution(recompute);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            isFromSavedDocument = false;

            if (isLoggedIn == false)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "API key is missing. It's ok to receive.");
            }

            string inputRoom = null;
            DA.GetData(0, ref inputRoom);

            if (inputRoom == null)
                return;

            if (inputRoom != STREAM_ID)
            {
                STREAM_ID = inputRoom;
                outputParams = new List<string>();

                dynamic payload = new ExpandoObject();
                payload.streamid = STREAM_ID;
                payload.role = "receiver";
                mySocket.Emit("join-stream", payload);
                Debug.WriteLine("jon stream " + STREAM_ID + " lol ");
            }

            if (parsedOutput == null)
                return;

            //this.Message += "\n " + lastReceive.ToShortDateString();

            int totalCount = 0;
            int structureindex = 0;

            string types = "";

            // jeeesus there must be a better way of doing this
            // k this works here, let's see in the server components - big up fucked up shait
            if (parsedOutput.structure.Count == Params.Output.Count)
                foreach (dynamic structure in parsedOutput.structure)
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
                                    try
                                    {
                                        myObj = StreamConverter.castToGH(parsedOutput.objects[totalCount + i].type.ToString(), parsedOutput.objects[totalCount + i].value);
                                    }
                                    catch
                                    {
                                        myObj = "Converter Error or Out of Bounds.";
                                    }

                                    myTree.EnsurePath(myPath).Add(myObj);

                                }

                                totalCount += elCount;
                            }
                        }

                    DA.SetDataTree(structureindex, myTree);
                    structureindex++;
                }

            types = totalCount + ", " + parsedOutput.objects.Count;

            if (GrasshopperDocument.Properties.ProjectFileName + "," + this.Attributes.InstanceGuid.ToString() != docName)
            {
                updateDocName();
            }

            mySocket.Emit("received-stream", null);
            lastReceive = DateTime.Now;

        }

        public void startPullStream()
        {
            // TODO: handle with sockets
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
            get { return new Guid("{1b295718-a0e6-4c59-95a0-f393ffb92e16}"); }
        }
    }
}