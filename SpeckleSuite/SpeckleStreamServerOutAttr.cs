using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SpeckleSuite
{
    internal class SpeckleStreamServerOutAttr : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
    {
        private SpeckleStreamServerOut owner;

        public SpeckleStreamServerOutAttr(SpeckleStreamServerOut owner) : base(owner)
        {
            this.owner = owner;
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            switch (channel)
            {
                case Grasshopper.GUI.Canvas.GH_CanvasChannel.Wires:
                    //We need to draw all wires normally, except for the first parameter.
                    if ((Owner.Params.Input.Count > 0))
                    {
                        IGH_Param param = Owner.Params.Input[0];
                        PointF p1 = param.Attributes.InputGrip;

                        foreach (IGH_Param source in param.Sources)
                        {
                            PointF p0 = source.Attributes.OutputGrip;
                            if ((!canvas.Painter.ConnectionVisible(p0, p1)))
                                continue;

                            System.Drawing.Drawing2D.GraphicsPath wirePath = GH_Painter.ConnectionPath(p0, p1, GH_WireDirection.right, GH_WireDirection.left);
                            if ((wirePath == null))
                                continue;

                            int width = 5;
                            //try {
                            //    width = owner.RECEIVER.jobQueue.Count > 22 ? 22 : owner.RECEIVER.jobQueue.Count;
                            //    if (width < 5) width = 5;
                            //    if (width > 20) width = 20;
                            //} catch { }

                            Pen wirePen = new Pen(Color.FromArgb(200, Color.White), 20); float[] arr = { 4, 2 };

                            //wirePen.DashPattern = arr;
                            wirePen.DashCap = System.Drawing.Drawing2D.DashCap.Triangle;
                            graphics.DrawPath(wirePen, wirePath);
                            wirePen = new Pen(Color.SkyBlue, 2); float[] arr2 = { 1, 1};
                            wirePen.DashPattern = arr2;
                            wirePen.DashCap = System.Drawing.Drawing2D.DashCap.Round ;
                            graphics.DrawPath(wirePen, wirePath);
                            wirePen.Dispose();
                            wirePath.Dispose();
                        }
                    }

                    //Draw all other parameters normally.
                    for (Int32 i = 1; i <= Owner.Params.Input.Count - 1; i++)
                    {
                        Owner.Params.Input[i].Attributes.RenderToCanvas(canvas, GH_CanvasChannel.Wires);
                    }
                    break;
                default:
                    base.Render(canvas, graphics, channel);
                    break;
            }
        }
    }

}