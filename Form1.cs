using System;
using System.Drawing;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;
using QRCoder;
using System.Runtime.InteropServices;
using SuperSocket.WebSocket;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase;
using System.Net.NetworkInformation;



namespace CopySync
{
    public partial class Form1 : Form
    {

        private WebSocketServer websocketServer;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_DRAWCLIPBOARD = 0x308;
        private IntPtr _nextClipboardViewer;

        public Form1()
        {
            InitializeComponent();
            _nextClipboardViewer = (IntPtr)SetClipboardViewer(this.Handle);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Console.WriteLine("qidong!");
            // 配置WebSocket服务器
            var serverConfig = new ServerConfig
            {
                Port = 10000,
                Ip = "Any",
                MaxConnectionNumber = 100,
                Mode = SocketMode.Tcp
            };
            // 创建WebSocket服务器实例
            websocketServer = new WebSocketServer();
            // 初始化服务器
            if (!websocketServer.Setup(serverConfig))
            {
                MessageBox.Show("WebSocket服务器初始化失败！");
                return;
            }
            // 为服务器设置连接事件处理程序
            websocketServer.NewSessionConnected += WebSocketServer_NewSessionConnected;
            websocketServer.SessionClosed += WebSocketServer_SessionClosed;
            websocketServer.NewMessageReceived += WebsocketServer_NewMessageReceived;
            // 启动服务器
            if (!websocketServer.Start())
            {
                MessageBox.Show("WebSocket服务器启动失败！");
            }
            else
            {
                //MessageBox.Show("WebSocket服务器启动成功！");
                label1.Text = "服务器已启动！";
            }

            // 生成WebSocket服务器的URL
            string wsUrl = "ws://" + GetLocalIPAddress() + ":10000/clipboard";

            // 创建二维码
            CreateQRCode(wsUrl);
        }

        private void WebsocketServer_NewMessageReceived(WebSocketSession session, string value)
        {
            this.Invoke((MethodInvoker)delegate {
                Clipboard.SetText(value);
            });
        }

        private void WebSocketServer_NewSessionConnected(WebSocketSession session)
        {
            // 当有客户端连接时，打印日志
            // MessageBox.Show("新客户端已连接！");
            label2.Invoke((MethodInvoker)delegate {
                label2.Text = "手机已连接！";
            });
        }
        private void WebSocketServer_SessionClosed(WebSocketSession session, SuperSocket.SocketBase.CloseReason value)
        {
            // 当客户端断开连接时，打印日志
            // MessageBox.Show("客户端已断开连接！");
            label2.Invoke((MethodInvoker)delegate {
                label2.Text = "手机已断开！";
            });
        }


        // 获取本机局域网IP地址
        private string GetLocalIPAddress()
        {
            // 获取所有网络接口
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                // 检查接口是否是WLAN，并且处于开机状态
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && adapter.OperationalStatus == OperationalStatus.Up)
                {
                    // 获取IPv4地址信息
                    IPInterfaceProperties ipProps = adapter.GetIPProperties();
                    UnicastIPAddressInformationCollection ipAddresses = ipProps.UnicastAddresses;
                    foreach (UnicastIPAddressInformation ipAddress in ipAddresses)
                    {
                        // 确保是IPv4地址
                        if (ipAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            return ipAddress.Address.ToString();
                        }
                    }
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        private void CreateQRCode(string text)
        {
            // 创建二维码
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCode(qrCodeData))
            using (var qrCodeImage = qrCode.GetGraphic(20))
            {
                // 将二维码显示在PictureBox控件中
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox1.Image = new Bitmap(qrCodeImage);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 停止WebSocket服务器
            websocketServer.Stop();

            // 可以在这里添加其他清理代码，例如保存状态、释放资源等
            Application.Exit();
        }


        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                    OnClipboardChanged();
                    SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    break;
                case 0x2 /* WM_DESTROY */:
                    ChangeClipboardChain(this.Handle, _nextClipboardViewer);
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }
        private void OnClipboardChanged()
        {
            // 尝试读取剪切板内容
            if (Clipboard.ContainsText())
            {
                string clipboardText = Clipboard.GetText();
                Console.WriteLine("Clipboard content changed: " + clipboardText);
                // 在这里处理剪切板内容
                if (websocketServer != null)
                {
                    foreach (var session in websocketServer.GetAllSessions())
                    {
                        session.Send(clipboardText);
                    }
                }
            }
        }

    }

}
