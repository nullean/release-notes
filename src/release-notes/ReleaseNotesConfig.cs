namespace ReleaseNotes;

public enum Format
{
	Markdown,
	AsciiDoc
}

public sealed class GitHubRepository(string owner, string repository)
{
	public string Owner { get; } = owner;
	public string Repository { get; } = repository;
	public string Url => $"https://github.com/{Owner}/{Repository}/";
}

public sealed class ReleaseNotesConfig
{
	public required GitHubRepository GitHub { get; init; }
	public required IReadOnlyDictionary<string, string> Labels { get; init; }
	public bool ApplyLabels { get; init; }
	public string? Token { get; init; }
	public required string Version { get; init; }
	public string? OldVersion { get; init; }
	public required string ReleaseTagFormat { get; init; }
	public required string ReleaseLabelFormat { get; init; }
	public string? BackportLabelFormat { get; init; }
	public required string UncategorizedLabel { get; init; }
	public required string UncategorizedHeader { get; init; }
	public string? Output { get; init; }
	public bool OldVersionOnly { get; init; }
	public required string LabelColor { get; init; }
	public bool GenerateReleaseOnGithub { get; init; }
	public IReadOnlyList<string>? ReleaseBodyFiles { get; init; }
	public string? VersionQuery { get; init; }
	public required Format Format { get; init; }
}
