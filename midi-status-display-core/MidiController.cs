using Commons.Music.Midi;

namespace MidiStatusDisplay.core
{
	public class MidiController : IDisposable
	{
		static IMidiOutput empty_output = MidiAccessManager.Empty
			.OpenOutputAsync(MidiAccessManager.Empty.Outputs.First().Id).Result;

		public IMidiOutput Output { get; private set; } = empty_output;
		public IMidiInput Input { get; private set; }
		public int Channel { get; set; } = 1;
		public int Program { get; private set; } = 0; // grand piano
		public int BankMsb { get; private set; } = 0;
		public int BankLsb { get; private set; } = 0;

		public IMidiAccess MidiAccess => MidiAccessManager.Default;

		public string CurrentDeviceId { get; set; }

		public MidiInstrumentMap CurrentInstrumentMap => MidiInstrumentMapOverride ?? MidiModuleDatabase.Default.Resolve(Output?.Details?.Name)?.Instrument?.Maps?.FirstOrDefault();
		public MidiInstrumentMap CurrentDrumMap => MidiDrumMapOverride ?? MidiModuleDatabase.Default.Resolve(Output?.Details?.Name)?.Instrument?.DrumMaps?.FirstOrDefault();

		public event EventHandler<NoteOnOffEventArgs> NoteOnOffReceived;

		public MidiInstrumentMap MidiInstrumentMapOverride { get; set; }
		public MidiInstrumentMap MidiDrumMapOverride { get; set; }

		MidiMachine machine = new MidiMachine();

		public void Dispose()
		{
			if (Input != null)
				Input.Dispose();
			Input = null;
			if (Output != null)
				Output.Dispose();
			Output = empty_output;
			DisableVirtualOutput();
		}

		void Send(byte[] buffer, int offset, int length, long timestamp)
		{
			Output.Send(buffer, offset, length, timestamp);
			if (virtual_port != null)
				virtual_port.Send(buffer, offset, length, timestamp);
		}

		public void SetupMidiDevices()
		{
			AppDomain.CurrentDomain.DomainUnload += delegate { Dispose(); };

			if (!MidiAccessManager.Default.Outputs.Any())
			{
				Console.WriteLine("No MIDI device was found.");
				Output = empty_output;
			}
			else
				ChangeOutputDevice(MidiAccessManager.Default.Outputs.First().Id);

			EnableVirtualOutput();
		}

		public void ChangeInputDevice(string deviceID)
		{
			if (Input != null)
			{
				Input.Dispose();
				Input = null;
			}

			Input = MidiAccessManager.Default.OpenInputAsync(deviceID).Result;
			Input.MessageReceived += (o, e) =>
				Send(e.Data, e.Start, e.Length, e.Timestamp);
		}

		public void ChangeOutputDevice(string deviceID)
		{
			if (Output != null)
			{
				Output.Dispose();
				Output = empty_output;
			}

			Output = MidiAccessManager.Default.OpenOutputAsync(deviceID).Result;
			Send([(byte)(MidiEvent.Program + Channel), (byte)Program], 0, 2, 0);

			CurrentDeviceId = deviceID;
		}

		public void ChangeProgram(int newProgram, byte bankMsb, byte bankLsb)
		{
			Program = newProgram;
			BankMsb = bankMsb;
			BankLsb = bankLsb;
			Send(new byte[] { (byte)(MidiEvent.CC + Channel), MidiCC.BankSelect, bankMsb }, 0, 3, 0);
			Send(new byte[] { (byte)(MidiEvent.CC + Channel), MidiCC.BankSelectLsb, bankLsb }, 0, 3, 0);
			Send(new byte[] { (byte)(MidiEvent.Program + Channel), (byte)Program }, 0, 2, 0);
		}

		public void NoteOnOff(byte note, byte velocity)
		{
			machine.Channels[Channel].NoteVelocity[note] = velocity;
			Send([(byte)(0x90 + Channel), note, velocity], 0, 3, 0);
			if (NoteOnOffReceived != null)
				NoteOnOffReceived(this, new NoteOnOffEventArgs { Note = note, Velocity = velocity });
		}

		public class NoteOnOffEventArgs
		{
			public int Note { get; set; }
			public int Velocity { get; set; }
		}

		IMidiOutput virtual_port;

		public bool EnableVirtualOutput()
		{
			IMidiAccess2 m2 = MidiAccess as IMidiAccess2;
			if (m2 == null)
				return false;
			var pc = m2.ExtensionManager.GetInstance<MidiPortCreatorExtension>();
			if (pc == null)
				return false;
			virtual_port = pc.CreateVirtualInputSender(new MidiPortCreatorExtension.PortCreatorContext { Manufacturer = "managed-midi project", ApplicationName = "Xmmk", PortName = "Xmmk Input Port" });
			return true;
		}

		public bool DisableVirtualOutput()
		{
			if (virtual_port == null)
				return false;
			virtual_port.CloseAsync();
			virtual_port = null;
			return true;
		}

		public IMidiOutput VirtualPort => virtual_port;
	}
}