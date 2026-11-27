using System;
using System.Runtime.InteropServices;
using KuroganesEcho.Core;

internal class Program
{
    /// <summary>
    /// The main entry point for the application. 
    /// This creates an instance of your game and calls the Run() method 
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    private static void Main(string[] args)
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "pipewire,pulse,alsa,oss,jack,port,null");
        
        using KuroganesEchoGame game = new();
        game.Run();
    }
}