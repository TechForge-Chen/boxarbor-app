using Android.Bluetooth;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using AndroidX.Core.Content;
using Android;
using AndroidX.Core.App;

namespace BluetoothMauiApp;

public class BluetoothTool
{
    private BluetoothAdapter _bluetoothAdapter;
    private BluetoothSocket _socket;
    private BluetoothDevice _device;
    private bool _isConnected = false;
    private Thread? _receiveThread; // 接收数据线程
    public byte[] buffer = new byte[1024];
    
    // 扫描相关
    private List<BluetoothDevice> _discoveredDevices = new List<BluetoothDevice>();
    public event EventHandler<BluetoothDevice> DeviceDiscovered;
    public event EventHandler ScanStarted;
    public event EventHandler ScanCompleted;
    
    // 数据接收事件
    public event EventHandler<byte[]> DataReceived;
    
    // 保持对接收器的强引用 避免被GC回收
    private BluetoothReceiver? _receiver;
    private bool _isScanning = false;
    
    // 扫描超时定时器
    private System.Threading.Timer? _scanTimeoutTimer;

    // UUID配置 - 根据你的设备修改
    private static readonly UUID SERVICE_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

    public bool Initialize()
    {
        // 注册编码提供程序以支持GB2312、GBK、Big5等编码
        try 
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.Diagnostics.Debug.WriteLine("编码提供程序注册成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"编码提供程序注册失败: {ex.Message}");
        }
        
        _bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
        return _bluetoothAdapter != null && _bluetoothAdapter.IsEnabled;
    }

    public bool CheckBluetoothPermissions()
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            
            // 检查必要的蓝牙权限
            var permissions = new[]
            {
                Manifest.Permission.Bluetooth,
                Manifest.Permission.BluetoothAdmin,
                Manifest.Permission.AccessCoarseLocation,
                Manifest.Permission.AccessFineLocation
            };

            // 在Android 12+上添加新的蓝牙权限
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
            {
                permissions = permissions.Concat(new[]
                {
                    Manifest.Permission.BluetoothConnect,
                    Manifest.Permission.BluetoothScan
                }).ToArray();
            }

