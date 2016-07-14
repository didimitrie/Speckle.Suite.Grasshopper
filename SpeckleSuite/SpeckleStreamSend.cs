using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using System.Windows.Forms;
using SocketIOClient;
using GH_IO.Serialization;
using Grasshopper.Kernel.Parameters;
using System.Dynamic;
using Grasshopper.Kernel.Special;
using System.Linq;

namespace SpeckleSuite
{
    public class SpeckleStreamSend : GH_Component, IGH_VariableParameterComponent
    {

        public Client mySocket = null;
        GH_Document GrasshopperDocument;

        SpeckleUtils myUtils;

        //connect params to sliders

        private List<GH_NumberSlider> ParamSliderObj;
        private List<bool> ParamIsSlider;
        private List<int> ParamSliderIndex;

        public bool connected;
        public string STREAM_ID = null;

        public bool streamingPaused = false;
        public bool pushStream = false;
        public bool isLoggedIn = false;

        public int retryAttempts = 0;

        public string streamName = null;
        public string docName = null;
        public string paramnames = "";

        public System.Timers.Timer SEND_TIMER;
        public System.Timers.Timer NAME_TIMER;
        public System.Timers.Timer STRUCTURE_TIMER;

        string sendDataBucket = null;
        dynamic sendStructureBucket = null;

        Action expireComponentAction;

        public SpeckleStreamSend()
          : base("Speckle Stream Sender", "StreamSender",
              "Stream data as a speckle stream.",
              "Params", "SpeckleSuite")
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new SpeckleStreamSendAttr(this);
        }

