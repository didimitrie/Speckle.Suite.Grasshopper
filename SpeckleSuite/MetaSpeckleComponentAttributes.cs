//using Grasshopper.Kernel;
//using System.Drawing;

//namespace SpeckleSuite
//{
//    internal class MetaSpeckleComponentAttributes : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
//    {
//        private MetaSpeckle owner;
//        private RectangleF bounds;

//        public override RectangleF Bounds
//        {
//            get
//            {
//                return this.bounds;
//            }
//            set
//            {
//                this.bounds = value;
//            }
//        }

//        protected override void Layout()
//        {
//            base.Layout();
//            this.bounds.Inflate(new SizeF(0f, 40f));
//        }

//        public MetaSpeckleComponentAttributes(MetaSpeckle owner) : base(owner)
//        {
//            this.owner = owner;
//        }

//        public override Grasshopper.GUI.Canvas.GH_ObjectResponse RespondToMouseDoubleClick(Grasshopper.GUI.Canvas.GH_Canvas sender, Grasshopper.GUI.GH_CanvasMouseEvent e)
//        {
//            if ((ContentBox.Contains(e.CanvasLocation)))
//            {
//                owner.startUploadThread();
//                return Grasshopper.GUI.Canvas.GH_ObjectResponse.Handled;
//            }
//            return Grasshopper.GUI.Canvas.GH_ObjectResponse.Ignore;
//        }
//    }
//}