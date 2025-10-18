using MidiStatusDisplay.core;

namespace MidiStatusDisplay.CL;

public class ConsoleLog : Log
{
	public override void Debug(string message) => Console.WriteLine(message);
	public override void Info(string message) => Console.WriteLine(message);
	public override void Warn(string message) => Console.WriteLine(message);
	public override void Error(string message) => Console.WriteLine(message);
}