using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// serializes a request body is suspect.
///
/// Deserialization turned out to be just as unreliable: the pre-check that's supposed to detect "release
/// already exists" (client.Repository.Release.Get, deserializing Octokit's own Release model the same
/// reflection way) silently failed under Native AOT too, so CreateRelease got called unconditionally on a
/// tag that already had a release and got a 422 "already_exists" conflict from GitHub. Rather than chase
/// which specific property breaks deserialization next, CreateRelease/CreateLabel are written as idempotent
/// upserts: always attempt the write, and treat GitHub's "already exists" response as success (falling back
/// to a minimal, source-generated-only GET to resolve the existing release's id for the update) instead of
/// depending on a separate, unreliable existence check beforehand.
/// </remarks>
internal static class GitHubReleaseClient
{
	private static HttpRequestMessage BuildRequest(HttpMethod method, string owner, string repository, string path, HttpContent? content, string? token)
	{
		var request = new HttpRequestMessage(method, $"https://api.github.com/repos/{owner}/{repository}/{path}") { Content = content };
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ReleaseNotesGenerator", "1.0"));
		request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
		if (token is { Length: > 0 })
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return request;
	}

	private static async Task Send(HttpClient httpClient, HttpRequestMessage request, string errorContext, bool ignoreAlreadyExists = false)
	{
		using var response = await httpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync();
			// Labeler.Create's own existence check (Get-then-Create) is inherently racy against concurrent
			// runs (e.g. a "push to master" build and a tag-triggered release both ensuring the same
			// "next major version" label exists at nearly the same time) - treat GitHub's 422 for a label
			// that already exists as success rather than failing the whole release over harmless bookkeeping.
			if (ignoreAlreadyExists && response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity && responseBody.Contains("already_exists"))
				return;
			throw new InvalidOperationException($"{errorContext}: {(int)response.StatusCode} {response.StatusCode}\n{responseBody}");
		}
	}

	/// <summary>Creates the release for <paramref name="tagName"/>, or updates it in place if it already exists.</summary>
	public static async Task CreateOrUpdateRelease(HttpClient httpClient, string owner, string repository, string tagName, string? body, string? token)
	{
		var content = System.Net.Http.Json.JsonContent.Create(
			new GitHubNewReleaseRequest { TagName = tagName, Body = body },
			GitHubJsonContext.Default.GitHubNewReleaseRequest);
		var request = BuildRequest(HttpMethod.Post, owner, repository, "releases", content, token);
		using var response = await httpClient.SendAsync(request);
		if (response.IsSuccessStatusCode)
			return;

		var responseBody = await response.Content.ReadAsStringAsync();
		if (response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity || !responseBody.Contains("already_exists"))
			throw new InvalidOperationException($"Failed to create GitHub release for tag '{tagName}' on {owner}/{repository}: {(int)response.StatusCode} {response.StatusCode}\n{responseBody}");

		var releaseId = await GetReleaseIdByTag(httpClient, owner, repository, tagName, token)
			?? throw new InvalidOperationException($"GitHub reports release for tag '{tagName}' on {owner}/{repository} already exists, but it couldn't be found by tag to update its body.");
		await UpdateRelease(httpClient, owner, repository, releaseId, body, token);
	}

	private static async Task<long?> GetReleaseIdByTag(HttpClient httpClient, string owner, string repository, string tagName, string? token)
	{
		var request = BuildRequest(HttpMethod.Get, owner, repository, $"releases/tags/{tagName}", content: null, token);
		using var response = await httpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
			return null;

		var release = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubReleaseSummary);
		return release?.Id;
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
		return Send(httpClient, request, $"Failed to create GitHub label '{name}' on {owner}/{repository}", ignoreAlreadyExists: true);
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

/// <summary>Just enough of GitHub's release response to resolve an id from a tag name - deliberately not
/// Octokit's own Release model, to avoid its unreliable-under-AOT deserialization.</summary>
internal sealed class GitHubReleaseSummary
{
	[JsonPropertyName("id")]
	public long Id { get; set; }
}

[JsonSerializable(typeof(GitHubNewReleaseRequest))]
[JsonSerializable(typeof(GitHubReleaseUpdateRequest))]
[JsonSerializable(typeof(GitHubNewLabelRequest))]
[JsonSerializable(typeof(GitHubReleaseSummary))]
internal partial class GitHubJsonContext : JsonSerializerContext;
