using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO.Ports; //跟串口相关，不能只是引用system.IO
using System.Threading; //跟串口相关，线程的引入
using System.Windows.Threading;//timer


namespace elevator
{
    using System.Net.Json;

    /// <summary>
    /// 电梯发送信息
    /// </summary>
    struct EvelatorInfo
    {
        /// <summary>
        /// 楼层号：从１开始
        /// </summary>
        public int floor;

        public bool up;
        public bool down;
        /// <summary>
        /// 与电梯侧通信状态：true－正常
        /// </summary>
        public bool conn1;
        /// <summary>
        /// 与PC通信状态：true－正常
        /// </summary>
        public bool conn2;
        /// <summary>
        /// 是否故障报警:true-error
        /// </summary>
        public bool error;
        /// <summary>
        /// 维保状态：true －通常状态，false－维保状态
        /// </summary>
        public bool modify;

        public double angle;
    };


    struct DoubleRect
    {
        public double x, y;
        public double w, h;
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {      
        
//         //委托；此为重点,更新界面
//         delegate void HandleInterfaceUpdateDelagate(ref EvelatorInfo info);
//         HandleInterfaceUpdateDelagate interfaceUpdateHandle;
        /// <summary>
        /// pointer rotate
        /// </summary>
        RotateTransform mRotate = new RotateTransform(0.0);

        bool mFullScreen = false;
        DoubleRect mWinRect = new DoubleRect();

        /// <summary>
        /// 最大楼层数
        /// </summary>
        int mMaxFloor = 7;
        /// <summary>
        /// 楼层角度
        /// </summary>
        double mFloorAngle = 0.0;
        /// <summary>
        /// 楼层间电梯运行时长，毫秒秒
        /// </summary>
        int mFloorTime = 3000;

        double mAutoAngle = 0.0;

        /// <summary>
        /// 上次信息
        /// </summary>
        EvelatorInfo mCurInfo = new EvelatorInfo { };


        DispatcherTimer mAutoTimer = new System.Windows.Threading.DispatcherTimer();
        DispatcherTimer mSendTimer = new System.Windows.Threading.DispatcherTimer();

        SerialPort mSerialPort = new SerialPort();
        byte[] mSendCmd = new byte[4];
        byte[] mReadBuf = new byte[6];

        public MainWindow()
        {
            InitializeComponent();


            loadCfg();
            InitParams();
            //InitSerialPort();
        }

        private void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            mWinRect.x = this.Left;
            mWinRect.y = this.Top;
            mWinRect.w = this.Width;
            mWinRect.h = this.Height;
//             mFullScreen = true;
//             FullScreen(mFullScreen);
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            DestroySerialPort();
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
        	// F1 全屏切换
            if (e.Key == Key.F11&& (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                mFullScreen = !mFullScreen;
                FullScreen(mFullScreen);
                return;
            }

            //ctrl+f5　刻度显示
            if (e.Key == Key.F5 && (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (Visibility.Hidden == mTick.Visibility)
                    mTick.Visibility = Visibility.Visible;
                else
                    mTick.Visibility = Visibility.Hidden;
                return;
            }
        }

        private void loadCfg()
        {
            string jsonText = System.IO.File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "setting.cfg");
            JsonTextParser parser = new JsonTextParser();
            JsonObject root = null;
            try
            {
                root = parser.Parse(jsonText);
            }
            catch (Exception e)
            {

            }

            if (root == null) //default
            {
                mSerialPort.PortName = "COM1"; //串口号（参考串口调试助手
                mSerialPort.BaudRate = 9600; //波特率
                mSerialPort.Parity = Parity.Even; //校验位
                mSerialPort.DataBits = 8; //数据位
                mSerialPort.StopBits = StopBits.One; //停止位    

                // byte[] bytesSend;// = System.Text.Encoding.Default.GetBytes(txtSend.Text);
                mSendCmd[0] = 0xFA;
                mSendCmd[1] = 0xFF;//发送主机地址
                mSendCmd[2] = 0x00;//目标从机地址
                mSendCmd[3] = 0xFE;

                return;
            }

            JsonObjectCollection lst = root as JsonObjectCollection;
            JsonObject obj = lst["com"];
            if (null != obj)
            {
                JsonObjectCollection prms = obj as JsonObjectCollection;
                mSerialPort.PortName = ((JsonStringValue)prms["name"]).Value;
                mSerialPort.BaudRate = (int)((JsonNumericValue )prms["baudrate"]).Value;
                mSerialPort.Parity = (Parity)((JsonNumericValue)prms["parity"]).Value;
                mSerialPort.DataBits = (int)((JsonNumericValue )prms["databits"]).Value;
                mSerialPort.StopBits = (StopBits)((JsonNumericValue )prms["stopbits"]).Value;
                mSendCmd[2] = (byte)((JsonNumericValue)prms["address"]).Value;
            }
            obj = lst["prm"];
            if (null != obj)
            {
                JsonObjectCollection prms = obj as JsonObjectCollection;
                mMaxFloor = (int)((JsonNumericValue)prms["maxfloor"]).Value;
                mFloorTime = (int)((JsonNumericValue)prms["floortime"]).Value;
            }
        }

        private void InitParams()
        {
            mCurInfo.floor = 1;
            mCurInfo.angle = -90.0;

            mFloorAngle = 180.0 / (mMaxFloor - 1);
            int autoIterval = 100;
            mAutoAngle = mFloorAngle / mFloorTime * autoIterval;

            ////
            mAutoTimer.Interval = new TimeSpan(0, 0, 0, 0, autoIterval);
            mAutoTimer.IsEnabled = true;
            mAutoTimer.Tick += new EventHandler(TimerAuto);

            mSendTimer.Interval = new TimeSpan(0, 0, 0, 0, mFloorTime / 5);
            mSendTimer.IsEnabled = true;
            mSendTimer.Tick += new EventHandler(TimerSend);
        }


        private void InitSerialPort()
        {

            if (mSerialPort.IsOpen)
            {
                mSerialPort.Close();
            }
            mSerialPort.Open();
            if (!mSerialPort.IsOpen)
            {
                //写日志
                MessageBox.Show("串口打开失败");
                return;
            }
            mSerialPort.DataReceived += new SerialDataReceivedEventHandler(ComDataReceivedEventHandler);
        }

        private void DestroySerialPort()
        {
            if (mSerialPort.IsOpen)
            {
                mSerialPort.Close();
            }
        }

        private void FullScreen(bool bFullscreen)
        {
            if (bFullscreen)
            {
                // 设置全屏    
                this.WindowState = System.Windows.WindowState.Normal;
                this.WindowStyle = System.Windows.WindowStyle.None;
                this.ResizeMode = System.Windows.ResizeMode.NoResize;
                this.Topmost = true;

                this.Left = 0.0;
                this.Top = 0.0;
                this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
                this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;    
            }
            else
            {
                this.WindowState = System.Windows.WindowState.Normal;
                this.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
                this.ResizeMode = System.Windows.ResizeMode.NoResize;
                this.Topmost = false;

                this.Left = mWinRect.x;
                this.Top = mWinRect.y;
                this.Width = mWinRect.w;
                this.Height = mWinRect.h;
            }

        }

        private void UpdateEvelator(ref EvelatorInfo info)
        {
             //故障...
            if (info.modify || info.error || !info.conn1 || !info.conn2)
            {
                //TODO:show....
                info.angle = -90.0;
                return;
            }

            if (info.floor > mMaxFloor)
                info.floor = mMaxFloor;
            if (info.floor < 1)
                info.floor = 1;

            double angle = (info.floor-1) * mFloorAngle - 90;
            
            if (!info.up && !info.down)//悬停
            {
                //指到特定楼层
                info.angle = angle;
            }
            else if (info.up)
            {
                info.angle += mAutoAngle;
            }
            else if (info.down)
            {
                info.angle -= mAutoAngle;
            }
            if (info.angle + 90 < 0.000000001)
                info.angle = -90.0;
            if (info.angle - 90 > 0.000000001)
                info.angle = 90.0;

            mRotate.Angle = info.angle;
            mPointer.RenderTransform = mRotate;
        }

        void TimerAuto(object sender, EventArgs e)
        {
            UpdateEvelator(ref mCurInfo);
        }


        void TimerSend(object sender, EventArgs e)
        {
            if (!mSerialPort.IsOpen)
            {
                mCurInfo.floor = 0;
                mCurInfo.up = false;
                mCurInfo.down = false;
                mCurInfo.conn1 = false;
                mCurInfo.conn2 = false;
                mCurInfo.error = true;
                mCurInfo.modify = true;
                return;
            }

            mSerialPort.Write(mSendCmd, 0, mSendCmd.Length);
        }

        private void ComDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (!mSerialPort.IsOpen)
                return;
            mSerialPort.Read(mReadBuf, 0, mReadBuf.Length);
            ParseReadData(ref mReadBuf);
        }

