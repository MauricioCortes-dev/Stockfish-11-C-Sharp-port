using System;
using System.Runtime.InteropServices;

namespace Stockfish_sf_11_CSharp_Library.Infrastructure
{
    /// <summary>Reports the managed engine's actual identity and runtime, without claiming native ISA optimizations.</summary>
    public static class EngineDiagnostics
    {
        public static string EngineInfo(bool toUci=false) => PortInfo.EngineName+" C# port"
            +(Environment.Is64BitProcess?" 64":"")+(toUci?"\nid author ":" by ")
            +"T. Romstad, M. Costalba, J. Kiiski, G. Linscott; C# port contributors";
        public static string CompilerInfo() => "\nManaged C# 8 / .NET Standard 2.1 engine\nRuntime: "
            +RuntimeInformation.FrameworkDescription+"\nPlatform: "+RuntimeInformation.OSDescription
            +"\nProcess architecture: "+RuntimeInformation.ProcessArchitecture+"\n";
    }
}
