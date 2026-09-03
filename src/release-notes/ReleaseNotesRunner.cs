using System.Text;
using System.Text.RegularExpressions;
using NuGet.Versioning;
using Octokit;

namespace ReleaseNotes;

public static class ReleaseNotesRunner
{
	private static async Task<Release?> HasLatestRelease(ReleaseNotesConfig config, GitHubClient client)
	{
		try
		{
			return await client.Repository.Release.GetLatest(config.GitHub.Owner, config.GitHub.Repository);
		}
		catch
		{
			return null;
		}
	}

	private static async Task<Release?> ReleaseExists(ReleaseNotesConfig config, GitHubClient client, string version)
	{
		var releaseTag = Labeler.ReleaseLabel(version, config.ReleaseTagFormat);
		try
		{
			return await client.Repository.Release.Get(config.GitHub.Owner, config.GitHub.Repository, releaseTag);
		}
		catch
		{
			return null;
		}
	}

	private static async Task CreateRelease(ReleaseNotesConfig config, GitHubClient client)
	{
		var files = config.ReleaseBodyFiles?.Select(Path.GetFullPath).ToList() ?? [];
		var unknownFiles = files.Where(f => !File.Exists(f)).ToList();
		if (unknownFiles.Count > 0)
			throw new InvalidOperationException(
				$"The following files were not found and can not be read to include in the release body: {string.Join(", ", unknownFiles)}");

		var body = new StringBuilder();
		foreach (var f in files)
			body.AppendLine(await File.ReadAllTextAsync(f));

		var existing = await ReleaseExists(config, client, config.Version);
		if (existing is not null)
		{
			Console.WriteLine("Found release");
			await client.Repository.Release.Edit(config.GitHub.Owner, config.GitHub.Repository, existing.Id, new ReleaseUpdate { Body = body.ToString() });
		}
		else
		{
			// Not client.Repository.Release.Create() - see GitHubReleaseClient's remarks for why Octokit's
			// own NewRelease serialization silently drops tag_name under Native AOT.
			using var httpClient = new HttpClient();
			await GitHubReleaseClient.CreateRelease(httpClient, config.GitHub.Owner, config.GitHub.Repository, config.Version, body.ToString(), config.Token);
		}
	}

	private static async Task<string?> LocateOldVersion(ReleaseNotesConfig config, GitHubClient client)
	{
		if (config.OldVersion is not null) return config.OldVersion;

		var semVerVersion = NuGetVersion.Parse(config.Version);
		if (await HasLatestRelease(config, client) is null) return null;

		var releases = await client.Repository.Release.GetAll(config.GitHub.Owner, config.GitHub.Repository);

		var foundOldVersion = releases
			.Where(r => NuGetVersion.TryParse(r.TagName, out _))
			.Select(r => NuGetVersion.Parse(r.TagName))
			.Where(v => v < semVerVersion)
			.OrderByDescending(v => v)
			.FirstOrDefault();

		return releases.Count switch
		{
			0 => null,
			1 when foundOldVersion is null => null,
			_ => foundOldVersion?.ToString()
		};
	}

