using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Net;
using System.Configuration;
using System.Threading;
using System.Timers;
using GsmComm;
using GsmComm.GsmCommunication;

namespace SMSApp
{
    public partial class Form1 : Form
    {

        SerialPort serialPort;
        SerialPort serialSms;
        string lastInput;
        System.Timers.Timer ti = new System.Timers.Timer();
        System.Timers.Timer applicationTimer = new System.Timers.Timer();

        public Form1()
        {
            InitializeComponent();
            //string test1 = comboBox2.SelectedText;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (System.Configuration.ConfigurationManager.AppSettings["FormVisible"] == "0")
            {
                //Form1 a = new Form1();
                //a.Visible = false;
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                this.Visible = false;
                //this.Visible = false;
            }

            panel2.Visible = false;

            textBox1.Text = System.Configuration.ConfigurationManager.AppSettings["PhoneNumber"];            
            comboBox2.Text = System.Configuration.ConfigurationManager.AppSettings["COMPort"];

            applicationTimer.Interval = Convert.ToInt64(System.Configuration.ConfigurationManager.AppSettings["ApplicationInterval"]);
            applicationTimer.Enabled = true;
            applicationTimer.Elapsed += new ElapsedEventHandler(applicationTimer_Elapsed);


            ti.Interval = Convert.ToInt64(System.Configuration.ConfigurationManager.AppSettings["SMSInterval"]);
            ti.Enabled = true;
            ti.Elapsed += new ElapsedEventHandler(ti_Elapsed);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            bool t = sendSMS("This is a test message by Behroz(Odesk Application).", textBox1.Text);

            MessageBox.Show("Message Result: " + t);
        }

        public string ExecCommand(SerialPort port, string command, int responseTimeout, string errorMessage)
        {
            try
            {
                port.DiscardOutBuffer();
                port.DiscardInBuffer();

                //TODO:Just for debugging . Remove this
                //AT AT+CMGF=1 AT+CMGS="+447715122691" 2
                if (command.Contains("AT+CMGF") && command.Contains("AT+CMGS"))
                {

                }

                port.Write(command + "\r");

                Thread.Sleep(2000);

                char[] charArray1 = new char[100];

                Thread.Sleep(2000);

                port.Read(charArray1, 0, port.BytesToRead);

                string res;
                res = new string(charArray1);

                if (errorMessage == "1")
                {
                    if (!res.Contains(">"))
                    {
                        WriteLogFile("Error", "Failed to accpt Phone Number" + " Error String: " + res);
                    }
                }
                else if (!res.Contains("OK"))
                {
                    WriteLogFile("Error" ,  errorMessage + " Error String: " + res);
                }
                //MessageBox.Show(
                return res;
            }
            catch (Exception ex)
            {
                port.Close();
                WriteLogFile(ex.Message);
                return string.Empty;
                //throw new ApplicationException(errorMessage, ex);
            }
        }

