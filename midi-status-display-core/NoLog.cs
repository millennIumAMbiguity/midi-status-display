namespace MidiStatusDisplay.core;

internal class NoLog : Log
{
	public override void Debug(string message) { }
	public override void Info(string message) { }
	public override void Warn(string message) { }
	public override void Error(string message) { }
}