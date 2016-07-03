//using Grasshopper.GUI;
//using Grasshopper.GUI.Canvas;
//using Grasshopper.Kernel;
//using System;
//using System.Drawing;
//using System.Windows.Forms;

//namespace SpeckleSuite
//{
//    internal class SpeckleStreamCloudComponentAttr : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
//    {
//        private SpeckleStreamCloudComponent owner;
//        private Rectangle SendStreamButtonBounds;
//        private Rectangle PlayPauseButtonBounds;
//        private Rectangle SetServerIdButtonBounds;

//        public SpeckleStreamCloudComponentAttr(SpeckleStreamCloudComponent owner) : base(owner)
//        {
//            this.owner = owner;
//        }

//        protected override void Layout()
//        {
//            base.Layout();
//            Rectangle rec0 = GH_Convert.ToRectangle(Bounds);
//            rec0.Height += 22;

//            Rectangle rec1 = rec0;
//            rec1.Y = rec1.Bottom - 22;
//            rec1.Height = 22;
//            rec1.Inflate(-5, -4);

//            Bounds = rec0;
//            PlayPauseButtonBounds = rec1;

//            if (owner.streamingPaused)
//            {
//                rec0.Height += 22;
//                Rectangle rec2 = rec0;
//                rec2.Y = rec2.Bottom - 24;
//                rec2.Height = 22;
//                rec2.Inflate(-5, -4);
//                Bounds = rec0;
//                SendStreamButtonBounds = rec2;
//            } else { SendStreamButtonBounds = new Rectangle(0,0,0,0); }

//            rec0.Height += 22;
//            Rectangle rec3 = rec0;
//            rec3.Height = 22;
//            rec3.Y = rec0.Bottom - (owner.streamingPaused ? 24 : 23);
//            rec3.Inflate(4, -2);
//            SetServerIdButtonBounds = rec3;
//            Bounds = rec0;



//        }

//        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
//        {
//            base.Render(canvas, graphics, channel);

//            if (channel == GH_CanvasChannel.Objects)
//            {
//                GH_Capsule button = GH_Capsule.CreateTextCapsule(PlayPauseButtonBounds, PlayPauseButtonBounds, GH_Palette.Black, owner.streamingPaused ? "Resume" : "Pause", 5, 0);
//                button.Render(graphics, Selected, Owner.Locked, false);
//                button.Dispose();

//                if (owner.streamingPaused)
//                {
//                    GH_Capsule button2 = GH_Capsule.CreateTextCapsule(SendStreamButtonBounds, SendStreamButtonBounds, GH_Palette.Normal, "Send Request", 5, 0);
//                    button2.Render(graphics, Selected, Owner.Locked, false);
//                    button2.Dispose();
//                }

//                GH_Capsule button3 = GH_Capsule.CreateTextCapsule(SetServerIdButtonBounds, SetServerIdButtonBounds, GH_Palette.White, owner.componentId == null || owner.componentId == "" ? @"Set Component Id" : owner.componentId, 0, 0);
//                button3.Render(graphics, Selected, Owner.Locked, false);
//                button3.Dispose();
//            }
//        }

//        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
//        {
//            if (e.Button == System.Windows.Forms.MouseButtons.Left)
//            {
//                System.Drawing.RectangleF rec = PlayPauseButtonBounds;
//                RectangleF rec2 = SendStreamButtonBounds;
//                RectangleF rec3 = SetServerIdButtonBounds;

//                if (rec.Contains(e.CanvasLocation))
//                {
//                    owner.streamingPaused = !owner.streamingPaused;
//                    owner.Message = owner.streamingPaused ? "continous streaming \n OFF" : "continous streaming \n ON";

//                    owner.ExpireSolution(true);
//                    return GH_ObjectResponse.Handled;
//                }
//                else if (rec2.Contains(e.CanvasLocation))
//                {
//                    owner.sendRequest = true;
//                    owner.ExpireSolution(true);
//                    return GH_ObjectResponse.Handled;
//                }
//                else if (rec3.Contains(e.CanvasLocation))
//                {
//                    //if (owner.serverId != null) return GH_ObjectResponse.Handled;
//                    owner.getcomponentId();
//                    return GH_ObjectResponse.Handled;
//                }
//            }
//            return base.RespondToMouseDown(sender, e);
//        }
//    }

//}