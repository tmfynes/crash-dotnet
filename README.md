# crash-dotnet

ASP.NET Core 8 demo for **homelab1.fynesy.com** Linux Diego cells (`cflinuxfs4` + `dotnet_core_buildpack`).

The home page shows the serving instance (index, GUID, PID) and a button that `FailFast`s that process so Diego marks it crashed and starts a replacement.

```bash
cf target -o hermes -s hermes
cf push -f manifest.yml -p .
```

Route: https://crash-dotnet.homelab1.fynesy.com

Binds `0.0.0.0:$PORT`. Two instances so the route stays up while one cell is replaced.
