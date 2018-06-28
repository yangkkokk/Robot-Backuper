﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KRcc;

namespace KawasakiRobotBackuper
{
    public partial class MainWindow : Form
    {
        volatile SerialPort serialPort;
        volatile bool isConnected = false;
        volatile bool isBusy = false;
        volatile bool isTyping = true;
        volatile int mode = 0;
        volatile String portName = "NULL";
        volatile String robotName = "Backup";
        volatile SoundPlayer soundPlayer;
        volatile Commu com = null;
        volatile System.Collections.ArrayList resp;
        Thread tryConThread;
        Thread checkConThread;
        Thread downloadThread;
        Thread portFinderThread;
        volatile String[] Sounds;
        //volatile Commu com;
        /*
         * 0 - Loaded
         * 1 - Connected
         * 2 - Disconnected
         * 3 - Backup started
         * 4 - Backup finished
        */

        public MainWindow()
        {
            InitializeComponent();
            Width = 345;
            
            //com = new Commu("COM3");

            tryConThread = new Thread(tryToConnect);
            checkConThread = new Thread(checkConnection);
            downloadThread = new Thread(downloadBackup);
            portFinderThread = new Thread(findPorts);

            soundPlayer = new SoundPlayer("test.wav");
            Sounds = File.ReadAllLines(@"sounds.dat");

            //Sounds.Add("Loaded", lines[0]);
            //Sounds.Add("Connected", lines[1]);
            //Sounds.Add("Disonnected", lines[2]);
            //Sounds.Add("BStart", lines[3]);
            //Sounds.Add("BEnd", lines[4]);
            //portFinderThread.Start();
        }

        private void findPorts()
        {
            string[] Ports;
            bool isFirst = true;
            int portBoxSize;
            while (true)
            {
                try
                {
                    Ports = SerialPort.GetPortNames();
                    portBoxSize = Ports.Length;
                    //
                    if (boxPortList.InvokeRequired)
                        boxPortList.Invoke(new Action(delegate ()
                        {
                            if (boxPortList.Items.Count != portBoxSize)
                            {
                                boxPortList.Items.Clear();
                                boxPortList.Items.AddRange(Ports);

                                consoleWrite("Found " + portBoxSize + " ports", Color.Green);
                                if (isFirst)
                                {
                                    consoleWrite("Trying to connect", Color.Blue);
                                    isFirst = false;
                                }

                                if (portBoxSize != 0)
                                {
                                    boxPortList.SelectedIndex = 0;
                                    portName = boxPortList.SelectedItem.ToString();
                                }
                            }
                        }));
                }
                catch (Exception e) { };
                Thread.Sleep(5000);

            }
        }

        /*private void asyncWrite(String text, Color color = default(Color))
        {
            textcConsole.WriteLine(text, color);
        }
        */
        private void consoleWrite(String text, Color color = default(Color))
        {
            if (isTyping)
            {

                if (textcConsole.InvokeRequired)
                    textcConsole.Invoke(new Action(delegate () { textcConsole.WriteLine(text, color); }));
                else
                    textcConsole.WriteLine(text, color);
            }
        }

