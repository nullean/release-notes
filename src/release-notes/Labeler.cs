using NuGet.Versioning;
using Octokit;

namespace ReleaseNotes;

public static class Labeler
{
	public static string ReleaseLabel(string version, string format) => format.Replace("VERSION", version);
	public static string BackportLabel(string branch, string format) => format.Replace("BRANCH", branch);

	private static async Task Create(ReleaseNotesConfig config, GitHubClient client, string label)
	{
		try
		{
			await client.Issue.Labels.Get(config.GitHub.Owner, config.GitHub.Repository, label);
			return;
		}
		catch
		{
			// Label does not exist yet; fall through and create it.
		}

		// Not client.Issue.Labels.Create() - see GitHubReleaseClient's remarks for why Octokit's own request
		// serialization is unreliable under Native AOT.
		using var httpClient = new HttpClient();
		await GitHubReleaseClient.CreateLabel(httpClient, config.GitHub.Owner, config.GitHub.Repository, label, config.LabelColor, config.Token);
	}

	private static async Task<Branch?> ExistsBranch(ReleaseNotesConfig config, GitHubClient client, string branch)
	{
		try
		{
			return await client.Repository.Branch.Get(config.GitHub.Owner, config.GitHub.Repository, branch);
		}
		catch
		{
			return null;
		}
	}

	public static async Task AddNewVersionLabels(ReleaseNotesConfig config, GitHubClient client)
	{
		var v = NuGetVersion.Parse(config.Version);
		var newMajor = $"{v.Major + 1}.0.0";
		var newMinor = $"{v.Major}.{v.Minor + 1}.0";
		var newPatch = $"{v.Major}.{v.Minor}.{v.Patch + 1}";

		await Create(config, client, ReleaseLabel(newMajor, config.ReleaseLabelFormat));
		await Create(config, client, ReleaseLabel(newMinor, config.ReleaseLabelFormat));
		await Create(config, client, ReleaseLabel(newPatch, config.ReleaseLabelFormat));
	}

	public static async Task AddBackportLabels(ReleaseNotesConfig config, GitHubClient client)
	{
		if (config.BackportLabelFormat is not { } backportLabelFormat)
		{
			Console.WriteLine("No backport label format given, skipping creation of backport labels");
			return;
		}

		var mainExists = await ExistsBranch(config, client, "main");
		var masterExists = await ExistsBranch(config, client, "master");
		switch (mainExists, masterExists)
		{
			case (not null, not null):
				await Create(config, client, BackportLabel("main", backportLabelFormat));
				await Create(config, client, BackportLabel("master", backportLabelFormat));
				break;
			case (not null, null):
				await Create(config, client, BackportLabel("main", backportLabelFormat));
				break;
			case (null, not null):
				await Create(config, client, BackportLabel("master", backportLabelFormat));
				break;
			default:
				Console.WriteLine("Repository does not have either main or master branch");
				break;
		}

		var v = NuGetVersion.Parse(config.Version);
		string[] backportBranches =
		[
			$"{v.Major}.x",
			$"{v.Major}.{v.Minor}",
			$"{v.Major + 1}.x",
			$"{v.Major}.{v.Minor + 1}"
		];

		foreach (var branch in backportBranches)
		{
			if (await ExistsBranch(config, client, branch) is not null)
				await Create(config, client, BackportLabel(branch, backportLabelFormat));
			else
				Console.WriteLine($"branch {branch} does not exist yet so skipping creating a backport label");
		}
	}
}
