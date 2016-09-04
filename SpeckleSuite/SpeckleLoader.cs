﻿using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Timers;
using Grasshopper;
using System.Windows.Forms;

namespace SpeckleSuite
{
    public class SpeckleLoader : GH_AssemblyPriority
    {
        public System.Timers.Timer loadTimer;
        SpeckleUtils myUtils;
        ToolStripMenuItem customItem;

        public SpeckleLoader()
        {
            myUtils = new SpeckleUtils();
        }

        public override GH_LoadingInstruction PriorityLoad()
        {
            loadTimer = new System.Timers.Timer(500);
            loadTimer.Elapsed += LoadTimer_Elapsed;
            loadTimer.Start();
            return GH_LoadingInstruction.Proceed;
        }

        private void LoadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {

            if (Grasshopper.Instances.DocumentEditor != null)
            {
                MenuStrip mainmenu = Instances.DocumentEditor.MainMenuStrip;

                customItem = new ToolStripMenuItem("Speckle Suite");
                mainmenu.Items.Add(customItem);

                if (myUtils.hasApiKey())
                {
                    ToolStripItem activeItem0 = customItem.DropDown.Items.Add("You are logged in");
                }

                ToolStripItem activeItem1 = customItem.DropDown.Items.Add("Set Api Key", null, MenuItemClickedAddApiKey);

                loadTimer.Stop();
            }
        }

        private void MenuItemClickedAddApiKey(object sender, EventArgs e)
        {
            myUtils.promptForApiKey();
        }

    }
}