using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace ReleaseNotes;

/// <summary>
/// Minimal, source-generated-JSON client for creating a GitHub release directly against the REST API.
/// </summary>
/// <remarks>
/// Octokit.Repository.Release.Create can't be used here: Octokit's SimpleJsonSerializer serializes request
/// bodies via raw, unannotated reflection (no [DynamicallyAccessedMembers] anywhere in Octokit/SimpleJson.cs),
/// and under Native AOT trimming the linker strips getter metadata for any property whose accessors have
/// mixed visibility. NewRelease.TagName is "public get; private set;" - the only such property among every
/// request model this tool sends - so in an AOT-published build tag_name silently vanishes from the outgoing
/// JSON while every other field (all plain public get/set) still serializes fine. GitHub then rejects the
/// request with 422 "tag_name" wasn't supplied, even though the C# call site clearly set it. Everything else
/// this tool does through Octokit (GetLatest/Get, ReleaseUpdate, labels) uses only plain public get/set
/// models, so those aren't affected - only the Create path needs this workaround.
/// </remarks>
internal static class GitHubReleaseClient
{
	public static async Task CreateRelease(HttpClient httpClient, string owner, string repository, string tagName, string? body, string? token)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{owner}/{repository}/releases")
		{
			Content = System.Net.Http.Json.JsonContent.Create(
				new GitHubNewReleaseRequest { TagName = tagName, Body = body },
				GitHubReleaseJsonContext.Default.GitHubNewReleaseRequest)
		};
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ReleaseNotesGenerator", "1.0"));
		request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
		if (token is { Length: > 0 })
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

		using var response = await httpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"Failed to create GitHub release for tag '{tagName}' on {owner}/{repository}: {(int)response.StatusCode} {response.StatusCode}\n{responseBody}");
		}
	}
}

internal sealed class GitHubNewReleaseRequest
{
	[JsonPropertyName("tag_name")]
	public required string TagName { get; set; }

	[JsonPropertyName("body")]
	public string? Body { get; set; }
}

[JsonSerializable(typeof(GitHubNewReleaseRequest))]
internal partial class GitHubReleaseJsonContext : JsonSerializerContext;