        /// <summary>
        /// Serializes the stream id on document save (also copy paste, which introduces a bug)
        /// </summary>
        /// <param name="writer"></param>
        /// <returns></returns>
        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("STREAM_ID", STREAM_ID);
            return base.Write(writer);
        }

        /// <summary>
        /// Deserializes the stream id on document load (also copy paste, which introduces a bug)
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public override bool Read(GH_IReader reader)
        {
            reader.TryGetString("STREAM_ID", ref STREAM_ID);
            return base.Read(reader);
        }

        /// <summary>
        /// Initializes the socket and its behaviours. 
        /// Initializes the debouncing timers.
        /// </summary>
        /// <param name="document"></param>
        public override void AddedToDocument(GH_Document document)
        {
            // set the gh doc
            GrasshopperDocument = this.OnPingDocument();

            this.ObjectChanged += SpeckleStreamSend_ObjectChanged;

            // paste check: if pasted, nullify the stream id so we have to create a new room. 
            // TODO: doesn't work between documents
            if (STREAM_ID != null)
            {
                List<IGH_ActiveObject> myobjs = GrasshopperDocument.ActiveObjects();
                foreach (IGH_ActiveObject mycomp in myobjs)
                {
                    SpeckleStreamSend test = mycomp as SpeckleStreamSend;
                    if ((test != null) && (test != this))
                    {
                        if (STREAM_ID == test.STREAM_ID)
                        {
                            STREAM_ID = null;
                        }
                    }
                }
            }

            // expire action
            expireComponentAction = expireComponent;

            // emit debouncing
            SEND_TIMER = new System.Timers.Timer(250);
            SEND_TIMER.Elapsed += SEND_TIMER_Elapsed;
            SEND_TIMER.AutoReset = false;
            SEND_TIMER.Enabled = false;

            NAME_TIMER = new System.Timers.Timer(250);
            NAME_TIMER.Elapsed += NAME_TIMER_Elapsed;
            NAME_TIMER.AutoReset = false;
            NAME_TIMER.Enabled = false;

            STRUCTURE_TIMER = new System.Timers.Timer(250);
            STRUCTURE_TIMER.Elapsed += STRUCTURE_TIMER_Elapsed;
            STRUCTURE_TIMER.AutoReset = false;
            STRUCTURE_TIMER.Enabled = false;

            // handle document closed
            Grasshopper.Instances.DocumentServer.DocumentRemoved += DocumentServer_DocumentRemoved;

            // utils (should be a static class)
            myUtils = new SpeckleUtils();
            isLoggedIn = myUtils.hasApiKey();

            // socket setup
            mySocket = new Client(myUtils.socketServer.ToString());
            mySocket.RetryConnectionAttempts = 10000;
            mySocket.Error += MySocket_SocketConnectionClosed;
            mySocket.ConnectionRetryAttempt += MySocket_ConnectionRetryAttempt;
            mySocket.Connect();

            // socket events
            mySocket.On("connect", (data) =>
            {
                this.Message = streamingPaused ? "continous streaming \n OFF" : "continous streaming \n ON";
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
                        payload.role = "emitter";
                        mySocket.Emit("join-stream", payload);

                    }
                    else
                    {
                        mySocket.Emit("create-stream", null);
                    }
                }

            });

            mySocket.On("create-stream-result", (data) =>
            {

                if (data.Json.Args[0].success == false)
                {
                    MessageBox.Show("Failed to create stream!");
                }
                else
                {
                    STREAM_ID = data.Json.Args[0].streamid;
                    dynamic payload = new ExpandoObject();

                    payload.streamid = STREAM_ID;
                    payload.role = "emitter";
                    mySocket.Emit("join-stream", payload);

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
                    connected = true;
                    this.NickName = data.Json.Args[0].streamname;
                    Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
                }
            });

            // reinstate events for the parameters
            foreach (IGH_Param myparam in Params.Input)
                myparam.ObjectChanged += Param_ObjectChanged;


            base.AddedToDocument(document);
        }

        /// <summary>
        /// Timers for debouncing data sending. We don't want to burn the server, do we?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void STRUCTURE_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            mySocket.Emit("update-structure", sendStructureBucket);
            STRUCTURE_TIMER.Stop();
        }

        private void NAME_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            mySocket.Emit("update-name", this.NickName);
            NAME_TIMER.Stop();
        }

        private void SEND_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("sending data.");
            mySocket.Emit("update-stream", sendDataBucket);
            SEND_TIMER.Stop();
        }

        private void SpeckleStreamSend_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            NAME_TIMER.Start();
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
            GrasshopperDocument = this.OnPingDocument();
            if (GrasshopperDocument == doc)
            {
                mySocket.Emit("document-closed", STREAM_ID);
                mySocket.Close();
                mySocket.Dispose();
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            mySocket.Emit("delete-stream", STREAM_ID);

            mySocket.Close();
            mySocket.Dispose();

            base.RemovedFromDocument(document);
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

        private void expireComponent()
        {
            this.ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("A", "A", "Things to be streamed. Change the parameter name for easier identification.", GH_ParamAccess.tree);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("KEY", "KEY", "KEY (Stream ID). Data can be received at this id.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.SetData(0, STREAM_ID);

            if (!myUtils.hasApiKey())
            {
                this.isLoggedIn = false;
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "You are not logged in.");
                return;
            }

            if (!connected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Not connected to server.");
                return;
            }

            if (streamingPaused && !pushStream) return;

            //try to see if input params are sliders
            try
            {
                this.ParamSliderObj = new List<GH_NumberSlider>();
                this.ParamIsSlider = new List<bool>();
                this.ParamSliderIndex = new List<int>();
                int k = 0;
                IList<IGH_Param> sources = this.Params.Input;
                if (sources.Any<IGH_Param>())
                {
                    foreach (IGH_Param source in sources)
                    {
                        try
                        {
                            GH_NumberSlider gHNumberSlider = source.Sources[0] as GH_NumberSlider;
                            if (gHNumberSlider != null)
                            {
                                ParamSliderObj.Add(gHNumberSlider);
                                this.ParamIsSlider.Add(true);
                                this.ParamSliderIndex.Add(k);
                            }
                            else
                            {
                                this.ParamIsSlider.Add(false);
                                this.ParamSliderIndex.Add(-1);
                            }
                            k++;
                        }
                        catch (Exception exception1)
                        {
                        }
                    }
                }
            }
            catch (Exception exception2)
            {
            }
            //end try to get sliders in params.

            dynamic sendEventData = new System.Dynamic.ExpandoObject();
            sendEventData.objects = new List<dynamic>();

            List<dynamic> structure = new List<dynamic>();
            int i = 0;
            foreach (IGH_Param param in this.Params.Input)
            {
                dynamic structureItem = new System.Dynamic.ExpandoObject();
                structureItem.name = param.NickName == null ? param.Name : param.NickName;
                structureItem.guid = param.InstanceGuid.ToString();
                structureItem.count = 0;
                structureItem.topology = getParamTopology(param);
                //if param is slider then get slider data
                if (ParamIsSlider[i])
                {
                    
                    structureItem.count++;
                    dynamic returnObject = new System.Dynamic.ExpandoObject();
                    returnObject.groupName = (string)structureItem.name;
                    returnObject.type = "GH_Slider";
                    returnObject.value = ParamSliderObj[i].Slider.Value;
                    returnObject.Min = ParamSliderObj[i].Slider.Minimum;
                    returnObject.Max = ParamSliderObj[i].Slider.Maximum;
                    //returnObject.Step = this.stepSize(In_SliderList[i]);
                    sendEventData.objects.Add(returnObject);
                }
                else { 
                foreach (Object myObj in param.VolatileData.AllData(true))
                {
                    structureItem.count++;
                    sendEventData.objects.Add(StreamConverter.castFromGH(myObj, (string)structureItem.name));
                }
                }
                structure.Add(structureItem as dynamic);
                i++;
            }

            sendEventData.structure = structure;
            dynamic toSend = sendEventData;

            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(sendEventData);
            sendDataBucket = serialized;

            if (pushStream)
            {
                pushStream = false;
                System.Diagnostics.Debug.WriteLine("Pushed data.");
                mySocket.Emit("update-stream", sendDataBucket);
            }
            else
                SEND_TIMER.Start();

            if (GrasshopperDocument.Properties.ProjectFileName + "," + this.Attributes.InstanceGuid.ToString() != docName)
            {
                //updateDocName();
            }
        }

        public string getParamTopology(IGH_Param param)
        {
            string topology = "";
            foreach (Grasshopper.Kernel.Data.GH_Path mypath in param.VolatileData.Paths)
            {
                topology += mypath.ToString(false) + "-" + param.VolatileData.get_Branch(mypath).Count + " ";
            }
            //System.Diagnostics.Debug.WriteLine(topology);
            return topology;
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
            get { return new Guid("{86b89a25-ddaf-457f-940e-697024f7bc76}"); }
        }

        #region special inputs

        public void updateStreamStructure()
        {
            dynamic sendEventData = new ExpandoObject();
            List<dynamic> inputStructure = new List<dynamic>();
            string newparamnames = "";

            for (int i = 0; i < Params.Input.Count; i++)
            {
                IGH_Param param = Params.Input[i];
                dynamic structureItem = new System.Dynamic.ExpandoObject();
                structureItem.name = param.NickName == null ? param.Name : param.NickName;

                newparamnames += structureItem.name;

                structureItem.guid = param.InstanceGuid.ToString();
                structureItem.count = 0;
                structureItem.topology = getParamTopology(param);
                inputStructure.Add(structureItem as dynamic);
            }

            if (paramnames == newparamnames) return; // same old same old, does this actually happen??? yes on load i think

            paramnames = newparamnames;
            sendEventData.structure = inputStructure;

            sendStructureBucket = sendEventData;

            STRUCTURE_TIMER.Start();
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index)
        {
            //We can only remove from the input
            if (side == GH_ParameterSide.Input && Params.Input.Count > 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index)
        {
            Grasshopper.Kernel.Parameters.Param_GenericObject param = new Param_GenericObject();

            param.Name = GH_ComponentParamServer.InventUniqueNickname("ABCDEFGHIJKLMNOPQRSTUVWXYZ", Params.Input);
            param.NickName = param.Name;
            param.Description = "Things to be streamed.";
            param.Optional = true;
            param.Access = GH_ParamAccess.tree;
            param.ObjectChanged += Param_ObjectChanged;

            return param;
        }

        private void Param_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            updateStreamStructure();
        }

        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index)
        {
            //Nothing to do here by the moment
            return true;
        }

        void IGH_VariableParameterComponent.VariableParameterMaintenance()
        {
            //Nothing to do here by the moment
        }

        #endregion
    }
}