        public bool sendSMS(string Message, string PhoneNo)
        {
            try
            {
                //string COMPort = ConfigurationSettings.AppSettings.Keys[0];
                string COMPort = System.Configuration.ConfigurationManager.AppSettings["COMPort"];
                //serialSms = new SerialPort(comboBox2.Text, Int32.Parse(comboBox3.Text), Parity.None, 8, StopBits.One);
                serialSms = new SerialPort(COMPort, 9600, Parity.None, 8, StopBits.One);
                serialSms.Open();
                bool isSend = false;
                string recievedData = ExecCommand(serialSms, "AT", 3000, "No phone connected");
                recievedData = ExecCommand(serialSms, "AT+CMGF=1", 3000, "Failed to set message format.");

                String command = "AT+CMGS=\"" + PhoneNo + "\"";
                recievedData = ExecCommand(serialSms, command, 3000, "1");

                command = Message + char.ConvertFromUtf32(26) + "\r";
                recievedData = ExecCommand(serialSms, command, 3000, "Failed to send message.Check Phone Number."); //3 seconds

                if (recievedData.Contains("OK"))
                {
                    isSend = true;
                }
                else if (recievedData.Contains("ERROR"))
                {                    
                    isSend = false;
                }
                serialSms.Close();
                return isSend;
            }
            catch (Exception ex)
            {
                serialSms.Close();
                WriteLogFile(ex.Message);                
                return false;
            }
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        //Timer for Application Restart
        protected void applicationTimer_Elapsed(Object source, ElapsedEventArgs e)
        {
            applicationTimer.Stop();
            //MessageBox.Show("Restart");
            Application.Restart();            
        }

        //1 minute delay
        protected void ti_Elapsed(Object source, ElapsedEventArgs e )
        {            
            ti.Stop();
            //Recieve SMS
            ReciveSMS();

            //MessageBox.Show("Time Elasped:");
            GetHTMLAndSendSMS();
            ti.Start();
        }

        private void GetHTMLAndSendSMS()
        {

            bool result = false;
            // We'll use WebClient class for reading HTML of web page
            WebClient MyWebClient = new WebClient();

            string pageHTML;
            try
            {
                // Read web page HTML to byte array
                Byte[] PageHTMLBytes;
                //if (txtURL.Text != "")
                {
                    PageHTMLBytes = MyWebClient.DownloadData(System.Configuration.ConfigurationManager.AppSettings["ListSMSUrl"]);

                    // Convert result from byte array to string
                    // and display it in TextBox txtPageHTML
                    UTF8Encoding oUTF8 = new UTF8Encoding();
                    pageHTML = oUTF8.GetString(PageHTMLBytes);                    
                }

                string[] HtmlArr = pageHTML.Split(new char[] { ';' });

                if (pageHTML.Trim() != "")
                {

                    if (HtmlArr.Length > 0)
                    {
                        string[] message;
                        string Id = string.Empty;
                        for (int i = 0; i < HtmlArr.Length; i++)
                        {
                            message = HtmlArr[i].Split(new char[] { '|' });
                            if (message[0].Contains('<') == true)
                            {
                                message[0] = message[0].Trim();
                                Id = message[0].Substring(message[0].IndexOf('>') + 1);
                            }
                            else
                                Id = message[0];

                            if (message.Length >= 3)
                            {
                                //Send SMS
                                bool resultOfZero = message[1].StartsWith("0");
                                if (resultOfZero)
                                {
                                    message[1] = "+44" + message[1].Substring(1);
                                }

                                //Send SMS
                                if ((message[3].Contains("AT+CMGF") && message[3].Contains("AT+CMGS")))
                                {

                                }

                                result = sendSMS(message[3], message[1]);

                                string updateURL = System.Configuration.ConfigurationManager.AppSettings["UpdateSMSUrl"];
                                string SMSIdParam = System.Configuration.ConfigurationManager.AppSettings["SMSIdParam"];
                                string StatusParam = System.Configuration.ConfigurationManager.AppSettings["StatusParam"];

                                if (result == true)
                                {
                                    string completeURL = updateURL + "?" + SMSIdParam + Id + "&" + StatusParam + "1";
                                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(completeURL);
                                    HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                                    if (res.StatusCode == HttpStatusCode.OK)
                                    {
                                        WriteLogFile("SMS Sent", "SMS ID: " + Id + " |Status 1");
                                    }
                                    else
                                    {
                                        WriteLogFile("SMS Not Sent", "Error in reading the ListSMS page. Check internet connectivity");
                                    }
                                    res.Close();
                                }
                                else
                                {
                                    string completeURL = updateURL + "?" + SMSIdParam + Id + "&" + StatusParam + "2";
                                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(completeURL);
                                    HttpWebResponse res = (HttpWebResponse)req.GetResponse();

                                    WriteLogFile("SMS Sending Failed. Check Modem configurations", "SMS ID: " + Id + " |Status 2");
                                    if (res.StatusCode == HttpStatusCode.OK)
                                    {
                                        //WriteLogFile("SMS Not Sent. Check Internet connectivity ", message[0] + " | 2");
                                    }
                                    else
                                    {
                                        WriteLogFile("SMS Not Sent", "Error in reading the ListSMS page. Check internet connectivity");
                                    }
                                    res.Close();
                                }
                            }
                        }
                        //WriteLogFile("Normal", "SMS Execution Complete");                        
                    }
                    else
                    {
                        //WriteLogFile("Normal", "No SMS exists in queue");                        
                    }
                }
                else
                {
                    //WriteLogFile("Normal", "All SMS have been sent. No SMS are pending.");                    
                }
            }
            catch (WebException ex1)
            {
                WriteLogFile(ex1.Message);
                //MessageBox.Show("Unable to download Data from Website. Check Internet connectivity." + " -- Exception:  " + ex1.Message);
            }
            catch (Exception ex)
            {
                WriteLogFile(ex.Message);
                //MessageBox.Show("Exception: " + ex.Message);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            /*
            bool result = false;
            // We'll use WebClient class for reading HTML of web page
            WebClient MyWebClient = new WebClient();

            string pageHTML;

            try
            {
                // Read web page HTML to byte array
                Byte[] PageHTMLBytes;
                //if (txtURL.Text != "")
                {
                    PageHTMLBytes = MyWebClient.DownloadData(System.Configuration.ConfigurationManager.AppSettings["ListSMSUrl"]);

                    // Convert result from byte array to string
                    // and display it in TextBox txtPageHTML
                    UTF8Encoding oUTF8 = new UTF8Encoding();
                    pageHTML = oUTF8.GetString(PageHTMLBytes);
                    //MessageBox.Show(oUTF8.GetString(PageHTMLBytes));
                }

                string[] HtmlArr = pageHTML.Split(new char[] { ';' });

                if (pageHTML.Trim() != "")
                {

                    if (HtmlArr.Length > 0)
                    {
                        string[] message;
                        string Id = string.Empty;
                        for (int i = 0; i < HtmlArr.Length; i++)
                        {
                            message = HtmlArr[i].Split(new char[] { '|' });
                            if (message[0].Contains('<') == true)
                            {
                                message[0] = message[0].Trim();
                                Id = message[0].Substring(message[0].IndexOf('>') + 1);
                            }
                            else
                                Id = message[0];

                            if (message.Length >= 3)
                            {
                                //Send SMS
                                result = sendSMS(message[3], message[1]);

                                string updateURL = System.Configuration.ConfigurationManager.AppSettings["UpdateSMSUrl"];
                                string SMSIdParam = System.Configuration.ConfigurationManager.AppSettings["SMSIdParam"];
                                string StatusParam = System.Configuration.ConfigurationManager.AppSettings["StatusParam"];
                                                                
                                if (result == true)
                                {
                                    string completeURL = updateURL + "?"+ SMSIdParam + Id + "&" + StatusParam + "1";
                                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(completeURL);
                                    HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                                    res.Close();
                                }
                                else
                                {
                                    string completeURL = updateURL + "?"+ SMSIdParam + Id + "&" + StatusParam + "2";
                                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(completeURL);
                                    HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                                    res.Close();
                                }
                            }
                            //MessageBox.Show("ID: " + Id + " Phone No: " + message[1] + " Message: " + message[3]);
                        }
                        MessageBox.Show("Done.");
                    }
                    else
                    {
                        MessageBox.Show("No SMS exists.");
                    }
                }
                else
                    MessageBox.Show("All SMS have been sent. No SMS are pending.");
            }
            catch (WebException ex1)
            {                
                MessageBox.Show("Unable to download Data from Website. Check Internet connectivity." + " -- Exception:  " + ex1.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
             */ 
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //System.Diagnostics.Process.Start("http://quinns.messageguardian.co.uk/quinns/updatesms.php?smsid=" + 5);
            //WebClient client = new WebClient();
            //client.UploadString("http://quinns.messageguardian.co.uk/quinns/updatesms.php?smsid="+2,"POST",
            //    WebRequest request = WebRequest.Create("http://quinns.messageguardian.co.uk/quinns/updatesms.php?smsid="+2);
            //request.Method = "POST";

            /*
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://quinns.messageguardian.co.uk/quinns/updatesms.php?smsid=" + 2 +"&status=1");

            HttpWebResponse res = (HttpWebResponse)req.GetResponse();
             */ 

        }

        private void WriteLogFile(string ex)
        {
            System.IO.StreamWriter tw = System.IO.File.AppendText("ErrorLogFile.txt");
            tw.WriteLine("-- Error Date/Time -- : " + DateTime.Now + "  \r\n Exception: " + ex + "  \r\n ---------------- \n\n");
            tw.Close();
            
        }

        private void WriteLogFile(string pType , string pMessage)
        {
            System.IO.StreamWriter tw = System.IO.File.AppendText("ErrorLogFile.txt");
            tw.WriteLine("-- " + pType  +" -- : " + DateTime.Now + "  \r\n Message: " + pMessage + "  \r\n ---------------- \n\n");
            tw.Close();
        }

        private void btnRead_Click(object sender, EventArgs e)
        {
            GsmCommMain comm = new GsmCommMain(3, 2400);
            try
            {   
                comm.Open();

                //string storage = GetMessageStorage();
                string storage = PhoneStorageType.Sim;

                DecodedShortMessage[] messages = comm.ReadMessages(PhoneMessageStatus.ReceivedRead, storage);
                int count = messages.Length;
                //MessageBox.Show(count.ToString());
                int i = 0;

                while (count != 0)
                {
                    GsmComm.PduConverter.SmsPdu pdu = messages[i].Data;
                    
                    //GsmComm.PduConverter.SmsSubmitPdu data = (GsmComm.PduConverter.SmsSubmitPdu)pdu;
                    string messageSMS = pdu.UserDataText;
                    string phoneNumber = pdu.SmscAddress;
                    //MessageBox.Show(i +" -- "+messageSMS);
                    string URL= System.Configuration.ConfigurationManager.AppSettings["IncomingSMSUrl"] + phoneNumber +"&incommingmessage=" + messageSMS;
                    
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(URL);
                    HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                    if (res.StatusCode == HttpStatusCode.OK)
                    {
                        comm.DeleteMessage(i, storage);
                        MessageBox.Show(i.ToString());
                        //WriteLogFile("SMS Sent", "SMS ID: " + Id + " |Status 1");
                    }
                    else
                    {
                        WriteLogFile("SMS Not Recieved", "Error in reading the web page. Check internet connectivity");
                    }
                    res.Close();

                    i++;
                    count--;
                }
            }
            catch(Exception ex)
            {
                //Log
                WriteLogFile("Exception", ex.Message);
            }
            finally
            {
                comm.Close();
            }
        }

        /*
        private string GetMessageStorage()
        {
            string storage = string.Empty;
            if (rbMessageSIM.Checked)
                storage = PhoneStorageType.Sim;
            if (rbMessagePhone.Checked)
                storage = PhoneStorageType.Phone;
            if (storage.Length == 0)
                throw new ApplicationException("Unknown message storage.");
            else
                return storage;
        }*/

        private string StatusToString(PhoneMessageStatus status)
        {
            // Map a message status to a string
            string ret;
            switch (status)
            {
                case PhoneMessageStatus.All:
                    ret = "All";
                    break;
                case PhoneMessageStatus.ReceivedRead:
                    ret = "Read";
                    break;
                case PhoneMessageStatus.ReceivedUnread:
                    ret = "Unread";
                    break;
                case PhoneMessageStatus.StoredSent:
                    ret = "Sent";
                    break;
                case PhoneMessageStatus.StoredUnsent:
                    ret = "Unsent";
                    break;
                default:
                    ret = "Unknown (" + status.ToString() + ")";
                    break;
            }
            return ret;
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            bool ress= sendSMS("SMS RECIVEING TESTING BY BEHROZ", "+447715122691");
            MessageBox.Show(ress.ToString());
        }

        private void ReciveSMS()
        {
            string COMPortNumber = System.Configuration.ConfigurationManager.AppSettings["COMPortNumber"];
            GsmCommMain comm = new GsmCommMain(Convert.ToInt32(COMPortNumber), 2400);
            try
            {
                comm.Open();

                //string storage = GetMessageStorage();
                string storage = PhoneStorageType.Sim;

                DecodedShortMessage[] messages = comm.ReadMessages(PhoneMessageStatus.ReceivedRead, storage);
                int count = messages.Length;
                //MessageBox.Show(count.ToString());
                int i = 0;

                while (count != 0)
                {
                    GsmComm.PduConverter.SmsPdu pdu = messages[i].Data;

                    //GsmComm.PduConverter.SmsSubmitPdu data = (GsmComm.PduConverter.SmsSubmitPdu)pdu;
                    string messageSMS = pdu.UserDataText;
                    string phoneNumber = pdu.SmscAddress;
                    //MessageBox.Show(i +" -- "+messageSMS);
                    string URL = System.Configuration.ConfigurationManager.AppSettings["IncomingSMSUrl"] + phoneNumber + "&incommingmessage=" + messageSMS;

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(URL);
                    HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                    if (res.StatusCode == HttpStatusCode.OK)
                    {
                        comm.DeleteMessage(i, storage);
                        //MessageBox.Show(i.ToString());
                        //WriteLogFile("SMS Sent", "SMS ID: " + Id + " |Status 1");
                    }
                    else
                    {
                        WriteLogFile("SMS Not Recieved", "Error in reading the web page. Check internet connectivity");
                    }
                    res.Close();

                    i++;
                    count--;
                }
            }
            catch (Exception ex)
            {
                //Log
                WriteLogFile("Exception", ex.Message);
            }
            finally
            {
                comm.Close();
            }
        }
    }
}
