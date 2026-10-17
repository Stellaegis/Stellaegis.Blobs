# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] — 2026-10-17

A blob is addressed by a `Space` from Stellaegis.Primitives 3.0.0 and a key: the blob space name is
gone, and the filesystem adapter no longer loses the previous content when a write fails.

### Changed

- `BlobLocation` takes a `Space` and a key: `new BlobLocation(space, key)`. A `Compartment` converts
  to its `Space`, so `new BlobLocation(compartment, key)` builds a location in a compartment, and
  `new BlobLocation(Space.Shared, key)` one in the shared space. A `null` space, including one
  converted from a `null` compartment, throws an `ArgumentNullException`.
- `FileSystemBlobStore` stores a blob at `{root}/{compartment}/{key}`, or `{root}/_shared/{key}` for
  the shared space. The first directory is `Space.ToSegment()`, the name every Stellaegis module
  gives a space in an address.
- Depends on Stellaegis.Primitives 3.0.0. Its API breaks are in Stellaegis.Primitives.AspNetCore,
  which this library does not use.

### Removed

- `BlobLocation.Name`, the blob space. A first key segment does the same within one store, and a
  store per kind of content can sit on a storage of its own: the README describes both.
- `BlobLocation.Shared(name, key)`, `IsShared` and `Compartment`. Use `new BlobLocation(Space.Shared,
  key)`, `Space.IsShared` and `Space.Compartment`.

### Fixed

- `FileSystemBlobStore.WriteAsync` emptied the existing file before copying the new content: a
  concurrent reader could see a truncated blob, and a copy that failed midway lost the previous
  content. The content is now written to a temporary file in the same directory, then renamed over
  the blob. A failed write leaves the previous content in place and deletes its temporary file.

### Updating from 2.0.1

- Replace `new BlobLocation(compartment, name, key)` with `new BlobLocation(compartment,
  $"{name}/{key}")`, and `BlobLocation.Shared(name, key)` with `new BlobLocation(Space.Shared,
  $"{name}/{key}")`. Keeping the former name as the first key segment keeps the files where they
  are with `FileSystemBlobStore`: the path `{root}/{compartment}/{name}/{key}` does not change.
- Replace `location.IsShared` with `location.Space.IsShared`, and `location.Compartment` with
  `location.Space.Compartment`.
- Your project resolves Stellaegis.Primitives 3.0.0 or later. If it references
  Stellaegis.Primitives.AspNetCore, follow that package's changelog for 3.0.0.

## [2.0.1] — 2026-10-14

Brings the dependencies up to date, starting with Stellaegis.Primitives 2.0.0. The API of this library
does not change.

### Changed

- Depends on Stellaegis.Primitives 2.0.0. Its API breaks are in Stellaegis.Primitives.AspNetCore, which
  this library does not use. Its new `Space` type names the shared space `Space.Shared`, the notion
  `BlobLocation.Shared` holds; the README shows how to build a location from a `Space`.
- `IsShared` tells the nullable analysis that `Compartment` is not `null` when it is `false`.

### Updating from 2.0.0

- Your project resolves Stellaegis.Primitives 2.0.0 or later. If it references
  Stellaegis.Primitives.AspNetCore, follow that package's changelog for 2.0.0.

## [2.0.0] — 2026-10-14

A blob now belongs to a `Compartment`, from Stellaegis.Primitives 1.1.0, or to the shared space, and
the store no longer offers download URLs.

### Changed

- `BlobLocation` takes a `Compartment` instead of a holder string: `new BlobLocation(compartment, name,
  key)`, and the `Holder` property becomes `Compartment`. The compartment grammar is the one of
  `Compartment`, which refuses a trailing hyphen that 1.0.1 accepted in a holder. A `null` compartment
  throws an `ArgumentNullException`.
- Content that belongs to no compartment lives in the shared space: `BlobLocation.Shared(name, key)`
  and `IsShared` replace `BlobLocation.Platform`, `ForPlatform` and `IsPlatform`. No compartment can
  designate the shared space, and `IBlobStore` requires every adapter to store it under a physical
  name no compartment can produce.
- `FileSystemBlobStore` nests its directories: a blob is stored under `{root}/{compartment}/{name}`
  instead of `{root}/{holder}_{name}`, and the shared space under `{root}/_shared/{name}` instead of
  `{root}/_platform_{name}`. Everything a compartment holds now sits under one directory.
- Depends on Stellaegis.Primitives 1.1.0 and Microsoft.Extensions 10.0.13.

### Removed

- `IBlobStore.TryGetDownloadUrlAsync`. The store reads and writes; serving content to a user, with
  its file name and type, belongs to the application, which reads it with `OpenReadAsync`.

### Updating from 1.0.1

- Build each location from a `Compartment`: `new BlobLocation(Compartment.Parse("acme"), …)`.
- Replace `BlobLocation.ForPlatform(name, key)` with `BlobLocation.Shared(name, key)`, and
  `IsPlatform` with `IsShared`.
- With `FileSystemBlobStore`, move each `{root}/{holder}_{name}` directory to
  `{root}/{holder}/{name}`, and each `{root}/_platform_{name}` directory to `{root}/_shared/{name}`.
- Where your code used `TryGetDownloadUrlAsync`, stream the content from `OpenReadAsync`.

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

[3.0.0]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v3.0.0
[2.0.1]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v2.0.1
[2.0.0]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v2.0.0
[1.0.1]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v1.0.1
[1.0.0]: https://github.com/Stellaegis/Stellaegis.Blobs/releases/tag/v1.0.0
