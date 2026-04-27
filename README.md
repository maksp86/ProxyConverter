# ProxyConverter

**Proxy links (share links) converter to Xray/sing-box JSON configs**

A lightweight CLI tool that converts proxy subscription links (`ss://`, `vless://`, `vmess://`, `hysteria://`, `tuic://`, etc.) into ready-to-use Xray/sing-box client configuration files (one outbound + standard inbounds/direct/block).

Designed for easy parsing and batch testing of large numbers of proxy links in Python scripts and automation pipelines.

## Features

- Supports all protocols recognized by `v2rayN`
- Select core type: `--core-type=xray` or `--core-type=singbox`
- Multiple input methods:
  - Positional arguments (one or more links)
  - Line-by-line from file or stdin (`--input-lines`)
  - JSON array of strings (`--input-json`)
- Multiple output methods:
  - Full config
  - Only outbound
- Always outputs **compact minified JSON** in the format:
  ```json
  {
    "link1": { ...full Xray/sing-box config... },
    "link2": null, # if parsing fails
    ...
  }
  ```
- Automatic port incrementing (`--change-ports --start-port 20000`)
- Fully self-contained single-file executables (no .NET runtime required)

## Downloads

Go to [Releases](https://github.com/maksp86/ProxyConverter/releases) and download the artifact for your platform:

| Platform      | Architecture | File                              |
|---------------|--------------|-----------------------------------|
| Windows       | x64          | `ProxyConverter-win-x64.exe`      |
| Linux         | x64          | `ProxyConverter-linux-x64`        |
| Linux         | arm64        | `ProxyConverter-linux-arm64`      |

## Usage

```bash
# Simple usage
ProxyConverter "ss://..." "vless://..."

# Line-by-line from file
ProxyConverter --input-lines proxies.txt --output configs.json

# JSON array input
ProxyConverter --input-json proxies.json --change-ports --start-port 30000

# Via stdin
cat proxies.txt | ProxyConverter --input-lines - --output result.json
```

Full list of options:

```bash
ProxyConverter --help
```

All flags are described in the help output.

## Building from Source

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64
dotnet publish -c Release -r linux-x64 --self-contained true -o publish/linux-x64
dotnet publish -c Release -r linux-arm64 --self-contained true -o publish/linux-arm64
```

