# Stellaegis.Blobs

Stellaegis.Blobs is a small .NET library for storing opaque binary content: files, documents,
templates, snapshots. Your code reads and writes blobs through one interface, `IBlobStore`, and
addresses each blob with a `BlobLocation`. The storage backend sits behind the interface and is
chosen by the adapter you register, so your code and the keys it stores do not depend on it.

Two packages are published:

| Package | Content |
| --- | --- |
| `Stellaegis.Blobs` | `IBlobStore` and `BlobLocation` |
| `Stellaegis.Blobs.FileSystem` | an adapter that stores blobs as files under a root directory |

The library targets .NET 10 and is released under the MIT license.

```sh
dotnet add package Stellaegis.Blobs.FileSystem
```

The adapter package depends on `Stellaegis.Blobs`, so this one command installs both.

## Addressing a blob: holder, space and key

A `BlobLocation` has three parts.

- **The holder** is the party the blob belongs to: a customer, an organization, a tenant, whatever
  your application partitions its data by. The empty string, available as `BlobLocation.Platform`,
  designates content that belongs to the application itself rather than to one of its holders, such
  as a default template.
- **The space** (`Name`) groups blobs of one kind: `invoices`, `templates`, `archive`. The library
  defines no space; your application chooses its own names.
- **The key** is the path of the blob within its space, such as `invoice.pdf` or
  `2026/06/08/3f2a.pdf`. Segments are separated by forward slashes.

```csharp
var invoice = new BlobLocation("acme", "invoices", "2026/06/08/3f2a.pdf");
var template = BlobLocation.ForPlatform("templates", "invoice.scriban");

template.IsPlatform; // true
```

Each holder and space pair is isolated from the others: the same key under two holders, or under a
holder and the platform, designates two different blobs.

A location holds no physical information: no provider, endpoint, container or credential. That
information belongs to the adapter. As a result, a stored key stays valid when the backend changes,
and it never carries a secret.

### What a location accepts

The constructor checks each part and throws an `ArgumentException` naming the part at fault when
one is invalid.

| Part | Rule |
| --- | --- |
| holder | `BlobLocation.Platform` (empty), or 3 to 63 characters: a lowercase letter, then lowercase letters, digits and hyphens, with no two consecutive hyphens |
| space | a lowercase letter, then lowercase letters, digits and hyphens |
| key | relative: no leading `/`, no `\`, no empty segment, no `.` or `..` segment |

These rules are the ones an adapter needs to store a blob safely. The holder and space rules keep
every pair on a distinct directory or container name, and the key rule keeps a key from escaping its
space (path traversal). They are not a business rule. If your application constrains its holder
identifiers further, with a format of its own or reserved values, check that rule in your code before
building a location. The library has no way to know it.

## Reading and writing

`IBlobStore` has five methods:

| Method | Behaviour |
| --- | --- |
| `WriteAsync(location, content)` | writes the stream, replacing any blob already at that location |
| `OpenReadAsync(location)` | returns a readable stream, or `null` when no blob exists at that location |
| `ExistsAsync(location)` | tells whether a blob exists at that location |
| `DeleteAsync(location)` | deletes the blob; does nothing when there is none |
| `TryGetDownloadUrlAsync(location, validity)` | returns a short-lived download URL, or `null` when the adapter cannot produce one |

```csharp
await store.WriteAsync(invoice, pdfStream, cancellationToken);

await using var content = await store.OpenReadAsync(invoice, cancellationToken);
if (content is null)
{
    // no blob at this location
}
```

An absent blob and an empty blob are two different cases. An absent blob reads as `null`. An empty
blob, written from a stream of length 0, reads as a stream of length 0. A document with no content
is therefore stored and found again like any other.

`OpenReadAsync` returns `null` rather than throwing because absence is an ordinary answer for a
store. Your code decides what an absent blob means: a missing optional file, or an inconsistency
with metadata that says the blob exists. Errors that are not about absence, such as a denied access
or a full disk, are raised as exceptions by the adapter.

`TryGetDownloadUrlAsync` lets a web application send a browser straight to the storage backend, with
a pre-signed URL, instead of streaming the content through its own process. An adapter that cannot
sign a URL returns `null`, and your code then streams the content with `OpenReadAsync`. The URL is
not checked against the blob's existence: ask for one only for a blob your code has already
authorized and knows to exist.

## The filesystem adapter

`FileSystemBlobStore` stores each blob as a file under a root directory. Each holder and space pair
has its own directory:

```text
{root}/acme_invoices/2026/06/08/3f2a.pdf      holder "acme", space "invoices"
{root}/_platform_templates/invoice.scriban    the platform, space "templates"
```

The underscore separates the holder from the space. Neither a holder nor a space name can contain an
underscore, and a holder starts with a letter, so two locations never share a directory and no holder
can reach the platform's directories.

Register the adapter with `AddStellaegisFileSystemBlobStore`. It registers `FileSystemBlobStore` as
the singleton `IBlobStore`; a second call keeps the first registration.

```csharp
builder.Services.AddStellaegisFileSystemBlobStore(o => o.RootPath = "/var/lib/myapp/blobs");
```

To read the root from configuration, bind the section named by `FileSystemBlobOptions.SectionName`:

```csharp
builder.Services.AddStellaegisFileSystemBlobStore(
    o => builder.Configuration.GetSection(FileSystemBlobOptions.SectionName).Bind(o));
```

```json
{
  "Stellaegis": {
    "Blobs": {
      "FileSystem": { "RootPath": "/var/lib/myapp/blobs" }
    }
  }
}
```

`RootPath` is required: the store throws an `ArgumentException` when it is created without one. A
relative path is resolved against the process's current directory when the store is created.
Directories are created on the first write and are not removed when their last blob is deleted.

The adapter suits development, tests and applications that run on a single machine. It cannot sign
download URLs, so `TryGetDownloadUrlAsync` always returns `null`. It shares the limits of the file
system underneath. On Windows, a key containing `:` is read as a path on another drive, so the
adapter refuses it with an `ArgumentException` before touching the disk, and a key containing another
character Windows refuses, such as `*` or `?`, fails with an `IOException`. On a case-insensitive file
system, two keys that differ only by case designate the same file.

## One storage per holder

Because a location carries the holder and nothing physical, an `IBlobStore` can send each holder to a
storage of its own. This is how a holder can bring its own storage account: your implementation of
`IBlobStore` looks up the storage configured for `location.Holder`, then delegates to an adapter
created for that storage. The keys your application stores do not change when a holder moves from one
storage to another; only the content has to be copied.

The library does not provide this routing. It depends on where your application keeps each holder's
storage settings and credentials, and on how long it keeps an adapter per holder, all of which your
application decides.

## Building and testing

```sh
dotnet test Stellaegis.Blobs.slnx
```

The tests need no external service. The filesystem tests write under the system's temporary
directory and remove what they write.

## License

MIT — see [LICENSE](https://github.com/Stellaegis/Stellaegis.Blobs/blob/main/LICENSE). The packages depend on third-party components listed in
[THIRD-PARTY-NOTICES.md](https://github.com/Stellaegis/Stellaegis.Blobs/blob/main/THIRD-PARTY-NOTICES.md).
