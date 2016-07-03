using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SpeckleSuite
{
    internal class SpeckleStreamReceiveAttr : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
    {
        private SpeckleStreamReceive owner;
        private Rectangle Underlay;
        private Rectangle SendStreamButtonBounds;
        private Rectangle PlayPauseButtonBounds;

        public SpeckleStreamReceiveAttr(SpeckleStreamReceive owner) : base(owner)
        {
            this.owner = owner;
        }

        protected override void Layout()
        {
            base.Layout();
            Rectangle rec0 = GH_Convert.ToRectangle(Bounds);
            rec0.Height += 25;

            Rectangle rec1 = rec0;
            rec1.Y = rec1.Bottom - 25;
            rec1.Height = 25;
            rec1.Inflate(-5, -4);

            Bounds = rec0;
            PlayPauseButtonBounds = rec1;

            if (owner.streamingPaused)
            {
                rec0.Height += 25;
                Rectangle rec2 = rec0;
                rec2.Y = rec2.Bottom - 27;
                rec2.Height = 25;
                rec2.Inflate(-5, -4);
                Bounds = rec0;
                SendStreamButtonBounds = rec2;
            }

            Underlay = GH_Convert.ToRectangle(Bounds);
            Underlay.Inflate(2,2);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                GH_Capsule button = GH_Capsule.CreateTextCapsule(PlayPauseButtonBounds, PlayPauseButtonBounds, GH_Palette.Black, owner.streamingPaused ? "Resume" : "Pause", 0, 0);
                button.Render(graphics, Selected, Owner.Locked, false);
                button.Dispose();

                if (owner.streamingPaused)
                {
                    GH_Capsule button2 = GH_Capsule.CreateTextCapsule(SendStreamButtonBounds, SendStreamButtonBounds, GH_Palette.Normal, "Pull Stream", 0, 0);
                    button2.Render(graphics, Selected, Owner.Locked, false);
                    button2.Dispose();
                }
            }
            
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                System.Drawing.RectangleF rec = PlayPauseButtonBounds;
                RectangleF rec2 = SendStreamButtonBounds;
                if (rec.Contains(e.CanvasLocation))
                {
                    owner.streamingPaused = !owner.streamingPaused;
                    owner.Message = owner.streamingPaused ? "continous streaming \n OFF" : "";

                    owner.ExpireSolution(true);
                    return GH_ObjectResponse.Handled;
                }
                else if (rec2.Contains(e.CanvasLocation))
                {
                    owner.pullStream = true;
                    owner.startPullStream();
                    owner.ExpireSolution(true);
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }
    }

}