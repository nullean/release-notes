using System.Text;
using Octokit;

namespace ReleaseNotes;

public sealed class GitHubItem(Issue issue, IReadOnlyList<int> relatedIssues)
{
	public Issue Issue { get; } = issue;
	public IReadOnlyList<int> RelatedIssues { get; } = relatedIssues;
	public int Number => Issue.Number;
	public IReadOnlyList<Label> Labels => Issue.Labels;

	public string Title
	{
		get
		{
			var builder = new StringBuilder("#").Append(Issue.Number).Append(' ');
			if (Issue.PullRequest is null)
				builder.AppendFormat("[ISSUE] {0}", Issue.Title);
			else
			{
				builder.Append(Issue.Title);
				if (RelatedIssues.Count > 0)
				{
					var related = string.Join(", ", RelatedIssues.Select(i => $"#{i}"));
					var noun = RelatedIssues.Count == 1 ? "issue" : "issues";
					builder.Append($" ({noun}: {related})");
				}
			}

			return builder.ToString();
		}
	}

	public string TitleAsciiDoc(string githubUrl)
	{
		if (Issue.PullRequest is null)
			return $"* {Issue.Title} {githubUrl}issues/{Issue.Number}[#{Issue.Number}]";

		var related = "";
		if (RelatedIssues.Count > 0)
		{
			var links = string.Join(", ", RelatedIssues.Select(i => $"{githubUrl}issues/{i}[#{i}]"));
			var noun = RelatedIssues.Count == 1 ? "issue" : "issues";
			related = $" ({noun}: {links})";
		}

		return $"* {Issue.Title} {githubUrl}pull/{Issue.Number}[#{Issue.Number}] {related}";
	}
}
