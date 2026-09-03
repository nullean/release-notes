using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace ReleaseNotes;

/// <summary>
/// Minimal, source-generated-JSON client for the GitHub REST calls that write data (create/update releases,
/// create labels), bypassing Octokit for those specific calls.
/// </summary>
/// <remarks>
/// Octokit's SimpleJsonSerializer serializes request bodies via raw, unannotated reflection (no
/// [DynamicallyAccessedMembers] anywhere in Octokit/SimpleJson.cs). Under Native AOT, the ILC compiler's
/// reflection analysis decides - per member, based on a whole-program heuristic with no annotations to guide
/// it - which property getters get an invocable reflection stub; it isn't simply "properties with a
/// non-public accessor get dropped". We first hit this on NewRelease.TagName (public get, private set: 422
/// "tag_name" wasn't supplied), then on NewLabel.Name, a perfectly ordinary public get/set property on an
/// unrelated model: 422 "name" wasn't supplied. Since there's no reliable per-property rule to work around,
/// and Octokit ships no JsonSerializerContext or other AOT-safe serialization path, every Octokit call that
/// serializes a request body is suspect. Read-only calls (Get/GetLatest, label/branch lookups) only need
/// deserialization, which hasn't shown this failure mode, so those keep using Octokit's GitHubClient.
/// </remarks>
internal static class GitHubReleaseClient
{
	private static HttpRequestMessage BuildRequest(HttpMethod method, string owner, string repository, string path, HttpContent content, string? token)
	{
		var request = new HttpRequestMessage(method, $"https://api.github.com/repos/{owner}/{repository}/{path}") { Content = content };
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ReleaseNotesGenerator", "1.0"));
		request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
		if (token is { Length: > 0 })
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return request;
	}

	private static async Task Send(HttpClient httpClient, HttpRequestMessage request, string errorContext)
	{
		using var response = await httpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException($"{errorContext}: {(int)response.StatusCode} {response.StatusCode}\n{responseBody}");
		}
	}

	public static Task CreateRelease(HttpClient httpClient, string owner, string repository, string tagName, string? body, string? token)
	{
		var content = System.Net.Http.Json.JsonContent.Create(
			new GitHubNewReleaseRequest { TagName = tagName, Body = body },
			GitHubJsonContext.Default.GitHubNewReleaseRequest);
		var request = BuildRequest(HttpMethod.Post, owner, repository, "releases", content, token);
		return Send(httpClient, request, $"Failed to create GitHub release for tag '{tagName}' on {owner}/{repository}");
	}

	public static Task UpdateRelease(HttpClient httpClient, string owner, string repository, long releaseId, string? body, string? token)
	{
		var content = System.Net.Http.Json.JsonContent.Create(
			new GitHubReleaseUpdateRequest { Body = body },
			GitHubJsonContext.Default.GitHubReleaseUpdateRequest);
		var request = BuildRequest(HttpMethod.Patch, owner, repository, $"releases/{releaseId}", content, token);
		return Send(httpClient, request, $"Failed to update GitHub release {releaseId} on {owner}/{repository}");
	}

	public static Task CreateLabel(HttpClient httpClient, string owner, string repository, string name, string color, string? token)
	{
		var content = System.Net.Http.Json.JsonContent.Create(
			new GitHubNewLabelRequest { Name = name, Color = color },
			GitHubJsonContext.Default.GitHubNewLabelRequest);
		var request = BuildRequest(HttpMethod.Post, owner, repository, "labels", content, token);
		return Send(httpClient, request, $"Failed to create GitHub label '{name}' on {owner}/{repository}");
	}
}

internal sealed class GitHubNewReleaseRequest
{
	[JsonPropertyName("tag_name")]
	public required string TagName { get; set; }

	[JsonPropertyName("body")]
	public string? Body { get; set; }
}

internal sealed class GitHubReleaseUpdateRequest
{
	[JsonPropertyName("body")]
	public string? Body { get; set; }
}

internal sealed class GitHubNewLabelRequest
{
	[JsonPropertyName("name")]
	public required string Name { get; set; }

	[JsonPropertyName("color")]
	public required string Color { get; set; }
}

[JsonSerializable(typeof(GitHubNewReleaseRequest))]
[JsonSerializable(typeof(GitHubReleaseUpdateRequest))]
[JsonSerializable(typeof(GitHubNewLabelRequest))]
internal partial class GitHubJsonContext : JsonSerializerContext;
