using Dobot_TCP.com.dobot.api;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Dobot_TCP
{
    public partial class Form1 : Form
    {
        private readonly Feedback mFeedback = new Feedback();
        private readonly DobotMove mDobotMove = new DobotMove();
        private readonly Dashboard mDashboard = new Dashboard();

        //定时获取数据并显示到UI
        private readonly System.Timers.Timer mTimerReader = new System.Timers.Timer(300);

        private bool isClearByCode = false;
        private bool[] isRealInput = Enumerable.Repeat(true, 16).ToArray();
        public Form1()
        {
            InitializeComponent();
            mFeedback.NetworkErrorEvent += new DobotClient.OnNetworkError(this.OnNetworkErrorEvent_Feedback);
            mDobotMove.NetworkErrorEvent += new DobotClient.OnNetworkError(this.OnNetworkErrorEvent_DobotMove);
            mDashboard.NetworkErrorEvent += new DobotClient.OnNetworkError(this.OnNetworkErrorEvent_Dashboard);
            foreach (Button btn in groupBoxOutput.Controls.OfType<Button>()) btn.Click += new EventHandler(DOInput_Click);
            foreach (Button btn in groupBoxInput.Controls.OfType<Button>()) btn.Click += new EventHandler(InputSim_Click);
            foreach (Button btn in groupBoxFeedback.Controls.OfType<Button>())
            {
                btn.MouseDown += new MouseEventHandler(this.OnMoveJogEvent);
                btn.MouseUp += new MouseEventHandler(this.OnStopMoveJogEvent);
            }
            foreach (Control ctr in groupBoxConnect.Controls)
            {
                if (ctr != btnConnect)
                {
                    ctr.MouseHover += new EventHandler(ShowToolTip);
                    ctr.MouseLeave += new EventHandler(HideToolTip);
                }
                if (ctr is TextBox txt) txt.KeyDown += new KeyEventHandler(OnGetConnectInfo);
            }
            //启动定时器
            mTimerReader.Elapsed += new System.Timers.ElapsedEventHandler(TimeoutEvent);

            //默认禁止窗口中的大部分控件
            DisableWindow();

            string strPath = Application.StartupPath + "\\";
            ErrorInfoHelper.ParseControllerJsonFile(strPath + "alarm_controller.json");
            ErrorInfoHelper.ParseServoJsonFile(strPath + "alarm_servo.json");
        }

        private void OnGetConnectInfo(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) btnConnect_Click(null, null);
        }
        private void ShowToolTip(object sender, EventArgs e)
        {
            if (sender is Control ctr)
            {
                string name = ctr.Tag.ToString();
                string msg;
                Point mousePos = ctr.PointToClient(MousePosition);
                switch (name)
                {
                    case "IP":
                    {
                        msg = "IP of the robot, default is 192.168.1.6";
                        break;
                    }
                    case "Dashboard":
                    {
                        msg = "Port for sending dashboard commands, default is 29999";
                        break;
                    }
                    case "Move":
                    {
                        msg = "Port for sending motion commands, default is 30003";
                        break;
                    }
                    case "Feedback":
                    {
                        msg = "Port for getting working status of the robot, default is 30004";
                        break;
                    }
                    default: return;
                }
                toolTipConnect.Show(msg, ctr, mousePos.X + 10, mousePos.Y + 10);
            }
        }
        private void HideToolTip(object sender, EventArgs e)
        {
            if (sender is Control ctr) toolTipConnect.Hide(ctr);
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            mTimerReader.Close();
            this.mFeedback?.Disconnect();
            this.mDashboard?.Disconnect();
            this.mDobotMove?.Disconnect();
        }
        private void InsertLogToRichBox(RichTextBox box, string str)
        {
            if (box.GetLineFromCharIndex(box.TextLength) > 100) box.Text = (str += "\r\n");
            else box.Text += (str + "\r\n");
            //box.Focus();
            box.Select(box.TextLength, 0);
            box.ScrollToCaret();
        }
        private void PrintLog(string str)
        {
            if (string.IsNullOrEmpty(str)) return;
            if (this.richTextBoxLog.InvokeRequired)
            {
                this.richTextBoxLog.Invoke(new Action<string>(log =>
                {
                    InsertLogToRichBox(this.richTextBoxLog, log);
                }), str);
            }
            else InsertLogToRichBox(this.richTextBoxLog, str);
        }
        private void PrintErrorInfo(string str)
        {
            if (string.IsNullOrEmpty(str)) return;
            if (this.richTextBoxErrInfo.InvokeRequired)
            {
                this.richTextBoxErrInfo.Invoke(new Action<string>(log =>
                {
                    InsertLogToRichBox(this.richTextBoxErrInfo, log);
                }), str);
            }
            else InsertLogToRichBox(this.richTextBoxErrInfo, str);
        }

        private void DisableWindow()
        {
            foreach (Control ctr in this.Controls) ctr.Enabled = (ctr == this.groupBoxConnect || ctr == this.groupBoxLog);
        }
        private void EnableWindow()
        {
            foreach (Control ctr in this.Controls) ctr.Enabled = true;
        }

        private void OnMoveJogEvent(object sender, MouseEventArgs e)
        {
            if (sender is Button btn) DoMoveJog(btn.Text);
        }
        private void OnStopMoveJogEvent(object sender, MouseEventArgs e)
        {
            if (sender is Button) DoStopMoveJog();
        }

        private void DoMoveJog(string str)
        {
            PrintLog(string.Format("send to {0}:{1}: MoveJog({2})", mDobotMove.IP, mDobotMove.Port, str));
            Thread thd = new Thread(() =>
            {
                string ret = mDobotMove.MoveJog(str);
                PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
            });
            thd.Start();
        }

        private void DoStopMoveJog()
        {
            PrintLog(string.Format("send to {0}:{1}: MoveJog()", mDobotMove.IP, mDobotMove.Port));
            Thread thd = new Thread(() =>
            {
                string ret = mDobotMove.StopMoveJog();
                PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
            });
            thd.Start();
        }
        private void TimeoutEvent(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!mFeedback.DataHasRead) return;
            mFeedback.DataHasRead = false;
            this.Invoke(new Action(() =>
            {
                ShowDataResult();
            }));
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (this.btnConnect.Text.Equals("Disconnect"))
            {
                mIsManualDisconnect = true;
                Disconnect();
                return;
            }
            Connect();
        }

        private bool mIsManualDisconnect = false;
        private void DoNetworkErrorEvent(DobotClient sender, string strIp, int iPort)
        {
            DisableWindow();
            PrintLog("retry connecting...");
            Thread thd = new Thread(() =>
            {
                sender.Disconnect();

                mTimerReader.Stop();

                if (!sender.Connect(strIp, iPort))
                {
                    PrintLog("Connect Fail!!!");
                    Thread.Sleep(500);
                    DoNetworkErrorEvent(sender, strIp, iPort);
                    return;
                }

                mTimerReader.Start();

                PrintLog("Connect Success!!!");

                this.Invoke(new Action(() =>
                {
                    EnableWindow();
                }));
            });
            thd.Start();
        }
        /// <summary>
        /// 当发生网络错误时，触发该事件
        /// </summary>
        /// <param name="sender">发送错误的对象</param>
        /// <param name="iErrCode">网络错误码</param>
        private void OnNetworkErrorEvent_Feedback(DobotClient sender, SocketError iErrCode)
        {
            if (mIsManualDisconnect) return;
            this.BeginInvoke(new Action(() =>
            {
                string strIp = textBoxIP.Text;
                int iPort = int.Parse(this.textBoxFeedbackPort.Text);
                DoNetworkErrorEvent(mFeedback, strIp, iPort);
            }));
        }
        private void OnNetworkErrorEvent_DobotMove(DobotClient sender, SocketError iErrCode)
        {
            if (mIsManualDisconnect) return;
            this.BeginInvoke(new Action(() =>
            {
                string strIp = textBoxIP.Text;
                int iPort = int.Parse(this.textBoxMovePort.Text);
                DoNetworkErrorEvent(mDobotMove, strIp, iPort);
            }));
        }
        private void OnNetworkErrorEvent_Dashboard(DobotClient sender, SocketError iErrCode)
        {
            if (mIsManualDisconnect) return;
            this.BeginInvoke(new Action(() =>
            {
                string strIp = textBoxIP.Text;
                int iPort = int.Parse(this.textBoxDashboardPort.Text);
                DoNetworkErrorEvent(mDashboard, strIp, iPort);
            }));
        }
        private void Connect()
        {
            string strIp = textBoxIP.Text;
            if (!IPAddress.TryParse(strIp, out IPAddress addr))
            {
                MessageBox.Show("IP Address Invalid");
                return;
            }
            int iPortFeedback = int.Parse(this.textBoxFeedbackPort.Text);
            int iPortMove = int.Parse(this.textBoxMovePort.Text);
            int iPortDashboard = int.Parse(this.textBoxDashboardPort.Text);

            PrintLog("Connecting...");
            this.btnConnect.Enabled = false;
            Thread thd = new Thread(() =>
            {
                if (!mDashboard.Connect(strIp, iPortDashboard))
                {
                    PrintLog(string.Format("Connect {0}:{1} Fail!!", strIp, iPortDashboard));
                    Invoke(new Action(() => { foreach (Control ctr in this.groupBoxConnect.Controls) ctr.Enabled = true; }));
                    return;
                }
                if (!mDobotMove.Connect(strIp, iPortMove))
                {
                    PrintLog(string.Format("Connect {0}:{1} Fail!!", strIp, iPortMove));
                    Invoke(new Action(() => { foreach (Control ctr in this.groupBoxConnect.Controls) ctr.Enabled = true; }));
                    return;
                }
                if (!mFeedback.Connect(strIp, iPortFeedback))
                {
                    PrintLog(string.Format("Connect {0}:{1} Fail!!", strIp, iPortFeedback));
                    Invoke(new Action(() => { foreach (Control ctr in this.groupBoxConnect.Controls) ctr.Enabled = true; }));
                    return;
                }

                mIsManualDisconnect = false;
                mTimerReader.Start();
                PrintLog("Connect Success!!!");

                this.Invoke(new Action(() =>
                {
                    EnableWindow();
                    this.btnConnect.Text = "Disconnect";
                    foreach (Control ctr in this.groupBoxConnect.Controls) ctr.Enabled = (ctr == btnConnect);
                }));
            });
            thd.Start();

        }
        private void Disconnect()
        {
            PrintLog("Disconnecting...");
            Thread thd = new Thread(() =>
            {
                mFeedback.Disconnect();
                mDobotMove.Disconnect();
                mDashboard.Disconnect();
                PrintLog("Disconnect success!!!");

                mTimerReader.Stop();

                this.Invoke(new Action(() =>
                {
                    DisableWindow();
                    this.btnConnect.Text = "Connect";
                    foreach (Control ctr in this.groupBoxConnect.Controls) ctr.Enabled = true;
                }));
            });
            thd.Start();

        }

        private void btnEnable_Click(object sender, EventArgs e)
        {
            bool bEnable = !mFeedback.IsEnabled();

            PrintLog(string.Format("send to {0}:{1}: {2}()", mDashboard.IP, mDashboard.Port, bEnable ? "EnableRobot" : "DisableRobot"));
            Thread thd = new Thread(() =>
            {
                string ret = bEnable ? mDashboard.EnableRobot() : mDashboard.DisableRobot();
                bool bOk = ret.StartsWith("0");

                this.btnEnable.Invoke(new Action(() =>
                {
                    if (bOk)
                    {
                        this.btnEnable.Text = bEnable ? "Disable" : "Enable";
                        bEnable = !mFeedback.IsEnabled();
                    }
                }));

                PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
            });
            thd.Start();
        }

        private void btnEnableAgain_Click(object sender, EventArgs e)
        {
            PrintLog(string.Format("send to {0}:{1}: {2}()", mDashboard.IP, mDashboard.Port, "EnableRobot"));
            Thread thd = new Thread(() =>
            {
                string ret = mDashboard.EnableRobot();
                bool bOk = ret.StartsWith("0");
                PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
            });
            thd.Start();
        }

        private void btnResetRobot_Click(object sender, EventArgs e)
        {
            PrintLog(string.Format("send to {0}:{1}: ResetRobot()", mDashboard.IP, mDashboard.Port));
            Thread thd = new Thread(() =>
            {
                string ret = mDashboard.ResetRobot();
                PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
            });
            thd.Start();
        }

        private void btnClearError_Click(object sender, EventArgs e)
        {
            PrintLog(string.Format("send to {0}:{1}: ClearError()", mDashboard.IP, mDashboard.Port));
            Thread thd = new Thread(() =>
            {
                string ret = mDashboard.ClearError();
                PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
            });
            thd.Start();
        }

        private void DOInput_Click(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                int idx = int.Parse(btn.Tag.ToString());
                bool bIsOn = string.Compare("on", btn.Text, true) == 0; //phím hiện on thì khi nhấn thực hiện off và ngược lại
                PrintLog(string.Format("send to {0}:{1}: DigitalOutputs({2},{3})", mDashboard.IP, mDashboard.Port,
                    idx, bIsOn ? "OFF" : "ON"));
                btn.Text = bIsOn ? "OFF" : "ON";
                btn.BackColor = (btn.Text == "ON") ? Color.LightGreen : Color.Transparent;
                Thread thd = new Thread(() =>
                {
                    string ret = mDashboard.DigitalOutputs(idx, !bIsOn);
                    PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
                });
                thd.Start();
            }
        }
        private void InputSim_Click(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                int idx = int.Parse(btn.Tag.ToString());
                if (isRealInput[idx])
                {
                    DialogResult result = MessageBox.Show($"Simulate the input signal {idx}?", "CONFIRM", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                    if (result == DialogResult.Cancel) return;
                    isRealInput[idx] = false;
                    btn.Text = "SIM";
                    btn.BackColor = Color.LightBlue;
                }
                else
                {
                    DialogResult result = MessageBox.Show($"Get the real input signal {idx}?", "CONFIRM", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                    if (result == DialogResult.Cancel) return;
                    isRealInput[idx] = true;
                }
            }
        }
        private void btnClearErrorInfo_Click(object sender, EventArgs e)
        {
            this.richTextBoxErrInfo.Clear();
        }

        private void ShowDataResult()
        {
            this.labCurrentSpeedRatio.Text = string.Format("Current Speed Ratio:{0:F0}%", mFeedback.feedbackData.SpeedScaling);
            this.labRobotMode.Text = string.Format("Robot Mode:{0}", mFeedback.ConvertRobotMode());
            this.btnEnable.Text = mFeedback.IsEnabled() ? "Disable" : "Enable";
            this.lblTest.Text = "Test 64 bit block: 0x" + Convert.ToString(mFeedback.feedbackData.TestValue, 16).PadLeft(16, '0');
            if (trackBarSpeed.Value == 0) trackBarSpeed.Value = (int)mFeedback.feedbackData.SpeedScaling;
            lblSpeed.Text = trackBarSpeed.Value.ToString();
            if (null != mFeedback.feedbackData.QActual && mFeedback.feedbackData.QActual.Length >= 4)
            {
                this.labJ1.Text = string.Format("J1:{0:F3}", mFeedback.feedbackData.QActual[0]);
                this.labJ2.Text = string.Format("J2:{0:F3}", mFeedback.feedbackData.QActual[1]);
                this.labJ3.Text = string.Format("J3:{0:F3}", mFeedback.feedbackData.QActual[2]);
                this.labJ4.Text = string.Format("J4:{0:F3}", mFeedback.feedbackData.QActual[3]);

                if (textBoxJ1.Text.Length == 0)
                {//第一次填充数据，免得用的时候一个一个输入
                    this.textBoxJ1.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[0]);
                    this.textBoxJ2.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[1]);
                    this.textBoxJ3.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[2]);
                    this.textBoxJ4.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[3]);
                }
            }

            if (null != mFeedback.feedbackData.ToolVectorActual && mFeedback.feedbackData.ToolVectorActual.Length >= 4)
            {
                this.labX.Text = string.Format("X:{0:F3}", mFeedback.feedbackData.ToolVectorActual[0]);
                this.labY.Text = string.Format("Y:{0:F3}", mFeedback.feedbackData.ToolVectorActual[1]);
                this.labZ.Text = string.Format("Z:{0:F3}", mFeedback.feedbackData.ToolVectorActual[2]);
                this.labRx.Text = string.Format("R:{0:F3}", mFeedback.feedbackData.ToolVectorActual[3]);

                if (textBoxX.Text.Length == 0)
                {//第一次填充数据，免得用的时候一个一个输入
                    this.textBoxX.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[0]);
                    this.textBoxY.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[1]);
                    this.textBoxZ.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[2]);
                    this.textBoxRx.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[3]);
                }
            }
            string DIstring = Convert.ToString(mFeedback.feedbackData.DigitalInputs, 2).PadLeft(64, '0');
            string DOstring = Convert.ToString(mFeedback.feedbackData.DigitalOutputs, 2).PadLeft(64, '0');
            foreach (Button btn in groupBoxInput.Controls.OfType<Button>())
            {
                //if (isRealInput[int.Parse(btn.Tag.ToString())])
                {
                    btn.Text = (DIstring[DIstring.Length - int.Parse(btn.Tag.ToString())] == '1') ? "ON" : "OFF";
                    btn.BackColor = (btn.Text == "ON") ? Color.LightGreen : Color.Transparent;
                }
            }
            if (groupBoxConnect.Tag == null)
            {
                foreach (Button btn in groupBoxOutput.Controls.OfType<Button>())
                {
                    btn.Text = (DOstring[DOstring.Length - int.Parse(btn.Tag.ToString())] == '1') ? "ON" : "OFF";
                    btn.BackColor = (btn.Text == "ON") ? Color.LightGreen : Color.Transparent;
                }
                groupBoxConnect.Tag = "1";
            }
            ParseWarn();
        }

        private void ParseWarn()
        {
            if (this.mFeedback.feedbackData.RobotMode != 9) return;
            string strResult = mDashboard.GetErrorID();
            //strResult=ErrorID,{[[id,...,id], [id], [id], [id], [id], [id], [id]]},GetErrorID()
            if (!strResult.StartsWith("0")) return;

            //截取第一个{}内容
            int iBegPos = strResult.IndexOf('{');
            if (iBegPos < 0) return;
            int iEndPos = strResult.IndexOf('}', iBegPos + 1);
            if (iEndPos <= iBegPos) return;
            strResult = strResult.Substring(iBegPos + 1, iEndPos - iBegPos - 1);
            if (string.IsNullOrEmpty(strResult)) return;

            //剩余7组[]，第1组是控制器报警，其他6组是伺服报警
            StringBuilder sb = new StringBuilder();
            JArray arrWarn = JArray.Parse(strResult);
            for (int i = 0; i < arrWarn.Count; ++i)
            {
                JArray arr = arrWarn[i].ToObject<JArray>();
                for (int j = 0; j < arr.Count; ++j)
                {
                    ErrorInfoBean bean = 0 == i ? ErrorInfoHelper.FindController(arr[j].ToObject<int>()) : ErrorInfoHelper.FindServo(arr[j].ToObject<int>());
                    if (null != bean)
                    {
                        sb.Append("ID:" + bean.id + "\r\n");
                        sb.Append("Type:" + bean.Type + "\r\n");
                        sb.Append("Level:" + bean.level + "\r\n");
                        sb.Append("Solution:" + bean.en.solution + "\r\n");
                    }
                }
            }

            if (sb.Length > 0)
            {
                DateTime dt = DateTime.Now;
                string strTime = string.Format("Time Stamp:{0}.{1}.{2} {3}:{4}:{5}", dt.Year,
                    dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
                PrintErrorInfo(strTime + "\r\n" + sb.ToString());
            }
            return;
        }

        private void btnMove_Click(object sender, EventArgs e)
        {
            string movetype = cmbMoveMode.Text;
            bool isRelative = btnRel.Text == "Relative";
            DescartesPoint pt = new DescartesPoint
            {
                x = double.Parse(this.textBoxX.Text),
                y = double.Parse(this.textBoxY.Text),
                z = double.Parse(this.textBoxZ.Text),
                r = double.Parse(this.textBoxRx.Text)
            };
            JointPoint jpt = new JointPoint
            {
                j1 = double.Parse(this.textBoxJ1.Text),
                j2 = double.Parse(this.textBoxJ2.Text),
                j3 = double.Parse(this.textBoxJ3.Text),
                j4 = double.Parse(this.textBoxJ4.Text)
            };
            OffsetPosition opt = new OffsetPosition
            {
                x = double.Parse(this.textBoxX.Text),
                y = double.Parse(this.textBoxY.Text),
                z = double.Parse(this.textBoxZ.Text),
                r = double.Parse(this.textBoxRx.Text),
                user = 0
            };
            OffsetPosition ojpt = new OffsetPosition
            {
                x = double.Parse(this.textBoxJ1.Text),
                y = double.Parse(this.textBoxJ2.Text),
                z = double.Parse(this.textBoxJ3.Text),
                r = double.Parse(this.textBoxJ4.Text),
                user = 0
            };
            switch (movetype)
            {
                case "MovJ":
                {
                    if (!isRelative)
                    {
                        PrintLog(string.Format("send to {0}:{1}: MovJ({2})", mDobotMove.IP, mDobotMove.Port, pt.ToString()));
                        Thread thd = new Thread(() =>
                        {
                            string ret = mDobotMove.MovJ(pt);
                            PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                        });
                        thd.Start();
                    }
                    else
                    {
                        PrintLog(string.Format("send to {0}:{1}: RelMovJUser({2})", mDobotMove.IP, mDobotMove.Port, pt.ToString()));
                        Thread thd = new Thread(() =>
                        {
                            string ret = mDobotMove.RelMovJUser(opt);
                            PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                        });
                        thd.Start();
                    }
                    break;
                }
                case "MovL":
                {
                    if (!isRelative)
                    {
                        PrintLog(string.Format("send to {0}:{1}: MovL({2})", mDobotMove.IP, mDobotMove.Port, pt.ToString()));
                        Thread thd = new Thread(() =>
                        {
                            string ret = mDobotMove.MovL(pt);
                            PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                        });
                        thd.Start();
                    }
                    else
                    {
                        PrintLog(string.Format("send to {0}:{1}: RelMovLUser({2})", mDobotMove.IP, mDobotMove.Port, pt.ToString()));
                        Thread thd = new Thread(() =>
                        {
                            string ret = mDobotMove.RelMovLUser(opt);
                            PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                        });
                        thd.Start();
                    }
                    break;
                }
                case "JointMovJ":
                {
                    if (!isRelative)
                    {
                        PrintLog(string.Format("send to {0}:{1}: JointMovJ({2})", mDobotMove.IP, mDobotMove.Port, pt.ToString()));
                        Thread thd = new Thread(() =>
                        {
                            string ret = mDobotMove.JointMovJ(jpt);
                            PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                        });
                        thd.Start();
                    }
                    else
                    {
                        PrintLog(string.Format("send to {0}:{1}: RelJointMovJ({2})", mDobotMove.IP, mDobotMove.Port, pt.ToString()));
                        Thread thd = new Thread(() =>
                        {
                            string ret = mDobotMove.RelJointMovJ(ojpt);
                            PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                        });
                        thd.Start();
                    }
                    break;
                }
                default:
                {
                    PrintLog("Wrong command");
                    break;
                }
            }
        }

        private void btnRel_Click(object sender, EventArgs e)
        {
            if (btnRel.Text == "Absolute")
            {
                btnRel.Text = "Relative";
                foreach (TextBox txt in groupBoxMove.Controls.OfType<TextBox>()) txt.Text = "0";
            }
            else
            {
                btnRel.Text = "Absolute";
                this.textBoxJ1.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[0]);
                this.textBoxJ2.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[1]);
                this.textBoxJ3.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[2]);
                this.textBoxJ4.Text = string.Format("{0:F3}", mFeedback.feedbackData.QActual[3]);
                this.textBoxX.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[0]);
                this.textBoxY.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[1]);
                this.textBoxZ.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[2]);
                this.textBoxRx.Text = string.Format("{0:F3}", mFeedback.feedbackData.ToolVectorActual[3]);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show("You are about to exit the program and will not be able to control the robot. Are you sure?", "CONFIRM", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            if (result == DialogResult.No) e.Cancel = true;
        }
        private readonly List<string> cmdHistory = new List<string>(5);
        private int historyIndex = -1;
        private void btnSendCmd_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtCmd.Text))
            {
                string cmd = txtCmd.Text;
                List<string> keys = new List<string>
                {
                    "mov", "arc", "circle", "sync"
                };

                bool isMotionCmd = false;
                if (!cmdHistory.Contains(cmd) && cmd != string.Empty)
                {
                    cmdHistory.Add(cmd);
                    if (cmdHistory.Count > 5) cmdHistory.RemoveAt(0);
                }
                historyIndex = cmdHistory.Count;
                isClearByCode = true;
                txtCmd.Clear();
                txtCmd.Focus();
                isClearByCode = false;
                var autoSource = new AutoCompleteStringCollection();
                autoSource.AddRange(cmdHistory.ToArray());
                txtCmd.AutoCompleteCustomSource = autoSource;
                foreach (string item in keys)
                {
                    isMotionCmd = (cmd.IndexOf(item, StringComparison.OrdinalIgnoreCase) != -1);
                    if (isMotionCmd) break;
                }
                if (isMotionCmd)
                {
                    PrintLog(string.Format("send to {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, cmd));
                    Thread thd = new Thread(() =>
                    {
                        string ret = mDobotMove.CustomCommand(cmd);
                        PrintLog(string.Format("Receive From {0}:{1}: {2}", mDobotMove.IP, mDobotMove.Port, ret));
                    });
                    thd.Start();
                }
                else
                {
                    PrintLog(string.Format("send to {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, cmd));
                    Thread thd = new Thread(() =>
                    {
                        string ret = mDashboard.CustomCommand(cmd);
                        PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
                    });
                    thd.Start();
                }
            }
            else
            {
                toolTipCmd.Show("You cannot send blank command", txtCmd, 0, -60, 1000);
            }
        }

        private void txtCmd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSendCmd_Click(null, null);
            }
            else
            {
                if (cmdHistory.Count == 0) return;

                if (e.KeyCode == Keys.Up)
                {
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        txtCmd.Text = cmdHistory[historyIndex];
                        txtCmd.SelectionStart = txtCmd.Text.Length; // đưa con trỏ về cuối
                    }
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (historyIndex < cmdHistory.Count - 1)
                    {
                        historyIndex++;
                        txtCmd.Text = cmdHistory[historyIndex];
                        txtCmd.SelectionStart = txtCmd.Text.Length;
                    }
                    else
                    {
                        // Nếu xuống quá cuối thì xóa TextBox để nhập mới
                        historyIndex = cmdHistory.Count;
                        txtCmd.Clear();
                    }
                    e.Handled = true;
                }
            }
        }

        private void trackBarSpeed_MouseUp(object sender, MouseEventArgs e)
        {
            int iValue = this.trackBarSpeed.Value;
            if (iValue > 0)
            {
                PrintLog(string.Format("send to {0}:{1}: SpeedFactor({1})", mDashboard.IP, mDashboard.Port, iValue));
                Thread thd = new Thread(() =>
                {
                    string ret = mDashboard.SpeedFactor(iValue);
                    PrintLog(string.Format("Receive From {0}:{1}: {2}", mDashboard.IP, mDashboard.Port, ret));
                });
                thd.Start();
            }
        }

        private void trackBarSpeed_ValueChanged(object sender, EventArgs e)
        {
            lblSpeed.Text = trackBarSpeed.Value.ToString();
        }

        private void lblTest_MouseHover(object sender, EventArgs e)
        {
            Point mousePos = lblTest.PointToClient(MousePosition);
            toolTipTestValue.Show("If this value is not 0x0123456789abcdef, then the data is corrupted. Please check your connection to the robot.", lblTest, mousePos.X + 10, mousePos.Y + 10);
        }

        private void txtCmd_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtCmd.Text) && !isClearByCode) toolTipCmd.Show("You cannot send blank command", txtCmd, -10, -70, 1000);

        }

        private void lblTest_MouseLeave(object sender, EventArgs e)
        {
            toolTipTestValue.Hide(lblTest);
        }
    }
}