            foreach (var permission in permissions)
            {
                if (ContextCompat.CheckSelfPermission(context, permission) != Android.Content.PM.Permission.Granted)
                {
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 专门检查扫描权限的方法
    private bool CheckScanPermissions()
    {
        try
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            
            // Android 12+ 需要 BLUETOOTH_SCAN 权限
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
            {
                return ContextCompat.CheckSelfPermission(context, Manifest.Permission.BluetoothScan) == Android.Content.PM.Permission.Granted;
            }
            
            // Android 11 及以下版本检查位置权限
            return ContextCompat.CheckSelfPermission(context, Manifest.Permission.AccessCoarseLocation) == Android.Content.PM.Permission.Granted;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 安全检查是否正在扫描
    private bool IsCurrentlyDiscovering()
    {
        try
        {
            if (!CheckScanPermissions())
            {
                return false;
            }
            
            return _bluetoothAdapter?.IsDiscovering == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void RequestBluetoothPermissions()
    {
        try
        {
            var activity = Platform.CurrentActivity as AndroidX.Fragment.App.FragmentActivity;
            if (activity == null) return;

            var permissions = new List<string>
            {
                Manifest.Permission.Bluetooth,
                Manifest.Permission.BluetoothAdmin,
                Manifest.Permission.AccessCoarseLocation,
                Manifest.Permission.AccessFineLocation
            };

            // 在Android 12+上添加新的蓝牙权限
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
            {
                permissions.Add(Manifest.Permission.BluetoothConnect);
                permissions.Add(Manifest.Permission.BluetoothScan);
            }

            ActivityCompat.RequestPermissions(activity, permissions.ToArray(), 1001);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"请求权限失败: {ex.Message}");
        }
    }

    public bool ConnectByMacAddress(string macAddress)
    {
        try
        {
            _device = _bluetoothAdapter.GetRemoteDevice(macAddress);
            _socket = _device.CreateInsecureRfcommSocketToServiceRecord(SERVICE_UUID);
            _socket.Connect();
            _isConnected = true;
            // 启动接收数据线程
            StartReceiveThread();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"连接失败: {ex.Message}");
            return false;
        }
    }
    // 启动接收数据线程
    private void StartReceiveThread()
    {
        _receiveThread = new Thread(() =>
        {
            while (_isConnected)
            {
                try
                {
                    // 读取串口数据
                    byte[] receivedData = ReadData();
                    
                    // 如果接收到数据 触发事件
                    if (receivedData != null && receivedData.Length > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"BluetoothService收到数据: {receivedData.Length} 字节");
                        DataReceived?.Invoke(this, receivedData);
                        
                        // 同时更新buffer以兼容现有代码
                        buffer = receivedData;
                    }
                    
                    Thread.Sleep(50); // 减少CPU占用
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"接收数据异常: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        });
        _receiveThread.Start();
    }

    public bool SendData(byte[] data)
    {
        try
        {
            if (_socket?.IsConnected == true)
            {
                System.Diagnostics.Debug.WriteLine($"发送数据: {data.Length} 字节 - {BitConverter.ToString(data)}");
                
                // 检查是否为UTF-8编码的中文字符
                string testString = null;
                try
                {
                    testString = Encoding.UTF8.GetString(data);
                    System.Diagnostics.Debug.WriteLine($"发送UTF-8文本: {testString}");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("发送的是二进制数据");
                }
                
                var outputStream = _socket.OutputStream;
                outputStream.Write(data, 0, data.Length);
                outputStream.Flush();
                System.Diagnostics.Debug.WriteLine("数据发送成功");
                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("发送失败: 蓝牙未连接");
                return false;
            }
        }
        catch (Exception ex)
        {
           
            System.Diagnostics.Debug.WriteLine($"发送数据异常: {ex.Message}");
            return false;
        }
    }
    
    // 发送文本数据的便捷方法 支持多种编码
    public void SendText(string text, Encoding encoding = null)
    {
        try
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
                
            byte[] data = encoding.GetBytes(text);
            System.Diagnostics.Debug.WriteLine($"发送文本 ({encoding.EncodingName}): {text}");
            System.Diagnostics.Debug.WriteLine($"转换为字节: {BitConverter.ToString(data)}");
            
            SendData(data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"发送文本异常: {ex.Message}");
        }
    }
    
    // 尝试多种编码解析接收到的数据
    public string TryDecodeReceivedData(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;
            
        System.Diagnostics.Debug.WriteLine($"尝试解码数据: {BitConverter.ToString(data)}");
        
        // 创建编码列表 安全地获取编码
        var encodings = new List<Encoding>();
        
        // 添加基础编码
        encodings.Add(Encoding.UTF8);
        encodings.Add(Encoding.Unicode);
        encodings.Add(Encoding.BigEndianUnicode);
        encodings.Add(Encoding.UTF32);
        encodings.Add(Encoding.ASCII);
        
        // 安全地添加扩展编码
        try 
        { 
            encodings.Add(Encoding.GetEncoding("GB2312")); // 简体中文
        } 
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine($"GB2312 编码不可用: {ex.Message}"); 
        }
        
        try 
        { 
            encodings.Add(Encoding.GetEncoding("GBK")); // 扩展的简体中文
        } 
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine($"GBK 编码不可用: {ex.Message}"); 
        }
        
        try 
        { 
            encodings.Add(Encoding.GetEncoding("Big5")); // 繁体中文
        } 
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine($"Big5 编码不可用: {ex.Message}"); 
        }
        
        foreach (var encoding in encodings)
        {
            try
            {
                string result = encoding.GetString(data).Trim();
                
                // 检查是否包含有效的字符 不是控制字符或乱码 
                bool isValid = !string.IsNullOrWhiteSpace(result) && 
                              !result.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') &&
                              !result.Contains('\uFFFD'); // 替换字符 表示解码失败
                              
                if (isValid)
                {
                    System.Diagnostics.Debug.WriteLine($"成功用 {encoding.EncodingName} 解码: {result}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{encoding.EncodingName} 解码失败: {ex.Message}");
                continue;
            }
        }
        
        // 如果所有编码都失败 返回HEX格式
        string hexResult = BitConverter.ToString(data).Replace("-", " ");
        System.Diagnostics.Debug.WriteLine($"所有编码都失败 返回HEX: {hexResult}");
        return $"[HEX] {hexResult}";
    }
    public byte[] ReadData()
    {
        byte[] buffer = new byte[1024];
        if (_socket?.IsConnected == true)
        {
            var inputStream = _socket.InputStream;
            if (inputStream.IsDataAvailable())
            {
                int len = inputStream.Read(buffer);
                inputStream.Flush();
                byte[] data = new byte[len];
                Array.Copy(buffer, 0, data, 0, len);
                return data;
            }
           
             
        }
        return null;
    }
    public void Disconnect()
    {
        _socket?.Close();
        _socket?.Dispose();
    }

    // 扫描相关方法
    public async Task StartScanAsync()
    {
        System.Diagnostics.Debug.WriteLine("开始蓝牙扫描...");
        
        if (_isScanning)
        {
            System.Diagnostics.Debug.WriteLine("已经在扫描中 停止当前扫描");
            StopScan();
            await Task.Delay(1000); // 等待停止完成
        }
        
        if (_bluetoothAdapter == null || !_bluetoothAdapter.IsEnabled)
        {
            System.Diagnostics.Debug.WriteLine("蓝牙适配器未启用");
            return;
        }

        // 检查权限
        if (!CheckBluetoothPermissions())
        {
            System.Diagnostics.Debug.WriteLine("蓝牙权限检查失败 尝试请求权限");
            RequestBluetoothPermissions();
            
            // 给用户时间授予权限
            await Task.Delay(2000);
            
            // 重新检查权限
            if (!CheckBluetoothPermissions())
            {
                throw new UnauthorizedAccessException("缺少蓝牙权限 请在应用设置中授予权限");
            }
        }

        System.Diagnostics.Debug.WriteLine("权限检查通过 开始扫描...");
        _isScanning = true;

        await Task.Run(() =>
        {
            try
            {
                _discoveredDevices.Clear();
                
                // 获取已配对的设备 需要BLUETOOTH_CONNECT权限 
                try 
                {
                    var pairedDevices = _bluetoothAdapter.BondedDevices;
                    if (pairedDevices != null && pairedDevices.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"找到 {pairedDevices.Count} 个已配对设备");
                        foreach (BluetoothDevice device in pairedDevices)
                        {
                            if (device.Name == "net2"|| device.Name == "JDY-34S-SPP")
                            {
                                _discoveredDevices.Add(device);
                                DeviceDiscovered?.Invoke(this, device);
                                break ;
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("没有找到已配对设备");
                    }
                }
                catch (Java.Lang.SecurityException ex)
                {
                    // 权限不足时忽略已配对设备 继续扫描新设备
                    System.Diagnostics.Debug.WriteLine($"无法获取已配对设备: {ex.Message}");
                }

                // 清理旧的接收器
                if (_receiver != null)
                {
                    try
                    {
                        var oldContext = Platform.CurrentActivity ?? Android.App.Application.Context;
                        oldContext.UnregisterReceiver(_receiver);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"清理旧接收器失败: {ex.Message}");
                    }
                    
                    _receiver.DeviceFound -= OnDeviceFound;
                    _receiver.ScanFinished -= OnScanFinished;
                }
                
                // 开始发现新设备
                // 检查是否正在扫描 如果是则先停止
                if (IsCurrentlyDiscovering())
                {
                    System.Diagnostics.Debug.WriteLine("取消当前扫描...");
                    _bluetoothAdapter.CancelDiscovery();
                    Thread.Sleep(1000); // 等待取消完成
                }
                
                // 创建新的接收器并保持强引用
                _receiver = new BluetoothReceiver();
                _receiver.DeviceFound += OnDeviceFound;
                _receiver.ScanFinished += OnScanFinished;
                
                // 注册广播接收器
                var context = Platform.CurrentActivity ?? Android.App.Application.Context;
                var filter = new IntentFilter(BluetoothDevice.ActionFound);
                filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
                
                System.Diagnostics.Debug.WriteLine("注册广播接收器...");
                context.RegisterReceiver(_receiver, filter);
                
                System.Diagnostics.Debug.WriteLine("启动设备发现...");
                bool discoveryStarted = _bluetoothAdapter.StartDiscovery();
                System.Diagnostics.Debug.WriteLine($"设备发现启动结果: {discoveryStarted}");
                
                if (discoveryStarted)
                {
                    ScanStarted?.Invoke(this, EventArgs.Empty);
                    
                    // 设置15秒超时
                    _scanTimeoutTimer?.Dispose();
                    _scanTimeoutTimer = new System.Threading.Timer(OnScanTimeout, null, 15000, System.Threading.Timeout.Infinite);
                    System.Diagnostics.Debug.WriteLine("已设置15秒扫描超时");
                }
                else
                {
                    _isScanning = false;
                    throw new Exception("无法启动蓝牙设备发现");
                }
            }
            catch (Exception ex)
            {
                _isScanning = false;
                System.Diagnostics.Debug.WriteLine($"扫描异常: {ex.Message}");
                throw new Exception($"蓝牙扫描失败: {ex.Message}");
            }
        });
    }

    private void OnScanTimeout(object state)
    {
        System.Diagnostics.Debug.WriteLine("扫描超时 强制停止");
        StopScan();
        ScanCompleted?.Invoke(this, EventArgs.Empty);
    }

    // 保留同步方法以兼容现有代码
    public void StartScan()
    {
        _ = StartScanAsync();
    }

    public void StopScan()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("停止蓝牙扫描...");
            
            // 停止超时定时器
            _scanTimeoutTimer?.Dispose();
            _scanTimeoutTimer = null;
            
            // 使用安全的IsDiscovering检查
            if (IsCurrentlyDiscovering())
            {
                _bluetoothAdapter.CancelDiscovery();
                System.Diagnostics.Debug.WriteLine("已取消设备发现");
            }
            
            // 清理接收器
            if (_receiver != null)
            {
                try
                {
                    var context = Platform.CurrentActivity ?? Android.App.Application.Context;
                    context.UnregisterReceiver(_receiver);
                    System.Diagnostics.Debug.WriteLine("已注销广播接收器");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"注销接收器失败: {ex.Message}");
                }
                
                _receiver.DeviceFound -= OnDeviceFound;
                _receiver.ScanFinished -= OnScanFinished;
                _receiver = null;
            }
            
            _isScanning = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"停止扫描失败: {ex.Message}");
        }
    }

    public List<BluetoothDevice> GetDiscoveredDevices()
    {
        return new List<BluetoothDevice>(_discoveredDevices);
    }

    private void OnDeviceFound(object sender, BluetoothDevice device)
    {
        System.Diagnostics.Debug.WriteLine($"发现设备: {device.Name ?? "未知"} - {device.Address}");
        
        if (!_discoveredDevices.Any(d => d.Address == device.Address))
        {
            _discoveredDevices.Add(device);
            System.Diagnostics.Debug.WriteLine($"添加新设备到列表: {device.Name ?? "未知"}");
            DeviceDiscovered?.Invoke(this, device);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"设备已存在 跳过: {device.Address}");
        }
    }

    private void OnScanFinished(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"扫描自然完成 共发现 {_discoveredDevices.Count} 个设备");
        
        // 停止超时定时器
        _scanTimeoutTimer?.Dispose();
        _scanTimeoutTimer = null;
        
        _isScanning = false;
        ScanCompleted?.Invoke(this, EventArgs.Empty);
    }
}

