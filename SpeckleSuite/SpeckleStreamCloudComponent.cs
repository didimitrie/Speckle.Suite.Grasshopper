//using System;
//using System.Collections.Generic;

//using Grasshopper.Kernel;
//using Rhino.Geometry;
//using System.Windows.Forms;
//using SocketIOClient;
//using System.IO;
//using Grasshopper.Kernel.Types;
//using GH_IO.Serialization;
//using Grasshopper.GUI.Canvas;
//using Grasshopper.Kernel.Parameters;
//using Grasshopper.GUI;
//using System.Dynamic;
//using System.Linq;
//using System.Diagnostics;

//namespace SpeckleSuite
//{
//    public class SpeckleStreamCloudComponent : GH_Component, IGH_VariableParameterComponent
//    {

//        public Client mySocket;
//        SpeckleUtils myUtils;

//        public string clientId = null, componentId = null;
//        private GH_Document GrasshopperDocument;
//        private bool isLoggedIn;

//        public List<string> outputParams = new List<string>();
//        public List<string> inputParams = new List<string>();

//        List<List<object>> buffer;

//        dynamic parsedInputStructure = null, parsedOutputStructure = null;
//        public bool inputChanged = false;

//        public bool streamingPaused = true;
//        public bool sendRequest = false;
//        string prevPayload;

//        dynamic parsedOutput = null;

//        public bool isFromSavedDocument = false;

//        Action expireComponentAction;

//        /// <summary>
//        /// Initializes a new instance of the SpeckleStreamServerClient class.
//        /// </summary>
//        public SpeckleStreamCloudComponent()
//          : base("SpeckleStreamCloudComponent", "Nickname",
//              "Description",
//              "Params", "Speckle Server")
//        {
//        }

//        public override void CreateAttributes()
//        {
//            m_attributes = new SpeckleStreamCloudComponentAttr(this);
//        }

//        public override bool Write(GH_IWriter writer)
//        {
//            writer.SetString("clientId", clientId);
//            writer.SetString("componentId", componentId);
//            writer.SetString("outputParams", String.Join(",", outputParams.ToArray()));
//            writer.SetString("inputParams", String.Join(",", inputParams.ToArray()));
//            return base.Write(writer);
//        }

//        public override bool Read(GH_IReader reader)
//        {
//            isFromSavedDocument = true;

//            clientId = null;
//            reader.TryGetString("clientId", ref clientId);

//            componentId = null;
//            reader.TryGetString("componentId", ref componentId);



//            outputParams = null; string temp = null;
//            reader.TryGetString("outputParams", ref temp);
//            outputParams = temp.Split(',').ToList();

//            inputParams = null; string temp2 = null;
//            reader.TryGetString("inputParams", ref temp2);
//            inputParams = temp2.Split(',').ToList();

//            Debug.WriteLine(temp);
//            Debug.WriteLine(temp2);

//            return base.Read(reader);
//        }

//        private void expireComponent()
//        {
//            this.ExpireSolution(true);
//        }

//        public override void ExpireSolution(bool recompute)
//        {
//            if (!isFromSavedDocument)
//            {
//                FixOutputParameters();
//                FixInputParameters();
//                Params.OnParametersChanged();
//            }
//            base.ExpireSolution(recompute);
//        }

//        public override void AddedToDocument(GH_Document document)
//        {
//            expireComponentAction = expireComponent;

//            GrasshopperDocument = this.OnPingDocument();
//            myUtils = new SpeckleUtils();

//            mySocket = new Client(myUtils.socketServer.ToString());
//            mySocket.RetryConnectionAttempts = 1000;

//            clientId = null;

//            mySocket.On("connect", (data) =>
//            {
//                if (clientId == null)
//                    mySocket.Emit("get-socket-id", componentId);
//                else
//                    mySocket.Emit("client-rejoined", clientId);

//            });

//            mySocket.On("receive-id", (data) =>
//            {
//                clientId = data.Json.Args[0];
//                //MessageBox.Show(clientId + " received client id. Server id is " + componentId);
//            });

//            mySocket.On("client-receive-structure-input", (data) =>
//            {
//                //MessageBox.Show("INPUT " + data.Json.ToJsonString());
//                dynamic temp = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.ToJsonString());

//                parsedInputStructure = temp.args[0].inputStructure;

//                Debug.WriteLine("received INPUT structure");
//                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
//            });

//            mySocket.On("client-receive-structure-output", (data) =>
//            {
//                //MessageBox.Show("OUTPUT " + data.Json.ToJsonString());
//                dynamic temp = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.ToJsonString());

//                parsedOutputStructure = temp.args[0].outputStructure;

//                Debug.WriteLine("received OUTPUT structure");
//                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
//            });

