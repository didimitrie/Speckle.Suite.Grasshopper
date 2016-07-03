using Microsoft.VisualBasic;
using RestSharp;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SpeckleSuite
{
    public class SpeckleUtils
    {
        public string APIKEY = "";
        public string APIENDPOINT = "";

        //public Uri httpServer = new Uri("http://46.101.85.144:3001");
        //public Uri socketServer = new Uri("http://46.101.85.144:3001");

        public Uri httpServer = new Uri("http://localhost:3001");
        public Uri socketServer = new Uri("http://localhost:3001");


        public bool verfied = false;
        public string familyName, givenName;

        public SpeckleUtils()
        {
            try
            {
                var path = Grasshopper.Folders.AppDataFolder + @"/speckle_api_key.txt";
                APIKEY = System.IO.File.ReadAllText(path);
            }
            catch
            {
                
            }
            try
            {
                //var path = Grasshopper.Folders.AppDataFolder + @"/speckle_api_endpoint.txt";
                //APIENDPOINT = System.IO.File.ReadAllText(path);
                //httpServer = new Uri(APIENDPOINT + ":3000");
                //socketServer = new Uri(APIENDPOINT + ":3001");
                //MessageBox.Show("You have set a different api endpoint: speckle will try to use " + APIENDPOINT + " as a server!");
            }
            catch
            {
                
            }
        }

        public void openHelp(Object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/didimitrie/speckle.exporter/wiki/User-Guide");
        }

        public void gotoGithub(Object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(@"https://github.com/didimitrie/speckle.exporter");
        }

        #region registration & api key verification

        public bool promptForApiKey()
        {

            string value = "APIKEY";
            if (InputBox("API Key Request", "Hey there! First time using Speckle? Paste in below your API key. \n (get it from your speckle.xyz/dashboard/ after you've logged in)", ref value) == DialogResult.OK)
            {
                APIKEY = value;
            }

            return checkApiKey();
        }

        public bool hasApiKey()
        {
            return APIKEY != "";
        }

        public void removeApiKey()
        {
            var path = Grasshopper.Folders.AppDataFolder + @"/speckle_api_key.txt";
            File.Delete(path);
            APIKEY = "";
            verfied = false;
            MessageBox.Show("Your API key has been deleted!");
        }

        public bool checkApiKey()
        {
            bool HasSpace = APIKEY.Contains(" ");
            if(HasSpace)
            {
                MessageBox.Show("Invalid key format.");
                verfied = false;
                APIKEY = "";
                return false;
            }
            var client = new RestClient(new Uri(this.httpServer, "/api/user/keycheck"));
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/json");
            request.AddParameter("application/json", "{\n    \"apikey\": \""+ APIKEY + "\"\n}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            var parsedResponse = "";

            try
            {
                parsedResponse = System.Text.Encoding.ASCII.GetString(response.RawBytes);
            }
            catch
            {
                parsedResponse = "no connection";
            }

            if (parsedResponse == "no connection")
            {
                MessageBox.Show("No internet connection - can't verify key. Try again later?"); verfied = false; APIKEY = "";
                return false;
            }
            else
            if (parsedResponse == "error")
            {
                MessageBox.Show("No user with this api key has been identified. Is your key expired?");
                verfied = false; APIKEY = "";
                return false;
            }
            else
            {
                var split = parsedResponse.Split(',');
                if (split[0] == "ok")
                {
                    MessageBox.Show("Welcome to Speckle, " + split[1] + "! ");
                    verfied = true;

                    var path = Grasshopper.Folders.AppDataFolder;

                    System.IO.StreamWriter file = new System.IO.StreamWriter( path + @"/speckle_api_key.txt");
                    file.WriteLine(APIKEY);
                    file.Close();
                    return true;
                }
            }
            return true;
        }

        #endregion

        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new FormWithShadow();
            Label label = new Label();
            Label label2 = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            label.Font = new Font("Arial Narrow", 12, FontStyle.Regular);
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label2.SetBounds(9, 10+10, 372, 13);
            //label2.AutoSize = true;
            label2.TextAlign = ContentAlignment.MiddleCenter;
            label2.Text = "Speckle Suite";
            label2.Font = new Font("Arial Narrow", 14, FontStyle.Bold);

            label.SetBounds(9, 45, 372, 13);
            textBox.SetBounds(12, 56 + 40, 372, 20);
            buttonOk.SetBounds(125, 82 + 40, 75, 23);
            buttonCancel.SetBounds(205, 82 + 40, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            textBox.TextAlign = HorizontalAlignment.Center;
            textBox.BorderStyle = BorderStyle.None;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(400, 200);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.BackColor = Color.FromArgb(255, 255, 255);
            form.ForeColor = Color.FromArgb(0, 0, 0);
            form.FormBorderStyle = FormBorderStyle.None;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }

    }

}