// 蓝牙广播接收器
public class BluetoothReceiver : BroadcastReceiver
{
    public event EventHandler<BluetoothDevice> DeviceFound;
    public event EventHandler ScanFinished;

    public override void OnReceive(Context context, Intent intent)
    {
        string action = intent.Action;
        System.Diagnostics.Debug.WriteLine($"接收到广播: {action}");

        if (BluetoothDevice.ActionFound.Equals(action))
        {
            try
            {
                // Android 13+ 使用新的API
                BluetoothDevice device;
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
                {
                    device = intent.GetParcelableExtra(BluetoothDevice.ExtraDevice, Java.Lang.Class.FromType(typeof(BluetoothDevice))) as BluetoothDevice;
                }
                else
                {
                    device = intent.GetParcelableExtra(BluetoothDevice.ExtraDevice) as BluetoothDevice;
                }

                if (device != null)
                {
                    System.Diagnostics.Debug.WriteLine($"广播接收器发现设备: {device.Name ?? "未知"} - {device.Address}");
                    DeviceFound?.Invoke(this, device);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("接收到设备发现广播 但设备为null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理设备发现广播时出错: {ex.Message}");
            }
        }
        else if (BluetoothAdapter.ActionDiscoveryFinished.Equals(action))
        {
            System.Diagnostics.Debug.WriteLine("设备发现完成");
            ScanFinished?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"未处理的广播: {action}");
        }
    }
}
