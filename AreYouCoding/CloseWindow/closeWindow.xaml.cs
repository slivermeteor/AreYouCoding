using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace AreYouCoding.CloseWindow
{
    /// <summary>
    /// closeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class closeWindow : Window
    {

        #region
        [DllImport("Kernel32")]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);
        #endregion

        private string inifilePath;
        private bool bConfirm;
        public bool bCancel;

        public closeWindow()
        {
            InitializeComponent();
            inifilePath = System.Environment.CurrentDirectory + "\\config.ini";
            this.Minimize.IsChecked = true;
            this.Exit.IsChecked = false;
            bCancel = false;
            bConfirm = false;
        }

        private void Exit_Checked(object sender, RoutedEventArgs e)
        {
            this.Minimize.IsChecked = false;
        }

        private void Minimize_Checked(object sender, RoutedEventArgs e)
        {
            this.Exit.IsChecked = false;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {         
            SetClose();
            bConfirm = true;        // 告诉Closing函数 是从Confirm里关闭的
            Close();
            return;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (bConfirm == false)  // 不是从confrim里关闭的窗口 说明我们要取消关闭操作
            {
                bCancel = true;
            }
            
            return;
        }

        private void SetClose()
        {
            // 检查CheckBox
            // 下次还显示吗？
            if (this.bAskAgain.IsChecked == false)
            {
                WritePrivateProfileString("config", "showClose", "1", inifilePath);
            }
            else
            {
                WritePrivateProfileString("config", "showClose", "2", inifilePath);
            }

            // 选择的关闭方法
            if (this.Exit.IsChecked == true && this.Minimize.IsChecked == false)
            {
                WritePrivateProfileString("config", "CloseIndex", "1", inifilePath);
            }
            else if (this.Exit.IsChecked == false && this.Minimize.IsChecked == true)
            {
                WritePrivateProfileString("config", "CloseIndex", "2", inifilePath);
            }
            else
            {
                MessageBox.Show("CheckBox's value Error.Will use default settting.You can go to setting to change it.", "Warning");
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {          
            if (this.Exit.IsChecked == true)    // 已经checked 无效点击
            {
                return;
            }
            else
            {
                this.Exit.IsChecked = true;
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (this.Minimize.IsChecked == true)    // 已经checked 无效点击
            {
                return;
            }
            else
            {
                this.Minimize.IsChecked = true;
            }
        }
    }
}
