//////////////////////////////////////////////////////////////////////////
//             本文件主要声明和定义所有Situation页面的处理函数          //
//////////////////////////////////////////////////////////////////////////
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;

namespace AreYouCoding
{
    public partial class MainWindow : Window
    {
        // 删除监视按钮 - 将来将集成到工具栏
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

            // 从监视列表中删除
            lock(monitorlist)
            {
                foreach(MonitorNode monitorNode in monitorlist)
                {
                    if (monitorNode.ProcessName.Equals(deleteProcessName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        monitorlist.Remove(monitorNode);
                        break;
                    }
                }
            }

            // 清空list
            DataGridItems.Clear();
            viewSource.Source = DataGridItems;
            runTimeList.DataContext = viewSource;

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

            DataGridItems.Clear();

            foreach (FileInfo file in fileInfo)
            {
                if (string.Equals(file.Name.Substring(0, strProcessName.Length), strProcessName, StringComparison.OrdinalIgnoreCase) &&
                    file.Name.Substring(strProcessName.Length, 1).Equals("-"))
                {
                    readRecordFile(file.DirectoryName + "\\" + file.Name, strProcessName,
                                   "PID:" + file.Name.Substring(strProcessName.Length + 1, file.Name.LastIndexOf('.') - strProcessName.Length - 1));

                    Thread.Sleep(10);  // 文件流 快速读取会出错
                }
            }

            // 更新source 刷新列表
            runTimeList.DataContext = DataGridItems;

            return;
        }

        // DataGrid 菜单条目
        // list菜单处理函数
        private void deleteItem_Click(object sender, RoutedEventArgs e)
        {
            // 拷贝一份listView
            ObservableCollection<DataGridItem> temp = new ObservableCollection<DataGridItem>(DataGridItems);

            int index = runTimeList.SelectedIndex;
            if (index == -1)
            {
                System.Windows.MessageBox.Show("未选中条目");
                return;
            }
            DataGridItems.Clear();
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
            DataGridItems = temp;
            viewSource.Source = DataGridItems;
            runTimeList.DataContext = viewSource;

            return;
        }

        // 自己添加结束或者开始时间
        private void correctItem_Click(object sender, RoutedEventArgs e)
        {
            // 拷贝一份listView
            ObservableCollection<DataGridItem> temp = new ObservableCollection<DataGridItem>(DataGridItems);

            int index = runTimeList.SelectedIndex;
            if (index == -1)
            {
                System.Windows.MessageBox.Show("未选中条目");
                return;
            }

        }
    }
}