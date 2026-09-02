namespace ReleaseNotes;

/// <summary>
/// Flags shared by every <see cref="ReleaseNotesCommands"/> command, expanded with <c>[AsParameters]</c>
/// so they don't need repeating on each method signature.
/// </summary>
/// <param name="Version">Version that is being released.</param>
/// <param name="Token">The GitHub token to use. If the issue list is long this may be necessary; defaults to anonymous.</param>
/// <param name="OldVersion">The previous version to generate release notes since. Optional; the tool will find the previous release.</param>
/// <param name="ReleaseTagFormat">The release tag format. VERSION is replaced by the actual version.</param>
/// <param name="ReleaseLabelFormat">The release label format. VERSION is replaced by the actual version.</param>
/// <param name="BackportLabelFormat">The backport label format, e.g. "Backport BRANCH". BRANCH is calculated from the version.</param>
/// <param name="Format">The format in which to print the results: Markdown or AsciiDoc.</param>
/// <param name="UncategorizedHeader">The header to use in the markdown for uncategorized issues/PRs.</param>
/// <param name="Label">
/// Map a GitHub label to a categorization heading, formatted as &lt;label&gt;=&lt;description&gt;. May be given
/// more than once. Defaults to bug=Bug Fixes, enhancement=New Features, documentation=Documentation Improvements.
/// </param>
/// <param name="Output">Write the release notes to a file as well as standard out. VERSION is replaced by the actual version.</param>
public sealed record ReleaseNotesOptions(
	string Version,
	string? Token = null,
	string? OldVersion = null,
	string ReleaseTagFormat = "VERSION",
	string ReleaseLabelFormat = "vVERSION",
	string? BackportLabelFormat = null,
	Format Format = Format.Markdown,
	string? UncategorizedHeader = null,
	List<string>? Label = null,
	string? Output = null)
{
	public const string UncategorizedLabel = "Uncategorized";

	/// <summary>
	/// Parses <see cref="Label"/> (each entry formatted as <c>label=description</c>) into a lookup from GitHub
	/// label to its categorization heading, defaulting to bug/enhancement/documentation when none were given.
	/// </summary>
	public IReadOnlyDictionary<string, string> ToLabelMap()
	{
		var entries = Label is { Count: > 0 }
			? Label.Select(ParseLabel)
			:
			[
				("bug", "Bug Fixes"),
				("enhancement", "New Features"),
				("documentation", "Documentation Improvements")
			];

		var map = new Dictionary<string, string> { [UncategorizedLabel] = UncategorizedHeader ?? UncategorizedLabel };
		foreach (var (label, description) in entries)
			map[label] = description;

		return map;
	}

	private static (string Label, string Description) ParseLabel(string entry)
	{
		var separatorIndex = entry.IndexOf('=');
		if (separatorIndex < 0)
			throw new ArgumentException($"--label '{entry}' is not in the expected <label>=<description> format");

		return (entry[..separatorIndex], entry[(separatorIndex + 1)..]);
	}
}