        void backupUSB(String dirName, String robName, ref bool isShowed)
        {

            String resp;
            String[] respl;
            Char[] delim = new Char[] { '\n' };
            try
            {

                lock (this)
                {
                    if (labStatus.InvokeRequired)
                        labStatus.Invoke(new Action(delegate ()
                        {
                            labStatus.Text = "Backuping";
                            labStatus.BackColor = Color.Gold;
                        }));
                    serialPort.WriteLine("USB_FDEL " + dirName);
                    resp = waitResponse();
                    respl = resp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                    if (respl[1] == "(P2103)USB memory is not inserted.\r")
                    {
                        if (!isShowed)
                        {
                            consoleWrite("Please input USB memory", Color.Red);
                            Thread.Sleep(1000);
                            soundPlayer = new SoundPlayer(Sounds[5]);
                            soundPlayer.Play();
                            isShowed = true;
                        }
                        return;
                    }
                    soundPlayer = new SoundPlayer(Sounds[3]);
                    soundPlayer.Play();
                    consoleWrite("Backuping", Color.Orange);
                    isShowed = false;

                    serialPort.WriteLine("USB_MKDIR " + dirName);
                    resp = waitResponse();
                    consoleWrite("Saving backup", Color.Blue);

                    serialPort.WriteLine("USB_SAVE " + dirName + "\\" + robName);
                    resp = waitResponse();
                    writeArray(resp);
                    consoleWrite("Backup saved\n", Color.Green);

                    consoleWrite("Saving operation log", Color.Blue);
                    serialPort.WriteLine("USB_SAVE/OPLOG " + dirName + "\\" + robName);
                    resp = waitResponse();
                    writeArray(resp);
                    consoleWrite("Operation log saved\n", Color.Green);

                    consoleWrite("Saving error log", Color.Blue);
                    serialPort.WriteLine("USB_SAVE/ELOG " + dirName + "\\" + robName);
                    resp = waitResponse();
                    writeArray(resp);
                    consoleWrite("Error log saved\n", Color.Green);

                    consoleWrite("Finished\n", Color.Green);
                    soundPlayer = new SoundPlayer(Sounds[4]);
                    soundPlayer.Play();

                    if (labStatus.InvokeRequired)
                        labStatus.Invoke(new Action(delegate ()
                        {
                            labStatus.Text = "Connected";
                            labStatus.BackColor = Color.Chartreuse;
                        }));
                    if (blockPanel.InvokeRequired)
                        blockPanel.Invoke(new Action(delegate ()
                        {
                            blockPanel.Enabled = true;
                        }));
                }
            } catch (Exception e) { };
            mode = 0;
            isBusy = false;
        }

        void backupRS232(String dirName, String robName)
        {
            lock (this)
            {
                try
                {
                    if (labStatus.InvokeRequired)
                        labStatus.Invoke(new Action(delegate ()
                        {
                            labStatus.Text = "Backuping";
                            labStatus.BackColor = Color.Orange;
                        }));
                    serialPort.Close();

                    //Commu com = new Commu(portName);
                    com = new Commu(portName);
                    soundPlayer = new SoundPlayer(Sounds[3]);
                    soundPlayer.Play();
                    consoleWrite("Backuping", Color.Orange);

                    if (!System.IO.Directory.Exists(dirName))
                    {
                        System.IO.Directory.CreateDirectory(dirName);
                    }
                    com.asInquiry = delegate (string as_msg)
                    {
                        consoleWrite(as_msg);
                        return null;
                    };
                    consoleWrite("Saving backup", Color.Blue);
                    com.save(dirName + "/" + robotName + ".as");
                    consoleWrite("Backup saved\n", Color.Green);

                    consoleWrite("Saving operation log", Color.Blue);
                    com.save(dirName + "/" + robotName + ".ol", "", "/OPLOG");
                    consoleWrite("Operation log saved\n", Color.Green);

                    consoleWrite("Saving error log", Color.Blue);
                    com.save(dirName + "/" + robotName + ".el", "", "/ELOG");
                    consoleWrite("Error log saved\n", Color.Green);

                    consoleWrite("Finished\n", Color.Green);
                    if (isTyping)
                    {
                        soundPlayer = new SoundPlayer(Sounds[4]);
                        soundPlayer.Play();
                    }
                    if (labStatus.InvokeRequired)
                        labStatus.Invoke(new Action(delegate ()
                        {
                            labStatus.Text = "Connected";
                            labStatus.BackColor = Color.Chartreuse;
                        }));
                    com.disconnect();
                    serialPort.Open();
                } catch (Exception e) { };
                mode = 0;
                isBusy = false;
            }
        }

        void checkConnection()
        {
            while (true)
            {
                if (isConnected == true && isBusy == false)
                {

                    try
                    {
                        serialPort.WriteLine("");
                        string ad = serialPort.ReadTo(">");
                    }
                    catch (Exception e)
                    {
                        //if (!isBusy)
                        //{
                        isConnected = false;
                        consoleWrite("Disconnected!", Color.Red);
                        consoleWrite("Trying to connect!", Color.Blue);
                        soundPlayer = new SoundPlayer(Sounds[2]);
                        soundPlayer.Play();
                        if (isBusy == false)
                        {
                            serialPort.Close();
                            if (labStatus.InvokeRequired)
                                labStatus.Invoke(new Action(delegate ()
                                {
                                    labStatus.Text = "Connecting";
                                    labStatus.BackColor = Color.SkyBlue;
                                }));
                            if (blockPanel.InvokeRequired)
                                blockPanel.Invoke(new Action(delegate ()
                                {
                                    blockPanel.Enabled = false;
                                }));
                        }
                        //}
                    }
                    Thread.Sleep(2000);
                }
                //
            }
        }

