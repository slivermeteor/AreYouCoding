//////////////////////////////////////////////////////////////////////////
//             本文件主要声明和定义所有SETTTINGS页面的处理函数          //
//////////////////////////////////////////////////////////////////////////
using System;
using System.Windows;
using System.Diagnostics;
using Microsoft.Win32;


namespace AreYouCoding
{
    public partial class MainWindow : Window
    {
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

    }
}