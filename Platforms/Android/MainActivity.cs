using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using BluetoothMauiApp;
using Plugin.BLE.Android;

namespace BluetoothMauiApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public BluetoothTool? _bluetoothService;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            //App.bluetoothService= new BluetoothService();
            base.OnCreate(savedInstanceState);
            // 这里可以添加自定义的初始化代码
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            
            if (requestCode == 1001)
            {
                // 蓝牙权限请求结果
                bool allGranted = grantResults.All(result => result == Permission.Granted);
                
                if (allGranted)
                {
                    // 权限已授予 可以进行蓝牙操作
                    System.Diagnostics.Debug.WriteLine("蓝牙权限已授予");
                }
                else
                {
                    // 权限被拒绝
                    System.Diagnostics.Debug.WriteLine("蓝牙权限被拒绝");
                }
            }
        }

        //private void InitializeBluetooth()
        //{
        //    try
        //    {
        //        string macAddress = "6A:A6:06:94:E7:6C"; // 目标设备MAC地址
        //        _bluetoothService = new BluetoothTool();

        //        // 检查权限
        //        if (!_bluetoothService.CheckBluetoothPermissions())
        //        {
        //            _bluetoothService.RequestBluetoothPermissions();
        //            return;
        //        }

        //        // 初始化蓝牙适配器
        //        if (_bluetoothService.Initialize())
        //        {
        //            var success = _bluetoothService.ConnectByMacAddress(macAddress);

        //            if (success)
        //            {
        //                byte[] data = new byte[1];
        //                data[0] = 0xaa;
        //                _bluetoothService.SendData(data);
        //                _bluetoothService.ReadData();
        //            }
        //            else
        //            {
        //                System.Diagnostics.Debug.WriteLine("蓝牙连接失败");
        //            }
        //        }
        //        else
        //        {
        //            System.Diagnostics.Debug.WriteLine("蓝牙未启用");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"蓝牙初始化错误: {ex.Message}");
        //    }
        //}
    }
}
