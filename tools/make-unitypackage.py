#!/usr/bin/env python3
# Copyright (C) 2026 Liquid Glass contributors
# SPDX-License-Identifier: GPL-3.0-only

from __future__ import annotations

import argparse
import gzip
import io
import json
import os
import re
import sys
import tarfile
import zipfile

ASSET_ROOT = "Assets/LiquidGlass"

FOLDER_META = {
    "Assets/LiquidGlass": (
        "7df9ef1f30e27ed408e61c77665960fe",
        """fileFormatVersion: 2
guid: 7df9ef1f30e27ed408e61c77665960fe
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ),
    "Assets/LiquidGlass/Editor": (
        "e14bd0120bef20946a055bf10dc2b53f",
        """fileFormatVersion: 2
guid: e14bd0120bef20946a055bf10dc2b53f
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ),
}

EXCLUDE_SUFFIXES = (".asmdef", ".meta")


def read_guid(meta_path: str) -> str:
    with open(meta_path, "r", encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^guid:\s*([0-9a-fA-F]{32})\s*$", line.strip())
            if m:
                return m.group(1).lower()
    raise SystemExit(f"[x] 找不到 guid：{meta_path}")


IMPORTER_BLOCK = {
    ".cs": """MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".asmdef": """AssemblyDefinitionImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".json": """TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".txt": """TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".md": """TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
}

IMPORTER_KEYS = (
    "MonoImporter:", "DefaultImporter:", "ScriptedImporter:",
    "AssemblyDefinitionImporter:", "TextScriptImporter:", "PluginImporter:",
    "TextureImporter:", "ShaderImporter:",
)


def complete_meta(meta_path: str, ext: str) -> str:
    with open(meta_path, "r", encoding="utf-8") as f:
        text = f.read().lstrip("\ufeff")

    if any(k in text for k in IMPORTER_KEYS):
        return text if text.endswith("\n") else text + "\n"

    guid = read_guid(meta_path)
    block = IMPORTER_BLOCK.get(ext)
    if block is None:
        block = """DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    return f"fileFormatVersion: 2\nguid: {guid}\n{block}"


ENTRY_MODE = 0o777
ENTRY_MTIME = 1700000000


def add_entry(tar: tarfile.TarFile, name: str, data: bytes) -> None:
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mtime = ENTRY_MTIME
    info.mode = ENTRY_MODE
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    tar.addfile(info, io.BytesIO(data))


def add_dir(tar: tarfile.TarFile, name: str) -> None:
    info = tarfile.TarInfo(name)
    info.type = tarfile.DIRTYPE
    info.size = 0
    info.mtime = ENTRY_MTIME
    info.mode = ENTRY_MODE
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    tar.addfile(info)


def add_asset(tar: tarfile.TarFile, guid: str, pathname: str,
              meta_text: str, blob: bytes | None) -> None:
    add_dir(tar, guid)
    if blob is not None:
        add_entry(tar, f"{guid}/asset", blob)
    add_entry(tar, f"{guid}/asset.meta", meta_text.encode("utf-8"))
    add_entry(tar, f"{guid}/pathname", pathname.encode("utf-8"))


def pack_gzip(data: bytes) -> bytes:
    out = bytearray(gzip.compress(data, 9, mtime=0))
    out[3] = 0x08
    out[8] = 0x04
    out[9] = 0x00
    out[10:10] = b"archtemp.tar\x00"
    return bytes(out)


def build_unitypackage(out: str, folders: dict, files: list) -> None:
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w", format=tarfile.USTAR_FORMAT) as tar:
        for pathname, (guid, meta_text) in sorted(folders.items()):
            add_asset(tar, guid, pathname, meta_text, None)

        for guid, pathname, src in files:
            with open(src, "rb") as f:
                blob = f.read()
            meta_text = complete_meta(src + ".meta", os.path.splitext(src)[1].lower())
            add_asset(tar, guid, pathname, meta_text, blob)

    with open(out, "wb") as f:
        f.write(pack_gzip(buf.getvalue()))


def build_zip(out: str, folders: dict, files: list) -> None:
    prefix = "Assets/"
    stamp = (1980, 1, 1, 0, 0, 0)

    def put(z: zipfile.ZipFile, arc: str, data: bytes) -> None:
        info = zipfile.ZipInfo(arc, date_time=stamp)
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o644 << 16
        z.writestr(info, data)

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for pathname, (guid, meta_text) in sorted(folders.items()):
            rel = pathname[len(prefix):]
            put(z, rel + "/", b"")
            put(z, rel + ".meta", meta_text.encode("utf-8"))
        for guid, pathname, src in files:
            rel = pathname[len(prefix):]
            with open(src, "rb") as f:
                put(z, rel, f.read())
            ext = os.path.splitext(src)[1].lower()
            put(z, rel + ".meta", complete_meta(src + ".meta", ext).encode("utf-8"))


def build_tgz(out: str, repo: str, editor_dir: str) -> None:
    def add(tar: tarfile.TarFile, arc: str, data: bytes) -> None:
        info = tarfile.TarInfo(arc)
        info.size = len(data)
        info.mtime = ENTRY_MTIME
        info.mode = 0o644
        tar.addfile(info, io.BytesIO(data))

    def add_folder(tar: tarfile.TarFile, arc: str) -> None:
        info = tarfile.TarInfo(arc.rstrip("/") + "/")
        info.type = tarfile.DIRTYPE
        info.size = 0
        info.mtime = ENTRY_MTIME
        info.mode = 0o755
        tar.addfile(info)

    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w", format=tarfile.USTAR_FORMAT) as tar:
        add_folder(tar, "package")
        for name in ("package.json", "README.md", "README.zh-CN.md",
                     "CHANGELOG.md", "CHANGELOG.zh-CN.md", "LICENSE"):
            p = os.path.join(repo, name)
            if os.path.isfile(p):
                with open(p, "rb") as f:
                    add(tar, f"package/{name}", f.read())

        add_folder(tar, "package/Editor")
        for root, dirs, names in os.walk(editor_dir):
            dirs.sort()
            rel_dir = os.path.relpath(root, editor_dir).replace(os.sep, "/")
            if rel_dir != ".":
                add_folder(tar, f"package/Editor/{rel_dir}")
            for name in sorted(names):
                if name.endswith(".meta"):
                    continue
                src = os.path.join(root, name)
                rel = os.path.relpath(src, editor_dir).replace(os.sep, "/")
                with open(src, "rb") as f:
                    add(tar, f"package/Editor/{rel}", f.read())

    with open(out, "wb") as f:
        f.write(gzip.compress(buf.getvalue(), 9, mtime=0))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=None, help=".unitypackage 输出路径")
    ap.add_argument("--zip", action="store_true", help="同时生成解压即用 zip")
    ap.add_argument("--tgz", action="store_true", help="同时生成 UPM tarball")
    ap.add_argument("--all", action="store_true", help="三种产物全出")
    args = ap.parse_args()

    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    editor_dir = os.path.join(repo, "Editor")
    if not os.path.isdir(editor_dir):
        print("[x] 找不到 Editor/", file=sys.stderr)
        return 1

    with open(os.path.join(repo, "package.json"), "r", encoding="utf-8") as f:
        version = json.load(f)["version"]

    dist = os.path.join(repo, "dist")
    os.makedirs(dist, exist_ok=True)
    out = args.out or os.path.join(dist, f"unity-macos-chrome-{version}.unitypackage")

    folders = dict(FOLDER_META)
    styles_meta = os.path.join(editor_dir, "Styles.meta")
    if os.path.isfile(styles_meta):
        with open(styles_meta, "r", encoding="utf-8") as f:
            folders[f"{ASSET_ROOT}/Editor/Styles"] = (read_guid(styles_meta), f.read())

    files: list[tuple[str, str, str]] = []
    seen: dict[str, str] = {}

    for root, dirs, names in os.walk(editor_dir):
        dirs.sort()
        for name in sorted(names):
            if name.endswith(EXCLUDE_SUFFIXES):
                continue
            src = os.path.join(root, name)
            meta = src + ".meta"
            if not os.path.isfile(meta):
                print(f"[x] 缺 meta，跳过：{src}", file=sys.stderr)
                continue
            rel = os.path.relpath(src, editor_dir).replace(os.sep, "/")
            guid = read_guid(meta)
            pathname = f"{ASSET_ROOT}/Editor/{rel}"

            if guid in seen:
                print(f"[x] guid 重复：{guid}  {seen[guid]} / {pathname}", file=sys.stderr)
                return 1
            seen[guid] = pathname
            files.append((guid, pathname, src))

    build_unitypackage(out, folders, files)
    print(f"[ok] {out}  ({os.path.getsize(out)/1024:.1f} KB)")
    print(f"     文件夹 {len(folders)} 个，脚本/资源 {len(files)} 个")

    if args.zip or args.all:
        zp = os.path.join(dist, f"unity-macos-chrome-{version}.zip")
        build_zip(zp, folders, files)
        print(f"[ok] {zp}  ({os.path.getsize(zp)/1024:.1f} KB)")

    if args.tgz or args.all:
        tg = os.path.join(dist, f"com.liquidglass.macos-chrome-{version}.tgz")
        build_tgz(tg, repo, editor_dir)
        print(f"[ok] {tg}  ({os.path.getsize(tg)/1024:.1f} KB)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
