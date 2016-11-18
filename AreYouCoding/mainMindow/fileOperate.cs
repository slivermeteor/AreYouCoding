//////////////////////////////////////////////////////////////////////////
//             本文件主要声明和定义所有关于文件操作的函数               //
//////////////////////////////////////////////////////////////////////////
using System;
using System.Windows;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;                          // StringBuilder

namespace AreYouCoding
{
    public partial class MainWindow : Window
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

        private bool writeRecordFile(string recordFilePath, string processName, string processId)
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
            addNewItem(processId, null, null, null);

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
                            // 说明当前读到的启动时间没有对应的结束时间 将其放入listview 然后将读到新的启动时间更新
                            addNewItem(count.ToString(), strstartingTime, null, "error ending");

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
                        addNewItem(count.ToString(), strstartingTime, null, "error ending");

                        count++;
                        strstartingTime = strDate.Substring(2);     // 并更新启动时间
                    }
                }

                startTime = DateTime.Parse(strstartingTime);

                if (strendingTime != "")        // 正常处理 有开始和结束时间
                {
                    endTime = DateTime.Parse(strendingTime);
                    tsrunningTime = endTime - startTime;

                    addNewItem(count.ToString(), strstartingTime, strendingTime, tsrunningTime.ToString());

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
                                if (processID == int.Parse(processId.Substring(4)))
                                {
                                    addNewItem(count.ToString(), strstartingTime, null, "still running");
                                    goto NEXT;
                                }
                            }
                        }
                    }

                    addNewItem(count.ToString(), strstartingTime, null, "error ending");

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

        private bool addNewItem(params string[] items)
        {
            DataGridItem newItem = new DataGridItem();
            int num = 0;

            foreach (string item in items)
            {
                switch (num)
                {
                    case 0:
                        {
                            newItem.Identification = item;
                            break;
                        }
                    case 1:
                        {
                            newItem.startingTime = item;
                            break;
                        }
                    case 2:
                        {
                            newItem.endingTime = item;
                            break;
                        }
                    case 3:
                        {
                            newItem.runningTime = item;
                            break;
                        }
                }
                num++;
            }

            DataGridItems.Add(newItem);

            return true;
        }
    }
}