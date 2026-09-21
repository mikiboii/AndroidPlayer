// using Avalonia;
// using System;
//
// namespace Androidplayer;
//
// class Program
// {
//     // Initialization code. Don't use any Avalonia, third-party APIs or any
//     // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
//     // yet and stuff might break.
//     [STAThread]
//     
//     public static void Main(string[] args)
//     {
//         StartupTimer.Mark("Main entered");
//         BuildAvaloniaApp()
//             .StartWithClassicDesktopLifetime(args);
//     }
//
//     // Avalonia configuration, don't remove; also used by visual designer.
//     public static AppBuilder BuildAvaloniaApp()
//         => AppBuilder.Configure<App>()
//             .UsePlatformDetect()
//             .WithInterFont()
//             .With(new Win32PlatformOptions
//             {
//                 RenderingMode = new[]
//                 {
//                     Win32RenderingMode.Software
//                 }
//             })
//             
//             
//             .LogToTrace();
// }






using Avalonia;
using System;

namespace Androidplayer;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTimer.Mark("Main entered");
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // Check if the OS is Windows 7 (Version 6.1)
        var osVersion = Environment.OSVersion.Version;
        if (osVersion.Major == 6 && osVersion.Minor == 1)
        {
            // Windows 7 detected: Force Software rendering
            builder = builder.With(new Win32PlatformOptions
            {
                
                RenderingMode = new[] { Win32RenderingMode.Software }
            });
        
            Console.WriteLine("Running on windows 7 using software for ui");
        }
        
        
        
        // For all other OS versions, the default RenderingMode (AngleEgl, Software) is used.

        return builder;
    }
}