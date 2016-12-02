using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.Drawing;           // 项目添加引用
using System.Windows.Forms;     // 项目添加引用
using System.Runtime.InteropServices;
using System.Text;              // StringBuilder

using AreYouCoding.CloseWindow;


namespace AreYouCoding
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>

    public struct MonitorNode
    {
        public string ProcessName;
        public List<int> ProcessIdList;
    }

    public partial class MainWindow : Window
    {
        // 类成员变量定义
        public delegate void delegateRefreshThread();     // 后台进程 委托数据类型
        public delegateRefreshThread refreshThread;                 // 委托实例

        private string recordDirectory;
        private string resourceDirectory;
        private string inifilePath;
        private FileStream recordFileStream;

        private CollectionViewSource viewSource = new CollectionViewSource();                               // listview source
        private ObservableCollection<DataGridItem> DataGridItems = new ObservableCollection<DataGridItem>(); // item数据结构

        private NotifyIcon notifyIcon;
        private List<MonitorNode> monitorlist = new List<MonitorNode>();

        // 窗口启动监视线程 知道监听到 我们才启动UI线程 来修改界面元素  
        public MainWindow()
        {
            this.Visibility = Visibility.Hidden;      // 初始化不可见
            InitializeComponent();

            ulong monitorProcessNumber = 0;
            StringBuilder monitorProcess = new StringBuilder(255);
            MonitorNode monitorNode = new MonitorNode();

            // 初始化INI文件目录
            inifilePath = System.Environment.CurrentDirectory + "\\config.ini";

            // 记录文件夹
            recordDirectory = System.Environment.CurrentDirectory;
            recordDirectory += "\\record";      // 注意最后没有\\

            resourceDirectory = System.Environment.CurrentDirectory;
            resourceDirectory += "\\resource";

            if (!IsDirectoryExists(recordDirectory))        // 记录文件夹不存在 创建文件夹
            {
                Directory.CreateDirectory(recordDirectory);
            }

            // 读取要监视的进程名 --- 读取ini文件 monitor节
            if (IsFileExists(inifilePath))  // 判断监视文件存在吗
            {
                // 读取 number节的值
                monitorProcessNumber = GetPrivateProfileInt("monitor", "number", 0, inifilePath);

                // 遍历 数字节 得到各个监控进程名 不包含extension 
                for (ulong i = 1; i <= monitorProcessNumber; i++)
                {
                    GetPrivateProfileString("monitor", i.ToString(), null, monitorProcess, 255, inifilePath);
                    monitorNode.ProcessName = monitorProcess.ToString();
                    monitorNode.ProcessIdList = new List<int>();
                    monitorNode.ProcessIdList.Clear();

                    lock (monitorlist)       // 锁上monitor
                    {
                        monitorlist.Add(monitorNode);     // 添加到monitorlist 
                    }

                    ThreadPool.QueueUserWorkItem(monitorManagerThread);
                }
            }

            // 设置托盘 
            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "monitor";        // 停留显示
            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(notify_MouseClick);

            // 右键菜单构造和处理函数添加
            System.Windows.Forms.MenuItem menuItem1 = new System.Windows.Forms.MenuItem("new monitor");
            menuItem1.Click += new EventHandler(notifyNewmonitor_Click);
            System.Windows.Forms.MenuItem menuItem2 = new System.Windows.Forms.MenuItem("detail dynamics");
            menuItem2.Click += new EventHandler(notifyDynamics_Click);
            System.Windows.Forms.MenuItem menuItem3 = new System.Windows.Forms.MenuItem("exit");
            menuItem3.Click += new EventHandler(notifyExit_Click);

            // 菜单和托盘绑定
            System.Windows.Forms.MenuItem[] notifyMenu = new System.Windows.Forms.MenuItem[] { menuItem1, menuItem2, menuItem3 };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(notifyMenu);

            // 读取ini文件 config 设置 setting 下各个设置的状态

            // 读取开启启动项的状态
            ulong bStartInBoot = GetPrivateProfileInt("config", "StartInBoot", 0, inifilePath);
            if (bStartInBoot == 0)
            {
                this.IsStartInBoot.IsChecked = false;
            }
            else
            {
                this.IsStartInBoot.IsChecked = true;
            }

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //窗口加载完成 读取记录文件里的监视进程名 添加到Combobox中 

            ComboBoxItem item;

            lock (monitorlist)
            {
                foreach (MonitorNode monitorNode in monitorlist)
                {
                    item = new ComboBoxItem();
                    item.Content = monitorNode.ProcessName;
                    notifyShowBalloomTip("start monitor", "target:" + item.Content.ToString() + ".exe", 0);
                    this.detailProcessName.Items.Add(item);
                }
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CloseWindow(sender, e);
        }

        private void CloseWindow(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 读取ini文件 判断是否显示选择匡
            uint showChoose = GetPrivateProfileInt("config", "showClose", 0, inifilePath);
            if (showChoose == 0 || showChoose == 1)   // 第一次关闭 从来没有选择过 | 选择了显示
            {
                // 启动选择框
                closeWindow close = new closeWindow();
                close.ShowDialog();
                if (close.bCancel == true)
                {
                    e.Cancel = true;
                    return;
                }
            }

            uint CloseIndex = GetPrivateProfileInt("config", "CloseIndex", 0, inifilePath);
            if (CloseIndex == 1)    // 直接关闭程序
            {
                notifyIcon.Dispose();
                // 结束所有线程
                System.Environment.Exit(System.Environment.ExitCode);
            }
            else if (CloseIndex == 2)   // 隐藏窗口
            {
                this.Visibility = Visibility.Hidden;
                e.Cancel = true;
            }

            return;
        }

        // 监视线程
        // 监视线程管理 - 主窗口启动本进程 - 本进程通过扫描系统对应进程PID 和 monitorList里对应进程名的PID 对比 - 决定是否监视这个进程
        public void monitorManagerThread(Object targetObject)
        {
            Process[] scanProcess = Process.GetProcesses();     // using System.Diagnostics

            while(true)
            {
                foreach (Process process in scanProcess)            // 遍历全部进程
                {
                    lock (monitorlist)
                    {
                        foreach (MonitorNode monitorNode in monitorlist)     // 遍历全部节点
                        {
                            if (monitorNode.ProcessName.Equals(process.ProcessName))        // 如果这个进程名我们要监视
                            {
                                foreach (int processId in monitorNode.ProcessIdList)
                                {
                                    if (processId == process.Id)        // 我们已经监视了 这个进程
                                    {
                                        goto NEXT_NODE;
                                    }
                                }
                                // 没有具体监视这个进程 - 启动监视线程，同时添加监视列表
                                ThreadPool.QueueUserWorkItem(monitorThread, process.Id);
                                monitorNode.ProcessIdList.Add(process.Id);

                            }

                            NEXT_NODE:
                            continue;
                        }
                    }
                }

                Thread.Sleep(3000);
                scanProcess = Process.GetProcesses();
            }

            
        }

        public void monitorThread(Object targetObject)        // 后台监视线程 不会阻塞界面
        {
            //[System.Runtime.InteropServices.DllImport("shell32.dll")]
            DateTime processTime;
            int processId = (int)targetObject;
            string strProcessName;
 
            Process[] scanProcess;

            // 首先寻找目标进程 直到找到

            while (true)
            {
                scanProcess = Process.GetProcesses();     // using System.Diagnostics
                foreach (Process process in scanProcess)
                {
                    
                    //if (string.Equals(process.ProcessName, targetThread, StringComparison.OrdinalIgnoreCase))
                    if (process.Id == processId)
                    {
                        /// 扫描到  启动监视进程结束
                        processTime = process.StartTime;
                        strProcessName = process.ProcessName;
                        recordRunTime(strProcessName, processId, processTime.ToString(), 1);

                        // 得到进程ICON 保留下来
                        if (!IsFileExists(resourceDirectory + "\\" + process.ProcessName + ".ico"))
                        {
                            Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName.ToString());
                            FileStream newIcon = new FileStream(resourceDirectory + "\\" + process.ProcessName + ".ico", FileMode.Create);
                            icon.Save(newIcon);
                            newIcon.Close();
                        }
                        
                        //System.Threading.ParameterizedThreadStart ts = new System.Threading.ParameterizedThreadStart(monitorEndThread);        
                        //System.Threading.Thread thread = new System.Threading.Thread(ts);
                        //thread.Start(targetThread);

                        ThreadPool.QueueUserWorkItem(monitorEndThread, process);       // 主窗口启动后台扫描进程

                        return;
                    }
                }

                // 没扫描到 休眠30秒再来
                Thread.Sleep(30000);
            }
        }

        public void monitorEndThread(Object targetObject)
        {
            // 一开始有两种想法 一种是每隔一分钟启动一个监视结束线程 另一种只启动一个监视结束线程 每扫描一次 睡一分钟
            Process[] scanProcess;
            DateTime processTime;
            Process targetProcess = (Process)targetObject;
            string strProcessName = targetProcess.ProcessName;
            int processId = targetProcess.Id;
            
            bool IsScan = false;

            while (true)
            {
                IsScan = false;
                scanProcess = Process.GetProcesses();
                foreach (Process process in scanProcess)
                {
                    if (process.Id == processId)
                    {
                        Thread.Sleep(60000);        /// -> 第一次等待的时间有待完善 最好等待 使下一次开始扫描的时候是每分钟开始的时候
                        IsScan = true;              // 扫描到了目标进程 不记录时间
                        break;
                    }

                }

                // 没扫描到目标进程 记录当前时间 
                if (IsScan == false)
                {
                    processTime = DateTime.Now;

                    // 写入文件 记录这一次开启关闭操作 
                    recordRunTime(strProcessName, processId, processTime.ToString(), 2);
                    // 从monitorlist里删除本进程DI
                    foreach (MonitorNode monitorNode in monitorlist)     // 遍历全部节点
                    {
                        if (monitorNode.ProcessName.Equals(targetProcess.ProcessName))        // 找个进程对应的节点
                        {
                            monitorNode.ProcessIdList.RemoveAt(monitorNode.ProcessIdList.IndexOf(processId));   // 删除对应的 PID Node
                        }
                    }
                       
                    return;
                }
            }
        }

        // 界面元素处理函数
        private void newMonitor_Click(object sender, RoutedEventArgs e)
        {
            newMonitor newWindow = new newMonitor();
            newWindow.ShowDialog();
            MonitorNode monitorNode = new MonitorNode();

            if (newWindow.monitoredProcessName == null)
            {
                return;
            }

            //System.Threading.ParameterizedThreadStart ts = new System.Threading.ParameterizedThreadStart(monitorThread);        // 主窗口启动后台扫描进程
            //System.Threading.Thread thread = new System.Threading.Thread(ts);
            //thread.Start(newWindow.monitoredProcessName);

            //  加入combox选项
            monitorNode.ProcessName = newWindow.monitoredProcessName;
            monitorNode.ProcessIdList = new List<int>();
            monitorNode.ProcessIdList.Clear();
            
            ComboBoxItem item = new ComboBoxItem();
            item.Content = newWindow.monitoredProcessName;
            notifyShowBalloomTip("start monitor", "target:" + item.Content.ToString() + ".exe", 0);
            this.detailProcessName.Items.Add(item);

            // 加入 monitorlist
            lock(monitorlist)
            {
                monitorlist.Add(monitorNode);
            }

            return;
        }

        // 托盘所有函数
        // 托盘点击函数
        private void notify_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {

            if (e.Button == MouseButtons.Left)
            {
                if (this.Visibility == Visibility.Hidden)
                {
                    this.Visibility = Visibility.Visible;
                    this.Activate();
                }
                else
                {
                    this.Visibility = Visibility.Hidden;
                }
            }


        }

        // 托盘菜单 新监控项
        private void notifyNewmonitor_Click(object sender, EventArgs e)
        {
            newMonitor newWindow = new newMonitor();
            newWindow.ShowDialog();
            MonitorNode monitorNode = new MonitorNode();

            if (newWindow.monitoredProcessName == null)
            {
                return;
            }

            //System.Threading.ParameterizedThreadStart ts = new System.Threading.ParameterizedThreadStart(monitorThread);        // 主窗口启动后台扫描进程
            //System.Threading.Thread thread = new System.Threading.Thread(ts);
            //thread.Start(newWindow.monitoredProcessName);                       // 启动监视线程

            // 加入combox选项
            monitorNode.ProcessName = newWindow.monitoredProcessName;
            monitorNode.ProcessIdList = new List<int>();
            monitorNode.ProcessIdList.Clear();
            
            ComboBoxItem item = new ComboBoxItem();
            item.Content = newWindow.monitoredProcessName;
            notifyShowBalloomTip("start monitor", "target:" + item.Content.ToString() + ".exe", 0);
            this.detailProcessName.Items.Add(item);

            // 加入monitorlist
            lock (monitorlist)
            {
                monitorlist.Add(monitorNode);
            }

            return;
        }

        // 点击了详细情况按钮
        private void notifyDynamics_Click(object sender, EventArgs e)
        {

        }
        
        // 退出
        private void notifyExit_Click(object sender, EventArgs e)
        {
            notifyIcon.Dispose();

            System.Environment.Exit(System.Environment.ExitCode);
        }

        // 托盘提示监控项目
        private void notifyShowBalloomTip(string tipTitle, string tipText, int tipTimeout)
        {
            if (tipTimeout == 0)
            {
                notifyIcon.ShowBalloonTip(3000, tipTitle, tipText, ToolTipIcon.Info);
            }
            else
            {
                notifyIcon.ShowBalloonTip(tipTimeout, tipTitle, tipText, ToolTipIcon.Info);
            }

        }

        // 点击 dynamics tabitem 响应函数
        private void Dynamics_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.detailProcessName.Visibility = Visibility.Hidden;
            BitmapImage bm;
            Uri uri;

            lock(monitorlist)
            {
                foreach (MonitorNode monitorNode in monitorlist)
                {
                    if (IsFileExists(resourceDirectory + "\\" + monitorNode.ProcessName + ".ico"))
                    {
                        uri = new Uri(resourceDirectory + "\\" + monitorNode.ProcessName + ".ico");
                        bm = new BitmapImage(uri);

                        this.firstIcon.Source = bm;
                    }

                }
            }
        }

        // 点击Detail 加载图片
        private void Situation_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 显示下拉框
            this.detailProcessName.Visibility = Visibility.Visible;
        }

        private void Setting_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 隐藏combox
            this.detailProcessName.Visibility = Visibility.Hidden;
            // 读取ini文件，加载所有设置
            uint StartInBoot = GetPrivateProfileInt("config", "StartInBoot", 0, inifilePath);
            if (StartInBoot == 2 || StartInBoot == 0)       // 第一次加载 || 不启动
            {
                this.IsStartInBoot.IsChecked = false;
            }
            else
            {
                this.IsStartInBoot.IsChecked = true;
            }

            uint closeIndex = GetPrivateProfileInt("config", "CloseIndex", 0, inifilePath);
            if (closeIndex == 0 || closeIndex == 2)   // 还未设置节点  || 最小化
            {
                this.miniRadio.IsChecked = true;
                this.exitRadio.IsChecked = false;
            }
            else
            {
                this.miniRadio.IsChecked = false;
                this.exitRadio.IsChecked = true;
            }

        }

        
    }

    // 扩展资源类 -  set 方法需要添加安全性检查
    public class DataGridItem
    {
        public string Identification { get; set; }
        public string startingTime { get; set; }
        public string endingTime { get; set; }
        public string runningTime { get; set; }
    }

    // monitorlist 旧泛型类型 - 已经被弃用 - 使用 C#官方泛型 List<>
    public class monitorListNode<T>
    {
        public monitorListNode(T Value)
        {
            this.Value = Value;
        }

        public T Value
        {
            get;
            private set;
        }

        public monitorListNode<T> Next
        {
            get;
            internal set;
        }

        public monitorListNode<T> Prev
        {
            get;
            internal set;
        }
    }

    public class monitorList<T>
    {
        public monitorListNode<T> First
        {
            get;

            private set;
        }

        public monitorListNode<T> Last
        {
            get;

            private set;
        }

        public void addNewNode(T value)
        {
            var newNode = new monitorListNode<T>(value);

            if (First == null)
            {
                First = newNode;
                newNode.Prev = Last;
                Last = First;
            }
            else
            {
                Last.Next = newNode;
                newNode.Prev = Last;
                Last = newNode;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            monitorListNode<T> travel = First;

            while (travel != null)
            {
                yield return (T)travel.Value;

                travel = travel.Next;
            }
        }
    }
}

