using Nullean.Argh;

namespace ReleaseNotes;

internal sealed class ReleaseNotesCommands
{
	private static ReleaseNotesConfig BuildConfig(
		string owner, string repositoryName, ReleaseNotesOptions opts,
		bool applyLabels = false, bool generateReleaseOnGithub = false, bool oldVersionOnly = false,
		string? versionQuery = null, IReadOnlyList<string>? releaseBodyFiles = null) =>
		new()
		{
			GitHub = new GitHubRepository(owner, repositoryName),
			Labels = opts.ToLabelMap(),
			ApplyLabels = applyLabels,
			Token = opts.Token,
			Version = opts.Version,
			OldVersion = opts.OldVersion,
			ReleaseTagFormat = opts.ReleaseTagFormat,
			ReleaseLabelFormat = opts.ReleaseLabelFormat,
			BackportLabelFormat = opts.BackportLabelFormat,
			UncategorizedLabel = ReleaseNotesOptions.UncategorizedLabel,
			UncategorizedHeader = opts.UncategorizedHeader ?? ReleaseNotesOptions.UncategorizedLabel,
			Output = opts.Output,
			OldVersionOnly = oldVersionOnly,
			LabelColor = "e3e3e3",
			GenerateReleaseOnGithub = generateReleaseOnGithub,
			ReleaseBodyFiles = releaseBodyFiles,
			VersionQuery = versionQuery,
			Format = opts.Format
		};

	/// <summary>
	/// Generates release notes for <paramref name="version"/> from closed GitHub issues and PRs, printed to
	/// standard out and optionally written to a file.
	/// </summary>
	/// <param name="owner">GitHub repository owner.</param>
	/// <param name="repositoryName">GitHub repository name.</param>
	public async Task<int> Generate([Argument] string owner, [Argument] string repositoryName, [AsParameters] ReleaseNotesOptions opts) =>
		await ReleaseNotesRunner.Run(BuildConfig(owner, repositoryName, opts));

	/// <summary>Creates the version and backport labels for the next release.</summary>
	/// <param name="owner">GitHub repository owner.</param>
	/// <param name="repositoryName">GitHub repository name.</param>
	[CommandName("apply-labels")]
	public async Task<int> ApplyLabels([Argument] string owner, [Argument] string repositoryName, [AsParameters] ReleaseNotesOptions opts) =>
		await ReleaseNotesRunner.Run(BuildConfig(owner, repositoryName, opts, applyLabels: true));

	/// <summary>Finds and prints the previous release for the given version.</summary>
	/// <param name="owner">GitHub repository owner.</param>
	/// <param name="repositoryName">GitHub repository name.</param>
	[CommandName("find-previous")]
	public async Task<int> FindPrevious([Argument] string owner, [Argument] string repositoryName, [AsParameters] ReleaseNotesOptions opts) =>
		await ReleaseNotesRunner.Run(BuildConfig(owner, repositoryName, opts, oldVersionOnly: true));

	/// <summary>Given a search query, finds the current and the next versions and prints them on separate lines.</summary>
	/// <param name="owner">GitHub repository owner.</param>
	/// <param name="repositoryName">GitHub repository name.</param>
	/// <param name="query">--query, An anchor query: M.N, M.x, or master/main. Finds the current and next patch, minor, or major release respectively.</param>
	[CommandName("current-version")]
	public async Task<int> CurrentVersion([Argument] string owner, [Argument] string repositoryName, [AsParameters] ReleaseNotesOptions opts, string query) =>
		await ReleaseNotesRunner.Run(BuildConfig(owner, repositoryName, opts, versionQuery: query));

	/// <summary>
	/// Makes sure the tag exists as a release on GitHub, and introduces new version labels for the next
	/// major/minor/patch.
	/// </summary>
	/// <param name="owner">GitHub repository owner.</param>
	/// <param name="repositoryName">GitHub repository name.</param>
	/// <param name="body">
	/// --body, Path to a file that will be read and used for the body of the release. May be given more than
	/// once to combine multiple files.
	/// </param>
	[CommandName("create-release")]
	public async Task<int> CreateRelease([Argument] string owner, [Argument] string repositoryName, [AsParameters] ReleaseNotesOptions opts, List<string>? body = null) =>
		await ReleaseNotesRunner.Run(BuildConfig(owner, repositoryName, opts, generateReleaseOnGithub: true, releaseBodyFiles: body));
}
