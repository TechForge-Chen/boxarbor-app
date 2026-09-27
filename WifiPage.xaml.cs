using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace BluetoothMauiApp
{
    public partial class WifiPage : ContentPage
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;

        public WifiPage()
        {
            InitializeComponent();
        }

        private async void OnConnectButtonClicked(object sender, EventArgs e)
        {
            if (_isConnected)
            {
                await DisplayAlert("提示", "已经连接到服务器", "确定");
                return;
            }

            await ConnectToServerAsync();
        }

        private async Task ConnectToServerAsync()
        {
            try
            {
                if (!int.TryParse(ServerPortEntry.Text, out int port) || port < 1 || port > 65535)
                {
                    await DisplayAlert("错误", "请输入有效的端口号 (1-65535)", "确定");
                    return;
                }

                var serverIp = ServerIpEntry.Text;
                if (string.IsNullOrEmpty(serverIp))
                {
                    await DisplayAlert("错误", "请输入服务器IP地址", "确定");
                    return;
                }

                UpdateStatus("连接中...", Colors.Orange);
                AddMessage($"正在连接到 {serverIp}:{port}...", MessageType.Info);

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(serverIp, port);

                _stream = _tcpClient.GetStream();
                _isConnected = true;
                _cancellationTokenSource = new CancellationTokenSource();

                UpdateUIState(true);
                UpdateStatus("已连接", Colors.Green);
                AddMessage($"成功连接到服务器 {serverIp}:{port}", MessageType.Success);

                // 启动接收消息的任务
                _ = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                UpdateStatus("连接失败", Colors.Red);
                AddMessage($"连接失败: {ex.Message}", MessageType.Error);
                await DisplayAlert("连接失败", $"无法连接到服务器: {ex.Message}", "确定");
            }
        }

        private async void OnDisconnectButtonClicked(object sender, EventArgs e)
        {
            DisconnectFromServer();
        }

        private void DisconnectFromServer()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
                _isConnected = false;

                UpdateUIState(false);
                UpdateStatus("未连接", Colors.Red);
                AddMessage("已断开与服务器的连接", MessageType.Info);
            }
            catch (Exception ex)
            {
                AddMessage($"断开连接时发生错误: {ex.Message}", MessageType.Error);
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[4096];

                while (!cancellationToken.IsCancellationRequested && _isConnected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        // 服务器关闭连接
                        break;
                    }

                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    AddMessage($"收到消息: {receivedData}", MessageType.Received);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    AddMessage($"接收消息时发生错误: {ex.Message}", MessageType.Error);
                }
            }
            finally
            {
                if (_isConnected)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DisconnectFromServer();
                    });
                }
            }
        }

        private async void OnSendMessageClicked(object sender, EventArgs e)
        {
            await SendMessageAsync();
        }

        private async Task SendMessageAsync()
        {
            try
            {
                if (!_isConnected)
                {
                    await DisplayAlert("错误", "请先连接到服务器", "确定");
                    return;
                }

                string message = SendMessageEntry.Text;
                if (string.IsNullOrWhiteSpace(message))
                {
                    await DisplayAlert("提示", "请输入要发送的消息", "确定");
                    return;
                }

                byte[] data = Encoding.UTF8.GetBytes(message);
                await _stream.WriteAsync(data, 0, data.Length);

                AddMessage($"发送消息: {message}", MessageType.Sent);
                SendMessageEntry.Text = string.Empty;
            }
            catch (Exception ex)
            {
                AddMessage($"发送消息失败: {ex.Message}", MessageType.Error);
                await DisplayAlert("发送失败", $"无法发送消息: {ex.Message}", "确定");
            }
        }

        private void OnClearMessageClicked(object sender, EventArgs e)
        {
            MessageStack.Children.Clear();
        }

        private void AddMessage(string message, MessageType type)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var frame = new Frame
                {
                    Padding = 10,
                    Margin = 2,
                    CornerRadius = 8,
                    BackgroundColor = GetMessageBackgroundColor(type),
                    HasShadow = false
                };

                var label = new Label
                {
                    Text = message,
                    FontSize = 12,
                    TextColor = GetMessageTextColor(type)
                };

                frame.Content = label;
                MessageStack.Children.Add(frame);

                // 滚动到底部
                var scrollView = MessageStack.Parent.Parent as ScrollView;
                scrollView?.ScrollToAsync(frame, ScrollToPosition.End, true);
            });
        }

        private Color GetMessageBackgroundColor(MessageType type)
        {
            return type switch
            {
                MessageType.Sent => Colors.LightBlue,
                MessageType.Received => Colors.LightGreen,
                MessageType.Error => Colors.LightPink,
                MessageType.Success => Colors.LightCyan,
                _ => Colors.LightGray
            };
        }

        private Color GetMessageTextColor(MessageType type)
        {
            return type switch
            {
                MessageType.Error => Colors.DarkRed,
                MessageType.Success => Colors.DarkGreen,
                _ => Colors.Black
            };
        }

        private void UpdateStatus(string status, Color color)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = status;
                StatusLabel.TextColor = color;
            });
        }

        private void UpdateUIState(bool isConnected)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ConnectButton.IsEnabled = !isConnected;
                DisconnectButton.IsEnabled = isConnected;
                SendMessageEntry.IsEnabled = isConnected;
                ServerIpEntry.IsEnabled = !isConnected;
                ServerPortEntry.IsEnabled = !isConnected;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // 页面关闭时断开连接
            if (_isConnected)
            {
                DisconnectFromServer();
            }
        }
    }

    public enum MessageType
    {
        Sent,
        Received,
        Error,
        Info,
        Success
    }
}
