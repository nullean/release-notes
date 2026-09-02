using System.Text.RegularExpressions;
using Octokit;

namespace ReleaseNotes;

public static class GithubScanner
{
	public static Regex IssueNumberRegex(string url)
	{
		var pattern = $@"\s(?:#|{Regex.Escape(url)}issues/)(?<num>\d+)";
		return new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
	}

	// Preserves the upstream (Argu/F#) tool's grouping order exactly: within each label bucket, items are
	// prepended rather than appended, so display order ends up reversed relative to scan order. Kept as-is
	// rather than "fixed" so existing consumers relying on the current output ordering aren't surprised.
	private static Dictionary<string, List<GitHubItem>> GroupByLabel(ReleaseNotesConfig config, IReadOnlyList<GitHubItem> items)
	{
		var dict = new Dictionary<string, List<GitHubItem>>();
		foreach (var item in items)
		{
			var categorized = false;
			foreach (var label in config.Labels)
			{
				if (item.Labels.Any(l => l.Name == label.Key))
				{
					if (dict.TryGetValue(label.Key, out var list))
						list.Insert(0, item);
					else
						dict[label.Key] = [item];

					categorized = true;
				}
			}

			if (!categorized)
			{
				if (dict.TryGetValue(ReleaseNotesOptions.UncategorizedLabel, out var list))
				{
					if (!list.Any(i => i.Number == item.Number))
						list.Insert(0, item);
				}
				else
					dict[ReleaseNotesOptions.UncategorizedLabel] = [item];
			}
		}

		return dict;
	}

	private static List<GitHubItem> FilterByPullRequests(Regex issueNumberRegex, IReadOnlyList<Issue> issues)
	{
		List<int> ExtractRelatedIssues(Issue issue)
		{
			if (issue.Body is null) return [];

			var matches = issueNumberRegex.Matches(issue.Body);
			return matches.Count == 0
				? []
				: matches.Where(m => m.Success).Select(m => int.Parse(m.Groups["num"].Value)).ToList();
		}

		var collectedIssues = new List<GitHubItem>();
		var items = new List<GitHubItem>();

		foreach (var issue in issues)
		{
			if (issue.PullRequest is not null)
			{
				var relatedIssues = ExtractRelatedIssues(issue);
				items.Add(new GitHubItem(issue, relatedIssues));
			}
			else
				collectedIssues.Add(new GitHubItem(issue, []));
		}

		// Remove all issues that are referenced by pull requests.
		foreach (var pullRequest in items)
		foreach (var relatedIssue in pullRequest.RelatedIssues)
			collectedIssues.RemoveAll(i => i.Issue.Number == relatedIssue);

		// Any remaining issues do not have an associated pull request, so add them.
		items.AddRange(collectedIssues);
		return items;
	}

	public static async Task<Dictionary<string, List<GitHubItem>>> GetClosedIssues(ReleaseNotesConfig config, GitHubClient client, string releasedLabel)
	{
		var issueNumberRegex = IssueNumberRegex(config.GitHub.Url);
		var filter = new RepositoryIssueRequest { State = ItemStateFilter.Closed };
		filter.Labels.Add(releasedLabel);

		List<GitHubItem> issues;
		try
		{
			var found = await client.Issue.GetAllForRepository(config.GitHub.Owner, config.GitHub.Repository, filter);
			issues = FilterByPullRequests(issueNumberRegex, found);
		}
		catch
		{
			issues = [];
		}

		return GroupByLabel(config, issues);
	}
}