//            mySocket.On("component-result", (data) =>
//            {
//                this.parsedOutput = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data.Json.ToJsonString());
//                inputChanged = true;
//                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
//            });

//            isLoggedIn = myUtils.hasApiKey();

//            if (!isLoggedIn)
//                isLoggedIn = myUtils.promptForApiKey();
//            else
//                mySocket.Connect();

//            base.AddedToDocument(document);

//        }

//        public override void RemovedFromDocument(GH_Document document)
//        {
//            Debug.WriteLine("Client " + this.clientId + " deleted");
//            mySocket.Close();
//            mySocket.Dispose();
//            base.RemovedFromDocument(document);
//        }

//        public void getcomponentId()
//        {
//            string value = "component id";
//            if (SpeckleUtils.InputBox("Component Id", "Input below the component id", ref value) == DialogResult.OK)
//            {

//                componentId = value;
//                outputParams = new List<string>();
//                inputParams = new List<string>();

//                Debug.WriteLine("server id set to " + componentId + ", \n unrgesitering all parameters");

//                for (int i = Params.Output.Count - 1; i >= 0; i--)
//                {
//                    Params.UnregisterOutputParameter(Params.Output[i], true);
//                }

//                for (int i = Params.Input.Count - 1; i >= 0; i--)
//                {
//                    Params.UnregisterInputParameter(Params.Input[i], true);
//                }

//                Params.OnParametersChanged();

//                this.Name = this.NickName = componentId + " > Cloud";

//                mySocket.Emit("get-component-structure", componentId);

//            }
//        }

//        /// <summary>
//        /// Registers all the input parameters for this component.
//        /// </summary>
//        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
//        {
//            pManager.AddTextParameter(">>> IN", ">>> IN", "Request parameters.", GH_ParamAccess.tree);
//            Params.Input[0].Optional = true;
//        }

//        /// <summary>
//        /// Registers all the output parameters for this component.
//        /// </summary>
//        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
//        {
//            pManager.AddTextParameter("OUT <<< ", "OUT <<< ", "Request results.", GH_ParamAccess.tree);
//        }

//        /// <summary>
//        /// This is the method that actually does the work.
//        /// </summary>
//        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
//        protected override void SolveInstance(IGH_DataAccess DA)
//        {
//            Debug.WriteLine("Solve instance > starts");
//            isFromSavedDocument = false;
//            if (componentId == null || componentId == "")
//            {
//                Debug.WriteLine("server id is " + componentId);
//                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No server id set.");
//                return;
//            }

//            if (parsedOutput != null && inputChanged)
//            {
//                Debug.WriteLine("Setting fresh data.");
//                //inputChanged = false;
//                List<List<object>> mainoutlist = new List<List<object>>();
//                for (int i = 0; i < Params.Output.Count; i++)
//                {
//                    mainoutlist.Add(new List<object>());
//                }

//                foreach (dynamic obj in parsedOutput.args[0].objects)
//                {
//                    int which = Convert.ToInt16((string)obj.groupName);
//                    if (mainoutlist[which] == null) mainoutlist[which] = new List<object>();
//                    mainoutlist[which].Add(StreamConverter.castToGH((string)obj.type, (string)obj.value));
//                }

//                for (int i = 0; i < mainoutlist.Count; i++)
//                {
//                    DA.SetDataList(i, mainoutlist[i]);
//                }

//                buffer = mainoutlist;
//                parsedOutput = null; // reset 
//            }
//            else
//            if (buffer != null)
//            {
//                Debug.WriteLine("Setting buffer data.");
//                int i = 0;
//                if(Params.Output.Count != buffer.Count)
//                {
//                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Structure mismatch.");
//                }else 
//                foreach (List<object> myList in buffer)
//                    DA.SetDataList(i++, myList);
//            }

//            if (streamingPaused && !sendRequest) return;
//            sendRequest = false;
//            emitRequest();

//        }

//        public void emitRequest()
//        {
//            if (componentId == null || componentId == "") return;
//            if (clientId == null || clientId == "") return;

//            dynamic payload = new System.Dynamic.ExpandoObject();
//            payload.objects = new List<dynamic>();
//            payload.componentId = componentId;
//            payload.requestId = clientId;

//            List<dynamic> structure = new List<dynamic>();
//            int count = 0;
//            foreach (IGH_Param param in this.Params.Input)
//            {
//                //var name = param.NickName == null ? param.Name : param.NickName;  
//                foreach (Object myObj in param.VolatileData.AllData(true))
//                {
//                    payload.objects.Add(StreamConverter.castFromGH(myObj, count.ToString()));
//                }
//                count++;
//            }

//            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
//            if (prevPayload == serialized) return;
//            prevPayload = serialized;

