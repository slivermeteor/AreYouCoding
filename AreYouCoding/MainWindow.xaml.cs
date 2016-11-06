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
using System.Security.Principal;


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
        public delegate void delegateRefreshThread();     // 后台进程 委托数据类型
        public delegateRefreshThread refreshThread;                 // 委托实例

        private string recordDirectory;
        private string resourceDirectory;
        private string inifilePath;
        private FileStream recordFileStream;

        private CollectionViewSource viewSource = new CollectionViewSource();                               // listview source
        private ObservableCollection<listviewItem> listviewItem = new ObservableCollection<listviewItem>(); // item数据结构

        private NotifyIcon notifyIcon;
        private monitorList<MonitorNode> monitorlist = new monitorList<MonitorNode>();

        // ini文件API导入
        #region
        [DllImport("Kernel32")]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("Kernel32")]
        private static extern long GetPrivateProfileString(string section, string key, string defaultString, StringBuilder returnString, int size, string filePath);

        [DllImport("Kernel32")]
        private static extern ulong GetPrivateProfileInt(string section, string key, int defaultValue, string filePath);
        #endregion

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

        // 监视线程管理 - 主窗口启动本进程 - 本进程通过扫描系统对应进程PID 和 monitorList里对应进程名的PID 对比 - 决定是否监视这个进程
        public void monitorManagerThread(Object targetObject)
        {
            Process[] scanProcess = Process.GetProcesses();     // using System.Diagnostics

            foreach (Process process in scanProcess)            // 遍历全部进程
            {
                lock(monitorlist)
                {
                    foreach (MonitorNode monitorNode in monitorlist)     // 遍历全部节点
                    {
                        if (monitorNode.ProcessName.Equals(process.ProcessName))        // 如果这个进程我们要监视
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
        }



        // 窗口启动监视线程 知道监听到 我们才启动UI线程 来修改界面元素  
        public MainWindow()
        {
            //this.Visibility = Visibility.Hidden;      // 初始化不可见
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

                    lock(monitorlist)       // 锁上monitor
                    {
                        monitorlist.addNewNode(monitorNode);     // 添加到monitorlist 
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

            lock(monitorlist)
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
            notifyIcon.Dispose();

            // 结束所有线程
            System.Environment.Exit(System.Environment.ExitCode);
        }

        public bool recordRunTime(string strProcessName, int processId, string timeString, int type)
        {
            byte[] timedata;
            string transferString;
            string recordFileName;

            recordFileName = recordDirectory + '\\' + strProcessName + "-" + processId.ToString() + ".txt";



            // 判断记录文件是否存在
            if (!IsFileExists(recordFileName))
            {
                recordFileStream = File.Create(recordFileName);
            }
            else
            {
                recordFileStream = File.Open(recordFileName, FileMode.Open, FileAccess.ReadWrite);
            }


            // 写入记录信息
            transferString = type.ToString() + " " + timeString + '\n';
            timedata = System.Text.Encoding.Default.GetBytes(transferString);
            recordFileStream.Seek(0, SeekOrigin.End);
            recordFileStream.Write(timedata, 0, timedata.Length);

            recordFileStream.Flush();
            recordFileStream.Close();

            return true;
        }

        // 文件操作函数
        public bool IsFileExists(string filename)
        {
            if (filename == null || filename == "")
            {
                return false;
            }

            if (File.Exists(filename))
            {
                return true;
            }

            return false;
        }

        public bool IsDirectoryExists(string directoryName)
        {
            if (directoryName == null || directoryName == "")
            {
                return false;
            }

            if (Directory.Exists(directoryName))
            {
                return true;
            }

            return false;
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
                monitorlist.addNewNode(monitorNode);
            }

            return;
        }

        // Combox 选中修改函数
        private void detailProcessName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string strProcessName;
            // 选中一个进程名 加载所有记录信息
            ComboBoxItem item = detailProcessName.SelectedItem as ComboBoxItem;
            if (item == null)       // 我们删除combobox项也会触发SeletedChanged
            {
                return;
            }
            strProcessName = item.Content.ToString();

            DirectoryInfo directoryInfo = new DirectoryInfo(System.Environment.CurrentDirectory + "\\record");
            FileInfo[] fileInfo = directoryInfo.GetFiles();

            listviewItem.Clear();

            foreach (FileInfo file in fileInfo)
            {
                if (string.Equals(file.Name.Substring(0, strProcessName.Length), strProcessName, StringComparison.OrdinalIgnoreCase) && 
                    file.Name.Substring(strProcessName.Length, 1).Equals("-"))
                {
                    readRecordFile(file.DirectoryName + "\\" + file.Name, strProcessName,
                                   "PID:" + file.Name.Substring(strProcessName.Length + 1, file.Name.LastIndexOf('.') - strProcessName.Length - 1));

                    Thread.Sleep(10);  // 文件流 快速读取好像会出错
                }
            }

            // 更新source 刷新列表
            viewSource.Source = listviewItem;
            this.listView.DataContext = viewSource;

            return;
        }

        private bool readRecordFile(string recordFilePath, string processName, string proessId)
        {
            string strDate;
            string strstartingTime = "";
            string strendingTime = "";
            string strTemp;
            TimeSpan tsrunningTime;
            DateTime startTime;
            DateTime endTime;

            FileStream fs;
            StreamReader streamReader;
            StreamWriter streamWriter;
            List<string> txtline;

            byte[] Byte;

            int count = 1;
            int linecount = -1;

            
            if (IsFileExists(recordFilePath))
            {
                txtline = new List<String>(File.ReadAllLines(recordFilePath));               // 声明泛型 读取所有数据

                // 打开文件 初始化读取流
                fs = File.Open(recordFilePath, FileMode.Open, FileAccess.ReadWrite);
                streamReader = new StreamReader(fs);
            }
            else
            {
                System.Windows.MessageBox.Show("record file don't exist", "Error");
                return false;
            }

            // 得到这个进程ID 单独插入一行 来区分
            listviewItem.Add(new listviewItem()
            {
                count = proessId,
                startingTime = null,
                endingTime = null,
                runningTime = null
            });

            while (!streamReader.EndOfStream)
            {
                // 清空两个时间 
                strendingTime = "";
                strendingTime = "";

                linecount++;
                strDate = streamReader.ReadLine();

                if (strDate[0] == '1')      // 读取开始时间
                {
                    strstartingTime = strDate.Substring(2);
                }
                else
                {
                    System.Windows.MessageBox.Show("Have read illegal data");
                    continue;
                }

                linecount++;
                if (!streamReader.EndOfStream)          // ??
                {
                    strDate = streamReader.ReadLine();
                }

                if (strDate[0] == '2')
                {
                    strendingTime = strDate.Substring(2);
                }
                else
                {
                    while (strDate[0] == '1' && !streamReader.EndOfStream)   // 异常处理 1.重复记录启动时间 - 原因:在监视进程的时候退出又启动
                    {
                        strTemp = strDate.Substring(2); // 读取重复记录的时间

                        if (string.Equals(strTemp, strstartingTime))        // 重复记录的情况 删除这个重复记录的情况 并去读取下一行
                        {
                            txtline.RemoveAt(linecount - 1);        // 删除这一行后 行数发生了变化 我们在这里进行下一次读取的时候 不用对行号进行加加
                            linecount--;                            // 回退 当前的读取行数
                        }
                        else        // 不匹配 - 在监视程序结束的时间里 对方关闭 这种异常 给用户自己添加结束时间的权利
                        {
                            listviewItem.Add(new listviewItem()         // 说明当前读到的启动时间没有对应的结束时间 将其放入listview 然后将读到新的启动时间更新
                            {
                                count = count.ToString(),
                                startingTime = strstartingTime,
                                endingTime = null,
                                runningTime = "error ending"
                            });

                            count++;
                            strstartingTime = strTemp;      // 更新启动时间
                        }

                        linecount++;
                        strDate = streamReader.ReadLine();
                    }

                    // 退出循环 说明读到了结束时间 / 超出了文件读取范围
                    if (strDate[0] == '2')      // 如果是读取到结束时间 更新结束时间
                    {
                        strendingTime = strDate.Substring(2);
                    }
                    else if (String.Equals(strDate.Substring(2), strstartingTime))      // 如果是还读取到了重复的情况
                    {
                        if (txtline.Count > 1)
                        {
                            txtline.RemoveAt(linecount);
                            linecount--;
                        }
                    }
                    else  // 没有得到到结束时间 但是是两个不同的开始时间说明前一个是异常时间 后一个仍在监视
                    {
                        // 把前一个当异常处理掉
                        listviewItem.Add(new listviewItem()
                        {
                            count = count.ToString(),
                            startingTime = strstartingTime,
                            endingTime = null,
                            runningTime = "error ending"
                        });

                        count++;
                        strstartingTime = strDate.Substring(2);     // 并更新启动时间
                    }
                }

                startTime = DateTime.Parse(strstartingTime);

                if (strendingTime != "")        // 正常处理 有开始和结束时间
                {
                    endTime = DateTime.Parse(strendingTime);
                    tsrunningTime = endTime - startTime;

                    listviewItem.Add(new listviewItem()
                    {
                        count = count.ToString(),
                        startingTime = strstartingTime,
                        endingTime = strendingTime,
                        runningTime = tsrunningTime.ToString()
                    });

                    count++;
                }
                else   // 异常处理 没有读取到结束时间 - 那么是已经异常退出 / 还在运行
                {

                    foreach (MonitorNode monitorNode in monitorlist)     // 遍历全部节点
                    {
                        if (monitorNode.ProcessName.Equals(processName))        // 是我们正在监视的进程
                        {
                            foreach (int processID in monitorNode.ProcessIdList)    // 这个PID是否还在监视 ?
                            {
                                if (processID == int.Parse(proessId.Substring(4)))
                                {
                                    listviewItem.Add(new listviewItem()
                                    {
                                        count = count.ToString(),
                                        startingTime = strstartingTime,
                                        endingTime = null,
                                        runningTime = "still running"
                                    });
                                    goto NEXT;
                                }
                            }
                        }
                    }

                    listviewItem.Add(new listviewItem()
                    {
                        count = count.ToString(),
                        startingTime = strstartingTime,
                        endingTime = null,
                        runningTime = "error ending"
                    });

                    NEXT:
                    count++;
                }

            }

            fs.Close();     // 关闭原文件

            // 创建新文件 覆盖旧文件
            fs = File.Open(recordFilePath, FileMode.Create, FileAccess.ReadWrite);
            streamWriter = new StreamWriter(fs);

            // 将整理完毕的新缓存 放回文件
            foreach (string strline in txtline)
            {
                strTemp = strline + '\n';
                Byte = System.Text.Encoding.Default.GetBytes(strTemp);
                fs.Write(Byte, 0, Byte.Length);
            }

            // 关闭新文件
            fs.Close();

            return true;
        }

        // 托盘处理函数
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
                monitorlist.addNewNode(monitorNode);
            }

            return;
        }

        // 点击了详细情况按钮
        private void notifyDynamics_Click(object sender, EventArgs e)
        {

        }

        // list菜单处理函数
        private void deleteItem_Click(object sender, RoutedEventArgs e)
        {
            // 拷贝一份listView
            ObservableCollection<listviewItem> temp = new ObservableCollection<listviewItem>(listviewItem);

            int index = listView.SelectedIndex;
            if (index == -1)
            {
                System.Windows.MessageBox.Show("未选中条目");
                return;
            }
            listviewItem.Clear();
            temp.RemoveAt(index);

            // 修改文件
            List<string> txtLine;
            FileStream fs;
            StreamWriter streamWriter;
            String strTemp;
            byte[] Byte;

            // TXT文件操作 要全部注意编码问题 标识UTF-8
            ComboBoxItem item = detailProcessName.SelectedItem as ComboBoxItem;
            if (item == null)       // 我们删除combobox项也会触发SeletedChanged
            {
                return;
            }
            string strRecordFileName = recordDirectory + "\\" + item.Content.ToString() + ".txt";

            if (IsFileExists(strRecordFileName))
            {
                txtLine = new List<String>(File.ReadAllLines(strRecordFileName, Encoding.UTF8));    // 将所有数据读出来 为修改准备

                txtLine.RemoveAt(index);        // 移除一行

                fs = File.Open(strRecordFileName, FileMode.Create, FileAccess.ReadWrite);       //  创建同名文件 覆盖原文件
                streamWriter = new StreamWriter(fs, Encoding.UTF8);

                foreach (string strline in txtLine)         //  一行行写入文件中
                {
                    strTemp = strline + '\n';               // 每行末尾的 \n
                    Byte = System.Text.Encoding.UTF8.GetBytes(strTemp);
                    fs.Write(Byte, 0, Byte.Length);
                }

                fs.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("无法修改文件");
            }

            // 将temp送回ListviewItem 然后更新列表
            listviewItem = temp;
            viewSource.Source = listviewItem;
            this.listView.DataContext = viewSource;

            return;
        }

        // 自己添加结束或者开始时间
        private void correctItem_Click(object sender, RoutedEventArgs e)
        {
            // 拷贝一份listView
            ObservableCollection<listviewItem> temp = new ObservableCollection<listviewItem>(listviewItem);

            int index = listView.SelectedIndex;
            if (index == -1)
            {
                System.Windows.MessageBox.Show("未选中条目");
                return;
            }
            
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
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Environment.CurrentDirectory + "\\resource\\Trash.png", UriKind.RelativeOrAbsolute);
            bitmap.EndInit();

            deleteImage.Source = bitmap;
        }

        // CheckBox 选中和不选中 设置开机启动和关闭
        private void IsStartInBoot_Checked(object sender, RoutedEventArgs e)
        {
            string strfileName = Process.GetCurrentProcess().MainModule.FileName;
            int iEnd = strfileName.LastIndexOf('.');
            if (RunInBoot(strfileName.Substring(0, iEnd), System.Windows.Forms.Application.ExecutablePath, true))
            {
                // 函数成功 设置ini文件
                WritePrivateProfileString("config", "StartInBoot", "1", inifilePath);
            }

        }

        private void IsStartInBoot_Unchecked(object sender, RoutedEventArgs e)
        {
            string strfileName = Process.GetCurrentProcess().MainModule.FileName;
            int iEnd = strfileName.LastIndexOf('.');
            if (RunInBoot(strfileName.Substring(0, iEnd), System.Windows.Forms.Application.ExecutablePath, false))
            {
                WritePrivateProfileString("config", "StartInBoot", "0", inifilePath);
            }
        }

        private bool RunInBoot(string fileName, string startPath, bool bIsStart)
        {
            // 读取注册表键值      写这个键值 不需要UAC!!!
            RegistryKey HKLM = Registry.CurrentUser;
            RegistryKey Run = HKLM.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

            if (bIsStart)
            {
                try
                {
                    Run.SetValue(fileName, startPath);
                    HKLM.Close();
                }
                catch (Exception error)
                {
                    System.Windows.MessageBox.Show(error.Message.ToString());
                    return false;
                }
            }
            else
            {
                try
                {
                    Run.DeleteValue(fileName);
                    HKLM.Close();
                }
                catch (Exception error)
                {
                    System.Windows.MessageBox.Show(error.Message.ToString());
                    return false;
                }
            }


            return true;
        }

        // 删除监视
        private void deleteMonitor_Click(object sender, RoutedEventArgs e)
        {
            // 移除combox选项
            string deleteProcessName = (string)((ComboBoxItem)detailProcessName.SelectedItem).Content;      // 保存string
            detailProcessName.Items.RemoveAt(detailProcessName.SelectedIndex);

            // 修改ini文件
            ulong processMonitorNumber = 0;
            processMonitorNumber = GetPrivateProfileInt("monitor", "number", 0, inifilePath);
            StringBuilder strProcessName = new StringBuilder(255);
            bool bIsMove = false;
            // 删除 ini中的监视条目 --- 不能是简单的删除 不然下次遍历的时候 就会少遍历 中间缺失了一项
            // 所以我们在遍历的时候 在删除指定条目后 要将后面的向前移动 不能出现key的缺失情况
            for (ulong i = 1; i <= processMonitorNumber; i++)  // ! <=
            {
                GetPrivateProfileString("monitor", i.ToString(), "", strProcessName, 255, inifilePath);

                if (bIsMove)    //删除后面的键值往前移动
                {
                    // 首先得到自己的KEY值
                    GetPrivateProfileString("monitor", i.ToString(), "", strProcessName, 255, inifilePath);
                    // 写入到上一个key
                    WritePrivateProfileString("monitor", (i - 1).ToString(), strProcessName.ToString(), inifilePath);
                    continue;
                }

                if (strProcessName.Equals(deleteProcessName))       // 找到要删除的那个key
                {
                    bIsMove = true;     // 告诉后面的key值往前移动 通过覆盖来删除
                }
            }
            // 删除最后一个key
            WritePrivateProfileString("monitor", processMonitorNumber.ToString(), null, inifilePath);

            // 修改监视个数
            processMonitorNumber--;

            if (!WritePrivateProfileString("monitor", "number", processMonitorNumber.ToString(), inifilePath))
            {
                System.Windows.MessageBox.Show("revise monitor number failed");
            }

            // 清空list
            listviewItem.Clear();
            viewSource.Source = listviewItem;
            this.listView.DataContext = viewSource;

        }




    }

    public class listviewItem
    {
        public string count { get; set; }
        public string startingTime { get; set; }
        public string endingTime { get; set; }
        public string runningTime { get; set; }
    }

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

