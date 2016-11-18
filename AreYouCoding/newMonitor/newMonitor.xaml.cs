using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace AreYouCoding
{
    /// <summary>
    /// newMonitor.xaml 的交互逻辑
    /// </summary>
    public partial class newMonitor : Window
    {
        // ini文件API导入
        #region
        [DllImport("Kernel32")]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("Kernel32")]
        private static extern long GetPrivateProfileString(string section, string key, string defaultString, StringBuilder returnString, int size, string filePath);

        [DllImport("Kernel32")]
        private static extern ulong GetPrivateProfileInt(string section, string key, int defaultValue, string filePath);
        #endregion

        public string monitoredProcessName;
        public newMonitor()
        {
            InitializeComponent();
        }

        private void monitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (processName.Text == "")             // 没输入进程名
            {
                MessageBox.Show("please input the process name");
                return;
            }

            monitoredProcessName = processName.Text;
            string inifilePath = System.Environment.CurrentDirectory + "\\config.ini";
            ulong monitorProcessNumber = 0;
            StringBuilder monitorProcess = new StringBuilder(255);

            // 检查是否重复
            if (IsFileExists(inifilePath))
            {
                monitorProcessNumber = GetPrivateProfileInt("monitor", "number", 0, inifilePath);   // 读取监视个数

                for (ulong i = 1; i <= monitorProcessNumber; i++)        // 遍历所有监视进程名
                {
                    GetPrivateProfileString("monitor", i.ToString(), "", monitorProcess, 255, inifilePath);
                    if (monitorProcess.Equals(monitorProcess.ToString()))      // 如果已经监视
                    {
                        System.Windows.MessageBox.Show("This process has been monitored");
                        return;
                    }
                }
            }
            else
            {
                FileStream fs = File.Create(inifilePath);
                fs.Close();
            }

            // 没有监视 / 没有ini文件
            // 首先修改监视数量
            monitorProcessNumber += 1;
            if (!WritePrivateProfileString("monitor", "number", monitorProcessNumber.ToString(), inifilePath))
            {
                System.Windows.MessageBox.Show("revise process number failed");
                return;
            }

            // 写入进程名
            if (!WritePrivateProfileString("monitor", monitorProcessNumber.ToString(), monitoredProcessName, inifilePath))
            {
                System.Windows.MessageBox.Show("write new process name failed");
                return;
            }

            this.Close();
            return;
        }

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

    }
}
