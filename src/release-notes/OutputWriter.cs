using System.Text;

namespace ReleaseNotes;

public sealed class OutputWriter : IDisposable
{
	private readonly TextWriter _stdout = Console.Out;
	private readonly StringBuilder _sb = new();
	private readonly string? _output;

	public OutputWriter(string? output)
	{
		_output = output;
		if (output is null) return;

		Console.WriteLine();
		Console.WriteLine($"Building {output}");
		Console.WriteLine("-------------------");
	}

	public void EmptyLine()
	{
		_stdout.WriteLine();
		_sb.AppendLine();
	}

	public void WriteLine(string s)
	{
		_stdout.WriteLine(s);
		_sb.AppendLine(s);
	}

	public override string ToString() => _sb.ToString();

	public void Dispose()
	{
		if (_output is not null)
		{
			var fi = new FileInfo(_output);
			Directory.CreateDirectory(fi.DirectoryName!);
			File.WriteAllText(_output, _sb.ToString());
		}
	}
}