	private static async Task<(NuGetVersion Current, NuGetVersion Next)?> FindCurrentAndNextVersion(ReleaseNotesConfig config, GitHubClient client, string versionQuery)
	{
		var releases = await client.Repository.Release.GetAll(config.GitHub.Owner, config.GitHub.Repository);

		var minVersion = versionQuery switch
		{
			"master" or "main" => NuGetVersion.Parse("0.0.1"),
			var q when Regex.IsMatch(q, @"\d+\.x") => NuGetVersion.Parse(q.Replace(".x", ".0")),
			var q when Regex.IsMatch(q, @"\d+\.\d+") => NuGetVersion.Parse(q + ".0"),
			_ => throw new InvalidOperationException($"{versionQuery} is not a valid version query")
		};

		var prefix = config.ReleaseTagFormat.Replace("VERSION", "");
		var prefixPattern = string.IsNullOrWhiteSpace(prefix) ? "" : $"(?:{Regex.Escape(prefix)})";
		var re = new Regex($@"^{prefixPattern}(\d+\.\d+\.\d+(?:-\w+)?)$");

		bool MatchesQuery(NuGetVersion v) => versionQuery switch
		{
			"master" or "main" => true,
			var q when Regex.IsMatch(q, @"\d+\.x") => v.Major == minVersion.Major,
			var q when Regex.IsMatch(q, @"\d+\.\d+") => v.Major == minVersion.Major && v.Minor == minVersion.Minor,
			_ => false
		};

		var foundOldVersion = releases
			.Select(r => re.Match(r.TagName))
			.Where(m => m.Success)
			.Select(m => m.Groups[1].Value)
			.Where(v => NuGetVersion.TryParse(v, out _))
			.Select(NuGetVersion.Parse)
			.Where(MatchesQuery)
			.OrderByDescending(v => v)
			.FirstOrDefault();

		var v = foundOldVersion ?? minVersion;

		switch (versionQuery)
		{
			case "master" or "main":
				return (v, NuGetVersion.Parse($"{v.Major + 1}.0.0"));
			case var q when Regex.IsMatch(q, @"\d+\.x"):
				return (v, NuGetVersion.Parse($"{v.Major}.{v.Minor + 1}.0"));
			case var q when Regex.IsMatch(q, @"^\d+\.\d+\.0$") || Regex.IsMatch(q, @"^\d+\.\d+$"):
				var bumped = NuGetVersion.Parse($"{v.Major}.{v.Minor}.{v.Patch + 1}");
				var next = await ReleaseExists(config, client, v.ToString()) is not null ? bumped : v;
				return (v, next);
			default:
				throw new InvalidOperationException($"{versionQuery} is not a valid version query");
		}
	}

	private static async Task<string> WriteMarkDownReleaseNotes(ReleaseNotesConfig config, GitHubClient client, string? oldVersion)
	{
		var gitHub = config.GitHub;
		using var writer = new OutputWriter(config.Output);

		// oldVersion can be null if the repository has never had a release.
		if (oldVersion is not null)
		{
			writer.WriteLine($"{gitHub.Url}compare/{oldVersion}...{config.Version}");
			writer.EmptyLine();
		}

		var releasedLabel = Labeler.ReleaseLabel(config.Version, config.ReleaseLabelFormat);
		var closedIssues = await GithubScanner.GetClosedIssues(config, client, releasedLabel);

		foreach (var (label, issues) in closedIssues)
		{
			writer.WriteLine($"## {config.Labels[label]}");
			writer.EmptyLine();

			foreach (var issue in issues)
				writer.WriteLine($"- {issue.Title}");

			writer.EmptyLine();
		}

		writer.WriteLine($"### [View the full list of issues and PRs]({config.GitHub.Url}issues?utf8=%E2%9C%93&q=label%3A{releasedLabel})");

		return writer.ToString();
	}