        private void ParseReadData(ref byte[] buf)
        {
            if (buf[0] != 0xfa || buf[1] != mSendCmd[2] || buf[2] != 0xff || buf[buf.Length - 1] != 0xfe)
                return;
            //floor
            mCurInfo.floor = buf[3];

            //status
            mCurInfo.up = ((buf[4] & 0x01) != 0x00);
            mCurInfo.down = ((buf[4]>>1 & 0x01) != 0x00);
            mCurInfo.conn1 = ((buf[4]>>2 & 0x01) == 0x00);
            mCurInfo.conn2 = ((buf[4] >> 3 & 0x01) == 0x00);
            mCurInfo.error = ((buf[4] >> 4 & 0x01) != 0x00);
            mCurInfo.modify = ((buf[4] >> 5 & 0x01) != 0x00);
        }


//         private void ReceiveThread()
//         {
//             Thread threadReceive = new Thread(new ParameterizedThreadStart(SynReceiveData));
//             threadReceive.Start(mSerialPort);
//         }
// 
//         /// <summary>
//         /// 同步阻塞读取
//         /// </summary>
//         /// <param name="serialPortobj"></param>
//         private void SynReceiveData(object serialPortobj)
//         {
//             SerialPort serialPort = (SerialPort)serialPortobj;
//             System.Threading.Thread.Sleep(0);
//             serialPort.ReadTimeout = 1000;
// 
//             try
//             {
//                 //先记录下来，避免某种原因，人为的原因，操作几次之间时间长，缓存不一致
//                 int n = serialPort.BytesToRead;
//                 byte[] buf = new byte[n];//声明一个临时数组存储当前来的串口数据
//                 //received_count += n;//增加接收计数
// 
//                 serialPort.Read(buf, 0, n);//读取缓冲数据
// 
//                 //因为要访问ui资源，所以需要使用invoke方式同步ui
//                 interfaceUpdateHandle = new HandleInterfaceUpdateDelagate(UpdateEvelator);//实例化委托对象
//                 Dispatcher.Invoke(interfaceUpdateHandle,null /*new string[] { Encoding.ASCII.GetString(buf) }*/);
//             }
//             catch (System.Exception ex)
//             {
//                 MessageBox.Show(ex.Message);
//             }
//         }

        private void btnClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
//             mCurInfo.door = 2;
        }

        private void btnOpen_Click(object sender, System.Windows.RoutedEventArgs e)
        {
//             mCurInfo.door = 1;
        }

        private void btnUp_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            mCurInfo.floor += 1;
            mCurInfo.up = true;
            mCurInfo.down = false;
            if (mCurInfo.floor > mMaxFloor)
            {
                mCurInfo.floor = mMaxFloor;
            }
        }

        private void btnDown_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            mCurInfo.floor -= 1;
            mCurInfo.up = false;
            mCurInfo.down = true;
            if (mCurInfo.floor <= 0)
            {
                mCurInfo.floor = 1;
            }
        }




    }
}