        void tryToConnect()
        {
            String resp;
            String[] respl;
            Char[] delimn = new Char[] { '\n', '\r' };
            Char[] deliml = new Char[] { ' ' };
            while (true)
            {

                if (isConnected == false && isBusy == false)
                {

                    serialPort = new SerialPort(portName);
                    serialPort.ReadTimeout = 1000;
                    resp = "";
                    try
                    {
                        serialPort.Open();
                        serialPort.WriteLine("");
                        resp = serialPort.ReadTo(">");
                        serialPort.WriteLine("ID");
                        serialPort.WriteLine(" ");
                        Thread.Sleep(1000);
                        resp = serialPort.ReadTo(">");
                        resp = resp.Replace('-', '_');
                        respl = resp.Split(delimn, StringSplitOptions.RemoveEmptyEntries);
                        respl = respl[1].Split(deliml, StringSplitOptions.RemoveEmptyEntries);
                        if (textRobotName.InvokeRequired)
                            textRobotName.Invoke(new Action(delegate ()
                            {
                                textRobotName.Text = respl[2] + respl[9];
                            }));
                        if (labStatus.InvokeRequired)
                            labStatus.Invoke(new Action(delegate ()
                            {
                                labStatus.Text = "Connected";
                                labStatus.BackColor = Color.Chartreuse;
                            }));
                        soundPlayer = new SoundPlayer(Sounds[1]);
                        soundPlayer.Play();
                        consoleWrite("Connected to " + portName, Color.Green);
                        isConnected = true;
                        if (blockPanel.InvokeRequired)
                            blockPanel.Invoke(new Action(delegate ()
                            {
                                blockPanel.Enabled = true;
                            }));
                    }
                    catch (Exception e)
                    {
                        serialPort.Close();
                    }

                }
                Thread.Sleep(1000);
            }
        }

        String waitResponse()
        {
            String resp = "";
            Thread.Sleep(100);
            int i = 0;
            while (true)
            {
                try
                {
                    resp = serialPort.ReadTo(">");
                    if (resp == "\r\n")
                    {
                        serialPort.WriteLine("");
                        Thread.Sleep(100);
                        continue;
                    }

                    break;
                }
                catch (Exception ex) { };
                i++;
                if (i == 10)
                {
                    break;
                }
            }
            return resp;
        }
        
        private void comDelay()
        {
            String temp;

            if (resp[1].ToString() != "")
            {
                return;
            }
            while (true)
            {
                resp = com.command();
                if (resp[1].ToString() != "")
                {
                    do
                    {
                        temp = resp[1].ToString();
                        Thread.Sleep(100);
                        resp = com.command();
                        if (resp[1].ToString() == "")
                            break;
                        temp += resp[1].ToString();
                    } while (true);
                    resp[1] = temp;
                    break;
                }
                Thread.Sleep(100);
            }
        }
        
        private void writeArray(String str)
        {
            String[] strings;
            Char[] delim = new Char[] { '\n', '\r' };
            strings = str.Split(delim, StringSplitOptions.RemoveEmptyEntries);
            foreach (String s in strings)
            {
                consoleWrite(s);
            }
        }

        void downloadBackup()
        {
            bool isShowed = false;
            while (true)
            {
                String dirName = robotName;
                String robName = DateTime.Now.ToString("ddMMyy");
                String resp;
                String[] respl;
                Char[] delim = new Char[] { '\n' };
                switch (mode)
                {
                    case 1:
                        {
                            backupUSB(dirName, robName, ref isShowed);
                            isTyping = true;
                            break;
                        }
                    case 2:
                        {
                            //RS
                            backupRS232(dirName, robName);
                            isTyping = true;
                            break;
                        }
                    default:
                        break;
                }
            }
        }

