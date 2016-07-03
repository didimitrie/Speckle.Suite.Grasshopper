using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SpeckleSuite
{
    internal class SpeckleStreamServerInAttr : Grasshopper.Kernel.Attributes.GH_ComponentAttributes
    {
        private SpeckleStreamServerIn owner;

        public SpeckleStreamServerInAttr(SpeckleStreamServerIn owner) : base(owner)
        {
            this.owner = owner;
        }

        //protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        //{
        //    base.Render(canvas, graphics, channel);
        //}

    }

}