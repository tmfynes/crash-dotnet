using System.Runtime.InteropServices;

var startedAt = DateTime.UtcNow;
var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();

object InstanceInfo() => new
{
    ok = true,
    index = Environment.GetEnvironmentVariable("CF_INSTANCE_INDEX") ?? "n/a",
    guid = Environment.GetEnvironmentVariable("CF_INSTANCE_GUID") ?? "n/a",
    ip = Environment.GetEnvironmentVariable("CF_INSTANCE_IP") ?? "n/a",
    addr = Environment.GetEnvironmentVariable("CF_INSTANCE_ADDR") ?? "n/a",
    machine = Environment.MachineName,
    pid = Environment.ProcessId,
    port,
    os = RuntimeInformation.OSDescription,
    framework = RuntimeInformation.FrameworkDescription,
    startedAt = startedAt.ToString("O"),
    uptimeSeconds = (int)(DateTime.UtcNow - startedAt).TotalSeconds,
    now = DateTime.UtcNow.ToString("O"),
};

app.MapGet("/", () =>
{
    var index = Environment.GetEnvironmentVariable("CF_INSTANCE_INDEX") ?? "n/a";
    var guid = Environment.GetEnvironmentVariable("CF_INSTANCE_GUID") ?? "n/a";
    var ip = Environment.GetEnvironmentVariable("CF_INSTANCE_IP") ?? "n/a";
    var html = $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Crash this Diego instance</title>
  <style>
    :root { color-scheme: dark; }
    body {
      margin: 0; min-height: 100vh;
      font-family: "Segoe UI", system-ui, sans-serif;
      background: #2a1010;
      color: #f6ecec;
      display: grid; place-items: center;
    }
    main {
      max-width: 46rem; padding: 2.2rem;
      background: #3a1818; border-radius: 16px;
      box-shadow: 0 20px 50px rgba(0,0,0,.4);
      border-top: 6px solid #ff5c5c;
    }
    h1 { margin: 0 0 .4rem; font-size: 1.8rem; }
    p { color: #e0c8c8; line-height: 1.45; }
    a { color: #ffb4b4; }
    dl { display: grid; grid-template-columns: 9rem 1fr; gap: .45rem .8rem; margin: 1.2rem 0 1.4rem; }
    dt { color: #ff8a8a; font-size: .82rem; text-transform: uppercase; letter-spacing: .04em; }
    dd { margin: 0; font-family: ui-monospace, Consolas, monospace; word-break: break-all; }
    button {
      appearance: none; border: 0; cursor: pointer;
      background: #ff5c5c; color: #2a1010;
      font-weight: 700; font-size: 1.05rem;
      padding: .85rem 1.3rem; border-radius: 10px;
    }
    button:disabled { opacity: .55; cursor: wait; }
    #status { margin: 1rem 0 0; min-height: 1.4em; font-family: ui-monospace, Consolas, monospace; color: #ffd2a6; }
  </style>
</head>
<body>
  <main>
    <h1>Crash this instance</h1>
    <p>This ASP.NET Core process is one Diego instance. The button calls <code>Environment.FailFast</code> so the platform sees a crash and starts a replacement. Watch index / GUID / PID change after it comes back.</p>
    <dl>
      <dt>CF instance</dt><dd id="idx">{{ index }}</dd>
      <dt>GUID</dt><dd id="guid">{{ guid }}</dd>
      <dt>Instance IP</dt><dd>{{ ip }}</dd>
      <dt>Machine</dt><dd>{{ Environment.MachineName }}</dd>
      <dt>PID</dt><dd id="pid">{{ Environment.ProcessId }}</dd>
      <dt>PORT</dt><dd>{{ port }}</dd>
      <dt>OS</dt><dd>{{ RuntimeInformation.OSDescription }}</dd>
      <dt>Framework</dt><dd>{{ RuntimeInformation.FrameworkDescription }}</dd>
      <dt>Started (UTC)</dt><dd>{{ startedAt.ToString("O") }}</dd>
      <dt>Time (UTC)</dt><dd>{{ DateTime.UtcNow.ToString("O") }}</dd>
    </dl>
    <button id="crash" type="button">Crash the instance serving this page</button>
    <p id="status"></p>
  </main>
  <script>
    const statusEl = document.getElementById('status');
    const btn = document.getElementById('crash');
    async function instance() {
      const r = await fetch('/instance?t=' + Date.now(), { cache: 'no-store' });
      if (!r.ok) throw new Error('http ' + r.status);
      return r.json();
    }
    btn.addEventListener('click', async () => {
      btn.disabled = true;
      let before;
      try { before = await instance(); }
      catch (e) { statusEl.textContent = 'Could not read /instance: ' + e; btn.disabled = false; return; }
      statusEl.textContent = 'Crashing instance ' + before.index + ' pid ' + before.pid + '…';
      try { await fetch('/crash', { method: 'POST', cache: 'no-store' }); }
      catch (_) { /* connection drop is expected */ }
      const start = Date.now();
      const poll = async () => {
        const secs = Math.round((Date.now() - start) / 1000);
        statusEl.textContent = 'Instance gone. Waiting for Diego to replace it… ' + secs + 's';
        try {
          const after = await instance();
          const changed = after.guid !== before.guid || after.pid !== before.pid;
          if (changed) {
            statusEl.textContent = 'Replacement is up: index ' + after.index + ' pid ' + after.pid + '. Reloading.';
            setTimeout(() => location.reload(), 600);
            return;
          }
        } catch (_) { /* still down or in-flight */ }
        setTimeout(poll, 1000);
      };
      setTimeout(poll, 800);
    });
  </script>
</body>
</html>
""";
    return Results.Content(html, "text/html");
});

app.MapGet("/instance", () => Results.Json(InstanceInfo()));
app.MapGet("/healthz", () => Results.Json(InstanceInfo()));

app.MapPost("/crash", () =>
{
    var index = Environment.GetEnvironmentVariable("CF_INSTANCE_INDEX") ?? "n/a";
    var guid = Environment.GetEnvironmentVariable("CF_INSTANCE_GUID") ?? "n/a";
    Console.Error.WriteLine($"DEMO CRASH requested on instance {index} guid={guid} pid={Environment.ProcessId}");
    _ = Task.Run(async () =>
    {
        await Task.Delay(250);
        Environment.FailFast($"demo crash of CF instance {index} ({guid})");
    });
    return Results.Json(new { crashing = true, index, guid, pid = Environment.ProcessId });
});

app.Run();
