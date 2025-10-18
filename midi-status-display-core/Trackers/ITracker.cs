using MidiStatusDisplay.core.Configuration;

namespace MidiStatusDisplay.core.Trackers;

public interface ITracker
{
	public long LastUpdate { get; set; }
	public void Update(Tracker tracker);
	
	public void Display(Device device, Tracker tracker);
	public void Init(Controller controller, Tracker tracker);
	
}