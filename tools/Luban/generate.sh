#!/usr/bin/env bash
# 用仓库锁定的 Luban CLI 生成表。参数原样传给 Luban.dll。
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
lock="$root/third_party/Luban/luban.lock"

if [[ ! -f "$lock" ]]; then
  echo "找不到 $lock" >&2
  exit 1
fi

version=""
archive_name=""
expected=""
while IFS= read -r line || [[ -n "$line" ]]; do
  case "$line" in
    ''|\#*) continue ;;
  esac
  key="${line%%=*}"
  value="${line#*=}"
  case "$key" in
    version) version="$value" ;;
    archive) archive_name="$value" ;;
    sha256) expected="$value" ;;
  esac
done < "$lock"

if [[ -z "$version" || -z "$archive_name" || -z "$expected" ]]; then
  echo "luban.lock 缺少 version、archive 或 sha256。" >&2
  exit 1
fi

archive="$root/third_party/Luban/$archive_name"
if [[ ! -f "$archive" ]]; then
  echo "缺少 $archive" >&2
  exit 1
fi

# sha256sum 是 GNU coreutils。macOS 默认没有，用 shasum -a 256。两者都取第一列。
if command -v sha256sum >/dev/null 2>&1; then
  actual="$(sha256sum "$archive" | awk '{print $1}')"
elif command -v shasum >/dev/null 2>&1; then
  actual="$(shasum -a 256 "$archive" | awk '{print $1}')"
else
  echo "需要 sha256sum 或 shasum 才能核对 Luban 压缩包。" >&2
  exit 1
fi
if [[ -z "$actual" || "$actual" != "$expected" ]]; then
  echo "Luban 压缩包校验失败：期望 $expected，实际 ${actual:-<空>}" >&2
  exit 1
fi

extract="$root/third_party/Luban/.extracted"
stamp="$extract/VERSION"
dll="$extract/Luban/Luban.dll"
if [[ ! -f "$dll" || ! -f "$stamp" || "$(cat "$stamp")" != "$version" ]]; then
  if ! command -v 7z >/dev/null 2>&1; then
    echo "需要 7z 才能解压 Luban CLI（Debian/Ubuntu: p7zip-full）。" >&2
    exit 1
  fi
  rm -rf "$extract"
  mkdir -p "$extract"
  7z x -y "-o${extract}" "$archive" >/dev/null
  printf '%s\n' "$version" > "$stamp"
fi

if [[ ! -f "$dll" ]]; then
  echo "解压后找不到 $dll" >&2
  exit 1
fi

if [[ $# -eq 0 ]]; then
  echo "SlimFramework Luban CLI ${version}" >&2
  echo "用法: tools/Luban/generate.sh -t client -c cs-bin -d bin --conf <luban.conf> -x outputCodeDir=<代码目录> -x outputDataDir=<数据目录>" >&2
  echo "不要把输出目录指到 src/SlimFramework。" >&2
  exit 2
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "需要 dotnet 才能运行 Luban.dll。" >&2
  exit 1
fi

exec dotnet "$dll" "$@"