//            mySocket.Emit("component-request", StreamConverter.Base64Encode(serialized));
//        }

//        public bool outisFixed = false, inisFixed = false;

//        public void FixInputParameters()
//        {
//            if (parsedInputStructure == null)
//                return;

//            Debug.WriteLine("Fixing IN params");
//            dynamic INStructure = parsedInputStructure;
//            parsedInputStructure = null;
//            // First run instance, when we have no previous record of sent params
//            if (inputParams.Count <= 0)
//            {
//                Debug.WriteLine("inputParams.Count <= 0");
//                // unregister any output params that are already there
//                for (int i = Params.Input.Count - 1; i >= 0; i--)
//                {
//                    Params.UnregisterInputParameter(Params.Input[i], true);
//                }
//                // add all the new ones
//                foreach (dynamic structureElement in INStructure)
//                {
//                    Param_GenericObject myParam = new Param_GenericObject();

//                    myParam.Name = myParam.NickName = structureElement.name;
//                    myParam.Description = "Expects a " + structureElement.access;
//                    myParam.MutableNickName = false;
//                    myParam.Optional = true;

//                    // smarty pants eh? 
//                    //if (structureElement.access == "item")
//                    //    myParam.Access = GH_ParamAccess.item;
//                    //else if (structureElement.access == "list")
//                    //    myParam.Access = GH_ParamAccess.list;
//                    //else
//                        myParam.Access = GH_ParamAccess.tree;

//                    Params.RegisterInputParam(myParam);
//                    inputParams.Add(myParam.InstanceGuid.ToString());
//                }
//                //Params.OnParametersChanged();
//                return;
//            }

//            // Normal run instance: let's diff and smart merge/remove
//            List<string> newParams = new List<string>();
//            foreach (dynamic structureElement in INStructure)
//            {
//                newParams.Add(structureElement.guid.ToString());
//            }

//            // simple case: counts equal, just check for name changes. guids should not change
//            // actually this should be run everytime; after the operations below we should
//            // always have newparams.count == oldparams.count, no? 
//            // to think about! 

//            if (INStructure.Count == Params.Input.Count)
//            {
//                Debug.WriteLine("INStructure.Count == Params.Input.Count");
//                bool changed = false;
//                int count = 0;
//                foreach (dynamic structureElement in INStructure)
//                {
//                    if (structureElement.name != Params.Input[count].Name)
//                    {
//                        Params.Input[count].Name = Params.Input[count].NickName = structureElement.name;
//                        changed = true;
//                    }
//                    count++;
//                }
//                Debug.WriteLine("Changed: " + changed);
//                //if (changed)
//                //Params.OnParametersChanged();
//                return;
//            }

//            var inOldOnly = inputParams.Except(newParams).ToList();
//            var inNewOnly = newParams.Except(inputParams).ToList();
//            // old count < new count
//            // inserting parameters at index. hope this works. 
//            if (inputParams.Count < newParams.Count)
//            {
//                Debug.WriteLine("inputParams.Count < newParams.Count");
//                int count = 0;
//                foreach (dynamic structureElement in INStructure)
//                {
//                    string myguid = structureElement.guid;
//                    if (inNewOnly.Contains(myguid))
//                    {
//                        // insert param
//                        Param_GenericObject myParam = new Param_GenericObject();
//                        myParam.Name = myParam.NickName = structureElement.name;
//                        myParam.MutableNickName = false;
//                        myParam.Access = GH_ParamAccess.tree;
//                        myParam.Optional = true;
//                        Params.RegisterInputParam(myParam, count);
//                    }
//                    count++;
//                }

//                inputParams = newParams;
//                //Params.OnParametersChanged();
//                return;
//            }
//            // old count > new count
//            // removing parameters that don't match
//            if (inputParams.Count > newParams.Count)
//            {
//                Debug.WriteLine("inputParams.Count > newParams.Count");
//                int count = 0;
//                List<IGH_Param> toRemove = new List<IGH_Param>();
//                foreach (string myguid in inputParams)
//                {
//                    if (inOldOnly.Contains(myguid))
//                    {
//                        toRemove.Add(Params.Input[count]);
//                    }
//                    count++;
//                }

//                foreach (IGH_Param myParam in toRemove)
//                {
//                    Params.UnregisterInputParameter(myParam, true);
//                }

//                inputParams = newParams;
//                //Params.OnParametersChanged();
//                return;
//            }
//        }

