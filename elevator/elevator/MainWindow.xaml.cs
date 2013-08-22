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
    /// <summary>
    /// 电梯发送信息
    /// </summary>
    struct EvelatorInfo
    {
        public int updown;//1-up,2-down
        /// <summary>
        /// 楼层号：从１开始
        /// </summary>
        public int floor;
        /// <summary>
        /// 与电梯侧通信状态：true－正常
        /// </summary>
        public bool conn1;
        /// <summary>
        /// 与PC通信状态：true－正常
        /// </summary>
        public bool conn2;
        /// <summary>
        /// 是否故障报警
        /// </summary>
        public bool error;
        /// <summary>
        /// 维保状态：０－通常状态，１－维保状态
        /// </summary>
        public int modify;

        public double angle;
    };

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {      
        
        //委托；此为重点,更新界面
        delegate void HandleInterfaceUpdateDelagate(ref EvelatorInfo info);
        HandleInterfaceUpdateDelagate interfaceUpdateHandle;
        /// <summary>
        /// pointer rotate
        /// </summary>
        RotateTransform mRotate = new RotateTransform(0.0);

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
        EvelatorInfo mLastInfo = new EvelatorInfo { };
        /// <summary>
        /// 上次信息
        /// </summary>
        EvelatorInfo mCurInfo = new EvelatorInfo { };


        /// <summary>
        /// 同一楼层自动更新定时器
        /// </summary>
        DispatcherTimer mAutoTimer = new System.Windows.Threading.DispatcherTimer();

        /// <summary>
        /// 数据发送定时器
        /// </summary>
        DispatcherTimer mSendTimer = new System.Windows.Threading.DispatcherTimer();

        /// <summary>
        /// 串口对象
        /// </summary>
        SerialPort mSerialPort = new SerialPort();
        /// <summary>
        /// 用于读取数据的发送命令.
        /// </summary>
        byte[] mSendCmd = new byte[4];

        public MainWindow()
        {
            InitializeComponent();

            mCurInfo.floor = 1;
            mCurInfo.angle = -90.0;
            mLastInfo.floor = 1;
            mLastInfo.angle = -90.0;

            mFloorAngle = 180.0 / (mMaxFloor - 1);
            int autoIterval = 100;
            mAutoAngle = mFloorAngle / mFloorTime * autoIterval;


            //InitSerialPort();

            mAutoTimer.Interval = new TimeSpan(0, 0, 0, 0,autoIterval);
            mAutoTimer.IsEnabled = true;
            mAutoTimer.Tick += new EventHandler(TimerAuto);

            mSendTimer.Interval = new TimeSpan(0, 0, 0, 0, mFloorTime/4);
            mSendTimer.IsEnabled = true;
            mSendTimer.Tick += new EventHandler(TimerSend);
        }

        private void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
        	 // 设置全屏    
// 			this.WindowState = System.Windows.WindowState.Normal;    
// 			this.WindowStyle = System.Windows.WindowStyle.None;    
// 			this.ResizeMode = System.Windows.ResizeMode.NoResize;    
// 			this.Topmost = true;    
// 			
// 			this.Left = 0.0;    
// 			this.Top = 0.0;    
// 			this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;    
// 			this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;    
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            DestroySerialPort();
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
        	// 退出全屏
			
        }
        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            mCurInfo.floor += 1;
            if (mCurInfo.floor > mMaxFloor)
            {
                mCurInfo.floor = 0;
            }
        }
        private void Button_Click_1(object sender, System.Windows.RoutedEventArgs e)
        {
            mCurInfo.floor -= 1;
            if (mCurInfo.floor < 1)
            {
                mCurInfo.floor = 1;
            }
        }

        /// <summary>
        /// 更新显示
        /// </summary>
        /// <param name="info">电梯信息</param>
        private void UpdateEvelator(ref EvelatorInfo info)
        {
             //故障...

            if (info.floor > mMaxFloor)
                info.floor = mMaxFloor;
            if (info.floor < 1)
                info.floor = 1;


            double angle = info.floor * mFloorAngle - 90;

            if (info.floor != mLastInfo.floor)
            {
            }
            else
            {
                //加入动态显示
                mCurInfo.angle += mAutoAngle;
            }

            mRotate.Angle = info.angle;
            mPointer.RenderTransform = mRotate;

            mLastInfo = info;
        }

        void TimerAuto(object sender, EventArgs e)
        {
            UpdateEvelator(ref mCurInfo);
        }

        /// <summary>
        /// 发送定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void TimerSend(object sender, EventArgs e)
        {

        }

       private void InitSerialPort()
        {
            mSerialPort.PortName = "COM1"; //串口号（参考串口调试助手
            mSerialPort.BaudRate = 9600; //波特率
            mSerialPort.Parity = Parity.Even; //校验位
            mSerialPort.DataBits = 8; //数据位
            mSerialPort.StopBits = StopBits.One; //停止位

            if (mSerialPort.IsOpen)
            {
                mSerialPort.Close();
            }
            mSerialPort.Open();
            if (!mSerialPort.IsOpen)
            {
                //写日志
                return;
            }

           // byte[] bytesSend;// = System.Text.Encoding.Default.GetBytes(txtSend.Text);
            mSendCmd[0] = 0xFA;
            mSendCmd[1] = 0xFF;//发送主机地址
            mSendCmd[2] = 0x00;//目标从机地址
            mSendCmd[3] = 0xFE;

        }

        /// <summary>
        /// 关闭通信
        /// </summary>
        private void DestroySerialPort()
        {
            if (mSerialPort.IsOpen)
            {
                mSerialPort.Close();
            }
        }

        /// <summary>
        /// 发送二进制数据
        /// </summary>
        /// <param name="serialPort"></param>
        private void SendBytesData(SerialPort serialPort)
        {
            serialPort.Write(mSendCmd, 0, mSendCmd.Length);
        }

        /// <summary>
        /// 开启接收数据线程
        /// </summary>
        /// <param name="serialPort"></param>
        private void ReceiveThread(SerialPort serialPort)
        {
            Thread threadReceive = new Thread(new ParameterizedThreadStart(SynReceiveData));
            threadReceive.Start(serialPort);
        }

        /// <summary>
        /// 同步阻塞读取
        /// </summary>
        /// <param name="serialPortobj"></param>
        private void SynReceiveData(object serialPortobj)
        {
            SerialPort serialPort = (SerialPort)serialPortobj;
            System.Threading.Thread.Sleep(0);
            serialPort.ReadTimeout = 1000;

            try
            {
                //先记录下来，避免某种原因，人为的原因，操作几次之间时间长，缓存不一致
                int n = serialPort.BytesToRead;
                byte[] buf = new byte[n];//声明一个临时数组存储当前来的串口数据
                //received_count += n;//增加接收计数

                serialPort.Read(buf, 0, n);//读取缓冲数据

                //因为要访问ui资源，所以需要使用invoke方式同步ui
                interfaceUpdateHandle = new HandleInterfaceUpdateDelagate(UpdateEvelator);//实例化委托对象
                Dispatcher.Invoke(interfaceUpdateHandle,null /*new string[] { Encoding.ASCII.GetString(buf) }*/);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }




    }
}