	private static async Task WriteAsciiDocReleaseNotes(ReleaseNotesConfig config, GitHubClient client, string? oldVersion)
	{
		_ = oldVersion;
		if (config.Output is null) return;

		var d = Directory.CreateDirectory(config.Output);
		FileInfo Path_(string f) => new(Path.Combine(d.FullName, f));

		var cv = NuGetVersion.Parse(config.Version);
		var releaseNotes = Path_("release-notes.asciidoc");
		var generatedNotes = Path_($"release-notes-{cv}-generated.asciidoc");
		var humanNotes = Path_($"release-notes-{cv}-human.asciidoc");

		void WriteHuman()
		{
			using var writer = new OutputWriter(humanNotes.FullName);
			writer.WriteLine("");
		}

		async Task WriteGenerated()
		{
			using var writer = new OutputWriter(generatedNotes.FullName);
			writer.WriteLine("");
			var releasedLabel = Labeler.ReleaseLabel(config.Version, config.ReleaseLabelFormat);

			var groupedClosedIssues = (await GithubScanner.GetClosedIssues(config, client, releasedLabel))
				.OrderBy(kv => kv.Key == ReleaseNotesOptions.UncategorizedLabel ? -1 : kv.Key.Length);

			foreach (var (key, issues) in groupedClosedIssues)
			{
				var header = key.ToLowerInvariant().Replace(" ", "-");
				var value = config.Labels[key];

				writer.WriteLine($"[float]\n[[{header}]]\n=== {value}");
				writer.EmptyLine();

				foreach (var issue in issues)
					writer.WriteLine(issue.TitleAsciiDoc(config.GitHub.Url));

				writer.EmptyLine();
			}

			writer.WriteLine("");
		}

		if (!humanNotes.Exists)
			WriteHuman();

		await WriteGenerated();

		var versionNotes = Path_($"release-notes-{cv}.asciidoc");

		void WriteVersioned()
		{
			using var writer = new OutputWriter(versionNotes.FullName);
			writer.WriteLine($"""
				[float]
				[[release-notes-{cv}]]
				== Release-Notes {cv}

				include::{humanNotes.Name}[]
				include::{generatedNotes.Name}[]

				""");
		}

		WriteVersioned();

		// Write release notes landing page.
		var currentPatchReleases = Enumerable.Range(0, (int)cv.Patch + 1)
			.Reverse()
			.Select(patch => NuGetVersion.Parse($"{cv.Major}.{cv.Minor}.{patch}"))
			.ToList();

		using var indexWriter = new OutputWriter(releaseNotes.FullName);
		indexWriter.WriteLine($"""
			[[release-notes]]
			= Release notes

			[partintro]
			--
			Review important information about {cv.Major}.{cv.Minor}.x releases.

			* <<release-notes-{cv}>>
			--

			""");

		foreach (var v in currentPatchReleases)
		{
			var fileName = $"release-notes-{v}.asciidoc";
			var versionReleaseNotes = Path_(fileName);

			if (versionReleaseNotes.Exists)
				indexWriter.WriteLine($"\n\ninclude::{fileName}[]\n\n");
			else
			{
				var r = await ReleaseExists(config, client, v.ToString());
				indexWriter.WriteLine(r is not null
					? $"""
						[float]
						[[release-notes-{v}]]
						== Release-Notes {v}
						{r.HtmlUrl}[Available on github]

						"""
					: $"""
						[float]
						[[release-notes-{v}]]
						== Release-Notes {v}
						No release notes available

						""");
			}
		}
	}

	private static async Task WriteReleaseNotes(ReleaseNotesConfig config, GitHubClient client, string? oldVersion)
	{
		if (config.Format == Format.Markdown)
			await WriteMarkDownReleaseNotes(config, client, oldVersion);
		else
			await WriteAsciiDocReleaseNotes(config, client, oldVersion);
	}

	public static async Task<int> Run(ReleaseNotesConfig config)
	{
		var client = new GitHubClient(new ProductHeaderValue("ReleaseNotesGenerator"))
		{
			Credentials = config.Token is { } token ? new Credentials(token) : Credentials.Anonymous
		};

		try
		{
			if (config.OldVersionOnly)
			{
				var oldVersion = await LocateOldVersion(config, client);
				Console.WriteLine(oldVersion ?? "");
			}
			else if (config.GenerateReleaseOnGithub)
			{
				var oldVersion = await LocateOldVersion(config, client);
				await CreateRelease(config, client);
				await Labeler.AddNewVersionLabels(config, client);
				await WriteReleaseNotes(config, client, oldVersion);
			}
			else if (config.ApplyLabels)
			{
				await Labeler.AddNewVersionLabels(config, client);
				await Labeler.AddBackportLabels(config, client);
			}
			else if (config.VersionQuery is { } versionQuery)
			{
				if (await FindCurrentAndNextVersion(config, client, versionQuery) is { } found)
				{
					Console.WriteLine(found.Current);
					Console.WriteLine(found.Next);
				}
				else
					Console.WriteLine(config.Version);
			}
			else
			{
				var oldVersion = await LocateOldVersion(config, client);
				await WriteReleaseNotes(config, client, oldVersion);
			}

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex);
			return 1;
		}
	}
}
