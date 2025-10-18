using Commons.Music.Midi;

namespace MidiStatusDisplay.core.Devices;

// Launchpad Pro Programmers Reference Guide
// https://fael-downloads-prod.focusrite.com/customer/prod/s3fs-public/downloads/Launchpad%20Pro%20Programmers%20Reference%20Guide%201.01.pdf
// Document number: FFFA001331-02

// All documentation and software for the device can be found here
// https://downloads.novationmusic.com/novation/launchpad-mk2/launchpad-pro

public class NovationLaunchpadPro(string deviceId, Log? log = null) : Device(deviceId, log)
{
	private static readonly byte[] SYSEX_CLEAR = [0xF0, 0x00, 0x20, 0x29, 0x02, 0x10, 0x0E, 0x00, 0xF7];

	/// <summary>
	/// Represent the target device colors.
	/// </summary>
	private readonly byte[] _buffer = new byte[100];
	private ushort _pendingColumn = 0;

	private protected override void OnInputOnMessageReceived(object? sender, MidiReceivedEventArgs e)
	{
		if (e.Data[e.Start] == 0x_00D0) return; // pressure

		if (e.Data.Length - e.Start < 2)
		{
			Log.Info($"MIDI Input: 0x{e.Data[e.Start].ToString("X").PadLeft(2, '0')}");
			return;
		}
		string msg = e.Data[e.Start + 1].ToString("00");
		if (msg[0] == '0' && msg[1] == '0') Log.Debug($"MIDI Input: y0 x0 (0x{e.Data[e.Start].ToString("X").PadLeft(2, '0')})");
		Log.Info($"MIDI Input: y{msg[1]} x{msg[0]} (0x{e.Data[e.Start].ToString("X").PadLeft(2, '0')})");
	}
	
	private protected override void OnDeviceInputConnected()
	{

	}
	
	private protected override void OnDeviceOutputConnected()
	{
		Send([0xF0, 0x00, 0x20, 0x29, 0x02, 0x10, 0x21, 0x00, 0xF7]); // Set to Ableton mode
		Send([0xF0, 0x00, 0x20, 0x29, 0x02, 0x10, 0x2C, 0x00, 0xF7]); // set to Session Layout
		
		// 0xF0 SysEx
		// 0x00 0x20 0x29 manufacturer ID (Novation )
		// 0x02 0x10 Product ID (Launchpad Pro)
		// ...
		// 0xF7 End of SysEx

		Clear();
	}

	private void Clear()
	{
		Send(SYSEX_CLEAR);
		Array.Fill(_buffer, (byte)0);
	}
	
	// reduce memory allocation operations by reusing a fixed buffer
	private readonly byte[] _drawBuffer = [144, 0, 0];
	public override void Draw(byte x, byte y, byte color)
	{
		_drawBuffer[1] = (byte)(x + y * 10);
		_drawBuffer[2] = color;
		_buffer[_drawBuffer[1]] = color;
		Send(_drawBuffer);
	}

	/// <summary>
	/// set multiple pads at once
	/// </summary>
	/// <param name="data">[ [x, y, color], ... ] | [ [x+y*10, color], ... ]</param>
	public void Send(byte [][] data) 
	{
		byte [] sendData = new byte[data.Length * 2 + 8];
		sendData[0] = 0xF0;
		sendData[1] = 0x00;
		sendData[2] = 0x20;
		sendData[3] = 0x29;
		sendData[4] = 0x02;
		sendData[5] = 0x10;
		sendData[6] = 0x0A;
		sendData[^1] = 0xF7;

		if (data[0].Length == 2)
		{
			for (var i = 0; i < data.Length; i++)
			{
				int i2 = i * 2;
				sendData[i2 + 7] = data[i][0];
				sendData[i2 + 8] = data[i][1];
			}
		}
		else
		{
			for (var i = 0; i < data.Length; i++)
			{
				int i2 = i * 2;
				sendData[i2 + 7] = (byte)(data[i][0] + data[i][1]*10);
				sendData[i2 + 8] = data[i][2];
			}
		}

		Send(sendData);
	}
	
