using System.Diagnostics.CodeAnalysis;
using Octokit;

namespace ReleaseNotes;

/// <summary>Creates the Octokit client the GitHub reads go through.</summary>
/// <remarks>
/// Octokit reads every response with SimpleJson, by reflection and with no trimming annotations, so the native-AOT
/// build has to keep by hand what that reflection reaches (#47). release-notes.csproj roots the Octokit assembly, which
/// keeps the models' constructors and setters. That is not enough, because SimpleJson also builds types at run time
/// that live outside Octokit or only exist as generic instantiations:
/// <list type="bullet">
/// <item>a JSON array becomes a <see cref="List{T}"/> made with <c>MakeGenericType</c> and its capacity constructor,
/// found by reflection. Nothing calls that constructor for Octokit's models, so without it every list read throws a
/// NullReferenceException. Keeping the open type's constructors keeps them for each reference type T;</item>
/// <item>a <see cref="StringEnum{TEnum}"/> property is made with <c>Activator.CreateInstance(type, string)</c>.
/// <c>StringEnum</c> is a struct, so each instantiation is compiled on its own and needs its own entry. These are the
/// ones the models this tool reads (<see cref="Release"/>, <see cref="Issue"/>, <see cref="Label"/> and
/// <see cref="Branch"/>) reach through their properties. A missing one makes Octokit throw
/// MissingMethodException, which the issue scan swallows into empty release notes: <c>./build.sh smokeaot</c>
/// catches that.</item>
/// </list>
/// The managed builds ignore these attributes.
/// </remarks>
internal static class OctokitClient
{
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<>))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(StringEnum<ItemState>))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(StringEnum<ItemStateReason>))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(StringEnum<LockReason>))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(StringEnum<MergeableState>))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(StringEnum<TeamPrivacy>))]
	public static GitHubClient Create(string? token) =>
		new(new ProductHeaderValue("ReleaseNotesGenerator")) { Credentials = token is { } t ? new Credentials(t) : Credentials.Anonymous };
}
