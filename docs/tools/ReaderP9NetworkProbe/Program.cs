using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Reader;

if (args.Length != 1 || Directory.Exists(args[0])) throw new ArgumentException("Choose a new isolated data directory.");
var root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
var source = Path.Combine(root, "synthetic.txt"); await File.WriteAllTextAsync(source, "春天的河流缓缓流过。阅读让平凡的日子拥有新的光亮。");
using var db = new DatabaseService(new DataPaths(Path.Combine(root, "data")));
var imported = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default);
if (imported.State != ImportState.Imported) throw new InvalidOperationException("Import failed.");
var info = await db.ReadAsync(c => (c.Find<Book>(imported.BookId), c.Find<EbookProgress>(imported.BookId)));
var start = ReaderContractValidator.ReadPosition(info.Item2.LastPositionJson);
var identity = new ReaderOpenIdentity(info.Item1.Id, info.Item1.BookUid, start.Document, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1);
var documents = new ReaderDocumentService(db, db.Coordinator); await documents.OpenAsync(identity, default);
var sessions = new ReaderSessionService(db, db.Coordinator, documents, new EbookProgressMaintenance(db, db.Coordinator), new(), new());
var opened = DateTimeOffset.Now; var context = await sessions.BeginAsync(identity, start, opened, default);
var range = new DocumentRange(start, start with { Utf16Offset = 10 });
var original = await documents.ExtractAsync(range, default);
var scope = new AiScopeSnapshot(context, null, "网络失败探针选区", original, "");
scope = scope with { ScopeSha256 = AiIntegrity.ScopeHash(scope) };
var settings = new AiSettingsStorage(db.Paths);
await settings.SaveAsync(AiSettingsStorage.Default with { UseMock = false, HasAcknowledgedTextUpload = true, Timeout = TimeSpan.FromSeconds(3) }, default);
var secrets = new SessionSecretStore(); await secrets.WriteApiKeyAsync("synthetic-network-probe-" + Guid.NewGuid().ToString("N"), default);
// Exercise real sockets with a refused loopback proxy, without changing OS routing,
// using a real key, or contacting DeepSeek. The API key is never sent over a socket.
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
using var client = new DeepSeekClient(settings, secrets, new HttpClientHandler { AllowAutoRedirect = false, UseProxy = true, Proxy = new WebProxy($"http://127.0.0.1:{port}") });
var quotes = new QuoteStorage(db, new ReadingService(), sessions, documents, db.Coordinator);
var ai = new ReaderAiService(client, settings, new AiQuoteVerifier(), quotes, sessions, db.Coordinator, documents);
string? code = null;
try { await ai.AnalyzeAsync(scope, AiOperation.Analyze, default); }
catch (AiException error) { code = error.Code; }
if (code != "Offline") throw new InvalidOperationException("Expected classified network failure.");
var after = await documents.ExtractAsync(range, default);
if (after.TextSha256 != original.TextSha256) throw new InvalidOperationException("Local reading changed after network failure.");
await sessions.CloseAsync(new(context, start, range.End, opened, DateTimeOffset.Now, false, ReaderExitReason.Back), default);
await secrets.DeleteApiKeyAsync(default);
var counts = await db.ReadAsync(c => new { quotes = c.Table<Quote>().Count(), sessions = c.Table<EbookReadingSession>().Count(), integrity = c.ExecuteScalar<string>("pragma integrity_check") });
if (counts.quotes != 0 || counts.sessions != 1 || counts.integrity != "ok") throw new InvalidOperationException("Reading close failed after network error.");
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, actualTcpFailure = true, failureCode = code, realDeepSeekCalls = 0, realCredentialsUsed = false, localReadingStillAvailable = true, normalSessionCommitted = true, counts }, new JsonSerializerOptions { WriteIndented = true }));