	/// <summary>
	/// set multiple pads at once with RGB color
	/// </summary>
	/// <param name="data">[ [x, y, r, g, b], ... ] | [ [x+y*10, r, g, b], ... ]</param>
	/// <remarks>the highest RGB value is not 255 but 99 (0x63)</remarks>
	public void SendRgb(byte [][] data) 
	{
		byte [] sendData = new byte[data.Length * 4 + 8];
		sendData[0] = 0xF0;
		sendData[1] = 0x00;
		sendData[2] = 0x20;
		sendData[3] = 0x29;
		sendData[4] = 0x02;
		sendData[5] = 0x10;
		sendData[6] = 0x0B;
		sendData[^1] = 0xF7;

		if (data[0].Length == 4)
		{
			for (var i = 0; i < data.Length; i++)
			{
				int i2 = i * 4;
				sendData[i2 + 7] = data[i][0]; // x + y*10
				sendData[i2 + 8] = data[i][1]; // red
				sendData[i2 + 9] = data[i][2]; // green
				sendData[i2 + 10] = data[i][3]; // blue
			}
		}
		else
		{
			for (var i = 0; i < data.Length; i++)
			{
				int i2 = i * 4;
				sendData[i2 + 7] = (byte)(data[i][0] + data[i][1]*10);
				sendData[i2 + 8] = data[i][2]; // red
				sendData[i2 + 9] = data[i][3]; // green
				sendData[i2 + 10] = data[i][4]; // blue
			}
		}

		Send(sendData);
	}

	/// <summary>
	/// Set LEDs by column.
	/// </summary>
	/// <param name="colum">Column number (0-9)</param>
	/// <param name="colors">Array of colors (max 10)</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when column number is out of range.</exception>
	/// <exception cref="ArgumentException">Thrown when colors array is empty or exceeds 10 elements.</exception>
	/// <remarks>colors[0] is the bottom LED, colors[9] is the top LED</remarks>
	public void SendColumn(byte colum, byte[] colors)
	{
		if (colum > 9) throw new ArgumentOutOfRangeException(nameof(colum), "Column number must be between 0 and 9.");
		if (colors.Length == 0) throw new ArgumentException("Colors array must not be empty.", nameof(colors));
		if (colors.Length > 10) throw new ArgumentException("Colors array must not contain more than 10 elements.", nameof(colors));
		
		byte [] sendData = new byte[colors.Length + 9];
		sendData[0] = 0xF0;
		sendData[1] = 0x00;
		sendData[2] = 0x20;
		sendData[3] = 0x29;
		sendData[4] = 0x02;
		sendData[5] = 0x10;
		sendData[6] = 0x0C;
		sendData[7] = colum;
		sendData[^1] = 0xF7;
		
		Array.Copy(colors, 0, sendData, 8, colors.Length);
		
		//Log.Info(BitConverter.ToString(sendData));

		Send(sendData);
	}

	// reduce memory allocation operations by reusing a fixed buffer
	private readonly byte[] _fixedColorBuffer = [0xF0, 0x00, 0x20, 0x29, 0x02, 0x10, 0x0C, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xF7];
	
	public void SendColumnFixed(byte colum)
	{
		_fixedColorBuffer[7] = colum;
		for (int i = 0; i < 10; i++)
		{
			_fixedColorBuffer[8 + i] = _buffer[colum + i * 10];
		}
		Send(_fixedColorBuffer);
	}
	
	public void SendRow(byte row)
	{
		row *= 10;
		SendRow(row, [_buffer[row], _buffer[row + 1], _buffer[row + 2], _buffer[row + 3], _buffer[row + 4], _buffer[row + 5], _buffer[row + 6], _buffer[row + 7], _buffer[row + 8], _buffer[row + 9]]);
	}
	
