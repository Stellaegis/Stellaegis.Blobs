# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] — 2026-10-12

### Changed

- `BlobLocation` accepts only keys that every supported backend stores unchanged: lowercase printable
  ASCII, none of `< > : " | ? *`, no segment ending with a dot or a space, no Windows device name
  (`con`, `nul`, `com1`…, with or without an extension), at most 1,024 characters and 255 per segment.
  Keys such as `Report.pdf`, `a:b` or `nul.txt`, accepted by 1.0.0, are now refused with an
  `ArgumentException`. 1.0.0 had no known consumer when this changed, hence a patch version.
- A space name is at most 63 characters.

## [1.0.0] — 2026-10-12

First release as a standalone library.

### Added

- `IBlobStore`, which writes, reads, checks, deletes blobs and offers a download URL when the adapter
  can sign one. An absent blob reads as `null`; an empty blob reads as a stream of length 0.
- `BlobLocation`, which addresses a blob by holder, space and key, with `BlobLocation.Platform` for
  content that belongs to the application itself. It refuses holders, space names and keys that an
  adapter could not store safely.
- `FileSystemBlobStore`, in `Stellaegis.Blobs.FileSystem`, which stores each holder and space pair in a
  directory of its own under a root, and `AddStellaegisFileSystemBlobStore`, which registers it.

[1.0.1]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v1.0.1
[1.0.0]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v1.0.0
