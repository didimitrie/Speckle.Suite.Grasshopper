using Grasshopper.Kernel;
using System.Drawing;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI;

namespace SpeckleSuite
{
    internal class SpeckleStreamSendAttr : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
    {
        private SpeckleStreamSend owner;
        private Rectangle PlayPauseButtonBounds;
        private Rectangle SendStreamButtonBounds;
        private Rectangle SaveStreamButtonBounds;

        public SpeckleStreamSendAttr(SpeckleStreamSend owner) : base (owner)
        {
            this.owner = owner;
        }

        protected override void Layout()
        {
            base.Layout();
            Rectangle rec0 = GH_Convert.ToRectangle(Bounds);
            rec0.Height += 22;

            Rectangle rec1 = rec0;
            rec1.Y = rec1.Bottom - 22;
            rec1.Height = 22;
            rec1.Inflate(-5, -4);

            Bounds = rec0;
            PlayPauseButtonBounds = rec1;

            if(owner.streamingPaused)
            {
                rec0.Height += 22;
                Rectangle rec2 = rec0;
                rec2.Y = rec2.Bottom - 24;
                rec2.Height = 22;
                rec2.Inflate(-5, -4);
                Bounds = rec0;
                SendStreamButtonBounds = rec2;
            }

            //rec0.Height += 22;
            //Rectangle rec3 = rec0;
            //rec3.Height = 20;
            //rec3.Width = 40;
            //rec3.Y = rec0.Bottom - (owner.streamingPaused ? 24 : 23);
            //rec3.X = rec0.Left + rec0.Width / 2 - 10;
            ////rec3.Inflate(-26, -4);
            //SaveStreamButtonBounds = rec3;
            ////Bounds = rec0;

        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if(channel == GH_CanvasChannel.Objects)
            {
                GH_Capsule button = GH_Capsule.CreateTextCapsule(PlayPauseButtonBounds, PlayPauseButtonBounds, GH_Palette.Black, owner.streamingPaused ? "Resume" : "Pause", 0, 0);
                button.Render(graphics, Selected, Owner.Locked, false);
                button.Dispose();

                if(owner.streamingPaused)
                {
                    GH_Capsule button2 = GH_Capsule.CreateTextCapsule(SendStreamButtonBounds, SendStreamButtonBounds, GH_Palette.Normal, "Push Stream", 0, 0);
                    button2.Render(graphics, Selected, Owner.Locked, false);
                    button2.Dispose();
                }

                //GH_Capsule button3 = GH_Capsule.CreateTextCapsule(SaveStreamButtonBounds, SaveStreamButtonBounds, GH_Palette.Hidden, @"Save", 2, 0);
                //button3.Render(graphics, Selected, Owner.Locked, false);
                //button3.Dispose();

            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                RectangleF rec = PlayPauseButtonBounds;
                RectangleF rec2 = SendStreamButtonBounds;
                RectangleF rec3 = SaveStreamButtonBounds;
                if (rec.Contains(e.CanvasLocation))
                {
                    owner.streamingPaused = !owner.streamingPaused;
                    owner.Message = owner.streamingPaused ? "continous streaming \n OFF" : "continous streaming \n ON";

                    owner.ExpireSolution(true);
                    return GH_ObjectResponse.Handled;
                }
                else if (rec2.Contains(e.CanvasLocation))
                {
                    owner.pushStream = true;
                    owner.ExpireSolution(true);
                    return GH_ObjectResponse.Handled;
                }
                //else if(rec3.Contains(e.CanvasLocation))
                //{
                //    System.Windows.Forms.MessageBox.Show("Current stream instance freezed.");
                //    return GH_ObjectResponse.Handled;
                //}
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}