        private void boxPortList_SelectedIndexChanged(object sender, EventArgs e)
        {
            //portName = boxPortList.SelectedItem.ToString();
            if (portName != boxPortList.SelectedItem.ToString())
            {
                portName = boxPortList.SelectedItem.ToString();
                try
                {
                    if (serialPort.IsOpen)
                    {
                        serialPort.Close();
                        //isConnected = false;
                    }

                }
                catch (Exception ex) { };
            }
            //isConnected = false;}
        }

        private void textRobotName_TextChanged(object sender, EventArgs e)
        {
            robotName = textRobotName.Text;
        }

        private void trigUSB_Click(object sender, EventArgs e)
        {
            blockPanel.Enabled = false;
            btnStop.Enabled = true;
            isBusy = true;
            mode = 1;
        }

        private void trigRS232_Click(object sender, EventArgs e)
        {
            blockPanel.Enabled = false;
            btnStop.Enabled = true;
            isBusy = true;
            mode = 2;
        }

        private void btnSide_Click(object sender, EventArgs e)
        {
            if (btnSide.Text == "Less")
            {
                this.Width = 345;
                btnSide.Text = "More";
            }
            else
            {
                this.Width = 885;
                btnSide.Text = "Less";
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            soundPlayer = new SoundPlayer(Sounds[0]);
            soundPlayer.Play();
            portFinderThread.Start();
            checkConThread.Start();
            tryConThread.Start();
            downloadThread.Start();
            Thread.Sleep(1000);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            tryConThread.Abort();
            checkConThread.Abort();
            downloadThread.Abort();
            portFinderThread.Abort();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            isTyping = false;
            consoleWrite("Cancelled", Color.Red);
            if (serialPort.IsOpen)
            {
                
                    serialPort.Close();
                    serialPort.Open();
            }
            else
            {
 
                com.disconnect();
                serialPort.Open();

                    //Thread.Sleep(500);
                //for (int i = 0; i < 10; i++)
                //{
                serialPort.WriteLine("\r\n\r\n\r\n\r\n");
                //}
            }
            btnStop.Enabled = false;
        }


    }
}




/*
                            lock (this)
                            {
                                if (labStatus.InvokeRequired)
                                    labStatus.Invoke(new Action(delegate ()
                                    {
                                        labStatus.Text = "Backuping";
                                        labStatus.ForeColor = Color.Orange;
                                    }));
                                serialPort.WriteLine("USB_FDEL " + dirName);
                                resp = waitResponse();
                                respl = resp.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                                if (respl[1] == "(P2103)USB memory is not inserted.\r")
                                {
                                    if (!isShowed)
                                    {
                                        consoleWrite("Please input USB memory", Color.Red);
                                        Thread.Sleep(1000);
                                        soundPlayer = new SoundPlayer(Sounds[5]);
                                        soundPlayer.Play();
                                        isShowed = true;
                                    }
                                    break;
                                }
                                soundPlayer = new SoundPlayer(Sounds[3]);
                                soundPlayer.Play();
                                consoleWrite("Backuping", Color.Orange);
                                isShowed = false;

                                serialPort.WriteLine("USB_MKDIR " + dirName);
                                resp = waitResponse();
                                consoleWrite("Saving backup", Color.Blue);

                                serialPort.WriteLine("USB_SAVE " + dirName + "\\" + robName);
                                resp = waitResponse();
                                consoleWrite(resp);
                                consoleWrite("Backup saved\n", Color.Green);
                                consoleWrite("Saving operation log", Color.Blue);
                                serialPort.WriteLine("USB_SAVE/OPLOG " + dirName + "\\" + robName);
                                resp = waitResponse();
                                consoleWrite(resp);
                                consoleWrite("Operation log saved\n", Color.Green);
                                //consoleWrite(resp);
                                consoleWrite("Saving error log", Color.Blue);
                                serialPort.WriteLine("USB_SAVE/ELOG " + dirName + "\\" + robName);
                                resp = waitResponse();
                                consoleWrite(resp);
                                consoleWrite("Error log saved\n", Color.Green);
                                consoleWrite("Finished\n", Color.Green);
                                soundPlayer = new SoundPlayer(Sounds[4]);
                                soundPlayer.Play();

                                if (labStatus.InvokeRequired)
                                    labStatus.Invoke(new Action(delegate ()
                                    {
                                        labStatus.Text = "Connected";
                                        labStatus.ForeColor = Color.Green;
                                    }));
                                mode = 0;
                                isBusy = false;
                                
                            }*/
