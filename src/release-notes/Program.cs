using Nullean.Argh;
using ReleaseNotes;

var app = new ArghApp();
app.Map<ReleaseNotesCommands>();

return await app.RunAsync(args);
