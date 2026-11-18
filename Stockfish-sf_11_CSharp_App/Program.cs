using Stockfish_sf_11_CSharp_Library.Protocol;
using Stockfish_sf_11_CSharp_Library.Infrastructure;

Console.WriteLine(EngineDiagnostics.EngineInfo());
using var engine = new UciEngine(Console.WriteLine, Console.Error.WriteLine);
if (args.Length != 0)
{
    engine.ProcessCommand(string.Join(" ", args));
    engine.WaitForSearchCompletion();
}
else
{
    string? line;
    while ((line = Console.ReadLine()) != null && engine.ProcessCommand(line)) { }
}
