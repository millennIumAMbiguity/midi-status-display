using System.Diagnostics;

namespace MidiStatusDisplay.core;

public abstract class Log
{
	public static Log None { get; } = new NoLog();
	
	[Conditional("DEBUG")]
	public virtual void Debug(string message) { }
	public virtual void Info(string message) { }
	public virtual void Warn(string message) { }
	public virtual void Error(string message) { }
}