	/// <summary>
	/// Set LEDs by row.
	/// </summary>
	/// <param name="row">Row number (0-9)</param>
	/// <param name="colors">Array of colors (max 10)</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when row number is out of range.</exception>
	/// <exception cref="ArgumentException">Thrown when colors array is empty or exceeds 10 elements.</exception>
	/// <remarks>colors[0] is the left LED, colors[9] is the right LED</remarks>
	public void SendRow(byte row, byte[] colors)
	{
		if (row > 9) throw new ArgumentOutOfRangeException(nameof(row), "Row number must be between 0 and 9.");
		if (colors.Length == 0) throw new ArgumentException("Colors array must not be empty.", nameof(colors));
		if (colors.Length > 10) throw new ArgumentException("Colors array must not contain more than 10 elements.", nameof(colors));
		
		byte [] sendData = new byte[colors.Length * 2 + 9];
		sendData[0] = 0xF0;
		sendData[1] = 0x00;
		sendData[2] = 0x20;
		sendData[3] = 0x29;
		sendData[4] = 0x02;
		sendData[5] = 0x10;
		sendData[6] = 0x0D;
		sendData[7] = row;
		sendData[^1] = 0xF7;
		
		Array.Copy(colors, 0, sendData, 8, colors.Length);

		Send(sendData);
	}

	public override void DrawBarX(byte value, byte x, byte color, bool clear = true)
	{
		if (clear)
		{
			for (int i = 1; i < 9; i++)
			{
				//Send([144, (byte)(x + i*10), (value >= i) ? color : (byte)0]);
				_buffer[(byte)(x + i*10)] = (value >= i) ? color : (byte)0;
			}
		}
		else
		{
			for (int i = 1; i < 9; i++)
			{
				//if (value >= i) Send([144, (byte)(x + i*10), color]);
				if (value >= i) _buffer[(byte)(x + i*10)] = color;
			}
		}
		
		_pendingColumn |= (ushort)(1 << x);
		//SendColumn(x);
	}
	
	//public override void DrawBarX(byte value, byte x, byte color, bool clear = true)
	//{
	//	byte[] data = [0xF0, 0x00, 0x20, 0x29, 0x02, 0x10, 0x0A, (byte)(10+x), 0, (byte)(20+x), 0, (byte)(30+x), 0, (byte)(40+x), 0, (byte)(50+x), 0, (byte)(60+x), 0, (byte)(70+x), 0, (byte)(80+x), 0];
	//	if (clear)
	//	{
	//		int i = 0;
	//		for (; i < 8; i++)
	//		{
	//			if (value >= i)
	//			{
	//				data[i * 2 + 8] = color;
	//			} else break;
	//		}
	//		Send(data, (i-1)*2 + 8);
	//	}
	//	else
	//	{
	//		for (int i = 0; i < 8; i++)
	//		{
	//			data[i * 2 + 8] = (value >= i) ? color : (byte)0;
	//		}
	//		Send(data);
	//	}
	//}
	
	public override void DrawBarX(byte value, byte x, bool clear = true)
	{
		if (clear)
		{
			for (int i = 1; i < 9; i++)
			{
				//Send([144, (byte)(x + i*10), (value >= i) ? (byte)((9-i)*4 + 3) : (byte)0]);
				_buffer[(byte)(x + i*10)] = (value >= i) ? (byte)((9-i)*4 + 3) : (byte)0;
			}
		}
		else
		{
			for (int i = 1; i < 9; i++)
			{
				//if (value >= i) Send([144, (byte)(x + i*10), (byte)((9-i)*4 + 3)]);
				if (value >= i) _buffer[(byte)(x + i*10)] = (byte)((9-i)*4 + 3);
			}
		}
		
		//SendColumn(x);
		_pendingColumn |= (ushort)(1 << x);
	}

	public override void Update()
	{
		for (int i = 0; i < 10; i++)
		{
			if (((_pendingColumn) & (1 << i)) == 0) continue;
			SendColumnFixed((byte)i);
		}
	}

	public override void Dispose()
	{
		if (Output != null && Output.Connection == MidiPortConnectionState.Open)
		{
			Clear();
		}

		base.Dispose();
	}
}