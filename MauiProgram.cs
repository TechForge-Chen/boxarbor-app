using Microsoft.Extensions.Logging;
using System.Text;
using BluetoothMauiApp;

namespace BluetoothMauiApp
{
    public static class MauiProgram
    {
        public static bool[] Device = new bool[110];
        public static int net编号 = 0, 权限类型 = 0, 场景 = 0;
        public static bool 蓝牙连接=false;
        public static string net地址 = "";

        public static MauiApp CreateMauiApp()
        {
            // 注册编码提供程序以支持GB2312、GBK、Big5等编码
            try 
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                System.Diagnostics.Debug.WriteLine("全局编码提供程序注册成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"全局编码提供程序注册失败: {ex.Message}");
            }

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // 设计稿(Figma)字体 Plus Jakarta Sans，四档对应设计稿 400/500/600/700
                    fonts.AddFont("PlusJakartaSans-Regular.ttf", "PJS");
                    fonts.AddFont("PlusJakartaSans-Medium.ttf", "PJSM");
                    fonts.AddFont("PlusJakartaSans-SemiBold.ttf", "PJSSB");
                    fonts.AddFont("PlusJakartaSans-Bold.ttf", "PJSB");
                });
            builder.Services.AddTransient<DataPage>();
            builder.Services.AddTransient<SetPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var  mauiApp = builder.Build();
            //Services = mauiApp.Services;

            return mauiApp;
        }
        // 静态属性用于访问服务提供者
       // public static IServiceProvider Services { get; private set; }
    }
}
