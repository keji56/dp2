﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DigitalPlatform;
using DigitalPlatform.IO;
using DigitalPlatform.LibraryClient;
using DigitalPlatform.Text;

namespace dp2SSL
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        // 主要的通道池，用于当前服务器
        public LibraryChannelPool _channelPool = new LibraryChannelPool();

        protected override void OnStartup(StartupEventArgs e)
        {
            if (DetectVirus.Detect360() || DetectVirus.DetectGuanjia())
            {
                MessageBox.Show("dp2SSL 被木马软件干扰，无法启动");
                System.Windows.Application.Current.Shutdown();
                return;
            }

            WpfClientInfo.TypeOfProgram = typeof(App);
            if (StringUtil.IsDevelopMode() == false)
                WpfClientInfo.PrepareCatchException();

            WpfClientInfo.Initial("dp2SSL");
            base.OnStartup(e);

            this._channelPool.BeforeLogin += new DigitalPlatform.LibraryClient.BeforeLoginEventHandle(Channel_BeforeLogin);
            this._channelPool.AfterLogin += new AfterLoginEventHandle(Channel_AfterLogin);

            // 后台自动检查更新
            Task.Run(() =>
            {
                NormalResult result = WpfClientInfo.InstallUpdateSync();
                if (result.Value == -1)
                    OutputHistory("自动更新出错: " + result.ErrorInfo, 2);
                else if (result.Value == 1)
                    OutputHistory(result.ErrorInfo, 1);
                else if (string.IsNullOrEmpty(result.ErrorInfo) == false)
                    OutputHistory(result.ErrorInfo, 0);
            });
        }

        // TODO: 如何显示后台任务执行信息? 可以考虑只让管理者看到
        public void OutputHistory(string strText, int nWarningLevel = 0)
        {
            // OutputText(DateTime.Now.ToShortTimeString() + " " + strText, nWarningLevel);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            this._channelPool.BeforeLogin -= new DigitalPlatform.LibraryClient.BeforeLoginEventHandle(Channel_BeforeLogin);
            this._channelPool.AfterLogin -= new AfterLoginEventHandle(Channel_AfterLogin);
            this._channelPool.Close();

            WpfClientInfo.Finish();
            base.OnExit(e);
        }

        public static App CurrentApp
        {
            get
            {
                return ((App)Application.Current);
            }
        }

        public void ClearChannelPool()
        {
            this._channelPool.Clear();
        }

        public static string dp2ServerUrl
        {
            get
            {
                return WpfClientInfo.Config.Get("global", "dp2ServerUrl", "");
            }
        }

        public static string dp2UserName
        {
            get
            {
                return WpfClientInfo.Config.Get("global", "dp2UserName", "");
            }
        }

        public static string RfidUrl
        {
            get
            {
                return WpfClientInfo.Config.Get("global", "rfidUrl", "");
            }
        }

        public static string FingerprintUrl
        {
            get
            {
                return WpfClientInfo.Config.Get("global", "fingerprintUrl", "");
            }
        }

        public static string dp2Password
        {
            get
            {
                return DecryptPasssword(WpfClientInfo.Config.Get("global", "dp2Password", ""));
            }
        }

#if NO
        // 用于锁屏的密码
        public string LockingPassword
        {
            get
            {
                return DecryptPasssword(WpfClientInfo.Config.Get("global", "lockingPassword", ""));
            }
            set
            {
                WpfClientInfo.Config.Set("global", "lockingPassword", EncryptPassword(value));
            }
        }
#endif

        public static void SetLockingPassword(string password)
        {
            string strSha1 = Cryptography.GetSHA1(password + "_ok");
            WpfClientInfo.Config.Set("global", "lockingPassword", strSha1);
        }

        public static bool MatchLockingPassword(string password)
        {
            string sha1 = WpfClientInfo.Config.Get("global", "lockingPassword", "");
            string current_sha1 = Cryptography.GetSHA1(password + "_ok");
            if (sha1 == current_sha1)
                return true;
            return false;
        }

        public static bool IsLockingPasswordEmpty()
        {
            string sha1 = WpfClientInfo.Config.Get("global", "lockingPassword", "");
            return (string.IsNullOrEmpty(sha1));
        }

        static string EncryptKey = "dp2ssl_client_password_key";

        public static string DecryptPasssword(string strEncryptedText)
        {
            if (String.IsNullOrEmpty(strEncryptedText) == false)
            {
                try
                {
                    string strPassword = Cryptography.Decrypt(
        strEncryptedText,
        EncryptKey);
                    return strPassword;
                }
                catch
                {
                    return "errorpassword";
                }
            }

            return "";
        }

        public static string EncryptPassword(string strPlainText)
        {
            return Cryptography.Encrypt(strPlainText, EncryptKey);
        }

#region LibraryChannel

        internal void Channel_BeforeLogin(object sender,
DigitalPlatform.LibraryClient.BeforeLoginEventArgs e)
        {
            if (e.FirstTry == true)
            {
                {
                    e.UserName = dp2UserName;

                    // e.Password = this.DecryptPasssword(e.Password);
                    e.Password = dp2Password;

#if NO
                    strPhoneNumber = AppInfo.GetString(
        "default_account",
        "phoneNumber",
        "");
#endif

                    bool bIsReader = false;

                    string strLocation = "";

                    e.Parameters = "location=" + strLocation;
                    if (bIsReader == true)
                        e.Parameters += ",type=reader";
                }

                e.Parameters += ",client=dp2ssl|" + WpfClientInfo.ClientVersion;

                if (String.IsNullOrEmpty(e.UserName) == false)
                    return; // 立即返回, 以便作第一次 不出现 对话框的自动登录
                else
                {
                    e.ErrorInfo = "尚未配置 dp2library 服务器用户名";
                    e.Cancel = true;
                }
            }

            // e.ErrorInfo = "尚未配置 dp2library 服务器用户名";
            e.Cancel = true;
        }

        string _currentUserName = "";

        public string ServerUID = "";

        internal void Channel_AfterLogin(object sender, AfterLoginEventArgs e)
        {
            LibraryChannel channel = sender as LibraryChannel;
            _currentUserName = channel.UserName;
            //_currentUserRights = channel.Rights;
            //_currentLibraryCodeList = channel.LibraryCodeList;
        }

        List<LibraryChannel> _channelList = new List<LibraryChannel>();

        public void AbortAllChannel()
        {
            foreach (LibraryChannel channel in _channelList)
            {
                if (channel != null)
                    channel.Abort();
            }
        }

        // parameters:
        //      style    风格。如果为 GUI，表示会自动添加 Idle 事件，并在其中执行 Application.DoEvents
        public LibraryChannel GetChannel()
        {
            string strServerUrl = dp2ServerUrl;

            string strUserName = dp2UserName;

            LibraryChannel channel = this._channelPool.GetChannel(strServerUrl, strUserName);
            _channelList.Add(channel);
            // TODO: 检查数组是否溢出
            return channel;
        }

        public void ReturnChannel(LibraryChannel channel)
        {
            this._channelPool.ReturnChannel(channel);
            _channelList.Remove(channel);
        }

#endregion
    }
}