//        public void FixOutputParameters()
//        {
//            // outputParams -> current guids
//            if (parsedOutputStructure == null)
//                return;
//            Debug.WriteLine("Fixing OUT params");
//            dynamic OUTStructure = parsedOutputStructure;
//            parsedOutputStructure = null; // make sure we run on a fresh one
//            // First run instance, when we have no previous record of sent params
//            if (outputParams.Count <= 0)
//            {
//                Debug.WriteLine("outputParams.Count <= 0");
//                // unregister any output params that are already there
//                for (int i = Params.Output.Count - 1; i >= 0; i--)
//                {
//                    Params.UnregisterOutputParameter(Params.Output[i], true);
//                }
//                // add all the new ones
//                foreach (dynamic structureElement in OUTStructure)
//                {
//                    Param_GenericObject myParam = new Param_GenericObject();
//                    myParam.Name = myParam.NickName = structureElement.name;
//                    myParam.MutableNickName = false;
//                    myParam.Access = GH_ParamAccess.tree;
//                    Params.RegisterOutputParam(myParam);
//                    outputParams.Add(myParam.InstanceGuid.ToString());
//                }
//                return;
//            }

//            // Normal run instance: let's diff and smart merge/remove
//            List<string> newParams = new List<string>();
//            foreach (dynamic structureElement in OUTStructure)
//            {
//                newParams.Add(structureElement.guid.ToString());
//            }

//            // simple case: counts equal, just check for name changes. guids should not change
//            // actually this should be run everytime; after the operations below we should
//            // always have newparams.count == oldparams.count, no? 
//            // to think about! 

//            if (OUTStructure.Count == Params.Output.Count)
//            {
//                Debug.WriteLine("parsedOutput.args[0].outputStructure.Count == Params.Output.Count");
//                bool changed = false;
//                int count = 0;
//                foreach (dynamic structureElement in OUTStructure)
//                {
//                    if (structureElement.name != Params.Output[count].Name)
//                    {
//                        Params.Output[count].Name = Params.Output[count].NickName = structureElement.name;
//                        changed = true;
//                    }
//                    count++;
//                }
//                //if (changed)
//                //  Params.OnParametersChanged();
//                Debug.WriteLine("Changed: " + changed);
//                return;
//            }

//            var inOldOnly = outputParams.Except(newParams).ToList();
//            var inNewOnly = newParams.Except(outputParams).ToList();
//            // old count < new count
//            // inserting parameters at index. hope this works. 
//            if (outputParams.Count < newParams.Count)
//            {
//                Debug.WriteLine("outputParams.Count < newParams.Count");
//                int count = 0;
//                foreach (dynamic structureElement in OUTStructure)
//                {
//                    string myguid = structureElement.guid;
//                    if (inNewOnly.Contains(myguid))
//                    {
//                        // insert param
//                        Param_GenericObject myParam = new Param_GenericObject();
//                        myParam.Name = myParam.NickName = structureElement.name;
//                        myParam.MutableNickName = false;
//                        myParam.Access = GH_ParamAccess.tree;
//                        Params.RegisterOutputParam(myParam, count);
//                    }
//                    count++;
//                }

//                outputParams = newParams;
//                Params.OnParametersChanged();
//                return;
//            }
//            // old count > new count
//            // removing parameters that don't match
//            if (outputParams.Count > newParams.Count)
//            {
//                Debug.WriteLine("outputParams.Count > newParams.Count");
//                int count = 0;
//                List<IGH_Param> toRemove = new List<IGH_Param>();
//                foreach (string myguid in outputParams)
//                {
//                    if (inOldOnly.Contains(myguid))
//                    {
//                        toRemove.Add(Params.Output[count]);
//                    }
//                    count++;
//                }

//                foreach (IGH_Param myParam in toRemove)
//                {
//                    Params.UnregisterOutputParameter(myParam, true);
//                }

//                outputParams = newParams;
//                //Params.OnParametersChanged();
//                return;
//            }

//        }

//        public bool CanInsertParameter(GH_ParameterSide side, int index)
//        {
//            return false;
//        }

//        public bool CanRemoveParameter(GH_ParameterSide side, int index)
//        {
//            return false;
//        }

//        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
//        {
//            return null;
//        }

//        public bool DestroyParameter(GH_ParameterSide side, int index)
//        {
//            return false;
//        }

//        public void VariableParameterMaintenance()
//        {
//            return;
//        }

//        /// <summary>
//        /// Provides an Icon for the component.
//        /// </summary>
//        protected override System.Drawing.Bitmap Icon
//        {
//            get
//            {
//                //You can add image files to your project resources and access them like this:
//                // return Resources.IconForThisComponent;
//                return null;
//            }
//        }

//        /// <summary>
//        /// Gets the unique ID for this component. Do not change this ID after release.
//        /// </summary>
//        public override Guid ComponentGuid
//        {
//            get { return new Guid("{53057d79-922d-4e9a-9693-824eda3c610c}"); }
//        }
//    }
//}